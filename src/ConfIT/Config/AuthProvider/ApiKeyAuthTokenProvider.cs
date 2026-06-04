using ConfIT.Contract;

namespace ConfIT.Config.AuthProvider;

internal sealed class ApiKeyAuthTokenProvider : IAuthTokenProvider
{
    private readonly string _headerKey;
    private readonly string _value;

    internal ApiKeyAuthTokenProvider(string headerKey, string value)
    {
        _headerKey = headerKey;
        _value     = value;
    }

    public string HeaderKey() => _headerKey;
    public string Token()     => _value;
}