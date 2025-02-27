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

    #region Send
    
    [Test]
    public void Send_AddedOneEventButDidNotDisposed_ShouldNotBeSend()
    {
        // Arrange
        var senderEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>()).Returns(true);

        // Act
        var result = _outboxEventManager.Store(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );

        // Assert
        result.Should().BeTrue();
        _outboxRepository.Received(0).BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>());
    }
    
    [Test]
    public void Send_AddedOneEventAndExecutedDispose_ShouldBeSent()
    {
        // Arrange
        var senderEvent = new SimpleOutboxEventCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>()).Returns(true);

        // Act
        var result = _outboxEventManager.Store(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );
        _outboxEventManager.Dispose();

        // Assert
        result.Should().BeTrue();
        _outboxRepository.Received(1)
            .BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>());
    }

    [Test]
    public void Send_AddedOneEventWithHeadersAndExecutedDispose_ShouldBeSent()
    {
        // Arrange
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

        // Act
        var result = _outboxEventManager.Store(
            senderEvent,
            EventProviderType.MessageBroker,
            "path"
        );
        _outboxEventManager.Dispose();

        // Assert
        result.Should().BeTrue();
        _outboxRepository.Received(1)
            .BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>());
    }
    
    #endregion
    
    #region Dispose

    [Test]
    public void Dispose_AddedTwoEventsToSend_EventsToSendCollectionShouldBeEmpty()
    {
        // Arrange/Given
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
        var eventsToSendField = typeof(OutboxEventManager).GetField("_eventsToSend",
            BindingFlags.NonPublic | BindingFlags.Instance);
        eventsToSendField.Should().NotBeNull();
        var eventsToSend =
            (ConcurrentBag<OutboxMessage>)eventsToSendField!.GetValue(_outboxEventManager);
        eventsToSend.Should().HaveCount(2);
        _outboxRepository.BulkInsertEvents(Arg.Any<IEnumerable<OutboxMessage>>()).Returns(true);
        
        // Act/When
        _outboxEventManager.Dispose();
        
        // Assert/Then
        eventsToSend.Should().HaveCount(0);
    }

    [Test]
    public void Dispose_AddedTwoEventsToSend_BulkInsertEventsMethodShouldBeExecutedForAddedEvents()
    {
        // Arrange/Given
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
        var eventsToSendField = typeof(OutboxEventManager).GetField("_eventsToSend",
            BindingFlags.NonPublic | BindingFlags.Instance);
        eventsToSendField.Should().NotBeNull();
        var eventsToSend =
            (ConcurrentBag<OutboxMessage>)eventsToSendField!.GetValue(_outboxEventManager);
        
        // Act/When
        _outboxEventManager.Dispose();
        
        // Assert/Then
        _outboxRepository.Received(1).BulkInsertEvents(eventsToSend);
    }

    #endregion
}