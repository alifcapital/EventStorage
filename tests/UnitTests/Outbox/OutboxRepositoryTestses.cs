using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using EventStorage.Tests.Infrastructure;

namespace EventStorage.Tests.UnitTests.Outbox
{
    /// <summary>
    /// The class inherits the base class, which contains all the tests
    /// written for the generic type.
    /// </summary>
    [TestFixture]
    internal class OutboxRepositoryTestses() : EventRepositoryTests<OutboxMessage>(
        eventRepository: new OutboxRepository(InboxAndOutboxSettings.Outbox),
        dataContext: new DataContext<OutboxMessage>(
            InboxAndOutboxSettings.Outbox.ConnectionString,
            InboxAndOutboxSettings.Outbox.TableName
        )
    );
}