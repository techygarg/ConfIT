# Documentation Strategy

This file tracks which documents exist, which are missing, and the conventions that keep them consistent. It is a working planning document — not published to users.

---

## Current State

| File | Status | What it covers |
|---|---|---|
| `suite-setup.md` | ✅ current | `SuiteBootstrapper` (primary), adapter chain, manual wiring, shared concepts |
| `app-launcher.md` | ✅ current | Command mode, language-agnostic testing, self-seeding, readiness probes, direct use |
| `auth-profiles.md` | ✅ current | Bearer, OAuth2, API key, OAuth2 stub pattern, custom `IAuthTokenProvider` |
| `extending-confit.md` | ✅ current | `ITestOutputLogger`, `ITestProcessor`/factory, `CustomMatchers`, factory + bootstrapper patterns |
| `test-execution-flow.md` | ✅ current | ASCII flow diagrams — fixture startup, execution loop, suite summary |
| `matchers-and-patterns.md` | ✅ current | `ignore`, `pattern`, `semantic`, nested paths, custom matchers |
| `variable-extraction-and-injection.md` | ✅ current | `extract`, `{{inject}}`, `${ENV}`, error cases |
| `test-dependency-graph.md` | ✅ current | `depends:` field, skip-not-fail semantics, cascading, load-time validation |
| `test-file-format.md` | ✅ current | DSL structure, all fields including `depends:`, JSON and YAML format |
| `mock-interactions.md` | ✅ current | WireMock stubs, request matching, YAML anchor reuse |
| `test-filtering.md` | ✅ current | TEST_TAGS, TEST_NAMES, CI patterns |
| `failure-output.md` | ✅ current | Field-level failure messages, path notation, suite summary, debugging tips |
| `doc-strategy.md` | ✅ this file | Planning only |

---

## What Changed in the Architecture Transformation (Buckets 1–4 + SuiteBootstrapper)

### Bucket 1 — Structural Skeleton (namespace reorganisation)

Key namespace changes that affect documentation:
- `ConfIT.Server.Dto` → `ConfIT.Model`
- `ConfIT.Server.Http` → `ConfIT.Runner.Http`
- `ConfIT.Server.Boot` → `ConfIT.Runner.Boot`
- `ConfIT.Util` → `ConfIT.Matching`, `ConfIT.Reader`, `ConfIT.Reporting`
- `IntegrationEnvironmentConfig` → `IntegrationConfig`

These are internal library types — consumers access them only via extension methods and the bootstrapper, so docs show minimal type names.

### Bucket 2 — Clean Data Model

- `BaseRequestResponse` renamed to `HttpPayload`
- `Initialize()` removed from all model types — `TestCaseResolver` handles file loading
- `TestFilter.Tags` / `TestNames` changed from `List<string>` to `IReadOnlyList<string>`
- `TestCase`, `ApiInteraction`, `HttpTestRequest`, `HttpTestResponse` are now pure data — no file I/O

Consumer-visible impact: none in normal usage (extension method `ToTestCase()` still works identically).

### Bucket 3 — Execution Pipeline Hardening

- `TestSuiteContext` record introduced — single object that bundles `HttpClient`, `Config`, `ProcessorFactory`, `Filter`, `ResultCollector`
- `BaseTest` now has `protected BaseTest(TestSuiteContext context, ITestOutputLogger? logger)` as the primary constructor; the 6-param constructor is kept as a delegating overload
- `BaseTest.Config` changed from `protected static` to `protected` (instance property via `_config`)
- `TestHttpClient` now builds per-request `HttpRequestMessage` instead of mutating `DefaultRequestHeaders`
- `AuthConfig.ValidateAuth()` moved into `SuiteConfiguration`

Consumer-visible impact: test class constructor simplified to `base(fixture.Context, logger)`.

### Bucket 4 — API Surface Polish

- `MatchResult` record introduced — `ResultMatcher.MatchResponseBody` returns `MatchResult` instead of throwing; `BaseTest.Verify` owns the assertion
- `SemanticMatcher.Apply` returns `string?` (failure description) instead of throwing via FluentAssertions
- `ITestReporter` skeleton added to `Reporting/`

Consumer-visible impact: none. `MatchResult` is an internal decoupling; the assertion behaviour from the test's perspective is identical.

### SuiteBootstrapper

New types:
- `SuiteBootstrapper` — static factory with `ForComponent<TStartup>`, `ForCommand`, `ForIntegration`
- `BootstrappedSuite` — disposable wrapper holding `TestSuiteContext Context` and `IServiceProvider? Services`

Consumer impact: fixtures shrink from 15–28 lines to 5–10. `SuiteBootstrapper` is the new recommended primary path. The adapter chain (`ToSuiteConfig()` etc.) and manual wiring remain fully supported.

---

## Document Inventory — Tier Structure

### Tier 1 — Foundation (everyone needs these)

| Document | One-liner |
|---|---|
| `suite-setup.md` | How to wire a ConfIT suite — bootstrapped (primary), adapter chain, manual |
| `test-execution-flow.md` | What happens at runtime — fixture startup, execution loop, suite summary |
| `app-launcher.md` | Out-of-process startup, language-agnostic scope, self-seeding, AppLauncher direct use |
| `auth-profiles.md` | All auth types, OAuth2 component stub pattern, custom `IAuthTokenProvider` |
| `test-file-format.md` | Full DSL reference — all fields, JSON and YAML |
| `matchers-and-patterns.md` | Asserting on dynamic fields without writing code |
| `variable-extraction-and-injection.md` | Passing data between tests declaratively |

### Tier 2 — Features

| Document | One-liner |
|---|---|
| `mock-interactions.md` | WireMock stubs inline, `bodyFromFile`, request/response matching |
| `test-filtering.md` | `TEST_NAMES`, `TEST_TAGS`, `TestFilter` factory methods, CI usage |
| `test-dependency-graph.md` | `depends:` field, skip-not-fail, cascading, load-time validation |

### Tier 3 — Reference

| Document | One-liner |
|---|---|
| `extending-confit.md` | Extension points: `ITestOutputLogger`, `ITestProcessor`, `CustomMatchers`, `ITestProcessorFactory` with bootstrapper |
| `failure-output.md` | Reading per-field failure messages, debugging a failing suite |

---

## Conventions

**One concept per file.** If a document needs to say "for X, see the Y section of Z", that's a signal to split.

**Feature-first, not API-first.** Lead with the user problem and the DSL, not with the class or method name.

**Live examples are mandatory.** Every significant code snippet must end with a `📄 Live example:` link pointing to a real test file in `example/`. Broken links are caught in review.

**Prefer component tests for examples.** They are self-contained (no external services). Mention integration tests only when the behaviour differs.

**Code snippet format.** Use JSON as the primary snippet format. When YAML is relevant, show it as an alternative after the JSON, not instead of it. For `suite.config.yaml`, YAML is primary.

**Length target.** Each doc should be readable in under 10 minutes. If a page scrolls longer than `matchers-and-patterns.md`, consider splitting.

**Bootstrapper is the recommended path.** In code examples, show `SuiteBootstrapper` by default. Show the adapter chain only in the "custom wiring" sections. Show manual `SuiteConfig` construction only in the "manual wiring / advanced" sections.
