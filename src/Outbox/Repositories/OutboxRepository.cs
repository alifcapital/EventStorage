using EventStorage.Configurations;
using EventStorage.Instrumentation;
using EventStorage.Outbox.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.Repositories;

internal class OutboxRepository(ILogger<OutboxRepository> logger, InboxAndOutboxSettings settings)
    : BaseEventRepository<OutboxMessage>(logger, settings.Outbox), IOutboxRepository
{
    protected override string TraceMessageTag => EventStorageInvestigationTagNames.OutboxEventTag;

    #region Overriden queries

    /// <summary>
    /// Since the outbox message does not have a property naming policy, we override the base class implementation to not create column for that.
    /// </summary>
    protected override string CreateTableSqlScript => $@"CREATE TABLE IF NOT EXISTS {TableName}
                (
                    id UUID NOT NULL PRIMARY KEY,
                    provider VARCHAR(50) NOT NULL,
                    event_name VARCHAR(100) NOT NULL,
                    event_path VARCHAR(255),
                    payload TEXT,
                    headers TEXT,
                    additional_data TEXT,
                    created_at TIMESTAMP(0) NOT NULL,
                    try_count integer DEFAULT 0 NOT NULL,
                    try_after_at TIMESTAMP(0) NOT NULL,
                    processed_at TIMESTAMP(0) DEFAULT NULL
                );";

    /// <summary>
    /// The SQL query for inserting a new event to the database without the naming policy column.
    /// </summary>
    protected override string SqlQueryToInsertEvent => $@"
                INSERT INTO {TableName} (
                    id, provider, event_name, event_path, payload, headers, 
                    additional_data, created_at, try_count, try_after_at
                ) VALUES (
                    @Id, @Provider, @EventName, @EventPath, @Payload, @Headers, 
                    @AdditionalData, @CreatedAt, @TryCount, @TryAfterAt
                )";
    
    /// <summary>
    /// The SQL query for getting unprocessed outbox events without the naming policy column.
    /// </summary>
    protected override string SqlQueryToGetUnprocessedEvents => $@"
                SELECT id as ""{nameof(OutboxMessage.Id)}"", provider as ""{nameof(OutboxMessage.Provider)}"", 
                        event_name as ""{nameof(OutboxMessage.EventName)}"", event_path as ""{nameof(OutboxMessage.EventPath)}"", 
                        payload as ""{nameof(OutboxMessage.Payload)}"", headers as ""{nameof(OutboxMessage.Headers)}"", 
                        additional_data as ""{nameof(OutboxMessage.AdditionalData)}"", created_at as ""{nameof(OutboxMessage.CreatedAt)}"", 
                        try_count as ""{nameof(OutboxMessage.TryCount)}"", try_after_at as ""{nameof(OutboxMessage.TryAfterAt)}"", 
                        processed_at as ""{nameof(OutboxMessage.ProcessedAt)}""
                FROM {TableName}
                WHERE 
                    processed_at IS NULL
                    AND try_after_at <= @CurrentTime
                ORDER BY created_at ASC
                LIMIT @Limit";

    #endregion
}