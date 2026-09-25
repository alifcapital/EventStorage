using Dapper;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EventStorage.Tests.UnitTests;

/// <summary>
/// Tests for migrating the old table schema (with the "processed_at" column) to the status schema.
/// Each test uses its own table to not affect the tables of other tests.
/// </summary>
[TestFixture]
internal class StatusSchemaMigrationTests : BaseTestEntity
{
    private string _tableName;
    private string _connectionString;

    #region Setup

    [SetUp]
    public void Setup()
    {
        // Keep the name short, since PostgreSQL truncates the index names (which contain the table name) to 63 characters.
        _tableName = $"inbox_mig_{Guid.NewGuid().ToString("N")[..12]}";
        _connectionString = InboxAndOutboxSettings.Inbox.ConnectionString;
        CreateTableWithOldSchema();
    }

    [TearDown]
    public void TearDown()
    {
        using var dbConnection = new NpgsqlConnection(_connectionString);
        dbConnection.Execute($"DROP TABLE IF EXISTS {_tableName};");
    }

    #endregion

    #region CreateTableIfNotExists

    [Test]
    public void CreateTableIfNotExists_TableHasOldSchema_ShouldMigrateStatusesAndDropProcessedAtColumn()
    {
        var processedAt = DateTime.Now.AddHours(-1);
        var processedEventId = InsertOldSchemaEvent(tryAfterAt: DateTime.Now.AddHours(-2), processedAt: processedAt);
        var pendingEventId = InsertOldSchemaEvent(tryAfterAt: DateTime.Now.AddMinutes(-1), processedAt: null);
        var failedEventId = InsertOldSchemaEvent(tryAfterAt: DateTime.Now.AddHours(1), processedAt: null);
        var repository = CreateRepository();

        repository.CreateTableIfNotExists();

        Assert.That(HasColumn("status"), Is.True);
        Assert.That(HasColumn("failure_reason"), Is.True);
        Assert.That(HasColumn("updated_at"), Is.True);
        Assert.That(HasColumn("updated_by"), Is.True);
        Assert.That(HasColumn("status_comment"), Is.True);
        Assert.That(HasColumn("processed_at"), Is.False);

        var processedEvent = GetStatusAndUpdatedAt(processedEventId);
        Assert.That(processedEvent.Status, Is.EqualTo(nameof(EventStatus.Processed)));
        Assert.That(processedEvent.UpdatedAt, Is.EqualTo(processedAt).Within(TimeSpan.FromSeconds(1)));

        var pendingEvent = GetStatusAndUpdatedAt(pendingEventId);
        Assert.That(pendingEvent.Status, Is.EqualTo(nameof(EventStatus.Pending)));
        Assert.That(pendingEvent.UpdatedAt, Is.Null);

        var failedEvent = GetStatusAndUpdatedAt(failedEventId);
        Assert.That(failedEvent.Status, Is.EqualTo(nameof(EventStatus.Failed)));
        Assert.That(failedEvent.UpdatedAt, Is.Null);
    }

    [Test]
    public void CreateTableIfNotExists_CalledTwiceOnOldSchema_SecondCallShouldNotChangeMigratedData()
    {
        var pendingEventId = InsertOldSchemaEvent(tryAfterAt: DateTime.Now.AddMinutes(-1), processedAt: null);
        var repository = CreateRepository();
        repository.CreateTableIfNotExists();
        using (var dbConnection = new NpgsqlConnection(_connectionString))
        {
            dbConnection.Execute($"UPDATE {_tableName} SET status = '{nameof(EventStatus.Rejected)}' WHERE id = @Id",
                new { Id = pendingEventId });
        }

        Assert.DoesNotThrow(() => repository.CreateTableIfNotExists());

        Assert.That(GetStatusAndUpdatedAt(pendingEventId).Status, Is.EqualTo(nameof(EventStatus.Rejected)));
    }

    [Test]
    public void CreateTableIfNotExists_MigratedTable_NewEventsCanBeStoredAndRead()
    {
        var repository = CreateRepository();
        repository.CreateTableIfNotExists();
        var inboxMessage = new InboxMessage
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent",
            EventPath = "/test/path",
            Payload = "{}",
            TryAfterAt = DateTime.Now.AddMinutes(-1)
        };

        var inserted = repository.InsertEvent(inboxMessage);

        Assert.That(inserted, Is.True);
        Assert.That(GetStatusAndUpdatedAt(inboxMessage.Id).Status, Is.EqualTo(nameof(EventStatus.Pending)));
    }

    [Test]
    public async Task CreateTableIfNotExists_TableIsLockedByAnotherTransaction_ShouldThrowAndKeepOldSchema()
    {
        InsertOldSchemaEvent(tryAfterAt: DateTime.Now.AddMinutes(-1), processedAt: null);
        var repository = CreateRepository(secondsToWaitForMigrationLock: 1);

        await using var lockingConnection = new NpgsqlConnection(_connectionString);
        await lockingConnection.OpenAsync();
        await using var lockingTransaction = await lockingConnection.BeginTransactionAsync();
        await lockingConnection.ExecuteAsync($"LOCK TABLE {_tableName} IN ACCESS SHARE MODE",
            transaction: lockingTransaction);

        Assert.Throws<EventStoreException>(() => repository.CreateTableIfNotExists());

        await lockingTransaction.RollbackAsync();
        Assert.That(HasColumn("status"), Is.False);
        Assert.That(HasColumn("processed_at"), Is.True);
    }

    [Test]
    public async Task CreateTableIfNotExists_EventIsStoredWhileMigrating_ShouldWaitForMigrationAndBeStored()
    {
        var repository = CreateRepository(secondsToWaitForMigrationLock: 30);

        // Hold a lock on the table so the migration has to wait for it, then store an event while the migration waits.
        await using var lockingConnection = new NpgsqlConnection(_connectionString);
        await lockingConnection.OpenAsync();
        var lockingTransaction = await lockingConnection.BeginTransactionAsync();
        await lockingConnection.ExecuteAsync($"LOCK TABLE {_tableName} IN ACCESS SHARE MODE",
            transaction: lockingTransaction);

        var migrationTask = Task.Run(() => repository.CreateTableIfNotExists());
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        var inboxMessage = new InboxMessage
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent",
            EventPath = "/test/path",
            Payload = "{}",
            TryAfterAt = DateTime.Now
        };
        var insertTask = repository.InsertEventAsync(inboxMessage);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.That(migrationTask.IsCompleted, Is.False);
        Assert.That(insertTask.IsCompleted, Is.False);

        await lockingTransaction.RollbackAsync();
        await migrationTask;
        var inserted = await insertTask;

        Assert.That(inserted, Is.True);
        Assert.That(GetStatusAndUpdatedAt(inboxMessage.Id).Status, Is.EqualTo(nameof(EventStatus.Pending)));
    }

    #endregion

    #region Helper methods

    private InboxRepository CreateRepository(int secondsToWaitForMigrationLock = 30)
    {
        var settings = new InboxAndOutboxSettings
        {
            SecondsToWaitForMigrationLock = secondsToWaitForMigrationLock,
            Inbox = InboxAndOutboxSettings.Inbox with { TableName = _tableName },
            Outbox = InboxAndOutboxSettings.Outbox
        };

        return new InboxRepository(NullLogger<InboxRepository>.Instance, settings);
    }

    /// <summary>
    /// Creates the table with the schema of the old versions of the library, which used the "processed_at" column.
    /// </summary>
    private void CreateTableWithOldSchema()
    {
        using var dbConnection = new NpgsqlConnection(_connectionString);
        dbConnection.Execute($@"CREATE TABLE {_tableName}
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
                    processed_at TIMESTAMP(0) DEFAULT NULL
                );

                CREATE INDEX idx_for_get_unprocessed_events_of_{_tableName}
                    ON public.{_tableName} (processed_at, try_after_at);

                CREATE INDEX idx_for_delete_processed_events_of_{_tableName}
                    ON public.{_tableName} (processed_at);");
    }

    private Guid InsertOldSchemaEvent(DateTime tryAfterAt, DateTime? processedAt)
    {
        var id = Guid.NewGuid();
        using var dbConnection = new NpgsqlConnection(_connectionString);
        dbConnection.Execute($@"INSERT INTO {_tableName}
                    (id, provider, event_name, event_path, payload, created_at, try_count, try_after_at, processed_at)
                VALUES
                    (@Id, 'TestProvider', 'TestEvent', '/test/path', '{{}}'::jsonb, @CreatedAt, 0, @TryAfterAt, @ProcessedAt)",
            new { Id = id, CreatedAt = DateTime.Now, TryAfterAt = tryAfterAt, ProcessedAt = processedAt });

        return id;
    }

    private bool HasColumn(string columnName)
    {
        using var dbConnection = new NpgsqlConnection(_connectionString);
        return dbConnection.ExecuteScalar<bool>(@"SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = @TableName AND column_name = @ColumnName
                )", new { TableName = _tableName, ColumnName = columnName });
    }

    private (string Status, DateTime? UpdatedAt) GetStatusAndUpdatedAt(Guid id)
    {
        using var dbConnection = new NpgsqlConnection(_connectionString);
        return dbConnection.QuerySingle<(string, DateTime?)>(
            $"SELECT status, updated_at FROM {_tableName} WHERE id = @Id", new { Id = id });
    }

    #endregion
}
