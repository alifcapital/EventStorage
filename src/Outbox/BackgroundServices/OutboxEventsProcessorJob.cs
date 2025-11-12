using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Outbox.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class OutboxEventsProcessorJob(
    IServiceProvider services,
    IOutboxEventsProcessor outboxEventsProcessor,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsProcessorJob> logger)
    : BaseEventsProcessorJob(services, outboxEventsProcessor, settings.Outbox, logger)
{
    protected override ITableCreator GetTableCreatorService(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IOutboxRepository>();
    }
}