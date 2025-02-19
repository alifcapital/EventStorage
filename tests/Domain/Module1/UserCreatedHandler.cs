using EventStorage.Inbox.Providers;

namespace EventStorage.Tests.Domain.Module1;

public class UserCreatedHandler : IMessageBrokerEventReceiver<SimpleEntityWasCreated>
{
    public async Task HandleAsync(SimpleEntityWasCreated @event)
    {
        await Task.CompletedTask;
    }
}