using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers;

/// <summary>
/// Base interface for implementing the publishing functionality of outbox events for all providers.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// For publishing an outbox event.
    /// </summary>
    /// <param name="outboxEvent">Publishing an event</param>
    /// <returns>It may throw an exception if fails</returns>
    Task PublishAsync(IOutboxEvent outboxEvent);
}