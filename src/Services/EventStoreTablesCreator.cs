using EventStorage.Configurations;
using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.Repositories;

namespace EventStorage.Services;

internal class EventStoreTablesCreator(
    InboxAndOutboxSettings settings,
    IInboxRepository inboxRepository = null,
    IOutboxRepository outboxRepository = null) : IEventStoreTablesCreator
{
    /// <summary>
    /// Semaphore to limit the number of concurrent table creation to 1.
    /// This is to prevent multiple instances of the application from trying to run migrations at the same time.
    /// </summary>
    private static readonly SemaphoreSlim LimitToExecuteTableCreation = new(1, 1);
    
    public async Task CreateTablesIfNotExistsAsync(CancellationToken cancellationToken)
    {
        var timeToDelay = TimeSpan.FromSeconds(settings.SecondsToDelayBeforeCreateEventStoreTables);
        await Task.Delay(timeToDelay, cancellationToken);
        
        await LimitToExecuteTableCreation.WaitAsync(cancellationToken);

        try
        {
            inboxRepository?.CreateTableIfNotExists();
            outboxRepository?.CreateTableIfNotExists();
        }
        finally
        {
            LimitToExecuteTableCreation.Release();
        }
    }
}