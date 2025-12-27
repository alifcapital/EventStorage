namespace EventStorage.Configurations;

public record InboxOrOutboxStructure
{
    /// <summary>
    /// To enable using an inbox/outbox for storing all received/sending events. Default value is "false".
    /// </summary>
    public bool IsEnabled { get; init; } = false;

    /// <summary>
    /// The table name of Inbox/Outbox for storing all received/sending events. Default value is "Inbox" if it is for Inbox, otherwise "Outbox".
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Maximum concurrency tasks to execute received/publishing events. Default value is "10".
    /// </summary>
    public int MaxConcurrency { get; init; } = 10;
    
    /// <summary>
    /// The maximum number of events to fetch and process in a single batch. Default value is "100".
    /// </summary>
    public int MaxEventsToFetch { get; init; } = 100;

    /// <summary>
    /// For increasing the TryAfterAt when the TryCount is higher than the value. Default value is "10".
    /// </summary>
    public int TryCount { get; init; } = 10;

    /// <summary>
    /// For increasing the TryAfterAt to amount of minutes if the event fails. Default value is "5".
    /// </summary>
    public int TryAfterMinutes { get; init; } = 5;

    /// <summary>
    /// For increasing the TryAfterAt to amount of minutes if the event not found to publish or receive. Default value is "60".
    /// </summary>
    public int TryAfterMinutesIfEventNotFound { get; init; } = 60;

    /// <summary>
    /// Seconds to delay for processing events. Default value is "1".
    /// </summary>
    public int SecondsToDelayProcessEvents { get; init; } = 1;

    /// <summary>
    /// Seconds to delay before creating event store tables. Default value is "0".
    /// Sometime, we may need to wait for other systems to create database itself before start processing events.
    /// </summary>
    public int SecondsToDelayBeforeCreateEventStoreTables { get; init; } = 0;

    /// <summary>
    /// Days to cleaning up the processed events. Default value is "0". It will work when value is higher than or equal 1.
    /// </summary>
    public int DaysToCleanUpEvents { get; init; }

    /// <summary>
    /// Hours to delay for cleaning up the processed events. Default value is "1".
    /// </summary>
    public int HoursToDelayCleanUpEvents { get; init; } = 1;

    /// <summary>
    /// The database connection string of Inbox/Outbox for storing or reading all received/sending events.
    /// </summary>
    public string ConnectionString { get; set; }
}