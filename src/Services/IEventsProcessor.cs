namespace EventStorage.Services;

/// <summary>
/// Service for processing unprocessed events by executing event handlers of inbox or outbox events.
/// </summary>
internal interface IEventsProcessor
{
    /// <summary>
    /// For executing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);
}