using Npgsql;

namespace EventStorage.Tests.Infrastructure.Extensions;

public static class DataReaderExtensions
{
    /// <summary>
    /// Checks if the data reader (reading executed query) has a specific column.
    /// </summary>
    /// <param name="reader">The data reader.</param>
    /// <param name="columnName">The name of the column to check.</param>
    /// <returns>true if the column exists; otherwise, false.</returns>
    public static bool HasColumn(this NpgsqlDataReader reader, string columnName)
    {
        var schema = reader.GetColumnSchema();
        return schema.Any(c => 
            c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }
}