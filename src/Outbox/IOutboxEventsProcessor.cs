using EventStorage.Outbox.Models;
using EventStorage.Services;

namespace EventStorage.Outbox;

/// <summary>
/// Service for executing event handlers of outbox events.
/// </summary>
internal interface IOutboxEventsProcessor : IEventsProcessor
{
    /// <summary>
    /// Get the publisher types of the event as combined string.
    /// </summary>
    /// <param name="outboxEvent">The outbox event</param>
    /// <returns>One or multiple publisher type name as a single string. If there is no publisher, return null.</returns>
    string GetEventPublisherTypes<TOutboxEvent>(TOutboxEvent outboxEvent)
        where TOutboxEvent : IOutboxEvent;
}