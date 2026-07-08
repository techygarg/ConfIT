---
epic: Reduce Team Friction
status: in progress
---

# Reduce Team Friction

Features that make ConfIT faster and easier for teams beyond the happy path.

## Features

<!-- GENERATED — regenerated from features/*.md frontmatter where epic matches, do not hand-edit below -->

| Feature | Summary |
|---|---|
| [[DSL-002] YAML Support](../features/dsl-002-yaml-support.md) | Full DSL support in `.yaml` / `.yml` files alongside existing JSON. |
| [DSL-003] VCR / Cassette Mode | Record real inter-service HTTP traffic; replay as WireMock stubs. No hand-crafted mocks needed. |
| [[ENV-001] Declarative Auth Profiles](../features/env-001-declarative-auth-profiles.md) | Bearer, OAuth2, API key auth via config — no `IAuthTokenProvider` C# needed for common cases. |
| [ENV-002] Environment Profiles | Named environments (local/staging/prod) with per-env URLs and headers. Switch via `TEST_ENV`. |
| [[ENV-003] Modern Test Host Initialization](../features/env-003-modern-test-host-initialization.md) | Replace legacy `WebHost.CreateDefaultBuilder` + `TestServer` with `WebApplicationFactory<TProgram>`; eliminate Startup subclass boilerplate. |
| [[ENV-004] Declarative Suite Configuration](../features/env-004-declarative-suite-bootstrap.md) | `suite.config.yaml` drives full fixture setup — two sections (component + integration), two startup modes, multi-environment integration targets. |
| [[ENV-005] AppLauncher](../features/env-005-app-launcher.md) | Start an external process before component tests run — readiness probing, env injection, graceful teardown. Powers ENV-004 command mode. |
| [MOCK-001] Mock Sequencing + Call Assertions | Sequential mock responses per call order; assert mock was called exactly N times. |
| [[FLOW-002] Test Dependency Graph](../features/flow-002-test-dependency-graph.md) | Declare `depends:` between tests; skip dependents when a prerequisite fails instead of erroring. |

<!-- END GENERATED -->
