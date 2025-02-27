using EventStorage.Configurations;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class OutboxEventsExecutorJob(
    IServiceProvider services,
    IOutboxEventsExecutor outboxEventsExecutor,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsExecutorJob> logger)
    : BackgroundService
{
    private readonly TimeSpan _timeToDelay = TimeSpan.FromSeconds(settings.Outbox.SecondsToDelayProcessEvents);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        outboxRepository.CreateTableIfNotExists();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await outboxEventsExecutor.ExecuteUnprocessedEvents(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Something is wrong while publishing/updating an outbox events. Happened at: {time}",
                    DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            finally
            {
                await Task.Delay(_timeToDelay, stoppingToken);
            }
        }
    }
}