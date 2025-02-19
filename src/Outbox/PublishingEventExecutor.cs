using System.Text.Json;
using EventStorage.Configurations;
using EventStorage.Exceptions;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Providers;
using EventStorage.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStorage.Outbox;

internal class PublishingEventExecutor : IPublishingEventExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PublishingEventExecutor> _logger;
    private readonly InboxOrOutboxStructure _settings;

    private readonly Dictionary<string, (Type typeOfEvent, Type typeOfPublisher, string provider, bool hasHeaders, bool
        hasAdditionalData, bool isGlobalPublisher)> _publishers;

    private const string PublisherMethodName = nameof(IEventPublisher.PublishAsync);
    private readonly SemaphoreSlim _singleExecutionLock = new(1, 1);
    private readonly SemaphoreSlim _semaphore;

    public PublishingEventExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<PublishingEventExecutor>>();
        _settings = serviceProvider.GetRequiredService<InboxAndOutboxSettings>().Outbox;
        _publishers = new();
        _semaphore = new(_settings.MaxConcurrency);
    }

    /// <summary>
    /// Registers a publisher 
    /// </summary>
    /// <param name="typeOfEventSender">Event type which we want to use to send</param>
    /// <param name="typeOfEventPublisher">Publisher type of the event which we want to publish event</param>
    /// <param name="providerType">Provider type of event publisher</param>
    /// <param name="hasHeaders">The event may have headers</param>
    /// <param name="hasAdditionalData">The event may have AdditionalData</param>
    /// <param name="isGlobalPublisher">Publisher of event is global publisher</param>
    public void AddPublisher(Type typeOfEventSender, Type typeOfEventPublisher, EventProviderType providerType,
        bool hasHeaders, bool hasAdditionalData, bool isGlobalPublisher)
    {
        var providerName = providerType.ToString();
        var publisherKey = GetPublisherKey(typeOfEventSender.Name, providerName);
        _publishers[publisherKey] = (typeOfEventSender, typeOfEventPublisher, providerName, hasHeaders,
            hasAdditionalData, isGlobalPublisher);
    }

    private string GetPublisherKey(string eventName, string providerName)
    {
        return $"{eventName}-{providerName}";
    }

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
            var tasks = eventsToPublish.Select(async eventToPublish =>
            {
                await _semaphore.WaitAsync(stoppingToken);
                try
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await ExecuteEventPublisher(eventToPublish, scope);
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

    private async Task ExecuteEventPublisher(IOutboxEvent @event, IServiceScope serviceScope)
    {
        try
        {
            var publisherKey = GetPublisherKey(@event.EventName, @event.Provider);
            if (_publishers.TryGetValue(publisherKey,
                    out (Type typeOfEvent, Type typeOfPublisher, string provider, bool hasHeaders, bool
                    hasAdditionalData, bool isGlobalPublisher) info))
            {
                _logger.LogTrace("Executing the {EventType} outbox event with ID {EventId} to publish.",
                    @event.EventName, @event.Id);

                var jsonSerializerSetting = @event.GetJsonSerializer();
                var eventToPublish = JsonSerializer.Deserialize(@event.Payload, info.typeOfEvent, jsonSerializerSetting) as ISendEvent;
                if (info.hasHeaders && @event.Headers is not null)
                    ((IHasHeaders)eventToPublish)!.Headers =
                        JsonSerializer.Deserialize<Dictionary<string, string>>(@event.Headers);

                if (info.hasAdditionalData && @event.AdditionalData is not null)
                    ((IHasAdditionalData)eventToPublish)!.AdditionalData =
                        JsonSerializer.Deserialize<Dictionary<string, string>>(@event!.AdditionalData);

                var eventHandlerSubscriber = serviceScope.ServiceProvider.GetRequiredService(info.typeOfPublisher);

                var publisherMethod = info.typeOfPublisher.GetMethod(PublisherMethodName);
                await (Task)publisherMethod.Invoke(eventHandlerSubscriber,
                    [eventToPublish, @event.EventPath]);
                @event.Processed();

                return;
            }
            
            @event.Failed(0, _settings.TryAfterMinutesIfEventNotFound);
            _logger.LogError(
                "The {EventType} outbox event with ID {EventId} requested to publish with {ProviderType} provider, but no publisher configured for this event.",
                @event.EventName, @event.Id, @event.Provider);
        }
        catch (Exception e)
        {
            var exception = new EventStoreException(e, $"Error while publishing event with ID: {@event.Id}");
            _logger.LogError(exception, exception.Message);
            throw exception;
        }
    }
}