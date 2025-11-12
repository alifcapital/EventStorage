using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Inbox;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using EventStorage.Tests.Domain.Module1;
using Medallion.Threading;
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
        MockDistributedLockProvider(serviceProvider);
        var logger = Substitute.For<ILogger<InboxEventsProcessor>>();
        serviceProvider.GetService(typeof(ILogger<InboxEventsProcessor>)).Returns(logger);
        serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure()
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
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
        _inboxRepository.GetUnprocessedEventsAsync(Arg.Any<int>()).Returns([]);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        await _inboxEventsProcessor.ExecuteUnprocessedEvents(CancellationToken.None);

        await _inboxRepository
            .DidNotReceive()
            .UpdateEventAsync(Arg.Any<InboxMessage>());
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
        _inboxRepository.GetUnprocessedEventsAsync(Arg.Any<int>()).Returns(items);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        _inboxEventsProcessor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);

        await _inboxEventsProcessor.ExecuteUnprocessedEvents(CancellationToken.None);

        await _inboxRepository
            .Received(2)
            .UpdateEventAsync(Arg.Any<InboxMessage>());
    }

    #endregion

    #region Helper methods

    private Dictionary<string, List<EventHandlerInformation>> GetHandlersInformation()
    {
        const string receiversFieldName = "_receivers";
        var field = _inboxEventsProcessor.GetType().GetField(receiversFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "_receivers field not found via reflection");

        var receivers = (Dictionary<string, List<EventHandlerInformation>>)field!.GetValue(_inboxEventsProcessor);
        return receivers;
    }

    void MockDistributedLockProvider(IKeyedServiceProvider serviceProvider)
    {
        var distributedLockProvider = Substitute.For<IDistributedLockProvider>();
        serviceProvider.GetRequiredKeyedService(typeof(IDistributedLockProvider), FunctionalityNames.Inbox)
            .Returns(distributedLockProvider);
        
        var distributedLock = Substitute.For<IDistributedLock>();
        distributedLockProvider.CreateLock(Arg.Any<string>()).Returns(distributedLock);

        var distributedSynchronizationHandle = Substitute.For<IDistributedSynchronizationHandle>();
        distributedLock.TryAcquireAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(distributedSynchronizationHandle);
    }

    #endregion
}