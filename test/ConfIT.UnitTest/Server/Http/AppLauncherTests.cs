using System.Net.Sockets;
using ConfIT.Runner.Boot;

namespace ConfIT.UnitTest.Server.Http;

public class AppLauncherTests
{
    #region Validation

    [Fact]
    public void Start_EmptyCommand_ThrowsArgumentException()
    {
        var config = new AppLauncherConfig
        {
            Command   = "",
            Readiness = new ReadinessConfig { Port = TestPort.GetFree() }
        };
        Assert.Throws<ArgumentException>(() => AppLauncher.Start(config));
    }

    [Fact]
    public void Start_NeitherUrlNorPort_ThrowsArgumentException()
    {
        var config = new AppLauncherConfig { Command = "echo hello" };
        Assert.Throws<ArgumentException>(() => AppLauncher.Start(config));
    }

    [Fact]
    public void Start_BothUrlAndPort_ThrowsArgumentException()
    {
        var config = new AppLauncherConfig
        {
            Command   = "echo hello",
            Readiness = new ReadinessConfig { Url = "http://localhost:5000/", Port = 5000 }
        };
        Assert.Throws<ArgumentException>(() => AppLauncher.Start(config));
    }

    #endregion

    #region Port-in-use

    [Fact]
    public void Start_PortAlreadyInUse_ThrowsBeforeProcessStarts()
    {
        var port = TestPort.GetFree();
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            var config = new AppLauncherConfig
            {
                Command   = "echo hello",
                Readiness = new ReadinessConfig { Port = port }
            };
            var ex = Assert.Throws<AppLauncherException>(() => AppLauncher.Start(config));
            Assert.Contains(port.ToString(), ex.Message);
            Assert.Contains("already in use", ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }

    #endregion

    #region Crash detection

    [Fact]
    public void Start_ProcessExitsWithErrorBeforeReady_ThrowsWithExitCode()
    {
        var config = new AppLauncherConfig
        {
            Command   = "exit 1",
            Readiness = new ReadinessConfig { Port = TestPort.GetFree(), TimeoutSeconds = 10, IntervalMs = 50 }
        };
        var ex = Assert.Throws<AppLauncherException>(() => AppLauncher.Start(config));
        Assert.Contains("exited with code", ex.Message);
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void Start_ProcessExitsBeforeReady_ThrowsWithExitedMessage()
    {
        var config = new AppLauncherConfig
        {
            Command   = "exit 0",
            Readiness = new ReadinessConfig { Port = TestPort.GetFree(), TimeoutSeconds = 5, IntervalMs = 50 }
        };
        var ex = Assert.Throws<AppLauncherException>(() => AppLauncher.Start(config));
        Assert.Contains("before becoming ready", ex.Message);
    }

    #endregion

    #region Convenience overload

    [Fact]
    public void Start_StringOverloadWithEmptyCommand_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => AppLauncher.Start("", "http://localhost:5000/health"));
    }

    #endregion
}

public class TcpReadinessProbeTests
{
    [Fact]
    public void TryProbe_PortListening_ReturnsTrue()
    {
        var port = TestPort.GetFree();
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            using var probe = new TcpReadinessProbe("localhost", port);
            Assert.True(probe.TryProbe());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void TryProbe_NothingListening_ReturnsFalse()
    {
        var port = TestPort.GetFree();
        using var probe = new TcpReadinessProbe("localhost", port, 300);
        Assert.False(probe.TryProbe());
    }
}

public class HttpReadinessProbeTests
{
    [Fact]
    public void TryProbe_ServerReturns200_ReturnsTrue()
    {
        var port = TestPort.GetFree();
        var url  = $"http://localhost:{port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        Task.Run(() =>
        {
            var ctx = listener.GetContext();
            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
        });

        using var probe = new HttpReadinessProbe(url);
        Assert.True(probe.TryProbe());

        listener.Stop();
    }

    [Fact]
    public void TryProbe_ServerReturns500_ReturnsFalse()
    {
        var port = TestPort.GetFree();
        var url  = $"http://localhost:{port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        Task.Run(() =>
        {
            var ctx = listener.GetContext();
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
        });

        using var probe = new HttpReadinessProbe(url);
        Assert.False(probe.TryProbe());

        listener.Stop();
    }

    [Fact]
    public void TryProbe_NoServer_ReturnsFalse()
    {
        var port = TestPort.GetFree();
        using var probe = new HttpReadinessProbe($"http://localhost:{port}/health", 300);
        Assert.False(probe.TryProbe());
    }
}

internal static class TestPort
{
    internal static int GetFree()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
