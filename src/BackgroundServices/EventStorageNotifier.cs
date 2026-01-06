using EventStorage.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.BackgroundServices;

/// <summary>
/// The background service for notifying about event storage operations' status.
/// </summary>
/// <param name="logger">The logger instance.</param>
internal class EventStorageNotifier(InboxAndOutboxSettings settings, ILogger<EventStorageNotifier> logger)
    : BackgroundService
{
    #region ExecuteAsync

    /// <summary>
    /// Shows informational logs if Inbox or Outbox functionalities are disabled.
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(!settings.Inbox.IsEnabled)
            logger.LogInformation("Since the Inbox functionality is disabled, storing events will be skipped.");
        
        if(!settings.Outbox.IsEnabled)
            logger.LogInformation("Since the Outbox functionality is disabled, storing events will be skipped.");
        
        return Task.CompletedTask;
    }

    #endregion
}