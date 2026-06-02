---
feature: AppLauncher
requirement_doc: .lattice/requirements/features/env-005-app-launcher.md
created: 2026-06-02
---

# AppLauncher

> Start an external process before component tests run — readiness probing, env injection, graceful teardown. Powers the `command` mode in ENV-004 suite configuration.

## Design: Level 1 — Capabilities

1. A test fixture can start an external app process and block until it is ready — `AppLauncher.Start` does not return until the readiness probe succeeds.
2. Readiness confirmed via HTTP (2xx health endpoint) OR TCP port probe — consumer picks one.
3. Injected environment variables reach the child process without touching the calling process's environment.
4. Startup failures (crash, timeout, port-in-use) produce distinct, descriptive exceptions before any test runs.
5. Disposing the launcher stops the process cleanly — SIGTERM → grace period → force kill, no orphaned processes.
6. `RecentOutput` exposes last captured stdout/stderr lines for debugging failures without CI log diving.

---

## Design: Level 2 — Components

| Component | Layer | Responsibility |
|---|---|---|
| `AppLauncherConfig` | Infrastructure (`src/ConfIT/Server/Http/`) | Public configuration DTO: command string, `ReadinessConfig` sub-object, env vars map, grace period seconds |
| `AppLauncher` | Infrastructure (`src/ConfIT/Server/Http/`) | Public API: static `Start` factory, lifecycle owner of child process, exposes `RecentOutput`, implements `IDisposable` |
| `IReadinessProbe` | Infrastructure (internal, nested) | Interface: `bool TryProbe()` — decouples probe loop from HTTP vs TCP strategy |
| `HttpReadinessProbe` | Infrastructure (internal) | `IReadinessProbe` impl — HTTP GET to readiness URL, returns true on 2xx |
| `TcpReadinessProbe` | Infrastructure (internal) | `IReadinessProbe` impl — TCP connect attempt to host:port, returns true on success |
| `OutputBuffer` | Infrastructure (private nested class) | Thread-safe ring buffer of last 50 lines from process stdout+stderr; appended by background threads |

```
Consumer Fixture
    │
    │  AppLauncher.Start(config)
    ▼
┌─────────────────────────────────────────────────┐
│  AppLauncher                  Server/Http/       │
│  + static Start(AppLauncherConfig)               │
│  + static Start(string, string, int)             │
│  + IReadOnlyList<string> RecentOutput            │
│  + IDisposable                                   │
│                                                  │
│  ┌───────────────────┐  ┌───────────────────┐   │
│  │ HttpReadiness     │  │ TcpReadiness      │   │
│  │ Probe (internal)  │  │ Probe (internal)  │   │
│  │ : IReadinessProbe │  │ : IReadinessProbe │   │
│  └───────────────────┘  └───────────────────┘   │
│                                                  │
│  ┌──────────────────────────────────────────┐   │
│  │ OutputBuffer (private nested)            │   │
│  │ lock-protected, last 50 lines            │   │
│  └──────────────────────────────────────────┘   │
└──────────────────────┬───────────────────────────┘
                       │ System.Diagnostics.Process
                       ▼
               Child process (app under test)
```

**Key decision: IReadinessProbe internal strategy pattern** — `AppLauncher`'s probe loop calls `probe.TryProbe()` without knowing HTTP vs TCP. Each probe type independently testable with a loopback listener.

---

## Design: Level 3 — Interactions

**Flow 1: `AppLauncher.Start(config)`**

```
Start(config)
  ├─1─ Validate config (Readiness has exactly one of Url/Port; Command non-empty)
  ├─2─ Port-in-use check → TcpListener.Start(port) → SocketException → throw
  ├─3─ Build ProcessStartInfo
  │     OS detection: /bin/sh -c <cmd>  OR  cmd.exe /c <cmd>
  │     UseShellExecute=false, RedirectStdout=true, RedirectStderr=true
  │     Merge config.Env additively into StartInfo.Environment
  ├─4─ Create OutputBuffer
  ├─5─ Wire OutputDataReceived + ErrorDataReceived → outputBuffer.Append
  ├─6─ process.Start() → BeginOutputReadLine() → BeginErrorReadLine()
  ├─7─ Create IReadinessProbe from Readiness.Url (HTTP) or Readiness.Port (TCP)
  └─8─ Probe loop → SUCCESS: return AppLauncher(process, outputBuffer)
                  → CRASH:   throw (exitCode + stderr tail)
                  → TIMEOUT: throw (command + readiness target + elapsed)
```

**Flow 2: Probe loop**

```
while (elapsed < timeout && !process.HasExited):
  probe.TryProbe()
    HTTP: GET url (2s) → 2xx → true  |  exception/non-2xx → false
    TCP:  ConnectAsync(host, port, 2s) → true  |  exception → false
  if true → exit loop (ready)
  if process.HasExited → throw immediately with exitCode + outputBuffer lines
  Thread.Sleep(intervalMs)
if timeout → throw with command + readiness URL + elapsed time
```

**Flow 3: `Dispose()`**

```
if process null or HasExited → return
process.Kill(entireProcessTree: true)   ← SIGKILL on Unix, TerminateProcess on Windows
process.WaitForExit(gracePeriodSeconds * 1000)
process.Dispose()
```

Note: .NET has no cross-platform SIGTERM — SIGKILL is correct for test fixtures. Grace period is OS confirmation time, not app drain time.

**Flow 4: Convenience overload**

```
Start(string command, string readinessUrl, int timeoutSeconds = 30)
  → AppLauncherConfig { Command=command, Readiness={Url=readinessUrl, TimeoutSeconds=timeout} }
  → Start(config)
```

**Command execution note:** `UseShellExecute=false` is required for stdout/stderr redirect. Command string is wrapped in OS shell (`/bin/sh -c` or `cmd.exe /c`) to support paths, env expansion, and chained commands — matches spec's "passed to OS shell" requirement.

---

## Decisions Log

<!-- Add new at bottom. Never remove. -->

| Date | Decision | Reasoning | Alternatives Considered |
|------|----------|-----------|------------------------|
| 2026-06-02 | `IReadinessProbe` internal strategy pattern | Keeps `AppLauncher`'s probe loop clean — calls `probe.TryProbe()` without knowing HTTP vs TCP. Each strategy independently testable with a loopback listener. | Two private methods in `AppLauncher` — simpler but gives `AppLauncher` two probe concerns. |
| 2026-06-02 | Command string wrapped in OS shell (`/bin/sh -c` or `cmd.exe /c`) | Supports quoted paths, env var expansion in command, and chained commands. Matches spec requirement "passed to OS shell". Required because `UseShellExecute=false` is needed for stdout/stderr redirect. | Token split (`command.Split(' ', 2)`) — breaks on paths with spaces. |
| 2026-06-02 | SIGKILL (not SIGTERM) on `Dispose()` | .NET's `Process` API has no cross-platform SIGTERM. For test fixtures, immediate kill is correct — no graceful HTTP drain needed, port must be freed quickly. | P/Invoke `kill(pid, SIGTERM)` on Unix — adds platform-specific code for negligible test benefit. |
| 2026-06-02 | Single `AppLauncherException` type (not subtypes per failure mode) | Three failure modes (port-in-use, crash, timeout) are distinguished by message content. Consumers rarely need to catch a specific failure mode programmatically in test code. Keeps the public API surface minimal. | Separate `AppLauncherPortInUseException`, `AppLauncherCrashException`, `AppLauncherTimeoutException` — more type-safe but adds three public types for marginal benefit in test infrastructure. |
| 2026-06-02 | Design approved at Level 4. Blueprint complete — ready for implementation. | All four levels reviewed and confirmed. Constraints and key files recorded. | — |
| 2026-06-02 | `IReadinessProbe` extended with `IDisposable` (minor deviation from L4 contracts) | `HttpReadinessProbe` owns an `HttpClient` that must be disposed after the probe loop. Adding `IDisposable` to the internal interface allows `using var probe = ...` in `Start()`. The deviation is internal-only — no public API change. | Pass `HttpClient` as a constructor argument from `Start()` scope — works but couples `Start()` to the probe's internal implementation. |
| 2026-06-02 | `ProcessStartInfo.ArgumentList` used instead of `Arguments` string | `ArgumentList` passes `-c` and the command as separate OS-level arguments, eliminating the need to escape quotes in the command string. Available in .NET Core 3.0+, so fully compatible with net9.0/net10.0. | `Arguments` string — requires manual quote-escaping which breaks commands containing quotes. |
| 2026-06-02 | `OutputBuffer` kept as private nested class; tested indirectly | Blueprint specified private nested. Direct unit testing requires making it internal, which widens visibility. Observable behaviour (crash message contains stderr lines, `RecentOutput`) covers the key contract without exposing internals. | Make `OutputBuffer` internal for direct testing — unnecessary for a 20-line ring buffer with a single lock. |

## Design: Level 4 — Contracts

### File: `src/ConfIT/Server/Http/AppLauncher.cs`

```csharp
// ── Public configuration ─────────────────────────────────────────────────

public sealed class AppLauncherConfig
{
    public string Command { get; init; } = string.Empty;
    public ReadinessConfig Readiness { get; init; } = new();
    public Dictionary<string, string> Env { get; init; } = new();
    public int GracePeriodSeconds { get; init; } = 5;
}

public sealed class ReadinessConfig
{
    public string? Url { get; init; }    // HTTP probe — one of Url/Port required
    public int? Port { get; init; }       // TCP probe  — one of Url/Port required
    public int TimeoutSeconds { get; init; } = 30;
    public int IntervalMs { get; init; } = 500;
}

// ── Public exception ──────────────────────────────────────────────────────

public sealed class AppLauncherException : Exception
{
    public AppLauncherException(string message) : base(message) { }
}
// Three message shapes (not subtypes):
// Port in use: "Port {port} is already in use. Free it before starting the app."
// Crash:       "Process exited with code {code} before becoming ready.\n{stderr tail}"
// Timeout:     "App did not become ready after {elapsed}s.\nCommand: {cmd}\nReadiness: {target}"

// ── Public API ────────────────────────────────────────────────────────────

public sealed class AppLauncher : IDisposable
{
    private AppLauncher(Process process, OutputBuffer outputBuffer);

    public static AppLauncher Start(AppLauncherConfig config);

    public static AppLauncher Start(
        string command,
        string readinessUrl,
        int timeoutSeconds = 30);

    public IReadOnlyList<string> RecentOutput { get; }

    public void Dispose();
}

// ── Internal probe strategies ─────────────────────────────────────────────

internal interface IReadinessProbe
{
    bool TryProbe();
}

internal sealed class HttpReadinessProbe : IReadinessProbe
{
    internal HttpReadinessProbe(string url, int perAttemptTimeoutMs = 2000);
    public bool TryProbe();    // GET → 2xx → true; any failure → false
}

internal sealed class TcpReadinessProbe : IReadinessProbe
{
    internal TcpReadinessProbe(string host, int port, int perAttemptTimeoutMs = 2000);
    public bool TryProbe();    // ConnectAsync → true; any failure → false
}

// ── Private nested ────────────────────────────────────────────────────────

// OutputBuffer: lock-protected ring buffer, last 50 lines stdout+stderr
// Append(string? line) / IReadOnlyList<string> GetLines()
```

---

## Design Summary

**Components and layer assignments:**
- `AppLauncherConfig` / `ReadinessConfig` — Infrastructure, `src/ConfIT/Server/Http/`. Public configuration DTOs.
- `AppLauncherException` — Infrastructure, same file. Single public exception type for all launch failures.
- `AppLauncher` — Infrastructure, same file. Public API: two `Start` factory overloads, `RecentOutput`, `IDisposable`.
- `IReadinessProbe` + `HttpReadinessProbe` + `TcpReadinessProbe` — Infrastructure (internal). Strategy pattern for HTTP vs TCP probing.
- `OutputBuffer` — private nested class inside `AppLauncher`. Thread-safe ring buffer, last 50 lines.

**Key contracts:**
- `AppLauncher.Start(AppLauncherConfig)` — primary entry point, blocks until ready, throws `AppLauncherException` on failure
- `AppLauncher.Start(string, string, int)` — convenience overload for simple HTTP cases
- `AppLauncher.RecentOutput` — `IReadOnlyList<string>` of buffered process output
- `AppLauncherException` — single exception type, three distinct message shapes for port-in-use / crash / timeout

**Architectural constraints:**
- Command string wrapped in OS shell (`/bin/sh -c` or `cmd.exe /c`) — supports real-world commands with paths, env vars, chaining
- SIGKILL via `process.Kill(entireProcessTree: true)` on dispose — .NET has no cross-platform SIGTERM; immediate kill is correct for test fixtures
- `IReadinessProbe` is internal — probe strategy never exposed; `AppLauncher`'s public surface is only `Start`, `RecentOutput`, `Dispose`
- `OutputBuffer` is a private nested class — implementation detail, not testable directly; tested via `RecentOutput` in integration tests

**Open questions resolved:**
- `RecentOutput` property: yes, include it — helps debugging failures without CI log access
- Convenience overload `Start(string, string, int)`: yes, include — lowest-friction entry point for simple cases
- SIGTERM vs SIGKILL: SIGKILL only — .NET limitation; documented as a constraint, not a gap

**Design status: Approved — ready for implementation.**

---

## Decisions Log

## Constraints

- `IReadinessProbe` and probe implementations are internal — never exposed in the public API.
- Command string must be wrapped in OS shell to support paths with spaces and env var expansion. Direct token-split is NOT acceptable.
- `process.Kill(entireProcessTree: true)` only — no SIGTERM. Documented limitation, not a gap.
- `OutputBuffer` capped at 50 lines — prevents memory growth during long-running fixtures.
- `AppLauncher.Start` is synchronous (blocking) — fixture constructors in xUnit are synchronous; no async path needed.
- All public types (`AppLauncher`, `AppLauncherConfig`, `ReadinessConfig`, `AppLauncherException`) live in one file.

## Key Files

- `src/ConfIT/Server/Http/AppLauncher.cs` — all public types + internal probe strategies + private OutputBuffer (new file, 164/164 unit tests pass)
- `test/ConfIT.UnitTest/Server/Http/AppLauncherTests.cs` — validation, port-in-use, crash detection, probe strategy tests (12 new tests)

