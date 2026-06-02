---
feature: Modern Test Host Initialization
requirement_doc: .lattice/requirements/features/env-003-modern-test-host-initialization.md
created: 2026-06-01
---

# Modern Test Host Initialization

> Modernize `TestSuiteInitializer` from the legacy `WebHost.CreateDefaultBuilder` + `TestServer` pattern to `WebApplicationFactory<TProgram>`, enabling minimal-hosting app support and eliminating the Startup subclass pattern for service overrides.

## Design: Level 1 — Capabilities

1. A component test suite can be wired to any modern ASP.NET Core app — both minimal-hosting and Startup-class apps work with the same `TestSuiteInitializer<TProgram>` API.
2. Service overrides are expressed inline via `Action<IServiceCollection>` callback — no separate `TestServerStartup.cs` subclass file required.
3. The DI container is accessible after initialization via `initializer.Services` (`IServiceProvider`) for fixture-level seeding.
4. Test-specific app settings load via a filename string — existing behaviour preserved.
5. Old constructor and `TestServer` property remain with `[Obsolete]` annotation — deprecation path, not a break.

---

## Design: Level 2 — Components

| Component | Layer | Responsibility |
|---|---|---|
| `TestSuiteInitializer<TProgram>` | Infrastructure (`src/ConfIT/Server/Http/`) | Public API: constructs in-process server, exposes `TestHttpClient` + `Services`, manages lifetime via `IDisposable`. Wraps `InternalFactory`. |
| `InternalFactory<TProgram>` | Infrastructure (private nested class inside above) | Concrete `WebApplicationFactory<TProgram>` subclass. Owns `ConfigureWebHost` — applies settings file + service override callback after app's own `Startup.ConfigureServices` runs. Never exposed outside `TestSuiteInitializer`. |
| `WebApplicationFactory<TProgram>` | External (`Microsoft.AspNetCore.Mvc.Testing`) | Creates in-process ASP.NET Core server from app's `Program` entry point. Boots app via its normal path (Startup-class or minimal hosting). `InternalFactory` inherits from this. |
| `TestHttpClient` | Infrastructure (`src/ConfIT/Server/Http/`) | Wraps `HttpClient` from `factory.CreateClient()`. **Unchanged.** |

```
Consumer Fixture (TestSuiteFixture.cs)
        │
        │  new TestSuiteInitializer<Program>(settingsFile, configureServices?)
        ▼
┌───────────────────────────────────────────────────────┐
│  TestSuiteInitializer<TProgram>                       │  src/ConfIT/Server/Http/
│  • TestHttpClient { get; }                            │
│  • IServiceProvider Services { get; }                 │
│  • IDisposable                                        │
│                                                       │
│  ┌───────────────────────────────────────────────┐   │
│  │  InternalFactory<TProgram>        (private)   │   │
│  │  : WebApplicationFactory<TProgram>            │   │
│  │                                               │   │
│  │  ConfigureWebHost(builder):                   │   │
│  │    1. load settingsFile                       │   │
│  │    2. apply configureServices callback        │   │
│  │       (runs after app's ConfigureServices)    │   │
│  └──────────────┬────────────────────────────────┘   │
└─────────────────┼─────────────────────────────────────┘
                  │ boots app via Program entry point
                  │ (Startup-class OR minimal hosting — both work)
          ┌───────┴─────────┐
          │ HttpClient      │ → TestHttpClient
          │ IServiceProvider│ → .Services
          └─────────────────┘
     (Microsoft.AspNetCore.Mvc.Testing)
```

**Key design decision: wrap, not extend.** `TestSuiteInitializer` holds `InternalFactory` privately. Public surface is only `TestHttpClient`, `Services`, and `IDisposable` — ConfIT does not expose the `WebApplicationFactory` API.

---

## Design: Level 3 — Interactions

**Flow 1: Fixture construction**

```
TestSuiteFixture.ctor
  │
  │  new TestSuiteInitializer<Program>(settingsFile, configureServices?)
  ▼
TestSuiteInitializer.ctor
  │  new InternalFactory(settingsFile, configureServices?)
  │  (server lazy — not started yet)
  │
  │  .Services accessed  ← triggers server start
  ▼
InternalFactory.ConfigureWebHost(builder) called by framework
  │  → builder.ConfigureAppConfiguration: clears sources, loads settingsFile only
  │  → builder.ConfigureServices: invokes configureServices? callback
  │       (runs AFTER app's Startup.ConfigureServices or minimal hosting registrations)
  │       e.g. removes DbContextOptions<UserDbContext>, adds InMemory replacement
  │
  ├──► factory.Services → IServiceProvider → initializer.Services
  │     └── fixture: services.CreateScope()
  │                  → GetRequiredService<UserDbContext>()
  │                  → dbInitializer.Seed()
  │
  └──► factory.CreateClient() → HttpClient
        └── new TestHttpClient(httpClient) → initializer.TestHttpClient
```

**Flow 2: Test execution (unchanged)**

```
BaseTest.Execute(testName, testCase, sourceFile)
  │  uses fixture.TestHttpClient (unchanged surface)
  ▼
TestHttpClient → HttpClient → in-process server
  │  middleware + controllers run as normal
  │  in-memory DB in scope
  ▼
Response → ResultMatcher → pass/fail → TestResultCollector
```

**Lazy startup note:** First access of `.Services` or `.TestHttpClient` (whichever comes first in fixture ctor) triggers the server. Both happen in the fixture constructor, so order is deterministic and controllable.

---

## Design: Level 4 — Contracts

### `TestSuiteInitializer<TProgram>` — `src/ConfIT/Server/Http/TestSuiteInitializer.cs`

```csharp
public class TestSuiteInitializer<TProgram> : IDisposable
    where TProgram : class          // was: class, IDisposable — constraint relaxed
{
    public TestSuiteInitializer(
        string settingsFile,
        Action<IServiceCollection>? configureServices = null);

    public TestHttpClient TestHttpClient { get; }

    // Root IServiceProvider. For scoped services use Services.CreateScope().
    public IServiceProvider Services { get; }

    public void Dispose();

    [Obsolete("Use Services (IServiceProvider) instead.")]
    public TestServer TestServer { get; }
}
```

### `InternalFactory` — private nested class inside `TestSuiteInitializer<TProgram>`

```csharp
private sealed class InternalFactory : WebApplicationFactory<TProgram>
{
    internal InternalFactory(string settingsFile, Action<IServiceCollection>? configureServices);

    protected override void ConfigureWebHost(IWebHostBuilder builder);
    // config.Sources.Clear(); config.AddJsonFile(settingsFile)
    // configureServices?.Invoke(services)  — runs after app's ConfigureServices
}
```

### Package change — `ConfIT.csproj`

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.x"/>   <!-- net9.0 -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0"/>  <!-- net10.0 -->
<!-- Microsoft.AspNetCore.TestHost: transitive dep of Mvc.Testing; evaluate removal -->
```

### Consumer migration

```csharp
// Delete: TestServerStartup.cs
// Before: new TestSuiteInitializer<TestServerStartup>("appsettings.Tests.json")
// After:
new TestSuiteInitializer<Program>(
    "appsettings.Tests.json",
    services =>
    {
        services.Remove(services.Single(d => d.ServiceType == typeof(DbContextOptions<UserDbContext>)));
        services.AddDbContext<UserDbContext>(opt => opt.UseInMemoryDatabase("UserDb"));
    });

// Services access (scoped):
using var scope = initializer.Services.CreateScope();
scope.ServiceProvider.GetRequiredService<UserDbContext>();
```

---

## Design Summary

**Components and layer assignments:**
- `TestSuiteInitializer<TProgram>` — Infrastructure, `src/ConfIT/Server/Http/`. Public API surface. Wraps `InternalFactory`, exposes `TestHttpClient` + `IServiceProvider Services`.
- `InternalFactory` — Infrastructure, private nested class. Extends `WebApplicationFactory<TProgram>`. Owns `ConfigureWebHost`.
- `WebApplicationFactory<TProgram>` — External (`Microsoft.AspNetCore.Mvc.Testing`). In-process server lifecycle.
- `TestHttpClient` — Infrastructure, `src/ConfIT/Server/Http/`. Unchanged.

**Key contracts:**
- `TestSuiteInitializer<TProgram>(string settingsFile, Action<IServiceCollection>? configureServices = null)`
- `TestHttpClient TestHttpClient { get; }` — unchanged
- `IServiceProvider Services { get; }` — replaces `TestServer TestServer`
- `TestServer` retained as `[Obsolete]` for one version

**Architectural constraints:**
- `TestSuiteInitializer` wraps (does not extend) `WebApplicationFactory` — ConfIT does not expose the Mvc.Testing API surface
- Generic constraint relaxed from `class, IDisposable` to `class` — this is a **runtime-breaking** change on the generic parameter; code using `TestSuiteInitializer<TestServerStartup>` compiles but fails at runtime
- Configuration sources cleared before loading settingsFile — preserves existing behaviour, no key merging with app's `appsettings.json`
- `configureServices` callback runs after app's own service registration — app boots unchanged, callback post-patches only test overrides

**Files changed:**
- `src/ConfIT/Server/Http/TestSuiteInitializer.cs` — rewrite
- `src/ConfIT/ConfIT.csproj` — add `Mvc.Testing`, evaluate removing direct `TestHost`
- `example/User.ComponentTests/SetUp/TestSuiteFixture.cs` — update constructor call and `InitializeDb`
- `example/User.ComponentTests/SetUp/TestServerStartup.cs` — **delete**

**Open questions resolved:**
- Wrap vs extend → wrap. Controls surface, aligns with ConfIT philosophy.
- Config loading → clear sources, load only settingsFile. Preserves existing behaviour.
- `TProgram` covers both Startup-class and minimal hosting apps uniformly.

**Design status: Approved — ready for implementation.**

---

## Decisions Log

<!-- Add new at bottom. Never remove. -->

| Date | Decision | Reasoning | Alternatives Considered |
|------|----------|-----------|------------------------|
| 2026-06-01 | `TestSuiteInitializer` wraps `WebApplicationFactory` (not extends) | ConfIT controls the public surface — only `TestHttpClient`, `Services`, `IDisposable` are exposed. Extending would leak the full `WebApplicationFactory` API which ConfIT has no contract to maintain. | Extend `WebApplicationFactory<TProgram>` directly — simpler, one class, but leaks API surface. |
| 2026-06-01 | Generic parameter changes from `TStartUp` to `TProgram` | `WebApplicationFactory<TProgram>` works at the entry point level. Startup-class apps and minimal-hosting apps both use `Program` as `TProgram`. One API covers both worlds. | Keep `TStartUp` and require consumers to register a Startup-equivalent — breaks minimal hosting apps. |
| 2026-06-01 | Configuration sources cleared before loading settingsFile | Current `TestSuiteInitializer` creates a fresh `IConfigurationRoot` from only the settings file. `WebApplicationFactory` would otherwise stack on top of the app's `appsettings.json`. Clearing sources preserves existing behaviour — only the test settings file is active. | Let app's default config load and override with test file — more production-like, but changes existing behaviour and could cause unexpected key merging. |
| 2026-06-01 | `Action<IServiceCollection>` callback runs after app's own `ConfigureServices` | This is `WebApplicationFactory`'s built-in behaviour via `ConfigureWebHost`. The app boots exactly as production does; the callback post-patches only what needs to differ. `TestServerStartup.cs` subclass pattern is eliminated entirely. | Override app services before Startup runs — not possible with `WebApplicationFactory`; would require replacing the startup entirely. |
| 2026-06-01 | Design approved at Level 4. Blueprint complete — ready for implementation. | All four levels reviewed and confirmed. Constraints and key files recorded. | — |
| 2026-06-01 | `TProgram` for Startup-class apps must be `Startup`, not `Program` | `User.Api.Program` is `public static class` — C# cannot use static types as generic type arguments. `WebApplicationFactory<Startup>` falls back to `UseStartup<Startup>()`, which is exactly the production hosting path. Minimal-hosting apps use `Program` (with `public partial class Program {}`). | Use `Program` for all apps — blocked by static class constraint. |
| 2026-06-01 | Settings file resolved to absolute path eagerly in `InternalFactory` constructor | `WebApplicationFactory` sets content root to the app's source directory, not the test output directory. Relative paths for `appsettings.Tests.json` would resolve to the wrong location. Capturing `Path.GetFullPath(settingsFile)` in the constructor (while cwd is still the test output dir) and using `PhysicalFileProvider` bypasses content root resolution. | Use relative path with `config.AddJsonFile` — resolves to app source dir, file not found. |
| 2026-06-01 | EF Core two-provider conflict resolved via config-driven Startup | Removing `DbContextOptions<UserDbContext>` and re-adding with `AddDbContext<T>(...UseInMemoryDatabase)` still leaves SQLite's internal `IDatabaseProvider` services registered, causing EF Core to throw "only one database provider allowed". Fix: `Startup.AddDbContexts` reads `IsLocalComponentTests` config flag (already `true` in `appsettings.Tests.json`) and chooses InMemory vs SQLite at service registration time — no service manipulation in callback needed. | Use `configureServices` callback to swap DbContext — works for service descriptor removal, fails on EF Core internal provider services conflict. |
| 2026-06-01 | `Microsoft.EntityFrameworkCore.InMemory` added to `User.Api.csproj` | The config-driven Startup approach calls `UseInMemoryDatabase` from inside `Startup.AddDbContexts`. This call lives in `User.Api`, so the package must be there. Acceptable for an example project. In a real app, prefer the callback approach to keep test packages out of production assemblies. | Keep InMemory in test project only — requires service manipulation in callback (see prior decision). |
| 2026-06-01 | No unit tests added for TestSuiteInitializer | Testing `TestSuiteInitializer` requires a real ASP.NET Core app; adding `Mvc.Testing` to the pure unit test project is disproportionate. Coverage is provided by the component tests (12/12 pass), which are the regression gate for this feature. | Add unit tests with a minimal inline app — adds heavyweight web dependency to unit test project. |

## Open Questions

<!-- Resolved — all captured in Decisions Log -->

## Constraints

- `TestSuiteInitializer` must NOT extend `WebApplicationFactory` — wrapping only. Mvc.Testing API surface is not part of ConfIT's public contract.
- Generic parameter change from `TStartUp` to `TProgram` IS a runtime-breaking change. No soft obsolete path. Migration must be documented explicitly.
- Configuration sources must be cleared before loading settingsFile. Merging with app defaults would change existing consumer behaviour.
- No changes to `BaseTest`, `TestReader`, `SuiteConfig`, `TestFilter`, `TestResultCollector`, matchers, or the DSL.

## Key Files

- `src/ConfIT/Server/Http/TestSuiteInitializer.cs` — full rewrite; `InternalFactory` private nested class; `PhysicalFileProvider` for settings path
- `src/ConfIT/ConfIT.csproj` — added `Microsoft.AspNetCore.Mvc.Testing` (version-matched); retained direct `TestHost` for `[Obsolete] TestServer` property
- `example/User.Api/Startup.cs` — `AddDbContexts` now config-driven: InMemory when `IsLocalComponentTests=true`, SQLite otherwise
- `example/User.Api/User.Api.csproj` — added `Microsoft.EntityFrameworkCore.InMemory` (needed by config-driven Startup)
- `example/User.ComponentTests/SetUp/TestSuiteFixture.cs` — simplified: `TestSuiteInitializer<Startup>`, no callback, `Services.CreateScope()` for DB seeding
- `example/User.ComponentTests/SetUp/TestServerStartup.cs` — **deleted**

