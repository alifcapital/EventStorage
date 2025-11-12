using EventStorage.Services;

namespace EventStorage.Inbox;

/// <summary>
/// Service for executing event handlers of inbox events.
/// </summary>
internal interface IInboxEventsProcessor : IEventsProcessor;