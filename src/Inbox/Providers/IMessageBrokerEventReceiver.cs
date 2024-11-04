using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// An interface for determine a receiver of events and implement receiving events functionality with the MessageBroker provider
/// </summary>
public interface IMessageBrokerEventReceiver<TReceiveEvent> : IEventReceiver<TReceiveEvent>
    where TReceiveEvent : class, IReceiveEvent
{
    
}