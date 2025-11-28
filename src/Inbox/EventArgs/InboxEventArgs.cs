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
    public required IInboxEvent Event { get; init; }

    /// <summary>
    /// The event handler type that will handle the event.
    /// </summary>
    public required Type EventHandlerType { get; init; }

    /// <summary>
    /// Type of event provider.
    /// </summary>
    public required EventProviderType ProviderType { get; init; }
    
    /// <summary>
    /// The <see cref="IServiceProvider"/> used to resolve dependencies from the scope.
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }
}