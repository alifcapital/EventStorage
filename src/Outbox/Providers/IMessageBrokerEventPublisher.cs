namespace EventStorage.Outbox.Providers;

/// <summary>
/// An interface for implementing the publishing functionality of outbox events for the MessageBroker provider;
/// </summary>
public interface IMessageBrokerEventPublisher : IEventPublisher;