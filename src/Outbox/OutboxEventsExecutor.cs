using System.Diagnostics;
using System.Text.Json;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Instrumentation.Trace;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Providers;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox;

internal class OutboxEventsExecutor : IOutboxEventsExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxEventsExecutor> _logger;
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

    public OutboxEventsExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<OutboxEventsExecutor>>();
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
    public async Task ExecuteUnprocessedEvents(CancellationToken stoppingToken)
    {
        await _singleExecutionLock.WaitAsync(stoppingToken);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var eventsToPublish = await repository.GetUnprocessedEventsAsync();
            if (eventsToPublish.Length == 0)
                return;

            stoppingToken.ThrowIfCancellationRequested();
            using var activity = CreateActivityForExecutingUnprocessedEventsIfEnabled(eventsToPublish.Length);
            
            var tasks = eventsToPublish.Select(async eventToPublish =>
            {
                await _semaphore.WaitAsync(stoppingToken);
                try
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await ExecuteEventPublisher(eventToPublish, scope.ServiceProvider, activity);
                }
                catch
                {
                    eventToPublish.Failed(_settings.TryCount, _settings.TryAfterMinutes);
                }
                finally
                {
                    _semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);

            await repository.UpdateEventsAsync(eventsToPublish);
        }
        finally
        {
            _singleExecutionLock.Release();
        }
    }

    private async Task ExecuteEventPublisher(IOutboxMessage outboxMessage, IServiceProvider serviceProvider, Activity parentActivity)
    {
        try
        {
            var publisherKey = GetPublisherKey(outboxMessage.EventName, outboxMessage.EventPath);
            if (_allPublishers.TryGetValue(publisherKey, out var publishers))
            {
                var eventPublishersToExecute = publishers.Values.Where(x => outboxMessage.Provider.Contains(x.ProviderType)).ToArray();
                if (eventPublishersToExecute.Length == 0)
                {
                    MarkEventAsFailedWhenThereIsNoPublisher();
                    return;
                }
                
                using var activity = CreateActivityForExecutingPublishersIfEnabled(outboxMessage, parentActivity);

                var firstEventInfo = publishers.First().Value;
                var jsonSerializerSetting = outboxMessage.GetJsonSerializer();
                var eventToPublish =
                    JsonSerializer.Deserialize(outboxMessage.Payload, firstEventInfo.EventType, jsonSerializerSetting)
                        as IOutboxEvent;
                if (firstEventInfo.HasHeaders && outboxMessage.Headers is not null)
                    ((IHasHeaders)eventToPublish)!.Headers =
                        JsonSerializer.Deserialize<Dictionary<string, string>>(outboxMessage.Headers);

                if (firstEventInfo.HasAdditionalData && outboxMessage.AdditionalData is not null)
                    ((IHasAdditionalData)eventToPublish)!.AdditionalData =
                        JsonSerializer.Deserialize<Dictionary<string, string>>(outboxMessage!.AdditionalData);

                foreach (var publisherInformation in eventPublishersToExecute)
                {
                    var eventHandlerSubscriber = serviceProvider.GetRequiredService(publisherInformation.EventPublisherType);

                    await ((Task)publisherInformation.PublishMethod.Invoke(eventHandlerSubscriber, [eventToPublish]))!;
                }
                outboxMessage.Processed();

                return;
            }

            MarkEventAsFailedWhenThereIsNoPublisher();
        }
        catch (Exception e)
        {
            var exception = new EventStoreException(e, $"Error while publishing event with ID: {outboxMessage.Id}");
            _logger.LogError(exception, exception.Message);
            throw exception;
        }
        return;

        void MarkEventAsFailedWhenThereIsNoPublisher()
        {
            outboxMessage.Failed(0, _settings.TryAfterMinutesIfEventNotFound);
            _logger.LogError(
                "The {EventType} outbox event with ID {EventId} requested to publish with {ProviderType} provider(s), but no publisher configured for this event.",
                outboxMessage.EventName, outboxMessage.Id, outboxMessage.Provider);
        }
    }

    #endregion

    #region Helper methods

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
    private Activity CreateActivityForExecutingPublishersIfEnabled(IOutboxMessage outboxMessage, Activity parentActivity)
    {
        if (!EventStorageTraceInstrumentation.IsEnabled) return null;

        var traceName = $"{EventStorageTraceInstrumentation.OutboxEventTag}: Executing a publisher(s) of the {outboxMessage.EventName} event";
        var traceParentId = parentActivity?.Id;
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, traceParentId, spanType: EventStorageTraceInstrumentation.OutboxEventTag);
        activity?.SetTag(EventStorageTraceInstrumentation.EventIdTag, outboxMessage.Id);

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

        var traceName = $"{EventStorageTraceInstrumentation.OutboxEventTag}: Executing {eventsCount} unprocessed event(s)";
        var activity = EventStorageTraceInstrumentation.StartActivity(traceName, ActivityKind.Server, spanType: EventStorageTraceInstrumentation.OutboxEventTag);

        return activity;
    }

    #endregion
}