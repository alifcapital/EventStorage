using EventStorage.Inbox.Models;
using EventStorage.Models;

namespace EventStorage.Inbox.EventArgs;

using System;

/// <summary>
/// Event arguments to use while executing received event on the ExecutingReceivedEvent event
/// </summary>
public class ReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Executing event.
    /// </summary>
    public IReceiveEvent Event { get; }
    
    /// <summary>
    /// Type of event provider.
    /// </summary>
    public EventProviderType ProviderType { get; }
    
    /// <summary>
    /// The <see cref="IServiceProvider"/> used to resolve dependencies from the scope.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
    
    public ReceivedEventArgs(IReceiveEvent @event, EventProviderType providerType, IServiceProvider serviceProvider)
    {
        Event = @event;
        ProviderType = providerType;
        ServiceProvider = serviceProvider;
    }
}