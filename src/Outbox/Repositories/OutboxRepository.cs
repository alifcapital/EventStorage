using EventStorage.Configurations;
using EventStorage.Instrumentation;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Repositories;

internal class OutboxRepository(ILogger<OutboxRepository> logger, InboxAndOutboxSettings settings)
    : BaseEventRepository<OutboxMessage>(logger, settings.Outbox, settings.SecondsToWaitForMigrationLock),
        IOutboxRepository
{
    protected override string TraceMessageTag => EventStorageInvestigationTagNames.OutboxEventTag;

    #region Overriden queries

    /// <summary>
    /// Since the outbox message does not have a property naming policy, the table is created without a column for that.
    /// </summary>
    internal override bool HasNamingPolicyColumn => false;

    /// <summary>
    /// The SQL query for inserting a new event to the database without the naming policy column.
    /// </summary>
    protected override string SqlQueryToInsertEvent => $@"
                INSERT INTO {TableName} (
                    id, provider, event_name, event_path, payload, headers, 
                    additional_data, created_at, try_count, try_after_at, status
                ) VALUES (
                    @Id, @Provider, @EventName, @EventPath, @Payload::jsonb, @Headers,
                    @AdditionalData, @CreatedAt, @TryCount, @TryAfterAt, @StatusName
                )";
    
    /// <summary>
    /// The SQL query for getting unprocessed outbox events without the naming policy column.
    /// </summary>
    protected override string SqlQueryToGetUnprocessedEvents => $@"
                SELECT id as ""{nameof(OutboxMessage.Id)}"", provider as ""{nameof(OutboxMessage.Provider)}"", 
                        event_name as ""{nameof(OutboxMessage.EventName)}"", event_path as ""{nameof(OutboxMessage.EventPath)}"", 
                        payload::text as ""{nameof(OutboxMessage.Payload)}"", headers as ""{nameof(OutboxMessage.Headers)}"",
                        additional_data as ""{nameof(OutboxMessage.AdditionalData)}"", created_at as ""{nameof(OutboxMessage.CreatedAt)}"", 
                        try_count as ""{nameof(OutboxMessage.TryCount)}"", try_after_at as ""{nameof(OutboxMessage.TryAfterAt)}"", 
                        status as ""{nameof(OutboxMessage.Status)}"", failure_reason as ""{nameof(OutboxMessage.FailureReason)}"",
                        updated_at as ""{nameof(OutboxMessage.UpdatedAt)}"", updated_by as ""{nameof(OutboxMessage.UpdatedBy)}"",
                        status_comment as ""{nameof(OutboxMessage.StatusComment)}""
                FROM {TableName}
                WHERE 
                    status IN ('{nameof(EventStatus.Pending)}', '{nameof(EventStatus.Failed)}')
                    AND try_after_at <= @CurrentTime
                ORDER BY created_at ASC
                LIMIT @Limit";

    #endregion
}