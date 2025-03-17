using EventStorage.Outbox.Providers.EventProviders;

namespace EventStorage.Tests.Domain;

public class SimpleSendEventCreatedHandler : IMessageBrokerEventPublisher<SimpleOutboxEventCreated>
{
    public async Task PublishAsync(SimpleOutboxEventCreated outboxEvent)
    {
        await Task.CompletedTask;
    }
}