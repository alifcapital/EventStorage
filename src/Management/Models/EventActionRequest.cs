namespace EventStorage.Management.Models;

/// <summary>
/// The request of a manual action on an inbox/outbox event.
/// </summary>
public record EventActionRequest
{
    /// <summary>
    /// The user name of who performs the action.
    /// </summary>
    public string PerformedBy { get; init; }

    /// <summary>
    /// The reason of the action.
    /// </summary>
    public string Comment { get; init; }

    /// <summary>
    /// To execute the event even if it is already processed. Default value is "false".
    /// Re-running a processed event may cause duplicate side effects, so it should be protected by its own permission.
    /// </summary>
    public bool Force { get; init; }
}
