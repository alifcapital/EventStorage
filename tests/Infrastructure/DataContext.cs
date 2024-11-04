using EventStorage.Models;
using Npgsql;

namespace EventStorage.Tests.Infrastructure;

class DataContext<TEvent> where TEvent : BaseEventBox, new()
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _tableName;

    public DataContext(string connectionString, string tableName)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = dataSourceBuilder.Build();
        _tableName = tableName;
    }

    public bool ExistTable()
    {
        var sqlTableCount = $"SELECT COUNT(*) FROM pg_tables WHERE tablename = '{_tableName}';";
        var tableCount = Convert.ToInt32(_dataSource.CreateCommand(sqlTableCount).ExecuteScalar());
        return tableCount > 0;
    }

    public TEvent GetById(Guid id)
    {
        var sql = $"SELECT * FROM {_tableName} where id = @id";
        var command = _dataSource.CreateCommand(sql);

        command.Parameters.Add(new NpgsqlParameter("@id", id));

        using var reader = command.ExecuteReader();
        TEvent outboxEvent;
        if (reader.Read())
        {
            outboxEvent = new TEvent
            {
                Id = reader.GetGuid(0),
                Provider = reader.GetString(1),
                EventName = reader.GetString(2),
                EventPath = reader.GetString(3),
                Payload = reader.GetString(4),
                Headers = reader.GetString(5),
                AdditionalData = reader.GetString(6),
                TryCount = reader.GetInt32(8),
                TryAfterAt = reader.GetDateTime(9),
                ProcessedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
            };
        }
        else
        {
            throw new Exception($"Event not found by given id: {id}");
        }

        return outboxEvent;
    }
    
    public bool ExistsById(Guid id)
    {
        var sql = $"SELECT COUNT(*) FROM {_tableName} where id = @id";
        var command = _dataSource.CreateCommand(sql);

        command.Parameters.Add(new NpgsqlParameter("@id", id));

        var count = Convert.ToInt32(command.ExecuteScalar());
        return count > 0;
    }
}