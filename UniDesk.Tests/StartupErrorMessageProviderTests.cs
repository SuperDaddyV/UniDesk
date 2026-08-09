using UniDesk.Services;

namespace UniDesk.Tests;

public class StartupErrorMessageProviderTests
{
    [Theory]
    [InlineData("zh-CN", "启动失败")]
    [InlineData("en-US", "failed to start")]
    [InlineData("ja-JP", "起動できません")]
    [InlineData("es-ES", "no pudo iniciarse")]
    public void GetStartupFailure_UsesResolvedLanguage(string language, string expectedText)
    {
        var result = StartupErrorMessageProvider.GetStartupFailure(language, @"C:\logs");

        Assert.Contains(expectedText, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"C:\logs", result.Message, StringComparison.Ordinal);
        if (language != "zh-CN")
        {
            Assert.DoesNotContain("启动失败", result.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("请查看", result.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    [InlineData("es-ES")]
    public void GetFatalFailure_NonChineseLanguagesDoNotLeakChineseFallback(string language)
    {
        var result = StartupErrorMessageProvider.GetFatalFailure(language, @"C:\logs");

        Assert.DoesNotContain("无法恢复", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("即将退出", result.Message, StringComparison.Ordinal);
    }
}
