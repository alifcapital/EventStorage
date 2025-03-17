using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Inbox.Models;
using EventStorage.Models;
using EventStorage.Outbox;
using EventStorage.Outbox.Models;
using EventStorage.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventsExecutorTests
{
    private OutboxEventsExecutor _outboxEventsExecutor;
    private IServiceProvider _serviceProvider;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<OutboxEventsExecutor>>();
        _serviceProvider.GetService(typeof(ILogger<OutboxEventsExecutor>)).Returns(logger);
        _serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Outbox = new InboxOrOutboxStructure
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        });
        _outboxEventsExecutor = new OutboxEventsExecutor(_serviceProvider);
    }

    #endregion

    #region AddPublisher
    
    [Test]
    public void AddPublisher_AddingOneEventTypeWithPublisherInfo_PublisherInfoShouldBeEqualToAddedInfo()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        var providerType = EventProviderType.MessageBroker;

        _outboxEventsExecutor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );
        
        var publishers = GetPublishersInformation();

        var publisherKey = _outboxEventsExecutor.GetPublisherKey(typeOfSentEvent.Name, providerType.ToString());
        Assert.That(publishers.ContainsKey(publisherKey), Is.True);

        var publisherInformation = publishers[publisherKey];
        Assert.That(publisherInformation.EventType, Is.EqualTo(typeOfSentEvent));
        Assert.That(publisherInformation.EventPublisherType, Is.EqualTo(typeOfEventPublisher));
    }
    
    [Test]
    public void AddPublisher_AddingOneEventTypeWithAdditionalInfo_PublisherInfoShouldBeEqualToAddedInfo()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        var providerType = EventProviderType.MessageBroker;

        _outboxEventsExecutor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: true,
            hasAdditionalData: true,
            isGlobalPublisher: true
        );

        var publishers = GetPublishersInformation();

        var publisherKey = _outboxEventsExecutor.GetPublisherKey(typeOfSentEvent.Name, providerType.ToString());
        Assert.That(publishers.ContainsKey(publisherKey), Is.True);

        var publisherInformation = publishers[publisherKey];
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
        var providerType = EventProviderType.MessageBroker;

        _outboxEventsExecutor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );

        var publishers = _outboxEventsExecutor.GetEventPublisherTypes(typeOfSentEvent.Name);
        Assert.That(publishers, Does.Contain(providerType));
    }
    
    [Test]
    public void AddEventProviderType_AddingOneEventTypeTwice_OnePublisherTypeShouldBeAdded()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        var providerType = EventProviderType.MessageBroker;

        _outboxEventsExecutor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );
        _outboxEventsExecutor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );

        var eventProviderTypes = _outboxEventsExecutor.GetEventPublisherTypes(typeOfSentEvent.Name);
        Assert.That(eventProviderTypes.Count, Is.EqualTo(1));
    }
    
    [Test]
    public void GetEventPublisherTypes_TryingToGetInvalidType_ShouldReturnNull()
    {
        var typeOfSentEvent = typeof(SimpleOutboxEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        var providerType = EventProviderType.MessageBroker;

        _outboxEventsExecutor.AddPublisher(
            typeOfOutboxEvent: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );

        var publishers = _outboxEventsExecutor.GetEventPublisherTypes(typeOfEventPublisher.Name);
        Assert.That(publishers, Is.Null);
    }
    
    #endregion

    #region Helper methods

    /// <summary>
    /// Get the publisher information from the OutboxEventsExecutor.
    /// </summary>
    private Dictionary<string, EventPublisherInformation> GetPublishersInformation()
    {
        const string publishersFieldName = "_publishers";
        var field = _outboxEventsExecutor.GetType().GetField(publishersFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();

        var publishers = (Dictionary<string, EventPublisherInformation>)field!.GetValue(_outboxEventsExecutor);
        return publishers;
    }

    #endregion
}