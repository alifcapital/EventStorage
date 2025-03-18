using EventStorage.Models;
using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Managers;

/// <summary>
/// The main interface that will provide the ability to collect publishing events to the memory and then store them to the database, or just store them to the database immediately.
/// </summary>
public interface IOutboxEventManager : IDisposable
{
    /// <summary>
    /// The first to collect all sending events to the memory and then store them to the database.
    /// The event provider will be identified based on existing event publishers and execute a publisher of all of them. But if there is no publisher, it will just add an error log and return false.
    /// </summary>
    /// <param name="outboxEvent">Event to send</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <typeparam name="TOutboxEvent">Event type that must implement from the ISendEvent</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated or if event does not have publisher. It can throw an exception if something goes wrong.</returns>
    public bool Collect<TOutboxEvent>(TOutboxEvent outboxEvent, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TOutboxEvent : IOutboxEvent;

    /// <summary>
    /// The first to collect all sending events to the memory and then store them to the database.
    /// </summary>
    /// <param name="outboxEvent">Event to send</param>
    /// <param name="eventProvider">Provider type of sending event</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <typeparam name="TOutboxEvent">Event type that must implement from the ISendEvent</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Collect<TOutboxEvent>(TOutboxEvent outboxEvent, EventProviderType eventProvider, 
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TOutboxEvent : IOutboxEvent;

    /// <summary>
    /// Stores the event to the database immediately.
    /// The event provider will be identified based on existing event publishers and execute a publisher of all of them. But if there is no publisher, it will just add an error log and return false.
    /// </summary>
    /// <param name="outboxEvent">Event to send</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <typeparam name="TOutboxEvent">Event type that must implement from the ISendEvent</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated or if event does not have publisher. It can throw an exception if something goes wrong.</returns>
    public Task<bool> StoreAsync<TOutboxEvent>(TOutboxEvent outboxEvent, NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TOutboxEvent : IOutboxEvent;
    
    /// <summary>
    /// Stores the event to the database immediately.
    /// </summary>
    /// <param name="outboxEvent">Event to send</param>
    /// <param name="eventProvider">Provider type of sending event</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <typeparam name="TOutboxEvent">Event type that must implement from the ISendEvent</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public Task<bool> StoreAsync<TOutboxEvent>(TOutboxEvent outboxEvent, EventProviderType eventProvider, 
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TOutboxEvent : IOutboxEvent;

    /// <summary>
    /// Clean all collected events from the memory.
    /// </summary>
    public void CleanCollectedEvents();
}