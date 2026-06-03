---
feature: Test Dependency Graph
epic: Reduce Team Friction
status: draft
priority: P2
depends_on: [FLOW-001]
personas: [test-author, ci-engineer]
source_docs: []
---

# [FLOW-002] Test Dependency Graph

## Problem Statement

When a prerequisite test fails, dependent tests that rely on extracted variables from that test will also fail — either with an "undefined variable" error (if the failed test's extract block never ran) or with misleading assertion failures (if they run against stale state). Neither outcome reflects what actually happened: the dependent test did not fail on its own merits; it lost its prerequisite.

Without an explicit dependency mechanism, a single root failure cascades into multiple reported failures. CI output becomes noisy: ten red tests when only one actually broke. Root-cause analysis requires reading the failures in sequence to identify which is the originating failure and which are downstream noise.

## User / Personas

- **test-author** — writes ConfIT test files with sequential chains (create → fetch → delete). Wants dependent tests to be cleanly skipped when their prerequisite fails, rather than reporting confusing errors that obscure which test actually broke.
- **ci-engineer** — reviews test results in CI dashboards and pull request checks. Wants a clear distinction between a test that failed independently and a test that was skipped because its prerequisite failed.

## Scope

**In scope:**
- `depends` field in JSON and YAML test definitions — accepts a list of test names in the same file
- Load-time validation: all named prerequisites must exist in the same file and must appear earlier in definition order than the declaring test
- Runtime skip: when any named prerequisite did not succeed (failed or was itself skipped), the dependent test is not executed and is reported as skipped with a reason message naming the direct prerequisite
- Cascading skip: if B depends on A and A is skipped, B is also skipped; the skipped status propagates to all transitive dependents

**Out of scope:**
- Cross-file dependencies — scoped to a single file by design: variable stores are per-file (FLOW-001), xUnit provides no stable cross-collection ordering guarantee, and cross-file test names require disambiguation syntax. Suite-wide dependency graphs are a separate, larger feature.
- Dynamic or conditional dependencies (depends-on-fail, depends-on-any, etc.)
- Automatic dependency inference from `{{variable}}` usage — `depends:` is always explicit
- Execution reordering — tests still run in file definition order; `depends:` controls skip behavior only, not ordering

## Boundary Conditions

- A test with no `depends` field runs unconditionally in definition order (unchanged from today)
- A "skipped" test is distinct from a "failed" test — xUnit reports these separately
- A skipped test's `extract` block does not run; variables it would have set are never populated in the store
- A test that declares `depends: []` (empty list) is valid and equivalent to no `depends` field
- A test with multiple entries in `depends:` is skipped if **any** of them did not succeed

## Assumptions

- Sequential, in-file-order execution (established in FLOW-001) is the foundation; FLOW-002 does not change execution order
- xUnit's skip mechanism can carry a reason message that identifies the failing prerequisite
- Forward-reference detection uses definition order in the parsed file, not alphabetical or any other ordering

## Scenarios

### Scenario 1: Prerequisite passes — dependent executes
A test declares `depends:` on a prior test that succeeded. Execution is unchanged — the dependent runs as if `depends:` were absent.

**Acceptance Criteria:**
- Given a test file where `ShouldFetchUser` declares `depends: [ShouldCreateUser]`, when `ShouldCreateUser` passes, then `ShouldFetchUser` runs and its result is determined by its own assertions alone.
- Given the prerequisite passed and had an `extract` block, when the dependent runs, then variables extracted by the prerequisite are available for injection in the normal way.
- Given the dependent runs and its assertions pass, then it is reported as passed — not skipped.

### Scenario 2: Prerequisite fails — dependent is skipped
A test declares `depends:` on a prior test that failed. The dependent is not executed.

**Acceptance Criteria:**
- Given `ShouldCreateUser` fails, and `ShouldFetchUser` declares `depends: [ShouldCreateUser]`, when the test runner reaches `ShouldFetchUser`, then it is not executed.
- Given the dependent is not executed, then xUnit reports it as skipped (not failed).
- Given the dependent is skipped, then the skip reason message names the direct failed prerequisite — e.g., `Skipped: prerequisite 'ShouldCreateUser' failed`.
- Given the dependent is skipped, then its own `extract` block does not run.

### Scenario 3: Cascading skip — transitive dependents skip
A test is skipped because its prerequisite failed. Tests that depend on the skipped test are also skipped.

**Acceptance Criteria:**
- Given test A fails, B declares `depends: [A]`, and C declares `depends: [B]`, when A fails, then both B and C are reported as skipped.
- Given C is skipped because B was skipped, then C's skip message names B as the direct prerequisite that did not pass — e.g., `Skipped: prerequisite 'ShouldFetchUser' was skipped`.
- Given a test declares multiple entries in `depends:`, when any one of them did not succeed, then the test is skipped.

### Scenario 4: Forward reference rejected at load time
A test names a prerequisite that is defined later in the file. This is rejected before any tests run.

**Acceptance Criteria:**
- Given `ShouldFetchUser` (defined first) declares `depends: [ShouldCreateUser]` where `ShouldCreateUser` appears after it in the file, when the test file is loaded, then loading fails immediately with an error before any test executes.
- Given the load error, then the message names the file, the test declaring the invalid dependency, and the forward-referenced test name.

### Scenario 5: Unknown prerequisite name rejected at load time
A test names a prerequisite that does not exist in the file.

**Acceptance Criteria:**
- Given `ShouldFetchUser` declares `depends: [ShouldCrateUser]` (a name that does not exist in the file), when the test file is loaded, then loading fails immediately with an error before any test executes.
- Given the load error, then the message names the file, the test with the invalid dependency, and the unrecognised prerequisite name.

## Implementation Notes

1. **DSL field** — add `depends` as an optional `string[]` on the test definition DTO; parsed identically from JSON and YAML without format-specific handling
2. **Load-time validation** — after parsing a file, scan all `depends` lists; for each entry, verify it names a test that (a) exists in the file and (b) appears before the declaring test in definition order; fail fast on first violation with a clear error message
3. **Runtime skip evaluation** — before executing each test, check every entry in its `depends` list against a per-run status map (`passed` / `failed` / `skipped`); if any entry is not `passed`, mark the current test as skipped and record the skip reason naming the direct prerequisite
4. **Cascading propagation** — the status map drives cascading naturally: a skipped test records `skipped` status, and any test depending on it reads that status as "not passed" and is itself skipped with its own reason message

## Decisions

- **Skip message depth:** Reports the direct prerequisite only — not the root failure. Each message names the test immediately above it in the chain (`Skipped: prerequisite 'ShouldFetchUser' was skipped`). Messages chain readably in test output without requiring the runner to trace the full graph.
- **Forward-reference detection:** Hard error. A forward reference always produces undefined behaviour with no useful runtime outcome; warning-and-continue would silently allow a misconfigured dependency that never fires.

## Links

- Design: [flow-002-test-dependency-graph.md](../../.lattice/context/flow-002-test-dependency-graph.md)
- Epic index: [index.md](../index.md)
- Related: [FLOW-001 Variable Extraction + Injection](extraction-and-injection.md) — `depends:` complements extraction; when a prerequisite fails its `extract` block does not run, making `depends:` the correct way to prevent confusing undefined-variable errors in downstream tests
