using EventStorage.Outbox.BackgroundServices;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Outbox;

public class OutputTableCreatorServiceTests
{
    private IServiceProvider _serviceProvider;

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
    }

    #region StartAsync
    
    [Test]
    public async Task StartAsync_WithDefaultSettings_ShouldWork()
    {
        // Arrange
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(scope);
        var outboxRepository = Substitute.For<IOutboxRepository>();
        scope.ServiceProvider.GetService(typeof(IOutboxRepository)).Returns(outboxRepository);
        var eventsPublisherService = new OutboxTableCreatorService(_serviceProvider);

        // Act
        await eventsPublisherService.StartAsync(CancellationToken.None);

        // Assert
        outboxRepository.Received(1).CreateTableIfNotExists();
    }
    
    #endregion
}