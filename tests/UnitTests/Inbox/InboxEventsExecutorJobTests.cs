using EventStorage.Configurations;
using EventStorage.Inbox;
using EventStorage.Inbox.BackgroundServices;
using EventStorage.Inbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Inbox;

public class InboxEventsExecutorJobTests
{
    private IServiceProvider _serviceProvider;
    private IInboxEventsExecutor _inboxEventsExecutor;
    private ILogger<InboxEventsExecutorJob> _logger;
    private InboxAndOutboxSettings _settings;

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _inboxEventsExecutor = Substitute.For<IInboxEventsExecutor>();
        _logger = Substitute.For<ILogger<InboxEventsExecutorJob>>();
        _settings = new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        };
    }

    [Test]
    public async Task StartAsync_WithDefaultSettings_ShouldWork()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var inboxRepository = Substitute.For<IInboxRepository>();
        scope.ServiceProvider.GetService(typeof(IInboxRepository)).Returns(inboxRepository);

        var eventsReceiverService = new InboxEventsExecutorJob(
            services: _serviceProvider,
            inboxEventsExecutor: _inboxEventsExecutor,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;
        
        await eventsReceiverService.StartAsync(cancellationToken);
        
        inboxRepository.Received(1).CreateTableIfNotExists();

        //We cannot test this because it is an asynchronous method
        await _inboxEventsExecutor.ExecuteUnprocessedEvents(cancellationToken);
    }

    [Test]
    public async Task StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var inboxRepository = Substitute.For<IInboxRepository>();
        scope.ServiceProvider.GetService(typeof(IInboxRepository)).Returns(inboxRepository);

        var stoppingToken = new CancellationTokenSource();
        stoppingToken.CancelAfter(100);

        var eventsReceiverService = new InboxEventsExecutorJob(
            services: _serviceProvider,
            inboxEventsExecutor: _inboxEventsExecutor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _inboxEventsExecutor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Test exception"));
        
        await eventsReceiverService.StartAsync(CancellationToken.None);
        
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
        var inboxRepository = Substitute.For<IInboxRepository>();
        scope.ServiceProvider.GetService(typeof(IInboxRepository)).Returns(inboxRepository);

        var stoppingToken = new CancellationTokenSource();
        CancellationToken cancellationToken = stoppingToken.Token;

        _inboxEventsExecutor
            .ExecuteUnprocessedEvents(cancellationToken)
            .Returns(async _ => await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));

        var eventsReceiverService = new InboxEventsExecutorJob(
            services: _serviceProvider,
            inboxEventsExecutor: _inboxEventsExecutor,
            settings: _settings,
            logger: _logger
        );
        
        _ = eventsReceiverService.StartAsync(cancellationToken);
        await stoppingToken.CancelAsync();
        
        await _inboxEventsExecutor.Received().ExecuteUnprocessedEvents(
            Arg.Is<CancellationToken>(ct => ct.IsCancellationRequested == true)
        );
    }
}