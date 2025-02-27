using System.Reflection;
using EventStorage.Inbox.Providers;
using EventStorage.Models;

namespace EventStorage.Inbox.Models;

internal record EventHandlerInformation
{
    /// <summary>
    /// The type of the inbox event.
    /// </summary>
    public required Type EventType { get; init; }
    
    /// <summary>
    /// The type of the event handler.
    /// </summary>
    public required Type EventHandlerType { get; init; }
    
    /// <summary>
    /// The handle method of the event handler.
    /// </summary>
    public required MethodInfo HandleMethod { get; init; }
    
    /// <summary>
    /// The provider type of the inbox event.
    /// </summary>
    public required EventProviderType ProviderType { get; init; }
    
    /// <summary>
    /// Represents whether the event has headers.
    /// </summary>
    public required bool HasHeaders { get; init; }
    
    /// <summary>
    /// Represents whether the event has additional data.
    /// </summary>
    public required bool HasAdditionalData { get; init; }
}