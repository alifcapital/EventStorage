using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Inbox;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using IServiceScopeFactory = Microsoft.Extensions.DependencyInjection.IServiceScopeFactory;

namespace EventStorage.Tests.UnitTests.Inbox;

internal class EventsReceiverManagerTests
{
    private EventsReceiverManager _eventReceiverManager;
    private IServiceProvider _serviceProvider;
    private IInboxRepository _inboxRepository;

    [SetUp]
    public void Setup()
    {
        var logger = NullLogger<EventsReceiverManager>.Instance;
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(ILogger<EventsReceiverManager>)).Returns(logger);

        _inboxRepository = Substitute.For<IInboxRepository>();
        _serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure()
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        });
        _serviceProvider.GetService(typeof(IInboxRepository)).Returns(_inboxRepository);

        _eventReceiverManager = new EventsReceiverManager(_serviceProvider);
    }

    [Test]
    public void AddReceiver_OneEvent_ShouldAddToDictionary()
    {
        // Arrange
        var typeOfReceiveEvent = typeof(SimpleEntityWasCreated);
        var typeOfEventReceiver = typeof(SimpleEntityWasCreatedHandler);
        var providerType = EventProviderType.Unknown;

        // Act
        _eventReceiverManager.AddReceiver(typeOfReceiveEvent, typeOfEventReceiver, providerType);

        // Assert
        var field = typeof(EventsReceiverManager).GetField("_receivers",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().NotBeNull();
        var receivers =
            (Dictionary<string, (Type eventType, Type eventReceiverType, string providerType, bool hasHeaders, bool
                hasAdditionalData)>)field.GetValue(_eventReceiverManager);

        receivers.Should().ContainKey(typeOfReceiveEvent.Name);
    }

    #region ExecuteUnprocessedEvents
    [Test]
    public async Task ExecuteUnprocessedEvents_EventTryAfterIsBeforeNow_ShouldProcessed()
    {
        // Arrange
        var inboxEvent = new InboxEvent
        {
            Id = Guid.NewGuid(),
            EventName = "SimpleEntityWasCreated",
            Provider = "Unknown",
            Payload = "{}",
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

        // Act
        _eventReceiverManager.AddReceiver(typeof(SimpleEntityWasCreated), typeof(SimpleEntityWasCreatedHandler),
            EventProviderType.Unknown);

        await _eventReceiverManager.ExecuteUnprocessedEvents(CancellationToken.None);

        // Assert
        await _inboxRepository
            .Received(1)
            .UpdateEventsAsync(Arg.Is<IEnumerable<InboxEvent>>(x =>
                x.Count() == 1 && x.First().TryCount == 0 && x.First().ProcessedAt != null)
            );
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_EventTryAfterIsAfterNow_ShouldNotProcessed()
    {
        // Arrange
        var inboxEvent = new InboxEvent
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

        // Act
        await _eventReceiverManager.ExecuteUnprocessedEvents(CancellationToken.None);

        // Assert
        await _inboxRepository
            .Received(1)
            .UpdateEventsAsync(Arg.Is<IEnumerable<InboxEvent>>(x =>
                x.Count() == 1 && x.First().TryCount == 1 && x.First().ProcessedAt == null)
            );
    }
    #endregion
}