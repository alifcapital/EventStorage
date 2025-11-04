namespace EventStorage.Instrumentation;

/// <summary>
/// This struct defines a set of investigation tag names as constants to avoid hardcoding strings while attaching tags to the open telemetry.
/// It can be extended from the other modules to add more tag names as needed.
/// </summary>
public class EventStorageInvestigationTagNames
{
    /// <summary>
    /// The tag to add the messaging system name to a trace for being able to filter the spans from the tracing system.
    /// </summary>
    public const string TraceMessagingTagName = "messaging.system";
    
    /// <summary>
    /// The tag to add the payload of event to a trace
    /// </summary>
    internal const string InboxEventTag = "Inbox";
    
    /// <summary>
    /// The tag to add the headers of event to a trace
    /// </summary>
    internal const string OutboxEventTag = "Outbox";
    
    /// <summary>
    /// The tag to add the event id to a trace
    /// </summary>
    public const string EventIdTag = "event.id";
    
    /// <summary>
    /// The tag to add the event type to a trace
    /// </summary>
    public const string EventTypeTag = "event.type";
    
    /// <summary>
    /// The tag to add the provider name of the event to a trace
    /// </summary>
    public const string EventProviderTag = "event.provider";
    
    /// <summary>
    /// The tag to add the naming policy type of the event to a trace
    /// </summary>
    public const string EventNamingPolicyTypeTag = "event.naming-policy-type";
}