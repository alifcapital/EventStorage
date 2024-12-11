using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// An interface to determine a publisher of events and implement publishing events functionality with the HTTP provider
/// </summary>
public interface IHttpEventPublisher<TSendEvent> : IEventPublisher<TSendEvent>
    where TSendEvent : class, ISendEvent
{
}