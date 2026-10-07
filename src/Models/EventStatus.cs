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
    Rejected,

    /// <summary>
    /// The event is fetched by a processor or locked by a manual action at the moment, so others skip it.
    /// Its "try_after_at" is the time after which it is considered abandoned (for example, the instance stopped
    /// before storing its result) and fetched again.
    /// </summary>
    Processing
}
