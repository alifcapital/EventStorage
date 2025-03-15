using System.Text.Json;
using System.Text.Json.Serialization;
using EventStorage.Models;

namespace EventStorage.Extensions;

public static class EventExtensions
{
    private static readonly JsonSerializerOptions SerializerSettings =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// Serializes the event data to JSON.
    /// </summary>
    internal static string SerializeToJson(this IEvent data)
    {
        return JsonSerializer.Serialize(data, data.GetType(), SerializerSettings);
    }
}