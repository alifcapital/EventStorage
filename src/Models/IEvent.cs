namespace EventStorage.Models;

public interface IEvent
{
    /// <summary>
    /// The id of event
    /// </summary>
    public Guid EventId { get; }
}