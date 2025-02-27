using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// An interface for implementing the handling functionality of received inbox event for the Email provider.
/// </summary>
public interface IEmailEventHandler<in TInboxEvent> : IEventHandler<TInboxEvent>
    where TInboxEvent : class, IInboxEvent
{
}