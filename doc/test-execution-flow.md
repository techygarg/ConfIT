# Test Execution Flow

This document shows what happens from the moment a test suite starts to the moment it finishes. All three suite types — component (in-process), component (command), and integration — share the same test execution loop. They differ only in how the service under test is started.

---

## Component Test — In-Process Mode

The service boots inside the test process. No network ports, no external processes.

```
┌─────────────────────────────────────────────────────────────┐
│  xUnit starts fixture                                        │
│                                                             │
│  1. SuiteConfiguration.LoadComponent("suite.config.yaml")  │
│     • Validate config, resolve ${ENV_VAR} references        │
│     • Return ComponentConfig                                 │
│                                                             │
│  2. TestSuiteInitializer<Startup>(settings, overrides?)     │
│     • WebApplicationFactory boots service in-process        │
│     • Service uses InMemory DB / test config                 │
│     • Services.CreateScope() → seed test data               │
│                                                             │
│  3. WireMock starts on MockServerUrl port                    │
│     (managed by ConfIT per-test, not here)                   │
└───────────────────────┬─────────────────────────────────────┘
                        │
              ┌─────────▼──────────────────────────────────┐
              │  For each test case in TestCase/ folder:    │
              │                                             │
              │  Load test file (JSON or YAML)              │
              │    └── TestReader discovers .json / .yaml   │
              │                                             │
              │  Filter check                               │
              │    └── Skip if tag / name filter active     │
              │                                             │
              │  Mock setup                                 │
              │    └── HttpMockServer registers WireMock    │
              │        stubs from mock.interactions         │
              │                                             │
              │  HTTP request sent                          │
              │    └── TestHttpClient → in-process server  │
              │                                             │
              │  Response validated                         │
              │    ├── Status code check                    │
              │    ├── Body matchers (ignore/pattern/       │
              │    │   semantic) applied                    │
              │    └── Field-level diff reported on fail    │
              │                                             │
              │  Result recorded                            │
              │    └── TestResultCollector.Record(pass/fail)│
              └─────────────────────────────────────────────┘
                        │
              ┌─────────▼─────────────────┐
              │  xUnit disposes fixture    │
              │  TestResultCollector       │
              │    prints suite summary    │
              └────────────────────────────┘
```

---

## Component Test — Command Mode (AppLauncher)

The service runs as a real external process started by the test fixture. Works with any language or framework.

```
┌─────────────────────────────────────────────────────────────┐
│  xUnit starts fixture                                        │
│                                                             │
│  1. SuiteConfiguration.LoadComponent("suite.config.yaml")  │
│     • Validate config, resolve ${ENV_VAR} references        │
│     • Return ComponentConfig (mode: command)                 │
│                                                             │
│  2. AppLauncher.Start(cfg.ToAppLauncherConfig())            │
│     • Runs shell command (e.g. dotnet run --no-build ...)   │
│     • Injects env vars (ASPNETCORE_ENVIRONMENT, etc.)       │
│     • Polls readiness probe (HTTP 2xx or TCP port)          │
│       ┌── process exits early → throw with stderr tail      │
│       └── timeout elapsed → kill process, throw             │
│     • App starts, self-seeds data, configures from env      │
│                                                             │
│  3. TestHttpClient.Create(cfg.Api.Url)                       │
└───────────────────────┬─────────────────────────────────────┘
                        │
              ┌─────────▼──────────────────────────────────┐
              │  Same execution loop as in-process mode    │
              │  (load, filter, mock setup, send, validate, │
              │   record result)                            │
              └─────────────────────────────────────────────┘
                        │
              ┌─────────▼─────────────────┐
              │  xUnit disposes fixture    │
              │  AppLauncher.Dispose()     │
              │    kills process + port    │
              │  TestResultCollector       │
              │    prints suite summary    │
              └────────────────────────────┘
```

---

## Integration Test

Services run out-of-process (started by the CI pipeline or `make integration`). ConfIT connects to their URLs. `TEST_ENVIRONMENT` selects which environment config to use.

```
┌─────────────────────────────────────────────────────────────┐
│  Services already running (started externally)              │
│                                                             │
│  xUnit starts fixture                                        │
│                                                             │
│  1. SuiteConfiguration.LoadIntegration("suite.config.yaml") │
│     • Read TEST_ENVIRONMENT env var (or use default)        │
│     • Load named environment block (local / qa / staging)   │
│     • Resolve ${ENV_VAR} for URLs, tokens                   │
│     • Return IntegrationEnvironmentConfig                    │
│                                                             │
│  2. TestHttpClient.Create(cfg.Api.Url, authProvider)        │
│     • Points at the real running service                    │
│     • Auth token injected per request if provider set       │
└───────────────────────┬─────────────────────────────────────┘
                        │
              ┌─────────▼──────────────────────────────────┐
              │  Same execution loop                        │
              │  (no WireMock — real dependencies run)      │
              └─────────────────────────────────────────────┘
                        │
              ┌─────────▼─────────────────┐
              │  xUnit disposes fixture    │
              │  TestResultCollector       │
              │    prints suite summary    │
              └────────────────────────────┘
```

---

## The Common Execution Loop

Every suite type runs the same loop regardless of startup mode. One iteration per test case.

```
test case (name + body)
        │
        ▼
   Dependency check ── prereq failed/skipped ──→ ⏭ Skip (reason recorded in summary)
   TestDependencyStore.CheckPrerequisites()
   skips if any entry in depends: did not pass
        │
        ▼
   Filter check ──── filtered out ──→ ⏭ Skip (logged, not failed)
        │
        ▼
  ITestProcessor.Before()   (if registered — legacy, optional)
        │
        ▼
  Mock setup
  HttpMockServer.Initialize() registers WireMock stubs
  from mock.interactions in the test definition
        │
        ▼
  HTTP request built
  • method, path, query params, headers from test definition
  • body from inline body or bodyFromFile
  • {{injected}} variables substituted
  • ${ENV} variables substituted
        │
        ▼
  HTTP request sent  ──→  service under test
        │
        ◄──────────────────  HTTP response
        │
        ▼
  ITestProcessor.After()    (if registered — legacy, optional)
        │
        ▼
  Response validation
  ├── status code: FluentAssertions exact match
  ├── semantic matchers: isUuid, isIsoDate, greaterThan, …
  ├── pattern matchers: regex on named fields
  ├── ignore: fields stripped from both sides before diff
  └── body diff: JsonDiffPatch field-level comparison
        │
        ├── pass ──→ TestResultCollector.Record(Passed)
        │
        └── fail ──→ field-level error output printed
                     TestResultCollector.Record(Failed)
                     test marked failed in xUnit
```

---

## Suite Summary

After all tests run, the fixture's `Dispose()` is called. `TestResultCollector` prints a table grouped by source file:

```
══════════════════════════════════════════════════════
  Suite Summary
══════════════════════════════════════════════════════

  01-user-lifecycle.yaml
    ✗  CreateUser                                  580ms
    ⏭  GetUserById
         └─ prerequisite 'CreateUser' failed
    ⏭  GetUserByEmail
         └─ prerequisite 'CreateUser' failed

  02-user-errors.yaml
    ✓  GetUser_NotFoundByEmail                      43ms
    ✓  CreateUser_ValidationFailure                 89ms
    ✓  GetUser_NotFoundById                         31ms

──────────────────────────────────────────────────────
  Total: 6   ✓ 3 passed   ✗ 1 failed   ⏭ 2 skipped
──────────────────────────────────────────────────────
```

Skipped tests show a `└─` line naming the direct prerequisite that did not pass. See [Test Dependency Graph](./test-dependency-graph.md) for skip semantics and cascading behavior.

See [Reading Failure Output](./failure-output.md) for how to interpret per-field failure messages.
