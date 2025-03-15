using EventStorage.Models;
using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Managers;

public interface IOutboxEventManager : IDisposable
{
    /// <summary>
    /// First to collect all sending events to the memory and then store them to the database
    /// </summary>
    /// <param name="outboxEvent">Event to send</param>
    /// <param name="eventProvider">Provider type of sending event</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <typeparam name="TOutboxEvent">Event type that must implement from the ISendEvent</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Store<TOutboxEvent>(TOutboxEvent outboxEvent, EventProviderType eventProvider, 
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TOutboxEvent : IOutboxEvent;

    /// <summary>
    /// Clean all collected events from the memory
    /// </summary>
    public void CleanCollectedEvents();
}