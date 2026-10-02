using EventStorage.Models;

namespace EventStorage.Management.Models;

/// <summary>
/// The summary of an inbox/outbox event without its payload, headers and additional data, to list the events cheaply.
/// </summary>
public record EventSummary
{
    /// <summary>
    /// The id of the event.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The provider(s) of the event. The outbox events may have multiple providers separated by comma.
    /// </summary>
    public string Provider { get; init; }

    /// <summary>
    /// The name of the event.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// The full path (namespace) of the event type.
    /// </summary>
    public string EventPath { get; init; }

    /// <summary>
    /// The creation time of the event.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// The count of attempts to process the event.
    /// </summary>
    public int TryCount { get; init; }

    /// <summary>
    /// The time after which the event can be processed.
    /// </summary>
    public DateTime TryAfterAt { get; init; }

    /// <summary>
    /// The processing status of the event.
    /// </summary>
    public EventStatus Status { get; init; }

    /// <summary>
    /// The reason of the last processing failure of the event.
    /// </summary>
    public string FailureReason { get; init; }

    /// <summary>
    /// The time of the last status change. For the processed event, it is the processed time.
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// The user name of who changed the status manually. It is null when the status is changed by the processor.
    /// </summary>
    public string UpdatedBy { get; init; }
}
