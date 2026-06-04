---
feature: Declarative Suite Configuration
requirement_doc: .lattice/requirements/features/env-004-declarative-suite-bootstrap.md
created: 2026-06-02
---

# Declarative Suite Configuration

> `suite.config.yaml` replaces fixture boilerplate. Two root sections (`component` / `integration`), two startup modes, multi-environment integration targets. Depends on ENV-003 (in-process mode) and ENV-005 (command mode).

## Design: Level 1 — Capabilities

1. A component test fixture reads `suite.config.yaml` once (`LoadComponent`) and gets all suite setup values as a typed object — API URL, mock URL, folder paths, filter, startup mode.
2. An integration test fixture reads the same format (`LoadIntegration`) and gets environment-specific values; `TEST_ENVIRONMENT` env var selects the active environment.
3. When `TEST_ENVIRONMENT` is unset, `integration.default` determines which environment loads automatically.
4. `${ENV_VAR}` references in scalar string values resolve to env var values at load time — no secrets committed.
5. Config objects expose `ToSuiteConfig()`, `ToTestFilter()`, `ToAppLauncherConfig()` — fixture never references DTO field names directly.
6. Typos, missing required values, or unresolvable `${ENV_VAR}` references throw with a specific error before any test runs.

---

## Design: Level 2 — Components

| Component | Layer | Responsibility |
|---|---|---|
| `SuiteConfiguration` | Infrastructure (`src/ConfIT/`) | Public static: `LoadComponent`, `LoadIntegration`. File reading, YAML parsing (via `YamlConverter`), complete validation, `${ENV_VAR}` resolution. **Single validation point — all checks run here, nothing downstream re-validates.** |
| `ComponentConfig` + sub-DTOs | Infrastructure (`SuiteConfiguration.cs`) | Typed return of `LoadComponent`. Guaranteed valid on return. Sub-objects: `StartupConfig`, `ApiConfig`, `MockConfig`, `FolderConfig`, `FilterConfig`. |
| `IntegrationEnvironmentConfig` | Infrastructure (same file) | Typed return of `LoadIntegration`. Guaranteed valid on return. Sub-objects: `ApiConfig`, `FolderConfig`, `FilterConfig`. |
| `SuiteConfigurationExtensions` | Infrastructure (`src/ConfIT/Extension/`) | `ToSuiteConfig()`, `ToTestFilter()`, `ToAppLauncherConfig()`. Pure mapping — no validation needed (input is pre-validated). |

```
Consumer Fixture
    │
    │  SuiteConfiguration.LoadComponent("suite.config.yaml")
    ▼
┌──────────────────────────────────────────────────────────┐
│  SuiteConfiguration (public static)    src/ConfIT/       │
│  + LoadComponent(filePath) → ComponentConfig             │
│  + LoadIntegration(filePath, env?) → IntegrationEnvConfig│
│                                                          │
│  ▸ Validate-at-boundary contract:                        │
│    ALL checks run here before returning — file, YAML,    │
│    unknown keys, required fields per mode,               │
│    filter strategy, ${ENV_VAR} resolution.               │
│    Downstream components (AppLauncher, TestSuiteInit)    │
│    receive pre-validated objects and trust the input.    │
│                                                          │
│  private helpers:                                        │
│  - ValidateKeys(JObject, knownKeys, path)                │
│  - ValidateCommandMode(StartupConfig)                    │
│  - ValidateInProcessMode(StartupConfig)                  │
│  - ResolveEnvVars(string?, fieldName) → string?          │
│  - ResolveEnvironment(JObject, env?) → string            │
└──────────────────────────────────────────────────────────┘
    │ uses YamlConverter.ToJObject (DSL-002 infrastructure)
    ▼
ComponentConfig / IntegrationEnvironmentConfig  (pre-validated)
    │
    │  .ToSuiteConfig()   .ToTestFilter()   .ToAppLauncherConfig()
    ▼
SuiteConfig / TestFilter / AppLauncherConfig  (pure mapping, no checks)
```

**Validation contract for `LoadComponent`:**
- File readable; valid YAML; root keys limited to `component` / `integration`
- `startup.mode` is `"in-process"` or `"command"` (absent → defaults to `"in-process"`)
- Mode `in-process`: `startup.settings` non-empty
- Mode `command`: `startup.command` non-empty; `startup.readiness` has exactly one of `url` / `port`
- `api.url` non-empty
- `filter.strategy` is `"tags"` or `"tests"` if filter is present; `filter.envVariable` non-empty
- All `${ENV_VAR}` references resolve

**Validation contract for `LoadIntegration`:**
- Same file-level checks; `integration` section present; active environment resolvable and exists
- `api.url` non-empty; same filter and env var checks

**`ReadinessConfig` reuse:** `StartupConfig.Readiness` uses the same `ReadinessConfig` type from `AppLauncher` (ENV-005). Fields are identical; `ToAppLauncherConfig()` maps directly.

---

## Design: Level 3 — Interactions

**Flow 1: `SuiteConfiguration.LoadComponent(filePath)`**
```
1. File.ReadAllText(filePath) → FileNotFoundException if missing
2. YamlConverter.ToJObject(content) → YamlException → InvalidDataException if malformed
3. ValidateKeys(root, ["component","integration"], "root") → throw on unknown key
4. Extract root["component"] → throw if absent
5. ValidateKeys(component, knownComponentKeys, "component")
6. Parse + validate startup:
     mode absent → "in-process" default
     mode "in-process" → startup.settings non-empty
     mode "command"    → startup.command non-empty; readiness present;
                         exactly one of readiness.url / readiness.port
     unknown mode      → throw
7. Validate api.url non-empty
8. Validate filter (if present): strategy "tags"|"tests"; envVariable non-empty
9. ResolveEnvVars — walk all string fields; ${VAR} → env var; null → throw with field path
10. Deserialize validated JObject → ComponentConfig; return (guaranteed valid)
```

**Flow 2: `SuiteConfiguration.LoadIntegration(filePath, environment?)`**
```
1-3. Same file + root key validation as Flow 1
4. Extract root["integration"] → throw if absent
5. ResolveEnvironment(integrationSection, environment?):
     argument → TEST_ENVIRONMENT env var → integration["default"] → throw if all unresolved
6. Extract integrationSection[resolvedEnv] → throw if env name not found
7. ValidateKeys(envBlock, knownEnvKeys, "integration.{name}")
8. Validate api.url, filter; ResolveEnvVars
9. Deserialize → IntegrationEnvironmentConfig; return (guaranteed valid)
```

**Flow 3: Projection methods (pure mapping, no checks)**
```
ToSuiteConfig():  ApiServerUrl, MockServerUrl, ResponseFolder, RequestBodyFolder, ResponseBodyFolder
ToTestFilter():   null if Filter absent | CreateForTagsFromEnvVariable | CreateForTestsFromEnvVariable
ToAppLauncherConfig(): Command, Readiness, Env from StartupConfig (command mode only; guard throw otherwise)
```

---

## Decisions Log

<!-- Add new at bottom. Never remove. -->

| Date | Decision | Reasoning | Alternatives Considered |
|------|----------|-----------|------------------------|
| 2026-06-02 | Validate-at-boundary — all validation in `SuiteConfiguration.Load*`, nothing downstream re-validates | Fixture loads config, validates, proceeds. `AppLauncher.Start` and `TestSuiteInitializer` receive pre-validated objects. Errors surface at config load time with file path and field name, not buried in execution stack. | Validate in each consumer (`AppLauncher`, `TestSuiteInitializer`) — scatters checks, user sees errors at runtime mid-test. |
| 2026-06-02 | `ReadinessConfig` from ENV-005 reused in `StartupConfig` | Fields identical; `ToAppLauncherConfig()` maps directly. Single definition, no duplication, dep acceptable within same library. | Define separate `SuiteStartupReadinessConfig` — identical fields, more verbosity for no gain. |
| 2026-06-02 | `StartupConfig.IsCommand` / `IsInProcess` helper properties added | Fixtures check mode without magic strings. `if (cfg.Startup.IsCommand)` is more readable and refactor-safe than `if (cfg.Startup.Mode == "command")`. | Compare Mode string directly — works but spreads a magic string across every fixture that branches on mode. |
| 2026-06-02 | `AuthToken` lives on `ApiConfig` as a resolved string, not wired through a ConfIT type | After `Load*`, token is a plain string. Fixture passes it to `TestHttpClient.Create` via its own `IAuthTokenProvider`. Keeps auth logic out of the config layer. | Add `ToTestHttpClient()` extension — premature; different auth schemes require code anyway. |
| 2026-06-02 | Design approved at Level 4. Blueprint complete — ready for implementation. | All four levels reviewed and confirmed. Constraints and key files recorded. | — |
| 2026-06-02 | `set` properties used on DTOs instead of `init` | Newtonsoft deserializes into DTOs — `set` is simpler and avoids any potential init-property deserialization edge cases across Newtonsoft versions. DTOs are internal config objects, not public immutable value types. | `init` — slightly more expressive but adds risk with older Newtonsoft builds. |
| 2026-06-02 | `using static ConfIT.UnitTest.ConfigTestHelper` in test file | Avoids duplicating `WriteYaml`/`Cleanup` helpers across three test classes in the same file. Clean pattern for file-scoped shared test infrastructure. | Helper in a base class — overkill for two utility methods in one file. |
| 2026-06-02 | `Validate` internal static class with four methods | `Required`, `OneOf`, `ExactlyOneSet`, `KnownKeys` cover all validation cases. Each method includes file path in error message. Custom implementation — no FluentValidation dependency — total ~40 lines. | FluentValidation NuGet — adds transitive dependency to core library; pre-deserialization key validation (unknown YAML keys) can't use it anyway. |
| 2026-06-02 | Env var resolution mutates the JObject in place before deserialization | Single-pass resolution: walk JObject strings, substitute `${VAR}`, then deserialize. No post-processing step. Post-deserialization string replacement would require reflection over arbitrary DTOs — fragile. | Resolve after deserialization via reflection — works but couples resolution to DTO property types. |

## Design: Level 4 — Contracts

### `src/ConfIT/SuiteConfiguration.cs`

```csharp
public static class SuiteConfiguration
{
    public static ComponentConfig LoadComponent(string filePath);
    public static IntegrationEnvironmentConfig LoadIntegration(string filePath, string? environment = null);
}

public sealed class ComponentConfig
{
    public StartupConfig Startup { get; init; } = new();
    public ApiConfig Api { get; init; } = new();
    public MockConfig? Mock { get; init; }
    public FolderConfig? Folders { get; init; }
    public FilterConfig? Filter { get; init; }
}

public sealed class StartupConfig
{
    public string Mode { get; init; } = "in-process";
    public bool IsInProcess => Mode == "in-process";
    public bool IsCommand   => Mode == "command";
    public string? Settings { get; init; }                 // in-process
    public string? Command { get; init; }                  // command
    public ReadinessConfig? Readiness { get; init; }       // command — same type as AppLauncherConfig.Readiness
    public Dictionary<string, string>? Env { get; init; }  // command
}

public sealed class IntegrationEnvironmentConfig
{
    public ApiConfig Api { get; init; } = new();
    public FolderConfig? Folders { get; init; }
    public FilterConfig? Filter { get; init; }
}

public sealed class ApiConfig    { public string? Url { get; init; } public string? AuthToken { get; init; } }
public sealed class MockConfig   { public string? Url { get; init; } }
public sealed class FolderConfig { public string? Response { get; init; } public string? RequestBody { get; init; } public string? ResponseBody { get; init; } }
public sealed class FilterConfig { public string? Strategy { get; init; } public string? EnvVariable { get; init; } }
```

### `src/ConfIT/Extension/SuiteConfigurationExtensions.cs`

```csharp
public static class SuiteConfigurationExtensions
{
    public static SuiteConfig ToSuiteConfig(this ComponentConfig config);
    public static TestFilter? ToTestFilter(this ComponentConfig config);
    public static AppLauncherConfig ToAppLauncherConfig(this ComponentConfig config);
    // throws InvalidOperationException if Mode != "command"

    public static SuiteConfig ToSuiteConfig(this IntegrationEnvironmentConfig config);
    public static TestFilter? ToTestFilter(this IntegrationEnvironmentConfig config);
}
```

### Known validation key sets (internal constants)

```
ComponentKnownKeys:   {startup, api, mock, folders, filter}
StartupKnownKeys:     {mode, settings, command, readiness, env}
ReadinessKnownKeys:   {url, port, timeoutSeconds, intervalMs}
ApiKnownKeys:         {url, authToken}
MockKnownKeys:        {url}
FolderKnownKeys:      {response, requestBody, responseBody}
FilterKnownKeys:      {strategy, envVariable}
EnvironmentKnownKeys: {api, folders, filter}
IntegrationRootKeys:  "default" is reserved; all other keys are environment names
```

---

## Design Summary

**Components and layer assignments:**
- `SuiteConfiguration` — Infrastructure, `src/ConfIT/`. Public static, two methods, all validation inside.
- Config DTOs (`ComponentConfig`, `StartupConfig`, `IntegrationEnvironmentConfig`, shared sub-DTOs) — Infrastructure, same file. Pre-validated on return.
- `SuiteConfigurationExtensions` — Infrastructure, `src/ConfIT/Extension/`. Pure mapping, no validation.

**Key contracts:**
- `SuiteConfiguration.LoadComponent(filePath)` — validates everything, resolves env vars, returns `ComponentConfig`
- `SuiteConfiguration.LoadIntegration(filePath, env?)` — resolves active environment, validates, returns `IntegrationEnvironmentConfig`
- `StartupConfig.IsCommand` / `IsInProcess` — helper properties, no magic strings in fixtures
- `ToAppLauncherConfig()` — pre-validated input, `AppLauncher.Start` trusts it
- `AuthToken` on `ApiConfig` — resolved string, fixture uses directly for `TestHttpClient.Create`

**Architectural constraints:**
- Validate-at-boundary: ALL validation in `Load*` methods. Downstream types (`AppLauncher`, `TestSuiteInitializer`) receive pre-validated objects.
- `${ENV_VAR}` resolution happens at load time inside `Load*`. After `Load*` returns, no `${...}` strings exist anywhere in the returned config.
- `ReadinessConfig` from `AppLauncher` (ENV-005) reused in `StartupConfig.Readiness`. Pending: rename `AppLauncher` namespace from `ConfIT.Server.Http` → `ConfIT.Server.Launcher` before implementation.
- Integration environment names are dynamic YAML keys — the `integration` section is NOT deserialized as a fixed DTO; environment block is extracted by key name then deserialized as `IntegrationEnvironmentConfig`.
- YAML parsed via existing `YamlConverter.ToJObject` (DSL-002); deserialization via `JsonConvert.DeserializeObject<T>` with camelCase resolver.

**Files changed:**
- `src/ConfIT/SuiteConfiguration.cs` — new file
- `src/ConfIT/Extension/SuiteConfigurationExtensions.cs` — new file
- `example/User.ComponentTests/suite.config.yaml` + fixture update
- `example/User.IntegrationTests/suite.config.yaml` + fixture update

**Design status: Approved — ready for implementation.**

---

## Open Questions

<!-- Resolved — all captured in Decisions Log -->

## Constraints

- Validate-at-boundary is non-negotiable. ALL validation runs inside `Load*`. Nothing downstream re-validates.
- `${ENV_VAR}` must be fully resolved before `Load*` returns. No unresolved references in the returned config.
- Integration environment names are dynamic YAML keys — cannot be deserialized as a fixed DTO. Extract by key name, then deserialize the block.
- `AppLauncher` namespace must be renamed to `ConfIT.Server.Launcher` before implementation — `SuiteConfiguration.cs` references `ReadinessConfig` from that namespace.
- YAML parsing via `YamlConverter.ToJObject` (existing infrastructure from DSL-002). Do not introduce a separate YAML parsing dependency.
- `ToAppLauncherConfig()` must throw `InvalidOperationException` if called on an in-process config — not silently return a broken config.

## Key Files

- `src/ConfIT/SuiteConfiguration.cs` — new file: `SuiteConfiguration` + all DTOs + internal `Validate` class
- `src/ConfIT/Extension/SuiteConfigurationExtensions.cs` — new file: projection extension methods
- `src/ConfIT/Server/Launcher/AppLauncher.cs` — moved from `Server/Http/` (namespace now `ConfIT.Server.Launcher`)
- `test/ConfIT.UnitTest/SuiteConfigurationTests.cs` — 22 new tests (186/186 passing)
- `example/User.ComponentTests/suite.config.yaml` — demo config, in-process mode
- `example/User.IntegrationTests/suite.config.yaml` — demo config, multi-environment integration
- `example/User.ComponentTests/SetUp/TestSuiteFixture.cs` — updated to `SuiteConfiguration.LoadComponent`
- `example/User.IntegrationTests/TestSuiteFixture.cs` — updated to `SuiteConfiguration.LoadIntegration`
- `example/User.ComponentTests/suite.config.yaml` — new demo config (in-process mode)
- `example/User.IntegrationTests/suite.config.yaml` — new demo config (multi-environment integration)
- `example/User.ComponentTests/SetUp/TestSuiteFixture.cs` — update to use `SuiteConfiguration.LoadComponent`
- `example/User.IntegrationTests/TestSuiteFixture.cs` — update to use `SuiteConfiguration.LoadIntegration`

