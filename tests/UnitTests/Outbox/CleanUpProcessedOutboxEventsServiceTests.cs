using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;

namespace EventStorage.Tests.UnitTests.Outbox;

/// <summary>
/// The class inherits the base class, which contains all the tests
/// written for the generic type.
/// </summary>
[TestFixture]
internal class CleanUpProcessedOutboxEventsServiceTests : 
    CleanUpProcessedEventsServiceTests<IOutboxRepository, OutboxEvent>;