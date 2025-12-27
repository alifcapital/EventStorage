using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class InboxEventsProcessorJob(
    IServiceScopeFactory scopeFactory,
    IInboxEventsProcessor inboxEventsProcessor,
    InboxAndOutboxSettings settings,
    ILogger<InboxEventsProcessorJob> logger)
    : BaseEventsProcessorJob(scopeFactory, inboxEventsProcessor, settings.Inbox, logger);