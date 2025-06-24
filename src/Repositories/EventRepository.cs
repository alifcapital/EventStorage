using System.Diagnostics;
using Dapper;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Instrumentation.Trace;
using EventStorage.Models;
using Npgsql;

namespace EventStorage.Repositories;

internal abstract class EventRepository<TBaseMessage>(InboxOrOutboxStructure settings) : IEventRepository<TBaseMessage>
    where TBaseMessage : class, IBaseMessageBox
{
    private readonly string _tableName = settings.TableName;
    private readonly string _connectionString = settings.ConnectionString;

    /// <summary>
    /// The tag/prefix of the trace message for logging purposes.
    /// </summary>
    protected abstract string TraceMessageTag { get; }

    public void CreateTableIfNotExists()
    {
        using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
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
        using var activity = CreateActivityIfEnabled(message);
        using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
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
        using var activity = CreateActivityIfEnabled(message);
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
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

    public async Task<bool> BulkInsertEventsAsync(TBaseMessage[] events)
    {
        using var activity = CreateActivityForBulkInsertIfEnabled(events);
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
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
        using var activity = CreateActivityForBulkInsertIfEnabled(events);
        using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
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
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
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

    private readonly string _sqlUpdateEventQuery = $@"
                UPDATE {settings.TableName}
                SET 
                    try_count = @TryCount,
                    try_after_at = @TryAfterAt,
                    processed_at = @ProcessedAt
                WHERE id = @Id";

    public async Task<bool> UpdateEventAsync(TBaseMessage @event)
    {
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
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
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync(_sqlUpdateEventQuery, events);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while updating events of the {_tableName} table.");
        }
    }

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

    #region Helper methods

    /// <summary>
    /// Creates an activity for tracing if the instrumentation is enabled. Also sets tags for event ID and provider.
    /// </summary>
    /// <param name="message">The message for which the activity is created.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateActivityIfEnabled(TBaseMessage message)
    {
        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        const string eventIdTag = "event.id";
        const string providerTag = "provider";
        var traceName = $"{TraceMessageTag}: Storing {message.EventName} event";
        var traceParentId = Activity.Current?.Id;
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, traceParentId);
        activity?.SetTag(eventIdTag, message.Id);
        activity?.SetTag(providerTag, message.Provider);

        return activity;
    }

    /// <summary>
    /// Creates an activity for tracing if the instrumentation is enabled. Also sets tags for event names.
    /// </summary>
    /// <param name="messages">The array of messages which will be stored.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateActivityForBulkInsertIfEnabled(TBaseMessage[] messages)
    {
        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        const string eventIdTag = "event.names";
        const string nameSeparator = ", ";
        var traceName = $"{TraceMessageTag}: Storing {messages.Length} event(s)";
        var traceParentId = Activity.Current?.Id;
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, traceParentId);
        activity?.SetTag(eventIdTag, string.Join(nameSeparator, messages.Select(e => e.EventName)));

        return activity;
    }

    #endregion
}