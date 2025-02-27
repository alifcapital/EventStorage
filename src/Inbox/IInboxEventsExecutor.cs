namespace EventStorage.Inbox;

/// <summary>
/// Service for executing event handlers of inbox events.
/// </summary>
internal interface IInboxEventsExecutor
{
    /// <summary>
    /// For executing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);
}