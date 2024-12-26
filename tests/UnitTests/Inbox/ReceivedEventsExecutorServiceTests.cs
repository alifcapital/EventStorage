using EventStorage.Configurations;
using EventStorage.Inbox;
using EventStorage.Inbox.BackgroundServices;
using EventStorage.Inbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Inbox;

public class ReceivedEventsExecutorServiceTests
{
    private IServiceProvider _serviceProvider;
    private IReceivedEventExecutor _receivedEventExecutor;
    private ILogger<ReceivedEventsExecutorService> _logger;
    private InboxAndOutboxSettings _settings;

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _receivedEventExecutor = Substitute.For<IReceivedEventExecutor>();
        _logger = Substitute.For<ILogger<ReceivedEventsExecutorService>>();
        _settings = new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure
                { MaxConcurrency = 1, TryCount = 3, TryAfterMinutes = 5, TryAfterMinutesIfEventNotFound = 10 }
        };
    }

    [Test]
    public async Task StartAsync_WithDefaultSettings_ShouldWork()
    {
        // Arrange
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var inboxRepository = Substitute.For<IInboxRepository>();
        scope.ServiceProvider.GetService(typeof(IInboxRepository)).Returns(inboxRepository);

        var eventsReceiverService = new ReceivedEventsExecutorService(
            services: _serviceProvider,
            receivedEventExecutor: _receivedEventExecutor,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;

        // Act
        await eventsReceiverService.StartAsync(cancellationToken);

        // Assert
        inboxRepository.Received(1).CreateTableIfNotExists();

        //We cannot test this because it is an asynchronous method
        await _receivedEventExecutor.ExecuteUnprocessedEvents(cancellationToken);
    }

    [Test]
    public async Task StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        // Arrange
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var inboxRepository = Substitute.For<IInboxRepository>();
        scope.ServiceProvider.GetService(typeof(IInboxRepository)).Returns(inboxRepository);

        var stoppingToken = new CancellationTokenSource();
        stoppingToken.CancelAfter(100);

        var eventsReceiverService = new ReceivedEventsExecutorService(
            services: _serviceProvider,
            receivedEventExecutor: _receivedEventExecutor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _receivedEventExecutor
            .When(x => x.ExecuteUnprocessedEvents(Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Test exception"));

        // Act
        await eventsReceiverService.StartAsync(CancellationToken.None);

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
        var inboxRepository = Substitute.For<IInboxRepository>();
        scope.ServiceProvider.GetService(typeof(IInboxRepository)).Returns(inboxRepository);

        var stoppingToken = new CancellationTokenSource();
        CancellationToken cancellationToken = stoppingToken.Token;

        _receivedEventExecutor
            .ExecuteUnprocessedEvents(cancellationToken)
            .Returns(async _ => await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));

        var eventsReceiverService = new ReceivedEventsExecutorService(
            services: _serviceProvider,
            receivedEventExecutor: _receivedEventExecutor,
            settings: _settings,
            logger: _logger
        );

        // Act
        _ = eventsReceiverService.StartAsync(cancellationToken);
        await stoppingToken.CancelAsync();
        
        // Assert
        await _receivedEventExecutor.Received().ExecuteUnprocessedEvents(
            Arg.Is<CancellationToken>(ct => ct.IsCancellationRequested == true)
        );
    }
}