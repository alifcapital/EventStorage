namespace EventStorage.Outbox;

/// <summary>
/// Service to execute a publisher of events.
/// </summary>
internal interface IPublishingEventExecutor
{
    /// <summary>
    /// For publishing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);
}