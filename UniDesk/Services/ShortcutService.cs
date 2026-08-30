using UniDesk.Models;
using UniDesk.Helpers;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace UniDesk.Services;

public class ShortcutService : IShortcutService
{
    private const string ShortcutSelectSql =
        "SELECT Id, Name, Path, Type, IconPath, SortOrder, CreatedAt, LaunchArguments FROM Shortcuts";

    private readonly IDatabaseService _databaseService;
    private readonly Func<ShortcutItem, int, DerivedIcon?> _createDerivedIcon;
    private static readonly object DerivedIconSync = new();

    internal sealed record DerivedIcon(string Path, bool IsNewFile)
    {
        internal string? ContentSha256 { get; init; } =
            IsNewFile ? ComputeFileSha256(Path) : null;
    }

    public ShortcutService(IDatabaseService databaseService)
        : this(databaseService, CreateDerivedIcon)
    {
    }

    internal ShortcutService(
        IDatabaseService databaseService,
        Func<ShortcutItem, int, DerivedIcon?> createDerivedIcon)
    {
        _databaseService = databaseService;
        _createDerivedIcon = createDerivedIcon;
    }

    internal ShortcutService(
        IDatabaseService databaseService,
        Func<ShortcutItem, int, string?> createDerivedIcon)
        : this(databaseService, (shortcut, id) =>
        {
            var iconPath = createDerivedIcon(shortcut, id);
            return iconPath == null ? null : new DerivedIcon(iconPath, IsNewFile: true);
        })
    {
    }

    public async Task<List<ShortcutItem>> GetAllShortcutsAsync()
    {
        var shortcuts = await _databaseService.QueryAsync(
                $"{ShortcutSelectSql} ORDER BY SortOrder ASC, CreatedAt ASC, Id ASC",
                MapShortcut
            );

            if (NeedsSortOrderRepair(shortcuts))
            {
                await SaveSortOrderAsync(shortcuts);
            }

        return shortcuts;
    }

    public async Task<ShortcutItem?> GetShortcutAsync(int id)
    {
        return await _databaseService.QuerySingleAsync(
                $"{ShortcutSelectSql} WHERE Id = @p0",
                MapShortcut,
                id
            );
    }

    public async Task<int> CreateShortcutAsync(ShortcutItem shortcut)
    {
        var createdAt = shortcut.CreatedAt == default ? DateTime.UtcNow : shortcut.CreatedAt;
            var id = await _databaseService.QuerySingleAsync(
                "INSERT INTO Shortcuts (Name, Path, Type, IconPath, SortOrder, CreatedAt, LaunchArguments) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6) RETURNING Id",
                reader => reader.GetInt32(0),
                shortcut.Name,
                shortcut.Path,
                shortcut.Type.ToString(),
                shortcut.IconPath,
                shortcut.SortOrder,
                createdAt.ToString("o"),
                shortcut.LaunchArguments
            );

            if (id > 0 && string.IsNullOrEmpty(shortcut.IconPath))
            {
                DerivedIcon? derivedIcon = null;
                try
                {
                    derivedIcon = _createDerivedIcon(shortcut, id);
                    if (derivedIcon != null)
                    {
                        var affected = await _databaseService.ExecuteNonQueryAsync(
                            "UPDATE Shortcuts SET IconPath = @p0 WHERE Id = @p1",
                            derivedIcon.Path,
                            id);
                        if (affected != 1)
                        {
                            throw new InvalidOperationException($"快捷方式 {id} 的派生图标未能写入。");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"ShortcutService.CreateDerivedIcon({id})");
                    DeleteDerivedIcon(derivedIcon, id);
                }
            }

        if (id <= 0)
        {
            throw new InvalidOperationException("快捷方式未能写入数据库。");
        }

        return id;
    }

    private static DerivedIcon? CreateDerivedIcon(ShortcutItem shortcut, int id) =>
        !string.IsNullOrEmpty(shortcut.BundledIconFileName)
            ? CopyBundledIcon(shortcut.BundledIconFileName, id)
            : ExtractAndSaveIcon(shortcut.IconLookupPath ?? shortcut.Path, id);

    internal static void DeleteDerivedIcon(DerivedIcon? derivedIcon, int id)
    {
        lock (DerivedIconSync)
        {
            if (derivedIcon == null ||
                !derivedIcon.IsNewFile ||
                derivedIcon.ContentSha256 == null ||
                !File.Exists(derivedIcon.Path))
            {
                return;
            }

            try
            {
                var currentSha256 = ComputeFileSha256(derivedIcon.Path);
                if (!derivedIcon.ContentSha256.Equals(
                        currentSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                File.Delete(derivedIcon.Path);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"ShortcutService.DeleteDerivedIcon({id})");
            }
        }
    }

    public async Task UpdateShortcutAsync(ShortcutItem shortcut)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync(
                "UPDATE Shortcuts SET Name = @p0, Path = @p1, Type = @p2, IconPath = @p3, SortOrder = @p4, LaunchArguments = @p5 WHERE Id = @p6",
                shortcut.Name,
                shortcut.Path,
                shortcut.Type.ToString(),
                shortcut.IconPath,
                shortcut.SortOrder,
                shortcut.LaunchArguments,
                shortcut.Id
            );
        if (affected != 1)
        {
            throw new InvalidOperationException($"快捷方式 {shortcut.Id} 不存在或未能更新。");
        }
    }

    public async Task DeleteShortcutAsync(int id)
    {
        var shortcut = await GetShortcutAsync(id);
        var affected = await _databaseService.ExecuteNonQueryAsync(
            "DELETE FROM Shortcuts WHERE Id = @p0",
            id
        );
        if (affected != 1)
        {
            throw new InvalidOperationException($"快捷方式 {id} 不存在或未能删除。");
        }

        if (shortcut != null && !string.IsNullOrEmpty(shortcut.IconPath) && File.Exists(shortcut.IconPath))
        {
            try { File.Delete(shortcut.IconPath); } catch (Exception ex) { Logger.LogError(ex, $"ShortcutService.DeleteIcon({id})"); }
        }
    }

    public async Task UpdateSortOrderAsync(List<int> ids)
    {
        await SaveSortOrderAsync(ids);
    }

    public async Task NormalizeSortOrderAsync()
    {
        var shortcuts = await _databaseService.QueryAsync(
                $"{ShortcutSelectSql} ORDER BY SortOrder ASC, CreatedAt ASC, Id ASC",
                MapShortcut
            );

            if (NeedsSortOrderRepair(shortcuts))
            {
                await SaveSortOrderAsync(shortcuts);
            }
    }

    public async Task RefreshMissingIconsAsync()
    {
        try
        {
            var shortcuts = await _databaseService.QueryAsync(
                $"{ShortcutSelectSql} WHERE IconPath IS NULL OR IconPath = '' ORDER BY Id ASC",
                MapShortcut);

            foreach (var shortcut in shortcuts)
            {
                DerivedIcon? derivedIcon = null;
                try
                {
                    derivedIcon = _createDerivedIcon(shortcut, shortcut.Id);
                    if (derivedIcon == null)
                    {
                        continue;
                    }

                    var affected = await _databaseService.ExecuteNonQueryAsync(
                        "UPDATE Shortcuts SET IconPath = @p0 WHERE Id = @p1",
                        derivedIcon.Path,
                        shortcut.Id);
                    if (affected != 1)
                    {
                        DeleteDerivedIcon(derivedIcon, shortcut.Id);
                    }
                }
                catch
                {
                    DeleteDerivedIcon(derivedIcon, shortcut.Id);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ShortcutService.RefreshMissingIconsAsync");
        }
    }

    public async Task LaunchShortcutAsync(int id)
    {
        try
        {
            var shortcut = await GetShortcutAsync(id);
            if (shortcut == null)
            {
                return;
            }

            ShortcutLaunchHelper.TryLaunch(shortcut);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ShortcutService.LaunchShortcutAsync");
        }
    }

    private static DerivedIcon? CopyBundledIcon(string fileName, int id)
    {
        try
        {
            var sourcePath = SystemAppCatalog.GetBundledIconPath(fileName);
            if (sourcePath == null)
            {
                return null;
            }

            var iconPath = Path.Combine(DirectoryHelper.IconsDirectory, $"shortcut_{id}.png");
            return WriteDerivedIconAtomically(
                iconPath,
                temporaryPath => File.Copy(sourcePath, temporaryPath, overwrite: false));
        }
        catch
        {
            return null;
        }
    }

    private static DerivedIcon? ExtractAndSaveIcon(string path, int id)
    {
        try
        {
            var iconPath = Path.Combine(DirectoryHelper.IconsDirectory, $"shortcut_{id}.png");
            if (IsReusablePng(iconPath))
            {
                return new DerivedIcon(iconPath, IsNewFile: false);
            }

            var lookupPath = Environment.ExpandEnvironmentVariables(path);
            var icon = ExtractShellIcon(lookupPath);
            if (icon == null && File.Exists(lookupPath))
            {
                icon = Icon.ExtractAssociatedIcon(lookupPath);
            }

            if (icon == null)
            {
                return null;
            }

            using (icon)
            using (var bitmap = icon.ToBitmap())
            {
                return WriteDerivedIconAtomically(
                    iconPath,
                    temporaryPath => bitmap.Save(temporaryPath, ImageFormat.Png));
            }
        }
        catch
        {
            return null;
        }
    }

    internal static DerivedIcon? WriteDerivedIconAtomically(
        string iconPath,
        Action<string> writeTemporaryFile)
    {
        lock (DerivedIconSync)
        {
            if (IsReusablePng(iconPath))
            {
                return new DerivedIcon(iconPath, IsNewFile: false);
            }

            var temporaryPath = $"{iconPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                writeTemporaryFile(temporaryPath);
                if (!IsReusablePng(temporaryPath))
                {
                    return null;
                }

                File.Move(temporaryPath, iconPath, overwrite: true);
                return new DerivedIcon(iconPath, IsNewFile: true);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    internal static bool IsReusablePng(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return image.Width > 0 &&
                   image.Height > 0 &&
                   image.RawFormat.Guid == ImageFormat.Png.Guid;
        }
        catch
        {
            return false;
        }
    }

    private static string? ComputeFileSha256(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch
        {
            return null;
        }
    }

    private static Icon? ExtractShellIcon(string path)
    {
        var info = new ShFileInfo();
        var flags = ShgfiIcon | ShgfiLargeIcon;
        uint attributes = 0;

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            flags |= ShgfiUseFileAttributes;
            attributes = FileAttributeNormal;
        }

        var result = SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return (Icon)Icon.FromHandle(info.hIcon).Clone();
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static bool NeedsSortOrderRepair(IReadOnlyList<ShortcutItem> shortcuts)
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < shortcuts.Count; i++)
        {
            var order = shortcuts[i].SortOrder;
            if (order != i || order < 0 || !seen.Add(order))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SaveSortOrderAsync(IReadOnlyList<ShortcutItem> shortcuts)
    {
        for (var i = 0; i < shortcuts.Count; i++)
        {
            shortcuts[i].SortOrder = i;
        }

        await SaveSortOrderAsync(shortcuts.Select(shortcut => shortcut.Id).ToList());
    }

    private async Task SaveSortOrderAsync(IReadOnlyList<int> ids)
    {
        await _databaseService.ExecuteInTransactionAsync(async session =>
        {
            for (var i = 0; i < ids.Count; i++)
            {
                var affected = await session.ExecuteNonQueryAsync(
                    "UPDATE Shortcuts SET SortOrder = @p0 WHERE Id = @p1",
                    i,
                    ids[i]
                );
                if (affected != 1)
                {
                    throw new InvalidOperationException($"快捷方式 {ids[i]} 不存在或未能更新排序。");
                }
            }

            return true;
        });
    }

    private static ShortcutItem MapShortcut(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new ShortcutItem
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Path = reader.GetString(2),
            Type = Enum.TryParse<ShortcutType>(reader.GetString(3), out var type) ? type : ShortcutType.Application,
            IconPath = reader.IsDBNull(4) ? null : reader.GetString(4),
            SortOrder = reader.GetInt32(5),
            CreatedAt = DateTime.Parse(reader.GetString(6)),
            LaunchArguments = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : null
        };
    }
}
