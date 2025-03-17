using EventStorage.Models;

namespace EventStorage.Outbox;

/// <summary>
/// Service for executing event handlers of outbox events.
/// </summary>
internal interface IOutboxEventsExecutor
{
    /// <summary>
    /// For publishing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);

    /// <summary>
    /// Get the publisher types of the event.
    /// </summary>
    /// <param name="eventName">Type name of outbox event</param>
    /// <returns>Array of outbox publisher types. If there is no publisher, return null.</returns>
    IEnumerable<EventProviderType> GetEventPublisherTypes(string eventName);
}