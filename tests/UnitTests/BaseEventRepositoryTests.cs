using System.Reflection;
using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Tests.Configs;
using EventStorage.Tests.Infrastructure;

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
            Payload = "TestPayload",
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
            Payload = "TestPayload",
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
            Payload = "TestPayload",
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
            Payload = "TestPayload",
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
            Payload = "TestPayload",
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
            Payload = "TestPayload",
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
            Payload = "TestPayload1",
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
            Payload = "TestPayload2",
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
            Payload = "TestPayload2",
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
            Payload = "TestPayload",
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
        Assert.That(updatedEvent.ProcessedAt, Is.EqualTo(outboxEvent.ProcessedAt).Within(TimeSpan.FromSeconds(1)));
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
            Payload = "TestPayload1",
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
            Payload = "TestPayload2",
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
        Assert.That(updatedEvent1.ProcessedAt, Is.EqualTo(outboxEvent1.ProcessedAt).Within(TimeSpan.FromSeconds(1)));

        Assert.That(updatedEvent2.TryCount, Is.EqualTo(outboxEvent2.TryCount));
        Assert.That(updatedEvent2.TryAfterAt, Is.EqualTo(outboxEvent2.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(updatedEvent2.ProcessedAt, Is.EqualTo(outboxEvent2.ProcessedAt).Within(TimeSpan.FromSeconds(1)));
    }

    #endregion

    #region IsEventProcessedAsync

    [Test]
    public async Task IsEventProcessedAsync_EventExistsAndProcessed_ShouldReturnTrue()
    {
        var processedEvent = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "ProcessedEvent",
            EventPath = "/test/path",
            Payload = "TestPayload",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 1,
            TryAfterAt = DateTime.Now
        };
        await Repository.InsertEventAsync(processedEvent);

        processedEvent.Processed();
        await Repository.UpdateEventAsync(processedEvent);

        var isProcessed = await Repository.IsEventProcessedAsync(processedEvent.Id);

        Assert.That(isProcessed, Is.True);
    }

    [Test]
    public async Task IsEventProcessedAsync_EventExistsButNotProcessed_ShouldReturnFalse()
    {
        var unprocessedEvent = new TEvent
        {
            Id = Guid.NewGuid(),
            Provider = "TestProvider",
            EventName = "UnprocessedEvent",
            EventPath = "/test/path",
            Payload = "TestPayload",
            Headers = "TestHeaders",
            AdditionalData = "TestAdditionalData",
            TryCount = 0,
            TryAfterAt = DateTime.Now
        };

        await Repository.InsertEventAsync(unprocessedEvent);

        var isProcessed = await Repository.IsEventProcessedAsync(unprocessedEvent.Id);

        Assert.That(isProcessed, Is.False);
    }

    [Test]
    public async Task IsEventProcessedAsync_EventDoesNotExist_ShouldReturnTrue()
    {
        var nonExistentId = Guid.NewGuid();

        var isProcessed = await Repository.IsEventProcessedAsync(nonExistentId);

        Assert.That(isProcessed, Is.True);
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
            Payload = "TestPayload1",
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
            Payload = "TestPayload2",
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

    #endregion

    #region Helper methods

    /// <summary>
    /// Sets the processed time of the event. Since ProcessedAt has a private setter, we use reflection to set its value.
    /// </summary>
    private void SetProcessedTimeOfEvent(TEvent eventBox, DateTime processedAt)
    {
        const string nameOfProperty = nameof(BaseMessageBox.ProcessedAt);
        var property = typeof(TEvent).GetProperty(nameOfProperty,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var setter = property?.GetSetMethod(nonPublic: true);
        if (setter == null)
            throw new InvalidOperationException($"Private setter for {nameOfProperty} not found in {typeof(TEvent).Name}");

        setter.Invoke(eventBox, new object[] { processedAt });
    }

    #endregion
}