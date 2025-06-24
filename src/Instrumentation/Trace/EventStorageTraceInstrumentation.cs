using System.Diagnostics;

namespace EventStorage.Instrumentation.Trace;

/// <summary>
/// The EventStorage instrumentation to create a trace activity 
/// </summary>
internal class EventStorageTraceInstrumentation
{
    /// <summary>
    /// The instrumentation name
    /// </summary>
    internal const string InstrumentationName = "EventStorage";
    
    /// <summary>
    /// Determines whether the instrumentation is enabled or not.
    /// </summary>
    public static bool IsEnabled { get; internal set; }
    
    /// <summary>
    /// The tag to add the payload of event to a trace
    /// </summary>
    public const string InboxEventTag = "Inbox";
    
    /// <summary>
    /// The tag to add the headers of event to a trace
    /// </summary>
    public const string OutboxEventTag = "Outbox";

    /// <summary>
    /// The activity source to create a new activity
    /// </summary>
    private static readonly ActivitySource ActivitySource = new(InstrumentationName);

    /// <summary>
    /// For creating activity and use it to add a span
    /// </summary>
    /// <param name="name">Name of new activity</param>
    /// <param name="kind">Type of new activity. The default is <see cref="ActivityKind.Internal"/></param>
    /// <param name="traceParentId">The id of activity (parent trace and span) to assign. Example: "{version}-{trace-id}-{parent-span-id}-{trace-flags}"</param>
    /// <returns>Newly created an open telemetry activity</returns>
    internal static Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal, string traceParentId = null)
    {
        ActivityContext.TryParse(traceParentId, null, out ActivityContext parentContext);
        var activity = ActivitySource.StartActivity(name, kind, parentContext);

        return activity;
    }
}