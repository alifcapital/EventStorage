using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Tests.Services;

internal class CleanUpProcessedEventsJob<TEventRepository, TEventBox>(
    IServiceScopeFactory scopeFactory,
    InboxOrOutboxStructure settings,
    ILogger logger)
    : BaseCleanUpProcessedEventsJob<TEventRepository, TEventBox>(scopeFactory, settings, logger)
    where TEventBox : class, IBaseMessageBox
    where TEventRepository : IBaseEventRepository<TEventBox>;