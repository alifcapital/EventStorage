using EventStorage.Configurations;
using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Tests.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
internal abstract class BaseCleanUpProcessedEventsJobTests<TEventRepository, TEventBox>
    where TEventBox : class, IBaseMessageBox
    where TEventRepository : class, IBaseEventRepository<TEventBox>
{
    private IServiceProvider _serviceProvider;
    private TEventRepository _eventRepository;
    private ILogger _logger;
    private InboxOrOutboxStructure _settings;

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _eventRepository = Substitute.For<TEventRepository>();
        _logger = Substitute.For<ILogger>();
        _settings = new InboxOrOutboxStructure { DaysToCleanUpEvents = 1 };
    }

    #region StartAsync
    
    [Test]
    public async Task StartAsync_SettingsDaysToCleanIsOne_ShouldDelete()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.GetService(typeof(TEventRepository)).Returns(_eventRepository);

        var cleanUpProcessedEventsService = new CleanUpProcessedEventsJob<TEventRepository, TEventBox>(
            services: _serviceProvider,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;
        
        await cleanUpProcessedEventsService.StartAsync(cancellationToken);
        
        await _eventRepository.Received().DeleteProcessedEventsAsync(
            Arg.Is<DateTime>(d => d.Day == DateTime.Now.AddDays(-1).Day)
        );
    }
    
    [Test]
    public async Task StartAsync_SettingsDaysToCleanIsZero_ShouldNotDelete()
    {
        _settings = _settings with
        {
            DaysToCleanUpEvents = 0
        };
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.GetService(typeof(TEventRepository)).Returns(_eventRepository);

        var cleanUpProcessedEventsService = new CleanUpProcessedEventsJob<TEventRepository, TEventBox>(
            services: _serviceProvider,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;
        
        await cleanUpProcessedEventsService.StartAsync(cancellationToken);
        
        await _eventRepository.DidNotReceive().DeleteProcessedEventsAsync(Arg.Any<DateTime>());
    }
    
    [Test]
    public async Task StartAsync_WhenReceiveExceptionWhileDeleting_ShouldLogException()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.GetService(typeof(TEventRepository)).Returns(_eventRepository);

        var stoppingToken = new CancellationTokenSource();
        stoppingToken.CancelAfter(100);

        var cleanUpProcessedEventsService = new CleanUpProcessedEventsJob<TEventRepository, TEventBox>(
            services: _serviceProvider,
            settings: _settings,
            logger: _logger
        );

        // Simulate exception in DeleteProcessedEventsAsync
        _eventRepository
            .When(x => x.DeleteProcessedEventsAsync(Arg.Any<DateTime>()))
            .Throw(new Exception("Simulated exception"));
        
        await cleanUpProcessedEventsService.StartAsync(stoppingToken.Token);
        
        _logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>()!
        );
    }
    #endregion
}