using EventStorage.Extensions;
using EventStorage.Inbox.Managers;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using EventStorage.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Inbox;

public class InboxEventManagerTests
{
    private IInboxRepository _inboxRepository;
    private ILogger<InboxEventManager> _logger;
    private IHostApplicationLifetime _applicationLifetime;
    private InboxEventManager _manager;

    [SetUp]
    public void Setup()
    {
        _inboxRepository = Substitute.For<IInboxRepository>();
        _logger = Substitute.For<ILogger<InboxEventManager>>();
        _applicationLifetime = Substitute.For<IHostApplicationLifetime>();
        _applicationLifetime.ApplicationStopping.Returns(CancellationToken.None);
        _manager = new InboxEventManager(_logger, _inboxRepository, _applicationLifetime);
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

    [Test]
    public async Task StoreAsync_CancellationTokenIsPassed_ShouldPassItToRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var receiveEvent = CreateEvent();
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        await _manager.StoreAsync(receiveEvent, EventProviderType.Unknown,
            cancellationToken: cancellationTokenSource.Token);

        await _inboxRepository.Received(1).InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == receiveEvent.EventId),
            cancellationTokenSource.Token);
    }

    [Test]
    public void StoreAsync_InsertingIsCancelled_ShouldThrowWithoutLoggingError()
    {
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new OperationCanceledException()));

        Assert.CatchAsync<OperationCanceledException>(() =>
            _manager.StoreAsync(CreateEvent(), EventProviderType.Unknown));

        AssertNoErrorIsLogged();
    }

    [Test]
    public void StoreAsync_InsertingFailsWhileApplicationIsStopping_ShouldThrowWithoutLoggingError()
    {
        _applicationLifetime.ApplicationStopping.Returns(new CancellationToken(canceled: true));
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new ObjectDisposedException(nameof(IServiceProvider))));

        Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _manager.StoreAsync(CreateEvent(), EventProviderType.Unknown));

        AssertNoErrorIsLogged();
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

    [Test]
    public async Task StoreAsync_EventDataAndCancellationTokenArePassed_ShouldPassThemToRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var eventId = Guid.NewGuid();
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _manager.StoreAsync(eventId, nameof(SimpleEntityWasCreated), EventProviderType.MessageBroker,
            payload: "{}", headers: null, eventPath: "routing.key", namingPolicyType: NamingPolicyType.CamelCase,
            cancellationToken: cancellationTokenSource.Token);

        Assert.That(result, Is.True);
        await _inboxRepository.Received(1).InsertEventAsync(Arg.Is<InboxMessage>(x => x.Id == eventId
                && x.EventName == nameof(SimpleEntityWasCreated)
                && x.EventPath == "routing.key"
                && x.Payload == "{}"
                && x.NamingPolicyType == nameof(NamingPolicyType.CamelCase)
                && x.Provider == nameof(EventProviderType.MessageBroker)),
            cancellationTokenSource.Token);
    }

    [Test]
    public void StoreAsync_InsertingFails_ShouldLogErrorAndThrow()
    {
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new EventStoreException("Test failure")));

        Assert.ThrowsAsync<EventStoreException>(() =>
            _manager.StoreAsync(Guid.NewGuid(), nameof(SimpleEntityWasCreated), EventProviderType.Unknown, "{}", null));

        _logger.Received(1).Log(LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(),
            Arg.Is<Exception>(exception => exception is EventStoreException),
            Arg.Any<Func<object, Exception, string>>()!);
    }

    [Test]
    public void StoreAsync_InsertingFailsWhileApplicationIsStopping_ShouldThrowWithoutLoggingErrorForEventData()
    {
        _applicationLifetime.ApplicationStopping.Returns(new CancellationToken(canceled: true));
        _inboxRepository.InsertEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new EventStoreException("Test failure")));

        Assert.ThrowsAsync<EventStoreException>(() =>
            _manager.StoreAsync(Guid.NewGuid(), nameof(SimpleEntityWasCreated), EventProviderType.Unknown, "{}", null));

        AssertNoErrorIsLogged();
    }

    #endregion

    #region Helper methods

    private static SimpleEntityWasCreated CreateEvent()
    {
        return new SimpleEntityWasCreated
        {
            EventId = Guid.NewGuid(),
            Type = "type",
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
    }

    private void AssertNoErrorIsLogged()
    {
        _logger.DidNotReceive().Log(LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>()!);
    }

    #endregion
}