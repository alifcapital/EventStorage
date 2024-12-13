using EventStorage.Models;
using EventStorage.Outbox.Models;

namespace EventStorage.Outbox.Managers;

public interface IEventSenderManager : IDisposable
{
    /// <summary>
    /// First to collect all sending events to the memory and then store them to the database
    /// </summary>
    /// <param name="event">Event to send</param>
    /// <param name="eventProvider">Provider type of sending event</param>
    /// <param name="eventPath">Path of event. It can be event name or routing kew or any other thing depend on event type. When it is not passed, it will use event type name as an event path</param>
    /// <param name="namingPolicyType">Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".</param>
    /// <typeparam name="TSendEvent">Event type that must implement from the ISendEvent</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Send<TSendEvent>(TSendEvent @event, EventProviderType eventProvider, string eventPath = null, 
        NamingPolicyType namingPolicyType = NamingPolicyType.PascalCase)
        where TSendEvent : ISendEvent;

    /// <summary>
    /// Clean all collected events from the memory
    /// </summary>
    public void CleanCollectedEvents();
}