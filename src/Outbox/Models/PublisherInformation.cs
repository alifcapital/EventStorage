using System.Reflection;

namespace EventStorage.Outbox.Models;

internal record PublisherInformation
{
    /// <summary>
    /// The type of the publishing event.
    /// </summary>
    public required Type EventType { get; init; }
    
    /// <summary>
    /// The type of the event publisher.
    /// </summary>
    public required Type EventPublisherType { get; init; }
    
    /// <summary>
    /// The publish method of the event publisher.
    /// </summary>
    public required MethodInfo PublishMethod { get; init; }
    
    /// <summary>
    /// The provider type of the publishing event.
    /// </summary>
    public required string ProviderType { get; init; }
    
    /// <summary>
    /// Represents whether the event has headers.
    /// </summary>
    public required bool HasHeaders { get; init; }
    
    /// <summary>
    /// Represents whether the event has additional data.
    /// </summary>
    public required bool HasAdditionalData { get; init; }
    
    /// <summary>
    /// Represents whether the event has a specific publisher or not. When it is true, the event will use the global publisher.
    /// </summary>
    public required bool IsGlobalPublisher { get; init; }
}