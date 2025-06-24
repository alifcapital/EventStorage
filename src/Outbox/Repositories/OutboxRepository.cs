using EventStorage.Configurations;
using EventStorage.Instrumentation.Trace;
using EventStorage.Outbox.Models;
using EventStorage.Repositories;

namespace EventStorage.Outbox.Repositories;

internal class OutboxRepository(InboxOrOutboxStructure settings)
    : EventRepository<OutboxMessage>(settings), IOutboxRepository
{
    protected override string TraceMessageTag => EventStorageTraceInstrumentation.OutboxEventTag;
}