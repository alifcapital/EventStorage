using EventStorage.Configurations;
using EventStorage.Instrumentation;
using EventStorage.Outbox.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Repositories;

internal class OutboxRepository(ILogger<OutboxRepository> logger, InboxAndOutboxSettings settings)
    : BaseEventRepository<OutboxMessage>(logger, settings.Outbox, settings.SecondsToWaitForMigrationLock),
        IOutboxRepository
{
    protected override string TraceMessageTag => EventStorageInvestigationTagNames.OutboxEventTag;

    /// <summary>
    /// Since the outbox message does not have a property naming policy, the table does not have a column for that.
    /// </summary>
    internal override bool HasNamingPolicyColumn => false;
}
