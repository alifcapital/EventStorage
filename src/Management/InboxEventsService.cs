using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Inbox;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Management;

internal class InboxEventsService(
    IServiceProvider serviceProvider,
    InboxAndOutboxSettings settings,
    ILogger<InboxEventsService> logger)
    : BaseEventsManagementService<IInboxRepository, IInboxEventsProcessor, InboxMessage>(serviceProvider,
        settings.Inbox, FunctionalityNames.Inbox, logger), IInboxEventsService;
