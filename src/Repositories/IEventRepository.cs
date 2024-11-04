using EventStorage.Models;

namespace EventStorage.Repositories;

internal interface IEventRepository<TBaseEvent> where TBaseEvent :  class, IBaseEventBox
{
    /// <summary>
    /// Creates the table if it does not exist.
    /// </summary>
    void CreateTableIfNotExists();

    /// <summary>
    /// Inserts a new event into the database.
    /// </summary>
    /// <param name="event">The event to insert.</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    bool InsertEvent(TBaseEvent @event);
    
    /// <summary>
    /// Inserts one or more new events into the database.
    /// </summary>
    /// <param name="events">Events to insert.</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    bool BulkInsertEvents(IEnumerable<TBaseEvent> events);

    /// <summary>
    /// Retrieves all unprocessed events based on Provider, and TryAfterAt.
    /// </summary>
    /// <param name="limit">Get first 500 events.</param>
    /// <returns>A list of unprocessed events that match the criteria.</returns>
    Task<TBaseEvent[]> GetUnprocessedEventsAsync(int limit = 500);

    /// <summary>
    /// Updates the specified Event properties.
    /// </summary>
    /// <param name="event">The event to update.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> UpdateEventAsync(TBaseEvent @event);

    /// <summary>
    /// Updates the specified events' properties.
    /// </summary>
    /// <param name="events">Events to update.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> UpdateEventsAsync(IEnumerable<TBaseEvent> events);

    /// <summary>
    /// Deletes all processed events which processed before the specified date.
    /// </summary>
    /// <param name="processedAt">The processed date to filter records.</param>
    /// <returns>Returns true if there are any affected rows.</returns>
    Task<bool> DeleteProcessedEventsAsync(DateTime processedAt);
}