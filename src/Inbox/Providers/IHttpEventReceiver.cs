using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;
/// <summary>
/// An interface to determine a receiver of events and implement receiving events functionality with the Http provider
/// </summary>
public interface IHttpEventReceiver<TReceiveEvent> : IEventReceiver<TReceiveEvent>
    where TReceiveEvent : class, IReceiveEvent
{
}