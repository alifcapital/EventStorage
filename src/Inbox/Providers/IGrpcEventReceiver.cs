using EventStorage.Inbox.Models;

namespace EventStorage.Inbox.Providers;

/// <summary>
/// An interface for determine a receiver of events and implement receiving events functionality with the gRPC provider
/// </summary>
public interface IGrpcEventReceiver<TReceiveEvent> : IEventReceiver<TReceiveEvent>
    where TReceiveEvent : class, IReceiveEvent
{
    
}