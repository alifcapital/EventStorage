using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;

namespace EventStorage.Tests.UnitTests.Inbox;

[Parallelizable(ParallelScope.Self)]
internal class CleanUpProcessedInboxEventsJobTests : 
    BaseCleanUpProcessedEventsJobTests<IInboxRepository, InboxMessage>;