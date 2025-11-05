namespace EventStorage.Services;

/// <summary>
/// Service for executing event handlers of inbox or outbox events.
/// </summary>
internal interface IEventsExecutor
{
    /// <summary>
    /// For executing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);
}