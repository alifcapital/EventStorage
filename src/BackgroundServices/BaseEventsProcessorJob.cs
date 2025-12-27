using EventStorage.Configurations;
using EventStorage.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.BackgroundServices;

/// <summary>
/// The base background service for creating table if not exists and process unprocessed events.
/// </summary>
/// <param name="eventStoreTablesCreator">The event store tables creator service.</param>
/// <param name="eventsProcessor">The events executor service to process unprocessed events.</param>
/// <param name="functionalitySettings">The functionality settings for delay configuration.</param>
/// <param name="logger">The logger instance.</param>
internal abstract class BaseEventsProcessorJob(
    IEventStoreTablesCreator eventStoreTablesCreator,
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
                await Task.Delay(_timeToDelay, stoppingToken);
            }
        }
    }

    #endregion

    #region CreateEventStoreTablesIfNotExists

    /// <summary>
    /// Semaphore to limit the number of concurrent table creation to 1.
    /// This is to prevent multiple instances of the application from trying to run migrations at the same time.
    /// </summary>
    private static readonly SemaphoreSlim LimitToExecuteTableCreation = new(1, 1);

    /// <summary>
    /// Creates event store tables if they do not already exist. It waits for a configured delay before attempting to create the tables.
    /// </summary>
    private async Task CreateEventStoreTablesIfNotExistsAsync(CancellationToken cancellationToken)
    {
        var timeToDelay = TimeSpan.FromSeconds(functionalitySettings.SecondsToDelayBeforeCreateEventStoreTables);
        await Task.Delay(timeToDelay, cancellationToken);
        await LimitToExecuteTableCreation.WaitAsync(cancellationToken);
        
        try
        {
            eventStoreTablesCreator.CreateTablesIfNotExists();
        }
        finally
        {
            LimitToExecuteTableCreation.Release();
        }
    }

    #endregion
}