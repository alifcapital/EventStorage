using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Tests.Infrastructure;
using FluentAssertions;

namespace EventStorage.Tests.UnitTests
{
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
            Assert.IsTrue(_dataContext.ExistTable());
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

            result.Should().BeTrue();
            var eventFromDb = _dataContext.GetById(eventBox.Id);

            eventFromDb.Id.Should().Be(eventBox.Id);
            eventFromDb.EventName.Should().Be(eventBox.EventName);
            eventFromDb.TryCount.Should().Be(eventBox.TryCount);
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

            result.Should().BeTrue();
            var eventFromDb = _dataContext.GetById(eventBox.Id);

            eventFromDb.Id.Should().Be(eventBox.Id);
            eventFromDb.EventName.Should().Be(eventBox.EventName);
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

            result.Should().BeTrue();
            var firstEventFromDb = _dataContext.GetById(firstEvent.Id);
            firstEventFromDb.Id.Should().Be(firstEvent.Id);
            firstEventFromDb.EventName.Should().Be(firstEvent.EventName);
            
            var secondEventFromDb = _dataContext.GetById(secondEvent.Id);
            secondEventFromDb.Id.Should().Be(secondEvent.Id);
            secondEventFromDb.EventName.Should().Be(secondEvent.EventName);
        }

        #endregion

        #region GetUnprocessedEventsAsync

        [Test]
        public async Task GetUnprocessedEventsAsync_TwoItems_ShouldReturnPendingEvents()
        {
            // Arrange
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

            // Act
            var result = await _repository.GetUnprocessedEventsAsync(2);

            // Assert
            result.Should().Contain(e => e.Id == baseEventBox2.Id);
            
            var firstEvent = result.FirstOrDefault(e => e.Id == baseEventBox1.Id);
            firstEvent.Should().NotBeNull();
            firstEvent.Should().BeEquivalentTo(baseEventBox1, e => 
                e.Excluding(e => e.CreatedAt)
                .Excluding(e => e.TryAfterAt));
            firstEvent.TryAfterAt.Should().BeCloseTo(baseEventBox1.TryAfterAt, TimeSpan.FromSeconds(1));
        }

        #endregion

        #region UpdateEventAsync
        
        [Test]
        public async Task UpdateEventAsync_OneItem_ShouldUpdateEvent()
        {
            // Arrange
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

            // Act
            var result = await _repository.UpdateEventAsync(outboxEvent);

            // Assert
            result.Should().BeTrue();

            var updatedEvent = _dataContext.GetById(outboxEvent.Id);

            updatedEvent.TryCount.Should().Be(outboxEvent.TryCount);
            updatedEvent.TryAfterAt.Should().BeCloseTo(outboxEvent.TryAfterAt, TimeSpan.FromSeconds(1));
            updatedEvent.ProcessedAt.Should().BeCloseTo(outboxEvent.ProcessedAt.Value, TimeSpan.FromSeconds(1));
        }
        
        #endregion

        #region UpdateEventsAsync
        
        [Test]
        public async Task UpdateEventsAsync_TwoItems_ShouldUpdateEvents()
        {
            // Arrange
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

            // Act
            var result = await _repository.UpdateEventsAsync(new List<TEvent> { outboxEvent1, outboxEvent2 });

            // Assert
            result.Should().BeTrue();

            var updatedEvent1 = _dataContext.GetById(outboxEvent1.Id);
            var updatedEvent2 = _dataContext.GetById(outboxEvent2.Id);
            
            updatedEvent1.TryCount.Should().Be(outboxEvent1.TryCount);
            updatedEvent1.TryAfterAt.Should().BeCloseTo(outboxEvent1.TryAfterAt, TimeSpan.FromSeconds(1));
            updatedEvent1.ProcessedAt.Should().BeCloseTo(outboxEvent1.ProcessedAt.Value, TimeSpan.FromSeconds(1));
            
            updatedEvent2.TryCount.Should().Be(outboxEvent2.TryCount);
            updatedEvent2.TryAfterAt.Should().BeCloseTo(outboxEvent2.TryAfterAt, TimeSpan.FromSeconds(1));
            updatedEvent2.ProcessedAt.Should().BeCloseTo(outboxEvent2.ProcessedAt.Value, TimeSpan.FromSeconds(1));
        }
        
        #endregion

        #region DeleteProcessedEventsAsync
        
        [Test]
        public async Task DeleteProcessedEventsAsync_OneItems_ShouldDeleteProcessedEvents()
        {
            // Arrange
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
           
            // Act
            var result = await _repository.DeleteProcessedEventsAsync(processedAt);

            // Assert
            result.Should().BeTrue();
            _dataContext.ExistsById(event1.Id).Should().BeFalse();
            _dataContext.ExistsById(event2.Id).Should().BeTrue();
        }
        
        #endregion
    }
}