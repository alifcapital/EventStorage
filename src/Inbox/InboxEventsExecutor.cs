using System.Text.Json;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Inbox.EventArgs;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Providers;
using EventStorage.Inbox.Repositories;
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

    private readonly Dictionary<string, List<EventHandlerInformation>> _receivers;

    private const string HandleMethodName = nameof(IEventHandler<IInboxEvent>.HandleAsync);

    private static readonly Type HasHeadersType = typeof(IHasHeaders);
    private static readonly Type HasAdditionalDataType = typeof(IHasAdditionalData);
    private readonly SemaphoreSlim _singleExecutionLock = new(1, 1);
    private readonly SemaphoreSlim _semaphore;

    public InboxEventsExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<InboxEventsExecutor>>();
        _settings = serviceProvider.GetRequiredService<InboxAndOutboxSettings>().Inbox;
        _receivers = new Dictionary<string, List<EventHandlerInformation>>();
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrency);
    }

    /// <summary>
    /// Registers a receiver 
    /// </summary>
    /// <param name="typeOfReceiveEvent">Event type which we want to use to receive</param>
    /// <param name="typeOfEventReceiver">Receiver type of the event which we want to receiver event</param>
    /// <param name="providerType">Provider type of received event</param>
    public void AddReceiver(Type typeOfReceiveEvent, Type typeOfEventReceiver, EventProviderType providerType)
    {
        var receiverKey = GetReceiverKey(typeOfReceiveEvent.Name, providerType.ToString());
        if (!_receivers.TryGetValue(receiverKey, out var receiversInformation))
        {
            receiversInformation = [];
            _receivers.Add(receiverKey, receiversInformation);
        }
        
        var hasHeaders = HasHeadersType.IsAssignableFrom(typeOfReceiveEvent);
        var hasAdditionalData = HasAdditionalDataType.IsAssignableFrom(typeOfReceiveEvent);
        var handleMethod = typeOfEventReceiver.GetMethod(HandleMethodName);

        var receiverInformation = new EventHandlerInformation
        {
            EventType = typeOfReceiveEvent,
            EventHandlerType = typeOfEventReceiver,
            HandleMethod = handleMethod,
            ProviderType = providerType,
            HasHeaders = hasHeaders,
            HasAdditionalData = hasAdditionalData
        };
        receiversInformation.Add(receiverInformation);
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
            var eventsToReceive = await repository.GetUnprocessedEventsAsync();
            if (eventsToReceive.Length == 0)
                return;

            stoppingToken.ThrowIfCancellationRequested();
            var tasks = eventsToReceive.Select(async eventToReceive =>
            {
                await _semaphore.WaitAsync(stoppingToken);
                try
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await ExecuteEventReceiver(eventToReceive, _serviceProvider);
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

            await repository.UpdateEventsAsync(eventsToReceive);
        }
        finally
        {
            _singleExecutionLock.Release();
        }
    }

    private async Task ExecuteEventReceiver(IInboxMessage message, IServiceProvider serviceProvider)
    {
        try
        {
            var receiverKey = GetReceiverKey(message.EventName, message.Provider);
            if (_receivers.TryGetValue(receiverKey, out var receiversInformation))
            {
                _logger.LogTrace("Executing the received {EventTypeName} event of inbox event with ID {EventId}.",
                    message.EventName, message.Id);

                //Create a new scope to execute the receiver service as a scoped service for each event
                using var serviceScope = serviceProvider.CreateScope();

                foreach (var receiverInformation in receiversInformation)
                {
                    var receivedEvent = LoadReceivedEvent(message, receiverInformation);
                    OnExecutingReceivedEvent(receivedEvent, receiverInformation.ProviderType,
                        serviceScope.ServiceProvider);
                    
                    var eventReceiver = serviceScope.ServiceProvider.GetRequiredService(receiverInformation.EventHandlerType);
                    await ((Task)receiverInformation.HandleMethod.Invoke(eventReceiver,
                        [receivedEvent]))!;
                }

                message.Processed();

                return;
            }

            _logger.LogError(
                "No event receiver provider configured for the {EventTypeName} type name of inbox event with ID: {EventId}.",
                message.EventName, message.Id);

            message.Failed(0, _settings.TryAfterMinutesIfEventNotFound);
        }
        catch (Exception e)
        {
            var exception = new EventStoreException(e, $"Error while receiving event with ID: {message.Id}");
            _logger.LogError(exception, exception.Message);
            throw exception;
        }
    }

    /// <summary>
    /// Invokes the ExecutingReceivedEvent event to be able to execute the event before the handler.
    /// </summary>
    /// <param name="event">Executing an event</param>
    /// <param name="providerType">The provider type of event</param>
    /// <param name="serviceProvider">The IServiceProvider used to resolve dependencies from the scope.</param>
    private void OnExecutingReceivedEvent(IInboxEvent @event, EventProviderType providerType,
        IServiceProvider serviceProvider)
    {
        if (ExecutingInboxEvent is null)
            return;

        var eventArgs = new InboxEventArgs(@event, providerType, serviceProvider);
        ExecutingInboxEvent.Invoke(this, eventArgs);
    }

    #region Helper methods

    /// <summary>
    /// Get the key of the receiver by event name and provider name.
    /// </summary>
    /// <param name="eventName">The name of event type</param>
    /// <param name="providerName">The name of event provider type</param>
    /// <returns>Based on the event name and provider name, it returns a unique key for the receiver.</returns>
    internal static string GetReceiverKey(string eventName, string providerName)
    {
        return $"{eventName}_{providerName}";
    }

    /// <summary>
    /// Load the received event from the inbox event.
    /// </summary>
    /// <param name="message">The event of Inbox</param>
    /// <param name="eventHandlerInformation">The event receivers information</param>
    /// <returns>Loaded instance of event</returns>
    private static IInboxEvent LoadReceivedEvent(IInboxMessage message, EventHandlerInformation eventHandlerInformation)
    {
        var jsonSerializerSetting = message.GetJsonSerializer();
        var receivedEvent =
            JsonSerializer.Deserialize(message.Payload, eventHandlerInformation.EventType, jsonSerializerSetting) as
                IInboxEvent;
        if (eventHandlerInformation.HasHeaders && message.Headers is not null)
            ((IHasHeaders)receivedEvent).Headers =
                JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers);

        if (eventHandlerInformation.HasAdditionalData && message.AdditionalData is not null)
            ((IHasAdditionalData)receivedEvent).AdditionalData =
                JsonSerializer.Deserialize<Dictionary<string, string>>(message!.AdditionalData);
        return receivedEvent;
    }

    #endregion
}