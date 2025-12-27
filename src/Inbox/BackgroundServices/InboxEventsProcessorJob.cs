using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Inbox.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class InboxEventsProcessorJob(
    IServiceProvider services,
    IInboxEventsProcessor inboxEventsProcessor,
    InboxAndOutboxSettings settings,
    ILogger<InboxEventsProcessorJob> logger)
    : BaseEventsProcessorJob(services, inboxEventsProcessor, settings.Inbox, logger)
{
}