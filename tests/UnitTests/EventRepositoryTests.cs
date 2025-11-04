using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Tests.Configs;
using EventStorage.Tests.Infrastructure;

namespace EventStorage.Tests.UnitTests;

internal abstract class EventRepositoryTests<TEvent> : BaseTestEntity where TEvent : BaseMessageBox, new()
{
    private readonly EventRepository<TEvent> _repository;
    private readonly DataContext<TEvent> _dataContext;

    internal EventRepositoryTests(
        EventRepository<TEvent> eventRepository,
        DataContext<TEvent> dataContext
    )
    {
        _repository = eventRepository;
        _dataContext = dataContext;
    }

    #region CreateTableIfNotExists
        
    [Test]
    public void CreateTableIfNotExists_ShouldCreateTable()
    {
        Assert.That(_dataContext.ExistTable(), Is.True);
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

        var result = _repository.InsertEvent(eventBox);

        Assert.That(result, Is.True);
        var eventFromDb = _dataContext.GetById(eventBox.Id);

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

        var result = await _repository.InsertEventAsync(eventBox);

        Assert.That(result, Is.True);
        var eventFromDb = _dataContext.GetById(eventBox.Id);

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

        var result = _repository.BulkInsertEvents([firstEvent, secondEvent]);

        Assert.That(result, Is.True);
        var firstEventFromDb = _dataContext.GetById(firstEvent.Id);
        Assert.That(firstEventFromDb.Id, Is.EqualTo(firstEvent.Id));
        Assert.That(firstEventFromDb.EventName, Is.EqualTo(firstEvent.EventName));
            
        var secondEventFromDb = _dataContext.GetById(secondEvent.Id);
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

        var result = await _repository.BulkInsertEventsAsync([firstEvent, secondEvent]);

        Assert.That(result, Is.True);
        var firstEventFromDb = _dataContext.GetById(firstEvent.Id);
        Assert.That(firstEventFromDb.Id, Is.EqualTo(firstEvent.Id));
        Assert.That(firstEventFromDb.EventName, Is.EqualTo(firstEvent.EventName));
            
        var secondEventFromDb = _dataContext.GetById(secondEvent.Id);
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
            TryAfterAt = DateTime.Now.AddMinutes(-1),
            ProcessedAt = null
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
            TryAfterAt = DateTime.Now.AddMinutes(-1),
            ProcessedAt = null
        };
            
        _repository.InsertEvent(baseEventBox1);
        _repository.InsertEvent(baseEventBox2);
            
        var result = await _repository.GetUnprocessedEventsAsync(2);
            
        Assert.That(result.Any(e => e.Id == baseEventBox2.Id), Is.True);
            
        var firstEvent = result.FirstOrDefault(e => e.Id == baseEventBox1.Id);
        Assert.That(firstEvent, Is.Not.Null);
        Assert.That(firstEvent, IsClass.EquivalentTo(baseEventBox1, nameof(baseEventBox1.CreatedAt), nameof(baseEventBox1.TryAfterAt)));
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
            TryCount = 0,
            ProcessedAt = null
        };

        _repository.InsertEvent(outboxEvent);

        // Modify the event
        outboxEvent.TryCount = 1;
        outboxEvent.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent.ProcessedAt = DateTime.Now;
            
        var result = await _repository.UpdateEventAsync(outboxEvent);
            
        Assert.That(result, Is.True);

        var updatedEvent = _dataContext.GetById(outboxEvent.Id);

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
            TryCount = 0,
            ProcessedAt = null
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
            TryCount = 0,
            ProcessedAt = null
        };

        _repository.InsertEvent(outboxEvent1);
        _repository.InsertEvent(outboxEvent2);

        // Modify the events
        outboxEvent1.TryCount = 1;
        outboxEvent1.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent1.ProcessedAt = DateTime.Now;

        outboxEvent2.TryCount = 1;
        outboxEvent2.TryAfterAt = DateTime.Now.AddMinutes(10);
        outboxEvent2.ProcessedAt = DateTime.Now;
            
        var result = await _repository.UpdateEventsAsync(new List<TEvent> { outboxEvent1, outboxEvent2 });
            
        Assert.That(result, Is.True);

        var updatedEvent1 = _dataContext.GetById(outboxEvent1.Id);
        var updatedEvent2 = _dataContext.GetById(outboxEvent2.Id);
            
        Assert.That(updatedEvent1.TryCount, Is.EqualTo(outboxEvent1.TryCount));
        Assert.That(updatedEvent1.TryAfterAt, Is.EqualTo(outboxEvent1.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(updatedEvent1.ProcessedAt, Is.EqualTo(outboxEvent1.ProcessedAt).Within(TimeSpan.FromSeconds(1)));
            
        Assert.That(updatedEvent2.TryCount, Is.EqualTo(outboxEvent2.TryCount));
        Assert.That(updatedEvent2.TryAfterAt, Is.EqualTo(outboxEvent2.TryAfterAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(updatedEvent2.ProcessedAt, Is.EqualTo(outboxEvent2.ProcessedAt).Within(TimeSpan.FromSeconds(1)));
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
            TryCount = 0,
            ProcessedAt = DateTime.Now.AddMinutes(-20)
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
            TryCount = 0,
            ProcessedAt = DateTime.Now.AddMinutes(-5)
        };

        _repository.InsertEvent(event1);
        _repository.InsertEvent(event2);

        await _repository.UpdateEventsAsync([event1, event2]);
            
        var result = await _repository.DeleteProcessedEventsAsync(processedAt);
            
        Assert.That(result, Is.True);
        Assert.That(_dataContext.ExistsById(event1.Id), Is.False);
        Assert.That(_dataContext.ExistsById(event2.Id), Is.True);
    }
        
    #endregion
}