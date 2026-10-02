using System.Text.Json;

namespace EventStorage.Models;

/// <summary>
/// Represents an event type for storing and reading.
/// </summary>
internal interface  IBaseMessageBox
{
    /// <summary>
    /// Gets or sets the ID of the record.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets provider of the event. It can be RabbitMQ, SMS, Webhook, Email, Unknown
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Gets or sets the name of the event.
    /// </summary>
    string EventName { get; }

    /// <summary>
    /// The full path (namespace) of the event type. It just for information.
    /// </summary>
    string EventPath { get; }

    /// <summary>
    /// Gets or sets the payload of the event in JSON format.
    /// </summary>
    string Payload { get; }

    /// <summary>
    /// Gets or sets the headers of the event in JSON format.
    /// </summary>
    string Headers { get; }

    /// <summary>
    /// Gets or sets the additional data of the event in JSON format.
    /// </summary>
    string AdditionalData { get; }

    /// <summary>
    /// Gets the creation time of the event.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the count of attempts to process the event.
    /// </summary>
    int TryCount { get; }
    
    /// <summary>
    /// Name of the naming policy type for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".
    /// </summary>
    string NamingPolicyType { get; }

    /// <summary>
    /// Gets the count of attempts to process the event.
    /// </summary>
    public DateTime TryAfterAt { get; set; }

    /// <summary>
    /// Gets the processing status of the event. It is stored as a string in the table.
    /// </summary>
    EventStatus Status { get; }

    /// <summary>
    /// Gets the reason of the last processing failure of the event.
    /// </summary>
    string FailureReason { get; }

    /// <summary>
    /// Gets the time of the last status change of the event. For the processed event, it is the processed time.
    /// </summary>
    DateTime? UpdatedAt { get; }

    /// <summary>
    /// Gets the username of who changed the status manually. It is null when the status is changed by the processor.
    /// </summary>
    string UpdatedBy { get; }

    /// <summary>
    /// Gets the comment of the last manual status change.
    /// </summary>
    string StatusComment { get; }

    /// <summary>
    /// For marking the event as failed with the reason. It increases the TryCount and the TryAfterAt.
    /// </summary>
    /// <param name="maxTryCount">The try count after which the TryAfterAt is increased by the tryAfterMinutesIfTryCountExceeded.</param>
    /// <param name="tryAfterSeconds">The seconds to increase the TryAfterAt while the TryCount is not higher than the maxTryCount.</param>
    /// <param name="tryAfterMinutesIfTryCountExceeded">The minutes to increase the TryAfterAt when the TryCount is higher than the maxTryCount.</param>
    /// <param name="failureReason">The reason of the failure.</param>
    /// <param name="performedBy">The username of who executed the event manually. Null when the processor executed it.</param>
    /// <param name="comment">The comment of the manual execution.</param>
    void Failed(int maxTryCount, int tryAfterSeconds, int tryAfterMinutesIfTryCountExceeded, string failureReason,
        string performedBy = null, string comment = null);

    /// <summary>
    /// For marking the event as failed because no processor (handler/publisher) is configured for it. It increases the TryCount and the TryAfterAt,
    /// and sets the failure reason by the EventName and Provider.
    /// </summary>
    /// <param name="tryAfterMinutes">The minutes to increase the TryAfterAt.</param>
    /// <param name="failureReason">The reason of the failure.</param>
    /// <param name="performedBy">The username of who executed the event manually. Null when the processor executed it.</param>
    void EventProcessorNotFound(int tryAfterMinutes, string failureReason, string performedBy = null);

    /// <summary>
    /// For marking the event as processed. The last failure reason is kept for the history.
    /// </summary>
    /// <param name="performedBy">The username of who processed the event manually. Null when the processor processed it.</param>
    /// <param name="comment">The comment of the manual change.</param>
    void Processed(string performedBy = null, string comment = null);

    /// <summary>
    /// For marking the event is rejected, so it will not be processed.
    /// </summary>
    /// <param name="performedBy">The username of who rejected the event.</param>
    /// <param name="comment">The reason of the rejection.</param>
    void Rejected(string performedBy = null, string comment = null);

    /// <summary>
    /// For marking the event as pending again to be processed at the given time.
    /// </summary>
    /// <param name="tryAfterAt">The time after which the event should be processed.</param>
    /// <param name="performedBy">The username of who rescheduled the event.</param>
    /// <param name="comment">The reason of the rescheduling.</param>
    void Rescheduled(DateTime tryAfterAt, string performedBy = null, string comment = null);

    /// <summary>
    /// Gets JsonSerializerOptions to use on naming police for serializing and deserializing properties of Event 
    /// </summary>
    JsonSerializerOptions GetJsonSerializer();
}