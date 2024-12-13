using System.Text.Json;

namespace EventStorage.Models;

internal abstract class BaseEventBox : IBaseEventBox
{
    public Guid Id { get; init; }
    public string Provider { get; init; }
    public string EventName { get; init; }
    public string EventPath { get; init; }
    public string Payload { get; internal set; }
    public string Headers { get; internal set; }
    public string AdditionalData { get; internal set; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public int TryCount { get; set; }
    public string NamingPolicyType { get; init; }
    public DateTime TryAfterAt { get; set; } = DateTime.Now;
    public DateTime? ProcessedAt { get; set; }

    public void Failed(int maxTryCount, int tryAfterMinutes)
    {
        IncreaseTryCount();
        if (TryCount > maxTryCount)
            TryAfterAt = DateTime.Now.AddMinutes(tryAfterMinutes);
    }

    private void IncreaseTryCount()
    {
        TryCount++;
    }

    public void Processed()
    {
        ProcessedAt = DateTime.Now;
    }

    private JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// Gets JsonSerializerOptions to use on naming police for serializing and deserializing properties of Event 
    /// </summary>
    public JsonSerializerOptions GetJsonSerializer()
    {
        if (_jsonSerializerOptions is not null)
            return _jsonSerializerOptions;

        _jsonSerializerOptions = NamingPolicyTypeNames.CreateJsonSerializer(NamingPolicyType);

        return _jsonSerializerOptions;
    }
}