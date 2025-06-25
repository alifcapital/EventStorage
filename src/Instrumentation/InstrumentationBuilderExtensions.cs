using EventStorage.Instrumentation.Trace;
using OpenTelemetry.Trace;

namespace EventStorage.Instrumentation;

public static class InstrumentationBuilderExtensions
{
    /// <summary>
    /// Enables the EventStorage instrumentation for adding telemetry to the Inbox and Outbox events.
    /// </summary>
    /// <param name="builder"><see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> to chain the calls.</returns>
    public static TracerProviderBuilder AddEventStorageInstrumentation(this TracerProviderBuilder builder)
    {
        builder.AddSource(EventStorageTraceInstrumentation.InstrumentationName);
        EventStorageTraceInstrumentation.IsEnabled = true;

        return builder;
    }
}