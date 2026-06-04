using System.Net.Http;
using ConfIT.Contract;

namespace ConfIT.Config.AuthProvider;

internal sealed class OAuth2ClientCredentialsProvider : IAuthTokenProvider
{
    private readonly string _accessToken;
    private readonly string _headerKey;

    internal OAuth2ClientCredentialsProvider(AuthConfig auth, HttpClient? httpClient = null)
    {
        _headerKey   = auth.HeaderKey ?? "Authorization";
        _accessToken = FetchToken(auth, httpClient);
    }

    public string HeaderKey() => _headerKey;
    public string Token()     => $"Bearer {_accessToken}";

    private static string FetchToken(AuthConfig auth, HttpClient? httpClient)
    {
        if (!Uri.TryCreate(auth.TokenUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException(
                $"auth.tokenUrl '{auth.TokenUrl}' is not a valid absolute URL.");

        var ownsClient = httpClient is null;
        var client     = httpClient ?? new HttpClient();
        try
        {
            var response = PostToTokenEndpoint(client, auth);
            Console.WriteLine("response: {0}", response);
            return ExtractAccessToken(response, auth.TokenUrl!);
        }
        finally
        {
            if (ownsClient) client.Dispose();
        }
    }

    private static HttpResponseMessage PostToTokenEndpoint(HttpClient client, AuthConfig auth)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = auth.ClientId!,
            ["client_secret"] = auth.ClientSecret!,
        };
        if (!string.IsNullOrWhiteSpace(auth.Scope))
            fields["scope"] = auth.Scope;

        var response = client.PostAsync(auth.TokenUrl!, new FormUrlEncodedContent(fields))
                              .GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"OAuth2 token request to '{auth.TokenUrl}' failed with " +
                $"{(int)response.StatusCode} {response.ReasonPhrase}.");

        return response;
    }

    private static string ExtractAccessToken(HttpResponseMessage response, string tokenUrl)
    {
        var body  = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var json  = JObject.Parse(body);
        var token = json["access_token"]?.Value<string>();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                $"OAuth2 token response from '{tokenUrl}' did not contain an 'access_token' field.");

        Console.WriteLine($"token recieved: {token}");
        return token;
    }
}
