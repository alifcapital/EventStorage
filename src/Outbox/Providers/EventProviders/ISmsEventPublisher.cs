using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// An interface for determine a publisher of events and implement publishing events functionality with the SMS provider
/// </summary>
public interface ISmsEventPublisher<TSendEvent> : IEventPublisher<TSendEvent>
    where TSendEvent : class, ISendEvent
{
    
}