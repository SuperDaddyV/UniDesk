using System.Globalization;
using UniDesk.Services;

namespace UniDesk.Tests;

public class InitialLanguageResolverTests
{
    [Theory]
    [InlineData("chinesesimp", "ja-JP", "zh-CN")]
    [InlineData("english", "ja-JP", "en-US")]
    [InlineData("japanese", "en-US", "ja-JP")]
    [InlineData("spanish", "en-US", "es-ES")]
    [InlineData("EN-us", "ja-JP", "en-US")]
    public void Resolve_InstallerLanguageTakesPrecedence(
        string installerLanguage,
        string windowsLanguage,
        string expected)
    {
        var result = InitialLanguageResolver.Resolve(
            [$"--initial-language={installerLanguage}"],
            CultureInfo.GetCultureInfo(windowsLanguage));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("en-GB", "en-US")]
    [InlineData("ja-JP", "ja-JP")]
    [InlineData("es-MX", "es-ES")]
    [InlineData("fr-FR", ILocalizationService.DefaultLanguage)]
    public void Resolve_WithoutInstallerHintMapsWindowsUiLanguage(
        string windowsLanguage,
        string expected)
    {
        var result = InitialLanguageResolver.Resolve(
            [],
            CultureInfo.GetCultureInfo(windowsLanguage));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_InvalidInstallerHintFallsBackToWindowsUiLanguage()
    {
        var result = InitialLanguageResolver.Resolve(
            ["--initial-language=unsupported"],
            CultureInfo.GetCultureInfo("es-ES"));

        Assert.Equal("es-ES", result);
    }
}
