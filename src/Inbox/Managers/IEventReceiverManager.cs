using EventStorage.Inbox.Models;
using EventStorage.Models;

namespace EventStorage.Inbox.Managers;

public interface IEventReceiverManager
{
    /// <summary>
    /// To store receiving an received event to the database
    /// </summary>
    /// <param name="receivedEvent">Event to send</param>
    /// <param name="eventPath">Path of receivedEvent. It can be receivedEvent name or routing kew or any other thing depend on receivedEvent type</param>
    /// <param name="eventProvider">Provider type of sending receivedEvent</param>
    /// <typeparam name="TReceiveEvent">Event type that must implement from the IEventReceiverManager</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Received<TReceiveEvent>(TReceiveEvent receivedEvent, string eventPath, EventProviderType eventProvider)
        where TReceiveEvent : IReceiveEvent;
    
    /// <summary>
    /// To store receiving an received event to the database
    /// </summary>
    /// <param name="receivedEvent">Event to send</param>
    /// <param name="eventPath">Path of receivedEvent. It can be receivedEvent name or routing kew or any other thing depend on receivedEvent type</param>
    /// <param name="eventProvider">Provider type of sending receivedEvent</param>
    /// <param name="headers">Headers of received event</param>
    /// <param name="additionalData">Additional data of received event if exists</param>
    /// <typeparam name="TReceiveEvent">Event type that must implement from the IEventReceiverManager</typeparam>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Received<TReceiveEvent>(TReceiveEvent receivedEvent, string eventPath, EventProviderType eventProvider, string headers, string additionalData = null)
        where TReceiveEvent : IReceiveEvent;

    /// <summary>
    /// To store receiving an event to the database
    /// </summary>
    /// <param name="eventId">The id of event</param>
    /// <param name="eventTypeName">The type name of event</param>
    /// <param name="eventPath">Path of event. It can be event name or routing kew or any other thing depend on event type</param>
    /// <param name="eventProvider">Provider type of sending event</param>
    /// <param name="payload">Payload of received event</param>
    /// <param name="headers">Headers of received event</param>
    /// <param name="additionalData">Additional data of received event if exists</param>
    /// <returns>Returns true if it was entered successfully or false if the value is duplicated. It can throw an exception if something goes wrong.</returns>
    public bool Received(Guid eventId, string eventTypeName, string eventPath, EventProviderType eventProvider,
        string payload, string headers = null, string additionalData = null);
}