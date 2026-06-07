using ConfIT.Config.AuthProvider;

namespace ConfIT.Config;

public sealed class IntegrationConfig
{
    public ApiConfig     Api     { get; set; } = new();
    public AuthConfig?   Auth    { get; set; }
    public FolderConfig? Folders { get; set; }
    public FilterConfig? Filter  { get; set; }
}
