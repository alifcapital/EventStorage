using EventStorage.Configurations;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.BackgroundServices;

/// <summary>
/// The base background service for creating table if not exists and process unprocessed events.
/// </summary>
/// <param name="scopeFactory">The service scope factory.</param>
/// <param name="eventsProcessor">The events executor service to process unprocessed events.</param>
/// <param name="functionalitySettings">The functionality settings for delay configuration.</param>
/// <param name="logger">The logger instance.</param>
internal abstract class BaseEventsProcessorJob(
    IServiceScopeFactory scopeFactory,
    IEventsProcessor eventsProcessor,
    InboxOrOutboxStructure functionalitySettings,
    ILogger logger)
    : BackgroundService
{
    #region ExecuteAsync

    private readonly TimeSpan _timeToDelay = TimeSpan.FromSeconds(functionalitySettings.SecondsToDelayProcessEvents);

    /// <summary>
    /// The method for executing unprocessed events in a loop with delay.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CreateEventStoreTablesIfNotExistsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await eventsProcessor.ExecuteUnprocessedEvents(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Something is wrong while processing events");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            finally
            {
                if (!stoppingToken.IsCancellationRequested)
                    await Task.Delay(_timeToDelay, stoppingToken);
            }
        }
    }

    #endregion

    #region CreateEventStoreTablesIfNotExists

    /// <summary>
    /// Creates event store tables if they do not already exist. It waits for a configured delay before attempting to create the tables.
    /// </summary>
    private async Task CreateEventStoreTablesIfNotExistsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventStoreTablesCreator = scope.ServiceProvider.GetRequiredService<IEventStoreTablesCreator>();
        await eventStoreTablesCreator.CreateTablesIfNotExistsAsync(cancellationToken);
    }

    #endregion
}