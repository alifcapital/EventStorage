using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Models;
using EventStorage.Outbox;
using EventStorage.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class EventsPublisherManagerTests
{
    private EventsPublisherManager _eventsPublisherManager;
    private IServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<EventsPublisherManager>>();
        _serviceProvider.GetService(typeof(ILogger<EventsPublisherManager>)).Returns(logger);
        _serviceProvider.GetService(typeof(InboxAndOutboxSettings)).Returns(new InboxAndOutboxSettings
        {
            Outbox = new InboxOrOutboxStructure
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        });
        _eventsPublisherManager = new EventsPublisherManager(_serviceProvider);
    }

    #region AddPublisher
    [Test]
    public void AddPublisher_OneEvent_ShouldAddOnDictionary()
    {
        // Arrange
        var typeOfSentEvent = typeof(SimpleSendEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        var providerType = EventProviderType.MessageBroker;

        // Act
        _eventsPublisherManager.AddPublisher(
            typeOfEventSender: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: false,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );
        
        // Assert
        var field = typeof(EventsPublisherManager).GetField("_publishers",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().NotBeNull();
        var publishers =
            (Dictionary<string, (Type typeOfEvent, Type typeOfPublisher, string provider, bool hasHeaders, bool
                hasAdditionalData, bool isGlobalPublisher)>)field.GetValue(_eventsPublisherManager);

        publishers.Should().ContainKey($"{typeOfSentEvent.Name}-{providerType.ToString()}");
    }
    
    [Test]
    public void AddPublisher_OneEventWithHeaders_ShouldAddOnDictionary()
    {
        // Arrange
        var typeOfSentEvent = typeof(SimpleSendEventCreated);
        var typeOfEventPublisher = typeof(SimpleSendEventCreatedHandler);
        var providerType = EventProviderType.MessageBroker;

        // Act
        _eventsPublisherManager.AddPublisher(
            typeOfEventSender: typeOfSentEvent,
            typeOfEventPublisher: typeOfEventPublisher,
            providerType: providerType,
            hasHeaders: true,
            hasAdditionalData: false,
            isGlobalPublisher: true
        );
        
        // Assert
        var field = typeof(EventsPublisherManager).GetField("_publishers",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().NotBeNull();
        var publishers =
            (Dictionary<string, (Type typeOfEvent, Type typeOfPublisher, string provider, bool hasHeaders, bool
                hasAdditionalData, bool isGlobalPublisher)>)field.GetValue(_eventsPublisherManager);

        publishers.First().Value.hasHeaders.Should().BeTrue();
    }
    #endregion
}