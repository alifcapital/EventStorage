using System.Diagnostics;

namespace EventStorage.Instrumentation.Trace;

/// <summary>
/// The EventStorage instrumentation to create a trace activity 
/// </summary>
internal static class EventStorageTraceInstrumentation
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
    /// The activity source to create a new activity
    /// </summary>
    private static readonly ActivitySource ActivitySource = new(InstrumentationName);

    /// <summary>
    /// For creating activity and use it to add a span.
    /// Also adds span type for being able to filter the spans from the tracing system.
    /// </summary>
    /// <param name="name">Name of new activity</param>
    /// <param name="kind">Type of new activity. The default is <see cref="ActivityKind.Internal"/></param>
    /// <param name="traceParentId">The id of activity (parent trace and span) to assign. Example: "{version}-{trace-id}-{parent-span-id}-{trace-flags}"</param>
    /// <param name="spanType">The type of span. The default is "EventStorage"</param>
    /// <returns>Newly created an open telemetry activity</returns>
    internal static Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal, string traceParentId = null, string spanType = InstrumentationName)
    {
        ActivityContext.TryParse(traceParentId, null, out ActivityContext parentContext);
        var activity = ActivitySource.StartActivity(name, kind, parentContext);
        if (activity == null) return null;
        
        const string spanTypeTagName = "messaging.system";
        activity.AddTag(spanTypeTagName, spanType);

        return activity;
    }
}