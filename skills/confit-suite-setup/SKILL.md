---
name: confit-suite-setup
description: This skill should be used when the user asks to "set up ConfIT", "add ConfIT to my project", "create a component test suite", "scaffold integration tests", "wire up suite.config.yaml", "add a ConfIT test project", "configure the ConfIT fixture", "run my API tests against QA", or when a ConfIT suite fails to start — no tests discovered, config not found, mock server not reachable, auth not applied. Routes to ConfIT's own working reference suites for the chosen startup mode (in-process, command/AppLauncher, integration), then adapts them to the target project.
---

# ConfIT Suite Setup

ConfIT ships three working test suites — one per startup mode — that run in its own CI. This
skill reads the right one and adapts it, rather than carrying templates that drift out of date.

Adding *tests* to an existing suite is a different job: `confit-component-tests` when the
developer works from their own controller and mocks, `confit-integration-tests` when someone
writes black-box tests against a deployed service.

## Step 0 — Locate the reference, always, before anything else

This skill ships inside the ConfIT repository, so its example suites and documentation are on
disk next to it. Resolve the root first — do not guess the path or count `../..` levels:

```bash
bash <this skill>/scripts/reference-path.sh
```

It prints the repository root. Everything this skill refers to is relative to it:

```
<root>/example/   three working suites, one per mode, all verified by CI
<root>/doc/       the prose documentation
```

Then read **`<root>/example/README.md`** — it maps modes to projects and lists what is
demo-specific.

**If the script exits non-zero, stop and say so.** It means the plugin install is broken or
partial. Ask the user to reinstall it. Do not reconstruct a suite from memory: the fixture shape,
config schema and package versions all move between releases, and a plausible invention fails in
ways that are slow to debug. No reference, no setup.

## Step 1 — Choose the mode

Two questions settle it:

1. **Is the service already running** — locally, in CI, or in a deployed environment — and should
   the tests hit it as-is? → **integration** (`SuiteBootstrapper.ForIntegration`). Nothing is
   mocked; environments are selected from config at runtime.
2. Otherwise the tests start the service. **Is it .NET, and may the test project reference it?**
   - Yes → **in-process** (`SuiteBootstrapper.ForComponent<TStartup>`). Fastest start, and the
     DI container is reachable for schema setup and seeding.
   - No — another language, or a .NET app you want exercised as a real process → **command /
     AppLauncher** (`SuiteBootstrapper.ForCommand`). ConfIT runs a shell command, waits for a
     readiness probe, and speaks only HTTP.

Both component modes can stub outbound dependencies with WireMock. Integration suites cannot —
a `mock:` block there is an error.

When the user wants both a fast inner loop and a real-environment check, that is two projects,
not one suite. The test definitions are largely portable between them minus the `mock:` blocks.

## Step 2 — Read the reference for that mode

| Mode | Read this project | And this doc |
|---|---|---|
| in-process | `<root>/example/User.ComponentTests/` | `<root>/doc/suite-setup.md` |
| command | `<root>/example/User.ComponentTests.AppLauncher/` | `<root>/doc/app-launcher.md` |
| integration | `<root>/example/User.IntegrationTests/` | `<root>/doc/suite-setup.md` |

Four files carry the wiring: `suite.config.yaml`, the fixture, the test class, and the `.csproj`.
Read those. Skip `TestCase/` unless also writing tests.

Add `<root>/doc/auth-profiles.md` when the API needs auth, and `<root>/doc/test-filtering.md` when
the suite needs tag or name filtering.

## Step 3 — Create the project

```bash
dotnet new xunit -n <Service>.ComponentTests
cd <Service>.ComponentTests
dotnet add package ConfIT
dotnet add reference ../<Service>.Api/<Service>.Api.csproj   # in-process mode only
```

**Take versions from the CLI, never from the reference.** The example projects build ConfIT from
source via `ProjectReference`, so they pin nothing a consumer should copy; `dotnet add package`
resolves what is current. The same goes for the target framework — match what `dotnet new`
produced, as long as ConfIT supports it (`<root>/src/ConfIT/ConfIT.csproj` lists its
`TargetFrameworks`).

## Step 4 — Author the files

Write fresh files modelled on the reference. Do not copy-and-rename: the examples carry demo
content that will not make sense in another project.

**Structural — every suite needs these:**

- `suite.config.yaml` in the project root, its top-level key (`component:` / `integration:`)
  matching the fixture's bootstrapper call.
- A fixture implementing `IDisposable` that calls one `SuiteBootstrapper.For*`, exposes
  `TestSuiteContext Context`, and disposes the suite — disposal prints the summary and shuts
  infrastructure down.
- A test class deriving from `BaseTest` with `IClassFixture<TestSuiteFixture>`, and a `[Theory]`
  fed by `[MemberData]` → `TestReader.GetTestsForAFolder`. That is ConfIT's only discovery
  mechanism.
- A `<None Update>` entry with `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` for every
  file read at runtime — test definitions, `suite.config.yaml`, body fixtures, app settings.
  `TestReader` reads from the build output directory; a missing entry is the usual cause of
  "no tests discovered".
- A `TestCase/` folder with at least one test definition.

**Optional, despite appearing in the reference:**

- `TestOutputLogger` — a three-line adapter onto xUnit's `ITestOutputHelper`. `BaseTest`'s logger
  parameter is nullable; the AppLauncher example passes `null`.
- `appsettings.Tests.json` — needed only for `mode: in-process`, where `startup.settings` names
  it. Neither other mode has one.
- The `onStarted` and `configureServices` hooks on `ForComponent` — for seeding and DI overrides.
  Omit them when the app configures itself.

**Demo-specific — leave behind:** the `User.*` namespaces, `UserDbInitializer`,
`JustAnotherService`, every port number, the `IsLocalComponentTests` flag, the EF Core InMemory
package, and everything under `TestCase/`. `<root>/example/README.md` has the full list with
reasons.

Two things in the reference that are available but *not* demonstrated, so do not present them as
worked examples: the `protected virtual` hooks on `User.Api.Startup`, which nothing overrides, and
`IAuthTokenProvider`, which no `SuiteBootstrapper` overload accepts — declarative auth goes in
`suite.config.yaml`.

## Step 5 — Verify

```bash
bash <root>/tools/verify-suite.sh <path to test project>
```

It checks the wiring faults that produce confusing runtime symptoms: missing package reference,
unregistered config or test files, a fixture that does not match the config section, a hardcoded
filter env var. Fix what it reports before interpreting a test failure.

Then add one trivial test definition hitting a health or list endpoint and run the suite. It is
wired correctly when the run prints ConfIT's summary table with one passing test. Zero tests
discovered means files are missing from the output directory — a `.csproj` registration problem,
not a ConfIT problem.

In a repository that wraps builds behind `make` or a CI target, use that target rather than
calling `dotnet test` directly; the wrapper usually handles service lifecycle and database
resets.

## Non-.NET services

Command mode is the reason ConfIT can test a Go, Node, Python or Java service. The test project
still needs to be a .NET xUnit project — it references ConfIT only, never the application — and
`startup.command` is any shell command that starts the service. `<root>/doc/app-launcher.md` is the
guide, and `<root>/example/User.ComponentTests.AppLauncher/` is the working proof: it has **no**
project reference to the app under test.

The application owns its own test environment in this mode: selecting a test database, seeding
itself, and pointing its dependencies at the mock URL, driven by whatever environment the command
sets.

## Additional resources

- **`references/troubleshooting.md`** — symptom → cause → fix for suites that will not start,
  mocks that do not match, auth that is not applied, and tests that pass alone but fail together.
- **`scripts/reference-path.sh`** — resolves and validates the reference root; `--check` also
  lists the suites it found.
- **`<root>/tools/verify-suite.sh`** — wiring diagnostics for an existing suite.
- **`<root>/example/README.md`** — mode-to-project map and the demo-specific list.
- **`<root>/doc/`** — the full documentation set, including `extending-confit.md` for custom
  matchers and processor hooks.
