namespace EventStorage.Inbox;

/// <summary>
/// Manager of event receiver
/// </summary>
internal interface IEventsReceiverManager
{
    /// <summary>
    /// For executing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);
}