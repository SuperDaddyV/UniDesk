using System.Text.RegularExpressions;

namespace UniDesk.Tests;

public class ModuleResourceScopeTests
{
    private static readonly Regex StaticResourceRegex =
        new("\\{StaticResource\\s+([^},]+)", RegexOptions.Compiled);

    private static readonly Regex ResourceKeyRegex =
        new("x:Key=\"([^\"]+)\"", RegexOptions.Compiled);

    [Fact]
    public void ModuleViews_ResolveStaticResourcesFromApplicationScope()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var appRoot = Path.Combine(projectRoot, "UniDesk");
        var applicationResources = new HashSet<string>(StringComparer.Ordinal);

        AddResourceKeys(Path.Combine(appRoot, "App.xaml"), applicationResources);
        foreach (var themeFile in Directory.EnumerateFiles(Path.Combine(appRoot, "Resources", "Themes"), "*.xaml"))
        {
            AddResourceKeys(themeFile, applicationResources);
        }

        var missingResources = new List<string>();
        foreach (var moduleView in Directory.EnumerateFiles(Path.Combine(appRoot, "Controls"), "*ModuleView.xaml"))
        {
            var xaml = File.ReadAllText(moduleView);
            var localResources = ResourceKeyRegex.Matches(xaml)
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (Match match in StaticResourceRegex.Matches(xaml))
            {
                var key = match.Groups[1].Value.Trim();
                if (!applicationResources.Contains(key) && !localResources.Contains(key))
                {
                    missingResources.Add($"{Path.GetFileName(moduleView)}: {key}");
                }
            }
        }

        Assert.True(
            missingResources.Count == 0,
            "Module views reference resources outside their initialization scope: " +
            string.Join(", ", missingResources.Distinct(StringComparer.Ordinal)));
    }

    private static void AddResourceKeys(string path, ISet<string> keys)
    {
        var xaml = File.ReadAllText(path);
        foreach (Match match in ResourceKeyRegex.Matches(xaml))
        {
            keys.Add(match.Groups[1].Value);
        }
    }
}
