using InMemoryMessaging.Models;

namespace EventStorage.Models;

public interface IEvent : IMessage
{
    /// <summary>
    /// The id of event
    /// </summary>
    public Guid EventId { get; init; }
}