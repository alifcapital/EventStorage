using EventStorage.Inbox.Models;

namespace EventStorage.Tests.Domain;

public class SimpleEntityWasCreated : IReceiveEvent
{
    public Guid EventId { get; init; }

    public string Type { get; init; }
    public DateTime Date { get; init; }
    public DateTime CreatedAt { get; init; }
}