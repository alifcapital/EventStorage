using EventStorage.Outbox.Providers.EventProviders;

namespace EventStorage.Tests.Domain;

public class SimpleSendEventCreatedHandler : IMessageBrokerEventPublisher<SimpleSendEventCreated>
{
    public async Task PublishAsync(SimpleSendEventCreated @event, string eventPath)
    {
        await Task.CompletedTask;
    }
}