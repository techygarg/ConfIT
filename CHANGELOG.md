# Changelog

All notable changes to ConfIT are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
ConfIT uses [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added

- **`mock.enableLogs`** — WireMock request logging is now switchable from `suite.config.yaml` instead of only from fixture code. Beyond debugging a stub that will not match, it turns an unknown dependency surface into a listing: run a component test with no `mock:` block and every outbound call is logged as an unmatched request, which is enough to write the interactions from. Works whatever language the service under test is written in, since it observes HTTP rather than code. See [Mock Interactions](doc/mock-interactions.md#discovering-what-a-service-calls).

- **Agent skills** — three [Agent Skills](https://docs.claude.com/en/docs/claude-code/skills) in [`skills/`](skills), distributed as agent plugins: `confit-suite-setup` (wire a suite), `confit-component-tests` (developer, mid-implementation — works from the controller plus the mocks behind it) and `confit-integration-tests` (QA, post-deployment — black box, works from a spec, collection or live endpoint and assumes no access to the service source). See [AI Agent Skills](doc/ai-skills.md).

- **Plugin manifests** — the repository is its own Claude Code marketplace (`/plugin marketplace add techygarg/ConfIT`, then `/plugin install confit@confit`), and carries a Codex manifest. Further agents are onboarded by adding one manifest directory each.

- **`example/README.md`** — maps the three startup modes to their example projects, states what every suite structurally needs, and lists what is demo-specific so none of it is copied into a consuming project.

- **Validation tooling in [`tools/`](tools)** — `check-testcases.py` statically validates test definitions (missing expected bodies, invalid `depends:`, unresolvable `{{variables}}`, `mock:` blocks in an integration suite, unregistered files, and matcher problems such as an unknown name, a missing closing parenthesis, or a wildcard in a `semantic` path). `verify-suite.sh` checks project wiring. Both exit non-zero on error and run in CI via the new `make skills` target, and both read ground truth from `src/ConfIT/` — supported frameworks, built-in matcher names — rather than hardcoding it.

### Changed

- **Skills read ConfIT's own `example/` and `doc/` instead of shipping templates.** Embedded copies of the fixture, `suite.config.yaml` and `.csproj` drifted from the real projects, so they were removed; each skill now resolves the repository it ships inside and reads the suites CI verifies.

### Fixed

- **`example/User.IntegrationTests`** — `TestReader.GetTestsForAFile` was called with its arguments reversed; removed `AuthTokenProvider.cs`, which implemented `IAuthTokenProvider` but was never referenced, since no `SuiteBootstrapper` overload accepts a custom provider (auth is configured declaratively). The `qa` environment now reads its URL from `${QA_API_URL}` instead of a hardcoded host.
- **`example/User.ComponentTests`** — `appsettings.Tests.json` carried an `environmentVariables` key that nothing reads; `UserDbInitializer.Seed()` had a commented-out body and now seeds real reference data, demonstrating the `onStarted` hook.
- **`example/User.ComponentTests.AppLauncher`** — configuration comments referred to a `Startup.SeedDatabase` method that does not exist, and now describe the actual `IsLocalComponentTests` / `UserDbContext` mechanism; the namespace was aligned with the project folder.

Other than `mock.enableLogs`, the library is unchanged — the skills are distributed as agent plugins, not as package content.

---

## [3.1.0]

### Added

- **GraphQL request support** — a `graphql` block (`query` / `queryFromFile` / `variables` / `operationName`) on `api.request` or on a mock interaction's `request` compiles into the standard `{ query, variables, operationName }` JSON body at test-load time. `method` defaults to `POST` only if unset, and `Content-Type: application/json` is added only if not already present — the rest of the pipeline (matchers, `extract`, `{{inject}}`, mocking) behaves exactly as it does for any other request. See [GraphQL Support](doc/graphql-support.md).

- **Array-wildcard matcher segments** — `ignore` and `pattern` paths accept a literal `*` segment (e.g. `errors__*__path`) to target a field across every element of an array regardless of length, so a variable-length array — such as a GraphQL `errors` list — can be matched without listing an index per entry.

### Changed

- **`ResultMatcher` wildcard matching** — parent-path template construction is hoisted to once per matcher rule instead of once per JSON property visited during tree traversal, and array-index regex normalization is skipped entirely when neither the template nor the path contains an array index.

### Documentation

- **[GraphQL Support](doc/graphql-support.md)** — new reference for the `graphql` block: structure, `query` vs `queryFromFile`, and how it composes into the request body.
- **[Test File Format](doc/test-file-format.md)** and **[Matchers and Patterns](doc/matchers-and-patterns.md)** — updated with the `graphql` block and array-wildcard matcher segment documentation.
- **NuGet package README** — `doc/Package.Readme.md` added as the package description shown on nuget.org.

---

## [3.0.0]

### Added

- **YAML test files** — `.yaml` / `.yml` test definitions alongside JSON. Full DSL parity: all matchers, extract/inject, mock interactions, and tags work identically in YAML. YAML anchors and comments supported.

- **Variable extraction and injection** — `extract` captures response field values; `{{varName}}` injects them into subsequent requests and paths. `${ENV_VAR}` references pull environment values directly into test definitions. Covers the majority of `ITestProcessor` use cases declaratively.

- **Semantic matcher library** — Type-aware named assertions: `isUuid`, `isIsoDate`, `isIsoDateTime`, `isEmail`, `isUrl`, `greaterThan(n)`, `lessThan(n)`, `hasLength(n)`, `isNull`, `isNotNull`, `isNotEmpty`. Custom matchers registered as a `customMatchers` parameter to `SuiteBootstrapper` or via `SuiteConfig.CustomMatchers` in the manual wiring path.

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
  - `cfg.ToAuthTokenProvider()` extension method on `ComponentConfig` and `IntegrationConfig` constructs the configured provider; returns `null` when no `auth:` block is declared, preserving existing no-auth behaviour

- **net10.0 support** — Library targets both `net9.0` and `net10.0`.

- **`SuiteBootstrapper`** — single-call fixture factory that replaces the manual adapter chain. Three methods cover every suite type:
  - `SuiteBootstrapper.ForComponent<TStartup>(configFile, configureServices?, onStarted?, customMatchers?)` — in-process component suite; `onStarted` callback receives `IServiceProvider` for DB seeding and project-specific init
  - `SuiteBootstrapper.ForCommand(configFile, customMatchers?)` — command/AppLauncher suite; starts the external process, builds auth provider, manages full lifecycle
  - `SuiteBootstrapper.ForIntegration(configFile, environment?, customMatchers?)` — integration suite; selects environment via parameter, `TEST_ENVIRONMENT` env var, or YAML default

- **`BootstrappedSuite`** — disposable wrapper returned by all `SuiteBootstrapper` methods. Holds `TestSuiteContext Context` and `IServiceProvider? Services` (in-process suites only). `Dispose()` prints the suite summary then shuts down infrastructure in the correct order. Reduces fixture boilerplate from 15–28 lines to 5–10 lines.

- **`TestSuiteContext`** — record that bundles the five objects `BaseTest` needs (`HttpClient`, `Config`, `ProcessorFactory`, `Filter`, `ResultCollector`). Passed as a single parameter to the primary `BaseTest` constructor. Supports the `with` expression for augmenting a bootstrapped context (e.g. adding a `ProcessorFactory` on top of a bootstrapped suite).

- **`TestCaseResolver`** — static class in `ConfIT.Reader` that owns file I/O for `bodyFromFile` loading and `override` merging. Replaces the self-mutating `Initialize()` pattern on model types.

- **`MatchResult`** — record in `ConfIT.Matching` that decouples diff computation from assertion. `ResultMatcher.MatchResponseBody` returns `MatchResult` instead of calling FluentAssertions directly; `BaseTest.Verify` owns the single assertion boundary.

### Changed

- **`TestSuiteInitializer` modernised** — Replaced legacy `WebHost.CreateDefaultBuilder()` and manual `TestServer` construction with `WebApplicationFactory<TProgram>`. Generic parameter is the app's entry point class (typically `Startup`); the `TestServerStartup` subclass pattern is eliminated. Service overrides via an optional `Action<IServiceCollection>` callback. `Services` (`IServiceProvider`) replaces the deprecated `TestServer` property.

- **`IAuthTokenProvider`** gains a `HeaderKey()` method with a default implementation of `"Authorization"`. Existing custom providers that only implement `Token()` continue to work unchanged — the default covers the standard bearer case. `TestHttpClient` now calls `HeaderKey()` to determine the header name, making API key auth expressible through the same interface without a separate injection point.

- **`BaseTest` constructor** — primary constructor is now `protected BaseTest(TestSuiteContext context, ITestOutputLogger? logger = null)`. The previous 6-parameter constructor is kept as a delegating overload for backward compatibility; existing subclasses continue to compile without changes.

- **`BaseTest.Config`** — changed from `protected static SuiteConfig Config` to `protected SuiteConfig Config` (instance property). Eliminates a race condition where parallel test classes with different configurations could overwrite each other's static field.

- **`TestFilter.Tags` and `TestFilter.TestNames`** — changed from `List<string> { get; set; }` to `IReadOnlyList<string> { get; init; }`. Filters are immutable after construction; the factory methods (`CreateForTags`, `CreateForTagsFromEnvVariable`, etc.) are the intended construction path.

- **`SemanticMatcher.Apply`** — now returns `string?` (a failure description, or `null` on success) instead of throwing via FluentAssertions. FluentAssertions is invoked only at the `BaseTest.Verify` boundary, decoupling matcher logic from the assertion framework.

- **`TestHttpClient`** — builds a fresh `HttpRequestMessage` per `Execute()` call instead of clearing and repopulating `DefaultRequestHeaders`. Eliminates a thread-safety issue under concurrent request execution.

- **`AuthConfig`** — validation logic moved from `AuthConfig.ValidateAuth()` into `SuiteConfiguration.ValidateComponent` / `ValidateIntegrationEnv`. `AuthConfig` is now a pure data class with no methods.

- **`BaseTest.Execute` accepts raw `JToken`** — new overload `Execute(string testName, JToken test, string? sourceFile)` resolves the token to a `TestCase` internally using the folder paths already present in `_config`. Test classes no longer need to call `test.ToTestCase(Config.RequestBodyFolder, Config.ResponseBodyFolder)` or `test.ToTestCase(null, null)` explicitly — `await Execute(testName, test, sourceFile)` works identically for both component and integration suites. The existing `Execute(string, TestCase, string?)` overload is unchanged.

- **Filter env var naming** — example env var names renamed from `RUN_POOLS` / `RUN_TESTS` to `TEST_TAGS` / `TEST_NAMES` for clarity. These are project-level conventions set in `suite.config.yaml` via the `envVariable:` field, not library constants; any name is valid.

- **Namespace restructure** — types reorganised into purpose-specific namespaces. Consumers using `SuiteBootstrapper`, `BaseTest`, and extension methods are unaffected. Code with explicit `using` directives for the old namespaces must update:

  | Old namespace | New namespace | Types affected |
  |---|---|---|
  | `ConfIT.Server.Dto` | `ConfIT.Model` | `TestCase`, `TestApi`, `MockInteraction`, `HttpTestRequest`, `HttpTestResponse`, `Matcher`, `TestMock` |
  | `ConfIT.Server.Http` | `ConfIT.Runner.Http` | `TestHttpClient`, `TestSuiteInitializer` |
  | `ConfIT.Server.Boot` | `ConfIT.Runner.Boot` | `AppLauncher`, `AppLauncherConfig`, `AppLauncherException`, `ReadinessConfig` |
  | `ConfIT.Server.Mock` | `ConfIT.Runner.Mock` | `HttpMockServer` |
  | `ConfIT.Util` | `ConfIT.Matching` | `ResultMatcher`, `SemanticMatcher`, `DeltaFormatter` |
  | `ConfIT.Util` | `ConfIT.Reader` | `TestReader`, `YamlConverter` |
  | `ConfIT.Util` | `ConfIT.Reporting` | `TestColor`, `TestResultCollector` |

- **`IntegrationEnvironmentConfig` renamed to `IntegrationConfig`** — the type returned by `SuiteConfiguration.LoadIntegration` and used by the `ToSuiteConfig()`, `ToTestFilter()`, and `ToAuthTokenProvider()` extension methods.

- **`BaseRequestResponse` renamed to `HttpPayload`** — the base class for `HttpTestRequest` and `HttpTestResponse`. Direct references must be updated; types accessed through normal test wiring are unaffected.

- **`BuilderExtension` made `internal`** — the WireMock extension methods in `ConfIT.Runner.Mock` are no longer part of the public API.

### Deprecated

- `TestSuiteInitializer.TestServer` — use `TestSuiteInitializer.Services` (`IServiceProvider`) instead.
- `ITestProcessor` / `ITestProcessorFactory` — the `extract` + `{{inject}}` DSL covers the majority of use cases without C#. These interfaces remain for genuinely imperative cases such as request signing or external side effects.

### Removed

- **net8.0 support** — The library now targets `net9.0` and `net10.0` only. Projects still on .NET 8 must upgrade before adopting this release.

- **`BaseRequestResponse.Initialize(string folder)`** and **`ApplyOverride(JToken)`** — file I/O is now handled by `TestCaseResolver`. Model types are pure data carriers. `JToken.ToTestCase(requestFolder, responseFolder)` calls `TestCaseResolver` internally; consumer call sites are unchanged.

- **`ApiInteraction.Initialize(string, string)`** and **`TestCase.Initialize(string, string)`** — same as above.

- **`EnvironmentKeys` class** — the `EnvironmentKeys.TestEnvironment` constant is inlined into `SuiteConfiguration`. Remove any `using ConfIT.Constant;` directives.

### Documentation

- **[Suite Setup](doc/suite-setup.md)** — full rewrite: `SuiteBootstrapper` is the primary path; adapter chain and manual wiring documented as advanced/custom alternatives.
- **[Test Filtering](doc/test-filtering.md)** — full rewrite: leads with the YAML `filter:` block; documents both strategies (`tags` / `tests`), the `envVariable` field, and the untagged-test skip rule.
- **[Auth Profiles](doc/auth-profiles.md)** — fixture code examples updated to reflect `SuiteBootstrapper` and bootstrapped path.
- **[AppLauncher](doc/app-launcher.md)** — fixture code examples updated to `SuiteBootstrapper.ForCommand()`.
- **[Extending ConfIT](doc/extending-confit.md)** — constructor snippets, `ITestProcessorFactory` wiring with `with` expression, and `CustomMatchers` parameter updated.
- **[Test Execution Flow](doc/test-execution-flow.md)** — startup diagrams updated to show `SuiteBootstrapper` as entry point.
- **[Test Dependency Graph](doc/test-dependency-graph.md)** — full reference for the `depends:` field: skip-not-fail semantics, cascading skip propagation, load-time validation rules, and interaction with variable extraction.
