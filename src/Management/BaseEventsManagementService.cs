using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Management.Models;
using EventStorage.Models;
using EventStorage.Repositories;
using EventStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Management;

/// <summary>
/// The base service for viewing and managing inbox/outbox events. Every action that changes an event locks the event
/// the same way as the processor does, so it never conflicts with the processing of the event.
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

    #region Get events

    public async Task<EventDetails> GetEventByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureIsEnabled();
        cancellationToken.ThrowIfCancellationRequested();

        var message = await Repository.GetEventByIdAsync(id, cancellationToken);
        return message.ToEventDetails();
    }

    public async Task<EventPagedList<EventSummary>> GetEventsAsync(EventsFilter filter,
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

        return await Repository.GetEventsAsync(filter, cancellationToken);
    }

    public string[] GetProviderTypes()
    {
        EnsureIsEnabled();

        return Enum.GetNames<EventProviderType>();
    }

    #endregion

    #region ExecuteAsync

    public async Task<EventActionResult> ExecuteAsync(Guid id, EventActionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureIsEnabled();
        request ??= new EventActionRequest();

        var result = await ExecuteUnderEventLockAsync(id,
            message => Processor.ProcessSingleEventAsync(message, request, cancellationToken), cancellationToken);
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
    /// Changes the status of the event under its lock if the current status allows it.
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

        var result = await ExecuteUnderEventLockAsync(id, async message =>
        {
            if (!canBeChanged(message.Status))
                return EventActionResult.InvalidState(
                    $"The {functionalityName.ToLower()} event with the {message.Status} status cannot be {actionName}.");

            changeStatus(message);
            await Repository.UpdateEventAsync(message, cancellationToken);

            return EventActionResult.Success();
        }, cancellationToken);

        if (result.IsSuccess)
            LogActionResult(actionName, id, request, result);

        return result;
    }

    /// <summary>
    /// Locks the event the same way as the processor does (marks it as "Processing"), so the action never conflicts
    /// with the processing of the event, executes the action with the locked event and restores the original status of
    /// the event if the action did not store it.
    /// </summary>
    /// <param name="id">The id of the event.</param>
    /// <param name="action">The action to execute with the locked event, which has its original status. The event is
    /// considered stored unless the action returns <see cref="EventActionResultStatus.InvalidState"/> or throws.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task<EventActionResult> ExecuteUnderEventLockAsync(Guid id,
        Func<TMessage, Task<EventActionResult>> action, CancellationToken cancellationToken)
    {
        //TODO: Even the event cannot be processed we are updating status of that. Need to review
        var processingTimeoutAt = Settings.GetProcessingTimeoutAt();
        var message = await Repository.LockEventByIdAsync(id, processingTimeoutAt, cancellationToken);
        if (message is null)
        {
            var status = await Repository.GetEventStatusByIdAsync(id, cancellationToken);
            return status is null ? EventActionResult.NotFound(id) : EventActionResult.AlreadyProcessing(id);
        }

        var isStored = false;
        try
        {
            var result = await action(message);
            isStored = result.Status != EventActionResultStatus.InvalidState;

            return result;
        }
        finally
        {
            if (!isStored)
                await Repository.UnlockEventsAsync([message], processingTimeoutAt, CancellationToken.None);
        }
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
