using EventStorage.Configurations;
using EventStorage.Inbox.Models;
using EventStorage.Instrumentation.Trace;
using EventStorage.Repositories;

namespace EventStorage.Inbox.Repositories;

internal class InboxRepository(InboxOrOutboxStructure settings)
    : EventRepository<InboxMessage>(settings), IInboxRepository
{
    protected override string TraceMessageTag => EventStorageTraceInstrumentation.InboxEventTag;
}