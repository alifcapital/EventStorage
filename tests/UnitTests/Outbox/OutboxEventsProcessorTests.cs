using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Models;
using EventStorage.Outbox;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using EventStorage.Tests.Domain;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventsProcessorTests
{
    private OutboxEventsProcessor _outboxEventsProcessor;
    private IServiceProvider _serviceProvider;
    private IOutboxRepository _outboxRepository;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        MockDistributedLockProvider(serviceProvider);
        var logger = Substitute.For<ILogger<OutboxEventsProcessor>>();
        serviceProvider.GetService(typeof(ILogger<OutboxEventsProcessor>)).Returns(logger);
        serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Outbox = new InboxOrOutboxStructure
            {
                MaxConcurrency = 1,
                TryCount = 3,
                TryAfterMinutes = 5,
                TryAfterMinutesIfEventNotFound = 10
            }
        });
        _outboxRepository = Substitute.For<IOutboxRepository>();
        serviceProvider.GetService(typeof(IOutboxRepository)).Returns(_outboxRepository);
        _serviceProvider = serviceProvider;

        _outboxEventsProcessor = new OutboxEventsProcessor(serviceProvider);
    }

    #endregion

    #region AddPublisher

    [Test]
    public void AddPublisher_AddingOneEventTypeWithPublisherInfo_PublisherInfoShouldBeEqualToAddedInfo()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        const EventProviderType providerType = EventProviderType.MessageBroker;

        _outboxEventsProcessor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );

        var publishers = GetPublishersInformation();
        var publisherKey = _outboxEventsProcessor.GetPublisherKey(typeOfSentEvent.Name, typeOfSentEvent.Namespace);
        Assert.That(publishers.ContainsKey(publisherKey), Is.True);

        var publishersInfo = publishers[publisherKey];
        Assert.That(publishersInfo.Count, Is.EqualTo(1));

        var publisherInformation = publishersInfo.First().Value;
        Assert.That(publisherInformation.EventType, Is.EqualTo(typeOfSentEvent));
        Assert.That(publisherInformation.EventPublisherType, Is.EqualTo(typeOfEventPublisher));
    }

    [Test]
    public void AddPublisher_AddingOneEventTypeWithAdditionalInfo_PublisherInfoShouldBeEqualToAddedInfo()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        const EventProviderType providerType = EventProviderType.MessageBroker;

        _outboxEventsProcessor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: true,
            hasAdditionalData: true,
            isGlobalPublisher: true
        );

        var publishers = GetPublishersInformation();
        var publisherKey = _outboxEventsProcessor.GetPublisherKey(typeOfSentEvent.Name, typeOfSentEvent.Namespace);
        Assert.That(publishers.ContainsKey(publisherKey), Is.True);

        var publisherInformation = publishers[publisherKey].First().Value;
        Assert.That(publisherInformation.HasHeaders, Is.True);
        Assert.That(publisherInformation.HasAdditionalData, Is.True);
        Assert.That(publisherInformation.IsGlobalPublisher, Is.True);
    }

    #endregion

    #region Add and get event provider types

    [Test]
    public void AddEventProviderType_AddingOneEventTypeWithPublisherInfo_OnePublisherTypeShouldBeAdded()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        const EventProviderType providerType = EventProviderType.MessageBroker;

        _outboxEventsProcessor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );

        var outboxEvent = new SimpleOutboxEventCreated();
        var publishers = _outboxEventsProcessor.GetEventPublisherTypes(outboxEvent);
        Assert.That(publishers, Does.Contain(providerType.ToString()));
    }

    [Test]
    public void AddEventProviderType_AddingOneEventTypeTwice_OnePublisherTypeShouldBeAdded()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        const EventProviderType providerType = EventProviderType.MessageBroker;

        _outboxEventsProcessor.AddPublisher(typeOfSentEvent, typeOfEventPublisher, providerType, false, false, true);
        _outboxEventsProcessor.AddPublisher(typeOfSentEvent, typeOfEventPublisher, providerType, false, false, true);

        var outboxEvent = new SimpleOutboxEventCreated();
        var publishers = _outboxEventsProcessor.GetEventPublisherTypes(outboxEvent);
        Assert.That(publishers, Does.Contain(providerType.ToString()));
    }

    [Test]
    public void GetEventPublisherTypes_TryingToGetInvalidType_ShouldReturnNull()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        const EventProviderType providerType = EventProviderType.MessageBroker;

        _outboxEventsProcessor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );

        var outboxEvent = new SimpleOutboxEventWithoutAdditionalProperties();
        var publishers = _outboxEventsProcessor.GetEventPublisherTypes(outboxEvent);
        Assert.That(publishers, Is.Null);
    }

    #endregion

    #region ExecuteUnprocessedEvents

    [Test]
    public async Task ExecuteUnprocessedEvents_ThereIsNoEventsToProcess_ShouldNotProcessedAnyEvents()
    {
        _outboxRepository.GetUnprocessedEventsAsync(Arg.Any<int>()).Returns([]);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        await _outboxEventsProcessor.ExecuteUnprocessedEvents(CancellationToken.None);

        await _outboxRepository
            .DidNotReceive()
            .UpdateEventAsync(Arg.Any<OutboxMessage>());
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_ThereIsTwoEventsToProcess_BothShouldBeProcessed()
    {
        var outboxEvent1 = new OutboxMessage
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
        var outboxEvent2 = new OutboxMessage
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

        var items = new[] { outboxEvent1, outboxEvent2 };
        _outboxRepository.GetUnprocessedEventsAsync(Arg.Any<int>()).Returns(items);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        const EventProviderType providerType = EventProviderType.MessageBroker;
        _outboxEventsProcessor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );

        await _outboxEventsProcessor.ExecuteUnprocessedEvents(CancellationToken.None);

        await _outboxRepository
            .Received(2)
            .UpdateEventAsync(Arg.Any<OutboxMessage>());
    }

    #endregion

    #region Helper methods

    private Dictionary<string, Dictionary<EventProviderType, EventPublisherInformation>> GetPublishersInformation()
    {
        const string publishersFieldName = "_allPublishers";
        var field = _outboxEventsProcessor.GetType().GetField(publishersFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "_allPublishers field not found in OutboxEventsExecutor");

        var publishers = (Dictionary<string, Dictionary<EventProviderType, EventPublisherInformation>>)
            field!.GetValue(_outboxEventsProcessor);

        return publishers!;
    }

    void MockDistributedLockProvider(IKeyedServiceProvider serviceProvider)
    {
        var distributedLockProvider = Substitute.For<IDistributedLockProvider>();
        serviceProvider.GetRequiredKeyedService(typeof(IDistributedLockProvider), FunctionalityNames.Outbox)
            .Returns(distributedLockProvider);
        
        var distributedLock = Substitute.For<IDistributedLock>();
        distributedLockProvider.CreateLock(Arg.Any<string>()).Returns(distributedLock);

        var distributedSynchronizationHandle = Substitute.For<IDistributedSynchronizationHandle>();
        distributedLock.TryAcquireAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(distributedSynchronizationHandle);
    }


    #endregion
}
