using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Outbox.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class OutboxEventsExecutorJob(
    IServiceProvider services,
    IOutboxEventsExecutor outboxEventsExecutor,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsExecutorJob> logger)
    : BaseEventsExecutorJob(services, outboxEventsExecutor, settings.Outbox, logger)
{
    protected override ITableCreator GetTableCreatorService(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IOutboxRepository>();
    }
}