using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;
/// <summary>
/// An interface for implementing the handling functionality of received inbox event for the Http provider.
/// </summary>
public interface IHttpEventHandler<in TInboxEvent> : IEventHandler<TInboxEvent>
    where TInboxEvent : class, IInboxEvent
{
}