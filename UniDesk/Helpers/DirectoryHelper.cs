namespace UniDesk.Helpers;

public static class DirectoryHelper
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string AppDataPath = System.IO.Path.Combine(LocalAppData, "UniDesk");
    private static readonly string LegacyAppDataPath = System.IO.Path.Combine(LocalAppData, "LumiDesk");

    public static string AppData => AppDataPath;
    public static string DataDirectory => AppDataPath;
    public static string DatabaseFile => System.IO.Path.Combine(AppDataPath, "UniDesk.db");
    public static string IconsDirectory => System.IO.Path.Combine(AppDataPath, "icons");
    public static string LogsDirectory => System.IO.Path.Combine(AppDataPath, "logs");
    public static string CacheDirectory => System.IO.Path.Combine(AppDataPath, "cache");

    public static void EnsureDirectoriesExist()
    {
        MigrateLegacyDataIfNeeded();

        if (!System.IO.Directory.Exists(AppDataPath))
            System.IO.Directory.CreateDirectory(AppDataPath);

        if (!System.IO.Directory.Exists(IconsDirectory))
            System.IO.Directory.CreateDirectory(IconsDirectory);

        if (!System.IO.Directory.Exists(LogsDirectory))
            System.IO.Directory.CreateDirectory(LogsDirectory);

        if (!System.IO.Directory.Exists(CacheDirectory))
            System.IO.Directory.CreateDirectory(CacheDirectory);
    }

    private static void MigrateLegacyDataIfNeeded()
    {
        MigrateLegacyDataIfNeeded(
            LegacyAppDataPath,
            AppDataPath,
            DatabaseFile,
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UniDesk-migration-error.log"));
    }

    internal static void MigrateLegacyDataIfNeeded(
        string legacyAppDataPath,
        string appDataPath,
        string databaseFile,
        string fallbackLog)
    {
        if (System.IO.File.Exists(databaseFile) ||
            !System.IO.Directory.Exists(legacyAppDataPath))
        {
            return;
        }

        try
        {
            if (!System.IO.Directory.Exists(appDataPath))
            {
                System.IO.Directory.CreateDirectory(appDataPath);
            }

            CopyDirectoryWithoutOverwrite(legacyAppDataPath, appDataPath);

            var legacyDatabase = System.IO.Path.Combine(legacyAppDataPath, "LumiDesk.db");
            if (System.IO.File.Exists(legacyDatabase) && !System.IO.File.Exists(databaseFile))
            {
                System.IO.File.Copy(legacyDatabase, databaseFile, overwrite: false);
            }
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(
                    fallbackLog,
                    $"[{DateTime.Now:O}] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
            }
            catch
            {
            }

            throw new System.IO.IOException(
                $"无法迁移旧版 LumiDesk 用户数据，已停止启动以避免创建不完整的数据副本。诊断日志：{fallbackLog}",
                ex);
        }
    }

    private static void CopyDirectoryWithoutOverwrite(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in System.IO.Directory.EnumerateDirectories(sourceDirectory, "*", System.IO.SearchOption.AllDirectories))
        {
            var relativePath = System.IO.Path.GetRelativePath(sourceDirectory, directory);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(sourceDirectory, "*", System.IO.SearchOption.AllDirectories))
        {
            var relativePath = System.IO.Path.GetRelativePath(sourceDirectory, file);
            var targetFile = System.IO.Path.Combine(targetDirectory, relativePath);
            var targetFileDirectory = System.IO.Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetFileDirectory))
            {
                System.IO.Directory.CreateDirectory(targetFileDirectory);
            }

            if (!System.IO.File.Exists(targetFile))
            {
                System.IO.File.Copy(file, targetFile, overwrite: false);
            }
        }
    }
}
