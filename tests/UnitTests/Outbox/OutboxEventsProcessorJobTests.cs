using EventStorage.Configurations;
using EventStorage.Outbox;
using EventStorage.Outbox.BackgroundServices;
using EventStorage.Outbox.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventsProcessorJobTests
{
    private IServiceProvider _serviceProvider;
    private IOutboxEventsProcessor _outboxEventsProcessor;
    private ILogger<OutboxEventsProcessorJob> _logger;
    private InboxAndOutboxSettings _settings;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _outboxEventsProcessor = Substitute.For<IOutboxEventsProcessor>();
        _logger = Substitute.For<ILogger<OutboxEventsProcessorJob>>();
        _settings = new InboxAndOutboxSettings
        {
            Outbox = new InboxOrOutboxStructure
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        };
    }

    #endregion

    #region StartAsync

    [Test]
    public async Task StartAsync_WithDefaultSettings_ShouldWork()
    {
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var eventsReceiverService = new OutboxEventsProcessorJob(
            services: _serviceProvider,
            outboxEventsProcessor: _outboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;

        await eventsReceiverService.StartAsync(cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        eventStoreTablesCreator.Received(1).CreateTablesIfNotExists();

        //We cannot test this because it is an asynchronous method
        await _outboxEventsProcessor.ExecuteUnprocessedEvents(cancellationToken);
    }

    [Test]
    public async Task StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var stoppingToken = new CancellationTokenSource();
        stoppingToken.CancelAfter(100);

        var eventsPublisherService = new OutboxEventsProcessorJob(
            services: _serviceProvider,
            outboxEventsProcessor: _outboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _outboxEventsProcessor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Test exception"));

        await eventsPublisherService.StartAsync(CancellationToken.None);

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
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var stoppingToken = new CancellationTokenSource();
        CancellationToken cancellationToken = stoppingToken.Token;
        _outboxEventsProcessor
            .ExecuteUnprocessedEvents(cancellationToken)
            .Returns(async _ => await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));

        var eventsPublisherService = new OutboxEventsProcessorJob(
            services: _serviceProvider,
            outboxEventsProcessor: _outboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );

        _ = eventsPublisherService.StartAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await stoppingToken.CancelAsync();

        await _outboxEventsProcessor.Received().ExecuteUnprocessedEvents(
            Arg.Is<CancellationToken>(ct => ct.IsCancellationRequested == true)
        );
    }

    #endregion
}