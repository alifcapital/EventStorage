using EventStorage.Inbox.Providers;

namespace EventStorage.Tests.Domain.Module2;

public class UserCreatedHandler : IMessageBrokerEventReceiver<SimpleEntityWasCreated>
{
    public Task Receive(SimpleEntityWasCreated @event)
    {
        return Task.CompletedTask;
    }
}