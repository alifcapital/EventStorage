using EventStorage.Extensions;
using EventStorage.Inbox.Managers;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
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
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        var result = _manager.Store(receiveEvent, EventProviderType.Unknown);

        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.AdditionalData == null
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_OneEventWithGenericAndHeaders_ShouldAdd()
    {
        var headers = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now,
            Headers = headers
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        var result = _manager.Store(receiveEvent, EventProviderType.Unknown);

        result.Should().BeTrue();

        var headerAsJson = JsonSerializer.Serialize(headers);
        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.Headers == headerAsJson
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_OneEventWithGenericAndAdditionalData_ShouldAdd()
    {
        var additionalData = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now,
            AdditionalData = additionalData
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        var result = _manager.Store(receiveEvent, EventProviderType.Unknown);

        result.Should().BeTrue();

        var additionalDataAsJson = JsonSerializer.Serialize(additionalData);
        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.AdditionalData == additionalDataAsJson
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_OneEventWithGenericAndAdditionalDataAndHeaders_ShouldAdd()
    {
        var headers = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        var additionalData = new Dictionary<string, string>
        {
            { "key", "value" }
        };
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now,
            Headers = headers,
            AdditionalData = additionalData
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        var result = _manager.Store(receiveEvent, EventProviderType.Unknown);

        result.Should().BeTrue();

        var headerAsJson = JsonSerializer.Serialize(headers);
        var additionalDataAsJson = JsonSerializer.Serialize(additionalData);
        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.Headers == headerAsJson
                                                 && x.AdditionalData == additionalDataAsJson
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }
    
    #endregion

    #region ReceivedWithoutGeneric
    
    [Test]
    public void Received_WithoutGeneric_ShouldAdd()
    {
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        var result = _manager.Store(receiveEvent, EventProviderType.Unknown);

        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.AdditionalData == null
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }

    [Test]
    public void Received_WithoutGenericAndWithHeaders_ShouldAdd()
    {
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEvent(Arg.Any<InboxMessage>()).Returns(true);

        var result = _manager.Store(receiveEvent, EventProviderType.Unknown);

        result.Should().BeTrue();

        _inboxRepository.Received(1)
            .InsertEvent(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.Headers == null
                                                 && x.AdditionalData == null
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                )
            );
    }
    
    #endregion
}