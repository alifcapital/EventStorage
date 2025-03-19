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
    internal class InboxRepositoryTestses() : EventRepositoryTests<InboxMessage>(
        eventRepository: new InboxRepository(InboxAndOutboxSettings.Inbox),
        dataContext: new DataContext<InboxMessage>(
            InboxAndOutboxSettings.Inbox.ConnectionString,
            InboxAndOutboxSettings.Inbox.TableName
        )
    );
}