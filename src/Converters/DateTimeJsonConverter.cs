using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStorage.Converters;

/// <summary>
/// Reads a <see cref="DateTime"/> property of an event, tolerating the non ISO-8601 timestamps that
/// publishers outside .NET commonly send. Registered for every event by
/// <see cref="Models.NamingPolicyTypeNames.CreateJsonSerializer"/>, so a new event does not need to opt in.
/// <para>
/// Writing is unchanged: the value is written in the same ISO-8601 round-trip format
/// <c>System.Text.Json</c> uses by default, so published payloads keep their existing shape. Nullable
/// properties are supported too — <c>System.Text.Json</c> wraps this converter and handles a JSON
/// <c>null</c> itself, so a missing or null value never reaches <see cref="Read"/>.
/// </para>
/// </summary>
public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return EventDateParser.Read(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
