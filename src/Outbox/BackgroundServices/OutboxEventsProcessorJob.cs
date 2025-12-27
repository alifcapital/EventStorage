using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Outbox.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class OutboxEventsProcessorJob(
    IEventStoreTablesCreator eventStoreTablesCreator,
    IOutboxEventsProcessor outboxEventsProcessor,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsProcessorJob> logger)
    : BaseEventsProcessorJob(eventStoreTablesCreator, outboxEventsProcessor, settings.Outbox, logger)
{
}