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

internal class OutboxEventsExecutor : IOutboxEventsExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxEventsExecutor> _logger;
    private readonly InboxOrOutboxStructure _settings;

    private readonly Dictionary<string, EventPublisherInformation> _publishers;

    private const string PublisherMethodName = nameof(IEventPublisher.PublishAsync);
    private readonly SemaphoreSlim _singleExecutionLock = new(1, 1);
    private readonly SemaphoreSlim _semaphore;

    public OutboxEventsExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<OutboxEventsExecutor>>();
        _settings = serviceProvider.GetRequiredService<InboxAndOutboxSettings>().Outbox;
        _publishers = new Dictionary<string, EventPublisherInformation>();
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrency);
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
        var publishMethod = typeOfEventPublisher.GetMethod(PublisherMethodName);
        var publisherInformation = new EventPublisherInformation
        {
            EventType = typeOfEventSender,
            EventPublisherType = typeOfEventPublisher,
            PublishMethod = publishMethod,
            ProviderType = providerName,
            HasHeaders = hasHeaders,
            HasAdditionalData = hasAdditionalData,
            IsGlobalPublisher = isGlobalPublisher
        };
        _publishers[publisherKey] = publisherInformation;
    }

    internal string GetPublisherKey(string eventName, string providerName)
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

    private async Task ExecuteEventPublisher(IOutboxMessage message, IServiceScope serviceScope)
    {
        try
        {
            var publisherKey = GetPublisherKey(message.EventName, message.Provider);
            if (_publishers.TryGetValue(publisherKey, out var publisherInformation))
            {
                _logger.LogTrace("Executing the {EventType} outbox event with ID {EventId} to publish.",
                    message.EventName, message.Id);

                var jsonSerializerSetting = message.GetJsonSerializer();
                var eventToPublish = JsonSerializer.Deserialize(message.Payload, publisherInformation.EventType, jsonSerializerSetting) as IOutboxEvent;
                if (publisherInformation.HasHeaders && message.Headers is not null)
                    ((IHasHeaders)eventToPublish)!.Headers =
                        JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers);

                if (publisherInformation.HasAdditionalData && message.AdditionalData is not null)
                    ((IHasAdditionalData)eventToPublish)!.AdditionalData =
                        JsonSerializer.Deserialize<Dictionary<string, string>>(message!.AdditionalData);

                var eventHandlerSubscriber = serviceScope.ServiceProvider.GetRequiredService(publisherInformation.EventPublisherType);

                await ((Task)publisherInformation.PublishMethod.Invoke(eventHandlerSubscriber, [eventToPublish, message.EventPath]))!;
                message.Processed();

                return;
            }
            
            message.Failed(0, _settings.TryAfterMinutesIfEventNotFound);
            _logger.LogError(
                "The {EventType} outbox event with ID {EventId} requested to publish with {ProviderType} provider, but no publisher configured for this event.",
                message.EventName, message.Id, message.Provider);
        }
        catch (Exception e)
        {
            var exception = new EventStoreException(e, $"Error while publishing event with ID: {message.Id}");
            _logger.LogError(exception, exception.Message);
            throw exception;
        }
    }
}