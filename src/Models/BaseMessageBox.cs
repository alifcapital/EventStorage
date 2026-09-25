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
    public EventStatus Status { get; protected set; } = EventStatus.Pending;

    /// <summary>
    /// The name of the <see cref="Status"/> to store it as a string in the table,
    /// since Dapper would pass the enum value as a number.
    /// </summary>
    public string StatusName => Status.ToString();

    public string FailureReason { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string UpdatedBy { get; protected set; }
    public string StatusComment { get; protected set; }

    #region Status changing methods

    public void Failed(int maxTryCount, int tryAfterMinutes, string failureReason, string performedBy = null,
        string comment = null)
    {
        IncreaseTryCount();
        if (TryCount > maxTryCount)
            TryAfterAt = DateTime.Now.AddMinutes(tryAfterMinutes);

        FailureReason = failureReason;
        ChangeStatus(EventStatus.Failed, performedBy, comment);
    }

    private void IncreaseTryCount()
    {
        TryCount++;
    }

    public void Processed()
    {
        ChangeStatus(EventStatus.Processed);
    }

    public void Rejected()
    {
        ChangeStatus(EventStatus.Rejected);
    }

    public void Rescheduled(DateTime tryAfterAt)
    {
        TryAfterAt = tryAfterAt;
        ChangeStatus(EventStatus.Pending);
    }

    /// <summary>
    /// Changes the status of the event and sets the time of the change.
    /// </summary>
    /// <param name="status">The new status of the event.</param>
    private void ChangeStatus(EventStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.Now;
    }

    #endregion

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