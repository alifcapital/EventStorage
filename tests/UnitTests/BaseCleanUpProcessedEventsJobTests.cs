using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Services;
using EventStorage.Tests.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests;

[TestFixture]
internal abstract class BaseCleanUpProcessedEventsJobTests<TEventRepository, TEventBox>
    where TEventBox : class, IBaseMessageBox
    where TEventRepository : class, IBaseEventRepository<TEventBox>
{
    private IServiceProvider _serviceProvider;
    private IServiceScopeFactory _serviceScopeFactory;
    private TEventRepository _eventRepository;
    private ILogger _logger;
    private InboxOrOutboxStructure _settings;

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        var scope = Substitute.For<IServiceScope>();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(_serviceProvider);

        _eventRepository = Substitute.For<TEventRepository>();
        _serviceProvider.GetService(typeof(TEventRepository)).Returns(_eventRepository);
        _logger = Substitute.For<ILogger>();
        _settings = new InboxOrOutboxStructure { DaysToCleanUpEvents = 1 };
    }

    #region StartAsync

    [Test]
    public async Task StartAsync_SettingsDaysToCleanIsOne_ShouldDelete()
    {
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var cleanUpProcessedEventsService = new CleanUpProcessedEventsJob<TEventRepository, TEventBox>(
            scopeFactory: _serviceScopeFactory,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;

        _ = ExecuteBackgroundServiceAsync(cleanUpProcessedEventsService, cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await eventStoreTablesCreator.Received(1).CreateTablesIfNotExistsAsync(cancellationToken);
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
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var cleanUpProcessedEventsService = new CleanUpProcessedEventsJob<TEventRepository, TEventBox>(
            scopeFactory: _serviceScopeFactory,
            settings: _settings,
            logger: _logger
        );
        var cancellationToken = CancellationToken.None;

        _ = ExecuteBackgroundServiceAsync(cleanUpProcessedEventsService, cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await _eventRepository.DidNotReceive().DeleteProcessedEventsAsync(Arg.Any<DateTime>());
    }

    [Test]
    public void StartAsync_WhenReceiveExceptionWhileDeleting_ShouldLogException()
    {
        var eventStoreTablesCreator = Substitute.For<IEventStoreTablesCreator>();
        _serviceProvider.GetService(typeof(IEventStoreTablesCreator)).Returns(eventStoreTablesCreator);
        var stoppingTokenSource = new CancellationTokenSource();
        var cleanUpProcessedEventsService = new CleanUpProcessedEventsJob<TEventRepository, TEventBox>(
            scopeFactory: _serviceScopeFactory,
            settings: _settings,
            logger: _logger
        );
        _eventRepository
            .When(x => x.DeleteProcessedEventsAsync(Arg.Any<DateTime>()))
            .Do(_ =>
            {
                stoppingTokenSource.Cancel();
                throw new EventStoreException("Simulated exception");
            });

        _ = ExecuteBackgroundServiceAsync(cleanUpProcessedEventsService, stoppingTokenSource.Token);

        _logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>()!
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