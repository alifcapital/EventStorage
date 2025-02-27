using EventStorage.Models;

namespace EventStorage.Inbox.Models;

/// <summary>
/// An interface for determining an inbox event.
/// </summary>
public interface IInboxEvent : IEvent;