using EventStorage.Inbox.Models;
using EventStorage.Models;
using EventStorage.Outbox.Models;

namespace EventStorage.Tests.Domain;

public record SimpleEntityWasCreated : IInboxEvent, IHasHeaders, IHasAdditionalData
{
    public Guid EventId { get; init; }

    public string Type { get; init; }
    public DateTime Date { get; init; }
    public DateTime CreatedAt { get; init; }
    public Dictionary<string, string> Headers { get; set; }
    public Dictionary<string, string> AdditionalData { get; set; }
}