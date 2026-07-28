using System.Reflection;
using System.Runtime.CompilerServices;
using UniDesk.ViewModels;

namespace UniDesk.Tests;

public class SettingsViewModelWeatherTests
{
    [Fact]
    public void City_WhenUserEntersManualValue_ShouldDisableAutoLocation()
    {
        var viewModel = CreateUninitializedViewModel();
        viewModel.AutoLocation = true;

        viewModel.City = "北京";

        Assert.False(viewModel.AutoLocation);
    }

    [Fact]
    public void City_WhenSettingsAreLoading_ShouldPreserveAutoLocation()
    {
        var viewModel = CreateUninitializedViewModel();
        SetLoading(viewModel, true);
        viewModel.AutoLocation = true;

        viewModel.City = "北京";

        Assert.True(viewModel.AutoLocation);
    }

    [Fact]
    public void AutoLocation_WhenUserEnablesIt_ShouldClearManualCity()
    {
        var viewModel = CreateUninitializedViewModel();
        SetLoading(viewModel, true);
        viewModel.City = "北京";
        SetLoading(viewModel, false);

        viewModel.AutoLocation = true;

        Assert.Empty(viewModel.City);
    }

    [Fact]
    public void AutoLocation_WhenSettingsAreLoading_ShouldPreserveManualCity()
    {
        var viewModel = CreateUninitializedViewModel();
        SetLoading(viewModel, true);
        viewModel.City = "北京";

        viewModel.AutoLocation = true;

        Assert.Equal("北京", viewModel.City);
    }

    private static SettingsViewModel CreateUninitializedViewModel() =>
        (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));

    private static void SetLoading(SettingsViewModel viewModel, bool value)
    {
        var field = typeof(SettingsViewModel).GetField("_isLoading", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, value);
    }
}
