using EventStorage.Outbox.Providers.EventProviders;

namespace EventStorage.Tests.Domain;

public class SimpleSendEventCreatedHandler : IMessageBrokerEventPublisher<SimpleOutboxEventCreated>
{
    public async Task PublishAsync(SimpleOutboxEventCreated @event, string eventPath)
    {
        await Task.CompletedTask;
    }
}