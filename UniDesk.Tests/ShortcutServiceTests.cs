using Xunit;
using UniDesk.Services;
using UniDesk.Models;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using UniDesk.Helpers;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class ShortcutServiceTests
{
    private string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_shortcut.db");

    private async Task<(DatabaseService db, ShortcutService svc)> InitAsync()
    {
        var connectionString = $"Data Source={_testDbFile}";
        var db = new DatabaseService(connectionString);
        await db.InitializeAsync();
        var svc = new ShortcutService(db);
        return (db, svc);
    }

    private void Cleanup()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_testDbFile))
            {
                File.Delete(_testDbFile);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task CreateShortcutAsync_ShouldInsertAndReturnId()
    {
        var (db, svc) = await InitAsync();

        var shortcut = new ShortcutItem
        {
            Name = "Test App",
            Path = "C:\\Windows\\notepad.exe",
            Type = ShortcutType.Application,
            SortOrder = 0
        };

        var id = await svc.CreateShortcutAsync(shortcut);
        Assert.True(id > 0);

        var fetched = await svc.GetShortcutAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Test App", fetched!.Name);
        Assert.Equal("C:\\Windows\\notepad.exe", fetched.Path);

        Cleanup();
    }

    [Fact]
    public async Task CreateShortcutAsync_WhenDerivedIconUpdateFails_ShouldKeepTheCreatedShortcut()
    {
        using var testLogs = new TestLogDirectoryScope();
        Cleanup();
        var db = new DatabaseService($"Data Source={_testDbFile}");
        await db.InitializeAsync();
        await db.ExecuteNonQueryAsync(
            "CREATE TRIGGER fail_shortcut_icon BEFORE UPDATE OF IconPath ON Shortcuts BEGIN SELECT RAISE(ABORT, 'forced icon update failure'); END");
        var derivedIcon = Path.Combine(Path.GetTempPath(), $"UniDesk-shortcut-icon-{Guid.NewGuid():N}.png");
        var service = new ShortcutService(
            db,
            (_, _) =>
            {
                File.WriteAllText(derivedIcon, "derived icon");
                return new ShortcutService.DerivedIcon(derivedIcon, IsNewFile: true);
            });

        var id = await service.CreateShortcutAsync(new ShortcutItem
        {
            Name = "Keeps record",
            Path = "notepad.exe"
        });

        Assert.True(id > 0);
        Assert.NotNull(await service.GetShortcutAsync(id));
        Assert.False(File.Exists(derivedIcon));
        Cleanup();
    }

    [Fact]
    public async Task CreateShortcutAsync_WhenIconUpdateMissesTarget_ShouldKeepPreExistingIcon()
    {
        using var testLogs = new TestLogDirectoryScope();
        Cleanup();
        var db = new DatabaseService($"Data Source={_testDbFile}");
        await db.InitializeAsync();
        var preExistingIcon = Path.Combine(Path.GetTempPath(), $"UniDesk-shortcut-existing-{Guid.NewGuid():N}.png");
        File.WriteAllText(preExistingIcon, "pre-existing icon");

        try
        {
            var service = new ShortcutService(
                db,
                (_, id) =>
                {
                    db.ExecuteNonQueryAsync("DELETE FROM Shortcuts WHERE Id = @p0", id)
                        .GetAwaiter()
                        .GetResult();
                    return new ShortcutService.DerivedIcon(preExistingIcon, IsNewFile: false);
                });

            var id = await service.CreateShortcutAsync(new ShortcutItem
            {
                Name = "Keeps existing icon",
                Path = "notepad.exe"
            });

            Assert.True(id > 0);
            Assert.True(File.Exists(preExistingIcon));
            Assert.Equal("pre-existing icon", await File.ReadAllTextAsync(preExistingIcon));
        }
        finally
        {
            if (File.Exists(preExistingIcon))
            {
                File.Delete(preExistingIcon);
            }

            Cleanup();
        }
    }

    [Fact]
    public async Task RefreshMissingIconsAsync_WhenTargetDisappears_ShouldCleanNewDerivedIcon()
    {
        Cleanup();
        var db = new DatabaseService($"Data Source={_testDbFile}");
        await db.InitializeAsync();
        var id = 1_000_000_000 + Random.Shared.Next(100_000_000);
        var derivedIcon = Path.Combine(Path.GetTempPath(), $"UniDesk-shortcut-refresh-{Guid.NewGuid():N}.png");
        var generatorCalled = false;

        await db.ExecuteNonQueryAsync(
            "INSERT INTO Shortcuts (Id, Name, Path, Type, IconPath, SortOrder, CreatedAt, LaunchArguments) VALUES (@p0, @p1, @p2, @p3, NULL, @p4, @p5, NULL)",
            id,
            "Refresh race",
            "notepad.exe",
            ShortcutType.Application.ToString(),
            0,
            DateTime.UtcNow.ToString("o"));

        try
        {
            var service = new ShortcutService(
                db,
                (_, shortcutId) =>
                {
                    generatorCalled = true;
                    File.WriteAllText(derivedIcon, "new derived icon");
                    db.ExecuteNonQueryAsync("DELETE FROM Shortcuts WHERE Id = @p0", shortcutId)
                        .GetAwaiter()
                        .GetResult();
                    return new ShortcutService.DerivedIcon(derivedIcon, IsNewFile: true);
                });

            await service.RefreshMissingIconsAsync();

            Assert.True(generatorCalled);
            Assert.False(File.Exists(derivedIcon));
            Assert.Null(await service.GetShortcutAsync(id));
        }
        finally
        {
            if (File.Exists(derivedIcon))
            {
                File.Delete(derivedIcon);
            }

            Cleanup();
        }
    }

    [Fact]
    public void WriteDerivedIconAtomically_WhenWriterFails_ShouldNotPublishPartialFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniDeskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var iconPath = Path.Combine(directory, "shortcut_1.png");

        try
        {
            var result = ShortcutService.WriteDerivedIconAtomically(
                iconPath,
                temporaryPath =>
                {
                    File.WriteAllText(temporaryPath, "partial");
                    throw new IOException("forced write failure");
                });

            Assert.Null(result);
            Assert.False(File.Exists(iconPath));
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteDerivedIconAtomically_ShouldReplaceInvalidCacheWithValidPng()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniDeskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var iconPath = Path.Combine(directory, "shortcut_2.png");
        File.WriteAllText(iconPath, "truncated png");

        try
        {
            var result = ShortcutService.WriteDerivedIconAtomically(
                iconPath,
                temporaryPath =>
                {
                    using var bitmap = new Bitmap(2, 2);
                    bitmap.Save(temporaryPath, ImageFormat.Png);
                });

            Assert.NotNull(result);
            Assert.True(result!.IsNewFile);
            Assert.True(ShortcutService.IsReusablePng(iconPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeleteDerivedIcon_WhenOwnedContentWasReplaced_ShouldKeepReplacement()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniDeskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var iconPath = Path.Combine(directory, "shortcut_3.png");

        try
        {
            var owned = ShortcutService.WriteDerivedIconAtomically(
                iconPath,
                temporaryPath =>
                {
                    using var bitmap = new Bitmap(2, 2);
                    bitmap.SetPixel(0, 0, Color.Red);
                    bitmap.Save(temporaryPath, ImageFormat.Png);
                });
            Assert.NotNull(owned);

            using (var replacement = new Bitmap(2, 2))
            {
                replacement.SetPixel(0, 0, Color.Blue);
                replacement.Save(iconPath, ImageFormat.Png);
            }

            ShortcutService.DeleteDerivedIcon(owned, 3);

            Assert.True(File.Exists(iconPath));
            Assert.True(ShortcutService.IsReusablePng(iconPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateSortOrderAsync_ShouldUpdateOrders()
    {
        var (db, svc) = await InitAsync();

        var id1 = await svc.CreateShortcutAsync(new ShortcutItem { Name = "App 1", Path = "path1" });
        var id2 = await svc.CreateShortcutAsync(new ShortcutItem { Name = "App 2", Path = "path2" });

        await svc.UpdateSortOrderAsync(new List<int> { id2, id1 });

        var s1 = await svc.GetShortcutAsync(id1);
        var s2 = await svc.GetShortcutAsync(id2);

        Assert.Equal(1, s1!.SortOrder);
        Assert.Equal(0, s2!.SortOrder);

        Cleanup();
    }

    [Fact]
    public async Task GetAllShortcutsAsync_ShouldNormalizeDuplicateSortOrders()
    {
        var (db, svc) = await InitAsync();

        var id1 = await svc.CreateShortcutAsync(new ShortcutItem { Name = "App 1", Path = "path1", SortOrder = 0 });
        var id2 = await svc.CreateShortcutAsync(new ShortcutItem { Name = "App 2", Path = "path2", SortOrder = 0 });
        var id3 = await svc.CreateShortcutAsync(new ShortcutItem { Name = "App 3", Path = "path3", SortOrder = 0 });

        var shortcuts = await svc.GetAllShortcutsAsync();

        Assert.Equal(new[] { id1, id2, id3 }, shortcuts.Select(shortcut => shortcut.Id).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, shortcuts.Select(shortcut => shortcut.SortOrder).ToArray());

        var fetched = await svc.GetAllShortcutsAsync();
        Assert.Equal(new[] { 0, 1, 2 }, fetched.Select(shortcut => shortcut.SortOrder).ToArray());

        Cleanup();
    }

    [Fact]
    public async Task DeleteShortcutAsync_ShouldRemoveShortcut()
    {
        var (db, svc) = await InitAsync();

        var id = await svc.CreateShortcutAsync(new ShortcutItem { Name = "To Delete", Path = "path" });
        Assert.NotNull(await svc.GetShortcutAsync(id));

        await svc.DeleteShortcutAsync(id);
        Assert.Null(await svc.GetShortcutAsync(id));

        Cleanup();
    }
}
