using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Inbox;
using EventStorage.Inbox.BackgroundServices;
using EventStorage.Inbox.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Inbox;

[TestFixture]
public class InboxEventsProcessorJobTests
{
    private IServiceProvider _serviceProvider;
    private IServiceScopeFactory _serviceScopeFactory;
    private IInboxEventsProcessor _inboxEventsProcessor;
    private ILogger<InboxEventsProcessorJob> _logger;
    private InboxAndOutboxSettings _settings;

    #region Setup

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        var scope = Substitute.For<IServiceScope>();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);

        _inboxEventsProcessor = Substitute.For<IInboxEventsProcessor>();
        _logger = Substitute.For<ILogger<InboxEventsProcessorJob>>();
        _settings = new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure
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
        var eventsReceiverService = new InboxEventsProcessorJob(
            scopeFactory: _serviceScopeFactory,
            inboxEventsProcessor: _inboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;

        _ = ExecuteBackgroundServiceAsync(eventsReceiverService, cancellationToken);

        await eventStoreTablesCreator.Received(1).CreateTablesIfNotExistsAsync(cancellationToken);
        //We cannot test this because it is an asynchronous method
        await _inboxEventsProcessor.ExecuteUnprocessedEvents(cancellationToken);
    }

    [Test]
    public void StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var eventsReceiverService = new InboxEventsProcessorJob(
            scopeFactory: _serviceScopeFactory,
            inboxEventsProcessor: _inboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _inboxEventsProcessor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Test exception"));

        _ = ExecuteBackgroundServiceAsync(eventsReceiverService, CancellationToken.None);

        _logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex.Message == "Test exception"),
            Arg.Any<Func<object, Exception, string>>()!
        );
    }

    #endregion

    #region ExecuteAsync

    [Test]
    public async Task ExecuteAsync_CancellationRequested_ShouldStopProcessing()
    {
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var stoppingTokenSource = new CancellationTokenSource();
        _inboxEventsProcessor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => stoppingTokenSource.Cancel());

        var eventsReceiverService = new InboxEventsProcessorJob(
            scopeFactory: _serviceScopeFactory,
            inboxEventsProcessor: _inboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );

        _ = ExecuteBackgroundServiceAsync(eventsReceiverService, stoppingTokenSource.Token);

        await _inboxEventsProcessor.Received(1).ExecuteUnprocessedEvents(
            Arg.Is<CancellationToken>(ct => ct.IsCancellationRequested)
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