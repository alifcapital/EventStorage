using System.Collections.Concurrent;
using System.Reflection;
using EventStorage.Models;
using EventStorage.Outbox.Managers;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using EventStorage.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class EventSenderManagerTests
{
    private IOutboxRepository _outboxRepository;
    private EventSenderManager _eventSenderManager;

    [SetUp]
    public void SetUp()
    {
        _outboxRepository = Substitute.For<IOutboxRepository>();
        var logger = Substitute.For<ILogger<EventSenderManager>>();
        _eventSenderManager = new EventSenderManager(logger, _outboxRepository);
    }
    
    [TearDown]
    public void TearDown()
    {
        _eventSenderManager.Dispose();
    }

    #region Send
    
    [Test]
    public void Send_AddedOneEventButDidNotDisposed_ShouldNotBeSend()
    {
        // Arrange
        var senderEvent = new SimpleSendEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxEvent>>()).Returns(true);

        // Act
        var result = _eventSenderManager.Send(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );

        // Assert
        result.Should().BeTrue();
        _outboxRepository.Received(0).BulkInsertEvents(Arg.Any<IEnumerable<OutboxEvent>>());
    }
    
    [Test]
    public void Send_AddedOneEventAndExecutedDispose_ShouldBeSent()
    {
        // Arrange
        var senderEvent = new SimpleSendEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxEvent>>()).Returns(true);

        // Act
        var result = _eventSenderManager.Send(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );
        _eventSenderManager.Dispose();

        // Assert
        result.Should().BeTrue();
        _outboxRepository.Received(1)
            .BulkInsertEvents(Arg.Any<IEnumerable<OutboxEvent>>());
    }

    [Test]
    public void Send_AddedOneEventWithHeadersAndExecutedDispose_ShouldBeSent()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        var senderEvent = new SimpleSendEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now,
            Headers = headers
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxEvent>>()).Returns(true);

        // Act
        var result = _eventSenderManager.Send(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );
        _eventSenderManager.Dispose();

        // Assert
        result.Should().BeTrue();
        _outboxRepository.Received(1)
            .BulkInsertEvents(Arg.Any<IEnumerable<OutboxEvent>>());
    }
    
    #endregion
    
    #region Dispose

    [Test]
    public void Dispose_AddedTwoEventsToSend_EventsToSendCollectionShouldBeEmpty()
    {
        // Arrange/Given
        var senderEvent1 = new SimpleSendEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var senderEvent2 = new SimpleSendEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _eventSenderManager.Send(
            senderEvent1,
            EventProviderType.MessageBroker,
            "path"
        );
        _eventSenderManager.Send(
            senderEvent2,
            EventProviderType.MessageBroker,
            "path"
        );
        var eventsToSendField = typeof(EventSenderManager).GetField("_eventsToSend",
            BindingFlags.NonPublic | BindingFlags.Instance);
        eventsToSendField.Should().NotBeNull();
        var eventsToSend =
            (ConcurrentBag<OutboxEvent>)eventsToSendField!.GetValue(_eventSenderManager);
        eventsToSend.Should().HaveCount(2);
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxEvent>>()).Returns(true);
        
        // Act/When
        _eventSenderManager.Dispose();
        
        // Assert/Then
        eventsToSend.Should().HaveCount(0);
    }

    [Test]
    public void Dispose_AddedTwoEventsToSend_BulkInsertEventsMethodShouldBeExecutedForAddedEvents()
    {
        // Arrange/Given
        var senderEvent1 = new SimpleSendEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var senderEvent2 = new SimpleSendEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _eventSenderManager.Send(
            senderEvent1,
            EventProviderType.MessageBroker,
            "path"
        );
        _eventSenderManager.Send(
            senderEvent2,
            EventProviderType.MessageBroker,
            "path"
        );
        var eventsToSendField = typeof(EventSenderManager).GetField("_eventsToSend",
            BindingFlags.NonPublic | BindingFlags.Instance);
        eventsToSendField.Should().NotBeNull();
        var eventsToSend =
            (ConcurrentBag<OutboxEvent>)eventsToSendField!.GetValue(_eventSenderManager);
        
        // Act/When
        _eventSenderManager.Dispose();
        
        // Assert/Then
        _outboxRepository.Received(1).BulkInsertEvents(eventsToSend);
    }

    #endregion
}