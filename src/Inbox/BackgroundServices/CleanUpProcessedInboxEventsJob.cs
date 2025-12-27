using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.BackgroundServices;

internal class CleanUpProcessedInboxEventsJob(
    IServiceScopeFactory serviceScopeFactory,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsProcessorJob> logger)
    : BaseCleanUpProcessedEventsJob<IInboxRepository, InboxMessage>(serviceScopeFactory, settings.Inbox,
        logger);