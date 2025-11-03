using System.Diagnostics;
using EventStorage.Instrumentation;
using EventStorage.Models;

namespace EventStorage.Extensions;

public static class ActivityExtensions
{
    /// <summary>
    /// Attaches event information to the activity as tags.
    /// </summary>
    /// <param name="activity">The activity to attach information to.</param>
    /// <param name="message">The event message containing the information.</param>
    /// <typeparam name="TEvent">The type of the event message, must implement <see cref="IBaseMessageBox"/>.</typeparam>
    internal static void AttachEventInfo<TEvent>(this Activity activity, TEvent message)
        where TEvent : class, IBaseMessageBox
    {
        activity?.SetTag(EventStorageInvestigationTagNames.EventIdTag, message.Id);
        activity?.SetTag(EventStorageInvestigationTagNames.EventTypeTag, message.EventName);
        activity?.SetTag(EventStorageInvestigationTagNames.EventProviderTag, message.Provider);
        activity?.SetTag(EventStorageInvestigationTagNames.EventNamingPolicyTypeTag, message.NamingPolicyType);
    }
}