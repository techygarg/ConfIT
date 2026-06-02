namespace ConfIT.Server.Launcher;

public sealed class AppLauncherConfig
{
    public string Command { get; init; } = string.Empty;
    public ReadinessConfig Readiness { get; init; } = new();
    public Dictionary<string, string> Env { get; init; } = new();
    public int GracePeriodSeconds { get; init; } = 5;
}