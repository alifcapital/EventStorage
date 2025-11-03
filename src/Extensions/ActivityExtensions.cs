using System.Diagnostics;
using EventStorage.Instrumentation;
using EventStorage.Models;
using Microsoft.Extensions.Logging;

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
        //TODO: Mirolim should uncomment it
        // activity?.SetTag(EventStorageInvestigationTagNames.EventIdTag, message.Id);
        // activity?.SetTag(EventStorageInvestigationTagNames.EventTypeTag, message.EventName);
        // activity?.SetTag(EventStorageInvestigationTagNames.EventProviderTag, message.Provider);
        // activity?.SetTag(EventStorageInvestigationTagNames.EventNamingPolicyTypeTag, message.NamingPolicyType);
    }

    /// <summary>
    /// Creates a logging scope and attaches event information as scope properties. Also adds a log entry with the specified display title.
    /// </summary>
    /// <param name="logger">The logger to attach information to.</param>
    /// <param name="message">The event message containing the information.</param>
    /// <param name="logDisplayTitle">The display title to add log.</param>
    /// <typeparam name="TEvent">The type of the event message, must implement <see cref="IBaseMessageBox"/>.</typeparam>
    internal static IDisposable CreateScopeAndAttachEventInfo<TEvent>(this ILogger logger, TEvent message, string logDisplayTitle)
        where TEvent : class, IBaseMessageBox
    {
        logger.LogInformation(logDisplayTitle);
        
        return logger.BeginScope(new Dictionary<string, string>
        {
            [EventStorageInvestigationTagNames.EventIdTag] = message.Id.ToString(),
            [EventStorageInvestigationTagNames.EventTypeTag] = message.EventName,
            [EventStorageInvestigationTagNames.EventProviderTag] = message.Provider,
            [EventStorageInvestigationTagNames.EventNamingPolicyTypeTag] = message.NamingPolicyType
        });
    }
}