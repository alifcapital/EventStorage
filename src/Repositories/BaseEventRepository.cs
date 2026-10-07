using System.Diagnostics;
using Dapper;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Instrumentation.Trace;
using EventStorage.Management.Models;
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
    /// <summary>
    /// The connection string of the database where the table is located.
    /// </summary>
    internal readonly string ConnectionString = settings.ConnectionString;

    /// <summary>
    /// The name of the table for connecting repository to the correct table in the database.
    /// </summary>
    protected internal readonly string TableName = settings.TableName;

    /// <summary>
    /// Seconds to wait for the exclusive lock of the table while migrating its old schema.
    /// </summary>
    internal readonly int SecondsToWaitForMigrationLock = secondsToWaitForMigrationLock;
    
    /// <summary>
    /// The logger instance.
    /// </summary>
    internal ILogger Logger => logger;

    /// <summary>
    /// The tag/prefix of the trace message for logging purposes.
    /// </summary>
    protected abstract string TraceMessageTag { get; }

    /// <summary>
    /// The tag/prefix of the storage type (inbox or outbox) for logging purposes.
    /// </summary>
    internal string StorageTypeTag => TraceMessageTag;

    /// <summary>
    /// Whether the table has the naming policy column. The outbox table does not have it.
    /// </summary>
    internal virtual bool HasNamingPolicyColumn => true;

    /// <summary>
    /// Creates the table if it does not exist and migrates its schema. All schema changes must be added to the
    /// <see cref="BaseEventRepositorySchemaExtensions"/> instead of this class.
    /// </summary>
    public void CreateTableIfNotExists() => this.CreateOrMigrateTableSchema();

    #region InsertEventAsync

    /// <summary>
    /// The SQL query for inserting a new event to the database. The naming policy column is included only if the table has it.
    /// </summary>
    private string SqlQueryToInsertEvent => $@"
                INSERT INTO {TableName} (
                    id, provider, event_name, event_path, payload, headers,
                    additional_data,{(HasNamingPolicyColumn ? " naming_policy_type," : string.Empty)} created_at, try_count, try_after_at, status
                ) VALUES (
                    @Id, @Provider, @EventName, @EventPath, @Payload::jsonb, @Headers,
                    @AdditionalData,{(HasNamingPolicyColumn ? " @NamingPolicyType," : string.Empty)} @CreatedAt, @TryCount, @TryAfterAt, @StatusName
                )";

    public bool InsertEvent(TBaseMessage message)
    {
        using var activity = CreateLogsForInvestigation(message);
        try
        {
            using var dbConnection = new NpgsqlConnection(ConnectionString);
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

    public async Task<bool> InsertEventAsync(TBaseMessage message, CancellationToken cancellationToken)
    {
        using var activity = CreateLogsForInvestigation(message);
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var affectedRows = await dbConnection.ExecuteAsync(
                new CommandDefinition(SqlQueryToInsertEvent, message, cancellationToken: cancellationToken));
            return affectedRows > 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            throw new EventStoreException(e,
                $"Error while inserting a new event to the {TableName} table with the {message.Id} id.");
        }
    }

    #endregion

    #region BulkInsertEventsAsync

    public async Task<bool> BulkInsertEventsAsync(TBaseMessage[] events, CancellationToken cancellationToken)
    {
        using var activity = CreateActivityAndAddLogForBulkInsertIfEnabled(events);
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var affectedRows = await dbConnection.ExecuteAsync(
                new CommandDefinition(SqlQueryToInsertEvent, events, cancellationToken: cancellationToken));
            return affectedRows > 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
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
            using var dbConnection = new NpgsqlConnection(ConnectionString);
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

    #region LockUnprocessedEventsAsync

    /// <summary>
    /// The columns of the table to select, mapped to the properties of the message.
    /// The naming policy column is included only if the table has it.
    /// </summary>
    private string SqlSelectColumns => GetSqlSelectColumns();

    /// <summary>
    /// Gets the columns of the table to select, mapped to the properties of the message. The status and the try time
    /// can be taken from other expressions, for example the original values of the event before it was locked.
    /// </summary>
    /// <param name="statusColumn">The expression of the status.</param>
    /// <param name="tryAfterAtColumn">The expression of the try time.</param>
    private string GetSqlSelectColumns(string statusColumn = "status", string tryAfterAtColumn = "try_after_at") => $@"
                        id as ""{nameof(IBaseMessageBox.Id)}"", provider as ""{nameof(IBaseMessageBox.Provider)}"",
                        event_name as ""{nameof(IBaseMessageBox.EventName)}"", event_path as ""{nameof(IBaseMessageBox.EventPath)}"",
                        payload::text as ""{nameof(IBaseMessageBox.Payload)}"", headers as ""{nameof(IBaseMessageBox.Headers)}"",
                        {(HasNamingPolicyColumn ? $@"naming_policy_type as ""{nameof(IBaseMessageBox.NamingPolicyType)}""," : string.Empty)}
                        additional_data as ""{nameof(IBaseMessageBox.AdditionalData)}"", created_at as ""{nameof(IBaseMessageBox.CreatedAt)}"",
                        try_count as ""{nameof(IBaseMessageBox.TryCount)}"", {tryAfterAtColumn} as ""{nameof(IBaseMessageBox.TryAfterAt)}"",
                        {statusColumn} as ""{nameof(IBaseMessageBox.Status)}"", failure_reason as ""{nameof(IBaseMessageBox.FailureReason)}"",
                        updated_at as ""{nameof(IBaseMessageBox.UpdatedAt)}"", updated_by as ""{nameof(IBaseMessageBox.UpdatedBy)}"",
                        status_comment as ""{nameof(IBaseMessageBox.StatusComment)}""";

    /// <summary>
    /// The name of the Common Table Expression which keeps the events to lock with their status and try time before they were locked.
    /// </summary>
    private const string EventsToLockTable = "events_to_lock";

    /// <summary>
    /// The columns of the locked event to select, with the status and try time of the event before it was locked.
    /// </summary>
    private string SqlSelectColumnsOfLockedEvent =>
        GetSqlSelectColumns($"{EventsToLockTable}.status_before_lock", $"{EventsToLockTable}.try_after_at_before_lock");

    /// <summary>
    /// The columns of the event to lock. The event which is still "Processing" after its lock time was abandoned
    /// (for example, the instance stopped before storing its result), so its status before the lock is considered as failed.
    /// The columns have different names from the columns of the table to not be ambiguous with them in the update
    /// statement, where both the table and the CTE are available.
    /// </summary>
    private const string SqlColumnsOfEventToLock = $@"
                        id AS locked_event_id,
                        CASE WHEN status = '{nameof(EventStatus.Processing)}' THEN '{nameof(EventStatus.Failed)}' ELSE status END AS status_before_lock,
                        try_after_at AS try_after_at_before_lock";

    /// <summary>
    /// The SQL part for locking the selected events by marking them as "Processing" until the given time, and for
    /// returning them with their status and try time before the lock.
    /// </summary>
    private string SqlQueryToMarkSelectedEventsAsProcessing => $@"
                UPDATE {TableName}
                SET status = '{nameof(EventStatus.Processing)}', try_after_at = @ProcessingTimeoutAt
                FROM {EventsToLockTable}
                WHERE id = {EventsToLockTable}.locked_event_id
                RETURNING {SqlSelectColumnsOfLockedEvent}";

    /// <summary>
    /// Selects the unprocessed events with "FOR UPDATE SKIP LOCKED", so the events which are being selected by another
    /// instance at the same time are skipped, and marks them as "Processing" in the same statement, so other instances
    /// skip them until they are processed, unlocked or their processing timeout has passed.
    /// </summary>
    private string SqlQueryToLockUnprocessedEvents => $@"
                WITH {EventsToLockTable} AS (
                    SELECT {SqlColumnsOfEventToLock}
                    FROM {TableName}
                    WHERE
                        status IN ('{nameof(EventStatus.Pending)}', '{nameof(EventStatus.Failed)}', '{nameof(EventStatus.Processing)}')
                        AND try_after_at <= @CurrentTime
                    ORDER BY created_at ASC
                    LIMIT @Limit
                    FOR UPDATE SKIP LOCKED
                )
                {SqlQueryToMarkSelectedEventsAsProcessing}";

    public async Task<TBaseMessage[]> LockUnprocessedEventsAsync(int limit, DateTime processingTimeoutAt,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var unprocessedEvents = await dbConnection.QueryAsync<TBaseMessage>(new CommandDefinition(
                SqlQueryToLockUnprocessedEvents, new
                {
                    CurrentTime = DateTime.Now,
                    ProcessingTimeoutAt = processingTimeoutAt,
                    Limit = limit
                }, cancellationToken: cancellationToken));

            // The returning rows of the update statement do not keep the order of the selected events.
            return unprocessedEvents.OrderBy(e => e.CreatedAt).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e, $"Error while locking unprocessed events of the {TableName} table.");
        }
    }

    #endregion

    #region LockEventByIdAsync

    /// <summary>
    /// Locks the event with any status, if it is not "Processing" or its processing timeout has passed. If the event is
    /// being changed at the same time by another statement, PostgreSQL waits for it and checks the condition again
    /// with the new value of the event.
    /// </summary>
    private string SqlQueryToLockEventById => $@"
                WITH {EventsToLockTable} AS (
                    SELECT {SqlColumnsOfEventToLock}
                    FROM {TableName}
                    WHERE id = @Id
                        AND (status <> '{nameof(EventStatus.Processing)}' OR try_after_at <= @CurrentTime)
                    FOR UPDATE
                )
                {SqlQueryToMarkSelectedEventsAsProcessing}";

    public async Task<TBaseMessage> LockEventByIdAsync(Guid id, DateTime processingTimeoutAt,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var command = new CommandDefinition(SqlQueryToLockEventById, new
            {
                Id = id,
                CurrentTime = DateTime.Now,
                ProcessingTimeoutAt = processingTimeoutAt
            }, cancellationToken: cancellationToken);
            return await dbConnection.QuerySingleOrDefaultAsync<TBaseMessage>(command);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e, $"Error while locking the event with id {id} of the {TableName} table.");
        }
    }

    #endregion

    #region UnlockEventsAsync

    /// <summary>
    /// Restores the original status and try time of the events only if they are still locked by the same lock, so
    /// the events which are already stored, or locked again by another instance after the processing timeout, are not changed.
    /// </summary>
    private readonly string _sqlUnlockEventsQuery = $@"
                UPDATE {settings.TableName}
                SET status = @StatusName, try_after_at = @TryAfterAt
                WHERE id = @Id AND status = '{nameof(EventStatus.Processing)}' AND try_after_at = @ProcessingTimeoutAt";

    public async Task UnlockEventsAsync(IEnumerable<TBaseMessage> events, DateTime processingTimeoutAt,
        CancellationToken cancellationToken)
    {
        var eventsToUnlock = events.Select(e => new
        {
            e.Id,
            StatusName = e.Status.ToString(),
            e.TryAfterAt,
            ProcessingTimeoutAt = processingTimeoutAt
        }).ToArray();
        if (eventsToUnlock.Length == 0)
            return;

        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            await dbConnection.ExecuteAsync(
                new CommandDefinition(_sqlUnlockEventsQuery, eventsToUnlock, cancellationToken: cancellationToken));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e, $"Error while unlocking events of the {TableName} table.");
        }
    }

    #endregion

    #region UpdateEventAsync

    /// <summary>
    /// Updates the event. It also unlocks the locked event, since its status and try time are changed.
    /// </summary>
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

    public async Task<bool> UpdateEventAsync(TBaseMessage @event, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var command = new CommandDefinition(_sqlUpdateEventQuery, @event, cancellationToken: cancellationToken);
            var affectedRows = await dbConnection.ExecuteAsync(command);
            return affectedRows > 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e,
                $"Error while updating the event in the {TableName} table with the {@event.Id} id.");
        }
    }

    public async Task<bool> UpdateEventsAsync(IEnumerable<TBaseMessage> events,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var affectedRows = await dbConnection.ExecuteAsync(
                new CommandDefinition(_sqlUpdateEventQuery, events, cancellationToken: cancellationToken));
            return affectedRows > 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e, $"Error while updating events of the {TableName} table.");
        }
    }

    #endregion

    #region GetEventStatusByIdAsync

    private readonly string _sqlGetEventStatusQuery = $@"
                SELECT status FROM {settings.TableName} WHERE id = @Id";

    public async Task<EventStatus?> GetEventStatusByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var status = await dbConnection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(_sqlGetEventStatusQuery, new { Id = id }, cancellationToken: cancellationToken));
            return status is null ? null : Enum.Parse<EventStatus>(status);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e,
                $"Error while getting the status of the event with id {id} from the {TableName} table.");
        }
    }

    #endregion

    #region GetEventByIdAsync

    private string SqlQueryToGetEventById => $@"
                SELECT {SqlSelectColumns}
                FROM {TableName}
                WHERE id = @Id";

    public async Task<TBaseMessage> GetEventByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var command = new CommandDefinition(SqlQueryToGetEventById, new { Id = id },
                cancellationToken: cancellationToken);
            return await dbConnection.QuerySingleOrDefaultAsync<TBaseMessage>(command);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e, $"Error while getting the event with id {id} from the {TableName} table.");
        }
    }

    #endregion

    #region GetEventsAsync

    private const string LikeEscapeCharacter = @"\";

    /// <summary>
    /// The columns of the table to select for listing events, mapped to the properties of the <see cref="EventSummary"/>.
    /// The other columns, such as the payload, headers and additional data, are not loaded, since they may be large.
    /// </summary>
    private string SqlSelectSummaryColumns => $@"
                        id as ""{nameof(EventSummary.Id)}"", provider as ""{nameof(EventSummary.Provider)}"",
                        event_name as ""{nameof(EventSummary.EventName)}"", event_path as ""{nameof(EventSummary.EventPath)}"",
                        created_at as ""{nameof(EventSummary.CreatedAt)}"",
                        try_count as ""{nameof(EventSummary.TryCount)}"", try_after_at as ""{nameof(EventSummary.TryAfterAt)}"",
                        status as ""{nameof(EventSummary.Status)}"", failure_reason as ""{nameof(EventSummary.FailureReason)}"",
                        updated_at as ""{nameof(EventSummary.UpdatedAt)}"", updated_by as ""{nameof(EventSummary.UpdatedBy)}""";

    public async Task<EventPagedList<EventSummary>> GetEventsAsync(EventsFilter filter,
        CancellationToken cancellationToken)
    {
        try
        {
            var (sqlQuery, parameters) = BuildQueryToGetEvents(filter);

            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync(cancellationToken);

            var events = await dbConnection.QueryAsync<EventSummary>(
                new CommandDefinition(sqlQuery, parameters, cancellationToken: cancellationToken));
            return events.ToArray().ToPagedList(filter.PageIndex, filter.PageSize);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new EventStoreException(e, $"Error while getting events by the filter from the {TableName} table.");
        }
    }

    /// <summary>
    /// Builds the SQL query with the conditions of the given filters only. All values are passed as parameters.
    /// </summary>
    private (string SqlQuery, DynamicParameters Parameters) BuildQueryToGetEvents(EventsFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (filter.Status.HasValue)
        {
            conditions.Add("status = @Status");
            parameters.Add("Status", filter.Status.Value.ToString());
        }

        if (!string.IsNullOrEmpty(filter.EventName))
        {
            conditions.Add("event_name = @EventName");
            parameters.Add("EventName", filter.EventName);
        }

        if (!string.IsNullOrWhiteSpace(filter.EventProviderType))
        {
            // The outbox event may have multiple providers separated by comma, so one of them must match entirely.
            conditions.Add($"(',' || provider || ',') LIKE @ProviderPattern ESCAPE '{LikeEscapeCharacter}'");
            parameters.Add("ProviderPattern", $"%,{EscapeLikePattern(filter.EventProviderType)},%");
        }

        if (filter.CreatedFrom.HasValue)
        {
            conditions.Add("created_at >= @CreatedFrom");
            parameters.Add("CreatedFrom", filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            conditions.Add("created_at <= @CreatedTo");
            parameters.Add("CreatedTo", filter.CreatedTo.Value);
        }

        if (filter.UpdatedFrom.HasValue)
        {
            conditions.Add("updated_at >= @UpdatedFrom");
            parameters.Add("UpdatedFrom", filter.UpdatedFrom.Value);
        }

        if (filter.UpdatedTo.HasValue)
        {
            conditions.Add("updated_at <= @UpdatedTo");
            parameters.Add("UpdatedTo", filter.UpdatedTo.Value);
        }

        if (!string.IsNullOrEmpty(filter.UpdatedBy))
        {
            // The user name may be a full name, so it is enough to match a part of it, such as the first name.
            conditions.Add($"updated_by ILIKE @UpdatedByPattern ESCAPE '{LikeEscapeCharacter}'");
            parameters.Add("UpdatedByPattern", $"%{EscapeLikePattern(filter.UpdatedBy)}%");
        }

        if (filter.MinTryCount.HasValue)
        {
            conditions.Add("try_count >= @MinTryCount");
            parameters.Add("MinTryCount", filter.MinTryCount.Value);
        }

        if (!string.IsNullOrEmpty(filter.FailureReasonContains))
        {
            conditions.Add($"failure_reason ILIKE @FailureReasonPattern ESCAPE '{LikeEscapeCharacter}'");
            parameters.Add("FailureReasonPattern", $"%{EscapeLikePattern(filter.FailureReasonContains)}%");
        }

        if (!string.IsNullOrEmpty(filter.PayloadContains))
        {
            conditions.Add($"payload::text ILIKE @PayloadPattern ESCAPE '{LikeEscapeCharacter}'");
            parameters.Add("PayloadPattern", $"%{EscapeLikePattern(filter.PayloadContains)}%");
        }

        // One more event is loaded to identify whether there is a next page without counting all events.
        var skip = (filter.PageIndex - 1) * filter.PageSize;
        var take = filter.PageSize + 1;
        parameters.Add("Offset", skip);
        parameters.Add("Limit", take);

        var sortDirection = filter.SortDescending ? "DESC" : "ASC";
        var whereClause = conditions.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", conditions)}";
        var sqlQuery = $@"
                SELECT {SqlSelectSummaryColumns}
                FROM {TableName}
                {whereClause}
                ORDER BY created_at {sortDirection}, id {sortDirection}
                OFFSET @Offset
                LIMIT @Limit";

        return (sqlQuery, parameters);
    }

    /// <summary>
    /// Escapes the special characters of the LIKE pattern, so the value is matched as a plain text.
    /// </summary>
    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
            .Replace("%", LikeEscapeCharacter + "%")
            .Replace("_", LikeEscapeCharacter + "_");
    }

    #endregion

    #region DeleteProcessedEventsAsync

    private readonly string _sqlDeleteEventQuery = $@"
                DELETE FROM {settings.TableName}
                WHERE status = '{nameof(EventStatus.Processed)}' AND updated_at < @ProcessedAt";

    public async Task<bool> DeleteProcessedEventsAsync(DateTime processedAt, CancellationToken cancellationToken)
    {
        await using var dbConnection = new NpgsqlConnection(ConnectionString);
        try
        {
            await dbConnection.OpenAsync(cancellationToken);

            var deletedRows = await dbConnection.ExecuteAsync(
                new CommandDefinition(_sqlDeleteEventQuery, new { ProcessedAt = processedAt },
                    cancellationToken: cancellationToken));
            return deletedRows > 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
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