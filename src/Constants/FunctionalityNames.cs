namespace EventStorage.Constants;

/// <summary>
/// The functionality names of the EventStorage.
/// </summary>
internal struct FunctionalityNames
{
    /// <summary>
    /// The name of the Inbox functionality.
    /// </summary>
    internal const string Inbox = nameof(Inbox);
    
    /// <summary>
    /// The name of the Outbox functionality.
    /// </summary>
    internal const string Outbox = nameof(Outbox);

    /// <summary>
    /// Gets the name of the distributed lock for processing or changing a single event. The processors and the
    /// management services use the same lock, so they never change the same event at the same time.
    /// </summary>
    /// <param name="functionalityName">The name of the functionality: <see cref="Inbox"/> or <see cref="Outbox"/>.</param>
    /// <param name="eventId">The id of the event.</param>
    internal static string GetEventLockName(string functionalityName, Guid eventId) =>
        $"Processing{functionalityName}Event_{eventId}";
}