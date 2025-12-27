namespace EventStorage.Services;

/// <summary>
/// Interface for creating event store tables
/// </summary>
public interface IEventStoreTablesCreator
{
    /// <summary>
    /// Creates Inbox and Outbox tables if they do not already exist.
    /// </summary>
    void CreateTablesIfNotExists();
}