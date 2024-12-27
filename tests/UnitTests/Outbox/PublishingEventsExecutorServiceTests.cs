using EventStorage.Configurations;
using EventStorage.Outbox;
using EventStorage.Outbox.BackgroundServices;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class PublishingEventsExecutorServiceTests
{
    private IServiceProvider _serviceProvider;
    private IPublishingEventExecutor _publishingEventExecutor;
    private ILogger<PublishingEventsExecutorService> _logger;
    private InboxAndOutboxSettings _settings;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _publishingEventExecutor = Substitute.For<IPublishingEventExecutor>();
        _logger = Substitute.For<ILogger<PublishingEventsExecutorService>>();
        _settings = new InboxAndOutboxSettings
        {
            Outbox = new InboxOrOutboxStructure
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        };
    }

    #endregion

    #region StartAsync
    
    [Test]
    public async Task StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        // Arrange
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var outboxRepository = Substitute.For<IOutboxRepository>();
        scope.ServiceProvider.GetService(typeof(IOutboxRepository)).Returns(outboxRepository);

        var stoppingToken = new CancellationTokenSource();
        stoppingToken.CancelAfter(100);

        var eventsPublisherService = new PublishingEventsExecutorService(
            publishingEventExecutor: _publishingEventExecutor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _publishingEventExecutor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Test exception"));

        // Act
        await eventsPublisherService.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex.Message == "Test exception"),
            Arg.Any<Func<object, Exception, string>>()!
        );
    }

    [Test]
    public async Task StartAsync_CancellationRequested_ShouldStopWithCancellationRequestTrue()
    {
        // Arrange
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var outboxRepository = Substitute.For<IOutboxRepository>();
        scope.ServiceProvider.GetService(typeof(IOutboxRepository)).Returns(outboxRepository);

        var stoppingToken = new CancellationTokenSource();
        CancellationToken cancellationToken = stoppingToken.Token;

        _publishingEventExecutor
            .ExecuteUnprocessedEvents(cancellationToken)
            .Returns(async _ => await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));

        var eventsPublisherService = new PublishingEventsExecutorService(
            publishingEventExecutor: _publishingEventExecutor,
            settings: _settings,
            logger: _logger
        );

        // Act
        _ = eventsPublisherService.StartAsync(cancellationToken);
        await stoppingToken.CancelAsync();
        
        // Assert
        await _publishingEventExecutor.Received().ExecuteUnprocessedEvents(
            Arg.Is<CancellationToken>(ct => ct.IsCancellationRequested == true)
        );
    }
    #endregion
}