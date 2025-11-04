using System.Text.Json;
using System.Text.Json.Serialization;
using EventStorage.Models;

namespace EventStorage.Extensions;

public static class EventExtensions
{
    /// <summary>
    /// The default JSON serializer settings to use for event serialization.
    /// It uses UnsafeRelaxedJsonEscaping to support UTF-8 characters and ignores null values.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerSettings =
        new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    /// <summary>
    /// Serializes the event data to JSON.
    /// </summary>
    internal static string SerializeToJson(this IEvent data)
    {
        return JsonSerializer.Serialize(data, data.GetType(), SerializerSettings);
    }
}