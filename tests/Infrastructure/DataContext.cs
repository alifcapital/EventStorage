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

            message.SetPropertyValue(nameof(BaseMessageBox.Status),
                Enum.Parse<EventStatus>(reader.GetString(reader.GetOrdinal("status"))));

            var failureReasonOrdinal = reader.GetOrdinal("failure_reason");
            if (!reader.IsDBNull(failureReasonOrdinal))
                message.SetPropertyValue(nameof(BaseMessageBox.FailureReason), reader.GetString(failureReasonOrdinal));

            var updatedAtOrdinal = reader.GetOrdinal("updated_at");
            if (!reader.IsDBNull(updatedAtOrdinal))
                message.SetPropertyValue(nameof(BaseMessageBox.UpdatedAt), reader.GetDateTime(updatedAtOrdinal));

            var updatedByOrdinal = reader.GetOrdinal("updated_by");
            if (!reader.IsDBNull(updatedByOrdinal))
                message.SetPropertyValue(nameof(BaseMessageBox.UpdatedBy), reader.GetString(updatedByOrdinal));

            var statusCommentOrdinal = reader.GetOrdinal("status_comment");
            if (!reader.IsDBNull(statusCommentOrdinal))
                message.SetPropertyValue(nameof(BaseMessageBox.StatusComment), reader.GetString(statusCommentOrdinal));
        }
        else
        {
            throw new Exception($"Event not found by given id: {id}");
        }

        return message;
    }
    
    /// <summary>
    /// Gets the raw value of the status column of the event, as it is stored in the table.
    /// </summary>
    public string GetStoredStatusById(Guid id)
    {
        var sql = $"SELECT status FROM {_tableName} where id = @id";
        var command = _dataSource.CreateCommand(sql);

        command.Parameters.Add(new NpgsqlParameter("@id", id));

        return command.ExecuteScalar() as string;
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