using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Outbox;
using EventStorage.Outbox.BackgroundServices;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventsProcessorJobTests
{
    private IServiceProvider _serviceProvider;
    private IServiceScopeFactory _serviceScopeFactory;
    private IOutboxEventsProcessor _outboxEventsProcessor;
    private ILogger<OutboxEventsProcessorJob> _logger;
    private InboxAndOutboxSettings _settings;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        var scope = Substitute.For<IServiceScope>();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);

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
            scopeFactory: _serviceScopeFactory,
            outboxEventsProcessor: _outboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;

        _ = ExecuteBackgroundServiceAsync(eventsReceiverService, cancellationToken);

        await eventStoreTablesCreator.Received(1).CreateTablesIfNotExistsAsync(cancellationToken);
        //We cannot test this because it is an asynchronous method
        await _outboxEventsProcessor.ExecuteUnprocessedEvents(cancellationToken);
    }

    [Test]
    public void StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var stoppingToken = new CancellationTokenSource();
        stoppingToken.CancelAfter(100);
        var eventsPublisherService = new OutboxEventsProcessorJob(
            scopeFactory: _serviceScopeFactory,
            outboxEventsProcessor: _outboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _outboxEventsProcessor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Test exception"));

        _ = ExecuteBackgroundServiceAsync(eventsPublisherService, CancellationToken.None);

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
        var stoppingTokenSource = new CancellationTokenSource();
        _outboxEventsProcessor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => stoppingTokenSource.Cancel());

        var eventsPublisherService = new OutboxEventsProcessorJob(
            scopeFactory: _serviceScopeFactory,
            outboxEventsProcessor: _outboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );

        _ = ExecuteBackgroundServiceAsync(eventsPublisherService, stoppingTokenSource.Token);

        await _outboxEventsProcessor.Received().ExecuteUnprocessedEvents(
            Arg.Is<CancellationToken>(ct => ct.IsCancellationRequested == true)
        );
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Executes the background service's ExecuteAsync method immediately.
    /// </summary>
    private async Task ExecuteBackgroundServiceAsync(BackgroundService service, CancellationToken cancellationToken)
    {
        var executeAsyncTask =
            (Task)service.GetType().BaseType!.GetMethod("ExecuteAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(service, [cancellationToken])!;
        await executeAsyncTask;
    }

    #endregion
}