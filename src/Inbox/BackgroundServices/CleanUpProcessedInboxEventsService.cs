using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.BackgroundServices;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class CleanUpProcessedInboxEventsService : CleanUpProcessedEventsService<IInboxRepository, InboxEvent>
{
    public CleanUpProcessedInboxEventsService(IServiceProvider services,
        InboxAndOutboxSettings settings, ILogger<EventsPublisherService> logger) : base(services, settings.Inbox,
        logger)
    {
    }
}