using UniDesk.Helpers;
using UniDesk.Models;
using System.Globalization;

namespace UniDesk.Services;

public class TodoService : ITodoService
{
    private readonly IDatabaseService _databaseService;

    private const string TodoSelectColumns =
        "Id, Title, IsCompleted, DueDate, CreatedAt, CompletedAt, Priority";

    public TodoService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<TodoItem>> GetAllTodosAsync()
    {
        var todos = await _databaseService.QueryAsync(
            $"SELECT {TodoSelectColumns} FROM Todos",
            MapTodo
        );
        return TodoSortHelper.Sort(todos).ToList();
    }

    public async Task<TodoItem?> GetTodoAsync(int id)
    {
        return await _databaseService.QuerySingleAsync(
            $"SELECT {TodoSelectColumns} FROM Todos WHERE Id = @p0",
            MapTodo,
            id
        );
    }

    public async Task<int> CreateTodoAsync(TodoItem todo)
    {
        var now = DateTime.UtcNow;
        var createdAt = todo.CreatedAt == default ? now : todo.CreatedAt;

            var id = await _databaseService.QuerySingleAsync(
                "INSERT INTO Todos (Title, IsCompleted, DueDate, CreatedAt, CompletedAt, Priority) VALUES (@p0, @p1, @p2, @p3, @p4, @p5) RETURNING Id",
                reader => reader.GetInt32(0),
                todo.Title ?? string.Empty,
                todo.IsCompleted ? 1 : 0,
                todo.DueDate?.ToString("o", CultureInfo.InvariantCulture),
                createdAt.ToString("o", CultureInfo.InvariantCulture),
                todo.CompletedAt?.ToString("o", CultureInfo.InvariantCulture),
                (int)todo.Priority
            );

        if (id <= 0)
        {
            throw new InvalidOperationException("待办事项未能写入数据库。");
        }

        return id;
    }

    public async Task UpdateTodoAsync(TodoItem todo)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync(
                "UPDATE Todos SET Title = @p0, IsCompleted = @p1, DueDate = @p2, CompletedAt = @p3, Priority = @p4 WHERE Id = @p5",
                todo.Title ?? string.Empty,
                todo.IsCompleted ? 1 : 0,
                todo.DueDate?.ToString("o", CultureInfo.InvariantCulture),
                todo.CompletedAt?.ToString("o", CultureInfo.InvariantCulture),
                (int)todo.Priority,
                todo.Id
            );
        if (affected != 1)
        {
            throw new InvalidOperationException($"待办事项 {todo.Id} 不存在或未能更新。");
        }
    }

    public async Task DeleteTodoAsync(int id)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync(
                "DELETE FROM Todos WHERE Id = @p0",
                id
            );
        if (affected != 1)
        {
            throw new InvalidOperationException($"待办事项 {id} 不存在或未能删除。");
        }
    }

    public async Task ToggleCompleteAsync(int id)
    {
        var todo = await GetTodoAsync(id)
            ?? throw new InvalidOperationException($"待办事项 {id} 不存在，无法切换完成状态。");

            todo.IsCompleted = !todo.IsCompleted;
            todo.CompletedAt = todo.IsCompleted ? DateTime.UtcNow : null;

        await UpdateTodoAsync(todo);
    }

    public async Task MarkCompletedAsync(int id)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync(
                "UPDATE Todos SET IsCompleted = 1, CompletedAt = @p0 WHERE Id = @p1",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                id
            );
        if (affected != 1)
        {
            throw new InvalidOperationException($"待办事项 {id} 不存在或未能标记完成。");
        }
    }

    public async Task MarkUncompletedAsync(int id)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync(
                "UPDATE Todos SET IsCompleted = 0, CompletedAt = NULL WHERE Id = @p0",
                id
            );
        if (affected != 1)
        {
            throw new InvalidOperationException($"待办事项 {id} 不存在或未能取消完成。");
        }
    }

    public async Task<List<TodoItem>> GetTodayTodosAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

            var todos = await _databaseService.QueryAsync(
                $"SELECT {TodoSelectColumns} FROM Todos WHERE (DueDate >= @p0 AND DueDate < @p1) OR (IsCompleted = 0 AND DueDate IS NULL)",
                MapTodo,
                today.ToString("o", CultureInfo.InvariantCulture),
                tomorrow.ToString("o", CultureInfo.InvariantCulture)
            );
        return TodoSortHelper.Sort(todos).ToList();
    }

    private static TodoItem MapTodo(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        var todo = new TodoItem
        {
            Id = reader.GetInt32(0),
            Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            IsCompleted = reader.GetInt32(2) != 0,
            DueDate = ParseDateTime(reader.IsDBNull(3) ? null : reader.GetString(3)),
            CreatedAt = ParseDateTime(reader.IsDBNull(4) ? null : reader.GetString(4)) ?? DateTime.UtcNow,
            CompletedAt = ParseDateTime(reader.IsDBNull(5) ? null : reader.GetString(5))
        };

        if (reader.FieldCount > 6 && !reader.IsDBNull(6))
        {
            var priorityValue = reader.GetInt32(6);
            todo.Priority = Enum.IsDefined(typeof(TodoPriority), priorityValue)
                ? (TodoPriority)priorityValue
                : TodoPriority.Medium;
        }

        return todo;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
