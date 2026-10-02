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
    /// Retrieves all unprocessed events based on Provider, and TryAfterAt.
    /// </summary>
    /// <param name="limit">Get first 500 events.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of unprocessed events that match the criteria.</returns>
    Task<TBaseMessage[]> GetUnprocessedEventsAsync(int limit = 500, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the specified Event properties.
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