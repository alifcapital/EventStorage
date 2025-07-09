using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Inbox;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using EventStorage.Tests.Domain.Module1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using IServiceScopeFactory = Microsoft.Extensions.DependencyInjection.IServiceScopeFactory;

namespace EventStorage.Tests.UnitTests.Inbox;

internal class InboxEventsExecutorTests
{
    private InboxEventsExecutor _inboxEventsExecutor;
    private IServiceProvider _serviceProvider;
    private IInboxRepository _inboxRepository;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        var logger = NullLogger<InboxEventsExecutor>.Instance;
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(ILogger<InboxEventsExecutor>)).Returns(logger);

        _inboxRepository = Substitute.For<IInboxRepository>();
        _serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure()
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        });
        _serviceProvider.GetService(typeof(IInboxRepository)).Returns(_inboxRepository);

        _inboxEventsExecutor = new InboxEventsExecutor(_serviceProvider);
    }

    #endregion

    [Test]
    public void AddHandler_OneEventWithSingleHandler_OneHandlerInformationShouldAddToDictionary()
    {
        var typeOfReceiveEvent = typeof(SimpleEntityWasCreated);
        var typeOfEventReceiver = typeof(SimpleEntityWasCreatedHandler);
        var providerType = EventProviderType.Unknown;
        _inboxEventsExecutor.AddHandler(typeOfReceiveEvent, typeOfEventReceiver, providerType);

        var handlers = GetHandlersInformation();

        var handlerKey = InboxEventsExecutor.GetHandlerKey(typeOfReceiveEvent.Name, providerType.ToString());
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
        _inboxEventsExecutor.AddHandler(typeOfReceiveEvent1, typeOfEventHandler1, providerType);
        _inboxEventsExecutor.AddHandler(typeOfReceiveEvent2, typeOfEventHandler2, providerType);

        var receivers = GetHandlersInformation();

        var receiverKey = InboxEventsExecutor.GetHandlerKey(typeOfReceiveEvent1.Name, providerType.ToString());
        Assert.That(receivers.ContainsKey(receiverKey), Is.True);

        var receiversInformation = receivers[receiverKey];
        Assert.That(receiversInformation.Count, Is.EqualTo(2));
        Assert.That(receiversInformation.Any(r =>
            r.EventType == typeOfReceiveEvent1 && r.EventHandlerType == typeOfEventHandler1), Is.True);
        Assert.That(receiversInformation.Any(r =>
            r.EventType == typeOfReceiveEvent2 && r.EventHandlerType == typeOfEventHandler2), Is.True);
    }

    #region ExecuteUnprocessedEvents

    [Test]
    public async Task ExecuteUnprocessedEvents_EventTryAfterIsBeforeNow_ShouldProcessed()
    {
        var inboxEvent = new InboxMessage
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
            TryAfterAt = DateTime.UtcNow.AddMinutes(-1),
            ProcessedAt = null
        };

        var items = new[] { inboxEvent };
        _inboxRepository.GetUnprocessedEventsAsync().Returns(items);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        _inboxEventsExecutor.AddHandler(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);

        await _inboxEventsExecutor.ExecuteUnprocessedEvents(CancellationToken.None);

        await _inboxRepository
            .Received(1)
            .UpdateEventsAsync(Arg.Is<IEnumerable<InboxMessage>>(x =>
                x.Count() == 1 &&
                x.First().TryCount == 0 &&
                x.First().ProcessedAt != null)
            );
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_EventTryAfterIsAfterNow_ShouldNotProcessed()
    {
        var inboxEvent = new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventName = "SimpleEntityWasCreated",
            Provider = "Unknown",
            Payload = "{}",
            TryCount = 0,
            TryAfterAt = DateTime.UtcNow.AddMinutes(1),
            ProcessedAt = null
        };

        var items = new[] { inboxEvent };
        _inboxRepository.GetUnprocessedEventsAsync().Returns(items);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        await _inboxEventsExecutor.ExecuteUnprocessedEvents(CancellationToken.None);

        await _inboxRepository
            .Received(1)
            .UpdateEventsAsync(Arg.Is<IEnumerable<InboxMessage>>(x =>
                x.Count() == 1 &&
                x.First().TryCount == 1 &&
                x.First().ProcessedAt == null)
            );
    }

    #endregion

    #region Helper methods

    private Dictionary<string, List<EventHandlerInformation>> GetHandlersInformation()
    {
        const string receiversFieldName = "_receivers";
        var field = _inboxEventsExecutor.GetType().GetField(receiversFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "_receivers field not found via reflection");

        var receivers = (Dictionary<string, List<EventHandlerInformation>>)field!.GetValue(_inboxEventsExecutor);
        return receivers;
    }

    #endregion
}