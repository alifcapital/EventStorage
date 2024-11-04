namespace EventStorage.Models;

public interface IHasHeaders
{
    /// <summary>
    /// Gets or sets the header data of the event.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; }
}