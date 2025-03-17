using EventStorage.Inbox.Models;
using EventStorage.Models;

namespace EventStorage.Inbox.Managers;

public interface IInboxEventManager
{
    /// <summary>
    /// To store an inbox event to the database.
    /// </summary>
    /// <param name="inboxEvent">Event to send</param>
    /// <param name="eventProvider">Provider type of sending receivedEvent</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Store<TInboxEvent>(TInboxEvent inboxEvent, EventProviderType eventProvider, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TInboxEvent : IInboxEvent;

    /// <summary>
    /// To store an inbox event to the database.
    /// </summary>
    /// <param name="eventId">The id of event</param>
    /// <param name="eventTypeName">The type name of event</param>
    /// <param name="eventProvider">Provider type of sending event</param>
    /// <param name="payload">Payload of received event</param>
    /// <param name="headers">Headers of received event</param>
    /// <param name="additionalData">Additional data of received event if exists</param>
    /// <param name="eventPath">The full path (namespace) of the event type.</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Store(Guid eventId, string eventTypeName, EventProviderType eventProvider,
        string payload, string headers = null, string additionalData = null, string eventPath = null, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase);
}