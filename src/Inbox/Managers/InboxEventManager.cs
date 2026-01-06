using System.Text.Json;
using EventStorage.Exceptions;
using EventStorage.Extensions;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Outbox.Models;
using Microsoft.Extensions.Logging;

namespace EventStorage.Inbox.Managers;

internal class InboxEventManager(ILogger<InboxEventManager> logger, IInboxRepository repository = null)
    : IInboxEventManager
{
    public bool Store<TInboxEvent>(TInboxEvent inboxEvent, EventProviderType eventProvider,
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TInboxEvent : IInboxEvent
    {
        var eventType = inboxEvent.GetType();
        try
        {
            string eventHeaders = null;
            if (inboxEvent is IHasHeaders hasHeaders)
            {
                if (hasHeaders.Headers?.Count > 0)
                    eventHeaders = JsonSerializer.Serialize(hasHeaders.Headers);
                hasHeaders.Headers = null;
            }

            string eventAdditionalData = null;
            if (inboxEvent is IHasAdditionalData hasAdditionalData)
            {
                if (hasAdditionalData.AdditionalData?.Count > 0)
                    eventAdditionalData = JsonSerializer.Serialize(hasAdditionalData.AdditionalData);
                hasAdditionalData.AdditionalData = null;
            }

            var eventPayload = inboxEvent.SerializeToJson();

            return Store(inboxEvent.EventId, eventType.Name, eventProvider, eventPayload, eventHeaders,
                eventAdditionalData, eventType.Namespace, namingPolicyType);
        }
        catch (Exception e) when (e is not EventStoreException)
        {
            logger.LogError(e,
                "Error while serializing data of the {EventType} received event with the {EventId} id to store to the the table of Inbox.",
                eventType.Name, inboxEvent.EventId);
            throw;
        }
    }

    public bool Store(Guid eventId, string eventTypeName, EventProviderType eventProvider,
        string payload, string headers, string additionalData = null, string eventPath = null,
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
    {
        try
        {
            if (repository is null)
            {
                logger.LogWarning(
                    "The system trying to store an event into the Inbox table, but the Inbox functionality is not enabled.");
                return false;
            }

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
}