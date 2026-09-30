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
    public DateTime CreatedAt { get; protected set; } = DateTime.Now;
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

    public void Processed(string performedBy = null, string comment = null)
    {
        ChangeStatus(EventStatus.Processed, performedBy, comment);
    }

    public void Rejected(string performedBy = null, string comment = null)
    {
        ChangeStatus(EventStatus.Rejected, performedBy, comment);
    }

    public void Rescheduled(DateTime tryAfterAt, string performedBy = null, string comment = null)
    {
        TryAfterAt = tryAfterAt;
        ChangeStatus(EventStatus.Pending, performedBy, comment);
    }

    /// <summary>
    /// Changes the status of the event and sets the time, the user and the comment of the change.
    /// </summary>
    /// <param name="status">The new status of the event.</param>
    /// <param name="performedBy">The user name of who changed the status manually. Null when the processor changed it.</param>
    /// <param name="comment">The comment of the manual change.</param>
    private void ChangeStatus(EventStatus status, string performedBy, string comment)
    {
        Status = status;
        UpdatedAt = DateTime.Now;
        UpdatedBy = performedBy;
        StatusComment = comment;
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