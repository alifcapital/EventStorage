using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// An interface for determine a receiver of events and implement receiving events functionality with the SMS provider
/// </summary>
public interface ISmsEventReceiver<TReceiveEvent> : IEventReceiver<TReceiveEvent>
    where TReceiveEvent : class, IReceiveEvent
{
    
}