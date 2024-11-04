using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EventStorage.Outbox.BackgroundServices;

internal class OutboxTableCreatorService : BackgroundService
{
    private readonly IServiceProvider _services;

    public OutboxTableCreatorService(IServiceProvider services)
    {
        _services = services;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        outboxRepository.CreateTableIfNotExists();

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}