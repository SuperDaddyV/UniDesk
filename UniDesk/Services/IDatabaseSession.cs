using Microsoft.Data.Sqlite;

namespace UniDesk.Services;

public interface IDatabaseSession
{
    Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters);
    Task<List<T>> QueryAsync<T>(string sql, Func<SqliteDataReader, T> map, params object?[] parameters);
    Task<T?> QuerySingleAsync<T>(string sql, Func<SqliteDataReader, T> map, params object?[] parameters);
}
