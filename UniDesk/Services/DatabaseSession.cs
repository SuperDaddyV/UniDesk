using Microsoft.Data.Sqlite;

namespace UniDesk.Services;

internal sealed class DatabaseSession : IDatabaseSession
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public DatabaseSession(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters)
    {
        using var command = CreateCommand(sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<List<T>> QueryAsync<T>(
        string sql,
        Func<SqliteDataReader, T> map,
        params object?[] parameters)
    {
        using var command = CreateCommand(sql, parameters);
        var results = new List<T>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(map(reader));
        }

        return results;
    }

    public async Task<T?> QuerySingleAsync<T>(
        string sql,
        Func<SqliteDataReader, T> map,
        params object?[] parameters)
    {
        using var command = CreateCommand(sql, parameters);
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? map(reader) : default;
    }

    private SqliteCommand CreateCommand(string sql, object?[] parameters)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;

        for (var i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
        }

        return command;
    }
}
