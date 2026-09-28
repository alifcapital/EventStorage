namespace EventStorage.Management.Models;

/// <summary>
/// The result of a manual action on an inbox/outbox event.
/// </summary>
public record EventActionResult
{
    /// <summary>
    /// The result status of the action.
    /// </summary>
    public EventActionResultStatus Status { get; private init; }

    /// <summary>
    /// The reason why the action is not successful. It is null when the action is successful.
    /// </summary>
    public string FailureReason { get; private init; }

    /// <summary>
    /// Whether the action is done successfully.
    /// </summary>
    public bool IsSuccess => Status == EventActionResultStatus.Success;

    internal static EventActionResult Success() => new() { Status = EventActionResultStatus.Success };

    internal static EventActionResult NotFound(Guid eventId) => new()
    {
        Status = EventActionResultStatus.NotFound,
        FailureReason = $"There is no event with the {eventId} id."
    };

    internal static EventActionResult AlreadyProcessing(Guid eventId) => new()
    {
        Status = EventActionResultStatus.AlreadyProcessing,
        FailureReason = $"The event with the {eventId} id is being processed at the moment. Try again later."
    };

    internal static EventActionResult InvalidState(string reason) => new()
    {
        Status = EventActionResultStatus.InvalidState,
        FailureReason = reason
    };

    internal static EventActionResult Failed(string failureReason) => new()
    {
        Status = EventActionResultStatus.Failed,
        FailureReason = failureReason
    };
}
