namespace UniDesk.Services;

public interface IUserDataProtector
{
    string Protect(string plaintext);
    bool TryUnprotect(string storedValue, out string plaintext);
    bool IsProtected(string storedValue);
}
