namespace EventStorage.Models;

internal abstract class BaseEventBox : IBaseEventBox
{
    public Guid Id { get; init; }
    public string Provider { get; init; }
    public string EventName { get; init; }
    public string EventPath { get; init; }
    public string Payload { get; internal set; }
    public string Headers { get; internal set; }
    public string AdditionalData { get; internal set; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public int TryCount { get; set; }
    public DateTime TryAfterAt { get; set; } = DateTime.Now;
    public DateTime? ProcessedAt { get; set; }

    public void Failed(int maxTryCount, int tryAfterMinutes)
    {
        IncreaseTryCount();
        if (TryCount > maxTryCount)
            TryAfterAt = DateTime.Now.AddMinutes(tryAfterMinutes);
    }

    private void IncreaseTryCount()
    {
        TryCount++;
    }

    public void Processed()
    {
        ProcessedAt = DateTime.Now;
    }
}