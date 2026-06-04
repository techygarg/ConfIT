using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConfIT.Config.AuthProvider;

namespace ConfIT.UnitTest.Config.AuthProvider;

public class OAuth2ClientCredentialsProviderTests
{
    [Fact]
    public void HeaderKey_NoOverride_ReturnsAuthorization()
    {
        var provider = new OAuth2ClientCredentialsProvider(
            OAuth2Auth(), FakeClient("""{"access_token":"tok"}""", HttpStatusCode.OK));

        Assert.Equal("Authorization", provider.HeaderKey());
    }

    [Fact]
    public void HeaderKey_WithOverride_ReturnsOverride()
    {
        var provider = new OAuth2ClientCredentialsProvider(
            OAuth2Auth(headerKey: "X-Token"),
            FakeClient("""{"access_token":"tok"}""", HttpStatusCode.OK));

        Assert.Equal("X-Token", provider.HeaderKey());
    }

    [Fact]
    public void Token_ReturnsBearerPrefixedAccessToken()
    {
        var provider = new OAuth2ClientCredentialsProvider(
            OAuth2Auth(), FakeClient("""{"access_token":"fetched-token"}""", HttpStatusCode.OK));

        Assert.Equal("Bearer fetched-token", provider.Token());
    }

    [Fact]
    public void Constructor_NonSuccessResponse_ThrowsWithUrlAndStatus()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new OAuth2ClientCredentialsProvider(
                OAuth2Auth(), FakeClient("{}", HttpStatusCode.Unauthorized)));

        Assert.Contains("https://auth/token", ex.Message);
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public void Constructor_MissingAccessTokenField_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new OAuth2ClientCredentialsProvider(
                OAuth2Auth(), FakeClient("""{"token_type":"Bearer"}""", HttpStatusCode.OK)));
    }

    [Fact]
    public void Constructor_InvalidTokenUrl_ThrowsBeforeHttpCall()
    {
        var auth = new AuthConfig
        {
            Type         = "oauth2-client-credentials",
            TokenUrl     = "not-a-url",
            ClientId     = "id",
            ClientSecret = "secret"
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new OAuth2ClientCredentialsProvider(auth));

        Assert.Contains("not-a-url", ex.Message);
        Assert.Contains("valid absolute URL", ex.Message);
    }

    private static AuthConfig OAuth2Auth(string? headerKey = null) => new()
    {
        Type         = "oauth2-client-credentials",
        TokenUrl     = "https://auth/token",
        ClientId     = "id",
        ClientSecret = "secret",
        HeaderKey    = headerKey
    };

    private static HttpClient FakeClient(string responseBody, HttpStatusCode statusCode) =>
        new(new FakeHttpHandler(responseBody, statusCode));

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string         _body;
        private readonly HttpStatusCode _status;

        internal FakeHttpHandler(string body, HttpStatusCode status)
        {
            _body   = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            };
            return Task.FromResult(response);
        }
    }
}
