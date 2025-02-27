using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// An interface for implementing the publishing functionality of specific outbox event for the gRPC provider.
/// </summary>
public interface IGrpcEventPublisher<in TOutboxEvent> : IEventPublisher<TOutboxEvent>
    where TOutboxEvent : class, IOutboxEvent;