using EventStorage.Management.Models;
using EventStorage.Models;

namespace EventStorage.Extensions;

internal static class EventDetailsExtensions
{
    /// <summary>
    /// Maps the inbox/outbox message to the event details.
    /// </summary>
    /// <param name="message">The inbox or outbox message.</param>
    /// <returns>Returns the details of the event or null if the message is null.</returns>
    internal static EventDetails ToEventDetails(this IBaseMessageBox message)
    {
        if (message is null)
            return null;

        return new EventDetails
        {
            Id = message.Id,
            Provider = message.Provider,
            EventName = message.EventName,
            EventPath = message.EventPath,
            Payload = message.Payload,
            Headers = message.Headers,
            AdditionalData = message.AdditionalData,
            NamingPolicyType = message.NamingPolicyType,
            CreatedAt = message.CreatedAt,
            TryCount = message.TryCount,
            TryAfterAt = message.TryAfterAt,
            Status = message.Status,
            FailureReason = message.FailureReason,
            UpdatedAt = message.UpdatedAt,
            UpdatedBy = message.UpdatedBy,
            StatusComment = message.StatusComment
        };
    }
}
