using EventStorage.Inbox.Managers;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Inbox;

public class InboxEventManagerTests
{
    private IInboxRepository _inboxRepository;
    private InboxEventManager _manager;

    [SetUp]
    public void Setup()
    {
        _inboxRepository = Substitute.For<IInboxRepository>();
        var logger = Substitute.For<ILogger<InboxEventManager>>();
        _manager = new InboxEventManager(_inboxRepository, logger);
    }

    #region ReceivedWithGeneric
    [Test]
    public void Received_OneEventWithGenericEvent_ShouldAdd()
    {
        // Arrange
        var receiveEvent = new SimpleEntityWasCreated()
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        // Act
        var result = _manager.Store<IInboxEvent>(
            receiveEvent,
            "path",
            EventProviderType.Unknown);

        // Assert
        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == JsonConvert.SerializeObject(receiveEvent)
                                                 && x.AdditionalData == null
                                                 && x.EventPath == "path"
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_OneEventWithGenericAndHeaders_ShouldAdd()
    {
        // Arrange
        var receiveEvent = new SimpleEntityWasCreated()
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var headers = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        // Act
        var result = _manager.Store<IInboxEvent>(
            receiveEvent,
            "path",
            EventProviderType.Unknown,
            headers: JsonConvert.SerializeObject(headers));

        // Assert
        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == JsonConvert.SerializeObject(receiveEvent)
                                                 && x.Headers == JsonConvert.SerializeObject(headers)
                                                 && x.EventPath == "path"
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_OneEventWithGenericAndAdditionalData_ShouldAdd()
    {
        // Arrange
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var additionalData = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        // Act
        var result = _manager.Store<IInboxEvent>(
            inboxEvent: receiveEvent,
            eventPath: "path",
            eventProvider: EventProviderType.Unknown,
            headers: null,
            additionalData: JsonConvert.SerializeObject(additionalData)
        );

        // Assert
        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == JsonConvert.SerializeObject(receiveEvent)
                                                 && x.AdditionalData == JsonConvert.SerializeObject(additionalData)
                                                 && x.EventPath == "path"
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_OneEventWithGenericAndAdditionalDataAndHeaders_ShouldAdd()
    {
        // Arrange
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var headers = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        var additionalData = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        // Act
        var result = _manager.Store<IInboxEvent>(
            inboxEvent: receiveEvent,
            eventPath: "path",
            eventProvider: EventProviderType.Unknown,
            headers: JsonConvert.SerializeObject(headers),
            additionalData: JsonConvert.SerializeObject(additionalData)
        );

        // Assert
        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == JsonConvert.SerializeObject(receiveEvent)
                                                 && x.Headers == JsonConvert.SerializeObject(headers)
                                                 && x.AdditionalData == JsonConvert.SerializeObject(additionalData)
                                                 && x.EventPath == "path"
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }
    #endregion

    #region ReceivedWithouGeneric
    [Test]
    public void Received_WithoutGeneric_ShouldAdd()
    {
        // Arrange
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        // Act
        var result = _manager.Store(
            receiveEvent,
            "path",
            EventProviderType.Unknown);

        // Assert
        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == JsonConvert.SerializeObject(receiveEvent)
                                                 && x.AdditionalData == null
                                                 && x.EventPath == "path"
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_WithoutGenericAndWithHeaders_ShouldAdd()
    {
        // Arrange
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        var headers = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        // Act
        var result = _manager.Store(
            receiveEvent,
            "path",
            EventProviderType.Unknown,
            headers: JsonConvert.SerializeObject(headers));

        // Assert
        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == JsonConvert.SerializeObject(receiveEvent)
                                                 && x.Headers == JsonConvert.SerializeObject(headers)
                                                 && x.EventPath == "path"
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }
    #endregion
}