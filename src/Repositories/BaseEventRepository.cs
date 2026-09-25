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

    public async Task<bool> InsertEventAsync(TBaseMessage message)
    {
        using var activity = CreateLogsForInvestigation(message);
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
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
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
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

    #region GetUnprocessedEventsAsync

    /// <summary>
    /// The columns of the table to select, mapped to the properties of the message.
    /// The naming policy column is included only if the table has it.
    /// </summary>
    private string SqlSelectColumns => $@"
                        id as ""{nameof(IBaseMessageBox.Id)}"", provider as ""{nameof(IBaseMessageBox.Provider)}"",
                        event_name as ""{nameof(IBaseMessageBox.EventName)}"", event_path as ""{nameof(IBaseMessageBox.EventPath)}"",
                        payload::text as ""{nameof(IBaseMessageBox.Payload)}"", headers as ""{nameof(IBaseMessageBox.Headers)}"",
                        {(HasNamingPolicyColumn ? $@"naming_policy_type as ""{nameof(IBaseMessageBox.NamingPolicyType)}""," : string.Empty)}
                        additional_data as ""{nameof(IBaseMessageBox.AdditionalData)}"", created_at as ""{nameof(IBaseMessageBox.CreatedAt)}"",
                        try_count as ""{nameof(IBaseMessageBox.TryCount)}"", try_after_at as ""{nameof(IBaseMessageBox.TryAfterAt)}"",
                        status as ""{nameof(IBaseMessageBox.Status)}"", failure_reason as ""{nameof(IBaseMessageBox.FailureReason)}"",
                        updated_at as ""{nameof(IBaseMessageBox.UpdatedAt)}"", updated_by as ""{nameof(IBaseMessageBox.UpdatedBy)}"",
                        status_comment as ""{nameof(IBaseMessageBox.StatusComment)}""";

    private string SqlQueryToGetUnprocessedEvents => $@"
                SELECT {SqlSelectColumns}
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
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
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
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
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
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
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

    #region GetEventStatusByIdAsync

    private readonly string _sqlGetEventStatusQuery = $@"
                SELECT status FROM {settings.TableName} WHERE id = @Id";

    public async Task<EventStatus?> GetEventStatusByIdAsync(Guid id)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync();

            var status = await dbConnection.QuerySingleOrDefaultAsync<string>(_sqlGetEventStatusQuery, new { Id = id });
            return status is null ? null : Enum.Parse<EventStatus>(status);
        }
        catch (Exception e)
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

    public async Task<TBaseMessage> GetEventByIdAsync(Guid id)
    {
        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync();

            return await dbConnection.QuerySingleOrDefaultAsync<TBaseMessage>(SqlQueryToGetEventById, new { Id = id });
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while getting the event with id {id} from the {TableName} table.");
        }
    }

    #endregion

    #region GetEventsAsync

    public async Task<(TBaseMessage[] Events, long TotalCount)> GetEventsAsync(EventsFilter filter)
    {
        var (whereClause, parameters) = BuildFilterConditions(filter);
        parameters.Add("Skip", filter.GetSkip());
        parameters.Add("Take", filter.GetTake());

        var sqlQuery = $@"
                SELECT COUNT(*) FROM {TableName} {whereClause};

                SELECT {SqlSelectColumns}
                FROM {TableName}
                {whereClause}
                ORDER BY created_at DESC
                OFFSET @Skip
                LIMIT @Take";

        try
        {
            await using var dbConnection = new NpgsqlConnection(ConnectionString);
            await dbConnection.OpenAsync();

            await using var result = await dbConnection.QueryMultipleAsync(sqlQuery, parameters);
            var totalCount = await result.ReadSingleAsync<long>();
            var events = await result.ReadAsync<TBaseMessage>();

            //TODO: Instead of attaching total count, just add indicator to know there is more items or not. 
            return (events.ToArray(), totalCount);
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while getting events from the {TableName} table.");
        }
    }
    
    #endregion

    #region DeleteProcessedEventsAsync

    private readonly string _sqlDeleteEventQuery = $@"
                DELETE FROM {settings.TableName}
                WHERE status = '{nameof(EventStatus.Processed)}' AND updated_at < @ProcessedAt";

    public async Task<bool> DeleteProcessedEventsAsync(DateTime processedAt)
    {
        await using var dbConnection = new NpgsqlConnection(ConnectionString);
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

    /// <summary>
    /// Builds the WHERE clause and its parameters from the filter. Only parameters are used for the values of the filter.
    /// </summary>
    private static (string WhereClause, DynamicParameters Parameters) BuildFilterConditions(EventsFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (filter.Ids?.Length > 0)
        {
            conditions.Add("id = ANY(@Ids)");
            parameters.Add("Ids", filter.Ids);
        }

        if (!string.IsNullOrWhiteSpace(filter.EventName))
        {
            conditions.Add("event_name = @EventName");
            parameters.Add("EventName", filter.EventName);
        }

        if (!string.IsNullOrWhiteSpace(filter.Provider))
        {
            // The outbox events may have multiple providers separated by comma.
            conditions.Add("@Provider = ANY(string_to_array(provider, ','))");
            parameters.Add("Provider", filter.Provider);
        }

        if (filter.Statuses?.Length > 0)
        {
            conditions.Add("status = ANY(@Statuses)");
            parameters.Add("Statuses", filter.Statuses.Select(s => s.ToString()).ToArray());
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

        if (filter.MinTryCount.HasValue)
        {
            conditions.Add("try_count >= @MinTryCount");
            parameters.Add("MinTryCount", filter.MinTryCount.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.FailureReasonContains))
        {
            conditions.Add("failure_reason ILIKE @FailureReasonPattern");
            parameters.Add("FailureReasonPattern", $"%{EscapeLikePattern(filter.FailureReasonContains)}%");
        }

        var whereClause = conditions.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", conditions)}";
        return (whereClause, parameters);
    }

    /// <summary>
    /// Escapes the special characters of the LIKE pattern, so the text is matched as it is.
    /// </summary>
    private static string EscapeLikePattern(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    #endregion
}