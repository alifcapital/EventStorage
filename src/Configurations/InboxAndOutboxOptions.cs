namespace EventStorage.Configurations;

public class InboxAndOutboxOptions
{
    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Inbox { get; }

    /// <summary>
    /// For getting settings of an Inbox.
    /// </summary>
    public InboxOrOutboxStructure Outbox { get; }

    public InboxAndOutboxOptions(InboxAndOutboxSettings inboxAndOutboxSettings)
    {
        Inbox = inboxAndOutboxSettings.Inbox;
        Outbox = inboxAndOutboxSettings.Outbox;
    }
}