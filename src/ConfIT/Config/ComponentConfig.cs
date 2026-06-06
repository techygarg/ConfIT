using ConfIT.Config.AuthProvider;
using ConfIT.Runner.Boot;

namespace ConfIT.Config;

public sealed class ComponentConfig
{
    public StartupConfig Startup { get; set; } = new();
    public ApiConfig     Api     { get; set; } = new();
    public AuthConfig?   Auth    { get; set; }
    public MockConfig?   Mock    { get; set; }
    public FolderConfig? Folders { get; set; }
    public FilterConfig? Filter  { get; set; }
}
