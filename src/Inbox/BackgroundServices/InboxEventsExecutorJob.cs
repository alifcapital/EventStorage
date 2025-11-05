using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Inbox.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class InboxEventsExecutorJob(
    IServiceProvider services,
    IInboxEventsExecutor inboxEventsExecutor,
    InboxAndOutboxSettings settings,
    ILogger<InboxEventsExecutorJob> logger)
    : BaseEventsExecutorJob(services, inboxEventsExecutor, settings.Inbox, logger)
{
    protected override ITableCreator GetTableCreatorService(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IInboxRepository>();
    }
}