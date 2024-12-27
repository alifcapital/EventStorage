namespace EventStorage.Inbox;

/// <summary>
/// Service for executing an event handler of received events
/// </summary>
internal interface IReceivedEventExecutor
{
    /// <summary>
    /// For executing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);
}