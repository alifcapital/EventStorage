using System.Collections.Concurrent;
using System.Text.Json;
using EventStorage.Extensions;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Managers;

internal class OutboxEventManager : IOutboxEventManager
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxEventsExecutor _outboxEventsExecutor;
    private readonly ILogger<OutboxEventManager> _logger;
    private readonly ConcurrentBag<OutboxMessage> _eventsToSend = [];
    private bool _disposed;

    /// <summary>
    /// The EventSenderManager class will keep injecting itself even the outbox pattern is off, but the repository will be null since that is not registered in the DI container.
    /// </summary>
    public OutboxEventManager(ILogger<OutboxEventManager> logger, IOutboxEventsExecutor outboxEventsExecutor, IOutboxRepository repository = null)
    {
        _repository = repository;
        _logger = logger;
        _outboxEventsExecutor = outboxEventsExecutor;
    }

    public bool Store<TOutboxEvent>(TOutboxEvent outboxEvent, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase) where TOutboxEvent : IOutboxEvent
    {
        var outboxEventName = outboxEvent.GetType().Name;
        var eventPublisherTypes = _outboxEventsExecutor.GetEventPublisherTypes(outboxEventName);
        if (eventPublisherTypes is null)
        {
            _logger.LogError("There is no publisher for the {OutboxEventName} outbox event type.", outboxEventName);
            return false;
        }
        
        foreach (var eventProvider in eventPublisherTypes)
            Store(outboxEvent, eventProvider);
        
        return true;
    }

    public bool Store<TOutboxEvent>(TOutboxEvent outboxEvent, EventProviderType eventProvider, 
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TOutboxEvent : IOutboxEvent
    {
        var eventType = outboxEvent.GetType();
        try
        {
            string eventHeaders = null;
            if (outboxEvent is IHasHeaders hasHeaders)
            {
                if (hasHeaders.Headers?.Count > 0)
                    eventHeaders = JsonSerializer.Serialize(hasHeaders.Headers);
                hasHeaders.Headers = null;
            }

            string eventAdditionalData = null;
            if (outboxEvent is IHasAdditionalData hasAdditionalData)
            {
                if (hasAdditionalData.AdditionalData?.Count > 0)
                    eventAdditionalData = JsonSerializer.Serialize(hasAdditionalData.AdditionalData);
                hasAdditionalData.AdditionalData = null;
            }
            
            var eventPayload = outboxEvent.SerializeToJson();
            
            var outboxMessage = new OutboxMessage
            {
                Id = outboxEvent.EventId,
                Provider = eventProvider.ToString(),
                EventName = eventType.Name,
                EventPath = eventType.Namespace,
                NamingPolicyType = namingPolicyType.ToString(),
                Headers = eventHeaders,
                AdditionalData = eventAdditionalData,
                Payload = eventPayload
            };

            _eventsToSend.Add(outboxMessage);
            
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while collecting the {EventType} event type with the {EventId} id to store the Outbox table.", eventType.Name, outboxEvent.EventId);
            throw;
        }
    }

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