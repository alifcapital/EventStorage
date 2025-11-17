using EventStorage.BackgroundServices;
using EventStorage.Configurations;
using EventStorage.Models;
using EventStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace EventStorage.Tests.Services;

internal class CleanUpProcessedEventsJob<TEventRepository, TEventBox> : BaseCleanUpProcessedEventsJob<TEventRepository, TEventBox>
    where TEventBox : class, IBaseMessageBox
    where TEventRepository : IBaseEventRepository<TEventBox>
{
    public CleanUpProcessedEventsJob(IServiceProvider services, InboxOrOutboxStructure settings, ILogger logger) : base(services, settings, logger)
    {
    }
}