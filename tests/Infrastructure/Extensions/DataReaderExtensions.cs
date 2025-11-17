using Npgsql;

namespace EventStorage.Tests.Infrastructure.Extensions;

public static class DataReaderExtensions
{
    public static bool HasColumn(this NpgsqlDataReader reader, string columnName)
    {
        var schema = reader.GetColumnSchema();
        return schema.Any(c => 
            c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }
}