using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;

namespace EventStorage.Tests.UnitTests.Inbox;

[TestFixture]
internal class CleanUpProcessedInboxEventsServiceTests : 
    CleanUpProcessedEventsServiceTests<IInboxRepository, InboxEvent>;