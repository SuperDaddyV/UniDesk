namespace UniDesk.Services;

public static class StartupErrorMessageProvider
{
    public static StartupErrorText GetStartupFailure(string language, string logsDirectory) =>
        language switch
        {
            "en-US" => new(
                "UniDesk startup failed",
                $"UniDesk failed to start. See the log: {logsDirectory}"),
            "ja-JP" => new(
                "UniDesk の起動に失敗しました",
                $"UniDesk を起動できませんでした。ログを確認してください：{logsDirectory}"),
            "es-ES" => new(
                "Error de inicio de UniDesk",
                $"UniDesk no pudo iniciarse. Consulte el registro: {logsDirectory}"),
            _ => new(
                "UniDesk 启动失败",
                $"UniDesk 启动失败。请查看日志：{logsDirectory}")
        };

    public static StartupErrorText GetFatalFailure(string language, string logsDirectory) =>
        language switch
        {
            "en-US" => new(
                "UniDesk",
                $"UniDesk encountered an unrecoverable error and will exit. Log: {logsDirectory}"),
            "ja-JP" => new(
                "UniDesk",
                $"UniDesk で回復できないエラーが発生したため終了します。ログ：{logsDirectory}"),
            "es-ES" => new(
                "UniDesk",
                $"UniDesk encontró un error irrecuperable y se cerrará. Registro: {logsDirectory}"),
            _ => new(
                "UniDesk",
                $"UniDesk 遇到无法恢复的错误，即将退出。日志：{logsDirectory}")
        };
}

public readonly record struct StartupErrorText(string Title, string Message);
