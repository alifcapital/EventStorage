using EventStorage.Configurations;
using EventStorage.Inbox.Models;
using EventStorage.Repositories;

namespace EventStorage.Inbox.Repositories;

internal class InboxRepository : EventRepository<InboxEvent>, IInboxRepository
{
    public InboxRepository(InboxOrOutboxStructure settings) : base(settings)
    {
    }
}