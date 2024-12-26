using System.Text.Json;
using EventStorage.Configurations;
using EventStorage.Exceptions;
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

    private readonly Dictionary<string, (Type eventType, Type eventReceiverType, string providerType, bool hasHeaders,
        bool hasAdditionalData)> _receivers;

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
        _receivers = new Dictionary<string, (Type eventType, Type eventReceiverType, string providerType, bool hasHeaders, bool hasAdditionalData)>();
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
        var eventName = typeOfReceiveEvent.Name;
        if (!_receivers.ContainsKey(eventName))
        {
            var hasHeaders = HasHeadersType.IsAssignableFrom(typeOfReceiveEvent);
            var hasAdditionalData = HasAdditionalDataType.IsAssignableFrom(typeOfReceiveEvent);

            _receivers.Add(eventName,
                (typeOfReceiveEvent, typeOfEventReceiver, providerType.ToString(), hasHeaders, hasAdditionalData));
        }
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
            if(eventsToReceive.Length == 0)
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
            if (_receivers.TryGetValue(@event.EventName,
                    out (Type eventType, Type eventReceiverType, string providerType, bool hasHeaders, bool
                    hasAdditionalData) info))
            {
                if (@event.Provider == info.providerType)
                {
                    _logger.LogTrace("Executing the {EventType} inbox event with ID {EventId} to receive.",
                        @event.EventName, @event.Id);

                    var jsonSerializerSetting = @event.GetJsonSerializer();
                    var eventToReceive = JsonSerializer.Deserialize(@event.Payload, info.eventType, jsonSerializerSetting) as IReceiveEvent;
                    if (info.hasHeaders && @event.Headers is not null)
                        ((IHasHeaders)eventToReceive).Headers =
                            JsonSerializer.Deserialize<Dictionary<string, string>>(@event.Headers);

                    if (info.hasAdditionalData && @event.AdditionalData is not null)
                        ((IHasAdditionalData)eventToReceive).AdditionalData =
                            JsonSerializer.Deserialize<Dictionary<string, string>>(@event!.AdditionalData);

                    //Create a new scope to execute the receiver service as a scoped service for each event
                    using var serviceScope = serviceProvider.CreateScope();
                    var eventReceiver = serviceScope.ServiceProvider.GetRequiredService(info.eventReceiverType);

                    var receiveMethod = info.eventReceiverType.GetMethod(ReceiverMethodName);
                    await (Task)receiveMethod.Invoke(eventReceiver,
                        [eventToReceive]);
                    @event.Processed();

                    return;
                }
                
                _logger.LogError(
                    "The {EventType} inbox event with ID {EventId} requested to receive with {ProviderType} provider, but that is configured to receive with the {ConfiguredProviderType} provider.",
                    @event.EventName, @event.Id, @event.Provider, info.providerType);
            }
            else
            {
                _logger.LogError(
                    "No publish provider configured for the {EventType} inbox event with ID: {EventId}.",
                    @event.EventName, @event.Id);
            }
            @event.Failed(0, _settings.TryAfterMinutesIfEventNotFound);
        }
        catch (Exception e)
        {
            var exception = new EventStoreException(e, $"Error while receiving event with ID: {@event.Id}");
            _logger.LogError(exception, exception.Message);
            throw exception;
        }
    }
}