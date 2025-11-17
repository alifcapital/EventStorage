using System.Text.Json;

namespace EventStorage.Models;

internal abstract class BaseMessageBox : IBaseMessageBox
{
    public Guid Id { get; init; }
    public string Provider { get; init; }
    public string EventName { get; init; }
    public string EventPath { get; init; }
    public string Payload { get; internal init; }
    public string Headers { get; internal init; }
    public string AdditionalData { get; internal init; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public int TryCount { get; set; }
    public string NamingPolicyType { get; init; } = NamingPolicyTypeNames.PascalCase;
    public DateTime TryAfterAt { get; set; } = DateTime.Now;
    public DateTime? ProcessedAt { get; protected set; }

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