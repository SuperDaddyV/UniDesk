using UniDesk.Services;

namespace UniDesk.Models;

public sealed class ShortcutImportResult
{
    public int AddedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int InvalidCount { get; set; }
    public int LimitSkippedCount { get; set; }

    public bool HasChanges => AddedCount > 0;

    public string ToUserMessage(ILocalizationService? localizationService = null)
    {
        var parts = new List<string>();
        if (AddedCount > 0)
        {
            parts.Add(Format(localizationService, "Shortcut.ImportAddedFormat", $"已添加 {AddedCount} 个快捷方式", AddedCount));
        }

        if (DuplicateCount > 0)
        {
            parts.Add(Format(localizationService, "Shortcut.ImportDuplicateFormat", $"跳过 {DuplicateCount} 个重复项", DuplicateCount));
        }

        if (InvalidCount > 0)
        {
            parts.Add(Format(localizationService, "Shortcut.ImportInvalidFormat", $"忽略 {InvalidCount} 个无效项", InvalidCount));
        }

        if (LimitSkippedCount > 0)
        {
            parts.Add(Format(localizationService, "Shortcut.ImportLimitFormat", $"因数量限制跳过 {LimitSkippedCount} 个", LimitSkippedCount));
        }

        return parts.Count == 0
            ? localizationService?.GetString("Shortcut.ImportNone") ?? "没有可添加的快捷方式"
            : string.Join(localizationService == null ? "，" : "; ", parts);
    }

    private static string Format(
        ILocalizationService? localizationService,
        string key,
        string fallback,
        int count) =>
        localizationService == null ? fallback : localizationService.Format(key, count);
}
