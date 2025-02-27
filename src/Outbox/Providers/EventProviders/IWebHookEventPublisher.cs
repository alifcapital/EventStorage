using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Providers.EventProviders;

/// <summary>
/// An interface for implementing the publishing functionality of specific outbox event for the WebHook provider.
/// </summary>
public interface IWebHookEventPublisher<in TOutboxEvent> : IEventPublisher<TOutboxEvent>
    where TOutboxEvent : class, IOutboxEvent;