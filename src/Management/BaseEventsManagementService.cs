using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Management.Models;
using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Services;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Management;

/// <summary>
/// The base service for viewing and managing inbox/outbox events. Every action that changes an event takes the same
/// distributed lock as the processor, so it never conflict with the processing of the event.
/// </summary>
/// <param name="serviceProvider">The service provider to resolve the services, which are registered only when the functionality is enabled.</param>
/// <param name="settings">The inbox or outbox settings.</param>
/// <param name="functionalityName">The name of the functionality: Inbox or Outbox.</param>
/// <param name="logger">The logger instance.</param>
/// <typeparam name="TRepository">The type of the inbox or outbox repository.</typeparam>
/// <typeparam name="TProcessor">The type of the inbox or outbox processor.</typeparam>
/// <typeparam name="TMessage">The type of the inbox or outbox message.</typeparam>
internal abstract class BaseEventsManagementService<TRepository, TProcessor, TMessage>(
    IServiceProvider serviceProvider,
    InboxOrOutboxStructure settings,
    string functionalityName,
    ILogger logger)
    : IEventsManagementService
    where TRepository : IBaseEventRepository<TMessage>
    where TProcessor : IEventsProcessor<TMessage>
    where TMessage : class, IBaseMessageBox
{
    private InboxOrOutboxStructure Settings { get; } = settings;

    private TRepository Repository => serviceProvider.GetRequiredService<TRepository>();

    private TProcessor Processor => serviceProvider.GetRequiredService<TProcessor>();

    private IDistributedLockProvider LockProvider =>
        serviceProvider.GetRequiredKeyedService<IDistributedLockProvider>(functionalityName);

    #region Get events

    public async Task<EventDetails> GetEventByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureIsEnabled();
        cancellationToken.ThrowIfCancellationRequested();

        var message = await Repository.GetEventByIdAsync(id, cancellationToken);
        return message.ToEventDetails();
    }

    public async Task<EventPagedList<EventDetails>> GetEventsAsync(EventsFilter filter,
        CancellationToken cancellationToken)
    {
        EnsureIsEnabled();
        cancellationToken.ThrowIfCancellationRequested();

        filter ??= new EventsFilter();
        filter = filter with
        {
            PageIndex = Math.Max(filter.PageIndex, 1),
            PageSize = filter.PageSize < 1 ? EventsFilter.DefaultPageSize : filter.PageSize
        };

        var messages = await Repository.GetEventsAsync(filter, cancellationToken);
        return messages.MapItems(m => m.ToEventDetails());
    }

    #endregion

    #region ExecuteAsync

    public async Task<EventActionResult> ExecuteAsync(Guid id, EventActionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureIsEnabled();
        request ??= new EventActionRequest();

        var message = await Repository.GetEventByIdAsync(id, cancellationToken);
        if (message is null)
            return EventActionResult.NotFound(id);

        var result = await Processor.ProcessSingleEventAsync(message, request, cancellationToken);
        LogActionResult("executed", id, request, result);

        return result;
    }

    #endregion

    #region Status

    public Task<EventActionResult> RescheduleAsync(Guid id, DateTime tryAfterAt, EventActionRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new EventActionRequest();
        return ChangeStatusAsync(id, request, "rescheduled", EventStatusTransitions.CanBeRescheduled,
            message => message.Rescheduled(tryAfterAt, request.PerformedBy, request.Comment), cancellationToken);
    }

    public Task<EventActionResult> RejectAsync(Guid id, EventActionRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new EventActionRequest();
        return ChangeStatusAsync(id, request, "rejected", EventStatusTransitions.CanBeRejected,
            message => message.Rejected(request.PerformedBy, request.Comment), cancellationToken);
    }

    public Task<EventActionResult> MarkAsProcessedAsync(Guid id, EventActionRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new EventActionRequest();
        return ChangeStatusAsync(id, request, "marked as processed", EventStatusTransitions.CanBeMarkedAsProcessed,
            message => message.Processed(request.PerformedBy, request.Comment), cancellationToken);
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Changes the status of the event under its distributed lock if the current status allows it.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="request">The information of the action.</param>
    /// <param name="actionName">The name of the action for the messages.</param>
    /// <param name="canBeChanged">Checks whether the current status of the event allows the action.</param>
    /// <param name="changeStatus">Changes the status of the event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task<EventActionResult> ChangeStatusAsync(Guid id, EventActionRequest request, string actionName,
        Func<EventStatus, bool> canBeChanged, Action<TMessage> changeStatus, CancellationToken cancellationToken)
    {
        EnsureIsEnabled();

        var lockName = FunctionalityNames.GetEventLockName(functionalityName, id);
        await using var distributedLock =
            await LockProvider.TryAcquireLockAsync(lockName, cancellationToken: cancellationToken);
        if (distributedLock is null)
            return EventActionResult.AlreadyProcessing(id);

        var message = await Repository.GetEventByIdAsync(id, cancellationToken);
        if (message is null)
            return EventActionResult.NotFound(id);

        if (!canBeChanged(message.Status))
            return EventActionResult.InvalidState(
                $"The {functionalityName.ToLower()} event with the {message.Status} status cannot be {actionName}.");

        changeStatus(message);
        await Repository.UpdateEventAsync(message, cancellationToken);

        var result = EventActionResult.Success();
        LogActionResult(actionName, id, request, result);

        return result;
    }

    /// <summary>
    /// Throws an exception if the functionality is not enabled, since its services are not registered in that case.
    /// </summary>
    private void EnsureIsEnabled()
    {
        if (!Settings.IsEnabled)
            throw new EventStoreException(
                $"The {functionalityName} functionality is not enabled, so its events cannot be managed.");
    }

    private void LogActionResult(string actionName, Guid id, EventActionRequest request, EventActionResult result)
    {
        logger.LogInformation(
            "{StorageType}: The event with ID {EventId} is {ActionName} by {PerformedBy} with the {ResultStatus} result. Comment: {Comment}",
            functionalityName, id, actionName, request.PerformedBy, result.Status, request.Comment);
    }

    #endregion
}
