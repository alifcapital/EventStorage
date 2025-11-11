using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using EventStorage.Models;
using EventStorage.Outbox;
using EventStorage.Outbox.Managers;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using EventStorage.Tests.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventManagerTests
{
    private IOutboxRepository _outboxRepository;
    private IOutboxEventsProcessor _outboxEventsProcessor;
    private OutboxEventManager _outboxEventManager;
    private ILogger<OutboxEventManager> _logger;

    [SetUp]
    public void SetUp()
    {
        _outboxRepository = Substitute.For<IOutboxRepository>();
        _outboxEventsProcessor = Substitute.For<IOutboxEventsProcessor>();
        _logger = Substitute.For<ILogger<OutboxEventManager>>();
        _outboxEventManager = new OutboxEventManager(_logger, _outboxEventsProcessor, _outboxRepository);
    }

    [TearDown]
    public void TearDown()
    {
        _outboxEventManager.Dispose();
    }

    #region Collect

    [Test]
    public void Collect_StoringEventDoesNotHavePublisher_ShouldNotAddMessageAndReturnFalse()
    {
        var outboxEvent = new SimpleOutboxEventCreated();
        _outboxEventsProcessor.GetEventPublisherTypes(outboxEvent)
            .Returns((string)null);

        var result = _outboxEventManager.Collect(outboxEvent);

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents, Is.Empty);
        Assert.That(result, Is.False);
    }

    [Test]
    public void Collect_StoringEventHasTwoPublishers_TwoOutboxMessagesShouldBeStoredAndReturnFalse()
    {
        var outboxEvent = new SimpleOutboxEventCreated();
        var eventProviderTypes = $"{EventProviderType.MessageBroker},{EventProviderType.Sms}";
        _outboxEventsProcessor.GetEventPublisherTypes(outboxEvent).Returns(eventProviderTypes);

        var result = _outboxEventManager.Collect(outboxEvent);

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents.Any(m => m.Provider == eventProviderTypes), Is.True);
        Assert.That(result, Is.True);
    }

    [Test]
    public void Collect_AddedOneEvent_ShouldBeCollected()
    {
        var senderEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };

        var result = _outboxEventManager.Collect(
            senderEvent,
            EventProviderType.MessageBroker
        );

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents.Any(m => m.Provider == nameof(EventProviderType.MessageBroker)), Is.True);
        Assert.That(result, Is.True);
    }

    [Test]
    public void Collect_AddedOneEventTwice_OnlyFirstOneShouldBeAdded()
    {
        var senderEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };

        _outboxEventManager.Collect(senderEvent, EventProviderType.MessageBroker);
        var result = _outboxEventManager.Collect(senderEvent, EventProviderType.MessageBroker);

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents, Has.Count.EqualTo(1));
        Assert.That(result, Is.False);
    }

    #endregion

    #region StoreAsync

    [Test]
    public async Task StoreAsync_AddedOneEventWithProvider_EventShouldNotBeCollected()
    {
        var outboxEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.InsertEventAsync(Arg.Any<OutboxMessage>()).Returns(true);

        await _outboxEventManager.StoreAsync(
            outboxEvent,
            EventProviderType.MessageBroker
        );

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents, Is.Empty);
    }

    [Test]
    public async Task StoreAsync_AddedOneEventWithProvider_ShouldBeStoredBasedOnPassingData()
    {
        var outboxEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.InsertEventAsync(Arg.Any<OutboxMessage>()).Returns(true);

        var result = await _outboxEventManager.StoreAsync(
            outboxEvent,
            EventProviderType.MessageBroker
        );

        Assert.That(result, Is.True);
        await _outboxRepository.Received(1).InsertEventAsync(Arg.Is<OutboxMessage>(e =>
            e.Provider == EventProviderType.MessageBroker.ToString()
            && e.Id == outboxEvent.EventId));
    }

    [Test]
    public async Task StoreAsync_StoringEventWithoutEventProvider_EventShouldBeStoredBasedOnCachedData()
    {
        var outboxEvent = new SimpleOutboxEventCreated();
        var eventProviderType = EventProviderType.Sms.ToString();
        _outboxEventsProcessor.GetEventPublisherTypes(outboxEvent).Returns(eventProviderType);
        _outboxRepository.InsertEventAsync(Arg.Any<OutboxMessage>()).Returns(true);

        var result = await _outboxEventManager.StoreAsync(outboxEvent);

        Assert.That(result, Is.True);
        await _outboxRepository.Received(1).InsertEventAsync(Arg.Is<OutboxMessage>(e =>
            e.Provider == eventProviderType
            && e.Id == outboxEvent.EventId));
    }

    [Test]
    public async Task StoreAsync_StoringEventDoesNotHaveCachedPublisher_MessageShouldNotBeAddedAndReturnFalse()
    {
        var outboxEvent = new SimpleOutboxEventCreated();
        _outboxEventsProcessor.GetEventPublisherTypes(outboxEvent)
            .Returns((string)null);

        var result = await _outboxEventManager.StoreAsync(outboxEvent);

        Assert.That(result, Is.False);
        await _outboxRepository.Received(0).InsertEventAsync(Arg.Any<OutboxMessage>());
    }

    [Test]
    public async Task StoreAsync_StoringMultipleEvents_ShouldBeStoredBasedOnPassingData()
    {
        var outboxEvents = new[]
        {
            new SimpleOutboxEventCreated
            {
                EventId = Guid.NewGuid(),
                Type = "type",
                Date = DateTime.Now,
                CreatedAt = DateTime.Now
            },
            new SimpleOutboxEventCreated
            {
                EventId = Guid.NewGuid(),
                Type = "type",
                Date = DateTime.Now,
                CreatedAt = DateTime.Now
            }
        };
        var eventProviderType = EventProviderType.MessageBroker.ToString();
        _outboxEventsProcessor.GetEventPublisherTypes(Arg.Any<IOutboxEvent>()).Returns(eventProviderType);
        _outboxRepository.BulkInsertEventsAsync(Arg.Any<OutboxMessage[]>()).Returns(true);

        var result = await _outboxEventManager.StoreAsync(outboxEvents);

        Assert.That(result, Is.True);
        await _outboxRepository.Received(1).BulkInsertEventsAsync(Arg.Is<OutboxMessage[]>(events =>
            outboxEvents.All(e => events.Any(m => m.Id == e.EventId))));
    }

    #endregion

    #region Payload of the event

    [Test]
    public void EventPayload_StoringEventWhichDoesNotHaveAdditionalProperties_AllPropertiesShouldBeStoredAsPayload()
    {
        var eventToStore = new SimpleOutboxEventWithoutAdditionalProperties
        {
            EventId = Guid.NewGuid(),
            Type = Guid.NewGuid().ToString(),
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };

        _outboxEventManager.Collect(eventToStore, EventProviderType.MessageBroker);

        var eventsToSend = GetCollectedEvents();
        Assert.That(eventsToSend, Has.Count.EqualTo(1));

        var storedEvent = eventsToSend.First();
        var expectedPayload = JsonSerializer.Serialize(eventToStore);
        Assert.That(storedEvent.Payload, Is.EqualTo(expectedPayload));
    }

    [Test]
    public void
        EventPayload_EventHasAdditionalPropertiesButThoseAreEmpty_AllPropertiesShouldBeStoredToPayloadButNotAdditionalProperties()
    {
        var eventToStore = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = Guid.NewGuid().ToString(),
            Date = DateTime.Now,
            CreatedAt = DateTime.Now,
            Headers = new Dictionary<string, string>(),
            AdditionalData = null
        };

        _outboxEventManager.Collect(eventToStore, EventProviderType.MessageBroker);

        var eventsToSend = GetCollectedEvents();
        Assert.That(eventsToSend, Has.Count.EqualTo(1));

        var storedEvent = eventsToSend.First();
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.EventId)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Type)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Date)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.CreatedAt)));
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.Headers)),
            "We will not include Headers to the payload even it has value");
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.AdditionalData)),
            "We will not include AdditionalData to the payload even it has value");
        Assert.That(storedEvent.Headers, Is.Null);
        Assert.That(storedEvent.AdditionalData, Is.Null);
    }

    [Test]
    public void
        EventPayload_EventHasAdditionalPropertiesIncludingValues_AllPropertiesShouldBeStoredToPayloadAndAdditionalProperties()
    {
        var eventToStore = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = Guid.NewGuid().ToString(),
            Date = DateTime.Now,
            CreatedAt = DateTime.Now,
            Headers = new Dictionary<string, string> { { "key", "value" } },
            AdditionalData = new Dictionary<string, string> { { "key", "value" } },
        };

        _outboxEventManager.Collect(eventToStore, EventProviderType.MessageBroker);

        var eventsToSend = GetCollectedEvents();
        Assert.That(eventsToSend, Has.Count.EqualTo(1));

        var storedEvent = eventsToSend.First();
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.EventId)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Type)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Date)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.CreatedAt)));
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.Headers)),
            "We will not include Headers to the payload even it has value");
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.AdditionalData)),
            "We will not include AdditionalData to the payload even it has value");
        Assert.That(storedEvent.Headers, Is.Not.Null);
        Assert.That(storedEvent.AdditionalData, Is.Not.Null);
    }

    #endregion

    #region Collect and Dispose

    [Test]
    public void Dispose_AddedTwoEventsToSend_EventsToSendCollectionShouldBeEmpty()
    {
        var senderEvent1 = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var senderEvent2 = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxEventManager.Collect(
            senderEvent1,
            EventProviderType.MessageBroker
        );
        _outboxEventManager.Collect(
            senderEvent2,
            EventProviderType.MessageBroker
        );
        var eventsToSend = GetCollectedEvents();
        Assert.That(eventsToSend, Has.Count.EqualTo(2));
        _outboxRepository.BulkInsertEvents(Arg.Any<OutboxMessage[]>()).Returns(true);

        _outboxEventManager.Dispose();

        eventsToSend = GetCollectedEvents();
        Assert.That(eventsToSend, Has.Count.EqualTo(0));
    }

    [Test]
    public void Dispose_AddedTwoEventsToSend_BulkInsertEventsMethodShouldBeExecutedForAddedEvents()
    {
        var senderEvent1 = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var senderEvent2 = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxEventManager.Collect(
            senderEvent1,
            EventProviderType.MessageBroker
        );
        _outboxEventManager.Collect(
            senderEvent2,
            EventProviderType.MessageBroker
        );

        _outboxEventManager.Dispose();

        _outboxRepository.Received(1).BulkInsertEvents(Arg.Any<OutboxMessage[]>());
    }

    #endregion

    #region Helper methods

    private static readonly FieldInfo EventsToPublishFieldInfo = typeof(OutboxEventManager).GetField(
        "_eventsToSend", BindingFlags.NonPublic | BindingFlags.Instance);

    private ICollection<OutboxMessage> GetCollectedEvents()
    {
        var eventsToSend =
            EventsToPublishFieldInfo!.GetValue(_outboxEventManager) as ConcurrentDictionary<Guid, OutboxMessage>;
        return eventsToSend!.Values;
    }

    #endregion
}