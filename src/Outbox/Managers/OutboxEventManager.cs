using System.Collections.Concurrent;
using System.Text.Json;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Managers;

internal class OutboxEventManager : IOutboxEventManager
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxEventsProcessor _outboxEventsProcessor;
    private readonly ILogger<OutboxEventManager> _logger;
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _eventsToSend = [];

    /// <summary>
    /// The EventSenderManager class will keep injecting itself even the outbox pattern is off, but the repository will be null since that is not registered in the DI container.
    /// </summary>
    public OutboxEventManager(ILogger<OutboxEventManager> logger, IOutboxEventsProcessor outboxEventsProcessor = null,
        IOutboxRepository repository = null)
    {
        _repository = repository;
        _logger = logger;
        _outboxEventsProcessor = outboxEventsProcessor;
    }

    #region Collect Methods

    public bool Collect<TOutboxEvent>(TOutboxEvent outboxEvent) where TOutboxEvent : IOutboxEvent
    {
        var eventPublisherTypes = _outboxEventsProcessor?.GetEventPublisherTypes(outboxEvent);
        if (string.IsNullOrEmpty(eventPublisherTypes))
        {
            _logger.LogError("There is no publisher for the {OutboxEventName} outbox event type.",
                outboxEvent.GetType().FullName);
            return false;
        }

        var stored = Collect(outboxEvent, eventPublisherTypes);
        return stored;
    }

    public bool Collect<TOutboxEvent>(TOutboxEvent outboxEvent, EventProviderType eventProvider)
        where TOutboxEvent : IOutboxEvent
    {
        var stored = Collect(outboxEvent, eventProvider.ToString());
        return stored;
    }

    private bool Collect<TOutboxEvent>(TOutboxEvent outboxEvent, string eventProvider)
        where TOutboxEvent : IOutboxEvent
    {
        if (_eventsToSend.ContainsKey(outboxEvent.EventId))
            return false;

        if (_repository is null)
        {
            _logger.LogError("The system trying to store an event into the outbox table, but the outbox functionality is not enabled.");
            return false;
        }

        try
        {
            var outboxMessage = CreateOutboxMessage(outboxEvent, eventProvider);
            return _eventsToSend.TryAdd(outboxMessage.Id, outboxMessage);
        }
        catch (Exception e)
        {
            var eventType = outboxEvent.GetType();
            _logger.LogError(e,
                "Error while collecting the {EventType} event type with the {EventId} id to store the Outbox table.",
                eventType.Name, outboxEvent.EventId);
            throw;
        }
    }

    #endregion

    #region StoreAsync Methods

    public Task<bool> StoreAsync<TOutboxEvent>(TOutboxEvent outboxEvent, EventProviderType eventProvider) where TOutboxEvent : IOutboxEvent
    {
        return StoreAsync(outboxEvent, eventProvider.ToString());
    }

    public Task<bool> StoreAsync<TOutboxEvent>(TOutboxEvent outboxEvent) where TOutboxEvent : IOutboxEvent
    {
        var eventPublisherTypes = _outboxEventsProcessor?.GetEventPublisherTypes(outboxEvent);
        if (string.IsNullOrEmpty(eventPublisherTypes))
        {
            _logger.LogError("There is no publisher for the {OutboxEventName} outbox event type.",
                outboxEvent.GetType().FullName);
            return Task.FromResult(false);
        }

        return StoreAsync(outboxEvent, eventPublisherTypes);
    }

    public async Task<bool> StoreAsync<TOutboxEvent>(TOutboxEvent[] outboxEvents) where TOutboxEvent : IOutboxEvent
    {
        if (_repository is null)
            throw new EventStoreException(
                "The system trying to store an events into the outbox table, but the outbox functionality is not enabled.");

        try
        {
            var outboxMessages = new List<OutboxMessage>();
            foreach (var outboxEvent in outboxEvents)
            {
                var eventPublisherTypes = _outboxEventsProcessor?.GetEventPublisherTypes(outboxEvent);
                if (string.IsNullOrEmpty(eventPublisherTypes))
                {
                    _logger.LogError("There is no publisher for the {OutboxEventName} outbox event type.",
                        outboxEvent.GetType().FullName);
                    continue;
                }

                var outboxMessage = CreateOutboxMessage(outboxEvent, eventPublisherTypes);
                outboxMessages.Add(outboxMessage);
            }

            if (outboxMessages.Count == 0)
                return false;

            var successfullyInserted = await _repository.BulkInsertEventsAsync(outboxMessages.ToArray());
            return successfullyInserted;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while storing multiple events to the Outbox table.");
            throw;
        }
    }

    private async Task<bool> StoreAsync<TOutboxEvent>(TOutboxEvent outboxEvent, string eventProvider)
        where TOutboxEvent : IOutboxEvent
    {
        if (_repository is null)
            throw new EventStoreException(
                "The system trying to store an event into the outbox table, but the outbox functionality is not enabled.");

        try
        {
            var outboxMessage = CreateOutboxMessage(outboxEvent, eventProvider);
            return await _repository.InsertEventAsync(outboxMessage)!;
        }
        catch (Exception e)
        {
            var eventType = outboxEvent.GetType();
            _logger.LogError(e,
                "Error while storing the {EventType} event type with the {EventId} id to store the Outbox table.",
                eventType.Name, outboxEvent.EventId);
            throw;
        }
    }

    #endregion

    #region StoreCollectedEvents

    /// <summary>
    /// Store all collected events to the database and clear the memory.
    /// </summary>
    private void StoreCollectedEvents()
    {
        var outboxMessages = _eventsToSend.Values.ToArray();
        if (outboxMessages.Length == 0)
            return;
        
        _repository?.BulkInsertEvents(outboxMessages);
        CleanCollectedEvents();
    }

    #endregion

    #region CleanCollectedEvents

    public void CleanCollectedEvents()
    {
        _eventsToSend.Clear();
    }

    #endregion

    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Store all collected events to the database and clear the memory.
    /// </summary>
    private void Disposing()
    {
        if (_disposed) return;

        StoreCollectedEvents();

        _disposed = true;
    }

    ~OutboxEventManager()
    {
        Disposing();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an OutboxMessage based on the provided outbox event for storing it to the database.
    /// </summary>
    /// <param name="outboxEvent">The event that we want to store in the outbox table.</param>
    /// <param name="eventProvider">The provider of the event that should be handled while publishing the event.</param>
    /// <typeparam name="TOutboxEvent">The type of the outbox event that should be stored in the outbox table.</typeparam>
    /// <returns>Newly created OutboxMessage based on the provided outbox event.</returns>
    private static OutboxMessage CreateOutboxMessage<TOutboxEvent>(TOutboxEvent outboxEvent, string eventProvider) where TOutboxEvent : IOutboxEvent
    {
        var eventType = outboxEvent.GetType();
        string eventHeaders = null;
        string eventAdditionalData = null;

        if (outboxEvent is IHasHeaders hasHeaders)
        {
            if (hasHeaders.Headers?.Count > 0)
                eventHeaders = JsonSerializer.Serialize(hasHeaders.Headers);
            hasHeaders.Headers = null;
        }

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
            Provider = eventProvider,
            EventName = eventType.Name,
            EventPath = eventType.Namespace,
            Headers = eventHeaders,
            AdditionalData = eventAdditionalData,
            Payload = eventPayload
        };

        return outboxMessage;
    }

    #endregion
}