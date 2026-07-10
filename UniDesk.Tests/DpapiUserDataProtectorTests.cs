using UniDesk.Services;

namespace UniDesk.Tests;

public class DpapiUserDataProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_ShouldRoundTripForCurrentUser()
    {
        var protector = new DpapiUserDataProtector();

        var stored = protector.Protect("weather-secret");

        Assert.StartsWith("dpapi:v1:", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("weather-secret", stored, StringComparison.Ordinal);
        Assert.True(protector.TryUnprotect(stored, out var plaintext));
        Assert.Equal("weather-secret", plaintext);
    }

    [Fact]
    public void EmptyValue_ShouldRemainEmpty()
    {
        var protector = new DpapiUserDataProtector();

        Assert.Equal(string.Empty, protector.Protect(string.Empty));
        Assert.True(protector.TryUnprotect(string.Empty, out var plaintext));
        Assert.Equal(string.Empty, plaintext);
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData("dpapi:v1:not-base64")]
    public void TryUnprotect_InvalidStoredValue_ShouldReturnFalse(string storedValue)
    {
        var protector = new DpapiUserDataProtector();

        Assert.False(protector.TryUnprotect(storedValue, out var plaintext));
        Assert.Equal(string.Empty, plaintext);
    }
}
