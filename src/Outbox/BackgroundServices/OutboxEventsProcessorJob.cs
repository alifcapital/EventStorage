using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class OutboxEventsProcessorJob(
    IServiceScopeFactory scopeFactory,
    IOutboxEventsProcessor outboxEventsProcessor,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsProcessorJob> logger)
    : BaseEventsProcessorJob(scopeFactory, outboxEventsProcessor, settings.Outbox, logger);