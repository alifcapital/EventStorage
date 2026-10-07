using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Inbox;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Management.Models;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using EventStorage.Tests.Domain.Module1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using IServiceScopeFactory = Microsoft.Extensions.DependencyInjection.IServiceScopeFactory;

namespace EventStorage.Tests.UnitTests.Inbox;

internal class InboxEventsProcessorTests
{
    private InboxEventsProcessor _inboxEventsProcessor;
    private IServiceProvider _serviceProvider;
    private IInboxRepository _inboxRepository;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        var logger = Substitute.For<ILogger<InboxEventsProcessor>>();
        serviceProvider.GetService(typeof(ILogger<InboxEventsProcessor>)).Returns(logger);
        serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure()
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutesIfTryCountExceeded = 5, TryAfterMinutesIfEventNotFound = 10 }
        });
        _inboxRepository = Substitute.For<IInboxRepository>();
        serviceProvider.GetService(typeof(IInboxRepository)).Returns(_inboxRepository);
        _serviceProvider = serviceProvider;

        _inboxEventsProcessor = new InboxEventsProcessor(serviceProvider);
    }

    #endregion

    #region AddHandler

    [Test]
    public void AddHandler_OneEventWithSingleHandler_OneHandlerInformationShouldAddToDictionary()
    {
        var typeOfReceiveEvent = typeof(SimpleEntityWasCreated);
        var typeOfEventReceiver = typeof(SimpleEntityWasCreatedHandler);
        var providerType = EventProviderType.Unknown;
        _inboxEventsProcessor.AddHandler(typeOfReceiveEvent, typeOfEventReceiver, providerType);

        var handlers = GetHandlersInformation();

        var handlerKey = InboxEventsProcessor.GetHandlerKey(typeOfReceiveEvent.Name, providerType.ToString());
        Assert.That(handlers.ContainsKey(handlerKey), Is.True);

        var handlersInformation = handlers[handlerKey];
        Assert.That(handlersInformation.Count, Is.EqualTo(1));
        Assert.That(handlersInformation.Any(r =>
                r.EventType == typeOfReceiveEvent && r.EventHandlerType == typeOfEventReceiver),
            Is.True);
    }

    [Test]
    public void AddHandler_OneEventWithTwoHandlers_TwoHandlersInformationShouldAddToDictionary()
    {
        var typeOfReceiveEvent1 = typeof(UserCreated);
        var typeOfReceiveEvent2 = typeof(UserCreated);
        var typeOfEventHandler1 = typeof(Domain.Module1.UserCreatedHandler);
        var typeOfEventHandler2 = typeof(Domain.Module2.UserCreatedHandler);
        var providerType = EventProviderType.MessageBroker;
        _inboxEventsProcessor.AddHandler(typeOfReceiveEvent1, typeOfEventHandler1, providerType);
        _inboxEventsProcessor.AddHandler(typeOfReceiveEvent2, typeOfEventHandler2, providerType);

        var receivers = GetHandlersInformation();

        var receiverKey = InboxEventsProcessor.GetHandlerKey(typeOfReceiveEvent1.Name, providerType.ToString());
        Assert.That(receivers.ContainsKey(receiverKey), Is.True);

        var receiversInformation = receivers[receiverKey];
        Assert.That(receiversInformation.Count, Is.EqualTo(2));
        Assert.That(receiversInformation.Any(r =>
            r.EventType == typeOfReceiveEvent1 && r.EventHandlerType == typeOfEventHandler1), Is.True);
        Assert.That(receiversInformation.Any(r =>
            r.EventType == typeOfReceiveEvent2 && r.EventHandlerType == typeOfEventHandler2), Is.True);
    }

    #endregion

    #region ExecuteUnprocessedEvents

    [Test]
    public async Task ExecuteUnprocessedEvents_ThereIsNoEventsToProcess_ShouldNotProcessedAnyEvents()
    {
        _inboxRepository.LockUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([]);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        await _inboxEventsProcessor.ExecuteUnprocessedEventsAsync(CancellationToken.None);

        await _inboxRepository
            .DidNotReceive()
            .UpdateEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_ThereIsTwoEventsToProcess_BothShouldBeProcessed()
    {
        var inboxEvent1 = new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventName = nameof(SimpleEntityWasCreated),
            EventPath = typeof(SimpleEntityWasCreated).Namespace,
            Provider = "Unknown",
            Payload = "{}",
            Headers = null,
            AdditionalData = null,
            NamingPolicyType = nameof(NamingPolicyType.PascalCase),
            TryCount = 0,
            TryAfterAt = DateTime.UtcNow.AddMinutes(-1)
        };
        var inboxEvent2 = new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventName = nameof(SimpleEntityWasCreated),
            EventPath = typeof(SimpleEntityWasCreated).Namespace,
            Provider = "Unknown",
            Payload = "{}",
            Headers = null,
            AdditionalData = null,
            NamingPolicyType = nameof(NamingPolicyType.PascalCase),
            TryCount = 0,
            TryAfterAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var items = new[] { inboxEvent1, inboxEvent2 };
        _inboxRepository.LockUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(items);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        _inboxEventsProcessor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);

        await _inboxEventsProcessor.ExecuteUnprocessedEventsAsync(CancellationToken.None);

        await _inboxRepository
            .Received(2)
            .UpdateEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>());
        await _inboxRepository.DidNotReceive().UnlockEventsAsync(Arg.Any<IEnumerable<InboxMessage>>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_StoringOneEventFails_ShouldUnlockOnlyThatEvent()
    {
        MockServiceScope();
        _inboxEventsProcessor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);
        var storedEvent = CreateInboxMessage("{}");
        var notStoredEvent = CreateInboxMessage("{}");
        _inboxRepository.LockUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([storedEvent, notStoredEvent]);
        _inboxRepository.UpdateEventAsync(notStoredEvent, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new EventStoreException("Test failure")));

        Assert.ThrowsAsync<EventStoreException>(() =>
            _inboxEventsProcessor.ExecuteUnprocessedEventsAsync(CancellationToken.None));

        await _inboxRepository.Received(1).UnlockEventsAsync(
            Arg.Is<IEnumerable<InboxMessage>>(events => events.Single() == notStoredEvent), Arg.Any<DateTime>(),
            CancellationToken.None);
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_CancelledAfterLockingEvents_ShouldUnlockEventsWithTheSameProcessingTimeout()
    {
        MockServiceScope();
        _inboxEventsProcessor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);
        using var stoppingTokenSource = new CancellationTokenSource();
        var items = new[] { CreateInboxMessage("{}") };
        var processingTimeoutAt = default(DateTime);
        _inboxRepository.LockUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                processingTimeoutAt = callInfo.ArgAt<DateTime>(1);
                stoppingTokenSource.Cancel();
                return items;
            });

        Assert.CatchAsync<OperationCanceledException>(() =>
            _inboxEventsProcessor.ExecuteUnprocessedEventsAsync(stoppingTokenSource.Token));

        Assert.That(processingTimeoutAt, Is.EqualTo(DateTime.Now.AddSeconds(600)).Within(TimeSpan.FromSeconds(5)));
        await _inboxRepository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>());
        await _inboxRepository.Received(1).UnlockEventsAsync(
            Arg.Is<IEnumerable<InboxMessage>>(events => events.SequenceEqual(items)), processingTimeoutAt,
            CancellationToken.None);
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_StoppingTokenIsPassed_ShouldPassItToRepository()
    {
        MockServiceScope();
        using var stoppingTokenSource = new CancellationTokenSource();
        _inboxRepository.LockUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([]);

        await _inboxEventsProcessor.ExecuteUnprocessedEventsAsync(stoppingTokenSource.Token);

        await _inboxRepository.Received(1).LockUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<DateTime>(), stoppingTokenSource.Token);
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_CancellationRequested_ShouldThrowWithoutFetchingEvents()
    {
        MockServiceScope();
        using var stoppingTokenSource = new CancellationTokenSource();
        stoppingTokenSource.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _inboxEventsProcessor.ExecuteUnprocessedEventsAsync(stoppingTokenSource.Token));
        await _inboxRepository.DidNotReceive()
            .LockUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region ProcessSingleEventAsync

    [Test]
    public async Task ProcessSingleEventAsync_EventIsAlreadyProcessed_ShouldSkipWithoutUpdating()
    {
        MockServiceScope();
        _inboxEventsProcessor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);
        var inboxEvent = CreateInboxMessage("{}");
        inboxEvent.Processed();

        var result = await _inboxEventsProcessor.ProcessSingleEventAsync(inboxEvent, manualRequest: null,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.InvalidState));
        await _inboxRepository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessSingleEventAsync_ProcessedEventWithForce_ShouldExecuteAgain()
    {
        MockServiceScope();
        _inboxEventsProcessor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);
        var inboxEvent = CreateInboxMessage("{}");
        inboxEvent.Processed();
        var request = new EventActionRequest { PerformedBy = "operator", Comment = "Re-run", Force = true };

        var result = await _inboxEventsProcessor.ProcessSingleEventAsync(inboxEvent, request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(inboxEvent.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(inboxEvent.UpdatedBy, Is.EqualTo("operator"));
        Assert.That(inboxEvent.StatusComment, Is.EqualTo("Re-run"));
        await _inboxRepository.Received(1).UpdateEventAsync(inboxEvent, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessSingleEventAsync_PayloadCannotBeDeserialized_ShouldStoreFailureReason()
    {
        MockServiceScope();
        _inboxEventsProcessor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);
        var inboxEvent = CreateInboxMessage("not a json");

        var result = await _inboxEventsProcessor.ProcessSingleEventAsync(inboxEvent, manualRequest: null,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.Failed));
        Assert.That(inboxEvent.Status, Is.EqualTo(EventStatus.Failed));
        Assert.That(inboxEvent.TryCount, Is.EqualTo(1));
        Assert.That(inboxEvent.FailureReason, Does.StartWith(
            $"EventStorage.Exceptions.EventStoreException: Could not deserialize the inbox event to the {typeof(SimpleEntityWasCreated).FullName} type."));
        Assert.That(inboxEvent.FailureReason, Does.Contain("System.Text.Json.JsonException"));
        Assert.That(result.FailureReason, Is.EqualTo(inboxEvent.FailureReason));
        await _inboxRepository.Received(1).UpdateEventAsync(inboxEvent, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessSingleEventAsync_ThereIsNoHandler_ShouldStoreFailureReason()
    {
        MockServiceScope();
        var inboxEvent = CreateInboxMessage("{}");

        var result = await _inboxEventsProcessor.ProcessSingleEventAsync(inboxEvent, manualRequest: null,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.Failed));
        Assert.That(inboxEvent.Status, Is.EqualTo(EventStatus.Failed));
        Assert.That(inboxEvent.FailureReason, Is.EqualTo(
            $"No event handler configured for the {nameof(SimpleEntityWasCreated)} event with the Unknown provider."));
        Assert.That(inboxEvent.TryAfterAt, Is.EqualTo(DateTime.Now.AddMinutes(10)).Within(TimeSpan.FromSeconds(5)));
        await _inboxRepository.Received(1).UpdateEventAsync(inboxEvent, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessSingleEventAsync_CancellationTokenIsPassed_ShouldPassItToUpdate()
    {
        MockServiceScope();
        using var cancellationTokenSource = new CancellationTokenSource();
        var inboxEvent = CreateInboxMessage("{}");

        await _inboxEventsProcessor.ProcessSingleEventAsync(inboxEvent, manualRequest: null,
            cancellationTokenSource.Token);

        await _inboxRepository.Received(1).UpdateEventAsync(inboxEvent, cancellationTokenSource.Token);
    }

    #endregion

    #region Helper methods

    private void MockServiceScope()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        _serviceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());
    }

    private static InboxMessage CreateInboxMessage(string payload)
    {
        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventName = nameof(SimpleEntityWasCreated),
            EventPath = typeof(SimpleEntityWasCreated).Namespace,
            Provider = nameof(EventProviderType.Unknown),
            Payload = payload,
            NamingPolicyType = nameof(NamingPolicyType.PascalCase),
            TryAfterAt = DateTime.Now.AddMinutes(-1)
        };
    }

    private Dictionary<string, List<EventHandlerInformation>> GetHandlersInformation()
    {
        const string receiversFieldName = "_receivers";
        var field = _inboxEventsProcessor.GetType().GetField(receiversFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "_receivers field not found via reflection");

        var receivers = (Dictionary<string, List<EventHandlerInformation>>)field!.GetValue(_inboxEventsProcessor);
        return receivers;
    }

    #endregion
}