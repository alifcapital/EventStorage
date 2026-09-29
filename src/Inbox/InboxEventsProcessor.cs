using System.Diagnostics;
using System.Text.Json;
using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Inbox.EventArgs;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Providers;
using EventStorage.Inbox.Repositories;
using EventStorage.Instrumentation;
using EventStorage.Instrumentation.Trace;
using EventStorage.Management.Models;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox;

internal class InboxEventsProcessor : IInboxEventsProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxEventsProcessor> _logger;
    private readonly InboxOrOutboxStructure _settings;
    private readonly IDistributedLockProvider _lockProvider;

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

    public InboxEventsProcessor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<InboxEventsProcessor>>();
        _lockProvider = _serviceProvider.GetRequiredKeyedService<IDistributedLockProvider>(FunctionalityNames.Inbox);
        _settings = _serviceProvider.GetRequiredService<InboxAndOutboxSettings>().Inbox;
        _receivers = new Dictionary<string, List<EventHandlerInformation>>();
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrency);
    }

    #region AddHandler

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

    #endregion

    #region ExecuteUnprocessedEvents
    
    /// <summary>
    /// The method to execute unprocessed events. We are locking the logic to prevent re-entry into the method while processing is ongoing.
    /// </summary>
    public async Task ExecuteUnprocessedEvents(CancellationToken stoppingToken)
    {
        await _singleExecutionLock.WaitAsync(stoppingToken);
        try
        {
            InboxMessage[] eventsToHandle;
            using (var scope = _serviceProvider.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();
                eventsToHandle = await repository.GetUnprocessedEventsAsync(_settings.MaxEventsToFetch);
            }

            if (eventsToHandle.Length == 0)
                return;

            stoppingToken.ThrowIfCancellationRequested();
            using var activity = CreateActivityForExecutingUnprocessedEventsIfEnabled(eventsToHandle.Length);

            var tasks = eventsToHandle.Select(async eventToReceive =>
            {
                await _semaphore.WaitAsync(stoppingToken);
                try
                {
                    await ProcessSingleEventAsync(eventToReceive, manualRequest: null, activity, stoppingToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }
        finally
        {
            _singleExecutionLock.Release();
        }
    }

    public Task<EventActionResult> ProcessSingleEventAsync(InboxMessage message, EventActionRequest manualRequest,
        CancellationToken cancellationToken)
    {
        return ProcessSingleEventAsync(message, manualRequest, parentActivity: Activity.Current, cancellationToken);
    }

    private async Task<EventActionResult> ProcessSingleEventAsync(InboxMessage message,
        EventActionRequest manualRequest, Activity parentActivity, CancellationToken cancellationToken)
    {
        var lockName = FunctionalityNames.GetEventLockName(FunctionalityNames.Inbox, message.Id);
        await using var distributedLock =
            await _lockProvider.TryAcquireLockAsync(lockName, cancellationToken: cancellationToken);
        if (distributedLock is null)
        {
            _logger.LogInformation(
                "Could not open distributed lock for processing inbox event with ID: {EventId}. It may be processing by another instance.",
                message.Id);
            return EventActionResult.AlreadyProcessing(message.Id);
        }

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();

        // The status is read again under the lock, since the event could be changed after it was fetched.
        var currentStatus = await repository.GetEventStatusByIdAsync(message.Id);
        if (currentStatus is null)
            return EventActionResult.NotFound(message.Id);

        var force = manualRequest?.Force == true;
        if (!EventStatusTransitions.CanBeExecuted(currentStatus.Value, force))
        {
            _logger.LogInformation("The inbox event with id {EventId} has the {Status} status. Skipping execution.",
                message.Id, currentStatus.Value);
            return EventActionResult.InvalidState(
                $"The inbox event with the {currentStatus.Value} status cannot be executed{(currentStatus == EventStatus.Processed ? " without the force option" : string.Empty)}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var performedBy = manualRequest?.PerformedBy;
        var comment = manualRequest?.Comment;
        try
        {
            var isSuccessfullyExecuted = await ExecuteEventHandlers(message, parentActivity);
            if (isSuccessfullyExecuted)
                message.Processed(performedBy, comment);
            else
                message.Failed(0, _settings.TryAfterMinutesIfEventNotFound,
                    $"No event handler configured for the {message.EventName} event with the {message.Provider} provider.", performedBy, comment);
        }
        catch (Exception e)
        {
            message.Failed(_settings.TryCount, _settings.TryAfterMinutes, e.ToFailureReason(_settings), performedBy,
                comment);
        }
        finally
        {
            await repository.UpdateEventAsync(message); 
        }

        return message.Status == EventStatus.Processed
            ? EventActionResult.Success()
            : EventActionResult.Failed(message.FailureReason);
    }

    /// <summary>
    /// Executes all handlers of the inbox event.
    /// </summary>
    /// <returns>Returns true if the handlers are executed, or false if there is no handler for the event.</returns>
    private async Task<bool> ExecuteEventHandlers(IInboxMessage inboxMessage, Activity parentActivity)
    {
        try
        {
            var receiverKey = GetHandlerKey(inboxMessage.EventName, inboxMessage.Provider);
            if (!_receivers.TryGetValue(receiverKey, out var inboxEventsInformation))
            {
                _logger.LogError(
                    "No event handler configured for the {EventType} type name of inbox event with id {EventId} which is {ProviderType} provider(s).",
                    inboxMessage.EventName, inboxMessage.Id, inboxMessage.Provider);
                return false;
            }

            _logger.LogDebug("{StorageType}: Executing subscribers of the event '{EventName}'(ID: {MessageId})", 
                EventStorageInvestigationTagNames.InboxEventTag, inboxMessage.EventName, inboxMessage.Id);
            using var activity = CreateActivityForExecutingHandlersIfEnabled(inboxMessage, parentActivity);

            // Create a new scope to execute the receiver services of the event as a scoped service
            // because each event's handlers must be executed in a separate scope to avoid conflicts in scoped services like DbContext.
            using var serviceScope = _serviceProvider.CreateScope();
            var isOnExecutingEventInvoked = false;

            foreach (var inboxEventInformation in inboxEventsInformation)
            {
                var inboxEvent = LoadInboxEvent(inboxMessage, inboxEventInformation);
                if (!isOnExecutingEventInvoked)
                {
                    OnExecutingInboxEvent(inboxEvent, inboxEventInformation,
                        serviceScope.ServiceProvider);
                    isOnExecutingEventInvoked = true;
                }

                var eventReceiver =
                    serviceScope.ServiceProvider.GetRequiredService(inboxEventInformation.EventHandlerType);
                await ((Task)inboxEventInformation.HandleMethod.Invoke(eventReceiver,
                    [inboxEvent]))!;
            }

            OnEndingInboxEvent(inboxMessage, serviceScope.ServiceProvider);

            return true;
        }
        catch (Exception e)
        {
            var exception =
                new EventStoreException(e, $"Error while executing handler of inbox event with ID: {inboxMessage.Id}");
            _logger.LogError(exception, exception.Message);
            throw exception;
        }
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Invokes the ExecutingReceivedEvent event to be able to execute the event before the handler.
    /// </summary>
    /// <param name="event">Executing an event</param>
    /// <param name="eventHandlerInformation">Contains metadata about the event handler, including its type and provider.</param>
    /// <param name="serviceProvider">The IServiceProvider used to resolve dependencies from the scope.</param>
    private void OnExecutingInboxEvent(
        IInboxEvent @event,
        EventHandlerInformation eventHandlerInformation,
        IServiceProvider serviceProvider
    )
    {
        if (ExecutingInboxEvent is null)
            return;

        var eventArgs = new InboxEventArgs
        {
            Event = @event,
            EventHandlerType = eventHandlerInformation.EventHandlerType,
            ProviderType = eventHandlerInformation.ProviderType,
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
        try
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
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            throw new EventStoreException(e,
                $"Could not deserialize the inbox event to the {eventHandlerInformation.EventType.FullName} type.");
        }
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
            $"{EventStorageInvestigationTagNames.InboxEventTag}: Executing publishers of the {inboxMessage.EventName} event";
        var traceParentId = parentActivity?.Id;
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, traceParentId, spanType: EventStorageInvestigationTagNames.InboxEventTag);
        activity?.AttachEventInfo(inboxMessage);

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
            $"{EventStorageInvestigationTagNames.InboxEventTag}: Executing {eventsCount} unprocessed event(s)";
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, spanType: EventStorageInvestigationTagNames.InboxEventTag);

        return activity;
    }

    #endregion
}