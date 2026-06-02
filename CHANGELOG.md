# Changelog

All notable changes to ConfIT are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
ConfIT uses [Semantic Versioning](https://semver.org/).

---

## [3.0.0] — 2026-06-02

### Added

- **YAML test files** — `.yaml` / `.yml` test definitions alongside JSON. Full DSL parity: all matchers, extract/inject, mock interactions, and tags work identically in YAML. YAML anchors and comments supported.

- **Variable extraction and injection** — `extract` captures response field values; `{{varName}}` injects them into subsequent requests and paths. `${ENV_VAR}` references pull environment values directly into test definitions. Covers the majority of `ITestProcessor` use cases declaratively.

- **Semantic matcher library** — Type-aware named assertions: `isUuid`, `isIsoDate`, `isIsoDateTime`, `isEmail`, `isUrl`, `greaterThan(n)`, `lessThan(n)`, `hasLength(n)`, `isNull`, `isNotNull`, `isNotEmpty`. Custom matchers registered via `SuiteConfig.CustomMatchers`.

- **Field-level failure output** — Test failures report per-field diffs (expected vs actual, `<missing>`, `<absent>`) rather than raw JSON dumps. Semantic and pattern matcher results are reported separately before the body diff.

- **Suite summary table** — `TestResultCollector` prints a grouped pass/fail table at the end of every suite run, organised by source file with colour-coded results and timing per test.

- **AppLauncher** — Out-of-process service startup via any shell command. Polls HTTP or TCP readiness before running tests. Enables language-agnostic testing: test Go, Node.js, Python, Java, or any HTTP API using the same test definitions. The application manages its own test environment; the test project holds no reference to application internals.

- **Declarative suite configuration** — `suite.config.yaml` with `SuiteConfiguration.LoadComponent` / `LoadIntegration` replaces manual fixture wiring. One file declares API URL, mock URL, folder paths, filter strategy, and startup mode. Integration suites support named environments (`local`, `qa`, `staging`) selected at runtime via `TEST_ENVIRONMENT`. `${ENV_VAR}` interpolation in config values keeps secrets out of committed files.

- **net10.0 support** — Library targets both `net9.0` and `net10.0`.

### Changed

- **`TestSuiteInitializer` modernised** — Replaced legacy `WebHost.CreateDefaultBuilder()` and manual `TestServer` construction with `WebApplicationFactory<TProgram>`. Generic parameter is the app's entry point class (typically `Startup`); the `TestServerStartup` subclass pattern is eliminated. Service overrides via an optional `Action<IServiceCollection>` callback. `Services` (`IServiceProvider`) replaces the deprecated `TestServer` property.

### Deprecated

- `TestSuiteInitializer.TestServer` — use `TestSuiteInitializer.Services` (`IServiceProvider`) instead.
- `ITestProcessor` / `ITestProcessorFactory` — the `extract` + `{{inject}}` DSL covers the majority of use cases without C#. These interfaces remain for genuinely imperative cases such as request signing or external side effects.
