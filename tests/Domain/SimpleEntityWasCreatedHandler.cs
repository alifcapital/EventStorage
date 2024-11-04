using EventStorage.Inbox.Providers;

namespace EventStorage.Tests.Domain;

public class SimpleEntityWasCreatedHandler: IEventReceiver<SimpleEntityWasCreated>
{
    public Task Receive(SimpleEntityWasCreated @event)
    {
        return Task.CompletedTask;
    }
}