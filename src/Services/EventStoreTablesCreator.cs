using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.Repositories;

namespace EventStorage.Services;

internal class EventStoreTablesCreator(
    IInboxRepository inboxRepository = null,
    IOutboxRepository outboxRepository = null) : IEventStoreTablesCreator
{
    public void CreateTablesIfNotExists()
    {
        inboxRepository?.CreateTableIfNotExists();
        outboxRepository?.CreateTableIfNotExists();
    }
}