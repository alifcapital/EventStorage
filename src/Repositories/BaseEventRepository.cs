using System.Diagnostics;
using Dapper;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Instrumentation.Trace;
using EventStorage.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventStorage.Repositories;

/// <summary>
/// The base event repository for working with the Inbox and Outbox events.
/// </summary>
/// <param name="logger">The logger instance.</param>
/// <param name="settings">The inbox or outbox settings.</param>
/// <param name="secondsToWaitForMigrationLock">Seconds to wait for the exclusive lock of the table while migrating its old schema.</param>
/// <typeparam name="TBaseMessage">The type of the message to store.</typeparam>
internal abstract class BaseEventRepository<TBaseMessage>(
    ILogger logger,
    InboxOrOutboxStructure settings,
    int secondsToWaitForMigrationLock)
    : IBaseEventRepository<TBaseMessage>
    where TBaseMessage : class, IBaseMessageBox
{
    private readonly string _connectionString = settings.ConnectionString;

    /// <summary>
    /// The name of the table for connecting repository to the correct table in the database.
    /// </summary>
    protected readonly string TableName = settings.TableName;

    /// <summary>
    /// The tag/prefix of the trace message for logging purposes.
    /// </summary>
    protected abstract string TraceMessageTag { get; }

    #region Create tables or indexes if not exists

    /// <summary>
    /// The SQL script for creating the table for storing events if it does not exist.
    /// </summary>
    protected virtual string CreateTableSqlScript => $@"CREATE TABLE IF NOT EXISTS {TableName}
                (
                    id UUID NOT NULL PRIMARY KEY,
                    provider VARCHAR(50) NOT NULL,
                    event_name VARCHAR(100) NOT NULL,
                    event_path VARCHAR(255),
                    payload JSONB,
                    headers TEXT,
                    additional_data TEXT,
                    naming_policy_type VARCHAR(15),
                    created_at TIMESTAMP(0) NOT NULL,
                    try_count integer DEFAULT 0 NOT NULL,
                    try_after_at TIMESTAMP(0) NOT NULL,
                    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
                    failure_reason TEXT,
                    updated_at TIMESTAMP(0),
                    updated_by VARCHAR(100),
                    status_comment TEXT
                );";

    /// <summary>
    /// The SQL script for migrating the payload column from text to jsonb type if the column exists and has text type.
    /// This is for supporting the old versions of the library which used text type for payload column.
    /// </summary>
    private string MigratePayloadColumnToJsonbScript => $@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = lower('{TableName}')
                            AND column_name = 'payload'
                            AND data_type = 'text'
                    ) THEN
                        ALTER TABLE {TableName} ALTER COLUMN payload TYPE JSONB USING payload::jsonb;
                    END IF;
                END
                $$;";

    /// <summary>
    /// The SQL script for creating indexes for the table: for getting unprocessed events, for deleting processed events
    /// and for filtering events by the creation time and the event name.
    /// </summary>
    private string CreateIndexesScript => $@"CREATE INDEX IF NOT EXISTS idx_{TableName}_status_try_after_at
                    ON public.{TableName} (status, try_after_at);

                CREATE INDEX IF NOT EXISTS idx_{TableName}_status_updated_at
                    ON public.{TableName} (status, updated_at);

                CREATE INDEX IF NOT EXISTS idx_{TableName}_created_at
                    ON public.{TableName} (created_at);

                CREATE INDEX IF NOT EXISTS idx_{TableName}_event_name_created_at
                    ON public.{TableName} (event_name, created_at);";

    public void CreateTableIfNotExists()
    {
        try
        {
            using var dbConnection = new NpgsqlConnection(_connectionString);
            dbConnection.Open();

            dbConnection.Execute(CreateTableSqlScript);
            dbConnection.Execute(MigratePayloadColumnToJsonbScript);
            MigrateToStatusSchemaIfNeeded(dbConnection);
            dbConnection.Execute(CreateIndexesScript);
        }
        catch (EventStoreException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while checking/creating {TableName} table.");
        }
    }

    #endregion

    #region Migrate to status schema

    /// <summary>
    /// The SQL query for checking whether the table already has the "status" column. If not, the table has the old schema.
    /// </summary>
    private string SqlQueryToCheckStatusColumnExists => $@"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                        AND table_name = lower('{TableName}')
                        AND column_name = 'status'
                )";

    /// <summary>
    /// The SQL script for adding all new columns of the status schema.
    /// </summary>
    private string AddStatusColumnsScript => $@"
                ALTER TABLE {TableName}
                    ADD COLUMN IF NOT EXISTS status VARCHAR(20) NOT NULL DEFAULT 'Pending',
                    ADD COLUMN IF NOT EXISTS failure_reason TEXT,
                    ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP(0),
                    ADD COLUMN IF NOT EXISTS updated_by VARCHAR(100),
                    ADD COLUMN IF NOT EXISTS status_comment TEXT;";

    /// <summary>
    /// The SQL script for migrating the existing events to the status schema:
    /// processed events become "Processed" (keeping the processed time), unprocessed events become "Pending"
    /// if their try time already came, otherwise "Failed".
    /// </summary>
    private string MigrateExistingEventsToStatusScript => $@"
                UPDATE {TableName}
                SET
                    status = CASE
                        WHEN processed_at IS NOT NULL THEN '{nameof(EventStatus.Processed)}'
                        WHEN try_after_at <= @CurrentTime THEN '{nameof(EventStatus.Pending)}'
                        ELSE '{nameof(EventStatus.Failed)}'
                    END,
                    updated_at = processed_at;";

    /// <summary>
    /// The SQL script for removing the old "processed_at" column. Its old indexes are dropped together with it.
    /// </summary>
    private string DropProcessedAtColumnScript => $@"ALTER TABLE {TableName} DROP COLUMN IF EXISTS processed_at;";

    /// <summary>
    /// Migrates the table from the old schema (with the "processed_at" column) to the status schema if it is needed.
    /// It runs in a single transaction and locks the table exclusively, so no event can be read or written while migrating.
    /// </summary>
    private void MigrateToStatusSchemaIfNeeded(NpgsqlConnection dbConnection)
    {
        using var transaction = dbConnection.BeginTransaction();

        // Only one application instance migrates at a time; the others wait and then find the table already migrated.
        dbConnection.Execute("SELECT pg_advisory_xact_lock(hashtext(@LockName))",
            new { LockName = $"{TableName}_schema_migration" }, transaction, commandTimeout: 0);

        var hasStatusColumn = dbConnection.ExecuteScalar<bool>(SqlQueryToCheckStatusColumnExists,
            transaction: transaction);
        if (hasStatusColumn)
        {
            transaction.Commit();
            return;
        }

        logger.LogWarning("{StorageType}: Migrating the {TableName} table to the status schema.", TraceMessageTag,
            TableName);
        try
        {
            dbConnection.Execute($"SET LOCAL lock_timeout = '{secondsToWaitForMigrationLock}s'",
                transaction: transaction);
            dbConnection.Execute($"LOCK TABLE {TableName} IN ACCESS EXCLUSIVE MODE", transaction: transaction,
                commandTimeout: 0);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new EventStoreException(e,
                $"Could not lock the {TableName} table within {secondsToWaitForMigrationLock} seconds to migrate it to the status schema. The migration is rolled back and will be retried on the next start.");
        }

        dbConnection.Execute(AddStatusColumnsScript, transaction: transaction, commandTimeout: 0);
        dbConnection.Execute(MigrateExistingEventsToStatusScript, new { CurrentTime = DateTime.Now }, transaction,
            commandTimeout: 0);
        dbConnection.Execute(DropProcessedAtColumnScript, transaction: transaction, commandTimeout: 0);

        transaction.Commit();
        logger.LogWarning("{StorageType}: The {TableName} table is migrated to the status schema.", TraceMessageTag,
            TableName);
    }

    #endregion

    #region InsertEventAsync

    /// <summary>
    /// The SQL query for inserting a new event to the database.
    /// </summary>
    protected virtual string SqlQueryToInsertEvent => $@"
                INSERT INTO {TableName} (
                    id, provider, event_name, event_path, payload, headers, 
                    additional_data, naming_policy_type, created_at, try_count, try_after_at, status
                ) VALUES (
                    @Id, @Provider, @EventName, @EventPath, @Payload::jsonb, @Headers,
                    @AdditionalData, @NamingPolicyType, @CreatedAt, @TryCount, @TryAfterAt, @StatusName
                )";

    public bool InsertEvent(TBaseMessage message)
    {
        using var activity = CreateLogsForInvestigation(message);
        try
        {
            using var dbConnection = new NpgsqlConnection(_connectionString);
            dbConnection.Open();
            dbConnection.Execute(SqlQueryToInsertEvent, message);

            return true;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            throw new EventStoreException(e,
                $"Error while inserting a new event to the {TableName} table with the {message.Id} id.");
        }
    }

    public async Task<bool> InsertEventAsync(TBaseMessage message)
    {
        using var activity = CreateLogsForInvestigation(message);
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync(SqlQueryToInsertEvent, message);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            throw new EventStoreException(e,
                $"Error while inserting a new event to the {TableName} table with the {message.Id} id.");
        }
    }

    #endregion

    #region BulkInsertEventsAsync

    public async Task<bool> BulkInsertEventsAsync(TBaseMessage[] events)
    {
        using var activity = CreateActivityAndAddLogForBulkInsertIfEnabled(events);
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync(SqlQueryToInsertEvent, events);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            var insertingEventIds = string.Join(", ", events.Select(x => x.Id));
            throw new EventStoreException(e,
                $"Error while inserting an events to the {TableName} table with the {insertingEventIds} ids.");
        }
    }

    public bool BulkInsertEvents(TBaseMessage[] events)
    {
        using var activity = CreateActivityAndAddLogForBulkInsertIfEnabled(events);
        try
        {
            using var dbConnection = new NpgsqlConnection(_connectionString);
            dbConnection.Open();

            var affectedRows = dbConnection.Execute(SqlQueryToInsertEvent, events);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            var insertingEventIds = string.Join(", ", events.Select(x => x.Id));
            throw new EventStoreException(e,
                $"Error while inserting an events to the {TableName} table with the {insertingEventIds} ids.");
        }
    }

    #endregion

    #region GetUnprocessedEventsAsync

    protected virtual string SqlQueryToGetUnprocessedEvents => $@"
                SELECT id as ""{nameof(IBaseMessageBox.Id)}"", provider as ""{nameof(IBaseMessageBox.Provider)}"", 
                        event_name as ""{nameof(IBaseMessageBox.EventName)}"", event_path as ""{nameof(IBaseMessageBox.EventPath)}"", 
                        payload::text as ""{nameof(IBaseMessageBox.Payload)}"", headers as ""{nameof(IBaseMessageBox.Headers)}"",
                        naming_policy_type as ""{nameof(IBaseMessageBox.NamingPolicyType)}"", 
                        additional_data as ""{nameof(IBaseMessageBox.AdditionalData)}"", created_at as ""{nameof(IBaseMessageBox.CreatedAt)}"", 
                        try_count as ""{nameof(IBaseMessageBox.TryCount)}"", try_after_at as ""{nameof(IBaseMessageBox.TryAfterAt)}"", 
                        status as ""{nameof(IBaseMessageBox.Status)}"", failure_reason as ""{nameof(IBaseMessageBox.FailureReason)}"",
                        updated_at as ""{nameof(IBaseMessageBox.UpdatedAt)}"", updated_by as ""{nameof(IBaseMessageBox.UpdatedBy)}"",
                        status_comment as ""{nameof(IBaseMessageBox.StatusComment)}""
                FROM {TableName}
                WHERE 
                    status IN ('{nameof(EventStatus.Pending)}', '{nameof(EventStatus.Failed)}')
                    AND try_after_at <= @CurrentTime
                ORDER BY created_at ASC
                LIMIT @Limit";

    public async Task<TBaseMessage[]> GetUnprocessedEventsAsync(int limit)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            await dbConnection.OpenAsync();

            var unprocessedEvents = await dbConnection.QueryAsync<TBaseMessage>(SqlQueryToGetUnprocessedEvents, new
            {
                CurrentTime = DateTime.Now,
                Limit = limit
            });

            return unprocessedEvents.ToArray();
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while retrieving unprocessed events from the {TableName} table.");
        }
    }

    #endregion

    #region UpdateEventAsync

    private readonly string _sqlUpdateEventQuery = $@"
                UPDATE {settings.TableName}
                SET 
                    try_count = @TryCount,
                    try_after_at = @TryAfterAt,
                    status = @StatusName,
                    failure_reason = @FailureReason,
                    updated_at = @UpdatedAt,
                    updated_by = @UpdatedBy,
                    status_comment = @StatusComment
                WHERE id = @Id";

    public async Task<bool> UpdateEventAsync(TBaseMessage @event)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync(_sqlUpdateEventQuery, @event);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e,
                $"Error while updating the event in the {TableName} table with the {@event.Id} id.");
        }
    }

    public async Task<bool> UpdateEventsAsync(IEnumerable<TBaseMessage> events)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync(_sqlUpdateEventQuery, events);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while updating events of the {TableName} table.");
        }
    }

    #endregion

    #region IsEventProcessedAsync

    private readonly string _sqlCheckEventQuery = $@"
                SELECT status NOT IN ('{nameof(EventStatus.Pending)}', '{nameof(EventStatus.Failed)}')
                FROM {settings.TableName} WHERE id = @Id";

    public async Task<bool> IsEventProcessedAsync(Guid id)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            dbConnection.Open();

            var result = await dbConnection.QuerySingleOrDefaultAsync<bool?>(_sqlCheckEventQuery, new { Id = id });
            return result ?? true;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e,
                $"Error while checking if the event with id {id} is processed in the {TableName} table.");
        }
    }

    #endregion

    #region DeleteProcessedEventsAsync

    private readonly string _sqlDeleteEventQuery = $@"
                DELETE FROM {settings.TableName}
                WHERE status = '{nameof(EventStatus.Processed)}' AND updated_at < @ProcessedAt";

    public async Task<bool> DeleteProcessedEventsAsync(DateTime processedAt)
    {
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
            await dbConnection.OpenAsync();

            var deletedRows = await dbConnection.ExecuteAsync(_sqlDeleteEventQuery, new { ProcessedAt = processedAt });
            return deletedRows > 0;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while deleting processed events from the {TableName} table.");
        }
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Creates an activity for tracing if the instrumentation is enabled. Also add logging scope with event info.
    /// </summary>
    /// <param name="message">The message for which the activity is created.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateLogsForInvestigation(TBaseMessage message)
    {
        logger.LogDebug("{StorageType}: Storing event '{EventName}' with ID {MessageId}", TraceMessageTag,
            message.EventName, message.Id);

        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        var traceParentId = Activity.Current?.Id;
        var spanName = $"{TraceMessageTag}: Storing {message.EventName} event";
        var activity = EventStorageTraceInstrumentation.StartActivity(spanName, ActivityKind.Server,
            traceParentId, spanType: TraceMessageTag);
        activity?.AttachEventInfo(message);

        return activity;
    }

    /// <summary>
    /// Creates an activity for tracing if the instrumentation is enabled. Also sets tags for event names.
    /// Also adds a debug log about the bulk insert operation.
    /// </summary>
    /// <param name="messages">The array of messages which will be stored.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateActivityAndAddLogForBulkInsertIfEnabled(TBaseMessage[] messages)
    {
        logger.LogDebug("{StorageType}: Storing {MessagesCount} event(s)", TraceMessageTag, messages.Length);

        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        const string eventIdTag = "event.names";
        const string nameSeparator = ", ";
        var traceParentId = Activity.Current?.Id;
        var spanName = $"{TraceMessageTag}: Storing {messages.Length} event(s)";
        var insertingEventIds = string.Join(nameSeparator, messages.Select(e => e.EventName));
        var activity = EventStorageTraceInstrumentation.StartActivity(spanName, ActivityKind.Server, traceParentId,
            spanType: TraceMessageTag);
        activity?.SetTag(eventIdTag, insertingEventIds);

        return activity;
    }

    #endregion
}