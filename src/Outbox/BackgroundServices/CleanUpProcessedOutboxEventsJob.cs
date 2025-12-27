using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox.BackgroundServices;

internal class CleanUpProcessedOutboxEventsJob(
    IServiceScopeFactory scopeFactory,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsProcessorJob> logger)
    : BaseCleanUpProcessedEventsJob<IOutboxRepository, OutboxMessage>(scopeFactory, settings.Outbox,
        logger);