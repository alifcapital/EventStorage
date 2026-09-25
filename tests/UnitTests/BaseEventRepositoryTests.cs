using EventStorage.Management.Models;
using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Tests.Configs;
using EventStorage.Tests.Infrastructure;
using EventStorage.Tests.Infrastructure.Extensions;

namespace EventStorage.Tests.UnitTests;

internal abstract class BaseEventRepositoryTests<TEvent> : BaseTestEntity where TEvent : BaseMessageBox, new()
{
    protected readonly BaseEventRepository<TEvent> Repository;
    protected readonly DataContext<TEvent> DataContext;

    internal BaseEventRepositoryTests(
        BaseEventRepository<TEvent> baseEventRepository,
        DataContext<TEvent> dataContext
    )
    {
        Repository = baseEventRepository;
        DataContext = dataContext;
    }

    #region CreateTableIfNotExists

    [Test]
    public void CreateTableIfNotExists_ShouldCreateTable()
    {
        Assert.That(DataContext.ExistTable(), Is.True);
    }

    #endregion

    #region InsertEvent

    [Test]
    public void InsertEvent_OneItem_EventShouldBeInserted()
    {
        var eventBox = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent",
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(5)
        };

        var result = Repository.InsertEvent(eventBox);

        Assert.That(result, Is.True);
        var eventFromDb = DataContext.GetById(eventBox.Id);

        Assert.That(eventFromDb.Id, Is.EqualTo(eventBox.Id));
        Assert.That(eventFromDb.EventName, Is.EqualTo(eventBox.EventName));
        Assert.That(eventFromDb.TryCount, Is.EqualTo(eventBox.TryCount));
    }

    #endregion

    #region InsertEventAsync

    [Test]
    public async Task InsertEventAsync_OneItem_EventShouldBeInserted()
    {
        var eventBox = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent",
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(5)
        };

        var result = await Repository.InsertEventAsync(eventBox);

        Assert.That(result, Is.True);
        var eventFromDb = DataContext.GetById(eventBox.Id);

        Assert.That(eventFromDb.Id, Is.EqualTo(eventBox.Id));
        Assert.That(eventFromDb.EventName, Is.EqualTo(eventBox.EventName));
    }

    #endregion

    #region BulkInsertEvents

    [Test]
    public void BulkInsertEvents_AddedTwoItems_BothEventsShouldBeInserted()
    {
        var firstEvent = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent1",
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(5)
        };
        var secondEvent = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent2",
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(5)
        };

        var result = Repository.BulkInsertEvents([firstEvent, secondEvent]);

        Assert.That(result, Is.True);
        var firstEventFromDb = DataContext.GetById(firstEvent.Id);
        Assert.That(firstEventFromDb.Id, Is.EqualTo(firstEvent.Id));
        Assert.That(firstEventFromDb.EventName, Is.EqualTo(firstEvent.EventName));

        var secondEventFromDb = DataContext.GetById(secondEvent.Id);
        Assert.That(secondEventFromDb.Id, Is.EqualTo(secondEvent.Id));
        Assert.That(secondEventFromDb.EventName, Is.EqualTo(secondEvent.EventName));
    }

    #endregion

    #region BulkInsertEventsAsync

    [Test]
    public async Task BulkInsertEventsAsync_AddedTwoItems_BothEventsShouldBeInserted()
    {
        var firstEvent = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent1",
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(5)
        };
        var secondEvent = new TEvent()
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent2",
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            NamingPolicyType = NamingPolicyTypeNames.PascalCase,
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(5)
        };

        var result = await Repository.BulkInsertEventsAsync([firstEvent, secondEvent]);

        Assert.That(result, Is.True);
        var firstEventFromDb = DataContext.GetById(firstEvent.Id);
        Assert.That(firstEventFromDb.Id, Is.EqualTo(firstEvent.Id));
        Assert.That(firstEventFromDb.EventName, Is.EqualTo(firstEvent.EventName));

        var secondEventFromDb = DataContext.GetById(secondEvent.Id);
        Assert.That(secondEventFromDb.Id, Is.EqualTo(secondEvent.Id));
        Assert.That(secondEventFromDb.EventName, Is.EqualTo(secondEvent.EventName));
    }

    #endregion

    #region GetUnprocessedEventsAsync

    [Test]
    public async Task GetUnprocessedEventsAsync_TwoItems_ShouldReturnPendingEvents()
    {
        var baseEventBox1 = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider1",
            EventName = "TestEvent1" + typeof(TEvent).FullName,
            EventPath = "/test/path1",
            Payload = "{}",
            Headers = "TestHeaders1",
            AdditionalData = "TestAdditionalData1",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(-1)
        };

        var baseEventBox2 = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider2",
            EventName = "TestEvent2" + typeof(TEvent).FullName,
            EventPath = "/test/path2",
            Payload = "{}",
            Headers = "TestHeaders2",
            AdditionalData = "TestAdditionalData2",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(5)
        };

        var baseEventBox3 = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider3",
            EventName = "TestEvent3" + typeof(TEvent).FullName,
            EventPath = "/test/path2",
            Payload = "{}",
            Headers = "TestHeaders2",
            AdditionalData = "TestAdditionalData2",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddMinutes(-3)
        };

        await Repository.BulkInsertEventsAsync([baseEventBox1, baseEventBox2, baseEventBox3]);

        var result = await Repository.GetUnprocessedEventsAsync(5);

        Assert.That(result.Length, Is.EqualTo(2));

        var firstEvent = result.Single(e => e.Id == baseEventBox1.Id);
        Assert.That(firstEvent, Is.Not.Null);
        Assert.That(firstEvent,
            IsClass.EquivalentTo(baseEventBox1, nameof(baseEventBox1.CreatedAt), nameof(baseEventBox1.TryAfterAt)));
        Assert.That(firstEvent.TryAfterAt, Is.EqualTo(baseEventBox1.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task
        GetUnprocessedEventsAsync_ProcessedFailedAndRejectedEvents_ShouldReturnOnlyPendingAndFailedEvents()
    {
        var pendingEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        var failedEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        var processedEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        var rejectedEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        await Repository.BulkInsertEventsAsync([pendingEvent, failedEvent, processedEvent, rejectedEvent]);

        failedEvent.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "Test failure");
        processedEvent.Processed();
        rejectedEvent.Rejected();
        await Repository.UpdateEventsAsync([failedEvent, processedEvent, rejectedEvent]);

        try
        {
            var result = await Repository.GetUnprocessedEventsAsync(500);
            var resultIds = result.Select(e => e.Id).ToArray();

            Assert.That(resultIds, Does.Contain(pendingEvent.Id));
            Assert.That(resultIds, Does.Contain(failedEvent.Id));
            Assert.That(resultIds, Does.Not.Contain(processedEvent.Id));
            Assert.That(resultIds, Does.Not.Contain(rejectedEvent.Id));
            Assert.That(result.Single(e => e.Id == failedEvent.Id).Status, Is.EqualTo(EventStatus.Failed));
        }
        finally
        {
            // The table is shared by the tests of the fixture, so we do not leave unprocessed events for other tests.
            pendingEvent.Processed();
            failedEvent.Processed();
            await Repository.UpdateEventsAsync([pendingEvent, failedEvent]);
        }
    }

    #endregion

    #region UpdateEventAsync

    [Test]
    public async Task UpdateEventAsync_OneItem_ShouldUpdateEvent()
    {
        var outboxEvent = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent",
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0
        };

        await Repository.InsertEventAsync(outboxEvent);

        // Modify the event
        outboxEvent.TryCount = 1;
        outboxEvent.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent.Processed();

        var result = await Repository.UpdateEventAsync(outboxEvent);

        Assert.That(result, Is.True);

        var updatedEvent = DataContext.GetById(outboxEvent.Id);

        Assert.That(updatedEvent.TryCount, Is.EqualTo(outboxEvent.TryCount));
        Assert.That(updatedEvent.TryAfterAt, Is.EqualTo(outboxEvent.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(updatedEvent.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(updatedEvent.UpdatedAt, Is.EqualTo(outboxEvent.UpdatedAt).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task UpdateEventAsync_EventIsRejected_ShouldStoreStatusNameAsString()
    {
        var rejectedEvent = CreateEvent(DateTime.Now);
        await Repository.InsertEventAsync(rejectedEvent);
        Assert.That(DataContext.GetStoredStatusById(rejectedEvent.Id), Is.EqualTo(nameof(EventStatus.Pending)));

        rejectedEvent.Rejected();
        await Repository.UpdateEventAsync(rejectedEvent);

        Assert.That(DataContext.GetStoredStatusById(rejectedEvent.Id), Is.EqualTo(nameof(EventStatus.Rejected)));
        Assert.That(DataContext.GetById(rejectedEvent.Id).Status, Is.EqualTo(EventStatus.Rejected));
    }

    #endregion

    #region UpdateEventsAsync

    [Test]
    public async Task UpdateEventsAsync_TwoItems_ShouldUpdateEvents()
    {
        var outboxEvent1 = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider1",
            EventName = "TestEvent1",
            EventPath = "/test/path1",
            Payload = "{}",
            Headers = "TestHeaders1",
            AdditionalData = "TestAdditionalData1",
            TryCount = 0
        };

        var outboxEvent2 = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider2",
            EventName = "TestEvent2",
            EventPath = "/test/path2",
            Payload = "{}",
            Headers = "TestHeaders2",
            AdditionalData = "TestAdditionalData2",
            TryCount = 0
        };

        await Repository.BulkInsertEventsAsync([outboxEvent1, outboxEvent2]);

        // Modify the events
        outboxEvent1.TryCount = 1;
        outboxEvent1.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent1.Processed();

        outboxEvent2.TryCount = 1;
        outboxEvent2.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent2.Processed();

        var result = await Repository.UpdateEventsAsync(new List<TEvent> { outboxEvent1, outboxEvent2 });

        Assert.That(result, Is.True);

        var updatedEvent1 = DataContext.GetById(outboxEvent1.Id);
        var updatedEvent2 = DataContext.GetById(outboxEvent2.Id);

        Assert.That(updatedEvent1.TryCount, Is.EqualTo(outboxEvent1.TryCount));
        Assert.That(updatedEvent1.TryAfterAt, Is.EqualTo(outboxEvent1.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(updatedEvent1.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(updatedEvent1.UpdatedAt, Is.EqualTo(outboxEvent1.UpdatedAt).Within(TimeSpan.FromSeconds(1)));

        Assert.That(updatedEvent2.TryCount, Is.EqualTo(outboxEvent2.TryCount));
        Assert.That(updatedEvent2.TryAfterAt, Is.EqualTo(outboxEvent2.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(updatedEvent2.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(updatedEvent2.UpdatedAt, Is.EqualTo(outboxEvent2.UpdatedAt).Within(TimeSpan.FromSeconds(1)));
    }

    #endregion

    #region GetEventStatusAsync

    // The events are created with a future try time, so they do not affect the tests of getting unprocessed events.
    [Test]
    public async Task GetEventStatusAsync_EventExistsAndProcessed_ShouldReturnProcessed()
    {
        var processedEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(processedEvent);

        processedEvent.Processed();
        await Repository.UpdateEventAsync(processedEvent);

        var status = await Repository.GetEventStatusByIdAsync(processedEvent.Id);

        Assert.That(status, Is.EqualTo(EventStatus.Processed));
    }

    [Test]
    public async Task GetEventStatusAsync_EventExistsButNotProcessed_ShouldReturnPending()
    {
        var unprocessedEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(unprocessedEvent);

        var status = await Repository.GetEventStatusByIdAsync(unprocessedEvent.Id);

        Assert.That(status, Is.EqualTo(EventStatus.Pending));
    }

    [Test]
    public async Task GetEventStatusAsync_EventDoesNotExist_ShouldReturnNull()
    {
        var status = await Repository.GetEventStatusByIdAsync(Guid.NewGuid());

        Assert.That(status, Is.Null);
    }

    [Test]
    public async Task GetEventStatusAsync_EventIsRejected_ShouldReturnRejected()
    {
        var rejectedEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(rejectedEvent);

        rejectedEvent.Rejected();
        await Repository.UpdateEventAsync(rejectedEvent);

        var status = await Repository.GetEventStatusByIdAsync(rejectedEvent.Id);

        Assert.That(status, Is.EqualTo(EventStatus.Rejected));
    }

    #endregion

    #region GetEventByIdAsync

    [Test]
    public async Task GetEventByIdAsync_EventIsFailed_ShouldReturnEventWithFailureReasonAndManualChangeInfo()
    {
        var failedEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(failedEvent);

        failedEvent.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "System.Exception: Test failure",
            performedBy: "operator", comment: "Manual execution");
        await Repository.UpdateEventAsync(failedEvent);

        var result = await Repository.GetEventByIdAsync(failedEvent.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(failedEvent.Id));
        Assert.That(result.EventName, Is.EqualTo(failedEvent.EventName));
        Assert.That(result.Payload, Is.EqualTo(failedEvent.Payload));
        Assert.That(result.TryCount, Is.EqualTo(1));
        Assert.That(result.Status, Is.EqualTo(EventStatus.Failed));
        Assert.That(result.FailureReason, Is.EqualTo("System.Exception: Test failure"));
        Assert.That(result.UpdatedBy, Is.EqualTo("operator"));
        Assert.That(result.StatusComment, Is.EqualTo("Manual execution"));
    }

    [Test]
    public async Task GetEventByIdAsync_EventDoesNotExist_ShouldReturnNull()
    {
        var result = await Repository.GetEventByIdAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    #endregion

    #region DeleteProcessedEventsAsync

    [Test]
    public async Task DeleteProcessedEventsAsync_OneItems_ShouldDeleteProcessedEvents()
    {
        var processedAt = DateTime.Now.AddMinutes(-10);
        var event1 = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider1",
            EventName = "TestEvent1",
            EventPath = "/test/path1",
            Payload = "{}",
            Headers = "TestHeaders1",
            AdditionalData = "TestAdditionalData1",
            TryCount = 0
        };

        var event2 = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider2",
            EventName = "TestEvent2",
            EventPath = "/test/path2",
            Payload = "{}",
            Headers = "TestHeaders2",
            AdditionalData = "TestAdditionalData2",
            TryCount = 0
        };

        await Repository.BulkInsertEventsAsync([event1, event2]);

        SetProcessedTimeOfEvent(event1, DateTime.Now.AddMinutes(-20));
        SetProcessedTimeOfEvent(event2, DateTime.Now.AddMinutes(-5));
        await Repository.UpdateEventsAsync([event1, event2]);

        var result = await Repository.DeleteProcessedEventsAsync(processedAt);
        Assert.That(result, Is.True);
        Assert.That(DataContext.ExistsById(event1.Id), Is.False);
        Assert.That(DataContext.ExistsById(event2.Id), Is.True);
    }

    [Test]
    public async Task DeleteProcessedEventsAsync_RejectedEvent_ShouldNotBeDeleted()
    {
        var rejectedEvent = CreateEvent(DateTime.Now);
        await Repository.InsertEventAsync(rejectedEvent);

        rejectedEvent.Rejected();
        rejectedEvent.SetPropertyValue(nameof(BaseMessageBox.UpdatedAt), DateTime.Now.AddDays(-1));
        await Repository.UpdateEventAsync(rejectedEvent);

        await Repository.DeleteProcessedEventsAsync(DateTime.Now);
        Assert.That(DataContext.ExistsById(rejectedEvent.Id), Is.True);
    }

    #endregion

    #region GetEventsAsync

    // Each test uses a unique event name to not see the events of other tests, and a future try time,
    // so the events do not affect the tests of getting unprocessed events.

    [Test]
    public async Task GetEventsAsync_FilterByEventNameAndStatuses_ShouldReturnOnlyMatchingEvents()
    {
        var eventName = CreateUniqueEventName();
        var pendingEvent = CreateEvent(DateTime.Now.AddHours(1), eventName);
        var failedEvent = CreateEvent(DateTime.Now.AddHours(1), eventName);
        var rejectedEvent = CreateEvent(DateTime.Now.AddHours(1), eventName);
        var otherEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.BulkInsertEventsAsync([pendingEvent, failedEvent, rejectedEvent, otherEvent]);
        failedEvent.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "Test failure");
        rejectedEvent.Rejected();
        await Repository.UpdateEventsAsync([failedEvent, rejectedEvent]);

        var (events, totalCount) = await Repository.GetEventsAsync(new EventsFilter
        {
            EventName = eventName,
            Statuses = [EventStatus.Failed, EventStatus.Rejected]
        });

        Assert.That(totalCount, Is.EqualTo(2));
        Assert.That(events.Select(e => e.Id), Is.EquivalentTo(new[] { failedEvent.Id, rejectedEvent.Id }));
        Assert.That(events.Single(e => e.Id == failedEvent.Id).FailureReason, Is.EqualTo("Test failure"));
    }

    [Test]
    public async Task GetEventsAsync_FilterByProvider_ShouldMatchEventsWithMultipleProviders()
    {
        var eventName = CreateUniqueEventName();
        var singleProviderEvent = CreateEvent(DateTime.Now.AddHours(1), eventName, provider: "Sms");
        var multipleProvidersEvent = CreateEvent(DateTime.Now.AddHours(1), eventName, provider: "MessageBroker,Sms");
        var otherProviderEvent = CreateEvent(DateTime.Now.AddHours(1), eventName, provider: "Email");
        await Repository.BulkInsertEventsAsync([singleProviderEvent, multipleProvidersEvent, otherProviderEvent]);

        var (events, totalCount) = await Repository.GetEventsAsync(new EventsFilter
        {
            EventName = eventName,
            Provider = "Sms"
        });

        Assert.That(totalCount, Is.EqualTo(2));
        Assert.That(events.Select(e => e.Id),
            Is.EquivalentTo(new[] { singleProviderEvent.Id, multipleProvidersEvent.Id }));
    }

    [Test]
    public async Task GetEventsAsync_FilterByFailureReasonWithSpecialCharacters_ShouldMatchTextAsItIs()
    {
        var eventName = CreateUniqueEventName();
        var matchingEvent = CreateEvent(DateTime.Now.AddHours(1), eventName);
        var notMatchingEvent = CreateEvent(DateTime.Now.AddHours(1), eventName);
        await Repository.BulkInsertEventsAsync([matchingEvent, notMatchingEvent]);
        matchingEvent.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "Timeout: 100% of pool_size used");
        notMatchingEvent.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "Timeout: 100 of pool size used");
        await Repository.UpdateEventsAsync([matchingEvent, notMatchingEvent]);

        var (events, _) = await Repository.GetEventsAsync(new EventsFilter
        {
            EventName = eventName,
            FailureReasonContains = "100% OF POOL_SIZE"
        });

        Assert.That(events.Select(e => e.Id), Is.EquivalentTo(new[] { matchingEvent.Id }));
    }

    [Test]
    public async Task GetEventsAsync_FilterByIdsMinTryCountAndCreatedAt_ShouldReturnOnlyMatchingEvents()
    {
        var eventName = CreateUniqueEventName();
        var triedEvent = CreateEvent(DateTime.Now.AddHours(1), eventName);
        var notTriedEvent = CreateEvent(DateTime.Now.AddHours(1), eventName);
        await Repository.BulkInsertEventsAsync([triedEvent, notTriedEvent]);
        triedEvent.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "Test failure");
        await Repository.UpdateEventAsync(triedEvent);

        var (events, totalCount) = await Repository.GetEventsAsync(new EventsFilter
        {
            Ids = [triedEvent.Id, notTriedEvent.Id],
            MinTryCount = 1,
            CreatedFrom = DateTime.Now.AddMinutes(-1),
            CreatedTo = DateTime.Now.AddMinutes(1)
        });

        Assert.That(totalCount, Is.EqualTo(1));
        Assert.That(events.Single().Id, Is.EqualTo(triedEvent.Id));
    }

    [Test]
    public async Task GetEventsAsync_SkipAndTake_ShouldReturnPageWithTotalCount()
    {
        var eventName = CreateUniqueEventName();
        await Repository.BulkInsertEventsAsync([
            CreateEvent(DateTime.Now.AddHours(1), eventName),
            CreateEvent(DateTime.Now.AddHours(1), eventName),
            CreateEvent(DateTime.Now.AddHours(1), eventName)
        ]);

        var (firstPage, totalCount) = await Repository.GetEventsAsync(new EventsFilter
            { EventName = eventName, Skip = 0, Take = 2 });
        var (secondPage, _) = await Repository.GetEventsAsync(new EventsFilter
            { EventName = eventName, Skip = 2, Take = 2 });

        Assert.That(totalCount, Is.EqualTo(3));
        Assert.That(firstPage, Has.Length.EqualTo(2));
        Assert.That(secondPage, Has.Length.EqualTo(1));
        Assert.That(firstPage.Select(e => e.Id), Does.Not.Contain(secondPage.Single().Id));
    }

    [Test]
    public async Task GetEventsAsync_SkipIsHigherThanCount_ShouldReturnEmptyPageWithTotalCount()
    {
        var eventName = CreateUniqueEventName();
        await Repository.InsertEventAsync(CreateEvent(DateTime.Now.AddHours(1), eventName));

        var (events, totalCount) = await Repository.GetEventsAsync(new EventsFilter
            { EventName = eventName, Skip = 10 });

        Assert.That(events, Is.Empty);
        Assert.That(totalCount, Is.EqualTo(1));
    }

    #endregion

    #region Helper methods

    private static string CreateUniqueEventName() => $"TestEvent_{Guid.NewGuid():N}";

    private static TEvent CreateEvent(DateTime tryAfterAt, string eventName = null, string provider = "TestProvider")
    {
        return new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventName = eventName ?? "TestEvent" + typeof(TEvent).FullName,
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = tryAfterAt
        };
    }

    /// <summary>
    /// Marks the event as processed at the given time. Since UpdatedAt has a non-public setter, we use reflection to set its value.
    /// </summary>
    private static void SetProcessedTimeOfEvent(TEvent eventBox, DateTime processedAt)
    {
        eventBox.Processed();
        eventBox.SetPropertyValue(nameof(BaseMessageBox.UpdatedAt), processedAt);
    }

    #endregion
}