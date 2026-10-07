using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Instrumentation;
using EventStorage.Instrumentation.Trace;
using EventStorage.Management.Models;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Providers;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox;

internal class OutboxEventsProcessor : IOutboxEventsProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxEventsProcessor> _logger;
    private readonly InboxOrOutboxStructure _settings;

    /// <summary>
    /// To collect all publisher information. The key is the event name and provider name. The value is the publisher information.
    /// </summary>
    private readonly Dictionary<string, Dictionary<EventProviderType, EventPublisherInformation>> _allPublishers;

    /// <summary>
    /// To collect all event names with their publisher types which have publishers.
    /// </summary>
    private readonly Dictionary<string, string> _eventPublisherTypes;

    private const string PublisherMethodName = nameof(IEventPublisher.PublishAsync);
    private readonly SemaphoreSlim _singleExecutionLock = new(1, 1);
    private readonly SemaphoreSlim _semaphore;

    public OutboxEventsProcessor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<OutboxEventsProcessor>>();
        _settings = serviceProvider.GetRequiredService<InboxAndOutboxSettings>().Outbox;
        _allPublishers = new Dictionary<string, Dictionary<EventProviderType, EventPublisherInformation>>();
        _eventPublisherTypes = new Dictionary<string, string>();
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrency);
    }

    #region Register publisher

    /// <summary>
    /// Registers a publisher.
    /// </summary>
    /// <param name="typeOfOutboxEvent">Event type which we want to use to send</param>
    /// <param name="typeOfEventPublisher">Publisher type of the event which we want to publish event</param>
    /// <param name="providerType">Provider type of event publisher</param>
    /// <param name="hasHeaders">The event may have headers</param>
    /// <param name="hasAdditionalData">The event may have AdditionalData</param>
    /// <param name="isGlobalPublisher">Publisher of event is global publisher</param>
    public void AddPublisher(Type typeOfOutboxEvent, Type typeOfEventPublisher, EventProviderType providerType,
        bool hasHeaders, bool hasAdditionalData, bool isGlobalPublisher)
    {
        var eventFullName = GetPublisherKey(typeOfOutboxEvent.Name, typeOfOutboxEvent.Namespace);
        if (!_allPublishers.TryGetValue(eventFullName!, out var publishers))
        {
            publishers = new Dictionary<EventProviderType, EventPublisherInformation>();
            _allPublishers.Add(eventFullName, publishers);
        }

        var publishMethod = typeOfEventPublisher.GetMethod(PublisherMethodName);
        var eventPublisherInfo = new EventPublisherInformation
        {
            EventType = typeOfOutboxEvent,
            EventPublisherType = typeOfEventPublisher,
            PublishMethod = publishMethod,
            ProviderType = providerType.ToString(),
            HasHeaders = hasHeaders,
            HasAdditionalData = hasAdditionalData,
            IsGlobalPublisher = isGlobalPublisher
        };

        publishers[providerType] = eventPublisherInfo;

        CacheEventProviderTypes(eventFullName, publishers.Keys);
    }

    #endregion

    #region Execute unprocessed events

    /// <summary>
    /// The method to execute unprocessed events. We are locking the logic to prevent re-entry into the method while processing is ongoing.
    /// </summary>
    public async Task ExecuteUnprocessedEventsAsync(CancellationToken stoppingToken)
    {
        await _singleExecutionLock.WaitAsync(stoppingToken);
        try
        {
            var processingTimeoutAt = _settings.GetProcessingTimeoutAt();
            // The same repository is used for locking and unlocking the events of the batch.
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var eventsToPublish = await repository.LockUnprocessedEventsAsync(_settings.MaxEventsToFetch,
                processingTimeoutAt, stoppingToken);

            if (eventsToPublish.Length == 0)
                return;

            var handledEventIds = new ConcurrentDictionary<Guid, bool>();
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                using var activity = CreateActivityForExecutingUnprocessedEventsIfEnabled(eventsToPublish.Length);

                var tasks = eventsToPublish.Select(async eventToPublish =>
                {
                    await _semaphore.WaitAsync(stoppingToken);
                    try
                    {
                        var result = await ProcessSingleEventAsync(eventToPublish, manualRequest: null, activity,
                            stoppingToken);
                        if (result.Status != EventActionResultStatus.InvalidState)
                            handledEventIds.TryAdd(eventToPublish.Id, true);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks);
            }
            finally
            {
                var notHandledEvents = eventsToPublish.Where(e => !handledEventIds.ContainsKey(e.Id)).ToArray();
                await UnlockEventsAsync(repository, notHandledEvents, processingTimeoutAt);
            }
        }
        finally
        {
            _singleExecutionLock.Release();
        }
    }

    public Task<EventActionResult> ProcessSingleEventAsync(OutboxMessage message, EventActionRequest manualRequest,
        CancellationToken cancellationToken)
    {
        return ProcessSingleEventAsync(message, manualRequest, parentActivity: Activity.Current, cancellationToken);
    }
    
    #endregion
    
    #region Helper methods

    /// <summary>
    /// Process single event which is already locked by the caller. The event has its original status which is read
    /// while locking it. The event is unlocked when its result is stored.
    /// Each event is processed in a separate scope to avoid conflicts in scoped services like DbContext.
    /// </summary>
    private async Task<EventActionResult> ProcessSingleEventAsync(OutboxMessage message,
        EventActionRequest manualRequest, Activity parentActivity, CancellationToken cancellationToken)
    {
        var force = manualRequest?.Force == true;
        if (!EventStatusTransitions.CanBeExecuted(message.Status, force))
        {
            _logger.LogDebug("The outbox event with id {EventId} has the {Status} status. Skipping execution.",
                message.Id, message.Status);
            return EventActionResult.InvalidState(
                $"The outbox event with the {message.Status} status cannot be executed{(message.Status == EventStatus.Processed ? " without the force option" : string.Empty)}.");
        }

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        cancellationToken.ThrowIfCancellationRequested();
        var performedBy = manualRequest?.PerformedBy;
        var comment = manualRequest?.Comment;
        try
        {
            var isSuccessfullyExecuted = await ExecuteEventPublisher(message, scope.ServiceProvider, parentActivity);
            if (isSuccessfullyExecuted)
                message.Processed(performedBy, comment);
            else
                message.EventProcessorNotFound(_settings.TryAfterMinutesIfEventNotFound,$"No publisher configured for the {message.EventName} event with the {message.Provider} provider(s).", performedBy);
        }
        catch (Exception e)
        {
            message.Failed(_settings.TryCount, _settings.TryAfterSeconds, _settings.TryAfterMinutesIfTryCountExceeded,
                e.ToFailureReason(_settings), performedBy, comment);
        }
        finally
        {
            await repository.UpdateEventAsync(message, cancellationToken); 
        }

        return message.Status == EventStatus.Processed
            ? EventActionResult.Success()
            : EventActionResult.Failed(message.FailureReason);
    }

    /// <summary>
    /// Unlocks the events which are not stored while processing, for example when the processing is cancelled,
    /// so they can be processed again without waiting for their processing timeout.
    /// </summary>
    /// <param name="repository">The repository which locked the events.</param>
    /// <param name="events">The events to unlock.</param>
    /// <param name="processingTimeoutAt">The processing timeout which the events were locked with.</param>
    private async Task UnlockEventsAsync(IOutboxRepository repository, OutboxMessage[] events,
        DateTime processingTimeoutAt)
    {
        if (events.Length == 0)
            return;

        try
        {
            //TODO: Need to check why do we need this instead of just updating event with its status.
            await repository.UnlockEventsAsync(events, processingTimeoutAt, CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while unlocking {EventsCount} outbox event(s). They will be processed again after their processing timeout.",
                events.Length);
        }
    }

    /// <summary>
    /// Executes all publishers of the outbox event.
    /// </summary>
    /// <returns>Returns true if the publishers are executed, or false if there is no publisher for the event.</returns>
    private async Task<bool> ExecuteEventPublisher(IOutboxMessage outboxMessage, IServiceProvider serviceProvider,
        Activity parentActivity)
    {
        try
        {
            var publisherKey = GetPublisherKey(outboxMessage.EventName, outboxMessage.EventPath);
            var eventPublishersToExecute = _allPublishers.TryGetValue(publisherKey, out var publishers)
                ? publishers.Values.Where(x => outboxMessage.Provider.Contains(x.ProviderType)).ToArray()
                : [];
            if (eventPublishersToExecute.Length == 0)
            {
                _logger.LogError(
                    "The {EventType} outbox event with ID {EventId} requested to publish with {ProviderType} provider(s), but no publisher configured for this event.",
                    outboxMessage.EventName, outboxMessage.Id, outboxMessage.Provider);
                return false;
            }

            _logger.LogDebug("{StorageType}: Executing publishers of the event '{EventName}' (ID: {MessageId})",
                EventStorageInvestigationTagNames.OutboxEventTag, outboxMessage.EventName, outboxMessage.Id);
            using var activity = CreateActivityForExecutingPublishersIfEnabled(outboxMessage, parentActivity);

            var eventToPublish = LoadOutboxEvent(outboxMessage, publishers!.First().Value);
            foreach (var publisherInformation in eventPublishersToExecute)
            {
                var eventHandlerSubscriber =
                    serviceProvider.GetRequiredService(publisherInformation.EventPublisherType);

                await ((Task)publisherInformation.PublishMethod.Invoke(eventHandlerSubscriber, [eventToPublish]))!;
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while publishing event with ID: {EventId}", outboxMessage.Id);
            throw;
        }
    }

    /// <summary>
    /// Load the event to publish from the outbox message.
    /// </summary>
    /// <param name="message">The outbox message.</param>
    /// <param name="eventPublisherInformation">The publisher information of the event.</param>
    /// <returns>Loaded instance of event</returns>
    private static IOutboxEvent LoadOutboxEvent(IOutboxMessage message,
        EventPublisherInformation eventPublisherInformation)
    {
        try
        {
            var jsonSerializerSetting = message.GetJsonSerializer();
            var eventToPublish =
                JsonSerializer.Deserialize(message.Payload, eventPublisherInformation.EventType, jsonSerializerSetting)
                    as IOutboxEvent;
            if (eventPublisherInformation.HasHeaders && message.Headers is not null)
                ((IHasHeaders)eventToPublish)!.Headers =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers);

            if (eventPublisherInformation.HasAdditionalData && message.AdditionalData is not null)
                ((IHasAdditionalData)eventToPublish)!.AdditionalData =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(message.AdditionalData);

            return eventToPublish;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            throw new EventStoreException(e,
                $"Could not deserialize the outbox event to the {eventPublisherInformation.EventType.FullName} type.");
        }
    }

    /// <summary>
    /// Cache the event publisher types.
    /// </summary>
    /// <param name="eventFullName">Full type name of outbox event</param>
    /// <param name="providerTypes">Event provider types which event has publisher for them</param>
    private void CacheEventProviderTypes(string eventFullName, IEnumerable<EventProviderType> providerTypes)
    {
        _eventPublisherTypes[eventFullName] = string.Join(",", providerTypes.Select(x => x.ToString()));
    }

    public string GetEventPublisherTypes<TOutboxEvent>(TOutboxEvent outboxEvent)
        where TOutboxEvent : IOutboxEvent
    {
        var eventFullName = outboxEvent.GetType().FullName;
        return _eventPublisherTypes.GetValueOrDefault(eventFullName);
    }

    internal string GetPublisherKey(string eventName, string eventNamespace)
    {
        return $"{eventNamespace}.{eventName}";
    }

    /// <summary>
    /// Creates an activity for executing publishers of the outbox event if tracing is enabled.
    /// </summary>
    /// <param name="outboxMessage">The outbox message for which the activity is created.</param>
    /// <param name="parentActivity">The parent activity to link to, if available.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateActivityForExecutingPublishersIfEnabled(IOutboxMessage outboxMessage,
        Activity parentActivity)
    {
        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        var traceName =
            $"{EventStorageInvestigationTagNames.InboxEventTag}: Executing publishers of the {outboxMessage.EventName} event";
        var traceParentId = parentActivity?.Id;
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, traceParentId,
            spanType: EventStorageInvestigationTagNames.OutboxEventTag);
        activity?.AttachEventInfo(outboxMessage);

        return activity;
    }

    /// <summary>
    /// Creates an activity for executing publishers of the unprocessed events if tracing is enabled.
    /// </summary>
    /// <param name="eventsCount">The count of unprocessed events being executed.</param>
    /// <returns>Newly created activity or null if tracing is not enabled.</returns>
    private Activity CreateActivityForExecutingUnprocessedEventsIfEnabled(int eventsCount)
    {
        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        var traceName =
            $"{EventStorageInvestigationTagNames.OutboxEventTag}: Executing {eventsCount} unprocessed event(s)";
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server,
            spanType: EventStorageInvestigationTagNames.OutboxEventTag);

        return activity;
    }

    #endregion
}