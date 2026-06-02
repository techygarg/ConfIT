---
feature: Modern Test Host Initialization
epic: Environment Setup
status: draft
priority: P0
depends_on: []
personas:
  - component-test-author
  - library-adopter
source_docs: []
---

# Modern Test Host Initialization

## Problem Statement

`TestSuiteInitializer` uses `WebHost.CreateDefaultBuilder()` and manual `TestServer` construction — the ASP.NET Core 2.x/3.x legacy hosting API. This creates two distinct problems for ConfIT consumers:

1. **Blocked adoption on modern apps.** Any app that uses the minimal hosting model (`WebApplication.CreateBuilder()`, no `Startup` class) cannot use `TestSuiteInitializer` at all. The generic parameter `TStartUp` requires a class with `ConfigureServices` and `Configure` methods — a pattern that modern .NET apps do not have.

2. **Forced boilerplate for service overrides.** Consumers who need to swap services for testing (e.g., replace a SQLite `DbContext` with an in-memory one) must create a `TestServerStartup` subclass of the app's `Startup` class. This file exists solely for the override — it carries no business logic — yet every team using ConfIT must write and maintain it.

## User / Personas

**Component-test author** — writes in-process component tests against the service under test. Currently must maintain a `TestServerStartup.cs` file that only exists to override the DB context. Wants to express service overrides inline without a separate class.

**Library adopter** — developer at a team whose API uses the minimal hosting model (`.NET 6+` default, `WebApplication.CreateBuilder()`). Currently cannot use `TestSuiteInitializer` at all — blocked from using ConfIT for component tests.

## Scope

**In scope:**
- Replace `WebHost.CreateDefaultBuilder()` + `new TestServer(builder)` internals with `WebApplicationFactory<TProgram>` from `Microsoft.AspNetCore.Mvc.Testing`
- Change generic parameter from `TStartUp` (a `Startup` subclass) to `TProgram` (the app's entry point class, typically `Program`)
- Add optional `Action<IServiceCollection>` parameter to `TestSuiteInitializer` constructor for service overrides — no subclass required
- Replace `TestServer TestServer { get; }` property with `IServiceProvider Services { get; }` to expose the DI container without leaking the underlying infrastructure type
- Keep `TestHttpClient TestHttpClient { get; }` unchanged — no changes to `BaseTest`, the DSL, or any matcher
- Mark the current `TestSuiteInitializer<TStartUp>(string settingsFile)` constructor as `[Obsolete]` with a migration message — not removed
- Update `example/User.ComponentTests` to demonstrate the new pattern: delete `TestServerStartup.cs`, rewrite `TestSuiteFixture.cs` to use `TestSuiteInitializer<Program>`

**Out of scope:**
- Migrating `User.Api` from the Startup-class pattern to minimal hosting — not required; `WebApplicationFactory<TProgram>` supports both
- Changing anything in `BaseTest`, `SuiteConfig`, `TestFilter`, `TestResultCollector`, or the test DSL
- Supporting non-web apps (gRPC, console, background services) — component tests are specifically for HTTP services
- `WebApplicationFactory`'s `WithWebHostBuilder` advanced override — consumers who need it can use `WebApplicationFactory` directly; the `Action<IServiceCollection>` callback covers the common case
- Generating or templating the `TestSuiteFixture` C# class — that is ENV-004 scope

## Boundary Conditions

- **Minimal hosting and `Program` visibility.** In .NET 6+ minimal hosting, the `Program` class is generated as internal from top-level statements. A test project in a different assembly cannot reference it directly. The consumer must add `public partial class Program {}` at the bottom of their `Program.cs` to make it accessible. ConfIT documentation must state this requirement clearly; the library cannot work around it.
- **Startup-class apps.** Apps that still use the `Startup` pattern (like `User.Api`) work unchanged: pass `Program` (not `Startup`) as `TProgram`. `WebApplicationFactory` locates the app's assembly via the entry point class, then calls `UseStartup<TStartup>()` internally as the app already registers.
- **`TestServer` removal.** Code that accesses `initializer.TestServer.Services` breaks. The replacement is `initializer.Services` (same `IServiceProvider`). This is a breaking change on the `TestServer` property — covered by `[Obsolete]` on the old constructor and migration guidance.
- **Settings file loading.** `appsettings.Tests.json` (or equivalent) is loaded via `IWebHostBuilder.ConfigureAppConfiguration` inside `WebApplicationFactory`. The existing pattern of passing the settings filename to the constructor is preserved.
- **Parallel test execution.** `WebApplicationFactory` is designed to be shared across test classes via `IClassFixture` — same as the current pattern. Thread safety characteristics are identical.

## Assumptions

- `Microsoft.AspNetCore.Mvc.Testing` (which ships `WebApplicationFactory`) is added to `ConfIT.csproj` alongside or in place of the current `Microsoft.AspNetCore.TestHost` reference. `Mvc.Testing` depends on `TestHost` transitively.
- The old constructor is retained as `[Obsolete]` for at least one minor version before removal — not removed in the same release as the new API ships.
- Consumers who access raw `TestServer` (not just `TestServer.Services`) are rare; `IServiceProvider Services` covers the documented use case (DB seeding).
- `User.Api` does not need to migrate from Startup-class to minimal hosting for this feature to work. Both patterns are supported by `WebApplicationFactory<TProgram>`.

## Scenarios

### Scenario 1: Initializing against a minimal-hosting app

A developer whose API uses `WebApplication.CreateBuilder()` (no `Startup` class) creates a component test fixture using ConfIT.

**Acceptance Criteria:**
- Given an ASP.NET Core app that uses the minimal hosting model (top-level statements in `Program.cs`, no `Startup` class)
- And the app's `Program.cs` exposes the entry point class (e.g., `public partial class Program {}` at the bottom of the file)
- When `new TestSuiteInitializer<Program>("appsettings.Tests.json")` is constructed in the test fixture
- Then the in-process server starts without error
- And `initializer.TestHttpClient` can make HTTP calls to the running app
- And the app's registered services are available via `initializer.Services`

### Scenario 2: Overriding services without a Startup subclass

A developer needs to replace a production DB context with an in-memory equivalent for testing. No `TestServerStartup.cs` file is required.

**Acceptance Criteria:**
- Given a test fixture that constructs `TestSuiteInitializer<Program>` with an `Action<IServiceCollection>` callback
- When the callback removes the production `DbContext` registration and adds an in-memory replacement
- Then the in-process server starts with the overridden service registration in effect
- And HTTP requests that touch the database use the in-memory DB, not the production DB
- And no subclass of `Startup` or `Program` is required in the test project

### Scenario 3: Accessing the DI container to seed test data

After initialization, a developer retrieves a service from the container to seed test state before tests run.

**Acceptance Criteria:**
- Given a constructed `TestSuiteInitializer<Program>`
- When the fixture accesses `initializer.Services.GetService<SomeDbContext>()`
- Then the resolved instance is the same service registration that the in-process server uses
- And the developer can call database seeding methods on the resolved instance before tests run

### Scenario 4: Initializing against a Startup-class app

A developer with an existing API that uses the `Startup` class pattern (including `User.Api` in the example) uses the new initializer without changes to the app's hosting code.

**Acceptance Criteria:**
- Given an ASP.NET Core app that uses `Host.CreateDefaultBuilder().UseStartup<Startup>()`
- When `new TestSuiteInitializer<Program>("appsettings.Tests.json", serviceOverrides)` is constructed
- Then the in-process server starts and routes requests via the `Startup` class exactly as production would
- And service overrides supplied via the callback take effect on top of `Startup.ConfigureServices`

### Scenario 5: Entry point type not accessible from the test project

A developer uses the minimal hosting model but forgets to expose `Program` publicly.

**Acceptance Criteria:**
- Given an app whose `Program` class is internal (default for minimal hosting with no `public partial class Program {}` declaration)
- When `new TestSuiteInitializer<Program>(...)` is constructed from a test project in a different assembly
- Then construction fails at startup with a descriptive exception
- And the exception message or ConfIT documentation guides the developer to add `public partial class Program {}` to their `Program.cs`

*(Scenarios ordered chronologically — natural implementation sequence.)*

## Implementation Notes

1. **Package change** — add `Microsoft.AspNetCore.Mvc.Testing` (version-matched per `TargetFramework`, same as current `TestHost` pattern) to `ConfIT.csproj`. This package owns `WebApplicationFactory`. Evaluate whether `Microsoft.AspNetCore.TestHost` can be removed (it is a transitive dependency of `Mvc.Testing`) or must be kept for direct usages.

2. **`TestSuiteInitializer<TProgram>` rewrite** — wrap (not extend) a `WebApplicationFactory<TProgram>` field. Constructor signature: `TestSuiteInitializer<TProgram>(string settingsFile, Action<IServiceCollection>? configureServices = null)`. Inside `ConfigureWebHost`, apply `builder.ConfigureAppConfiguration` (settings file) and `builder.ConfigureServices` (the callback). Expose `Services` as `factory.Services`. Create `TestHttpClient` from `factory.CreateClient()`.

3. **`[Obsolete]` path** — keep the current `TestSuiteInitializer<TStartUp>(string settingsFile)` signature and mark it `[Obsolete("Use TestSuiteInitializer<TProgram> with the configureServices callback. See migration guide.")]`. Do not remove it in this version.

4. **`TestServer` property** — mark `public TestServer TestServer { get; }` as `[Obsolete("Use Services (IServiceProvider) instead.")]`. Map `Services` to `factory.Services`.

5. **Example update (`User.ComponentTests`)** — delete `TestServerStartup.cs`. Rewrite `TestSuiteFixture.InitializeServer()` to use `new TestSuiteInitializer<Program>("appsettings.Tests.json", services => { /* in-memory DB override */ })`. Update `InitializeDb` to use `initializer.Services.GetService<UserDbContext>()`.

6. **Unit tests** — add tests in `ConfIT.UnitTest` covering: successful construction with and without the callback, `Services` access after construction, `Dispose` lifecycle (factory disposed when initializer disposed).

## Open Questions

- [ ] Should `TestSuiteInitializer` expose `WebApplicationFactory<TProgram>` directly for consumers who need `WithWebHostBuilder` for advanced configuration beyond what the `Action<IServiceCollection>` covers? Recommendation: do not expose it in this version — add if a concrete consumer need surfaces. Keep the surface minimal.

## Links

- Design: [env-003-modern-test-host-initialization.md](../../context/env-003-modern-test-host-initialization.md)
- Epic index: [index.md](../index.md)
