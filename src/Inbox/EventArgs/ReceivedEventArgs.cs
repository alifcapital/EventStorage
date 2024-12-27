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
    public required IReceiveEvent Event { get; init; }
    
    /// <summary>
    /// Type of event provider.
    /// </summary>
    public required EventProviderType ProviderType { get; init; }
}