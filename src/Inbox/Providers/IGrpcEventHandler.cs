using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// An interface for implementing the handling functionality of received inbox event for the gRPC provider.
/// </summary>
public interface IGrpcEventHandler<in TInboxEvent> : IEventHandler<TInboxEvent>
    where TInboxEvent : class, IInboxEvent
{
    
}