namespace EventStorage.Management.Models;

/// <summary>
/// The result status of a manual action on an inbox/outbox event.
/// </summary>
public enum EventActionResultStatus
{
    /// <summary>
    /// The action is done successfully.
    /// </summary>
    Success,

    /// <summary>
    /// There is no event with the given id.
    /// </summary>
    NotFound,

    /// <summary>
    /// The event is being processed by the processor or by another action at the moment.
    /// </summary>
    AlreadyProcessing,

    /// <summary>
    /// The action is not allowed for the current status of the event.
    /// </summary>
    InvalidState,

    /// <summary>
    /// The event is executed, but it failed. See the <see cref="EventActionResult.FailureReason"/>.
    /// </summary>
    Failed
}