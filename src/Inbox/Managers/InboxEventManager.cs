using System.Text.Json;
using System.Text.Json.Serialization;
using EventStorage.Exceptions;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.Managers;

internal class InboxEventManager(IInboxRepository repository, ILogger<InboxEventManager> logger)
    : IInboxEventManager
{
    public bool Store<TInboxEvent>(TInboxEvent inboxEvent, string eventPath, EventProviderType eventProvider, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TInboxEvent : IInboxEvent
    {
        var receivedEventType = inboxEvent.GetType().Name;
        try
        {
            string headers = null;
            if (inboxEvent is IHasHeaders hasHeaders)
            {
                if (hasHeaders.Headers?.Any() == true)
                    headers = SerializeHeadersData(hasHeaders.Headers);
                hasHeaders.Headers = null;
            }

            string additionalData = null;
            if (inboxEvent is IHasAdditionalData hasAdditionalData)
            {
                if (hasAdditionalData.AdditionalData?.Any() == true)
                    additionalData = SerializeHeadersData(hasAdditionalData.AdditionalData);
                hasAdditionalData.AdditionalData = null;
            }

            var payload = SerializeData(inboxEvent);

            return Store(inboxEvent.EventId, receivedEventType, eventPath, eventProvider, payload, headers,
                additionalData, namingPolicyType);
        }
        catch (Exception e) when (e is not EventStoreException)
        {
            logger.LogError(e,
                "Error while serializing data of the {EventType} received event with the {EventId} id to store to the the table of Inbox.",
                receivedEventType, inboxEvent.EventId);
            throw;
        }

        static string SerializeHeadersData<TValue>(TValue data)
        {
            return JsonSerializer.Serialize(data);
        }
    }

    public bool Store<TReceiveEvent>(TReceiveEvent inboxEvent, string eventPath, EventProviderType eventProvider,
        string headers, string additionalData = null, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TReceiveEvent : IInboxEvent
    {
        var receivedEventType = inboxEvent.GetType().Name;
        try
        {
            var payload = SerializeData(inboxEvent);

            return Store(inboxEvent.EventId, receivedEventType, eventPath, eventProvider, payload, headers,
                additionalData, namingPolicyType);
        }
        catch (Exception e) when (e is not EventStoreException)
        {
            logger.LogError(e,
                "Error while serializing data of the {EventType} received event with the {EventId} id to store to the the table of Inbox.",
                receivedEventType, inboxEvent.EventId);
            throw;
        }
    }

    public bool Store(Guid eventId, string eventTypeName, string eventPath, EventProviderType eventProvider,
        string payload, string headers, string additionalData = null, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
    {
        try
        {
            var inboxEvent = new InboxMessage
            {
                Id = eventId,
                Provider = eventProvider.ToString(),
                EventName = eventTypeName,
                EventPath = eventPath,
                Payload = payload,
                NamingPolicyType = namingPolicyType.ToString(),
                Headers = headers,
                AdditionalData = additionalData
            };

            var successfullyInserted = repository.InsertEvent(inboxEvent);
            if (!successfullyInserted)
                logger.LogWarning(
                    "The {EventType} event type with the {EventId} id is already added to the table of Inbox.",
                    eventTypeName, eventId);

            return successfullyInserted;
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Error while entering the {EventType} event type with the {EventId} id to the table of Inbox.",
                eventTypeName, eventId);
            throw;
        }
    }

    private static readonly JsonSerializerOptions SerializerSettings =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static string SerializeData<TValue>(TValue data)
    {
        return JsonSerializer.Serialize(data, data.GetType(), SerializerSettings);
    }
}