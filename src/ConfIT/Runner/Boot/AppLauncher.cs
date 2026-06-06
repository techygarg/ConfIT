using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConfIT.Runner.Boot;

public sealed class AppLauncher : IDisposable
{
    private readonly int _gracePeriodMs;
    private readonly OutputBuffer _outputBuffer;
    private readonly int? _port;
    private readonly Process _process;
    private readonly string? _stopCommand;
    private bool _disposed;

    private AppLauncher(Process process, OutputBuffer outputBuffer, int gracePeriodMs,
                        string? stopCommand, int? port)
    {
        _process     = process;
        _outputBuffer = outputBuffer;
        _gracePeriodMs = gracePeriodMs;
        _stopCommand  = stopCommand;
        _port         = port;
    }

    public IReadOnlyList<string> RecentOutput => _outputBuffer.GetLines();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_stopCommand is not null)
            RunStopCommand(_stopCommand);
        else
            KillProcess();

        try { _process.WaitForExit(_gracePeriodMs); } catch { /* ignore */ }
        if (!_process.HasExited)
            KillProcess();
        _process.Dispose();

        // Ensure the port is actually free before returning — the OS may hold it
        // briefly after the process exits. Both StopCommand and Kill paths wait here.
        WaitForPortRelease();
    }

    /// <summary>
    ///     Starts the process, blocks until the readiness probe succeeds, and returns a launcher.
    ///     <para>
    ///         The command string is passed to the OS shell (/bin/sh or cmd.exe).
    ///         Ensure <see cref="AppLauncherConfig.Command" /> comes from a trusted source
    ///         (a committed config file) — never from user-supplied input.
    ///     </para>
    /// </summary>
    public static AppLauncher Start(AppLauncherConfig config)
    {
        // Defensive guard for callers who construct AppLauncherConfig directly
        // (SuiteConfiguration.LoadComponent already validates before calling ToAppLauncherConfig).
        ValidateConfig(config);

        var port = ExtractPort(config.Readiness);
        if (port.HasValue)
            CheckPortAvailable(port.Value);

        var outputBuffer = new OutputBuffer();
        var process      = StartProcess(config, outputBuffer);

        WaitForReady(process, config, outputBuffer);

        return new AppLauncher(process, outputBuffer, config.GracePeriodSeconds * 1000,
                               config.StopCommand, port);
    }

    public static AppLauncher Start(string command, string readinessUrl, int timeoutSeconds = 30)
    {
        return Start(new AppLauncherConfig
        {
            Command   = command,
            Readiness = new ReadinessConfig { Url = readinessUrl, TimeoutSeconds = timeoutSeconds }
        });
    }

    #region Private helpers

    private void KillProcess()
    {
        if (!_process.HasExited)
            try { _process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* exited between check and kill */ }
    }

    private static void RunStopCommand(string stopCommand)
    {
        var psi = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo("cmd.exe")
            : new ProcessStartInfo("/bin/sh");

        psi.ArgumentList.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/c" : "-c");
        psi.ArgumentList.Add(stopCommand);
        psi.UseShellExecute = false;

        try
        {
            using var stop = Process.Start(psi);
            stop?.WaitForExit(5_000);
        }
        catch { /* best effort */ }
    }

    private void WaitForPortRelease()
    {
        if (_port is null) return;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 5)
        {
            var listener = new TcpListener(IPAddress.Loopback, _port.Value);
            try { listener.Start(); listener.Stop(); return; }
            catch (SocketException) { Thread.Sleep(100); }
        }
    }

    private static Process StartProcess(AppLauncherConfig config, OutputBuffer outputBuffer)
    {
        var process = new Process { StartInfo = BuildProcessStartInfo(config) };
        process.OutputDataReceived += (_, e) => outputBuffer.Append(e.Data);
        process.ErrorDataReceived  += (_, e) => outputBuffer.Append(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void WaitForReady(Process process, AppLauncherConfig config, OutputBuffer outputBuffer)
    {
        var readinessTarget = config.Readiness.Url ?? $"tcp://localhost:{config.Readiness.Port}";

        using var probe = CreateProbe(config.Readiness);
        var sw      = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(config.Readiness.TimeoutSeconds);

        while (sw.Elapsed < timeout)
        {
            if (process.HasExited)
                throw new AppLauncherException(
                    $"Process exited with code {process.ExitCode} before becoming ready.\n" +
                    string.Join('\n', outputBuffer.GetLines()));

            if (probe.TryProbe())
                return;

            Thread.Sleep(config.Readiness.IntervalMs);
        }

        try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
        throw new AppLauncherException(
            $"App did not become ready after {(int)sw.Elapsed.TotalSeconds}s.\n" +
            $"Command: {config.Command}\n" +
            $"Readiness: {readinessTarget}");
    }

    private static IReadinessProbe CreateProbe(ReadinessConfig readiness)
    {
        return readiness.Url is not null
            ? new HttpReadinessProbe(readiness.Url)
            : new TcpReadinessProbe("localhost", readiness.Port!.Value);
    }

    private static void ValidateConfig(AppLauncherConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Command))
            throw new ArgumentException("Command must not be empty.", nameof(config));
        if (config.Readiness.Url is null && config.Readiness.Port is null)
            throw new ArgumentException(
                "Readiness must specify either Url (HTTP probe) or Port (TCP probe).",
                nameof(config));
        if (config.Readiness.Url is not null && config.Readiness.Port is not null)
            throw new ArgumentException(
                "Readiness must specify either Url or Port, not both.",
                nameof(config));
    }

    private static int? ExtractPort(ReadinessConfig readiness)
    {
        if (readiness.Port.HasValue) return readiness.Port.Value;
        if (readiness.Url is not null
            && Uri.TryCreate(readiness.Url, UriKind.Absolute, out var uri))
            return uri.Port;
        return null;
    }

    private static void CheckPortAvailable(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try { listener.Start(); listener.Stop(); }
        catch (SocketException)
        {
            throw new AppLauncherException(
                $"Port {port} is already in use. Free it before starting the app.");
        }
    }

    private static ProcessStartInfo BuildProcessStartInfo(AppLauncherConfig config)
    {
        // UseShellExecute=false is required for stdout/stderr redirect.
        // Wrap command in OS shell so paths with spaces, env vars, and chained
        // commands work without manual escaping.
        var psi = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo("cmd.exe")
            : new ProcessStartInfo("/bin/sh");

        psi.ArgumentList.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/c" : "-c");
        psi.ArgumentList.Add(config.Command);

        psi.UseShellExecute        = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError  = true;

        foreach (var (key, value) in config.Env)
            psi.EnvironmentVariables[key] = value;

        return psi;
    }

    #endregion

    #region OutputBuffer

    private sealed class OutputBuffer
    {
        private const int MaxLines = 50;
        private readonly Queue<string> _lines = new();
        private readonly object _lock = new();

        internal void Append(string? line)
        {
            if (line is null) return;
            lock (_lock)
            {
                if (_lines.Count >= MaxLines)
                    _lines.Dequeue();
                _lines.Enqueue(line);
            }
        }

        internal IReadOnlyList<string> GetLines()
        {
            lock (_lock) { return _lines.ToList(); }
        }
    }

    #endregion
}
