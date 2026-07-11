using UniDesk.Helpers;

namespace UniDesk.Tests;

public class SystemThemeSelectionTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("invalid", true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void IsLightTheme_ShouldInterpretRegistryValue(object? value, bool expected)
    {
        Assert.Equal(expected, SystemThemeSelection.IsLightTheme(value));
    }

    [Theory]
    [InlineData(false, true, "Taro", "LightGrey", "DarkGrey", "Taro")]
    [InlineData(true, true, "Taro", "LightGrey", "DarkGrey", "LightGrey")]
    [InlineData(true, false, "Taro", "LightGrey", "DarkGrey", "DarkGrey")]
    public void GetEffectiveScheme_ShouldRespectFollowMode(
        bool followSystem,
        bool systemLight,
        string manual,
        string light,
        string dark,
        string expected)
    {
        Assert.Equal(
            expected,
            SystemThemeSelection.GetEffectiveScheme(followSystem, systemLight, manual, light, dark));
    }
}
