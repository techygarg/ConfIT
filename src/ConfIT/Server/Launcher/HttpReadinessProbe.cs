using System.Net.Http;

namespace ConfIT.Server.Launcher;

internal sealed class HttpReadinessProbe : IReadinessProbe
{
    private readonly string _url;
    private readonly HttpClient _client;

    internal HttpReadinessProbe(string url, int perAttemptTimeoutMs = 2000)
    {
        _url    = url;
        _client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(perAttemptTimeoutMs) };
    }

    public bool TryProbe()
    {
        try
        {
            var response = _client.GetAsync(_url).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _client.Dispose();
}