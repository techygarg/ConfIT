# ConfIT Roadmap

Feature ideas for making ConfIT significantly more capable and broadly adopted.
Each item has a stable ID. Detailed requirements live in `doc/features/`.

**ID format:** `[CATEGORY-NNN]`  
Categories: `ASSERT` · `DSL` · `FLOW` · `ENV` · `MOCK` · `TOOL` · `REACH` · `INT`

---

## Tier 1 — Remove the Capability Ceiling

The moment a test scenario gets moderately complex today, it requires C# fallback. These items close that gap.

| ID | Feature | One-liner | Status |
|----|---------|-----------|--------|
| [FLOW-001] | [Variable Extraction + Injection](features/flow-001.md) | Extract response values, inject into subsequent tests. Eliminates ~80% of `ITestProcessor` usage. | ✅ implemented |
| [ASSERT-001] | [Semantic Matcher Library](features/semantic-matcher-library.md) | Type-aware assertions: `isUuid`, `isIsoDate`, `greaterThan`, `hasLength`, `isNull`, and more. | ✅ implemented |
| [ASSERT-002] | [Field-Level Failure Output](features/assert-002-field-level-failure-output.md) | Replace raw JSON diffs with per-field pass/fail lines showing expected vs actual. | ✅ implemented |
| [ASSERT-003] | Soft Assertions | Continue evaluating all assertions after first failure; report all failures at once. | — |
| [DSL-001] | Snapshot / Record Mode | First run captures real responses as expected baselines. No hand-written expected bodies needed. | — |

---

## Tier 2 — Reduce Team Friction

Features that make ConfIT faster and easier for teams beyond the happy path.

| ID | Feature | One-liner | Status |
|----|---------|-----------|--------|
| [DSL-002] | YAML Support | Full DSL support in `.yaml` / `.yml` files alongside existing JSON. | — |
| [DSL-003] | VCR / Cassette Mode | Record real inter-service HTTP traffic; replay as WireMock stubs. No hand-crafted mocks needed. | — |
| [ENV-001] | Declarative Auth Profiles | Bearer, OAuth2, API key auth via config — no `IAuthTokenProvider` C# needed for common cases. | — |
| [ENV-002] | Environment Profiles | Named environments (local/staging/prod) with per-env URLs and headers. Switch via `TEST_ENV`. | — |
| [MOCK-001] | Mock Sequencing + Call Assertions | Sequential mock responses per call order; assert mock was called exactly N times. | — |
| [FLOW-002] | Test Dependency Graph | Declare `depends:` between tests; skip dependents when a prerequisite fails instead of erroring. | — |

---

## Tier 3 — Expand the Use Case Surface

Features that open ConfIT to new categories of testing work.

| ID | Feature | One-liner | Status |
|----|---------|-----------|--------|
| [TOOL-001] | OpenAPI → Test Stub Generation | Generate skeleton test files from an OpenAPI spec. Primary onboarding path for existing APIs. | — |
| [ASSERT-004] | JSON Schema Response Validation | Validate response structure against a JSON Schema file instead of exact body matching. | — |
| [FLOW-003] | Declarative Setup / Teardown | Define suite-level HTTP setup and teardown calls in config — no C# fixture needed. | — |
| [FLOW-004] | Data-Driven Parameterization | Run the same test with multiple input rows defined inline in the DSL. | — |
| [INT-001] | Event / Message Queue Assertions | Assert that Kafka/RabbitMQ/SQS messages were published after an HTTP call. | — |
| [TOOL-002] | HTML / JUnit Report Generation | Human-readable HTML report + JUnit XML for CI dashboards. | — |

---

## Tier 4 — Platform Play

Features that expand ConfIT's reach beyond the .NET/xUnit ecosystem.

| ID | Feature | One-liner | Status |
|----|---------|-----------|--------|
| [TOOL-003] | CLI Tool | `dotnet tool install -g confit` — run tests without xUnit or C# glue code. | — |
| [TOOL-004] | VS Code Extension + JSON Schema | Autocomplete and validation for test files; publish schema to SchemaStore.org. | — |
| [REACH-001] | NUnit / MSTest Adapters | Thin adapters so ConfIT works with NUnit and MSTest, not just xUnit. | — |
| [INT-002] | GraphQL Support | First-class GraphQL query/mutation test definitions with the same matcher model. | — |
| [MOCK-002] | Chaos / Fault Injection | Add delays, connection drops, partial responses to mock interactions for resilience testing. | — |
| [TOOL-005] | Custom Matcher Plugin API | Register project-specific matchers (domain IDs, enum values) without forking the library. | — |

---

## Tier 5 — Differentiators

Longer-horizon features that could make ConfIT uniquely positioned in the ecosystem.

| ID | Feature | One-liner | Status |
|----|---------|-----------|--------|
| [ASSERT-005] | Performance Assertions | Assert `maxResponseTimeMs` per test; suite-level p95/p99 thresholds. | — |
| [TOOL-006] | Negative Test / Mutation Generation | Auto-generate missing-field and wrong-type variants from a passing test definition. | — |
| [REACH-002] | Contract Testing / Pact Integration | Export test definitions as Pact consumer contracts or validate against provider stubs. | — |
| [INT-003] | WebSocket / SSE / Streaming | Test APIs that push data over WebSocket or Server-Sent Events. | — |
| [TOOL-007] | AI-Assisted Test Generation | Generate test cases from an OpenAPI spec or endpoint description using Claude. | — |
