using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers;

/// <summary>
/// Base interface to implement publishing events functionality for all providers
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// For publishing an event 
    /// </summary>
    /// <param name="event">Publishing an event</param>
    /// <param name="eventPath">Event path of publishing an event. It can be routing key, URL, or different value depend on provider type.</param>
    /// <returns>It may throw an exception if fails</returns>
    Task Publish(ISendEvent @event, string eventPath);
}