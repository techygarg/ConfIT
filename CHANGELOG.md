# Changelog

All notable changes to ConfIT are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
ConfIT uses [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added

- **YAML test files** — `.yaml` / `.yml` test definitions alongside JSON. Full DSL parity: all matchers, extract/inject, mock interactions, and tags work identically in YAML. YAML anchors and comments supported.

- **Variable extraction and injection** — `extract` captures response field values; `{{varName}}` injects them into subsequent requests and paths. `${ENV_VAR}` references pull environment values directly into test definitions. Covers the majority of `ITestProcessor` use cases declaratively.

- **Semantic matcher library** — Type-aware named assertions: `isUuid`, `isIsoDate`, `isIsoDateTime`, `isEmail`, `isUrl`, `greaterThan(n)`, `lessThan(n)`, `hasLength(n)`, `isNull`, `isNotNull`, `isNotEmpty`. Custom matchers registered via `SuiteConfig.CustomMatchers`.

- **Field-level failure output** — Test failures report per-field diffs (expected vs actual, `<missing>`, `<absent>`) rather than raw JSON dumps. Semantic and pattern matcher results are reported separately before the body diff.

- **Suite summary table** — `TestResultCollector` prints a grouped pass/fail table at the end of every suite run, organised by source file with colour-coded results and timing per test.

- **AppLauncher** — Out-of-process service startup via any shell command. Polls HTTP or TCP readiness before running tests. Enables language-agnostic testing: test Go, Node.js, Python, Java, or any HTTP API using the same test definitions. The application manages its own test environment; the test project holds no reference to application internals.

- **Declarative suite configuration** — `suite.config.yaml` with `SuiteConfiguration.LoadComponent` / `LoadIntegration` replaces manual fixture wiring. One file declares API URL, mock URL, folder paths, filter strategy, and startup mode. Integration suites support named environments (`local`, `qa`, `staging`) selected at runtime via `TEST_ENVIRONMENT`. `${ENV_VAR}` interpolation in config values keeps secrets out of committed files.

- **Test Dependency Graph** — `depends:` field on test definitions declares prerequisites within the same file. When a prerequisite fails or is skipped, all dependents are skipped rather than producing cascading errors or misleading `UndefinedVariableException` failures. The skip reason is shown in the suite summary beneath each skipped test (`└─ prerequisite 'CreateUser' failed`). Dependencies are validated at load time — forward references and references to tests in other files are rejected with a clear message naming the test and the file.

- **Declarative Auth Profiles** — `auth:` block in `suite.config.yaml` replaces `IAuthTokenProvider` C# boilerplate for the common auth cases. Three types are supported:
  - `type: bearer` — static token or `${ENV_VAR}` reference; every request carries `Authorization: Bearer {token}`
  - `type: oauth2-client-credentials` — posts to `tokenUrl` at suite startup using `grant_type=client_credentials`; caches the `access_token` for the entire run; throws with endpoint URL and HTTP status if the token endpoint fails
  - `type: api-key` — injects a value into any named request header; `headerKey:` (required) names the header, `value:` supplies the key
  - All types accept an optional `headerKey:` override; bearer and OAuth2 default to `Authorization`
  - `cfg.ToAuthTokenProvider()` extension method on `ComponentConfig` and `IntegrationEnvironmentConfig` constructs the configured provider; returns `null` when no `auth:` block is declared, preserving existing no-auth behaviour

- **net10.0 support** — Library targets both `net9.0` and `net10.0`.

### Changed

- **`TestSuiteInitializer` modernised** — Replaced legacy `WebHost.CreateDefaultBuilder()` and manual `TestServer` construction with `WebApplicationFactory<TProgram>`. Generic parameter is the app's entry point class (typically `Startup`); the `TestServerStartup` subclass pattern is eliminated. Service overrides via an optional `Action<IServiceCollection>` callback. `Services` (`IServiceProvider`) replaces the deprecated `TestServer` property.

- **`IAuthTokenProvider`** gains a `HeaderKey()` method with a default implementation of `"Authorization"`. Existing custom providers that only implement `Token()` continue to work unchanged — the default covers the standard bearer case. `TestHttpClient` now calls `HeaderKey()` to determine the header name, making API key auth expressible through the same interface without a separate injection point.

### Deprecated

- `TestSuiteInitializer.TestServer` — use `TestSuiteInitializer.Services` (`IServiceProvider`) instead.
- `ITestProcessor` / `ITestProcessorFactory` — the `extract` + `{{inject}}` DSL covers the majority of use cases without C#. These interfaces remain for genuinely imperative cases such as request signing or external side effects.

### Removed

- **net8.0 support** — The library now targets `net9.0` and `net10.0` only. Projects still on .NET 8 must upgrade before adopting this release.

### Documentation

- **[Auth Profiles](doc/auth-profiles.md)** — dedicated reference for all auth configuration: bearer, OAuth2, and API key declarative types; custom `IAuthTokenProvider` for signing, rotating tokens, and complex flows; OAuth2 WireMock testing pattern for self-contained component tests; YAML-based header verification with an echo endpoint.
- **[Test Dependency Graph](doc/test-dependency-graph.md)** — full reference for the `depends:` field: skip-not-fail semantics, cascading skip propagation, load-time validation rules, and interaction with variable extraction.
