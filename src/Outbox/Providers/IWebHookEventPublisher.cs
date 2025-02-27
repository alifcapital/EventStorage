namespace EventStorage.Outbox.Providers;

/// <summary>
/// An interface for implementing the publishing functionality of outbox events for the WebHook provider.
/// </summary>
public interface IWebHookEventPublisher : IEventPublisher;