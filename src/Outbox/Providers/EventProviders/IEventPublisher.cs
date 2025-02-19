using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// Base interface for determine a publisher of events. It may use for Unknown type.
/// </summary>
public interface IEventPublisher<TSendEvent>
    where TSendEvent : class, ISendEvent
{
    /// <summary>
    /// For publishing an event 
    /// </summary>
    /// <param name="event">Publishing an event</param>
    /// <param name="eventPath">Event path of publishing an event. It can be routing key, URL, or different value depend on provider type.</param>
    /// <returns>It may throw an exception if fails</returns>
    Task PublishAsync(TSendEvent @event, string eventPath);
}