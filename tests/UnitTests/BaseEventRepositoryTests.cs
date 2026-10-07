using EventStorage.Configurations;
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

    [Test]
    public void CreateTableIfNotExists_ShouldCreateIndexesForProcessingAndGettingEvents()
    {
        var tableName = Repository.TableName;

        var indexes = DataContext.GetIndexDefinitions();

        Assert.That(indexes[$"idx_{tableName}_status_try_after_at"], Does.EndWith("(status, try_after_at)"));
        Assert.That(indexes[$"idx_{tableName}_status_updated_at"], Does.EndWith("(status, updated_at)"));
        Assert.That(indexes[$"idx_{tableName}_created_at_id"], Does.EndWith("(created_at, id)"));
        Assert.That(indexes[$"idx_{tableName}_event_name_created_at_id"], Does.EndWith("(event_name, created_at, id)"));
        Assert.That(indexes[$"idx_{tableName}_status_created_at_id"], Does.EndWith("(status, created_at, id)"));
        Assert.That(indexes.Keys, Has.No.Member($"idx_{tableName}_created_at"));
        Assert.That(indexes.Keys, Has.No.Member($"idx_{tableName}_event_name_created_at"));
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

        var result = await Repository.InsertEventAsync(eventBox, CancellationToken.None);

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

        var result = await Repository.BulkInsertEventsAsync([firstEvent, secondEvent], CancellationToken.None);

        Assert.That(result, Is.True);
        var firstEventFromDb = DataContext.GetById(firstEvent.Id);
        Assert.That(firstEventFromDb.Id, Is.EqualTo(firstEvent.Id));
        Assert.That(firstEventFromDb.EventName, Is.EqualTo(firstEvent.EventName));

        var secondEventFromDb = DataContext.GetById(secondEvent.Id);
        Assert.That(secondEventFromDb.Id, Is.EqualTo(secondEvent.Id));
        Assert.That(secondEventFromDb.EventName, Is.EqualTo(secondEvent.EventName));
    }

    #endregion

    #region LockUnprocessedEventsAsync

    [Test]
    public async Task LockUnprocessedEventsAsync_TwoItems_ShouldReturnPendingEventsWithOriginalValues()
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

        await Repository.BulkInsertEventsAsync([baseEventBox1, baseEventBox2, baseEventBox3], CancellationToken.None);

        var result = await Repository.LockUnprocessedEventsAsync(5, GetProcessingTimeoutAt(), CancellationToken.None);

        Assert.That(result.Length, Is.EqualTo(2));

        var firstEvent = result.Single(e => e.Id == baseEventBox1.Id);
        Assert.That(firstEvent, Is.Not.Null);
        Assert.That(firstEvent,
            IsClass.EquivalentTo(baseEventBox1, nameof(baseEventBox1.CreatedAt), nameof(baseEventBox1.TryAfterAt)));
        Assert.That(firstEvent.TryAfterAt, Is.EqualTo(baseEventBox1.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task LockUnprocessedEventsAsync_PendingEvent_ShouldStoreItAsProcessingUntilProcessingTimeout()
    {
        var eventBox = CreateEvent(DateTime.Now.AddMinutes(-1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        var processingTimeoutAt = GetProcessingTimeoutAt();

        try
        {
            await Repository.LockUnprocessedEventsAsync(500, processingTimeoutAt, CancellationToken.None);

            var storedEvent = DataContext.GetById(eventBox.Id);
            Assert.That(storedEvent.Status, Is.EqualTo(EventStatus.Processing));
            Assert.That(storedEvent.TryAfterAt, Is.EqualTo(processingTimeoutAt));
            Assert.That(storedEvent.UpdatedAt, Is.Null, "The lock must not change the history of the event.");
        }
        finally
        {
            await MarkAsProcessedAsync(eventBox);
        }
    }

    [Test]
    public async Task LockUnprocessedEventsAsync_EventIsAlreadyLocked_ShouldSkipIt()
    {
        var eventBox = CreateEvent(DateTime.Now.AddMinutes(-1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);

        try
        {
            var firstResult = await Repository.LockUnprocessedEventsAsync(500, GetProcessingTimeoutAt(),
                CancellationToken.None);
            var secondResult = await Repository.LockUnprocessedEventsAsync(500, GetProcessingTimeoutAt(),
                CancellationToken.None);

            Assert.That(firstResult.Select(e => e.Id), Does.Contain(eventBox.Id));
            Assert.That(secondResult.Select(e => e.Id), Does.Not.Contain(eventBox.Id));
        }
        finally
        {
            await MarkAsProcessedAsync(eventBox);
        }
    }

    [Test]
    public async Task LockUnprocessedEventsAsync_ProcessingTimeoutHasPassed_ShouldLockItAgainAsFailedEvent()
    {
        var eventBox = CreateEvent(DateTime.Now.AddMinutes(-1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);

        try
        {
            // The instance which locked the event stopped before storing its result, and its timeout has passed.
            var passedProcessingTimeoutAt = TruncateToSeconds(DateTime.Now.AddMinutes(-1));
            await Repository.LockUnprocessedEventsAsync(500, passedProcessingTimeoutAt, CancellationToken.None);

            var result = await Repository.LockUnprocessedEventsAsync(500, GetProcessingTimeoutAt(),
                CancellationToken.None);

            var lockedEvent = result.Single(e => e.Id == eventBox.Id);
            Assert.That(lockedEvent.Status, Is.EqualTo(EventStatus.Failed));
            Assert.That(lockedEvent.TryAfterAt, Is.EqualTo(passedProcessingTimeoutAt));
        }
        finally
        {
            await MarkAsProcessedAsync(eventBox);
        }
    }

    [Test]
    public async Task LockUnprocessedEventsAsync_LockedAtTheSameTime_ShouldNotLockTheSameEventTwice()
    {
        var events = Enumerable.Range(0, 20).Select(_ => CreateEvent(DateTime.Now.AddMinutes(-1))).ToArray();
        await Repository.BulkInsertEventsAsync(events, CancellationToken.None);

        try
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(_ => Repository.LockUnprocessedEventsAsync(500, GetProcessingTimeoutAt(),
                    CancellationToken.None)));

            var lockedIds = results.SelectMany(r => r.Select(e => e.Id)).ToArray();
            Assert.That(lockedIds, Is.Unique);
            Assert.That(lockedIds, Is.SupersetOf(events.Select(e => e.Id)));
        }
        finally
        {
            await MarkAsProcessedAsync(events);
        }
    }

    [Test]
    public async Task LockUnprocessedEventsAsync_EventIsUpdatedAfterLocking_ShouldUnlockIt()
    {
        var eventBox = CreateEvent(DateTime.Now.AddMinutes(-1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);

        try
        {
            var lockedEvent = (await Repository.LockUnprocessedEventsAsync(500, GetProcessingTimeoutAt(),
                    CancellationToken.None))
                .Single(e => e.Id == eventBox.Id);
            // The column is rounded to seconds, so we keep the failed event in the past to be fetched.
            lockedEvent.Failed(maxTryCount: 10, tryAfterSeconds: 0, tryAfterMinutesIfTryCountExceeded: 5,
                failureReason: "Test failure");
            lockedEvent.TryAfterAt = DateTime.Now.AddMinutes(-1);
            await Repository.UpdateEventAsync(lockedEvent, CancellationToken.None);

            var result = await Repository.LockUnprocessedEventsAsync(500, GetProcessingTimeoutAt(),
                CancellationToken.None);

            Assert.That(result.Select(e => e.Id), Does.Contain(eventBox.Id));
        }
        finally
        {
            await MarkAsProcessedAsync(eventBox);
        }
    }

    [Test]
    public async Task
        LockUnprocessedEventsAsync_ProcessedFailedAndRejectedEvents_ShouldReturnOnlyPendingAndFailedEvents()
    {
        var pendingEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        var failedEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        var processedEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        var rejectedEvent = CreateEvent(DateTime.Now.AddMinutes(-1));
        await Repository.BulkInsertEventsAsync([pendingEvent, failedEvent, processedEvent, rejectedEvent], CancellationToken.None);

        failedEvent.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "Test failure");
        // The column is rounded to seconds, so we keep the failed event in the past to be fetched.
        failedEvent.TryAfterAt = DateTime.Now.AddMinutes(-1);
        processedEvent.Processed();
        rejectedEvent.Rejected();
        await Repository.UpdateEventsAsync([failedEvent, processedEvent, rejectedEvent], CancellationToken.None);

        try
        {
            var result = await Repository.LockUnprocessedEventsAsync(500, GetProcessingTimeoutAt(),
                CancellationToken.None);
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
            await MarkAsProcessedAsync(pendingEvent, failedEvent);
        }
    }

    #endregion

    #region LockEventByIdAsync

    [Test]
    public async Task LockEventByIdAsync_EventIsNotLocked_ShouldReturnEventWithOriginalValues()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        var processingTimeoutAt = GetProcessingTimeoutAt();

        var result = await Repository.LockEventByIdAsync(eventBox.Id, processingTimeoutAt, CancellationToken.None);

        Assert.That(result, IsClass.EquivalentTo(eventBox, nameof(eventBox.CreatedAt), nameof(eventBox.TryAfterAt)));
        Assert.That(result.TryAfterAt, Is.EqualTo(eventBox.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(DataContext.GetStoredStatusById(eventBox.Id), Is.EqualTo(nameof(EventStatus.Processing)));
        Assert.That(DataContext.GetById(eventBox.Id).TryAfterAt, Is.EqualTo(processingTimeoutAt));
    }

    [Test]
    public async Task LockEventByIdAsync_RejectedEvent_ShouldLockItWithItsOriginalStatus()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        eventBox.Rejected();
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);

        var result = await Repository.LockEventByIdAsync(eventBox.Id, GetProcessingTimeoutAt(),
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventStatus.Rejected));
    }

    [Test]
    public async Task LockEventByIdAsync_EventIsAlreadyLocked_ShouldReturnNull()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        await Repository.LockEventByIdAsync(eventBox.Id, GetProcessingTimeoutAt(), CancellationToken.None);

        var result = await Repository.LockEventByIdAsync(eventBox.Id, GetProcessingTimeoutAt(),
            CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task LockEventByIdAsync_ProcessingTimeoutHasPassed_ShouldLockItAgainAsFailedEvent()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        await Repository.LockEventByIdAsync(eventBox.Id, TruncateToSeconds(DateTime.Now.AddMinutes(-1)),
            CancellationToken.None);

        var result = await Repository.LockEventByIdAsync(eventBox.Id, GetProcessingTimeoutAt(),
            CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(EventStatus.Failed));
    }

    [Test]
    public async Task LockEventByIdAsync_EventDoesNotExist_ShouldReturnNull()
    {
        var result = await Repository.LockEventByIdAsync(Guid.NewGuid(), GetProcessingTimeoutAt(),
            CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region UnlockEventsAsync

    [Test]
    public async Task UnlockEventsAsync_EventIsLocked_ShouldRestoreItsOriginalStatusAndTryTime()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        var processingTimeoutAt = GetProcessingTimeoutAt();
        var lockedEvent = await Repository.LockEventByIdAsync(eventBox.Id, processingTimeoutAt, CancellationToken.None);

        await Repository.UnlockEventsAsync([lockedEvent], processingTimeoutAt, CancellationToken.None);

        var storedEvent = DataContext.GetById(eventBox.Id);
        Assert.That(storedEvent.Status, Is.EqualTo(EventStatus.Pending));
        Assert.That(storedEvent.TryAfterAt, Is.EqualTo(eventBox.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        var result = await Repository.LockEventByIdAsync(eventBox.Id, GetProcessingTimeoutAt(),
            CancellationToken.None);
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task UnlockEventsAsync_EventIsStored_ShouldNotChangeIt()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        var processingTimeoutAt = GetProcessingTimeoutAt();
        var lockedEvent = await Repository.LockEventByIdAsync(eventBox.Id, processingTimeoutAt, CancellationToken.None);
        var originalEvent = await Repository.GetEventByIdAsync(eventBox.Id, CancellationToken.None);
        lockedEvent.Rejected();
        await Repository.UpdateEventAsync(lockedEvent, CancellationToken.None);

        await Repository.UnlockEventsAsync([originalEvent], processingTimeoutAt, CancellationToken.None);

        Assert.That(DataContext.GetStoredStatusById(eventBox.Id), Is.EqualTo(nameof(EventStatus.Rejected)));
    }

    [Test]
    public async Task UnlockEventsAsync_EventIsLockedAgainByOthers_ShouldNotRemoveTheirLock()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        var firstProcessingTimeoutAt = TruncateToSeconds(DateTime.Now.AddMinutes(-1));
        var firstLockedEvent = await Repository.LockEventByIdAsync(eventBox.Id, firstProcessingTimeoutAt,
            CancellationToken.None);
        var secondProcessingTimeoutAt = GetProcessingTimeoutAt();
        var secondLockedEvent = await Repository.LockEventByIdAsync(eventBox.Id, secondProcessingTimeoutAt,
            CancellationToken.None);

        await Repository.UnlockEventsAsync([firstLockedEvent], firstProcessingTimeoutAt, CancellationToken.None);

        Assert.That(secondLockedEvent, Is.Not.Null);
        var storedEvent = DataContext.GetById(eventBox.Id);
        Assert.That(storedEvent.Status, Is.EqualTo(EventStatus.Processing));
        Assert.That(storedEvent.TryAfterAt, Is.EqualTo(secondProcessingTimeoutAt));
    }

    [Test]
    public void UnlockEventsAsync_EventsAreNotLocked_ShouldNotThrow()
    {
        Assert.DoesNotThrowAsync(() =>
            Repository.UnlockEventsAsync([CreateEvent(DateTime.Now)], GetProcessingTimeoutAt(),
                CancellationToken.None));
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

        await Repository.InsertEventAsync(outboxEvent, CancellationToken.None);

        // Modify the event
        outboxEvent.TryCount = 1;
        outboxEvent.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent.Processed();

        var result = await Repository.UpdateEventAsync(outboxEvent, CancellationToken.None);

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
        await Repository.InsertEventAsync(rejectedEvent, CancellationToken.None);
        Assert.That(DataContext.GetStoredStatusById(rejectedEvent.Id), Is.EqualTo(nameof(EventStatus.Pending)));

        rejectedEvent.Rejected();
        await Repository.UpdateEventAsync(rejectedEvent, CancellationToken.None);

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

        await Repository.BulkInsertEventsAsync([outboxEvent1, outboxEvent2], CancellationToken.None);

        // Modify the events
        outboxEvent1.TryCount = 1;
        outboxEvent1.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent1.Processed();

        outboxEvent2.TryCount = 1;
        outboxEvent2.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent2.Processed();

        var result = await Repository.UpdateEventsAsync(new List<TEvent> { outboxEvent1, outboxEvent2 }, CancellationToken.None);

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
        await Repository.InsertEventAsync(processedEvent, CancellationToken.None);

        processedEvent.Processed();
        await Repository.UpdateEventAsync(processedEvent, CancellationToken.None);

        var status = await Repository.GetEventStatusByIdAsync(processedEvent.Id, CancellationToken.None);

        Assert.That(status, Is.EqualTo(EventStatus.Processed));
    }

    [Test]
    public async Task GetEventStatusAsync_EventExistsButNotProcessed_ShouldReturnPending()
    {
        var unprocessedEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(unprocessedEvent, CancellationToken.None);

        var status = await Repository.GetEventStatusByIdAsync(unprocessedEvent.Id, CancellationToken.None);

        Assert.That(status, Is.EqualTo(EventStatus.Pending));
    }

    [Test]
    public async Task GetEventStatusAsync_EventDoesNotExist_ShouldReturnNull()
    {
        var status = await Repository.GetEventStatusByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(status, Is.Null);
    }

    [Test]
    public async Task GetEventStatusAsync_EventIsRejected_ShouldReturnRejected()
    {
        var rejectedEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(rejectedEvent, CancellationToken.None);

        rejectedEvent.Rejected();
        await Repository.UpdateEventAsync(rejectedEvent, CancellationToken.None);

        var status = await Repository.GetEventStatusByIdAsync(rejectedEvent.Id, CancellationToken.None);

        Assert.That(status, Is.EqualTo(EventStatus.Rejected));
    }

    #endregion

    #region GetEventByIdAsync

    [Test]
    public async Task GetEventByIdAsync_EventIsFailed_ShouldReturnEventWithFailureReasonAndManualChangeInfo()
    {
        var failedEvent = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(failedEvent, CancellationToken.None);

        failedEvent.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "System.Exception: Test failure",
            performedBy: "operator", comment: "Manual execution");
        await Repository.UpdateEventAsync(failedEvent, CancellationToken.None);

        var result = await Repository.GetEventByIdAsync(failedEvent.Id, CancellationToken.None);

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
        var result = await Repository.GetEventByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetEventByIdAsync_EventExists_ShouldReturnStoredCreationTimeInsteadOfLoadingTime()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        var result = await Repository.GetEventByIdAsync(eventBox.Id, CancellationToken.None);

        // The column keeps only seconds, so the stored value is rounded.
        Assert.That(result.CreatedAt, Is.EqualTo(eventBox.CreatedAt).Within(TimeSpan.FromSeconds(1)));
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

        await Repository.BulkInsertEventsAsync([event1, event2], CancellationToken.None);

        SetProcessedTimeOfEvent(event1, DateTime.Now.AddMinutes(-20));
        SetProcessedTimeOfEvent(event2, DateTime.Now.AddMinutes(-5));
        await Repository.UpdateEventsAsync([event1, event2], CancellationToken.None);

        var result = await Repository.DeleteProcessedEventsAsync(processedAt, CancellationToken.None);
        Assert.That(result, Is.True);
        Assert.That(DataContext.ExistsById(event1.Id), Is.False);
        Assert.That(DataContext.ExistsById(event2.Id), Is.True);
    }

    [Test]
    public async Task DeleteProcessedEventsAsync_RejectedEvent_ShouldNotBeDeleted()
    {
        var rejectedEvent = CreateEvent(DateTime.Now);
        await Repository.InsertEventAsync(rejectedEvent, CancellationToken.None);

        rejectedEvent.Rejected();
        rejectedEvent.SetPropertyValue(nameof(BaseMessageBox.UpdatedAt), DateTime.Now.AddDays(-1));
        await Repository.UpdateEventAsync(rejectedEvent, CancellationToken.None);

        await Repository.DeleteProcessedEventsAsync(DateTime.Now, CancellationToken.None);
        Assert.That(DataContext.ExistsById(rejectedEvent.Id), Is.True);
    }

    #endregion

    #region GetEventsAsync

    // Each test uses a unique event name, since the table is shared by the tests of the fixture.

    [Test]
    public async Task GetEventsAsync_FilterByStatus_ShouldReturnOnlyEventsWithThatStatus()
    {
        var eventName = CreateUniqueEventName();
        var (_, failedEvent, _, _) = await InsertEventsWithAllStatuses(eventName);

        var result = await Repository.GetEventsAsync(
            new EventsFilter { EventName = eventName, Status = EventStatus.Failed }, CancellationToken.None);

        Assert.That(result.Select(e => e.Id), Is.EquivalentTo(new[] { failedEvent.Id }));
    }

    [Test]
    public async Task GetEventsAsync_StatusIsNotProvided_ShouldReturnEventsOfAllStatuses()
    {
        var eventName = CreateUniqueEventName();
        var (pendingEvent, failedEvent, rejectedEvent, processedEvent) = await InsertEventsWithAllStatuses(eventName);

        var result = await Repository.GetEventsAsync(new EventsFilter { EventName = eventName },
            CancellationToken.None);

        Assert.That(result.Select(e => e.Id),
            Is.EquivalentTo(new[] { pendingEvent.Id, failedEvent.Id, rejectedEvent.Id, processedEvent.Id }));
    }

    [Test]
    public async Task GetEventsAsync_FilterByProvider_ShouldMatchOneOfMultipleProvidersEntirely()
    {
        var eventName = CreateUniqueEventName();
        var smsEvent = CreateEventWithName(eventName, provider: "Sms");
        var multipleProvidersEvent = CreateEventWithName(eventName, provider: "MessageBroker,Sms");
        var httpEvent = CreateEventWithName(eventName, provider: "Http");
        await Repository.BulkInsertEventsAsync([smsEvent, multipleProvidersEvent, httpEvent], CancellationToken.None);

        var result = await Repository.GetEventsAsync(
            new EventsFilter { EventName = eventName, EventProviderType = nameof(EventProviderType.Sms) }, CancellationToken.None);

        Assert.That(result.Select(e => e.Id), Is.EquivalentTo(new[] { smsEvent.Id, multipleProvidersEvent.Id }));
    }

    [Test]
    public async Task GetEventsAsync_FilterByCreatedTime_ShouldReturnOnlyEventsInRange()
    {
        var eventName = CreateUniqueEventName();
        var eventBox = CreateEventWithName(eventName);
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);

        var inRange = await Repository.GetEventsAsync(new EventsFilter
        {
            EventName = eventName, CreatedFrom = DateTime.Now.AddMinutes(-1), CreatedTo = DateTime.Now.AddMinutes(1)
        }, CancellationToken.None);
        var createdLater = await Repository.GetEventsAsync(
            new EventsFilter { EventName = eventName, CreatedFrom = DateTime.Now.AddMinutes(1) }, CancellationToken.None);
        var createdEarlier = await Repository.GetEventsAsync(
            new EventsFilter { EventName = eventName, CreatedTo = DateTime.Now.AddMinutes(-1) }, CancellationToken.None);

        Assert.That(inRange.Select(e => e.Id), Is.EqualTo(new[] { eventBox.Id }));
        Assert.That(createdLater, Is.Empty);
        Assert.That(createdEarlier, Is.Empty);
    }

    [Test]
    public async Task GetEventsAsync_FilterByUpdatedTimeAndUser_ShouldReturnOnlyEventsChangedByUserInRange()
    {
        var eventName = CreateUniqueEventName();
        var rejectedByOperator = CreateEventWithName(eventName);
        var rejectedByOtherUser = CreateEventWithName(eventName);
        var pendingEvent = CreateEventWithName(eventName);
        await Repository.BulkInsertEventsAsync([rejectedByOperator, rejectedByOtherUser, pendingEvent],
            CancellationToken.None);
        rejectedByOperator.Rejected(performedBy: "operator");
        rejectedByOtherUser.Rejected(performedBy: "other");
        await Repository.UpdateEventsAsync([rejectedByOperator, rejectedByOtherUser], CancellationToken.None);

        var result = await Repository.GetEventsAsync(new EventsFilter
        {
            EventName = eventName, UpdatedBy = "operator", UpdatedFrom = DateTime.Now.AddMinutes(-1),
            UpdatedTo = DateTime.Now.AddMinutes(1)
        }, CancellationToken.None);

        Assert.That(result.Select(e => e.Id), Is.EqualTo(new[] { rejectedByOperator.Id }));
    }

    [TestCase("John")]
    [TestCase("doe")]
    [TestCase("JOHN DOE")]
    public async Task GetEventsAsync_UpdatedByIsPartOfFullName_ShouldReturnEventsChangedByThatUser(string updatedBy)
    {
        var eventName = CreateUniqueEventName();
        var rejectedByJohn = CreateEventWithName(eventName);
        var rejectedByOtherUser = CreateEventWithName(eventName);
        await Repository.BulkInsertEventsAsync([rejectedByJohn, rejectedByOtherUser], CancellationToken.None);
        rejectedByJohn.Rejected(performedBy: "John Doe");
        rejectedByOtherUser.Rejected(performedBy: "Jane Smith");
        await Repository.UpdateEventsAsync([rejectedByJohn, rejectedByOtherUser], CancellationToken.None);

        var result = await Repository.GetEventsAsync(new EventsFilter { EventName = eventName, UpdatedBy = updatedBy },
            CancellationToken.None);

        Assert.That(result.Select(e => e.Id), Is.EqualTo(new[] { rejectedByJohn.Id }));
    }

    [TestCase("%")]
    [TestCase("_")]
    public async Task GetEventsAsync_UpdatedByHasLikeWildcards_ShouldMatchAsPlainText(string updatedBy)
    {
        var eventName = CreateUniqueEventName();
        var rejectedEvent = CreateEventWithName(eventName);
        await Repository.InsertEventAsync(rejectedEvent, CancellationToken.None);
        rejectedEvent.Rejected(performedBy: "John Doe");
        await Repository.UpdateEventAsync(rejectedEvent, CancellationToken.None);

        var result = await Repository.GetEventsAsync(new EventsFilter { EventName = eventName, UpdatedBy = updatedBy },
            CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetEventsAsync_FilterByMinTryCountAndFailureReason_ShouldReturnOnlyMatchingFailedEvents()
    {
        var eventName = CreateUniqueEventName();
        var failedTwice = CreateEventWithName(eventName);
        var failedOnce = CreateEventWithName(eventName);
        var failedTwiceWithOtherReason = CreateEventWithName(eventName);
        await Repository.BulkInsertEventsAsync([failedTwice, failedOnce, failedTwiceWithOtherReason],
            CancellationToken.None);
        failedTwice.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "Timeout");
        failedTwice.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "System.TimeoutException: 100%_done");
        failedOnce.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "System.TimeoutException: 100%_done");
        failedTwiceWithOtherReason.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "Other");
        failedTwiceWithOtherReason.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "Other");
        await Repository.UpdateEventsAsync([failedTwice, failedOnce, failedTwiceWithOtherReason],
            CancellationToken.None);

        var result = await Repository.GetEventsAsync(new EventsFilter
        {
            EventName = eventName, MinTryCount = 2, FailureReasonContains = "timeoutexception: 100%_"
        }, CancellationToken.None);

        Assert.That(result.Select(e => e.Id), Is.EqualTo(new[] { failedTwice.Id }));
    }

    [TestCase("a1b2c3")]
    [TestCase("\"UserId\": \"A1B2C3\"")]
    public async Task GetEventsAsync_FilterByPayload_ShouldReturnOnlyEventsWhosePayloadContainsText(
        string payloadContains)
    {
        var eventName = CreateUniqueEventName();
        var matchingEvent = CreateEventWithName(eventName, payload: "{\"UserId\":\"A1B2C3\",\"Name\":\"Test\"}");
        var otherEvent = CreateEventWithName(eventName, payload: "{\"UserId\":\"D4E5F6\",\"Name\":\"Test\"}");
        await Repository.BulkInsertEventsAsync([matchingEvent, otherEvent], CancellationToken.None);

        var result = await Repository.GetEventsAsync(
            new EventsFilter { EventName = eventName, PayloadContains = payloadContains }, CancellationToken.None);

        Assert.That(result.Select(e => e.Id), Is.EqualTo(new[] { matchingEvent.Id }));
    }

    [TestCase("%")]
    [TestCase("_")]
    public async Task GetEventsAsync_PayloadContainsHasLikeWildcards_ShouldMatchAsPlainText(string payloadContains)
    {
        var eventName = CreateUniqueEventName();
        await Repository.InsertEventAsync(CreateEventWithName(eventName, payload: "{\"Name\":\"Test\"}"),
            CancellationToken.None);

        var result = await Repository.GetEventsAsync(
            new EventsFilter { EventName = eventName, PayloadContains = payloadContains }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetEventsAsync_EventExists_ShouldLoadAllColumnsOfEventSummary()
    {
        var eventName = CreateUniqueEventName();
        var failedEvent = CreateEventWithName(eventName);
        await Repository.InsertEventAsync(failedEvent, CancellationToken.None);
        failedEvent.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "Test failure", performedBy: "operator",
            comment: "Test comment");
        await Repository.UpdateEventAsync(failedEvent, CancellationToken.None);

        var result = (await Repository.GetEventsAsync(new EventsFilter { EventName = eventName }, CancellationToken.None)).Single();

        Assert.That(result.Id, Is.EqualTo(failedEvent.Id));
        Assert.That(result.Provider, Is.EqualTo(failedEvent.Provider));
        Assert.That(result.EventName, Is.EqualTo(eventName));
        Assert.That(result.EventPath, Is.EqualTo(failedEvent.EventPath));
        Assert.That(result.Status, Is.EqualTo(EventStatus.Failed));
        Assert.That(result.TryCount, Is.EqualTo(1));
        Assert.That(result.FailureReason, Is.EqualTo("Test failure"));
        Assert.That(result.UpdatedBy, Is.EqualTo("operator"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetEventsAsync_PagingByPageIndex_ShouldReturnAllEventsOnceInOrder(bool sortDescending)
    {
        var eventName = CreateUniqueEventName();
        var events = Enumerable.Range(0, 5).Select(_ => CreateEventWithName(eventName)).ToArray();
        await Repository.BulkInsertEventsAsync(events, CancellationToken.None);
        var filter = new EventsFilter { EventName = eventName, SortDescending = sortDescending };

        var allEvents = await Repository.GetEventsAsync(filter, CancellationToken.None);
        var pages = new List<EventPagedList<EventSummary>>();
        for (var pageIndex = 1; pageIndex <= 3; pageIndex++)
            pages.Add(await Repository.GetEventsAsync(filter with { PageIndex = pageIndex, PageSize = 2 },
                CancellationToken.None));

        // Postgres compares the uuid values byte by byte, which is the same as the ordinal order of their strings.
        var expectedOrder = sortDescending
            ? allEvents.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id.ToString(), StringComparer.Ordinal)
            : allEvents.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id.ToString(), StringComparer.Ordinal);
        Assert.That(allEvents.Select(e => e.Id), Is.EquivalentTo(events.Select(e => e.Id)));
        Assert.That(allEvents.Select(e => e.Id), Is.EqualTo(expectedOrder.Select(e => e.Id)));
        Assert.That(pages.SelectMany(p => p).Select(e => e.Id), Is.EqualTo(allEvents.Select(e => e.Id)));
        Assert.That(pages.Select(p => p.Count), Is.EqualTo(new[] { 2, 2, 1 }));
        Assert.That(pages.Select(p => p.HasNextPage), Is.EqualTo(new[] { true, true, false }));
        Assert.That(pages.Select(p => p.PageIndex), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task GetEventsAsync_EventsFitPageExactly_ShouldNotHaveNextPage()
    {
        var eventName = CreateUniqueEventName();
        var events = Enumerable.Range(0, 2).Select(_ => CreateEventWithName(eventName)).ToArray();
        await Repository.BulkInsertEventsAsync(events, CancellationToken.None);

        var result = await Repository.GetEventsAsync(new EventsFilter { EventName = eventName, PageSize = 2 },
            CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.HasNextPage, Is.False);
        Assert.That(result.PageSize, Is.EqualTo(2));
    }

    [Test]
    public async Task GetEventsAsync_PageIsAfterLastEvent_ShouldReturnEmptyPage()
    {
        var eventName = CreateUniqueEventName();
        await Repository.InsertEventAsync(CreateEventWithName(eventName), CancellationToken.None);

        var result = await Repository.GetEventsAsync(
            new EventsFilter { EventName = eventName, PageIndex = 2, PageSize = 1 }, CancellationToken.None);

        Assert.That(result, Is.Empty);
        Assert.That(result.HasNextPage, Is.False);
    }

    #endregion

    #region Cancellation

    protected static IEnumerable<TestCaseData> AsyncMethodsWithCancellation()
    {
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.InsertEventAsync),
            (repository, token) => repository.InsertEventAsync(CreateEvent(DateTime.Now), token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.BulkInsertEventsAsync),
            (repository, token) => repository.BulkInsertEventsAsync([CreateEvent(DateTime.Now)], token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.LockUnprocessedEventsAsync),
            (repository, token) => repository.LockUnprocessedEventsAsync(5, DateTime.Now, token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.UpdateEventAsync),
            (repository, token) => repository.UpdateEventAsync(CreateEvent(DateTime.Now), token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.UpdateEventsAsync),
            (repository, token) => repository.UpdateEventsAsync([CreateEvent(DateTime.Now)], token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.GetEventStatusByIdAsync),
            (repository, token) => repository.GetEventStatusByIdAsync(Guid.NewGuid(), token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.GetEventByIdAsync),
            (repository, token) => repository.GetEventByIdAsync(Guid.NewGuid(), token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.LockEventByIdAsync),
            (repository, token) => repository.LockEventByIdAsync(Guid.NewGuid(), DateTime.Now, token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.DeleteProcessedEventsAsync),
            (repository, token) => repository.DeleteProcessedEventsAsync(DateTime.Now, token));
        yield return CreateAsyncMethodCase(nameof(BaseEventRepository<TEvent>.GetEventsAsync),
            (repository, token) => repository.GetEventsAsync(new EventsFilter(), token));
    }

    [TestCaseSource(nameof(AsyncMethodsWithCancellation))]
    public void AsyncMethod_CancellationRequested_ShouldThrowOperationCanceledInsteadOfEventStoreException(
        Func<BaseEventRepository<TEvent>, CancellationToken, Task> executeMethod)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => executeMethod(Repository, cancellationTokenSource.Token));
    }

    [Test]
    public void InsertEventAsync_CancellationRequested_EventShouldNotBeInserted()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            Repository.InsertEventAsync(eventBox, cancellationTokenSource.Token));
        Assert.That(DataContext.ExistsById(eventBox.Id), Is.False);
    }

    [Test]
    public async Task UpdateEventAsync_CancellationRequested_EventShouldNotBeUpdated()
    {
        var eventBox = CreateEvent(DateTime.Now.AddHours(1));
        await Repository.InsertEventAsync(eventBox, CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        eventBox.Rejected();
        Assert.CatchAsync<OperationCanceledException>(() =>
            Repository.UpdateEventAsync(eventBox, cancellationTokenSource.Token));
        Assert.That(DataContext.GetStoredStatusById(eventBox.Id), Is.EqualTo(nameof(EventStatus.Pending)));
    }

    #endregion

    #region Helper methods

    private static TestCaseData CreateAsyncMethodCase(string methodName,
        Func<BaseEventRepository<TEvent>, CancellationToken, Task> executeMethod)
    {
        return new TestCaseData(executeMethod).SetArgDisplayNames(methodName);
    }

    private static string CreateUniqueEventName() => $"Filter_{Guid.NewGuid():N}";

    /// <summary>
    /// Creates an event with a future try time, so it does not affect the tests of getting unprocessed events.
    /// </summary>
    /// <summary>
    /// Inserts one event per status with the given name.
    /// </summary>
    private async Task<(TEvent Pending, TEvent Failed, TEvent Rejected, TEvent Processed)> InsertEventsWithAllStatuses(
        string eventName)
    {
        var pendingEvent = CreateEventWithName(eventName);
        var failedEvent = CreateEventWithName(eventName);
        var rejectedEvent = CreateEventWithName(eventName);
        var processedEvent = CreateEventWithName(eventName);
        await Repository.BulkInsertEventsAsync([pendingEvent, failedEvent, rejectedEvent, processedEvent],
            CancellationToken.None);
        failedEvent.Failed(maxTryCount: 10, tryAfterSeconds: 3600, tryAfterMinutesIfTryCountExceeded: 5, failureReason: "Test failure");
        rejectedEvent.Rejected();
        processedEvent.Processed();
        await Repository.UpdateEventsAsync([failedEvent, rejectedEvent, processedEvent], CancellationToken.None);

        return (pendingEvent, failedEvent, rejectedEvent, processedEvent);
    }

    private static TEvent CreateEventWithName(string eventName, string provider = "TestProvider",
        string payload = "{}")
    {
        return new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventName = eventName,
            EventPath = "/test/path",
            Payload = payload,
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = DateTime.Now.AddHours(1)
        };
    }

    private static TEvent CreateEvent(DateTime tryAfterAt)
    {
        return new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "TestEvent" + typeof(TEvent).FullName,
            EventPath = "/test/path",
            Payload = "{}",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = tryAfterAt
        };
    }

    /// <summary>
    /// Marks the events as processed, so the table which is shared by the tests of the fixture does not keep
    /// unprocessed events for other tests.
    /// </summary>
    private async Task MarkAsProcessedAsync(params TEvent[] events)
    {
        foreach (var eventBox in events)
            eventBox.Processed();

        await Repository.UpdateEventsAsync(events, CancellationToken.None);
    }

    /// <summary>
    /// Gets the processing timeout the same way as the processors do, with the default settings.
    /// </summary>
    private static DateTime GetProcessingTimeoutAt() => new InboxOrOutboxStructure().GetProcessingTimeoutAt();

    /// <summary>
    /// Truncates the time to whole seconds, since the try_after_at column stores no fractions of a second.
    /// </summary>
    private static DateTime TruncateToSeconds(DateTime time) => time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));

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