---
feature: Declarative Suite Configuration
epic: Environment Setup
status: draft
priority: P1
depends_on:
  - ENV-003
  - ENV-005
personas:
  - component-test-author
  - platform-engineer
source_docs: []
---

# Declarative Suite Configuration

## Problem Statement

Every ConfIT consumer writes 60–80 lines of near-identical C# fixture boilerplate per test project: wiring `SuiteConfig`, `TestHttpClient`, `TestFilter`, `TestResultCollector`, folder paths, and startup initialization. The structure is the same across every team; only the values differ. Integration test suites compound this further — when targeting multiple environments (local, QA, staging), teams hardcode URLs and tokens per environment or manage them ad hoc across CI scripts and fixture files.

This uniformity is a signal that ConfIT should own the wiring and let consumers declare only the values in a config file. A `suite.config.yaml` in the test project replaces the boilerplate; the fixture drops to ~10 lines that supply what genuinely requires C# — the entry point type, service overrides, auth provider.

## User / Personas

**Component-test author** — sets up a new component or integration test suite for a microservice. Currently writes a fixture class from scratch or copies from another project. Small mistakes (missing `Dispose`, wrong folder path, hardcoded filter) are easy to introduce and hard to spot in review.

**Platform/infra engineer** — standardises test setup across many teams. Wants a canonical file format that can be linted, templated, and enforced across projects — rather than N slightly different fixture variations accumulating across an organisation.

## Scope

**In scope:**
- `suite.config.yaml` file format with two root sections: `component` and `integration`
- `component` section declares startup mode (`in-process` or `command`), API URL, mock URL, folders, and filter; `command` mode fields feed directly into `AppLauncher` (ENV-005)
- `integration` section declares named environments (e.g., `local`, `qa`, `staging`), each with its own API URL, optional auth token, folders, and filter; a `default` key names which environment is active when `TEST_ENVIRONMENT` is not set
- `SuiteConfiguration.LoadComponent(filePath)` — reads the `component` section, returns a typed config object
- `SuiteConfiguration.LoadIntegration(filePath, environment?)` — reads `integration.{environment}` section; `environment` defaults to `TEST_ENVIRONMENT` env var, then to the `default` key in the config
- Extension methods `ToSuiteConfig()` and `ToTestFilter()` on the returned config objects, so consumers can construct ConfIT objects without knowing the DTO internals
- `${ENV_VAR}` interpolation in scalar string values — auth tokens and URLs injected from environment at load time, no secrets in the committed file
- Clear validation errors on unknown fields, missing required fields, and unresolvable `${ENV_VAR}` references
- Example coverage in `User.ComponentTests` and `User.IntegrationTests` demonstrating both startup modes and multi-environment integration

**Out of scope:**
- Service overrides declared in the config file — `Action<IServiceCollection>` is C# code and belongs in C#; the app manages its own test-environment configuration via `ASPNETCORE_ENVIRONMENT`, launch profiles, and `appsettings.{env}.json`
- The `TestSuiteInitializer<TProgram>` generic type argument — types cannot be resolved safely from YAML strings; the consumer supplies this in C#
- `IAuthTokenProvider` implementation — auth logic belongs in code, not config
- `ITestProcessorFactory` — extension points remain in C#
- Per-test or per-file configuration — this is suite-level bootstrap only
- Config hot-reload — the file is read once at fixture construction time
- IDE autocomplete or JSON Schema publishing for the config format

## Boundary Conditions

- The config file is read once at fixture construction time. Changes during a test run are not detected.
- `${ENV_VAR}` interpolation applies to scalar string values only (URLs, tokens, folder paths). It does not apply to boolean flags, integer values, or nested objects.
- If a referenced `${ENV_VAR}` is not set in the environment, `LoadComponent` / `LoadIntegration` throws at load time with a message naming the variable and the field it was expected in. Silent null substitution is not permitted.
- `LoadIntegration` with no `environment` argument reads `TEST_ENVIRONMENT` env var first. If that is also unset, it falls back to the value of `integration.default`. If `default` is also absent, it throws — no silent guessing.
- The config file must be copied to the build output directory (`<CopyToOutputDirectory>Always</CopyToOutputDirectory>`). ConfIT resolves it relative to the working directory at runtime.
- `component` and `integration` are the only valid root keys. A file with both sections is valid — a single file could theoretically serve both suites if they live in the same project, but this is not the expected usage.
- `ToSuiteConfig()` and `ToTestFilter()` produce the same ConfIT objects that consumers today construct manually — no behavioural change downstream.

## Assumptions

- YAML is the primary format. The same `YamlDotNet` infrastructure used for test files (DSL-002) parses the suite config. JSON is not required as an alternative for this version.
- `TestResultCollector` is always constructed and wired — no config flag for it.
- Auth token values resolved from `${ENV_VAR}` are passed to `TestHttpClient.Create` as the bearer token string. If the auth scheme is more complex, the consumer uses `IAuthTokenProvider` in C# as today.
- For `command` mode in the `component` section, `AppLauncher.Start(cfg.Startup)` is called by the fixture before `TestHttpClient.Create` — process must be ready before the HTTP client is used.
- The `suite.config.yaml` filename is conventional but not enforced — `LoadComponent` and `LoadIntegration` accept any path.

## Scenarios

### Scenario 1: Bootstrapping a component suite with in-process startup from config

A developer adds ConfIT component tests to a microservice. They declare suite values in the config file and supply only the entry point type and any service overrides in C#.

**Acceptance Criteria:**
- Given a `suite.config.yaml` with a `component` section declaring `startup.mode: in-process`, `startup.settings`, `api.url`, `mock.url`, folder paths, and filter
- And the file is copied to the build output directory
- When the fixture calls `SuiteConfiguration.LoadComponent("suite.config.yaml")`
- Then a typed config object is returned with all declared values populated
- And `cfg.ToSuiteConfig()` returns a `SuiteConfig` with `ApiServerUrl`, `MockServerUrl`, and folder paths set from the config
- And `cfg.ToTestFilter()` returns the correct `TestFilter` for the declared strategy and env variable
- And `cfg.Startup.Settings` contains the settings filename so the fixture can pass it to `TestSuiteInitializer<TStartup>`

### Scenario 2: Bootstrapping a component suite with command startup from config

A developer uses command mode so the app runs as an external process. The fixture needs almost no C#.

**Acceptance Criteria:**
- Given a `suite.config.yaml` with `startup.mode: command`, a `startup.command` string, and a `startup.readiness.url`
- When `SuiteConfiguration.LoadComponent("suite.config.yaml")` is called and the fixture passes `cfg.Startup` to `AppLauncher.Start`
- Then the app process is started, readiness is confirmed, and tests run against `cfg.Api.Url`
- And when the fixture disposes, `AppLauncher.Dispose()` stops the process
- And no `TestSuiteInitializer` or service override C# is required in the fixture

### Scenario 3: Bootstrapping an integration suite for a named environment

A CI pipeline runs integration tests against the QA environment. No code change is required to switch from local to QA.

**Acceptance Criteria:**
- Given a `suite.config.yaml` with an `integration` section containing `local` and `qa` environment blocks
- And the `qa` block declares an `api.url` and an `api.authToken: ${QA_API_TOKEN}`
- And `QA_API_TOKEN` is set in the CI environment
- When `SuiteConfiguration.LoadIntegration("suite.config.yaml")` is called with `TEST_ENVIRONMENT=qa`
- Then `cfg.Api.Url` is the QA URL and the resolved token is available for `TestHttpClient.Create`
- And all other values (folders, filter) are read from the `qa` block

### Scenario 4: Default environment used when TEST_ENVIRONMENT is not set

A developer runs integration tests locally without setting `TEST_ENVIRONMENT`. The config's `default` key determines which environment is active.

**Acceptance Criteria:**
- Given a `suite.config.yaml` with `integration.default: local` and a `local` environment block
- And `TEST_ENVIRONMENT` is not set in the environment
- When `SuiteConfiguration.LoadIntegration("suite.config.yaml")` is called
- Then the `local` block is loaded and `cfg.Api.Url` contains the local URL
- And no error or warning is raised about the missing `TEST_ENVIRONMENT`

### Scenario 5: Config file missing or contains an unknown field

A developer makes a typo in a field name or the file is not copied to the output directory.

**Acceptance Criteria:**
- Given a `suite.config.yaml` with an unrecognised field name (e.g., `apiUrl` instead of `api.url`)
- When `LoadComponent` or `LoadIntegration` is called
- Then an exception is thrown before any test runs
- And the exception message names the unrecognised field and the file path
- Given the config file does not exist at the specified path
- When `LoadComponent` or `LoadIntegration` is called
- Then an exception is thrown with a message naming the expected file path

### Scenario 6: `${ENV_VAR}` references in config values are resolved at load time

Auth tokens and environment-specific URLs are stored as env var references, not hardcoded values.

**Acceptance Criteria:**
- Given a `suite.config.yaml` with `api.authToken: ${QA_API_TOKEN}` in an environment block
- And `QA_API_TOKEN=secret-token-value` is set in the environment
- When `LoadIntegration` is called
- Then `cfg.Api.AuthToken` is `secret-token-value`
- Given the same config but `QA_API_TOKEN` is not set in the environment
- When `LoadIntegration` is called
- Then an exception is thrown naming the unresolved variable and the field it appeared in

*(Scenarios ordered chronologically — natural implementation sequence.)*

## Implementation Notes

1. **`SuiteBootstrapConfig` DTO hierarchy** — define `ComponentConfig` (startup, api, mock, folders, filter) and `IntegrationConfig` (default, named environment map). Each named environment is `IntegrationEnvironmentConfig` (api, folders, filter). All DTOs use nullable properties; validation runs after deserialization.

2. **`SuiteConfiguration` static class** — `LoadComponent(string filePath)` deserializes the `component` root key; `LoadIntegration(string filePath, string? environment = null)` resolves environment from argument → `TEST_ENVIRONMENT` env var → `integration.default`. Both methods call `Validate()` and `ResolveEnvVars()` before returning.

3. **`${ENV_VAR}` resolver** — after deserialization, walk all string properties, match `\$\{[A-Z_][A-Z0-9_]*\}` pattern, substitute from `Environment.GetEnvironmentVariable`. Throw `InvalidOperationException` naming the variable and property path if unresolved.

4. **`ToSuiteConfig()` and `ToTestFilter()` extension methods** — map DTO fields to `SuiteConfig` (ApiServerUrl, MockServerUrl, folder paths) and `TestFilter` (strategy → `CreateForTagsFromEnvVariable` or `CreateForTestsFromEnvVariable`). Keep extension methods in a separate `SuiteConfigurationExtensions` class.

5. **Validation** — after deserialization, check required fields for the active mode (e.g., `startup.command` required when `mode: command`; `startup.settings` required when `mode: in-process`). Unknown YAML keys detected via strict deserialization mode in YamlDotNet or manual key inspection.

6. **Example coverage** — add `suite.config.yaml` to `example/User.ComponentTests` (component section, in-process mode) and `example/User.IntegrationTests` (integration section, local environment). Update both fixtures to use `SuiteConfiguration.Load*` + extension methods.

## Open Questions

- [ ] Should `LoadComponent` and `LoadIntegration` be methods on a static class (`SuiteConfiguration`) or a builder-style fluent API? Recommendation: static class — simpler, matches the one-call-per-fixture usage pattern.
- [ ] Should unknown YAML keys throw or warn? Recommendation: throw — silent unknown keys hide typos and produce subtly wrong test setups, which is worse than a loud failure.

## Links

- Design: [env-004-declarative-suite-configuration.md](../../context/env-004-declarative-suite-configuration.md)
- Epic index: [index.md](../index.md)
