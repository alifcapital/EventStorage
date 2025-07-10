using EventStorage.Configurations;
using EventStorage.Outbox;
using EventStorage.Outbox.BackgroundServices;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutboxEventsExecutorJobTests
{
    private IServiceProvider _serviceProvider;
    private IOutboxEventsExecutor _outboxEventsExecutor;
    private ILogger<OutboxEventsExecutorJob> _logger;
    private InboxAndOutboxSettings _settings;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _outboxEventsExecutor = Substitute.For<IOutboxEventsExecutor>();
        _logger = Substitute.For<ILogger<OutboxEventsExecutorJob>>();
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
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var inboxRepository = Substitute.For<IOutboxRepository>();
        scope.ServiceProvider.GetService(typeof(IOutboxRepository)).Returns(inboxRepository);

        var eventsReceiverService = new OutboxEventsExecutorJob(
            services: _serviceProvider,
            outboxEventsExecutor: _outboxEventsExecutor,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;
        
        await eventsReceiverService.StartAsync(cancellationToken);
        
        inboxRepository.Received(1).CreateTableIfNotExists();

        //We cannot test this because it is an asynchronous method
        await _outboxEventsExecutor.ExecuteUnprocessedEvents(cancellationToken);
    }
    
    [Test]
    public async Task StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var outboxRepository = Substitute.For<IOutboxRepository>();
        scope.ServiceProvider.GetService(typeof(IOutboxRepository)).Returns(outboxRepository);

        var stoppingToken = new CancellationTokenSource();
        stoppingToken.CancelAfter(100);

        var eventsPublisherService = new OutboxEventsExecutorJob(
            services: _serviceProvider,
            outboxEventsExecutor: _outboxEventsExecutor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _outboxEventsExecutor
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
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var outboxRepository = Substitute.For<IOutboxRepository>();
        scope.ServiceProvider.GetService(typeof(IOutboxRepository)).Returns(outboxRepository);

        var stoppingToken = new CancellationTokenSource();
        CancellationToken cancellationToken = stoppingToken.Token;

        _outboxEventsExecutor
            .ExecuteUnprocessedEvents(cancellationToken)
            .Returns(async _ => await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));

        var eventsPublisherService = new OutboxEventsExecutorJob(
            services: _serviceProvider,
            outboxEventsExecutor: _outboxEventsExecutor,
            settings: _settings,
            logger: _logger
        );
        
        _ = eventsPublisherService.StartAsync(cancellationToken);
        await stoppingToken.CancelAsync();
        
        await _outboxEventsExecutor.Received().ExecuteUnprocessedEvents(
            Arg.Is<CancellationToken>(ct => ct.IsCancellationRequested == true)
        );
    }
    #endregion
}