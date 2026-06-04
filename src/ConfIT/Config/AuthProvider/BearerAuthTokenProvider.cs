using ConfIT.Contract;

namespace ConfIT.Config.AuthProvider;

internal sealed class BearerAuthTokenProvider : IAuthTokenProvider
{
    private readonly string _token;
    private readonly string _headerKey;

    internal BearerAuthTokenProvider(string token, string? headerKey)
    {
        _token     = token;
        _headerKey = headerKey ?? "Authorization";
    }

    public string HeaderKey() => _headerKey;
    public string Token()     => $"Bearer {_token}";
}