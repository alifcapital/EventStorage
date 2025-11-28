using EventStorage.Configurations;
using EventStorage.Inbox;
using EventStorage.Inbox.BackgroundServices;
using EventStorage.Inbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Inbox;

public class InboxEventsProcessorJobTests
{
    private IServiceProvider _serviceProvider;
    private IInboxEventsProcessor _inboxEventsProcessor;
    private ILogger<InboxEventsProcessorJob> _logger;
    private InboxAndOutboxSettings _settings;

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _inboxEventsProcessor = Substitute.For<IInboxEventsProcessor>();
        _logger = Substitute.For<ILogger<InboxEventsProcessorJob>>();
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

        var eventsReceiverService = new InboxEventsProcessorJob(
            services: _serviceProvider,
            inboxEventsProcessor: _inboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;
        
        await eventsReceiverService.StartAsync(cancellationToken);
        
        inboxRepository.Received(1).CreateTableIfNotExists();

        //We cannot test this because it is an asynchronous method
        await _inboxEventsProcessor.ExecuteUnprocessedEvents(cancellationToken);
    }

    [Test]
    [Parallelizable(ParallelScope.Self)]
    public async Task StartAsync_ThrowingExceptionOnExecutingUnprocessedEvents_ShouldLogException()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var inboxRepository = Substitute.For<IInboxRepository>();
        scope.ServiceProvider.GetService(typeof(IInboxRepository)).Returns(inboxRepository);

        var eventsReceiverService = new InboxEventsProcessorJob(
            services: _serviceProvider,
            inboxEventsProcessor: _inboxEventsProcessor,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in ExecuteUnprocessedEvents
        _inboxEventsProcessor
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
}