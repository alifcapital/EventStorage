using EventStorage.Extensions;
using EventStorage.Inbox.Managers;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
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
        _manager = new InboxEventManager(logger, _inboxRepository);
    }

    #region ReceivedWithGeneric
    
    [Test]
    public async Task Received_OneEventWithGenericEvent_ShouldAdd()
    {
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _manager.StoreAsync(receiveEvent, EventProviderType.Unknown);

        Assert.That(result, Is.True);

        await _inboxRepository.Received(1)
            .InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.AdditionalData == null
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                ), Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task Received_OneEventWithGenericAndHeaders_ShouldAdd()
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
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _manager.StoreAsync(receiveEvent, EventProviderType.Unknown);

        Assert.That(result, Is.True);

        var headerAsJson = JsonSerializer.Serialize(headers);
        await _inboxRepository.Received(1)
            .InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.Headers == headerAsJson
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                ), Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task Received_OneEventWithGenericAndAdditionalData_ShouldAdd()
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
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _manager.StoreAsync(receiveEvent, EventProviderType.Unknown);

        Assert.That(result, Is.True);

        var additionalDataAsJson = JsonSerializer.Serialize(additionalData);
        await _inboxRepository.Received(1)
            .InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.AdditionalData == additionalDataAsJson
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                ), Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task Received_OneEventWithGenericAndAdditionalDataAndHeaders_ShouldAdd()
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
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _manager.StoreAsync(receiveEvent, EventProviderType.Unknown);

        Assert.That(result, Is.True);

        var headerAsJson = JsonSerializer.Serialize(headers);
        var additionalDataAsJson = JsonSerializer.Serialize(additionalData);
        await _inboxRepository.Received(1)
            .InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.EventPath == receiveEvent.GetType().Namespace
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.Headers == headerAsJson
                                                 && x.AdditionalData == additionalDataAsJson
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                ), Arg.Any<CancellationToken>()
            );
    }
    
    #endregion

    #region ReceivedWithoutGeneric
    
    [Test]
    public async Task Received_WithoutGeneric_ShouldAdd()
    {
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _manager.StoreAsync(receiveEvent, EventProviderType.Unknown);

        Assert.That(result, Is.True);

        await _inboxRepository.Received(1)
            .InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.AdditionalData == null
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                ), Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task Received_WithoutGenericAndWithHeaders_ShouldAdd()
    {
        var receiveEvent = new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _manager.StoreAsync(receiveEvent, EventProviderType.Unknown);

        Assert.That(result, Is.True);

        await _inboxRepository.Received(1)
            .InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId
                                                 && x.EventName == receiveEvent.GetType().Name
                                                 && x.Payload == receiveEvent.SerializeToJson()
                                                 && x.Headers == null
                                                 && x.AdditionalData == null
                                                 && x.Provider == EventProviderType.Unknown.ToString()
                ), Arg.Any<CancellationToken>()
            );
    }
    
    #endregion
}