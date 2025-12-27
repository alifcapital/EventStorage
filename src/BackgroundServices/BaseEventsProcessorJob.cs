using EventStorage.Configurations;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.BackgroundServices;

/// <summary>
/// The base background service for creating table if not exists and process unprocessed events.
/// </summary>
/// <param name="services">The service provider to inject a table creator service.</param>
/// <param name="eventsProcessor">The events executor service to process unprocessed events.</param>
/// <param name="functionalitySettings">The functionality settings for delay configuration.</param>
/// <param name="logger">The logger instance.</param>
internal abstract class BaseEventsProcessorJob(
    IServiceProvider services,
    IEventsProcessor eventsProcessor,
    InboxOrOutboxStructure functionalitySettings,
    ILogger logger)
    : BackgroundService
{
    private readonly TimeSpan _timeToDelay = TimeSpan.FromSeconds(functionalitySettings.SecondsToDelayProcessEvents);

    #region StartAsync
    
    /// <summary>
    /// The method for creating the table if not exists before starting the application.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(functionalitySettings.SecondsToDelayBeforeProcessingEvents), cancellationToken);
        
        using var scope = services.CreateScope();
        var tableCreator = GetTableCreatorService(scope.ServiceProvider);
        tableCreator.CreateTableIfNotExists();

        await base.StartAsync(cancellationToken);
    }

    #endregion

    #region GetTableCreatorService
    
    /// <summary>
    /// Gets the table creator service from the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to get the table creator service.</param>
    /// <returns>Returns the table creator service.</returns>
    protected abstract ITableCreator GetTableCreatorService(IServiceProvider serviceProvider);

    #endregion

    #region ExecuteAsync
    
    /// <summary>
    /// The method for executing unprocessed events in a loop with delay.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
}