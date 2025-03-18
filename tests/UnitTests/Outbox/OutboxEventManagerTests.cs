using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using EventStorage.Models;
using EventStorage.Outbox;
using EventStorage.Outbox.Managers;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using EventStorage.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventManagerTests
{
    private IOutboxRepository _outboxRepository;
    private IOutboxEventsExecutor _outboxEventsExecutor;
    private OutboxEventManager _outboxEventManager;
    private ILogger<OutboxEventManager> _logger;

    [SetUp]
    public void SetUp()
    {
        _outboxRepository = Substitute.For<IOutboxRepository>();
        _outboxEventsExecutor = Substitute.For<IOutboxEventsExecutor>();
        _logger = Substitute.For<ILogger<OutboxEventManager>>();
        _outboxEventManager = new OutboxEventManager(_logger, _outboxEventsExecutor, _outboxRepository);
    }

    [TearDown]
    public void TearDown()
    {
        _outboxEventManager.Dispose();
    }

    #region Store

    [Test]
    public void Store_StoringEventDoesNotHavePublisher_ShouldNotAddMessageAndReturnFalse()
    {
        var outboxEvent = new SimpleOutboxEventCreated();
        _outboxEventsExecutor.GetEventPublisherTypes(outboxEvent)
            .Returns((string)null);

        var result = _outboxEventManager.Collect(outboxEvent);

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents, Is.Empty);
        Assert.That(result, Is.False);
    }

    [Test]
    public void Store_StoringEventHasTwoPublishers_TwoOutboxMessagesShouldBeStoredAndReturnFalse()
    {
        var outboxEvent = new SimpleOutboxEventCreated();
        var eventProviderTypes = $"{EventProviderType.MessageBroker},{EventProviderType.Sms}";
        _outboxEventsExecutor.GetEventPublisherTypes(outboxEvent).Returns(eventProviderTypes);

        var result = _outboxEventManager.Collect(outboxEvent);

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents.Any(m => m.Provider == eventProviderTypes), Is.True);
        Assert.That(result, Is.True);
    }

    [Test]
    public void Store_AddedOneEvent_ShouldBeCollected()
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
        Assert.That(collectedEvents.Any(m => m.Provider == EventProviderType.MessageBroker.ToString()), Is.True);
        result.Should().BeTrue();
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

    #region Dispose

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
        eventsToSend.Should().HaveCount(2);
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>()).Returns(true);

        _outboxEventManager.Dispose();

        eventsToSend.Should().HaveCount(0);
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
        var eventsToSend = GetCollectedEvents();

        _outboxEventManager.Dispose();

        _outboxRepository.Received(1).BulkInsertEvents(eventsToSend);
    }

    #endregion

    #region Helper methods

    private static readonly FieldInfo EventsToPublishFieldInfo = typeof(OutboxEventManager).GetField(
        "_eventsToSend", BindingFlags.NonPublic | BindingFlags.Instance);

    private ConcurrentBag<OutboxMessage> GetCollectedEvents()
    {
        var eventsToSend = EventsToPublishFieldInfo!.GetValue(_outboxEventManager) as ConcurrentBag<OutboxMessage>;
        return eventsToSend;
    }

    #endregion
}