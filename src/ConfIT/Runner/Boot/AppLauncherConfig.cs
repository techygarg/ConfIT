namespace ConfIT.Runner.Boot;

public sealed class AppLauncherConfig
{
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// Optional OS-specific command that cleanly stops the started process and releases its port.
    /// If not set, AppLauncher kills the process tree and waits for port release automatically.
    /// Unix/macOS example:  "lsof -ti :5170 -sTCP:LISTEN | xargs kill -9"
    ///   (-sTCP:LISTEN targets only the listening server, not test runner connections)
    /// Windows example:     "taskkill /F /IM MyApi.exe"
    /// </summary>
    public string? StopCommand { get; init; }

    public ReadinessConfig Readiness { get; init; } = new();
    public Dictionary<string, string> Env { get; init; } = new();
    public int GracePeriodSeconds { get; init; } = 5;
}
