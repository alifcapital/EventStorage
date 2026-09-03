using System.Text.Json.Serialization;

namespace EventStorage.Converters;

/// <summary>
/// The JSON converters every event payload is serialized and deserialized with.
/// <para>
/// Publishers outside .NET commonly send a date as a database timestamp
/// (<c>"2025-03-07 20:55:50"</c>) rather than in the strict ISO-8601 format the built-in converters
/// require, which would otherwise fail the whole event while deserializing it. Keeping the set here means
/// a new event is covered without having to opt in per property, and a new converter is added in one
/// place instead of at every call site that builds serializer options.
/// </para>
/// </summary>
public static class EventJsonConverters
{
    /// <summary>
    /// Every converter is stateless, so these instances are shared by all serializer options.
    /// </summary>
    public static IReadOnlyList<JsonConverter> All { get; } =
    [
        new DateTimeJsonConverter(),
        new DateOnlyJsonConverter()
    ];
}
