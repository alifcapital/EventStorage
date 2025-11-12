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

internal abstract class EventRepository<TBaseMessage>(ILogger logger, InboxOrOutboxStructure settings)
    : IEventRepository<TBaseMessage>
    where TBaseMessage : class, IBaseMessageBox
{
    private readonly string _tableName = settings.TableName;
    private readonly string _connectionString = settings.ConnectionString;

    /// <summary>
    /// The tag/prefix of the trace message for logging purposes.
    /// </summary>
    protected abstract string TraceMessageTag { get; }

    #region CreateTableIfNotExists

    public void CreateTableIfNotExists()
    {
        try
        {
            using var dbConnection = new NpgsqlConnection(_connectionString);
            dbConnection.Open();

            var sql = $@"CREATE TABLE IF NOT EXISTS {_tableName}
                (
                    id UUID NOT NULL PRIMARY KEY,
                    provider VARCHAR(50) NOT NULL,
                    event_name VARCHAR(100) NOT NULL,
                    event_path VARCHAR(255),
                    payload TEXT,
                    headers TEXT,
                    additional_data TEXT,
                    naming_policy_type VARCHAR(15),
                    created_at TIMESTAMP(0) NOT NULL,
                    try_count integer DEFAULT 0 NOT NULL,
                    try_after_at TIMESTAMP(0) NOT NULL,
                    processed_at TIMESTAMP(0) DEFAULT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_for_get_unprocessed_events_of_{_tableName}
                    ON public.{_tableName} (processed_at, try_after_at);

                CREATE INDEX IF NOT EXISTS idx_for_delete_processed_events_of_{_tableName}
                    ON public.{_tableName} (processed_at);";

            dbConnection.Execute(sql);
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while checking/creating {_tableName} table.");
        }
    }

    #endregion

    #region InsertEventAsync

    private readonly string _sqlQueryToInsertEvent = $@"
                INSERT INTO {settings.TableName} (
                    id, provider, event_name, event_path, payload, headers, 
                    additional_data, naming_policy_type, created_at, try_count, try_after_at
                ) VALUES (
                    @Id, @Provider, @EventName, @EventPath, @Payload, @Headers, 
                    @AdditionalData, @NamingPolicyType, @CreatedAt, @TryCount, @TryAfterAt
                )";

    public bool InsertEvent(TBaseMessage message)
    {
        using var activity = CreateLogsForInvestigation(message);
        try
        {
            using var dbConnection = new NpgsqlConnection(_connectionString);
            dbConnection.Open();
            dbConnection.Execute(_sqlQueryToInsertEvent, message);

            return true;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            throw new EventStoreException(e,
                $"Error while inserting a new event to the {_tableName} table with the {message.Id} id.");
        }
    }

    public async Task<bool> InsertEventAsync(TBaseMessage message)
    {
        using var activity = CreateLogsForInvestigation(message);
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync(_sqlQueryToInsertEvent, message);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            throw new EventStoreException(e,
                $"Error while inserting a new event to the {_tableName} table with the {message.Id} id.");
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

            var affectedRows = await dbConnection.ExecuteAsync(_sqlQueryToInsertEvent, events);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            var insertingEventIds = string.Join(", ", events.Select(x => x.Id));
            throw new EventStoreException(e,
                $"Error while inserting an events to the {_tableName} table with the {insertingEventIds} ids.");
        }
    }

    public bool BulkInsertEvents(TBaseMessage[] events)
    {
        using var activity = CreateActivityAndAddLogForBulkInsertIfEnabled(events);
        try
        {
            using var dbConnection = new NpgsqlConnection(_connectionString);
            dbConnection.Open();

            var affectedRows = dbConnection.Execute(_sqlQueryToInsertEvent, events);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;

            var insertingEventIds = string.Join(", ", events.Select(x => x.Id));
            throw new EventStoreException(e,
                $"Error while inserting an events to the {_tableName} table with the {insertingEventIds} ids.");
        }
    }

    #endregion

    #region GetUnprocessedEventsAsync

    private readonly string _selectSqlQuery = $@"
                SELECT id as ""{nameof(IBaseMessageBox.Id)}"", provider as ""{nameof(IBaseMessageBox.Provider)}"", 
                        event_name as ""{nameof(IBaseMessageBox.EventName)}"", event_path as ""{nameof(IBaseMessageBox.EventPath)}"", 
                        payload as ""{nameof(IBaseMessageBox.Payload)}"", headers as ""{nameof(IBaseMessageBox.Headers)}"", 
                        naming_policy_type as ""{nameof(IBaseMessageBox.NamingPolicyType)}"", 
                        additional_data as ""{nameof(IBaseMessageBox.AdditionalData)}"", created_at as ""{nameof(IBaseMessageBox.CreatedAt)}"", 
                        try_count as ""{nameof(IBaseMessageBox.TryCount)}"", try_after_at as ""{nameof(IBaseMessageBox.TryAfterAt)}"", 
                        processed_at as ""{nameof(IBaseMessageBox.ProcessedAt)}""
                FROM {settings.TableName}
                WHERE 
                    processed_at IS NULL
                    AND try_after_at <= @CurrentTime
                ORDER BY created_at ASC
                LIMIT @Limit";

    public async Task<TBaseMessage[]> GetUnprocessedEventsAsync(int limit)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(_connectionString);
            await dbConnection.OpenAsync();

            var unprocessedEvents = await dbConnection.QueryAsync<TBaseMessage>(_selectSqlQuery, new
            {
                CurrentTime = DateTime.Now,
                Limit = limit
            });

            return unprocessedEvents.ToArray();
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while retrieving unprocessed events from the {_tableName} table.");
        }
    }

    #endregion

    #region UpdateEventAsync

    private readonly string _sqlUpdateEventQuery = $@"
                UPDATE {settings.TableName}
                SET 
                    try_count = @TryCount,
                    try_after_at = @TryAfterAt,
                    processed_at = @ProcessedAt
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
                $"Error while updating the event in the {_tableName} table with the {@event.Id} id.");
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
            throw new EventStoreException(e, $"Error while updating events of the {_tableName} table.");
        }
    }

    #endregion

    #region IsEventProcessedAsync

    private readonly string _sqlCheckEventQuery = $@"
                SELECT processed_at IS NOT NULL FROM {settings.TableName} WHERE id = @Id";

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
                $"Error while checking if the event with id {id} is processed in the {_tableName} table.");
        }
    }

    #endregion

    #region DeleteProcessedEventsAsync

    private readonly string _sqlDeleteEventQuery = $@"
                DELETE FROM {settings.TableName}
                WHERE processed_at < @ProcessedAt";

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
            throw new EventStoreException(e, $"Error while deleting processed events from the {_tableName} table.");
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