using EventStorage.Configurations;
using EventStorage.Inbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class EventsReceiverService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IEventsReceiverManager _eventsReceiverManager;
    private readonly ILogger<EventsReceiverService> _logger;
    private readonly TimeSpan _timeToDelay;

    public EventsReceiverService(IServiceProvider services, IEventsReceiverManager eventsReceiverManager,
        InboxAndOutboxSettings settings, ILogger<EventsReceiverService> logger)
    {
        _services = services;
        _eventsReceiverManager = eventsReceiverManager;
        _logger = logger;
        _timeToDelay = TimeSpan.FromSeconds(settings.Inbox.SecondsToDelayProcessEvents);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
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
                await _eventsReceiverManager.ExecuteUnprocessedEvents(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Something is wrong while receiving/updating an inbox events. Happened at: {time}",
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