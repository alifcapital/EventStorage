namespace EventStorage.Outbox;

/// <summary>
/// Manager of event publisher
/// </summary>
internal interface IEventsPublisherManager
{
    /// <summary>
    /// For publishing unprocessed events
    /// </summary>
    Task ExecuteUnprocessedEvents(CancellationToken stoppingToken);
}