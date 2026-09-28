namespace EventStorage.Models;

/// <summary>
/// The allowed transitions between the statuses of an event. Any other transition is not allowed.
/// </summary>
internal static class EventStatusTransitions
{
    /// <summary>
    /// Pending/Failed events can be executed. Processed events can be executed only when it is forced.
    /// </summary>
    internal static bool CanBeExecuted(EventStatus status, bool force) =>
        status is EventStatus.Pending or EventStatus.Failed || (force && status == EventStatus.Processed);

    /// <summary>
    /// Pending/Failed events can be rejected.
    /// </summary>
    internal static bool CanBeRejected(EventStatus status) =>
        status is EventStatus.Pending or EventStatus.Failed;

    /// <summary>
    /// Pending/Failed/Rejected events can be rescheduled to be pending again.
    /// </summary>
    internal static bool CanBeRescheduled(EventStatus status) =>
        status is EventStatus.Pending or EventStatus.Failed or EventStatus.Rejected;

    /// <summary>
    /// Pending/Failed/Rejected events can be marked as processed manually.
    /// </summary>
    internal static bool CanBeMarkedAsProcessed(EventStatus status) =>
        status is EventStatus.Pending or EventStatus.Failed or EventStatus.Rejected;
}
