namespace EventStorage.Configurations;

public class InboxAndOutboxSettings
{
    /// <summary>
    /// Seconds to delay before creating event store tables. Default value is "0".
    /// Sometime, we may need to wait for other systems to create database itself before start processing events.
    /// </summary>
    public int SecondsToDelayBeforeCreatingEventStoreTables { get; init; }

    /// <summary>
    /// Seconds to wait for the exclusive lock of the inbox/outbox table while migrating its old schema. Default value is "30".
    /// If the lock cannot be taken in time, the migration is rolled back and an exception is thrown. The "0" value means waiting without a limit.
    /// </summary>
    public int SecondsToWaitForMigrationLock { get; init; } = 30;

    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Inbox { get; set; }

    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Outbox { get; set; }
}