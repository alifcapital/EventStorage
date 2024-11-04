using EventStorage.Outbox.Models;
using EventStorage.Repositories;

namespace EventStorage.Outbox.Repositories;

internal interface IOutboxRepository: IEventRepository<OutboxEvent>
{
}