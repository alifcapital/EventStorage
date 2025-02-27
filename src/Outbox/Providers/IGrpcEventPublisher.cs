namespace EventStorage.Outbox.Providers;

/// <summary>
/// An interface for implementing the publishing functionality of outbox events for the gRPC provider.
/// </summary>
public interface IGrpcEventPublisher : IEventPublisher;