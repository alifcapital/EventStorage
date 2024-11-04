using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Tests.Infrastructure;

namespace EventStorage.Tests.UnitTests.Inbox
{
    /// <summary>
    /// The class inherits the base class, which contains all the tests
    /// written for the generic type.
    /// </summary>
    [TestFixture]
    internal class InboxRepositoryTests() : EventRepositoryTest<InboxEvent>(
        eventRepository: new InboxRepository(InboxAndOutboxSettings.Inbox),
        dataContext: new DataContext<InboxEvent>(
            InboxAndOutboxSettings.Inbox.ConnectionString,
            InboxAndOutboxSettings.Inbox.TableName
        )
    );
}