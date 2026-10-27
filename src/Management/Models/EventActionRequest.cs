using InMemoryMessaging.Management.Models;

namespace EventStorage.Management.Models;

/// <summary>
/// The request of a manual action on an inbox/outbox event.
/// </summary>
public record EventActionRequest : MessageActionRequest
{
    /// <summary>
    /// To execute the event even if it is already processed. Default value is "false".
    /// Re-running a processed event may cause duplicate side effects, so it should be protected by its own permission.
    /// </summary>
    public bool Force { get; init; }
}
