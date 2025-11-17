using EventStorage.Models;
using EventStorage.Tests.Infrastructure.Extensions;
using Npgsql;

namespace EventStorage.Tests.Infrastructure;

internal class DataContext<TEvent> where TEvent : BaseMessageBox, new()
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
        var sql = @$"SELECT * FROM {_tableName} where id = @id";
        var command = _dataSource.CreateCommand(sql);

        command.Parameters.Add(new NpgsqlParameter("@id", id));

        using var reader = command.ExecuteReader();
        TEvent message;
        if (reader.Read())
        {
            string namingPolicyTypeValue = null;
            if (reader.HasColumn("naming_policy_type"))
            {
                var ordNamingPolicy = reader.GetOrdinal("naming_policy_type");
                if (!reader.IsDBNull(ordNamingPolicy))
                    namingPolicyTypeValue = reader.GetString(ordNamingPolicy);
            }

            message = new TEvent
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                Provider = reader.GetString(reader.GetOrdinal("provider")),
                EventName = reader.GetString(reader.GetOrdinal("event_name")),
                EventPath = reader.GetString(reader.GetOrdinal("event_path")),
                Payload = reader.GetString(reader.GetOrdinal("payload")),
                Headers = reader.GetString(reader.GetOrdinal("headers")),
                NamingPolicyType = namingPolicyTypeValue,
                AdditionalData = reader.GetString(reader.GetOrdinal("additional_data")),
                TryCount = reader.GetInt32(reader.GetOrdinal("try_count")),
                TryAfterAt = reader.GetDateTime(reader.GetOrdinal("try_after_at"))
            };

            var processedOrdinal = reader.GetOrdinal("processed_at");
            if (!reader.IsDBNull(processedOrdinal))
                message.Processed();
        }
        else
        {
            throw new Exception($"Event not found by given id: {id}");
        }

        return message;
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