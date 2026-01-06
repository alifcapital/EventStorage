namespace EventStorage.Configurations;

public class InboxAndOutboxSettings
{
    /// <summary>
    /// Seconds to delay before creating event store tables. Default value is "0".
    /// Sometime, we may need to wait for other systems to create database itself before start processing events.
    /// </summary>
    public int SecondsToDelayBeforeCreatingEventStoreTables { get; init; }
    
    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Inbox { get; set; }

    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Outbox { get; set; }
}