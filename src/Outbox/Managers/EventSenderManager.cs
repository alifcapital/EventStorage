using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Managers;

internal class EventSenderManager : IEventSenderManager
{
    private readonly IOutboxRepository _repository;
    private readonly ILogger<EventSenderManager> _logger;
    private readonly ConcurrentBag<OutboxEvent> _eventsToSend = new();
    private bool _disposed;

    /// <summary>
    /// The EventSenderManager class will keep injecting itself even the outbox pattern is off, but the repository will be null since that is not registered in the DI container.
    /// </summary>
    public EventSenderManager(ILogger<EventSenderManager> logger, IOutboxRepository repository = null)
    {
        _repository = repository;
        _logger = logger;
    }

    public bool Send<TSendEvent>(TSendEvent @event, EventProviderType eventProvider, string eventPath)
        where TSendEvent : ISendEvent
    {
        var eventName = @event.GetType().Name;
        try
        {
            var _event = new OutboxEvent
            {
                Id = @event.EventId,
                Provider = eventProvider.ToString(),
                EventName = @event.GetType().Name,
                EventPath = eventPath,
            };

            if (@event is IHasHeaders hasHeaders)
            {
                if (hasHeaders.Headers?.Any() == true)
                    _event.Headers = SerializeData(hasHeaders.Headers);
                hasHeaders.Headers = null;
            }

            if (@event is IHasAdditionalData hasAdditionalData)
            {
                if (hasAdditionalData.AdditionalData?.Any() == true)
                    _event.AdditionalData = SerializeData(hasAdditionalData.AdditionalData);
                hasAdditionalData.AdditionalData = null;
            }

            _event.Payload = SerializeData(@event);

            _eventsToSend.Add(_event);
            
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while collecting the {EventType} event type with the {EventId} id to store the Outbox table.",  eventName, @event.EventId);
            throw;
        }
    }

    #region SerializeData

    private static readonly JsonSerializerOptions SerializerSettings = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static string SerializeData<TValue>(TValue data)
    {
        return JsonSerializer.Serialize(data, SerializerSettings);
    }

    #endregion

    #region PublishCollectedEvents
    
    /// <summary>
    /// Store all collected events to the database and clear the memory.
    /// </summary>
    private void PublishCollectedEvents()
    {
        _repository?.BulkInsertEvents(_eventsToSend);
        _eventsToSend.Clear();
    }

    #endregion

    #region CleanCollectedEvents

    void IEventSenderManager.CleanCollectedEvents()
    {
        _eventsToSend.Clear();
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Add all collected activity logs to the activity log collector.
    /// </summary>
    private void Disposing()
    {
        if (_disposed) return;

        PublishCollectedEvents();

        _disposed = true;
    }

    ~EventSenderManager()
    {
        Disposing();
    }

    #endregion
}