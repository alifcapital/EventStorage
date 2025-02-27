namespace EventStorage.Outbox.Providers;

/// <summary>
/// An interface for implementing the publishing functionality of outbox events for the Email provider.
/// </summary>
public interface IEmailEventPublisher : IEventPublisher;