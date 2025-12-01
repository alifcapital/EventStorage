using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;

namespace EventStorage.Tests.UnitTests.Inbox;

internal class CleanUpProcessedInboxEventsJobTests : 
    BaseCleanUpProcessedEventsJobTests<IInboxRepository, InboxMessage>;