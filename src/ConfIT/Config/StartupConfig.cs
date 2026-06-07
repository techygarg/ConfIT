using ConfIT.Runner.Boot;

namespace ConfIT.Config;

public sealed class StartupConfig
{
    public const string InProcessMode = "in-process";
    public const string CommandMode   = "command";

    public string Mode { get; set; } = InProcessMode;
    public bool IsInProcess => Mode == InProcessMode;
    public bool IsCommand   => Mode == CommandMode;

    public string? Settings    { get; set; }
    public string? Command     { get; set; }
    public string? StopCommand { get; set; }
    public ReadinessConfig? Readiness { get; set; }
    public Dictionary<string, string>? Env { get; set; }
}
