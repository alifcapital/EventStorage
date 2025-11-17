using EventStorage.Configurations;
using EventStorage.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.BackgroundServices;

internal abstract class BaseCleanUpProcessedEventsJob<TEventRepository, TEventBox> : BackgroundService
    where TEventBox : class, IBaseMessageBox
    where TEventRepository : IBaseEventRepository<TEventBox>
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly InboxOrOutboxStructure _settings;

    protected BaseCleanUpProcessedEventsJob(IServiceProvider services,
        InboxOrOutboxStructure settings, ILogger logger)
    {
        _services = services;
        _logger = logger;
        _settings = settings;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TEventRepository>();
        repository.CreateTableIfNotExists();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.DaysToCleanUpEvents <= 0) return;
        
        var timeToDelay = TimeSpan.FromHours(_settings.HoursToDelayCleanUpEvents);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<TEventRepository>();
                var processedAt = DateTime.Now.AddDays(-_settings.DaysToCleanUpEvents);
                await repository.DeleteProcessedEventsAsync(processedAt);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e,
                    "Something is wrong while cleaning up the processed events from the {TableName} table. Happened at: {time}",
                    _settings.TableName, DateTime.Now);
            }
            finally
            {
                await Task.Delay(timeToDelay, stoppingToken);
            }
        }
    }
}