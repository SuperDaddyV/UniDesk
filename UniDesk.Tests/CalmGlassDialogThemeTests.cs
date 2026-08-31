using System.Text.RegularExpressions;

namespace UniDesk.Tests;

public class CalmGlassDialogThemeTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    private static readonly (string FileName, string[] SemanticBrushes)[] DialogContracts =
    [
        ("UniDesk/Windows/UpdateResultWindow.xaml", [
            "WindowSurfaceBrush", "DividerBrush", "PrimaryTextBrush", "SecondaryTextBrush",
            "CardSurfaceBrush", "ControlSurfaceBrush", "AccentBrush", "AccentForegroundBrush"]),
        ("UniDesk/Windows/ToastWindow.xaml", [
            "SecondarySurfaceBrush", "DividerBrush", "AccentBrush", "PrimaryTextBrush"]),
        ("UniDesk/Windows/CompactConfirmWindow.xaml", [
            "WindowSurfaceBrush", "DividerBrush", "PrimaryTextBrush", "SecondaryTextBrush",
            "ControlSurfaceBrush", "AccentBrush", "AccentForegroundBrush"]),
        ("UniDesk/Windows/BackupImportPreviewWindow.xaml", [
            "WindowSurfaceBrush", "DividerBrush", "CardSurfaceBrush", "SecondaryTextBrush",
            "DangerBrush", "DangerForegroundBrush", "WarningBrush"]),
        ("UniDesk/TextSnippetEditWindow.xaml", [
            "WindowSurfaceBrush", "DividerBrush", "PrimaryTextBrush", "SecondaryTextBrush",
            "ControlSurfaceBrush", "AccentBrush", "AccentForegroundBrush"]),
        ("UniDesk/NoteEditWindow.xaml", [
            "NoteBackgroundBrush", "NoteTextBrush", "DividerBrush", "AccentBrush",
            "AccentForegroundBrush", "ControlSurfaceBrush"]),
        ("UniDesk/QuickNoteEditorWindow.xaml", [
            "WindowSurfaceBrush", "DividerBrush", "PrimaryTextBrush", "SecondaryTextBrush",
            "ControlSurfaceBrush", "AccentBrush", "AccentForegroundBrush", "AccentSoftBrush",
            "DangerBrush"]),
        ("UniDesk/QuickTextManagerWindow.xaml", [
            "WindowSurfaceBrush", "DividerBrush", "PrimaryTextBrush", "SecondaryTextBrush",
            "ControlSurfaceBrush", "AccentBrush", "AccentSoftBrush", "AccentForegroundBrush"]),
        ("UniDesk/TodoEditWindow.xaml", [
            "WindowSurfaceBrush", "CardSurfaceBrush", "DividerBrush", "PrimaryTextBrush",
            "SecondaryTextBrush", "MutedTextBrush", "ControlSurfaceBrush", "AccentBrush",
            "AccentForegroundBrush", "AccentSoftBrush", "WarningBrush", "DangerBrush"]),
        ("UniDesk/Resources/TrayMenu.xaml", [
            "SecondarySurfaceBrush", "DividerBrush", "PrimaryTextBrush", "MutedTextBrush",
            "CardHoverBrush", "DangerBrush", "DangerForegroundBrush"])
    ];

    [Fact]
    public void DialogsAndTray_ShouldUseSemanticBrushesAndRejectLegacyLiteralColors()
    {
        foreach (var (fileName, semanticBrushes) in DialogContracts)
        {
            var xaml = ReadProjectFile(fileName.Split('/'));

            foreach (var brush in semanticBrushes)
            {
                Assert.Contains(
                    $"{{DynamicResource {brush}}}",
                    xaml,
                    StringComparison.Ordinal);
            }

            foreach (Match match in Regex.Matches(xaml, "#[0-9A-Fa-f]{3,8}"))
            {
                var effectStart = xaml.LastIndexOf(
                    "<DropShadowEffect",
                    match.Index,
                    StringComparison.Ordinal);
                var effectEnd = xaml.IndexOf("/>", match.Index, StringComparison.Ordinal);
                var isAllowedDropShadowColor =
                    match.Value.Equals("#000000", StringComparison.OrdinalIgnoreCase) &&
                    effectStart >= 0 &&
                    effectEnd >= match.Index;

                Assert.True(
                    isAllowedDropShadowColor,
                    $"{fileName} contains a non-semantic literal color {match.Value}.");
            }
        }
    }

    [Fact]
    public void TodoPriorityDots_ShouldUseMutedWarningAndDangerSemantics()
    {
        var todoXaml = ReadProjectFile("UniDesk", "TodoEditWindow.xaml");

        Assert.Contains(
            "Fill=\"{DynamicResource MutedTextBrush}\"",
            todoXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Fill=\"{DynamicResource WarningBrush}\"",
            todoXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Fill=\"{DynamicResource DangerBrush}\"",
            todoXaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoteEdit_ShouldRetainNoteSurfaceAndTextSemantics()
    {
        var noteXaml = ReadProjectFile("UniDesk", "NoteEditWindow.xaml");

        Assert.Contains(
            "Background=\"{DynamicResource NoteBackgroundBrush}\"",
            noteXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Foreground=\"{DynamicResource NoteTextBrush}\"",
            noteXaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ToastKinds_ShouldResolveSemanticThemeBrushes()
    {
        var toastCode = ReadProjectFile("UniDesk", "Windows", "ToastWindow.xaml.cs");

        Assert.Contains("SuccessBrush", toastCode, StringComparison.Ordinal);
        Assert.Contains("WarningBrush", toastCode, StringComparison.Ordinal);
        Assert.Contains("DangerBrush", toastCode, StringComparison.Ordinal);
        Assert.Contains("AccentBrush", toastCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromRgb", toastCode, StringComparison.Ordinal);
    }

    private static string ReadProjectFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([ProjectRoot, .. segments]));

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniDesk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the UniDesk repository root.");
    }
}
