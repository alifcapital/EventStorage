using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// An interface for implementing the handling functionality of received inbox event for the WebHook provider.
/// </summary>
public interface IWebHookEventHandler<in TInboxEvent> : IEventHandler<TInboxEvent>
    where TInboxEvent : class, IInboxEvent
{
}