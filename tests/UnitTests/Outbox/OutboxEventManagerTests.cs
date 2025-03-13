using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using EventStorage.Models;
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
    private OutboxEventManager _outboxEventManager;

    [SetUp]
    public void SetUp()
    {
        _outboxRepository = Substitute.For<IOutboxRepository>();
        var logger = Substitute.For<ILogger<OutboxEventManager>>();
        _outboxEventManager = new OutboxEventManager(logger, _outboxRepository);
    }

    [TearDown]
    public void TearDown()
    {
        _outboxEventManager.Dispose();
    }

    #region Store

    [Test]
    public void Store_AddedOneEventButDidNotDisposed_ShouldNotBeSend()
    {
        var senderEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>()).Returns(true);

        var result = _outboxEventManager.Store(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );

        result.Should().BeTrue();
        _outboxRepository.Received(0).BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>());
    }

    [Test]
    public void Store_AddedOneEventAndExecutedDispose_ShouldBeSent()
    {
        var senderEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>()).Returns(true);

        var result = _outboxEventManager.Store(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );
        _outboxEventManager.Dispose();

        result.Should().BeTrue();
        _outboxRepository.Received(1)
            .BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>());
    }

    [Test]
    public void Store_AddedOneEventWithHeadersAndExecutedDispose_ShouldBeSent()
    {
        var headers = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        var senderEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now,
            Headers = headers
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>()).Returns(true);

        var result = _outboxEventManager.Store(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );
        _outboxEventManager.Dispose();

        result.Should().BeTrue();
        _outboxRepository.Received(1)
            .BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>());
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

        _outboxEventManager.Store(eventToStore, EventProviderType.MessageBroker);

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
            AdditionalData = new Dictionary<string, string>()
        };

        _outboxEventManager.Store(eventToStore, EventProviderType.MessageBroker);

        var eventsToSend = GetCollectedEvents();
        Assert.That(eventsToSend, Has.Count.EqualTo(1));

        var storedEvent = eventsToSend.First();
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.EventId)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Type)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Date)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.CreatedAt)));
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.Headers)), "We will not include Headers to the payload even it has value");
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.AdditionalData)), "We will not include AdditionalData to the payload even it has value");
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

        _outboxEventManager.Store(eventToStore, EventProviderType.MessageBroker);

        var eventsToSend = GetCollectedEvents();
        Assert.That(eventsToSend, Has.Count.EqualTo(1));

        var storedEvent = eventsToSend.First();
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.EventId)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Type)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.Date)));
        Assert.That(storedEvent.Payload, Does.Contain(nameof(SimpleOutboxEventCreated.CreatedAt)));
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.Headers)), "We will not include Headers to the payload even it has value");
        Assert.That(storedEvent.Payload, Does.Not.Contain(nameof(SimpleOutboxEventCreated.AdditionalData)), "We will not include AdditionalData to the payload even it has value");
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
        _outboxEventManager.Store(
            senderEvent1,
            EventProviderType.MessageBroker,
            "path"
        );
        _outboxEventManager.Store(
            senderEvent2,
            EventProviderType.MessageBroker,
            "path"
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
        _outboxEventManager.Store(
            senderEvent1,
            EventProviderType.MessageBroker,
            "path"
        );
        _outboxEventManager.Store(
            senderEvent2,
            EventProviderType.MessageBroker,
            "path"
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