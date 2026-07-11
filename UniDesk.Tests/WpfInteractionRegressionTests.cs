namespace UniDesk.Tests;

public class WpfInteractionRegressionTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void QuickNoteEditor_ShouldDeferCloseAndUseDoneLabel()
    {
        var windowXaml = ReadProjectFile("UniDesk", "QuickNoteEditorWindow.xaml");
        var windowCode = ReadProjectFile("UniDesk", "QuickNoteEditorWindow.xaml.cs");

        Assert.Contains("Content=\"{DynamicResource Common.Done}\"", windowXaml, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.BeginInvoke", windowCode, StringComparison.Ordinal);
        Assert.Contains("if (!await _viewModel.FlushAndCleanupAsync())", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "await _viewModel.FlushAndCleanupAsync();\n        Close();",
            windowCode.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TodoCompletionCircle_ShouldUseControlClickHandler()
    {
        var viewXaml = ReadProjectFile("UniDesk", "Controls", "TodosModuleView.xaml");
        var viewCode = ReadProjectFile("UniDesk", "Controls", "TodosModuleView.xaml.cs");

        Assert.Contains("MouseLeftButtonUp=\"TodoCheck_OnMouseLeftButtonUp\"", viewXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Ellipse.InputBindings>", viewXaml, StringComparison.Ordinal);
        Assert.Contains("ToggleTodoCommand.Execute", viewCode, StringComparison.Ordinal);
    }

    private static string ReadProjectFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([ProjectRoot, .. segments]));
}
