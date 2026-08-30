using Xunit;
using UniDesk.Services;
using UniDesk.Models;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using UniDesk.Helpers;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class TodoServiceTests
{
    private string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_todo.db");

    private async Task<(DatabaseService db, TodoService svc)> InitAsync()
    {
        var connectionString = $"Data Source={_testDbFile}";
        var db = new DatabaseService(connectionString);
        await db.InitializeAsync();
        var svc = new TodoService(db);
        return (db, svc);
    }

    private void Cleanup()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_testDbFile))
            {
                File.Delete(_testDbFile);
            }
        }
        catch
        {
        }
    }

    private sealed class AtomicToggleDatabase : IDatabaseService
    {
        public List<(string Sql, object?[] Parameters)> Commands { get; } = new();

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters)
        {
            Commands.Add((sql, parameters));
            return Task.FromResult(1);
        }

        public Task<List<T>> QueryAsync<T>(
            string sql,
            Func<Microsoft.Data.Sqlite.SqliteDataReader, T> map,
            params object?[] parameters) =>
            Task.FromException<List<T>>(new InvalidOperationException("Toggle must not query before updating."));

        public Task<T?> QuerySingleAsync<T>(
            string sql,
            Func<Microsoft.Data.Sqlite.SqliteDataReader, T> map,
            params object?[] parameters) =>
            Task.FromException<T?>(new InvalidOperationException("Toggle must not query before updating."));

        public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation) =>
            Task.FromException<T>(new NotSupportedException());
    }

    [Fact]
    public async Task CreateTodoAsync_ShouldInsertAndReturnId()
    {
        var (db, svc) = await InitAsync();

        var todo = new TodoItem
        {
            Title = "Test Todo",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var id = await svc.CreateTodoAsync(todo);
        Assert.True(id > 0);

        var fetched = await svc.GetTodoAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Test Todo", fetched!.Title);
        Assert.False(fetched.IsCompleted);

        Cleanup();
    }

    [Fact]
    public async Task ToggleCompleteAsync_ShouldChangeStatus()
    {
        var (db, svc) = await InitAsync();

        var todo = new TodoItem
        {
            Title = "Toggle Test",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var id = await svc.CreateTodoAsync(todo);
        Assert.True(id > 0);
        
        await svc.ToggleCompleteAsync(id);
        var fetched = await svc.GetTodoAsync(id);
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsCompleted);
        Assert.NotNull(fetched.CompletedAt);

        await svc.ToggleCompleteAsync(id);
        fetched = await svc.GetTodoAsync(id);
        Assert.NotNull(fetched);
        Assert.False(fetched!.IsCompleted);
        Assert.Null(fetched.CompletedAt);

        Cleanup();
    }

    [Fact]
    public async Task ToggleCompleteAsync_ShouldUseSingleAtomicUpdate()
    {
        var database = new AtomicToggleDatabase();
        var service = new TodoService(database);

        await service.ToggleCompleteAsync(42);

        var command = Assert.Single(database.Commands);
        Assert.Contains("CASE", command.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsCompleted", command.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", command.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(42, command.Parameters[1]);
    }

    [Fact]
    public async Task ToggleCompleteAsync_WhenTodoDoesNotExist_ShouldPreserveAffectedRowFailure()
    {
        var (db, service) = await InitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleCompleteAsync(-1));

        Cleanup();
    }

    [Fact]
    public async Task GetTodayTodosAsync_ShouldReturnRelevantTodos()
    {
        var (db, svc) = await InitAsync();

        await svc.CreateTodoAsync(new TodoItem { Title = "Today", DueDate = DateTime.UtcNow });
        await svc.CreateTodoAsync(new TodoItem { Title = "No Due Date", IsCompleted = false });
        await svc.CreateTodoAsync(new TodoItem { Title = "Tomorrow", DueDate = DateTime.UtcNow.AddDays(1) });

        var todayTodos = await svc.GetTodayTodosAsync();
        
        Assert.Contains(todayTodos, t => t.Title == "Today");
        Assert.Contains(todayTodos, t => t.Title == "No Due Date");
        Assert.DoesNotContain(todayTodos, t => t.Title == "Tomorrow");

        Cleanup();
    }

    [Fact]
    public async Task DeleteTodoAsync_ShouldRemoveTodo()
    {
        var (db, svc) = await InitAsync();

        var id = await svc.CreateTodoAsync(new TodoItem { Title = "Delete Me" });
        Assert.True(id > 0);
        Assert.NotNull(await svc.GetTodoAsync(id));

        await svc.DeleteTodoAsync(id);
        Assert.Null(await svc.GetTodoAsync(id));

        Cleanup();
    }
}
