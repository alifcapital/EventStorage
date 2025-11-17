using EventStorage.Configurations;
using EventStorage.Inbox.Models;
using EventStorage.Instrumentation;
using EventStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.Repositories;

internal class InboxRepository(ILogger<InboxRepository> logger, InboxAndOutboxSettings settings)
    : BaseEventRepository<InboxMessage>(logger, settings.Inbox), IInboxRepository
{
    protected override string TraceMessageTag => EventStorageInvestigationTagNames.InboxEventTag;
}