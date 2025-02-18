using EventStorage.Models;

namespace EventStorage.Inbox.Models;

public record ReceiversInformation
{
    /// <summary>
    /// The type of the received event.
    /// </summary>
    public required Type EventType { get; init; }
    
    /// <summary>
    /// The type of the event receivers.
    /// </summary>
    public List<Type> EventReceiverTypes { get; init; } = [];
    
    /// <summary>
    /// The provider type of the received event.
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