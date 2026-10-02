using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Management.Models;
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
                TryAfterMinutesIfTryCountExceeded = 5,
                TryAfterMinutesIfEventNotFound = 10
            }
        });
        _outboxRepository = Substitute.For<IOutboxRepository>();
        _outboxRepository.GetEventStatusByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(EventStatus.Pending);
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
        _outboxRepository.GetUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);
        scope.ServiceProvider.GetService(typeof(SimpleEntityWasCreatedHandler))
            .Returns(new SimpleEntityWasCreatedHandler());

        await _outboxEventsProcessor.ExecuteUnprocessedEventsAsync(CancellationToken.None);

        await _outboxRepository
            .DidNotReceive()
            .UpdateEventAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
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
        _outboxRepository.GetUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(items);

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

        await _outboxEventsProcessor.ExecuteUnprocessedEventsAsync(CancellationToken.None);

        await _outboxRepository
            .Received(2)
            .UpdateEventAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_StoppingTokenIsPassed_ShouldPassItToRepository()
    {
        MockServiceScope();
        using var stoppingTokenSource = new CancellationTokenSource();
        _outboxRepository.GetUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        await _outboxEventsProcessor.ExecuteUnprocessedEventsAsync(stoppingTokenSource.Token);

        await _outboxRepository.Received(1).GetUnprocessedEventsAsync(Arg.Any<int>(), stoppingTokenSource.Token);
    }

    [Test]
    public async Task ExecuteUnprocessedEvents_CancellationRequested_ShouldThrowWithoutFetchingEvents()
    {
        MockServiceScope();
        using var stoppingTokenSource = new CancellationTokenSource();
        stoppingTokenSource.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _outboxEventsProcessor.ExecuteUnprocessedEventsAsync(stoppingTokenSource.Token));
        await _outboxRepository.DidNotReceive()
            .GetUnprocessedEventsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region ProcessSingleEventAsync

    [Test]
    public async Task ProcessSingleEventAsync_EventIsAlreadyProcessed_ShouldSkipWithoutUpdating()
    {
        MockServiceScope();
        _outboxRepository.GetEventStatusByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(EventStatus.Processed);
        var outboxEvent = CreateOutboxMessage("{}");

        var result = await _outboxEventsProcessor.ProcessSingleEventAsync(outboxEvent, manualRequest: null,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.InvalidState));
        await _outboxRepository.DidNotReceive().UpdateEventAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessSingleEventAsync_PayloadCannotBeDeserialized_ShouldStoreFailureReason()
    {
        MockServiceScope();
        AddSimpleOutboxEventPublisher();
        var outboxEvent = CreateOutboxMessage("not a json");

        var result = await _outboxEventsProcessor.ProcessSingleEventAsync(outboxEvent, manualRequest: null,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.Failed));
        Assert.That(outboxEvent.Status, Is.EqualTo(EventStatus.Failed));
        Assert.That(outboxEvent.FailureReason, Does.StartWith(
            $"EventStorage.Exceptions.EventStoreException: Could not deserialize the outbox event to the {typeof(SimpleOutboxEventCreated).FullName} type."));
        await _outboxRepository.Received(1).UpdateEventAsync(outboxEvent, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessSingleEventAsync_PublisherIsExecuted_ShouldMarkEventAsProcessed()
    {
        MockServiceScope();
        AddSimpleOutboxEventPublisher();
        var outboxEvent = CreateOutboxMessage("{}");

        var result = await _outboxEventsProcessor.ProcessSingleEventAsync(outboxEvent, manualRequest: null,
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(outboxEvent.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(outboxEvent.UpdatedBy, Is.Null);
        await _outboxRepository.Received(1).UpdateEventAsync(outboxEvent, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessSingleEventAsync_CancellationTokenIsPassed_ShouldPassItToStatusCheck()
    {
        MockServiceScope();
        using var cancellationTokenSource = new CancellationTokenSource();
        var outboxEvent = CreateOutboxMessage("{}");

        await _outboxEventsProcessor.ProcessSingleEventAsync(outboxEvent, manualRequest: null,
            cancellationTokenSource.Token);

        await _outboxRepository.Received(1).GetEventStatusByIdAsync(outboxEvent.Id, cancellationTokenSource.Token);
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
        _serviceProvider.GetService(typeof(SimpleSendEventCreatedHandler))
            .Returns(new SimpleSendEventCreatedHandler());
    }

    private void AddSimpleOutboxEventPublisher()
    {
        _outboxEventsProcessor.AddPublisher(
            typeOfOutboxEvent: typeof(SimpleOutboxEventCreated),
            typeOfEventPublisher: typeof(SimpleSendEventCreatedHandler),
            providerType: EventProviderType.MessageBroker,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: false);
    }

    private static OutboxMessage CreateOutboxMessage(string payload)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventName = nameof(SimpleOutboxEventCreated),
            EventPath = typeof(SimpleOutboxEventCreated).Namespace,
            Provider = nameof(EventProviderType.MessageBroker),
            Payload = payload,
            NamingPolicyType = nameof(NamingPolicyType.PascalCase),
            TryAfterAt = DateTime.Now.AddMinutes(-1)
        };
    }

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
