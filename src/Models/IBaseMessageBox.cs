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
    /// Gets the processing status of the event. It is one of the <see cref="EventStatus"/> names.
    /// </summary>
    string Status { get; }

    /// <summary>
    /// Gets the reason of the last processing failure of the event.
    /// </summary>
    string FailureReason { get; }

    /// <summary>
    /// Gets the time of the last status change of the event. For the processed event, it is the processed time.
    /// </summary>
    DateTime? UpdatedAt { get; }

    /// <summary>
    /// Gets the user name of who changed the status manually. It is null when the status is changed by the processor.
    /// </summary>
    string UpdatedBy { get; }

    /// <summary>
    /// Gets the comment of the last manual status change.
    /// </summary>
    string StatusComment { get; }

    /// <summary>
    /// To increase the TryCount and TryAfterAt when it is failed
    /// </summary>
    void Failed(int maxTryCount, int tryAfterMinutes);

    /// <summary>
    /// For marking the event is processed
    /// </summary>
    void Processed();

    /// <summary>
    /// Gets JsonSerializerOptions to use on naming police for serializing and deserializing properties of Event 
    /// </summary>
    /// <returns></returns>
    JsonSerializerOptions GetJsonSerializer();
}