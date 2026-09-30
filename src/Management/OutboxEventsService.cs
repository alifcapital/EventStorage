using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Outbox;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Management;

internal class OutboxEventsService(
    IServiceProvider serviceProvider,
    InboxAndOutboxSettings settings,
    ILogger<OutboxEventsService> logger)
    : BaseEventsManagementService<IOutboxRepository, IOutboxEventsProcessor, OutboxMessage>(serviceProvider,
        settings.Outbox, FunctionalityNames.Outbox, logger), IOutboxEventsService;
