using EventStorage.Models;

namespace EventStorage.Outbox.Models;

/// <summary>
/// An interface for determine events to send
/// </summary>
public interface ISendEvent : IEvent
{
}