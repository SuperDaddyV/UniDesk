using UniDesk.Services;

namespace UniDesk.Tests;

public class LocalizationServiceTests
{
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("en-US", "en-US")]
    [InlineData("ja-JP", "ja-JP")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("bad-value", ILocalizationService.DefaultLanguage)]
    [InlineData("", ILocalizationService.DefaultLanguage)]
    [InlineData(null, ILocalizationService.DefaultLanguage)]
    public void NormalizeLanguage_ShouldFallbackForUnsupportedValues(string? input, string expected)
    {
        var service = new LocalizationService();

        Assert.Equal(expected, service.NormalizeLanguage(input));
    }
}
