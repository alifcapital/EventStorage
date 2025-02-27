using EventStorage.Inbox.Providers;

namespace EventStorage.Tests.Domain.Module1;

public class UserCreatedHandler : IMessageBrokerEventHandler<SimpleEntityWasCreated>
{
    public async Task HandleAsync(SimpleEntityWasCreated @event)
    {
        await Task.CompletedTask;
    }
}