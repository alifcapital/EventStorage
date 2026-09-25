using EventStorage.Management.Models;

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

/// <summary>
/// Service for processing inbox or outbox events, including processing a single event.
/// </summary>
/// <typeparam name="TMessage">The type of the inbox or outbox message.</typeparam>
internal interface IEventsProcessor<in TMessage> : IEventsProcessor
{
    /// <summary>
    /// Processes a single event under its distributed lock: re-checks its current status, executes it and stores the result.
    /// It is used by both the background processing and the manual execution of the management service.
    /// </summary>
    /// <param name="message">The event to process.</param>
    /// <param name="manualRequest">The request of the manual execution. Null when it is processed by the background processing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the processing.</returns>
    Task<EventActionResult> ProcessSingleEventAsync(TMessage message, EventActionRequest manualRequest,
        CancellationToken cancellationToken);
}
