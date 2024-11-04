using EventStorage.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class EventsPublisherService : BackgroundService
{
    private readonly IEventsPublisherManager _eventsPublisherManager;
    private readonly ILogger<EventsPublisherService> _logger;
    private readonly TimeSpan _timeToDelay;

    public EventsPublisherService(IEventsPublisherManager eventsPublisherManager,
        InboxAndOutboxSettings settings, ILogger<EventsPublisherService> logger)
    {
        _eventsPublisherManager = eventsPublisherManager;
        _logger = logger;
        _timeToDelay = TimeSpan.FromSeconds(settings.Outbox.SecondsToDelayProcessEvents);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _eventsPublisherManager.ExecuteUnprocessedEvents(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Something is wrong while publishing/updating an outbox events. Happened at: {time}",
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