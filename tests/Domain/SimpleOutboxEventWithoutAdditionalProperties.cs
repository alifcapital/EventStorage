using EventStorage.Outbox.Models;

namespace EventStorage.Tests.Domain;

public class SimpleOutboxEventWithoutAdditionalProperties : IOutboxEvent
{
    public Guid EventId { get; init; }
    public string Type { get; init; }
    public DateTime Date { get; init; }
    public DateTime CreatedAt { get; init; }
}