using System.Net.Sockets;

namespace ConfIT.Runner.Boot;

internal sealed class TcpReadinessProbe : IReadinessProbe
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;

    internal TcpReadinessProbe(string host, int port, int perAttemptTimeoutMs = 2000)
    {
        _host = host;
        _port = port;
        _timeoutMs = perAttemptTimeoutMs;
    }

    public bool TryProbe()
    {
        using var client = new TcpClient();
        try
        {
            return client.ConnectAsync(_host, _port).Wait(_timeoutMs);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}
