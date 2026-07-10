using System.Security.Cryptography;
using System.Text;

namespace UniDesk.Services;

public sealed class DpapiUserDataProtector : IUserDataProtector
{
    public const string Prefix = "dpapi:v1:";
    private static readonly byte[] Entropy =
        SHA256.HashData("UniDesk.UserData.v1"u8.ToArray());

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0) return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(
            plainBytes,
            Entropy,
            DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public bool TryUnprotect(string storedValue, out string plaintext)
    {
        plaintext = string.Empty;
        if (string.IsNullOrEmpty(storedValue)) return true;
        if (!IsProtected(storedValue)) return false;

        try
        {
            var protectedBytes = Convert.FromBase64String(storedValue[Prefix.Length..]);
            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
            plaintext = Encoding.UTF8.GetString(plainBytes);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    public bool IsProtected(string storedValue) =>
        !string.IsNullOrEmpty(storedValue) &&
        storedValue.StartsWith(Prefix, StringComparison.Ordinal);
}
