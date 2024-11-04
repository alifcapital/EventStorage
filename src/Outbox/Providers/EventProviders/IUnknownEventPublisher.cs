using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// An interface for determine a publisher of events and implement publishing events functionality with the Unknown provider
/// </summary>
public interface IUnknownEventPublisher<TSendEvent> : IEventPublisher<TSendEvent>
    where TSendEvent : class, ISendEvent
{
}