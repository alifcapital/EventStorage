using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;

namespace EventStorage.Tests.UnitTests.Inbox;

[Parallelizable]
internal class CleanUpProcessedInboxEventsJobTests : 
    BaseCleanUpProcessedEventsJobTests<IInboxRepository, InboxMessage>;