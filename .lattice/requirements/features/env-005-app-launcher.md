---
feature: AppLauncher
epic: Reduce Team Friction
status: draft
priority: P1
depends_on: []
personas:
  - component-test-author
  - platform-engineer
source_docs: []
---

# AppLauncher

## Problem Statement

When component tests run against an app started as an external process (the `command` mode introduced in ENV-004), something must start that process, wait for it to be ready, and stop it when the tests finish. ConfIT has no mechanism for this today. Without a first-class primitive, each team writes their own process management — Makefile targets, shell scripts, or ad hoc `Process.Start` calls in fixtures — with no consistent readiness check, no structured error messages when the app fails to start, and no guaranteed cleanup. The result: flaky test runs where tests execute before the app is ready, orphaned processes on CI when a test run crashes, and poor developer feedback when the app fails to start.

## User / Personas

**Component-test author** — wants to run component tests with the app as a real external process rather than in-process. Currently manages process lifecycle manually (Makefile, CI scripts). Wants a ConfIT-provided primitive that handles start, readiness, and stop with consistent behaviour and clear error messages.

**Platform/infra engineer** — standardises how test apps are started across teams. Wants one reliable mechanism that gives the same readiness guarantees and failure messages everywhere, rather than N different homegrown approaches.

## Scope

**In scope:**
- `AppLauncher` class: start a shell command as a child process, probe readiness, inject env vars, stop the process on dispose
- HTTP readiness probe: poll a URL until it returns HTTP 2xx or a timeout elapses
- TCP readiness probe: attempt a TCP connection to a host:port until it succeeds or a timeout elapses (for apps without HTTP health endpoints)
- Environment variable injection: values specified in the config are added to the child process's environment
- Stdout/stderr capture: buffer the last N lines of the process's output for inclusion in error messages
- Port-in-use pre-check: before starting the command, verify the target port is not already bound; fail fast with a descriptive error if it is
- Graceful teardown: on `Dispose()`, send a termination signal (kill the process), wait for a configurable grace period, force-kill if still running
- `AppLauncher.Start(AppLauncherConfig config)` — the primary entry point; returns an `AppLauncher` instance once the app is ready; throws if the app fails to start or become ready
- Cross-platform support: works on macOS, Linux, and Windows (CI environments)

**Out of scope:**
- Starting multiple processes — `AppLauncher` manages one process per instance; callers create multiple instances if needed
- Docker or container lifecycle management — process-level only, not container orchestration
- Automatic port assignment — the command and readiness URL specify the port explicitly
- App log streaming to test output during test execution — captured stderr/stdout is only used in error messages, not forwarded during normal execution
- Database migration or seed commands before app start — callers run those separately before calling `AppLauncher.Start`
- Process restarts if the app crashes mid-test — `AppLauncher` does not monitor the process after readiness is confirmed

## Boundary Conditions

- `AppLauncher.Start` blocks the calling thread until readiness is confirmed or the timeout elapses. It is not async — fixture constructors are synchronous in xUnit.
- The readiness probe polls on a configurable interval (default 500ms). Each poll attempt is independent; a single connection failure does not abort the probe loop.
- If the process exits before readiness is confirmed (crash on startup), `AppLauncher.Start` throws immediately without waiting for the full timeout.
- The port-in-use check is best-effort — it runs before the process is started, but there is an inherent TOCTOU race. It is a fast-fail convenience, not a reliability guarantee.
- Environment variables injected via `AppLauncherConfig.Env` are additive — they extend the current process's environment, not replace it. If a key in `Env` already exists in the environment, the config value overrides it for the child process only.
- On `Dispose()`, the grace period is the time waited between sending the termination signal and force-killing. If the process exits before the grace period elapses, `Dispose` returns immediately.
- Stderr/stdout buffering is bounded — the last 50 lines are kept in memory. Earlier output is discarded.

## Assumptions

- `System.Diagnostics.Process` is the mechanism for process management on all target platforms.
- `HttpClient` (a fresh instance per probe attempt, or a shared instance with short timeout) is used for HTTP readiness probes.
- `System.Net.Sockets.TcpClient` is used for TCP readiness probes.
- The command string is passed to the OS shell for execution (i.e., environment variable expansion in the command string is handled by the shell, not by `AppLauncher`).
- `AppLauncher` targets the same .NET versions as the ConfIT library (`net9.0`, `net10.0`).
- `IDisposable` is the lifecycle contract — the caller is responsible for wrapping `AppLauncher` in a `using` statement or disposing it explicitly in the fixture's `Dispose()` method.

## Scenarios

### Scenario 1: App starts and becomes ready within the timeout

The happy path — process starts, readiness probe succeeds, tests run.

**Acceptance Criteria:**
- Given an `AppLauncherConfig` with a valid command, a readiness HTTP URL, and a timeout of 30 seconds
- When `AppLauncher.Start(config)` is called
- And the started process binds to its port and the readiness URL returns HTTP 2xx within the timeout
- Then `AppLauncher.Start` returns an `AppLauncher` instance without throwing
- And subsequent HTTP requests to the app's API URL succeed

### Scenario 2: App fails to become ready within the timeout

The process starts but never responds at the readiness URL — misconfigured port, slow startup, missing dependency.

**Acceptance Criteria:**
- Given an `AppLauncherConfig` where the app takes longer than the configured `timeoutSeconds` to become ready
- When `AppLauncher.Start(config)` is called
- Then an exception is thrown after the timeout elapses
- And the exception message includes: the command that was run, the readiness URL that was polled, and the elapsed time

### Scenario 3: Process exits before becoming ready

The app crashes on startup — missing config file, failed DB migration, port conflict inside the process.

**Acceptance Criteria:**
- Given an `AppLauncherConfig` where the command starts a process that immediately exits with a non-zero exit code
- When `AppLauncher.Start(config)` is called
- Then an exception is thrown immediately without waiting for the full timeout
- And the exception message includes the process exit code and the last captured lines of stderr output

### Scenario 4: Target port is already in use before launch

A previous test run left an orphaned process, or another service occupies the port.

**Acceptance Criteria:**
- Given an `AppLauncherConfig` where the readiness URL's port is already bound by another process
- When `AppLauncher.Start(config)` is called
- Then an exception is thrown before the command is executed
- And the exception message identifies the port number and states that it is already in use

### Scenario 5: Dispose stops the process and releases the port

The test suite finishes and the fixture disposes `AppLauncher`.

**Acceptance Criteria:**
- Given a running `AppLauncher` instance returned by `AppLauncher.Start`
- When `Dispose()` is called
- Then the child process receives a termination signal
- And if the process exits within the grace period, `Dispose` returns without force-killing
- And if the process does not exit within the grace period, it is force-killed before `Dispose` returns
- And the port the app was bound to is no longer in use after `Dispose` returns

### Scenario 6: Environment variables from config are visible in the started process

The startup command needs `ASPNETCORE_ENVIRONMENT` set to the component test profile without modifying the consumer's shell environment.

**Acceptance Criteria:**
- Given an `AppLauncherConfig` with an `env` map containing `ASPNETCORE_ENVIRONMENT: ComponentTest`
- When `AppLauncher.Start(config)` is called
- Then the child process has `ASPNETCORE_ENVIRONMENT` set to `ComponentTest`
- And this value is not set in the calling process's environment after `AppLauncher.Start` returns

*(Scenarios ordered chronologically — natural implementation sequence.)*

## Implementation Notes

1. **`AppLauncherConfig`** — DTO with fields: `Command` (string), `Readiness` (sub-object: `Url` string, `Port` int?, `TimeoutSeconds` int, `IntervalMs` int default 500), `Env` (Dictionary<string, string>), `GracePeriodSeconds` (int default 5). Readiness can specify either `Url` (HTTP probe) or `Port` (TCP probe) — exactly one required.

2. **`AppLauncher.Start(AppLauncherConfig config)`** — static factory: (a) port-in-use check if port determinable from config, (b) create and start `System.Diagnostics.Process` with `UseShellExecute = false`, `RedirectStandardOutput = true`, `RedirectStandardError = true`, env vars merged in, (c) start background threads to buffer last 50 lines of stdout/stderr, (d) enter readiness probe loop until success, timeout, or process exit, (e) throw descriptively on failure, (f) return `AppLauncher` instance on success.

3. **Readiness probe loop** — `while (elapsed < timeout && process.HasExited == false)`: attempt HTTP GET (2xx = success) or TCP connect (connected = success); on failure sleep `IntervalMs`; check `process.HasExited` after each failed attempt to enable immediate crash detection.

4. **Port-in-use check** — attempt `TcpListener.Start()` on the target port; if it throws `SocketException` with `AddressAlreadyInUse`, throw the descriptive error before starting the process.

5. **`Dispose()`** — call `process.Kill(entireProcessTree: true)` (cross-platform in .NET 5+), wait up to `GracePeriodSeconds` for exit via `process.WaitForExit(gracePeriodMs)`, then `process.Kill()` again if still running. Dispose `HttpClient` if created for probing.

6. **Unit tests** — test the readiness probe logic using a loopback TCP listener as a controllable stand-in; test the crash detection path by starting a process that exits immediately; test env var injection by starting a process that echoes its environment.

## Open Questions

- [ ] Should `AppLauncher` expose a `Logs` property (the buffered stdout/stderr) for consumers who want to inspect app output after a test failure? Recommendation: yes — a `IReadOnlyList<string> RecentOutput { get; }` property adds no complexity and significantly helps debugging.
- [ ] Should `AppLauncher.Start` be overloaded to accept a plain command string with defaults (no config object) for the simplest case? Recommendation: yes, add `AppLauncher.Start(string command, string readinessUrl, int timeoutSeconds = 30)` as a convenience overload.

## Links

- Design: [env-005-app-launcher.md](../../context/env-005-app-launcher.md)
- Epic index: [reduce-team-friction.md](../epics/reduce-team-friction.md)
