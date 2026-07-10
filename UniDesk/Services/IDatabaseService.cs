namespace UniDesk.Services;

public interface IDatabaseService : IDatabaseSession
{
    Task InitializeAsync();
    Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation);
}
