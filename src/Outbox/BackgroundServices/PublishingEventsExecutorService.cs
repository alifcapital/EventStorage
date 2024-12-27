using EventStorage.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class PublishingEventsExecutorService(
    IPublishingEventExecutor publishingEventExecutor,
    InboxAndOutboxSettings settings,
    ILogger<PublishingEventsExecutorService> logger)
    : BackgroundService
{
    private readonly TimeSpan _timeToDelay = TimeSpan.FromSeconds(settings.Outbox.SecondsToDelayProcessEvents);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await publishingEventExecutor.ExecuteUnprocessedEvents(stoppingToken);
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