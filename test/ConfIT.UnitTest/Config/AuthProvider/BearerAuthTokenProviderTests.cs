using ConfIT.Config.AuthProvider;

namespace ConfIT.UnitTest.Config.AuthProvider;

public class BearerAuthTokenProviderTests
{
    [Fact]
    public void HeaderKey_NoOverride_ReturnsAuthorization()
    {
        var provider = new BearerAuthTokenProvider("tok", null);
        Assert.Equal("Authorization", provider.HeaderKey());
    }

    [Fact]
    public void HeaderKey_WithOverride_ReturnsOverride()
    {
        var provider = new BearerAuthTokenProvider("tok", "X-Auth-Token");
        Assert.Equal("X-Auth-Token", provider.HeaderKey());
    }

    [Fact]
    public void Token_ReturnsBearerPrefixedValue()
    {
        var provider = new BearerAuthTokenProvider("abc123", null);
        Assert.Equal("Bearer abc123", provider.Token());
    }
}