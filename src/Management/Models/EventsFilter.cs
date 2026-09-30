using EventStorage.Models;

namespace EventStorage.Management.Models;

/// <summary>
/// The filter to get inbox/outbox events. All filters are optional and combined with AND.
/// The events are ordered by the creation time and paged by the page index.
/// </summary>
public record EventsFilter
{
    /// <summary>
    /// The default count of events to return in a page.
    /// </summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Returns only the events with the given status. If it is null, the events of all statuses are returned.
    /// </summary>
    public EventStatus? Status { get; init; }

    /// <summary>
    /// Returns only the events with the given name.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// Returns only the events with the given provider. The outbox events with multiple providers (separated by comma)
    /// are returned if one of them matches.
    /// </summary>
    public EventProviderType? EventProviderType { get; init; }

    /// <summary>
    /// Returns only the events created at or after the given time.
    /// </summary>
    public DateTime? CreatedFrom { get; init; }

    /// <summary>
    /// Returns only the events created at or before the given time.
    /// </summary>
    public DateTime? CreatedTo { get; init; }

    /// <summary>
    /// Returns only the events whose status was changed at or after the given time.
    /// </summary>
    public DateTime? UpdatedFrom { get; init; }

    /// <summary>
    /// Returns only the events whose status was changed at or before the given time.
    /// </summary>
    public DateTime? UpdatedTo { get; init; }

    /// <summary>
    /// Returns only the events whose status was changed manually by the user whose name contains the given text
    /// (case-insensitive), so a part of the full name such as the first name is enough.
    /// </summary>
    public string UpdatedBy { get; init; }

    /// <summary>
    /// Returns only the events with at least the given count of processing attempts.
    /// </summary>
    public int? MinTryCount { get; init; }

    /// <summary>
    /// Returns only the events whose failure reason contains the given text (case-insensitive).
    /// It is not indexed, so it should be combined with other filters on big tables.
    /// </summary>
    public string FailureReasonContains { get; init; }

    /// <summary>
    /// Returns only the events whose payload contains the given text (case-insensitive), for example the id of an entity.
    /// The payload is searched as the JSON text in the format of PostgreSQL, which has a space after the colon and
    /// comma: {"Id": 1, "Name": "Test"}.
    /// It is not indexed, so it should be combined with other filters on big tables.
    /// </summary>
    public string PayloadContains { get; init; }

    /// <summary>
    /// The index of the page, starting from 1. If it is less than 1, the first page is returned.
    /// </summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>
    /// The count of events to return in a page. If it is less than 1, the <see cref="DefaultPageSize"/> is used.
    /// </summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// Whether to return the newest events first. Default is true.
    /// </summary>
    public bool SortDescending { get; init; } = true;
}