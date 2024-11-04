using EventStorage.Models;
using EventStorage.Outbox.Models;

namespace EventStorage.Tests.Domain;

public class SimpleSendEventCreated: ISendEvent, IHasHeaders
{ 
    public Guid EventId { get; init; }
    public string Type { get; init; }
    public DateTime Date { get; init; }
    public DateTime CreatedAt { get; init; }
    
    public Dictionary<string, string> Headers { get; set; }
}