using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Models;
using EventStorage.Outbox;
using EventStorage.Outbox.Models;
using EventStorage.Tests.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventsProcessorTests
{
    private OutboxEventsProcessor _outboxEventsProcessor;
    private IServiceProvider _serviceProvider;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<OutboxEventsProcessor>>();
        _serviceProvider.GetService(typeof(ILogger<OutboxEventsProcessor>)).Returns(logger);
        _serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Outbox = new InboxOrOutboxStructure
            {
                MaxConcurrency = 1,
                TryCount = 3,
                TryAfterMinutes = 5,
                TryAfterMinutesIfEventNotFound = 10
            }
        });

        _outboxEventsProcessor = new OutboxEventsProcessor(_serviceProvider);
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

    #endregion
}
