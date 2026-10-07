using EventStorage.Management.Models;
using EventStorage.Models;
using EventStorage.Services;

namespace EventStorage.Repositories;

/// <summary>
/// The base event repository for working with the Inbox and Outbox events.
/// </summary>
internal interface IBaseEventRepository<TBaseMessage> : ITableCreator
    where TBaseMessage : IBaseMessageBox
{
    /// <summary>
    /// Inserts a new event into the database.
    /// </summary>
    /// <param name="message">The event to insert.</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    bool InsertEvent(TBaseMessage message);

    /// <summary>
    /// Inserts a new event into the database.
    /// </summary>
    /// <param name="message">The event to insert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    Task<bool> InsertEventAsync(TBaseMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts one or more new events into the database.
    /// </summary>
    /// <param name="events">Events to insert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    Task<bool> BulkInsertEventsAsync(TBaseMessage[] events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts one or more new events into the database.
    /// </summary>
    /// <param name="events">Events to insert.</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    bool BulkInsertEvents(TBaseMessage[] events);

    /// <summary>
    /// Retrieves the oldest Pending/Failed events whose try time has come, and the "Processing" events whose processing
    /// timeout has passed, with "FOR UPDATE SKIP LOCKED", and locks them by marking them as "Processing" until the
    /// processing timeout, so other instances skip them until they are stored, unlocked or the timeout passes.
    /// </summary>
    /// <param name="limit">The maximum number of events to lock.</param>
    /// <param name="processingTimeoutAt">The time after which the locked events are considered abandoned. See <see cref="Configurations.InboxOrOutboxStructure.GetProcessingTimeoutAt"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked events with their original status and try time (an abandoned event is returned as failed), ordered by the creation time.</returns>
    Task<TBaseMessage[]> LockUnprocessedEventsAsync(int limit, DateTime processingTimeoutAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the event with any status by its id for processing or changing it, if it is not "Processing" or its
    /// processing timeout has passed.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="processingTimeoutAt">The time after which the locked event is considered abandoned.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the locked event with its original status and try time, or null if there is no event with the specified id or it is already locked.</returns>
    Task<TBaseMessage> LockEventByIdAsync(Guid id, DateTime processingTimeoutAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the original status and try time of the locked events which are still locked by the same lock.
    /// The stored events (<see cref="UpdateEventAsync"/>) and the events locked again by others are not changed.
    /// </summary>
    /// <param name="events">The locked events with their original status and try time.</param>
    /// <param name="processingTimeoutAt">The processing timeout which was used to lock the events.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task UnlockEventsAsync(IEnumerable<TBaseMessage> events, DateTime processingTimeoutAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the specified Event properties. It also unlocks the locked event.
    /// </summary>
    /// <param name="event">The event to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> UpdateEventAsync(TBaseMessage @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the specified events' properties.
    /// </summary>
    /// <param name="events">Events to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> UpdateEventsAsync(IEnumerable<TBaseMessage> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the event by its id.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the status of the event or null if there is no event with the specified id.</returns>
    Task<EventStatus?> GetEventStatusByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the event by its id.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the event or null if there is no event with the specified id.</returns>
    Task<TBaseMessage> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the page of events by the filter, ordered by the creation time. Only the columns of the
    /// <see cref="EventSummary"/> are loaded.
    /// </summary>
    /// <param name="filter">The filter of the events with the valid page index and page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the page of event summaries which match the filter.</returns>
    Task<EventPagedList<EventSummary>> GetEventsAsync(EventsFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all processed events which processed before the specified date.
    /// </summary>
    /// <param name="processedAt">The processed date to filter records.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> DeleteProcessedEventsAsync(DateTime processedAt, CancellationToken cancellationToken = default);
}