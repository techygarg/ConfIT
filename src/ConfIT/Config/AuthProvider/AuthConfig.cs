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
    
    
    public void ValidateAuth(string section, string filePath)
    {
        Validate.OneOf(this.Type, $"{section}.auth.type", filePath, "bearer", "oauth2-client-credentials", "api-key");

        switch (Type)
        {
            case "bearer":
                Validate.Required(Token, $"{section}.auth.token", filePath);
                break;
            case "oauth2-client-credentials":
                Validate.Required(TokenUrl,     $"{section}.auth.tokenUrl",     filePath);
                Validate.Required(ClientId,     $"{section}.auth.clientId",     filePath);
                Validate.Required(ClientSecret, $"{section}.auth.clientSecret", filePath);
                break;
            case "api-key":
                Validate.Required(HeaderKey, $"{section}.auth.headerKey", filePath);
                Validate.Required(Value,     $"{section}.auth.value",     filePath);
                break;
        }
    }

}