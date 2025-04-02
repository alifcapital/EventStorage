namespace EventStorage.Inbox.EventArgs;

using System;

/// <summary>
/// Event arguments to use before disposing the inbox event handler scope.
/// </summary>
public class EventHandlerArgs : EventArgs
{
    /// <summary>
    /// The name of executed event.
    /// </summary>
    public string EventName { get; init; }
    
    /// <summary>
    /// The name of event provider.
    /// </summary>
    public string EventProviderType { get; init; }
    
    /// <summary>
    /// The <see cref="IServiceProvider"/> used to resolve dependencies from the scope.
    /// </summary>
    public IServiceProvider ServiceProvider { get; init; }
}