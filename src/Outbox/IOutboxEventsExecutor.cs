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
}