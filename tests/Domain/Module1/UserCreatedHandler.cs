using EventStorage.Inbox.Providers;

namespace EventStorage.Tests.Domain.Module1;

public class UserCreatedHandler : IMessageBrokerEventReceiver<SimpleEntityWasCreated>
{
    public Task Receive(SimpleEntityWasCreated @event)
    {
        return Task.CompletedTask;
    }
}