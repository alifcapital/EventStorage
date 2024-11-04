using EventStorage.Configurations;
using EventStorage.Outbox.Models;
using EventStorage.Repositories;

namespace EventStorage.Outbox.Repositories;

internal class OutboxRepository : EventRepository<OutboxEvent>, IOutboxRepository
{
    public OutboxRepository(InboxOrOutboxStructure settings) : base(settings)
    {
    }
}