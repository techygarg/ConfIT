using ConfIT.Config.AuthProvider;

namespace ConfIT.UnitTest.Config.AuthProvider;

public class ApiKeyAuthTokenProviderTests
{
    [Fact]
    public void HeaderKey_ReturnsConfiguredHeaderName()
    {
        var provider = new ApiKeyAuthTokenProvider("X-API-Key", "secret");
        Assert.Equal("X-API-Key", provider.HeaderKey());
    }

    [Fact]
    public void Token_ReturnsRawKeyValue()
    {
        var provider = new ApiKeyAuthTokenProvider("X-API-Key", "secret-value");
        Assert.Equal("secret-value", provider.Token());
    }
}