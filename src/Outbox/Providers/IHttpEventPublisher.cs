namespace EventStorage.Outbox.Providers;

/// <summary>
/// An interface for implementing the publishing functionality of outbox events for the HTTP provider.
/// </summary>
public interface IHttpEventPublisher : IEventPublisher;