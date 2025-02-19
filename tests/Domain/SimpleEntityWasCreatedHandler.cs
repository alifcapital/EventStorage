using EventStorage.Inbox.Providers;

namespace EventStorage.Tests.Domain;

public class SimpleEntityWasCreatedHandler : IUnknownEventReceiver<SimpleEntityWasCreated>
{
    public async Task HandleAsync(SimpleEntityWasCreated @event)
    {
        await Task.CompletedTask;
    }
}