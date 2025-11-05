using EventStorage.Configurations;
using EventStorage.Instrumentation;
using EventStorage.Outbox.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Repositories;

internal class OutboxRepository(ILogger<OutboxRepository> logger, InboxAndOutboxSettings settings)
    : EventRepository<OutboxMessage>(logger, settings.Outbox), IOutboxRepository
{
    protected override string TraceMessageTag => EventStorageInvestigationTagNames.OutboxEventTag;
}