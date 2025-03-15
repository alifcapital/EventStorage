using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Inbox;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using EventStorage.Tests.Domain.Module1;
using FluentAssertions;
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
    public void AddReceiver_OneEventWithSingleReceiver_OneReceiverInformationShouldAddToDictionary()
    {
        var typeOfReceiveEvent = typeof(SimpleEntityWasCreated);
        var typeOfEventReceiver = typeof(SimpleEntityWasCreatedHandler);
        var providerType = EventProviderType.Unknown;
        _inboxEventsExecutor.AddReceiver(typeOfReceiveEvent, typeOfEventReceiver, providerType);

        var receivers = GetReceiversInformation();

        var receiverKey = InboxEventsExecutor.GetReceiverKey(typeOfReceiveEvent.Name, providerType.ToString());
        Assert.That(receivers.ContainsKey(receiverKey), Is.True);

        var receiversInformation = receivers[receiverKey];
        Assert.That(receiversInformation.Count, Is.EqualTo(1));
        Assert.That(
            receiversInformation.Any(r =>
                r.EventType == typeOfReceiveEvent && r.EventHandlerType == typeOfEventReceiver), Is.True);
    }

    [Test]
    public void AddReceiver_OneEventWithTwoReceivers_TwoReceiversInformationShouldAddToDictionary()
    {
        var typeOfReceiveEvent1 = typeof(UserCreated);
        var typeOfReceiveEvent2 = typeof(UserCreated);
        var typeOfEventReceiver1 = typeof(Domain.Module1.UserCreatedHandler);
        var typeOfEventReceiver2 = typeof(Domain.Module2.UserCreatedHandler);
        var providerType = EventProviderType.MessageBroker;
        _inboxEventsExecutor.AddReceiver(typeOfReceiveEvent1, typeOfEventReceiver1, providerType);
        _inboxEventsExecutor.AddReceiver(typeOfReceiveEvent2, typeOfEventReceiver2, providerType);

        var receivers = GetReceiversInformation();

        var receiverKey = InboxEventsExecutor.GetReceiverKey(typeOfReceiveEvent1.Name, providerType.ToString());
        Assert.That(receivers.ContainsKey(receiverKey), Is.True);

        var receiversInformation = receivers[receiverKey];
        Assert.That(receiversInformation.Count, Is.EqualTo(2));
        Assert.That(receiversInformation.Any(r => r.EventType == typeOfReceiveEvent1 && r.EventHandlerType == typeOfEventReceiver1), Is.True);
        Assert.That(receiversInformation.Any(r => r.EventType == typeOfReceiveEvent2 && r.EventHandlerType == typeOfEventReceiver2), Is.True);
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
            NamingPolicyType = NamingPolicyType.PascalCase.ToString(),
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

        _inboxEventsExecutor.AddReceiver(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);

        await _inboxEventsExecutor.ExecuteUnprocessedEvents(CancellationToken.None);

        await _inboxRepository
            .Received(1)
            .UpdateEventsAsync(Arg.Is<IEnumerable<InboxMessage>>(x =>
                x.Count() == 1 && x.First().TryCount == 0 && x.First().ProcessedAt != null)
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
                x.Count() == 1 && x.First().TryCount == 1 && x.First().ProcessedAt == null)
            );
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Get the receivers information from the ReceivedEventExecutor
    /// </summary>
    private Dictionary<string, List<EventHandlerInformation>> GetReceiversInformation()
    {
        const string receiversFieldName = "_receivers";
        var field = _inboxEventsExecutor.GetType().GetField(receiversFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();

        var receivers = (Dictionary<string, List<EventHandlerInformation>>)field!.GetValue(_inboxEventsExecutor);
        return receivers;
    }

    #endregion
}