using EventStorage.Configurations;
using EventStorage.Outbox.Models;
using EventStorage.Repositories;

namespace EventStorage.Outbox.Repositories;

internal class OutboxRepository(InboxOrOutboxStructure settings)
    : EventRepository<OutboxMessage>(settings), IOutboxRepository;