namespace EventStorage.Models;

/// <summary>
/// Provider type of event
/// </summary>
public enum EventProviderType
{
    MessageBroker,
    WebHook,
    Sms,
    Email,
    gRPC,
    Unknown
}