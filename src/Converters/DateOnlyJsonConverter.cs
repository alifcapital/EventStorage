using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStorage.Converters;

/// <summary>
/// Reads a <see cref="DateOnly"/> property of an event, tolerating a value that carries a time part. The
/// built-in converter accepts nothing but a bare <c>"yyyy-MM-dd"</c>, while publishers outside .NET
/// commonly send the whole timestamp, e.g. <c>"2026-08-10T16:37:45.324081"</c>. The time part is dropped
/// without applying a time zone, so the date can never shift to the neighbouring day. Registered for every
/// event by <see cref="Models.NamingPolicyTypeNames.CreateJsonSerializer"/>, so a new event does not need
/// to opt in.
/// <para>
/// Writing is unchanged: the value is written as <c>"yyyy-MM-dd"</c>, the same format
/// <c>System.Text.Json</c> uses by default, so published payloads keep their existing shape. Nullable
/// properties are supported too — <c>System.Text.Json</c> wraps this converter and handles a JSON
/// <c>null</c> itself, so a missing or null value never reaches <see cref="Read"/>.
/// </para>
/// </summary>
public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.FromDateTime(EventDateParser.Read(ref reader));
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat));
    }
}
