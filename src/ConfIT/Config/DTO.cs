namespace ConfIT.Config;
using Server.Boot;

public sealed class ComponentConfig
{
    public StartupConfig Startup { get; set; } = new();
    public ApiConfig Api { get; set; } = new();
    public MockConfig? Mock { get; set; }
    public FolderConfig? Folders { get; set; }
    public FilterConfig? Filter { get; set; }
}

public sealed class IntegrationEnvironmentConfig
{
    public ApiConfig Api { get; set; } = new();
    public FolderConfig? Folders { get; set; }
    public FilterConfig? Filter { get; set; }
}

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

public sealed class ApiConfig
{
    public string? Url       { get; set; }
    public string? AuthToken { get; set; }
}

public sealed class MockConfig
{
    public string? Url { get; set; }
}

public sealed class FolderConfig
{
    public string? Response     { get; set; }
    public string? RequestBody  { get; set; }
    public string? ResponseBody { get; set; }
}

public sealed class FilterConfig
{
    public string? Strategy    { get; set; }
    public string? EnvVariable { get; set; }
}