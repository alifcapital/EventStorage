using EventStorage.Configurations;
using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.BackgroundServices;

internal abstract class BaseCleanUpProcessedEventsJob<TEventRepository, TEventBox>(
    IServiceScopeFactory scopeFactory,
    InboxOrOutboxStructure settings,
    ILogger logger)
    : BackgroundService
    where TEventBox : class, IBaseMessageBox
    where TEventRepository : IBaseEventRepository<TEventBox>
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (settings.DaysToCleanUpEvents <= 0) return;

        await CreateEventStoreTablesIfNotExistsAsync(stoppingToken);

        var timeToDelay = TimeSpan.FromHours(settings.HoursToDelayCleanUpEvents);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<TEventRepository>();
                var processedAt = DateTime.Now.AddDays(-settings.DaysToCleanUpEvents);
                await repository.DeleteProcessedEventsAsync(processedAt);
            }
            catch (Exception e)
            {
                logger.LogCritical(e,
                    "Something is wrong while cleaning up the processed events from the {TableName} table. Happened at: {time}",
                    settings.TableName, DateTime.Now);
            }
            finally
            {
                if (!stoppingToken.IsCancellationRequested)
                    await Task.Delay(timeToDelay, stoppingToken);
            }
        }
    }

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