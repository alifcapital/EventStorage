using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventStorage.Tests.UnitTests.Inbox
{
    /// <summary>
    /// The class inherits the base class, which contains all the tests
    /// written for the generic type.
    /// </summary>
    [TestFixture]
    internal class InboxRepositoryTests() : EventRepositoryTests<InboxMessage>(
        eventRepository: new InboxRepository(NullLogger<InboxRepository>.Instance, InboxAndOutboxSettings),
        dataContext: new DataContext<InboxMessage>(
            InboxAndOutboxSettings.Inbox.ConnectionString,
            InboxAndOutboxSettings.Inbox.TableName
        )
    );
}