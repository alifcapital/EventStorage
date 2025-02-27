using EventStorage.Inbox.Models;

namespace EventStorage.Tests.Domain.Module1;

public record UserCreated : IInboxEvent
{
    public required Guid EventId { get; init; } = Guid.CreateVersion7();

    public string Type { get; init; }
    public DateTime Date { get; init; }
    public DateTime CreatedAt { get; init; }
}