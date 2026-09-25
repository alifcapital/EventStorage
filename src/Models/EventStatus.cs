namespace EventStorage.Models;

/// <summary>
/// The processing status of an inbox/outbox event. It is stored as a string in the "status" column.
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// The event is waiting to be processed.
    /// </summary>
    Pending,

    /// <summary>
    /// Processing of the event failed; it will be retried once its "try_after_at" time comes.
    /// </summary>
    Failed,

    /// <summary>
    /// The event is processed.
    /// </summary>
    Processed,

    /// <summary>
    /// The event is ignored and will not be processed.
    /// </summary>
    Rejected
}
