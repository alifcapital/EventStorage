using EventStorage.Configurations;
using EventStorage.Inbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class ReceivedEventsExecutorService(
    IServiceProvider services,
    IReceivedEventExecutor receivedEventExecutor,
    InboxAndOutboxSettings settings,
    ILogger<ReceivedEventsExecutorService> logger)
    : BackgroundService
{
    private readonly TimeSpan _timeToDelay = TimeSpan.FromSeconds(settings.Inbox.SecondsToDelayProcessEvents);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();
        inboxRepository.CreateTableIfNotExists();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await receivedEventExecutor.ExecuteUnprocessedEvents(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Something is wrong while receiving/updating an inbox events. Happened at: {time}",
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