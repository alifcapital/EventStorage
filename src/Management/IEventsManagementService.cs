using EventStorage.Management.Models;

namespace EventStorage.Management;

/// <summary>
/// The service for viewing and managing inbox/outbox events by the external application.
/// The library does not apply any authorization, so the host application must protect each operation with its own permissions.
/// All methods throw an <see cref="Exceptions.EventStoreException"/> if the functionality is not enabled.
/// </summary>
public interface IEventsManagementService
{
    /// <summary>
    /// Gets the event by its id.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the event or null if there is no event with the given id.</returns>
    Task<EventDetails> GetEventByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the page of events by the filter, ordered by the creation time. The events are returned without the large
    /// data such as the payload, which can be got by the <see cref="GetEventByIdAsync"/>.
    /// The <see cref="EventPagedList{TItem}.HasNextPage"/> shows whether the next page exists.
    /// </summary>
    /// <param name="filter">The filter of the events. Null to get the first page of all events with the default page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventPagedList<EventDetails>> GetEventsAsync(EventsFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the Pending/Failed event right now and waits for the result. A processed event is executed only with
    /// the <see cref="EventActionRequest.Force"/> option, since re-running it may cause duplicate side effects.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="request">The information of the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventActionResult> ExecuteAsync(Guid id, EventActionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Makes the rejected event pending again, to be processed at the given time.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="tryAfterAt">The time after which the event should be processed.</param>
    /// <param name="request">The information of the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventActionResult> RescheduleAsync(Guid id, DateTime tryAfterAt, EventActionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rejects the Pending/Failed event, so it will not be processed.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="request">The information of the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventActionResult> RejectAsync(Guid id, EventActionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks the Pending/Failed/Rejected event as processed without executing it.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="request">The information of the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventActionResult> MarkAsProcessedAsync(Guid id, EventActionRequest request,
        CancellationToken cancellationToken);
}
