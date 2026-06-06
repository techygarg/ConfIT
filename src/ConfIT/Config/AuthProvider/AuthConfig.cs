namespace ConfIT.Config.AuthProvider;

public sealed class AuthConfig
{
    public string? Type         { get; set; }  // "bearer" | "oauth2-client-credentials" | "api-key"
    public string? HeaderKey    { get; set; }  // optional override; defaults to "Authorization"
    public string? Token        { get; set; }  // bearer: token value
    public string? TokenUrl     { get; set; }  // oauth2: token endpoint URL
    public string? ClientId     { get; set; }  // oauth2: client ID
    public string? ClientSecret { get; set; }  // oauth2: client secret
    public string? Scope        { get; set; }  // oauth2: optional scope
    public string? Value        { get; set; }  // api-key: key value
}
