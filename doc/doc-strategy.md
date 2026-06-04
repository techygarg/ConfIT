# Documentation Strategy

This file tracks which documents exist, which are missing, and the conventions that keep them consistent. It is a working planning document — not published to users.

---

## Current State

| File | Status | What it covers |
|---|---|---|
| `matchers-and-patterns.md` | ✅ complete | `ignore`, `pattern`, `semantic`, nested paths, custom matchers |
| `variable-extraction-and-injection.md` | ✅ complete | `extract`, `{{inject}}`, `${ENV}`, error cases, migration from `ITestProcessor` |
| `test-dependency-graph.md` | ✅ complete | `depends:` field, skip-not-fail semantics, cascading, load-time validation |
| `test-file-format.md` | ✅ complete | DSL structure, all fields including `depends:`, JSON and YAML format |
| `suite-setup.md` | ⚠️ major update needed | Manual fixture wiring only — pre-dates config-driven setup and AppLauncher |
| `mock-interactions.md` | ✅ complete | WireMock stubs, request matching, YAML anchor reuse |
| `test-filtering.md` | ✅ complete | RUN_POOLS, RUN_TESTS, CI patterns |
| `extending-confit.md` | ⚠️ minor update needed | `ITestProcessor` section references old `TestServerStartup` pattern (deleted) |
| `failure-output.md` | ✅ complete | Field-level failure messages, path notation, suite summary, debugging tips |
| `app-launcher.md` | ✅ complete | Out-of-process startup, language-agnostic testing, application responsibility |
| `test-execution-flow.md` | ✅ complete | ASCII flow diagrams — component (in-process), component (command), integration, execution loop |
| `doc-strategy.md` | ✅ this file | Planning only |

---

## What Changed Since Last Doc Round

Three new features shipped (ENV-003, ENV-004, ENV-005) that significantly change the onboarding story and expand ConfIT's scope.

### ENV-003 — Modern Test Host Initialization

`TestSuiteInitializer` was rewritten to use `WebApplicationFactory<TProgram>`. The old `TStartup` generic pattern (which required subclassing `Startup` as `TestServerStartup`) is gone. The new API uses a callback for service overrides and `Startup` (not a subclass) as the type argument.

Impact on docs: `suite-setup.md` shows the old pattern. Must be updated.

### ENV-004 — Declarative Suite Configuration

`SuiteConfiguration.LoadComponent(filePath)` and `LoadIntegration(filePath, env?)` read a `suite.config.yaml` file and return a typed config object. Extension methods `ToSuiteConfig()`, `ToTestFilter()`, `ToAppLauncherConfig()` project it to ConfIT objects. This replaces 60-80 lines of fixture boilerplate.

The `integration` section supports named environments (`local`, `qa`, `staging`) — `TEST_ENVIRONMENT` env var selects which one runs. This changes how integration tests are configured for multiple targets.

Impact on docs: `suite-setup.md` must be substantially rewritten. The config-driven path is now the RECOMMENDED onboarding path — the manual wiring remains documented as the advanced/custom path.

### ENV-005 — AppLauncher

`AppLauncher` starts an external process (any shell command), waits for a readiness probe (HTTP 2xx or TCP port), and stops the process on dispose. Combined with `suite.config.yaml` (`startup.mode: command`), it enables out-of-process component testing.

**This is the most significant capability shift.** Key aspects that must be documented:

1. **Language-agnostic testing**: Because AppLauncher runs a shell command, ConfIT can now test APIs written in Go, Node.js, Python, or any other stack — not just .NET. The test definitions (JSON/YAML), matchers, mocks, and assertions work identically regardless of what's running on the other end.

2. **Application responsibility for test environment**: In AppLauncher mode, the API manages its own test configuration through standard mechanisms (environment variables, launch profiles, config files). ConfIT does not reach inside the app to override services or seed data. The app responds to `ASPNETCORE_ENVIRONMENT=ComponentTest` (or equivalent) by configuring itself appropriately — InMemory DB, pointing to WireMock, etc.

3. **Self-seeding pattern**: The app seeds its own test data on startup when in the component test environment. This eliminates the `InitializeDb` call in the fixture (which required a reference to the app's internals). Tests that need data create it themselves via the API; error/validation tests need no setup.

4. **Comparison with in-process mode**: Both modes produce identical test execution. In-process is faster and gives a fresh DB per run automatically. Command/AppLauncher is language-agnostic and keeps the test project completely decoupled from app internals.

---

## Document Inventory — What Should Exist

### Tier 1 — Foundation (everyone needs these)

| Document | Status | One-liner |
|---|---|---|
| `suite-setup.md` | ✅ complete | How to install, configure, and wire ConfIT — both config-driven and manual paths |
| `test-execution-flow.md` | ✅ complete | What happens at runtime — fixture startup, execution loop, suite summary |
| `app-launcher.md` | ✅ complete | Out-of-process startup, language-agnostic scope, app test-env responsibility |
| `auth-profiles.md` | ✅ complete | Bearer, OAuth2, API key declarative auth; custom IAuthTokenProvider; WireMock OAuth2 testing; YAML header verification |
| `test-file-format.md` | ✅ no change | Full DSL reference — all fields, JSON and YAML |
| `matchers-and-patterns.md` | ✅ no change | Asserting on dynamic fields without writing code |
| `variable-extraction-and-injection.md` | ✅ no change | Passing data between tests declaratively |

### Tier 2 — Features (go deeper once foundation is read)

| Document | Status | One-liner |
|---|---|---|
| `mock-interactions.md` | ✅ no change | Declaring WireMock stubs inline, `bodyFromFile`, request/response matching |
| `test-filtering.md` | ✅ no change | `RUN_TESTS`, `RUN_POOLS`, `TestFilter` factory methods, CI usage |
| `test-dependency-graph.md` | ✅ complete | `depends:` field, skip-not-fail, cascading, load-time validation |
| `auth-profiles.md` | ✅ complete | Single auth reference — all types, WireMock OAuth2 pattern, IAuthTokenProvider C# |

### Tier 3 — Reference (for advanced use or extension)

| Document | Status | One-liner |
|---|---|---|
| `extending-confit.md` | ⚠️ minor update | Remove `TestServerStartup` example; update `TestSuiteInitializer` snippet to new API |
| `failure-output.md` | ✅ no change | Reading per-field failure messages, debugging a failing suite |

---

## `suite-setup.md` Rewrite Plan

The doc must now present two distinct onboarding paths clearly:

**Path A — Config-driven (recommended for most teams):**
1. Drop a `suite.config.yaml` in your test project
2. Call `SuiteConfiguration.LoadComponent` or `LoadIntegration` in the fixture
3. Use `ToSuiteConfig()`, `ToTestFilter()`, etc. to get ConfIT objects
4. For component tests: choose `startup.mode: in-process` or `startup.mode: command`

**Path B — Manual wiring (for custom scenarios or full control):**
- The current content — still valid, just clearly labelled as the advanced/custom path

The `suite.config.yaml` format should be shown for both in-process and command modes. The multi-environment integration config (`local`, `qa`, `staging`) should be shown in the integration section.

The `TestSuiteInitializer` entry should be updated: `TProgram` is now the entry point class (typically `Startup` for Startup-class apps, `Program` with `public partial class Program {}` for minimal-hosting apps). The `TestServerStartup` subclass pattern is gone.

---

## `app-launcher.md` New Doc Plan

Sections:
1. **What AppLauncher does** — starts a process, probes readiness, stops on dispose. One paragraph.
2. **Why it matters: language-agnostic testing** — the API can be written in any language. ConfIT tests HTTP; it doesn't care what's serving it.
3. **The two modes compared** — in-process vs command mode side-by-side. When to use each.
4. **How the app manages its test environment** — the app reads `ASPNETCORE_ENVIRONMENT` (or equivalent), loads test-specific config, sets up InMemory DB, points to WireMock. ConfIT doesn't reach inside the app.
5. **The self-seeding pattern** — tests create their own data via the API rather than depending on pre-seeded state; error tests need nothing. Why this is better architecture for component tests.
6. **`suite.config.yaml` command mode** — the YAML config, what each field does, readiness probe options (HTTP vs TCP).
7. **Running with `make component.applauncher`** — how the Makefile pre-builds and runs.
8. **Live example** — links to `User.ComponentTests.AppLauncher`.

---

## `extending-confit.md` Minor Update Plan

One targeted change: the `TestSuiteInitializer` code snippet in the fixture setup section shows the old `TestServerStartup` subclass pattern. Replace with the current pattern: `new TestSuiteInitializer<Startup>("appsettings.Tests.json")` and note that service overrides go in the optional `Action<IServiceCollection>` callback.

The `IAuthTokenProvider`, `ITestOutputLogger`, `ITestProcessor`, and custom matchers sections are unchanged.

---

## Conventions

**One concept per file.** If a document needs to say "for X, see the Y section of Z", that's a signal to split.

**Feature-first, not API-first.** Lead with the user problem and the DSL, not with the class or method name.

**Live examples are mandatory.** Every significant code snippet must end with a `📄 Live example:` link pointing to a real test file in `example/`. Broken links are caught in review.

**Prefer component tests for examples.** They are self-contained (no external services). Mention integration tests only when the behaviour differs.

**Code snippet format.** Use JSON as the primary snippet format. When YAML is relevant, show it as an alternative after the JSON, not instead of it. For `suite.config.yaml`, YAML is primary.

**Length target.** Each doc should be readable in under 10 minutes. If a page scrolls longer than `matchers-and-patterns.md`, consider splitting.
