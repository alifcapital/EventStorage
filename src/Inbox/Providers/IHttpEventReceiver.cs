using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

public interface IHttpEventReceiver<TReceiveEvent> : IEventReceiver<TReceiveEvent>
    where TReceiveEvent : class, IReceiveEvent
{
}