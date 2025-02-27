using EventStorage.Inbox.Models;
using EventStorage.Models;

namespace EventStorage.Inbox.EventArgs;

using System;

/// <summary>
/// Event arguments to use while executing an inbox event on the ExecutingInboxEvent event
/// </summary>
public class InboxEventArgs : EventArgs
{
    /// <summary>
    /// Executing event.
    /// </summary>
    public IInboxEvent Event { get; }
    
    /// <summary>
    /// Type of event provider.
    /// </summary>
    public EventProviderType ProviderType { get; }
    
    /// <summary>
    /// The <see cref="IServiceProvider"/> used to resolve dependencies from the scope.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
    
    public InboxEventArgs(IInboxEvent @event, EventProviderType providerType, IServiceProvider serviceProvider)
    {
        Event = @event;
        ProviderType = providerType;
        ServiceProvider = serviceProvider;
    }
}