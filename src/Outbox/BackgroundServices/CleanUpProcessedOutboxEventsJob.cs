using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class CleanUpProcessedOutboxEventsJob : BaseCleanUpProcessedEventsJob<IOutboxRepository, OutboxMessage>
{
    public CleanUpProcessedOutboxEventsJob(IServiceProvider services,
        InboxAndOutboxSettings settings, ILogger<OutboxEventsProcessorJob> logger) : base(services, settings.Outbox,
        logger)
    {
    }
}