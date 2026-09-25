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
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    Task<bool> InsertEventAsync(TBaseMessage message);

    /// <summary>
    /// Inserts one or more new events into the database.
    /// </summary>
    /// <param name="events">Events to insert.</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    Task<bool> BulkInsertEventsAsync(TBaseMessage[] events);

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
    /// <returns>A list of unprocessed events that match the criteria.</returns>
    Task<TBaseMessage[]> GetUnprocessedEventsAsync(int limit = 500);

    /// <summary>
    /// Updates the specified Event properties.
    /// </summary>
    /// <param name="event">The event to update.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> UpdateEventAsync(TBaseMessage @event);

    /// <summary>
    /// Updates the specified events' properties.
    /// </summary>
    /// <param name="events">Events to update.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> UpdateEventsAsync(IEnumerable<TBaseMessage> events);
    
    /// <summary>
    /// Gets the current status of the event by its id.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <returns>Returns the status of the event or null if there is no event with the specified id.</returns>
    Task<EventStatus?> GetEventStatusByIdAsync(Guid id);

    /// <summary>
    /// Gets the event by its id.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <returns>Returns the event or null if there is no event with the specified id.</returns>
    Task<TBaseMessage> GetEventByIdAsync(Guid id);

    /// <summary>
    /// Gets a page of events that match the filter, sorted by the creation time in descending order.
    /// </summary>
    /// <param name="filter">The filter of the events.</param>
    /// <returns>Returns the events of the page and the total count of the events that match the filter.</returns>
    Task<(TBaseMessage[] Events, long TotalCount)> GetEventsAsync(EventsFilter filter);

    /// <summary>
    /// Deletes all processed events which processed before the specified date.
    /// </summary>
    /// <param name="processedAt">The processed date to filter records.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> DeleteProcessedEventsAsync(DateTime processedAt);
}