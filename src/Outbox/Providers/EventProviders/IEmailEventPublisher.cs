using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// An interface for determine a publisher of events and implement publishing events functionality with the Email provider
/// </summary>
public interface IEmailEventPublisher<TSendEvent> : IEventPublisher<TSendEvent>
    where TSendEvent : class, ISendEvent
{
}