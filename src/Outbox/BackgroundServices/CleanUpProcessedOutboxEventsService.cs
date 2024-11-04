using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class CleanUpProcessedOutboxEventsService : CleanUpProcessedEventsService<IOutboxRepository, OutboxEvent>
{
    public CleanUpProcessedOutboxEventsService(IServiceProvider services,
        InboxAndOutboxSettings settings, ILogger<EventsPublisherService> logger) : base(services, settings.Outbox,
        logger)
    {
    }
}