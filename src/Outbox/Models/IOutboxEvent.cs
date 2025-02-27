using EventStorage.Models;

namespace EventStorage.Outbox.Models;

/// <summary>
/// An interface for determining an outbox event.
/// </summary>
public interface IOutboxEvent : IEvent;