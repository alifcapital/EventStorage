using System.Diagnostics;
using System.Text.Json;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Inbox.EventArgs;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Providers;
using EventStorage.Inbox.Repositories;
using EventStorage.Instrumentation.Trace;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox;

internal class InboxEventsExecutor : IInboxEventsExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxEventsExecutor> _logger;
    private readonly InboxOrOutboxStructure _settings;

    /// <summary>
    /// The event to be executed before executing the handler of the inbox event.
    /// </summary>
    public static event EventHandler<InboxEventArgs> ExecutingInboxEvent;

    /// <summary>
    /// The event to be executed before disposing the inbox event handler scope.
    /// </summary>
    public static event EventHandler<EventHandlerArgs> DisposingEventHandlerScope;

    private readonly Dictionary<string, List<EventHandlerInformation>> _receivers;

    private const string HandleMethodName = nameof(IEventHandler<IInboxEvent>.HandleAsync);

    private static readonly Type HasHeadersType = typeof(IHasHeaders);
    private static readonly Type HasAdditionalDataType = typeof(IHasAdditionalData);
    private readonly SemaphoreSlim _singleExecutionLock = new(1, 1);
    private readonly SemaphoreSlim _semaphore;

    public InboxEventsExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<InboxEventsExecutor>>();
        _settings = _serviceProvider.GetRequiredService<InboxAndOutboxSettings>().Inbox;
        _receivers = new Dictionary<string, List<EventHandlerInformation>>();
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrency);
    }

    /// <summary>
    /// Registers a handler 
    /// </summary>
    /// <param name="typeOfHandlerEvent">Event type which we want to use to receive</param>
    /// <param name="typeOfEventHandler">Handler type of the event which we want to handler event</param>
    /// <param name="providerType">Provider type of received event</param>
    public void AddHandler(Type typeOfHandlerEvent, Type typeOfEventHandler, EventProviderType providerType)
    {
        var receiverKey = GetHandlerKey(typeOfHandlerEvent.Name, providerType.ToString());
        if (!_receivers.TryGetValue(receiverKey, out var handlersInformation))
        {
            handlersInformation = [];
            _receivers.Add(receiverKey, handlersInformation);
        }

        var hasHeaders = HasHeadersType.IsAssignableFrom(typeOfHandlerEvent);
        var hasAdditionalData = HasAdditionalDataType.IsAssignableFrom(typeOfHandlerEvent);
        var handleMethod = typeOfEventHandler.GetMethod(HandleMethodName);

        var receiverInformation = new EventHandlerInformation
        {
            EventType = typeOfHandlerEvent,
            EventHandlerType = typeOfEventHandler,
            HandleMethod = handleMethod,
            ProviderType = providerType,
            HasHeaders = hasHeaders,
            HasAdditionalData = hasAdditionalData
        };
        handlersInformation.Add(receiverInformation);
    }

    /// <summary>
    /// The method to execute unprocessed events. We are locking the logic to prevent re-entry into the method while processing is ongoing.
    /// </summary>
    public async Task ExecuteUnprocessedEvents(CancellationToken stoppingToken)
    {
        await _singleExecutionLock.WaitAsync(stoppingToken);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();
            var eventsToHandle = await repository.GetUnprocessedEventsAsync();
            if (eventsToHandle.Length == 0)
                return;

            stoppingToken.ThrowIfCancellationRequested();
            using var activity = CreateActivityForExecutingUnprocessedEventsIfEnabled(eventsToHandle.Length);

            var tasks = eventsToHandle.Select(async eventToReceive =>
            {
                await _semaphore.WaitAsync(stoppingToken);
                try
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await ExecuteEventHandlers(eventToReceive, _serviceProvider, activity);
                }
                catch
                {
                    eventToReceive.Failed(_settings.TryCount, _settings.TryAfterMinutes);
                }
                finally
                {
                    _semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            await repository.UpdateEventsAsync(eventsToHandle);
        }
        finally
        {
            _singleExecutionLock.Release();
        }
    }

    private async Task ExecuteEventHandlers(IInboxMessage inboxMessage, IServiceProvider serviceProvider,
        Activity parentActivity)
    {
        try
        {
            var receiverKey = GetHandlerKey(inboxMessage.EventName, inboxMessage.Provider);
            if (_receivers.TryGetValue(receiverKey, out var inboxEventsInformation))
            {
                using var activity = CreateActivityForExecutingHandlersIfEnabled(inboxMessage, parentActivity);

                // Create a new scope to execute the receiver services of the event as a scoped service
                // because each event's handlers must be executed in a separate scope to avoid conflicts in scoped services like DbContext.
                using var serviceScope = serviceProvider.CreateScope();
                var isOnExecutingEventInvoked = false;

                foreach (var inboxEventInformation in inboxEventsInformation)
                {
                    var inboxEvent = LoadInboxEvent(inboxMessage, inboxEventInformation);
                    if (!isOnExecutingEventInvoked)
                    {
                        OnExecutingInboxEvent(inboxEvent, inboxEventInformation.ProviderType,
                            serviceScope.ServiceProvider);
                        isOnExecutingEventInvoked = true;
                    }

                    var eventReceiver =
                        serviceScope.ServiceProvider.GetRequiredService(inboxEventInformation.EventHandlerType);
                    await ((Task)inboxEventInformation.HandleMethod.Invoke(eventReceiver,
                        [inboxEvent]))!;
                }

                OnEndingInboxEvent(inboxMessage, serviceScope.ServiceProvider);

                inboxMessage.Processed();

                return;
            }

            MarkEventAsFailedWhenThereIsNoPublisher();
        }
        catch (Exception e)
        {
            var exception =
                new EventStoreException(e, $"Error while executing handler of inbox event with ID: {inboxMessage.Id}");
            _logger.LogError(exception, exception.Message);
            throw exception;
        }

        return;

        void MarkEventAsFailedWhenThereIsNoPublisher()
        {
            inboxMessage.Failed(0, _settings.TryAfterMinutesIfEventNotFound);
            _logger.LogError(
                "No event handler configured for the {EventType} type name of inbox event with id {EventId} which is {ProviderType} provider(s).",
                inboxMessage.EventName, inboxMessage.Id, inboxMessage.Provider);
        }
    }

    #region Helper methods

    /// <summary>
    /// Invokes the ExecutingReceivedEvent event to be able to execute the event before the handler.
    /// </summary>
    /// <param name="event">Executing an event</param>
    /// <param name="providerType">The provider type of event</param>
    /// <param name="serviceProvider">The IServiceProvider used to resolve dependencies from the scope.</param>
    private void OnExecutingInboxEvent(IInboxEvent @event, EventProviderType providerType,
        IServiceProvider serviceProvider)
    {
        if (ExecutingInboxEvent is null)
            return;

        var eventArgs = new InboxEventArgs
        {
            Event = @event,
            ProviderType = providerType,
            ServiceProvider = serviceProvider
        };
        ExecutingInboxEvent.Invoke(this, eventArgs);
    }

    /// <summary>
    /// Invokes the DisposingEventHandlerScope event to be able to execute the event after the handler.
    /// </summary>
    /// <param name="message">Information of the inbox event</param>
    /// <param name="serviceProvider">The IServiceProvider used to resolve dependencies from the scope.</param>
    private void OnEndingInboxEvent(IInboxMessage message, IServiceProvider serviceProvider)
    {
        if (DisposingEventHandlerScope is null)
            return;

        var eventArgs = new EventHandlerArgs
        {
            EventName = message.EventName,
            EventProviderType = message.Provider,
            ServiceProvider = serviceProvider
        };
        DisposingEventHandlerScope.Invoke(this, eventArgs);
    }

    /// <summary>
    /// Get the key of the receiver by event name and provider name.
    /// </summary>
    /// <param name="eventName">The name of event type</param>
    /// <param name="providerName">The name of event provider type</param>
    /// <returns>Based on the event name and provider name, it returns a unique key for the receiver.</returns>
    internal static string GetHandlerKey(string eventName, string providerName)
    {
        return $"{eventName}_{providerName}";
    }

    /// <summary>
    /// Load the received event from the inbox event.
    /// </summary>
    /// <param name="message">The event of Inbox</param>
    /// <param name="eventHandlerInformation">The event receivers information</param>
    /// <returns>Loaded instance of event</returns>
    private static IInboxEvent LoadInboxEvent(IInboxMessage message, EventHandlerInformation eventHandlerInformation)
    {
        var jsonSerializerSetting = message.GetJsonSerializer();
        var inboxEvent =
            JsonSerializer.Deserialize(message.Payload, eventHandlerInformation.EventType, jsonSerializerSetting) as
                IInboxEvent;
        if (eventHandlerInformation.HasHeaders && message.Headers is not null)
            ((IHasHeaders)inboxEvent).Headers =
                JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers);

        if (eventHandlerInformation.HasAdditionalData && message.AdditionalData is not null)
            ((IHasAdditionalData)inboxEvent).AdditionalData =
                JsonSerializer.Deserialize<Dictionary<string, string>>(message!.AdditionalData);
        return inboxEvent;
    }

    /// <summary>
    /// Creates an activity for executing publishers of the outbox event if tracing is enabled.
    /// </summary>
    /// <param name="inboxMessage">The outbox message for which the activity is created.</param>
    /// <param name="parentActivity">The parent activity to link to, if available.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateActivityForExecutingHandlersIfEnabled(IInboxMessage inboxMessage, Activity parentActivity)
    {
        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        var traceName =
            $"{EventStorageTraceInstrumentation.InboxEventTag}: Executing a publisher(s) of the {inboxMessage.EventName} event";
        var traceParentId = parentActivity?.Id;
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, traceParentId, spanType: EventStorageTraceInstrumentation.InboxEventTag);
        activity?.SetTag(EventStorageTraceInstrumentation.EventIdTag, inboxMessage.Id);

        return activity;
    }

    /// <summary>
    /// Creates an activity for executing publishers of the unprocessed events if tracing is enabled.
    /// </summary>
    /// <param name="eventsCount">The count of unprocessed events being executed.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateActivityForExecutingUnprocessedEventsIfEnabled(int eventsCount)
    {
        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        var traceName =
            $"{EventStorageTraceInstrumentation.InboxEventTag}: Executing {eventsCount} unprocessed event(s)";
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, spanType: EventStorageTraceInstrumentation.InboxEventTag);

        return activity;
    }

    #endregion
}