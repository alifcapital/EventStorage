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

internal class ReceivedEventExecutor : IReceivedEventExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReceivedEventExecutor> _logger;
    private readonly InboxOrOutboxStructure _settings;

    /// <summary>
    /// The event to be executed before executing the handler of the received event.
    /// </summary>
    public static event EventHandler<ReceivedEventArgs> ExecutingReceivedEvent;

    private readonly Dictionary<string, ReceiversInformation> _receivers;

    private const string ReceiverMethodName = nameof(IEventReceiver<IReceiveEvent>.Receive);

    private static readonly Type HasHeadersType = typeof(IHasHeaders);
    private static readonly Type HasAdditionalDataType = typeof(IHasAdditionalData);
    private readonly SemaphoreSlim _singleExecutionLock = new(1, 1);
    private readonly SemaphoreSlim _semaphore;

    public ReceivedEventExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ReceivedEventExecutor>>();
        _settings = serviceProvider.GetRequiredService<InboxAndOutboxSettings>().Inbox;
        _receivers = new Dictionary<string, ReceiversInformation>();
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
            var hasHeaders = HasHeadersType.IsAssignableFrom(typeOfReceiveEvent);
            var hasAdditionalData = HasAdditionalDataType.IsAssignableFrom(typeOfReceiveEvent);

            receiversInformation = new ReceiversInformation
            {
                EventType = typeOfReceiveEvent,
                ProviderType = providerType,
                HasHeaders = hasHeaders,
                HasAdditionalData = hasAdditionalData
            };

            _receivers.Add(receiverKey, receiversInformation);
        }

        receiversInformation.EventReceiverTypes.Add(typeOfEventReceiver);
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

    private async Task ExecuteEventReceiver(IInboxEvent @event, IServiceProvider serviceProvider)
    {
        try
        {
            var receiverKey = GetReceiverKey(@event.EventName, @event.Provider);
            if (_receivers.TryGetValue(receiverKey, out var receiversInformation))
            {
                _logger.LogTrace("Executing the received {EventTypeName} event of inbox event with ID {EventId}.",
                    @event.EventName, @event.Id);

                //Create a new scope to execute the receiver service as a scoped service for each event
                using var serviceScope = serviceProvider.CreateScope();

                var receivedEvent = LoadReceivedEvent(@event, receiversInformation);
                OnExecutingReceivedEvent(receivedEvent, receiversInformation.ProviderType,
                    serviceScope.ServiceProvider);

                foreach (var eventReceiverType in receiversInformation.EventReceiverTypes)
                {
                    var eventReceiver = serviceScope.ServiceProvider.GetRequiredService(eventReceiverType);
                    var receiveMethod = eventReceiverType.GetMethod(ReceiverMethodName);
                    await (Task)receiveMethod.Invoke(eventReceiver,
                        [receivedEvent]);
                }

                @event.Processed();

                return;
            }

            _logger.LogError(
                "No event receiver provider configured for the {EventTypeName} type name of inbox event with ID: {EventId}.",
                @event.EventName, @event.Id);

            @event.Failed(0, _settings.TryAfterMinutesIfEventNotFound);
        }
        catch (Exception e)
        {
            var exception = new EventStoreException(e, $"Error while receiving event with ID: {@event.Id}");
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
    private void OnExecutingReceivedEvent(IReceiveEvent @event, EventProviderType providerType,
        IServiceProvider serviceProvider)
    {
        if (ExecutingReceivedEvent is null)
            return;

        var eventArgs = new ReceivedEventArgs(@event, providerType, serviceProvider);
        ExecutingReceivedEvent.Invoke(this, eventArgs);
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
    /// <param name="event">The event of Inbox</param>
    /// <param name="receiversInformation">The event receivers information</param>
    /// <returns>Loaded instance of event</returns>
    private static IReceiveEvent LoadReceivedEvent(IInboxEvent @event, ReceiversInformation receiversInformation)
    {
        var jsonSerializerSetting = @event.GetJsonSerializer();
        var receivedEvent =
            JsonSerializer.Deserialize(@event.Payload, receiversInformation.EventType, jsonSerializerSetting) as
                IReceiveEvent;
        if (receiversInformation.HasHeaders && @event.Headers is not null)
            ((IHasHeaders)receivedEvent).Headers =
                JsonSerializer.Deserialize<Dictionary<string, string>>(@event.Headers);

        if (receiversInformation.HasAdditionalData && @event.AdditionalData is not null)
            ((IHasAdditionalData)receivedEvent).AdditionalData =
                JsonSerializer.Deserialize<Dictionary<string, string>>(@event!.AdditionalData);
        return receivedEvent;
    }

    #endregion
}