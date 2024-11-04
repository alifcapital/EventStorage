using EventStorage.Outbox.Providers.EventProviders;

namespace EventStorage.Tests.Domain;

public class SimpleSendEventCreatedHandler : IMessageBrokerEventPublisher<SimpleSendEventCreated>
{
    public Task Publish(SimpleSendEventCreated @event, string eventPath)
    {
        return Task.CompletedTask;
    }
}