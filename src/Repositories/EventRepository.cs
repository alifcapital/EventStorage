using Dapper;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Models;
using Npgsql;

namespace EventStorage.Repositories;

internal abstract class EventRepository<TBaseMessage>(InboxOrOutboxStructure settings) : IEventRepository<TBaseMessage>
    where TBaseMessage : class, IBaseMessageBox
{
    private readonly string _tableName = settings.TableName;
    private readonly string _connectionString = settings.ConnectionString;

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

                CREATE INDEX IF NOT EXISTS idx_for_get_unprocessed_events
                    ON public.{_tableName} (processed_at, try_after_at);

                CREATE INDEX IF NOT EXISTS idx_for_delete_processed_events
                    ON public.{_tableName} (processed_at);";

            dbConnection.Execute(sql);
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while checking/creating {_tableName} table.");
        }
    }

    public bool InsertEvent(TBaseMessage @event)
    {
        using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
            dbConnection.Open();
            string sql = $@"
                INSERT INTO {_tableName} (
                    id, provider, event_name, event_path, payload, headers, 
                    additional_data, naming_policy_type, created_at, try_count, try_after_at
                ) VALUES (
                    @Id, @Provider, @EventName, @EventPath, @Payload, @Headers, 
                    @AdditionalData, @NamingPolicyType, @CreatedAt, @TryCount, @TryAfterAt
                )";

            dbConnection.Execute(sql, @event);

            return true;
        }
        catch (Exception e)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return false;
                
            throw new EventStoreException(e,
                $"Error while inserting a new event to the {_tableName} table with the {@event.Id} id.");
        }
    }

    public bool BulkInsertEvents(IEnumerable<TBaseMessage> events)
    {
        using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
            dbConnection.Open();
            string sql = $@"
                INSERT INTO {_tableName} (
                    id, provider, event_name, event_path, payload, headers, 
                    additional_data, naming_policy_type, created_at, try_count, try_after_at
                ) VALUES (
                    @Id, @Provider, @EventName, @EventPath, @Payload, @Headers, 
                    @AdditionalData, @NamingPolicyType, @CreatedAt, @TryCount, @TryAfterAt
                )";

            dbConnection.Execute(sql, events);

            return true;
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

    private const string SelectSqlQueryTemplate = $@"
                SELECT id as ""{nameof(IBaseMessageBox.Id)}"", provider as ""{nameof(IBaseMessageBox.Provider)}"", 
                        event_name as ""{nameof(IBaseMessageBox.EventName)}"", event_path as ""{nameof(IBaseMessageBox.EventPath)}"", 
                        payload as ""{nameof(IBaseMessageBox.Payload)}"", headers as ""{nameof(IBaseMessageBox.Headers)}"", 
                        naming_policy_type as ""{nameof(IBaseMessageBox.NamingPolicyType)}"", 
                        additional_data as ""{nameof(IBaseMessageBox.AdditionalData)}"", created_at as ""{nameof(IBaseMessageBox.CreatedAt)}"", 
                        try_count as ""{nameof(IBaseMessageBox.TryCount)}"", try_after_at as ""{nameof(IBaseMessageBox.TryAfterAt)}"", 
                        processed_at as ""{nameof(IBaseMessageBox.ProcessedAt)}""
                FROM {{0}}
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
            var selectSqlQuery = string.Format(SelectSqlQueryTemplate, _tableName);
            await dbConnection.OpenAsync();
            var unprocessedEvents = await dbConnection.QueryAsync<TBaseMessage>(selectSqlQuery, new 
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

    public async Task<bool> UpdateEventAsync(TBaseMessage @event)
    {
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
            await dbConnection.OpenAsync();

            var sql = $@"
                UPDATE {_tableName}
                SET 
                    try_count = @TryCount,
                    try_after_at = @TryAfterAt,
                    processed_at = @ProcessedAt
                WHERE id = @Id";

            var affectedRows = await dbConnection.ExecuteAsync(sql, @event);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while updating the event in the {_tableName} table with the {@event.Id} id.");
        }
    }

    public async Task<bool> UpdateEventsAsync(IEnumerable<TBaseMessage> events)
    {
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
            await dbConnection.OpenAsync();

            var sql = $@"
                UPDATE {_tableName}
                SET 
                    try_count = @TryCount,
                    try_after_at = @TryAfterAt,
                    processed_at = @ProcessedAt
                WHERE id = @Id";

            var affectedRows = await dbConnection.ExecuteAsync(sql, events);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while updating events of the {_tableName} table.");
        }
    }

    public async Task<bool> DeleteProcessedEventsAsync(DateTime processedAt)
    {
        await using var dbConnection = new NpgsqlConnection(_connectionString);
        try
        {
            await dbConnection.OpenAsync();

            var sql = $@"
                DELETE FROM {_tableName}
                WHERE processed_at < @ProcessedAt";

            var deletedRows = await dbConnection.ExecuteAsync(sql, new { ProcessedAt = processedAt });
            return deletedRows > 0;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while deleting processed events from the {_tableName} table.");
        }
    }
}
