using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Managers;

internal class OutboxEventManager : IOutboxEventManager
{
    private readonly IOutboxRepository _repository;
    private readonly ILogger<OutboxEventManager> _logger;
    private readonly ConcurrentBag<OutboxMessage> _eventsToSend = [];
    private bool _disposed;

    /// <summary>
    /// The EventSenderManager class will keep injecting itself even the outbox pattern is off, but the repository will be null since that is not registered in the DI container.
    /// </summary>
    public OutboxEventManager(ILogger<OutboxEventManager> logger, IOutboxRepository repository = null)
    {
        _repository = repository;
        _logger = logger;
    }

    public bool Store<TSendEvent>(TSendEvent @event, EventProviderType eventProvider, string eventPath = null, 
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TSendEvent : IOutboxEvent
    {
        var eventName = @event.GetType().Name;
        try
        {
            var _event = new OutboxMessage
            {
                Id = @event.EventId,
                Provider = eventProvider.ToString(),
                EventName = eventName,
                EventPath = eventPath?? eventName,
                NamingPolicyType = namingPolicyType.ToString()
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

    void IOutboxEventManager.CleanCollectedEvents()
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

    ~OutboxEventManager()
    {
        Disposing();
    }

    #endregion
}