namespace EventStorage.Outbox.Models;

public interface IHasAdditionalData
{
    /// <summary>
    /// Gets or sets the additional data of the event. The data structure is similar to the headers, but since it does not use as a header while publishing, we need to split that.
    /// </summary>
    public Dictionary<string, string> AdditionalData { get; set; }
}