namespace UniDesk.Tests;

public class ThirdPartyLicensePackagingTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Theory]
    [InlineData("Hardcodet.NotifyIcon.Wpf-CPOL-1.02.txt")]
    [InlineData("SQLitePCLRaw-Apache-2.0.txt")]
    [InlineData("CommunityToolkit.Mvvm-LICENSE.txt")]
    [InlineData("CommunityToolkit.Mvvm-THIRD-PARTY-NOTICES.txt")]
    [InlineData("DotNet-Runtime-LICENSE.txt")]
    [InlineData("DotNet-Runtime-THIRD-PARTY-NOTICES.txt")]
    [InlineData("WindowsDesktop-Runtime-LICENSE.txt")]
    [InlineData("LibreHardwareMonitor-MPL-2.0.txt")]
    [InlineData("HidSharp-Apache-2.0.txt")]
    [InlineData("Mono.Posix.NETStandard-LICENSE.txt")]
    [InlineData("QWeather-Icons-LICENSE.txt")]
    [InlineData("PawnIO-LICENSE-EXCEPTION.txt")]
    [InlineData("Inter-OFL-1.1.txt")]
    [InlineData("SourceHanSans-OFL-1.1.txt")]
    public void RequiredLicensePayload_IsPresentAndNonEmpty(string fileName)
    {
        var path = Path.Combine(ProjectRoot, "installer-assets", "licenses", fileName);

        Assert.True(File.Exists(path), $"Missing third-party license payload: {fileName}");
        Assert.True(new FileInfo(path).Length > 100, $"Empty or incomplete license payload: {fileName}");
    }

    [Fact]
    public void Installer_PackagesTheCompleteLicenseDirectory()
    {
        var installer = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains(
            "Source: \"installer-assets\\licenses\\*\"; DestDir: \"{app}\\licenses\"",
            installer,
            StringComparison.Ordinal);
        Assert.Contains("--initial-language={language}", installer, StringComparison.Ordinal);
    }

    [Fact]
    public void Notices_DescribePawnIoExceptionAndDistributedRuntime()
    {
        var notices = File.ReadAllText(Path.Combine(ProjectRoot, "THIRD-PARTY-NOTICES.md"));

        Assert.Contains("device IO control interface", notices, StringComparison.Ordinal);
        Assert.Contains(".NET 10 Runtime", notices, StringComparison.Ordinal);
        Assert.Contains("Hardcodet.NotifyIcon.Wpf", notices, StringComparison.Ordinal);
        Assert.Contains("SQLitePCLRaw", notices, StringComparison.Ordinal);
        Assert.Contains("QWeather Icons", notices, StringComparison.Ordinal);
        Assert.Contains("Inter", notices, StringComparison.Ordinal);
        Assert.Contains("Source Han Sans SC", notices, StringComparison.Ordinal);
    }

    [Fact]
    public void Notices_ListEveryDirectPackageInDistributedProjects()
    {
        var notices = File.ReadAllText(Path.Combine(ProjectRoot, "THIRD-PARTY-NOTICES.md"));
        var lockFiles = new[]
        {
            Path.Combine(ProjectRoot, "UniDesk", "packages.lock.json"),
            Path.Combine(ProjectRoot, "UniDesk.HardwareService", "packages.lock.json")
        };

        foreach (var lockFile in lockFiles)
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(lockFile));
            foreach (var target in document.RootElement.GetProperty("dependencies").EnumerateObject())
            {
                foreach (var package in target.Value.EnumerateObject())
                {
                    if (package.Value.TryGetProperty("type", out var type) &&
                        type.GetString() == "Direct")
                    {
                        Assert.Contains(package.Name, notices, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(
        "DiskInfoToolkit 1.1.2",
        "https://github.com/Blacktempel/DiskInfoToolkit/commit/25319eae5781e75bcf141e844ceab2afe94d40ea")]
    [InlineData(
        "RAMSPDToolkit-NDD 1.4.2",
        "https://github.com/Blacktempel/RAMSPDToolkit/commit/3b47b960e0830fef344624ad5e389675d5f0a1ce")]
    [InlineData(
        "BlackSharp.Core 1.0.7",
        "https://github.com/Blacktempel/BlackSharp/commit/c70b735c6cec123ee8a046ac4a0bc6c606f52cf0")]
    public void Notices_ProvideStableSourceCodeFormForMplRuntimeDependencies(
        string packageAndVersion,
        string sourceCommitUrl)
    {
        var notices = File.ReadAllText(Path.Combine(ProjectRoot, "THIRD-PARTY-NOTICES.md"));

        Assert.Contains(packageAndVersion, notices, StringComparison.Ordinal);
        Assert.Contains(sourceCommitUrl, notices, StringComparison.Ordinal);
        Assert.Contains("Source Code Form", notices, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("README.md", "当前正式安装包示例")]
    [InlineData("README.zh-CN.md", "当前正式安装包示例")]
    [InlineData("README.en-US.md", "Current installer example")]
    [InlineData("README.ja-JP.md", "現在のインストーラー例")]
    [InlineData("README.es-ES.md", "Ejemplo del instalador actual")]
    public void Readme_DoesNotPresentUnreleasedCandidateAsCurrentRelease(
        string fileName,
        string prohibitedText)
    {
        var readme = File.ReadAllText(Path.Combine(ProjectRoot, fileName));

        Assert.DoesNotContain(prohibitedText, readme, StringComparison.Ordinal);
        Assert.Contains("2.1.0", readme, StringComparison.Ordinal);
    }

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
