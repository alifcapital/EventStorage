using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// An interface for implementing the handling functionality of received inbox event for the SMS provider.
/// </summary>
public interface ISmsEventHandler<in TInboxEvent> : IEventHandler<TInboxEvent>
    where TInboxEvent : class, IInboxEvent
{
    
}