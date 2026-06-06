using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ConfIT.Contract;
using ConfIT.Model;

namespace ConfIT.Runner.Http;

public class TestHttpClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly IAuthTokenProvider? _tokenProvider;

    public TestHttpClient(HttpClient client, IAuthTokenProvider? tokenProvider = null)
    {
        _client        = client ?? throw new ArgumentNullException(nameof(client));
        _tokenProvider = tokenProvider;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    public async Task<HttpResponseMessage> Execute(TestApi testApi)
    {
        if (testApi == null) throw new ArgumentNullException(nameof(testApi));

        var method = testApi.Request.Method.ToUpper() switch
        {
            "GET"    => HttpMethod.Get,
            "PUT"    => HttpMethod.Put,
            "PATCH"  => HttpMethod.Patch,
            "POST"   => HttpMethod.Post,
            "DELETE" => HttpMethod.Delete,
            var m    => throw new NotSupportedException($"HTTP method '{m}' is not supported.")
        };

        var request = new HttpRequestMessage(method, testApi.Request.Path);
        AddHeaders(request, testApi.Request.Headers);

        if (method != HttpMethod.Get && method != HttpMethod.Delete)
            request.Content = RequestBody(testApi.Request.Body);

        return await _client.SendAsync(request);
    }

    private void AddHeaders(HttpRequestMessage request, Dictionary<string, string>? headers)
    {
        if (headers is { Count: > 0 })
            foreach (var (name, value) in headers)
                if (!string.IsNullOrEmpty(value))
                    request.Headers.TryAddWithoutValidation(name, value);

        var token = _tokenProvider?.Token();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation(_tokenProvider!.HeaderKey(), token);
    }

    private static StringContent RequestBody(JToken? body) =>
        new(body?.ToString() ?? string.Empty, Encoding.UTF8, "application/json");

    public static TestHttpClient Create(string serverUrl, IAuthTokenProvider? authTokenProvider)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentException("Server URL cannot be null or empty", nameof(serverUrl));

        return new TestHttpClient(
            new HttpClient { BaseAddress = new Uri(serverUrl) },
            authTokenProvider);
    }
}
