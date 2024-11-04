namespace EventStorage.Exceptions;

public class EventStoreException : Exception
{
    public EventStoreException(string message) : base(message)
    {
    }

    public EventStoreException(Exception innerException, string message) : base(message, innerException)
    {
    }
}