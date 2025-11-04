using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using EventStorage.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventStorage.Tests.UnitTests.Outbox
{
    /// <summary>
    /// The class inherits the base class, which contains all the tests
    /// written for the generic type.
    /// </summary>
    [TestFixture]
    internal class OutboxRepositoryTests() : EventRepositoryTests<OutboxMessage>(
        eventRepository: new OutboxRepository(NullLogger<OutboxRepository>.Instance, InboxAndOutboxSettings),
        dataContext: new DataContext<OutboxMessage>(
            InboxAndOutboxSettings.Outbox.ConnectionString,
            InboxAndOutboxSettings.Outbox.TableName
        )
    );
}