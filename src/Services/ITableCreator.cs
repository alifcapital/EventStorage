namespace EventStorage.Services;

/// <summary>
/// The service for creating table in the database if it does not exist.
/// </summary>
internal interface ITableCreator
{
    /// <summary>
    /// Creates the table if it does not exist.
    /// </summary>
    void CreateTableIfNotExists();
}