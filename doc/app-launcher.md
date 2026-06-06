# AppLauncher

`AppLauncher` starts an external process from a shell command, blocks until a readiness probe passes, exposes the running process for the duration of the test suite, and kills it cleanly on dispose. It is the mechanism behind `mode: command` in `suite.config.yaml`.

---

## The Core Philosophy

> **ConfIT runs your command. Your API takes care of itself.**

With command mode, ConfIT does one thing: it executes the command you give it, waits for the service to be ready, and speaks HTTP. That's the entire contract.

Your API is responsible for everything inside its boundary — seeding test data, configuring itself for the test environment, cleaning up on exit. ConfIT never reaches into the application. It holds no reference to it, knows nothing about its database, and imports nothing from its codebase.

This has three practical consequences:

1. **Write once, test anything.** The same `suite.config.yaml`, the same test definitions, and the same matchers work against a Go API, a Node.js service, a Python microservice, or a .NET application. The technology stack of the service under test is irrelevant to the test project.

2. **Test project setup is minimal.** Your test project references only `ConfIT`. No application code, no DI container wiring, no service override callbacks. The fixture is a dozen lines that load a config file and point at a URL.

3. **The application owns its test environment.** Seeding, schema setup, in-memory database selection, pointing dependencies at WireMock — all of that lives in the application, gated by an environment variable or launch profile. This keeps test concerns inside the component that actually knows about them.

---

## Why Command Mode Matters: Language-Agnostic Testing

In-process mode (`TestSuiteInitializer`) only works with .NET services — the test project must reference the application and boot it via ASP.NET Core's `TestServer`. Command mode removes that constraint entirely.

Because `AppLauncher` runs any shell command, ConfIT can test APIs written in Go, Node.js, Python, Java, or any other language. The test definitions (JSON/YAML), matchers, mock interactions, and assertions are identical regardless of what is serving HTTP on the other end. The test project references only `ConfIT` — never the application under test.

---

## In-Process vs Command Mode

| | In-process (`TestSuiteInitializer`) | Command (`AppLauncher`) |
|---|---|---|
| App language | .NET only | Any language |
| Startup speed | Fast (~1 s) | Slower (real process boot) |
| Test project | References app internals | No app reference — HTTP only |
| Service overrides | Via `Action<IServiceCollection>` | App manages via env/config |
| Data seeding | `initializer.Services.CreateScope()` | App seeds itself on startup |
| Port management | Not needed (in-process) | Port must be free before test run |

For in-process setup, see [Suite Setup](./suite-setup.md).

---

## How the App Manages Its Test Environment

In command mode, ConfIT has no reach into the application. The application is responsible for configuring itself correctly when started for component tests.

The standard pattern is an environment variable. For .NET:

```json
"ASPNETCORE_ENVIRONMENT": "ComponentTest"
```

The application reads this at startup and applies component-test-specific configuration: an in-memory database instead of a file or hosted DB, dependencies pointed at the WireMock URL, any test-specific feature flags.

### Self-Seeding Pattern

Because the test project holds no reference to the application, it cannot reach in to create schema, run migrations, or seed reference data. The application seeds its own test data as part of startup when running in component-test mode.

From `User.Api/Startup.cs`:

```csharp
protected virtual void AddDbContexts(IServiceCollection services)
{
    if (IsLocalComponentTestsRunning(Configuration))
        services.AddDbContext<UserDbContext>(opt => opt.UseInMemoryDatabase("UserDb"));
    else
        services.AddDbContext<UserDbContext>(opt => opt.UseSqlite(@"Data Source=User.db"));
}

public static bool IsLocalComponentTestsRunning(IConfiguration configuration) =>
    configuration.GetValue("IsLocalComponentTests", false);
```

The `IsLocalComponentTests` flag comes from `appsettings.ComponentTest.json`. When the flag is set the app uses an in-memory database; when absent it uses the real SQLite file. No test project code drives this.

Tests that need an entity create it via HTTP in their own test definition. Error and validation tests typically need no setup at all.

---

## Setting Up Command Mode

### Step 1 — Add a launch profile to your API

Add a `ComponentTest` profile to `Properties/launchSettings.json`. Setting `dotnetRunMessages: false` reduces noise in captured process output.

```json
"ComponentTest": {
  "commandName": "Project",
  "dotnetRunMessages": false,
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "ComponentTest",
    "ASPNETCORE_URLS": "http://localhost:5170"
  }
}
```

📄 Live example: [`User.Api/Properties/launchSettings.json`](../example/User.Api/Properties/launchSettings.json)

For non-.NET applications, the equivalent is passing an environment variable directly on the command line or in `suite.config.yaml` under `startup.env`.

### Step 2 — Add environment-specific config to your API

For .NET, add `appsettings.ComponentTest.json`. This file is loaded automatically when `ASPNETCORE_ENVIRONMENT=ComponentTest`.

```json
{
  "IsLocalComponentTests": "true",
  "JustAnotherService": {
    "Url": "http://localhost:8888"
  }
}
```

The mock URL (`http://localhost:8888`) must match the WireMock URL in `suite.config.yaml`.

📄 Live example: [`User.Api/appsettings.ComponentTest.json`](../example/User.Api/appsettings.ComponentTest.json)

For other languages, the equivalent is a test-specific config file or environment variables that re-point downstream service clients at the WireMock URL.

### Step 3 — Add `suite.config.yaml` to your test project

```yaml
component:
  startup:
    mode: command
    command: dotnet run --no-build --project ../../../../User.Api --launch-profile ComponentTest
    # stopCommand: OS-specific command that kills the server and releases its port on dispose.
    # If omitted, AppLauncher kills the process tree and waits for port release automatically.
    # Unix/macOS: target only the LISTENING server — not all processes with the port open:
    stopCommand: lsof -ti :5170 -sTCP:LISTEN | xargs kill -9
    # Windows:    taskkill /F /IM User.Api.exe
    readiness:
      port: 5170          # TCP probe — app is ready when port 5170 responds
      timeoutSeconds: 60  # allow time for dotnet build on first run
      intervalMs: 500
  api:
    url: http://localhost:5170
  mock:
    url: http://localhost:8888
  folders:
    response: responses
  filter:
    strategy: tags
    envVariable: TEST_TAGS
```

The `command` path is relative to the test output directory (e.g., `bin/Debug/net10.0/`). Use `--no-build` only after a separate pre-build step — see [Makefile Pre-Build Pattern](#makefile-pre-build-pattern).

`suite.config.yaml` must be copied to the output directory:

```xml
<None Update="suite.config.yaml">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</None>
```

📄 Live example: [`User.ComponentTests.AppLauncher/suite.config.yaml`](../example/User.ComponentTests.AppLauncher/suite.config.yaml)

### Step 4 — Write the fixture

The fixture has no reference to the application project. `SuiteBootstrapper.ForCommand` reads the config, starts the launcher, builds the HTTP client, and manages the full lifecycle:

```csharp
public class TestSuiteFixture : IDisposable
{
    private readonly BootstrappedSuite _suite;

    public TestSuiteFixture() =>
        _suite = SuiteBootstrapper.ForCommand("suite.config.yaml");

    public TestSuiteContext Context => _suite.Context;

    public void Dispose() => _suite.Dispose();
}
```

`ForCommand` blocks until the readiness probe passes (or throws `AppLauncherException` on timeout or premature exit). `Dispose` shuts down the process, waits for port release, then prints the suite summary.

There is no `TestSuiteInitializer`, no `InitializeDb`, and no reference to `User.Api` anywhere in the fixture or project file.

If you need to pass `CustomMatchers` or a non-standard config source, use the [adapter chain pattern](./suite-setup.md#adapter-chain-custom-wiring) with `AppLauncher.Start(cfg.ToAppLauncherConfig())` directly instead of `ForCommand`.

📄 Live example: [`User.ComponentTests.AppLauncher/SetUp/TestSuiteFixture.cs`](../example/User.ComponentTests.AppLauncher/SetUp/TestSuiteFixture.cs)

The test class itself is identical to other component test classes:

📄 Live example: [`User.ComponentTests.AppLauncher/UserComponentTests.cs`](../example/User.ComponentTests.AppLauncher/UserComponentTests.cs)

---

## Stopping the Process

When `Dispose()` is called, `AppLauncher` stops the running process in one of two ways.

**Default behaviour (no `stopCommand`):** Calls `Kill(entireProcessTree: true)` on the shell wrapper process, then polls the port until it is free before returning. This works reliably in most cases but can leave orphan grandchild processes on some platforms (e.g. when the shell spawns `dotnet run` which spawns the app — killing the shell may not kill all descendants).

**`stopCommand` (recommended when running sequential suites):** Runs the provided command and then polls the port until it is free. Use this when the default kill leaves stale processes that block subsequent test suites from binding the same port.

```yaml
startup:
  stopCommand: lsof -ti :5170 -sTCP:LISTEN | xargs kill -9   # Unix/macOS
  # Windows:  taskkill /F /IM User.Api.exe
```

**Critical:** on Unix, use `-sTCP:LISTEN` with `lsof`. Without it, `lsof -ti :5170` returns every process that has port 5170 open — including the **test runner itself**, which holds client connections to the same port. Killing the test runner causes an abrupt "test host crashed" failure. The `-sTCP:LISTEN` filter restricts the result to processes in the `LISTEN` state, which is only the server.

In both cases `AppLauncher` waits for the port to be bindable before `Dispose()` returns, so the caller can immediately start the next service on the same port without a race.

---

## Readiness Probes

Exactly one probe type must be specified per `readiness` block.

**TCP probe** — waits until a TCP connection to `localhost:<port>` is accepted. Use when the application does not expose a health endpoint.

```yaml
readiness:
  port: 5170
  timeoutSeconds: 60
  intervalMs: 500
```

**HTTP probe** — polls a URL until it returns a 2xx response. Use when the application exposes a health or ready endpoint and you want to verify the full HTTP stack is up.

```yaml
readiness:
  url: http://localhost:5170/health
  timeoutSeconds: 60
  intervalMs: 500
```

Both probe types check on each interval whether the process has already exited. If it has, `AppLauncherException` is thrown immediately with the exit code and any captured output — no need to wait for the full timeout.

When using the HTTP probe URL form, `AppLauncher` also extracts the port from the URL and verifies it is free before starting the process. The TCP probe always does this check.

---

## Diagnosing Startup Failures

`AppLauncher` captures the last 50 lines of stdout and stderr from the started process in a rolling buffer. The buffer is accessible via `RecentOutput` at any point after `Start()` returns.

```csharp
var launcher = AppLauncher.Start(cfg.ToAppLauncherConfig());
Console.WriteLine(string.Join('\n', launcher.RecentOutput));
```

When the process exits before becoming ready, `AppLauncherException` is thrown and the captured output is embedded in the exception message automatically — you do not need to read `RecentOutput` in that case. `RecentOutput` is most useful for diagnosing intermittent failures during an otherwise-successful run, or for printing process output in a test fixture teardown when tests fail unexpectedly.

---

## Direct Use (Without `suite.config.yaml`)

If you are not using `SuiteConfiguration.LoadComponent`, you can construct `AppLauncherConfig` directly.

**Convenience overload** (URL probe):

```csharp
var launcher = AppLauncher.Start(
    "dotnet run --launch-profile ComponentTest",
    "http://localhost:5170/health",
    timeoutSeconds: 60);
```

**Full config** (TCP probe, stop command, extra env):

```csharp
var launcher = AppLauncher.Start(new AppLauncherConfig
{
    Command     = "dotnet run --launch-profile ComponentTest",
    StopCommand = "lsof -ti :5170 -sTCP:LISTEN | xargs kill -9",  // Unix/macOS
    Readiness = new ReadinessConfig
    {
        Port           = 5170,
        TimeoutSeconds = 60,
        IntervalMs     = 500
    },
    Env = new Dictionary<string, string>
    {
        ["ASPNETCORE_ENVIRONMENT"] = "ComponentTest"
    },
    GracePeriodSeconds = 5   // default; how long Dispose waits after Kill
});
```

`Env` entries are added to the process environment on top of the inheriting environment. Use this to pass env vars that are not set by the launch profile.

---

## Makefile Pre-Build Pattern

`dotnet run --no-build` requires the project to already be built. If you run the component tests against a freshly-checked-out repo, the first run will fail.

The recommended pattern is a Makefile target that builds the application first:

```makefile
component.applauncher: ## Run AppLauncher component tests
    @lsof -ti :5170 -sTCP:LISTEN 2>/dev/null | xargs kill -9 2>/dev/null || true
    @dotnet build example/User.Api --configuration Debug -v minimal -nologo
    @$(DOTNET) test example/User.ComponentTests.AppLauncher $(TEST_OPTS)
```

The `lsof` line ensures port 5170 is free before the test run starts. `AppLauncher` also checks this at startup and will throw `AppLauncherException` if the port is occupied, but the Makefile check avoids a less informative error when a previous run left a stale process. The `-sTCP:LISTEN` flag is required — without it, `lsof` would also target client-side connections held by any currently-running test runner.

📄 Live example: [`Makefile`](../Makefile) — `component.applauncher` target
