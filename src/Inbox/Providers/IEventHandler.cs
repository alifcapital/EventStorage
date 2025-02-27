using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// Base interface for implementing the handling functionality of received inbox event for all providers.
/// </summary>
public interface IEventHandler<in TInboxEvent>
    where TInboxEvent : class, IInboxEvent
{
    /// <summary>
    /// Handles a received inbox event.
    /// </summary>
    /// <param name="event">Received an inbox event</param>
    /// <returns>It may throw an exception if fails</returns>
    Task HandleAsync(TInboxEvent @event);
}