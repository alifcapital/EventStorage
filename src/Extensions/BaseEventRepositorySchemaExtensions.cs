using Dapper;
using EventStorage.Exceptions;
using EventStorage.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventStorage.Extensions;

/// <summary>
/// The extensions for creating and migrating the schema of the Inbox and Outbox tables.
/// All schema changes of the tables (new columns, indexes, migrations of the old schemas) must be added here
/// instead of changing the <see cref="BaseEventRepository{TBaseMessage}"/>.
/// </summary>
internal static class BaseEventRepositorySchemaExtensions
{
    /// <summary>
    /// Creates the table and its indexes if they do not exist and migrates the old schemas of the table if it is needed.
    /// </summary>
    internal static void CreateOrMigrateTableSchema<TBaseMessage>(this BaseEventRepository<TBaseMessage> repository)
        where TBaseMessage : class, IBaseMessageBox
    {
        var tableName = repository.TableName;
        try
        {
            using var dbConnection = new NpgsqlConnection(repository.ConnectionString);
            dbConnection.Open();

            dbConnection.Execute(CreateTableSqlScript(tableName, repository.HasNamingPolicyColumn));
            dbConnection.Execute(MigratePayloadColumnToJsonbScript(tableName));
            repository.MigrateToStatusSchemaIfNeeded(dbConnection);
            dbConnection.Execute(CreateIndexesScript(tableName));
        }
        catch (EventStoreException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new EventStoreException(e, $"Error while checking/creating {tableName} table.");
        }
    }

    #region Create tables or indexes if not exists

    /// <summary>
    /// The SQL script for creating the table for storing events if it does not exist.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="hasNamingPolicyColumn">Whether the table has the naming policy column. The outbox table does not have it.</param>
    private static string CreateTableSqlScript(string tableName, bool hasNamingPolicyColumn) => $@"CREATE TABLE IF NOT EXISTS {tableName}
                (
                    id UUID NOT NULL PRIMARY KEY,
                    provider VARCHAR(50) NOT NULL,
                    event_name VARCHAR(100) NOT NULL,
                    event_path VARCHAR(255),
                    payload JSONB,
                    headers TEXT,
                    additional_data TEXT,{(hasNamingPolicyColumn ? @"
                    naming_policy_type VARCHAR(15)," : string.Empty)}
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
    private static string MigratePayloadColumnToJsonbScript(string tableName) => $@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = '{tableName}'
                            AND column_name = 'payload'
                            AND data_type = 'text'
                    ) THEN
                        ALTER TABLE {tableName} ALTER COLUMN payload TYPE JSONB USING payload::jsonb;
                    END IF;
                END
                $$;";

    /// <summary>
    /// The SQL script for creating indexes for the table: for getting unprocessed events, for deleting processed events
    /// and for filtering events by the creation time and the event name.
    /// </summary>
    private static string CreateIndexesScript(string tableName) => $@"CREATE INDEX IF NOT EXISTS idx_{tableName}_status_try_after_at
                    ON public.{tableName} (status, try_after_at);

                CREATE INDEX IF NOT EXISTS idx_{tableName}_status_updated_at
                    ON public.{tableName} (status, updated_at);

                CREATE INDEX IF NOT EXISTS idx_{tableName}_created_at
                    ON public.{tableName} (created_at);

                CREATE INDEX IF NOT EXISTS idx_{tableName}_event_name_created_at
                    ON public.{tableName} (event_name, created_at);";

    #endregion

    #region Migrate to status schema

    /// <summary>
    /// The SQL query for checking whether the table already has the "status" column. If not, the table has the old schema.
    /// </summary>
    private static string SqlQueryToCheckStatusColumnExists(string tableName) => $@"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                        AND table_name = '{tableName}'
                        AND column_name = 'status'
                )";

    /// <summary>
    /// The SQL script for adding all new columns of the status schema.
    /// </summary>
    private static string AddStatusColumnsScript(string tableName) => $@"
                ALTER TABLE {tableName}
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
    private static string MigrateExistingEventsToStatusScript(string tableName) => $@"
                UPDATE {tableName}
                SET
                    status = CASE
                        WHEN processed_at IS NOT NULL THEN '{nameof(EventStatus.Processed)}'
                        WHEN try_after_at <= @CurrentTime THEN '{nameof(EventStatus.Pending)}'
                        ELSE '{nameof(EventStatus.Failed)}'
                    END,
                    updated_at = processed_at;";

    /// <summary>
    /// The SQL script for removing the old "processed_at" column.
    /// </summary>
    private static string DropProcessedAtColumnScript(string tableName) =>
        $@"ALTER TABLE {tableName} DROP COLUMN IF EXISTS processed_at;";

    /// <summary>
    /// The SQL script for removing the indexes of the old schema if they still exist. PostgreSQL drops them together
    /// with the "processed_at" column, but they are dropped explicitly to not depend on it.
    /// </summary>
    private static string DropOldIndexesScript(string tableName) => $@"
                DROP INDEX IF EXISTS public.idx_for_get_unprocessed_events_of_{tableName};
                DROP INDEX IF EXISTS public.idx_for_delete_processed_events_of_{tableName};";

    /// <summary>
    /// Migrates the table from the old schema (with the "processed_at" column) to the status schema if it is needed.
    /// It runs in a single transaction and locks the table exclusively, so no event can be read or written while migrating.
    /// </summary>
    private static void MigrateToStatusSchemaIfNeeded<TBaseMessage>(this BaseEventRepository<TBaseMessage> repository,
        NpgsqlConnection dbConnection)
        where TBaseMessage : class, IBaseMessageBox
    {
        var tableName = repository.TableName;
        var secondsToWaitForMigrationLock = repository.SecondsToWaitForMigrationLock;
        using var transaction = dbConnection.BeginTransaction();

        // Only one application instance migrates at a time; the others wait and then find the table already migrated.
        dbConnection.Execute("SELECT pg_advisory_xact_lock(hashtext(@LockName))",
            new { LockName = $"{tableName}_schema_migration" }, transaction, commandTimeout: 0);

        var hasStatusColumn = dbConnection.ExecuteScalar<bool>(SqlQueryToCheckStatusColumnExists(tableName),
            transaction: transaction);
        if (hasStatusColumn)
        {
            transaction.Commit();
            return;
        }

        repository.Logger.LogWarning("{StorageType}: Migrating the {TableName} table to the status schema.",
            repository.StorageTypeTag, tableName);
        try
        {
            dbConnection.Execute($"SET LOCAL lock_timeout = '{secondsToWaitForMigrationLock}s'",
                transaction: transaction);
            dbConnection.Execute($"LOCK TABLE {tableName} IN ACCESS EXCLUSIVE MODE", transaction: transaction,
                commandTimeout: 0);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new EventStoreException(e,
                $"Could not lock the {tableName} table within {secondsToWaitForMigrationLock} seconds to migrate it to the status schema. The migration is rolled back and will be retried on the next start.");
        }

        dbConnection.Execute(AddStatusColumnsScript(tableName), transaction: transaction, commandTimeout: 0);
        dbConnection.Execute(MigrateExistingEventsToStatusScript(tableName), new { CurrentTime = DateTime.Now },
            transaction, commandTimeout: 0);
        dbConnection.Execute(DropProcessedAtColumnScript(tableName), transaction: transaction, commandTimeout: 0);
        dbConnection.Execute(DropOldIndexesScript(tableName), transaction: transaction, commandTimeout: 0);

        transaction.Commit();
        repository.Logger.LogWarning("{StorageType}: The {TableName} table is migrated to the status schema.",
            repository.StorageTypeTag, tableName);
    }

    #endregion
}
