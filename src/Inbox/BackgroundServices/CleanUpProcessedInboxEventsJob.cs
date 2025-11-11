using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.BackgroundServices;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class CleanUpProcessedInboxEventsJob : BaseCleanUpProcessedEventsJob<IInboxRepository, InboxMessage>
{
    public CleanUpProcessedInboxEventsJob(IServiceProvider services,
        InboxAndOutboxSettings settings, ILogger<OutboxEventsProcessorJob> logger) : base(services, settings.Inbox,
        logger)
    {
    }
}