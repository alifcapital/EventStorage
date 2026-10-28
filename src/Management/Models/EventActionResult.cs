using InMemoryMessaging.Management.Models;

namespace EventStorage.Management.Models;

/// <summary>
/// The result of a manual action on an inbox/outbox event.
/// </summary>
public record EventActionResult : MessageActionResult
{
    protected override string FunctionalityName => "Event storage";

    #region Factory Methods

    internal static EventActionResult Success() => Success<EventActionResult>();

    internal static EventActionResult NotFound(Guid eventId) => NotFound<EventActionResult>(eventId);

    internal static EventActionResult AlreadyProcessing(Guid eventId) => AlreadyProcessing<EventActionResult>(eventId);

    internal static EventActionResult InvalidState(string reason) => InvalidState<EventActionResult>(reason);

    internal static EventActionResult Failed(string failureReason) => Failed<EventActionResult>(failureReason);

    #endregion
}
