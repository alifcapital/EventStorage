namespace EventStorage.Configurations;

public class InboxAndOutboxSettings
{
    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Inbox { get; set; }

    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Outbox { get; set; }
}