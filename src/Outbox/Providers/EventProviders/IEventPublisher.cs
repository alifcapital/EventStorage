using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// Base interface for implementing the publishing functionality of specific outbox event for all providers.
/// </summary>
public interface IEventPublisher<in TOutboxEvent>
    where TOutboxEvent : class, IOutboxEvent
{
    /// <summary>
    /// For publishing an outbox event.
    /// </summary>
    /// <param name="outboxEvent">Publishing an event</param>
    /// <returns>It may throw an exception if fails</returns>
    Task PublishAsync(TOutboxEvent outboxEvent);
}