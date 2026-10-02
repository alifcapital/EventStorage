namespace EventStorage.Management.Models;

/// <summary>
/// The details of an inbox/outbox event, including its payload, headers and additional data.
/// </summary>
public record EventDetails : EventSummary
{
    /// <summary>
    /// The payload of the event in JSON format.
    /// </summary>
    public string Payload { get; init; }

    /// <summary>
    /// The headers of the event in JSON format.
    /// </summary>
    public string Headers { get; init; }

    /// <summary>
    /// The additional data of the event in JSON format.
    /// </summary>
    public string AdditionalData { get; init; }

    /// <summary>
    /// The naming policy type of the event properties. The outbox events always have the default one.
    /// </summary>
    public string NamingPolicyType { get; init; }

    /// <summary>
    /// The comment of the last manual status change.
    /// </summary>
    public string StatusComment { get; init; }
}
