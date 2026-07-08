---
epic: Remove the Capability Ceiling
status: in progress
---

# Remove the Capability Ceiling

The moment a test scenario gets moderately complex today, it requires C# fallback. These items close that gap.

## Features

<!-- GENERATED — regenerated from features/*.md frontmatter where epic matches, do not hand-edit below -->

| Feature | Summary |
|---|---|
| [[FLOW-001] Variable Extraction + Injection](../features/extraction-and-injection.md) | Extract response values, inject into subsequent tests. Eliminates ~80% of `ITestProcessor` usage. |
| [[ASSERT-001] Semantic Matcher Library](../features/semantic-matcher-library.md) | Type-aware assertions: `isUuid`, `isIsoDate`, `greaterThan`, `hasLength`, `isNull`, and more. |
| [[ASSERT-002] Field-Level Failure Output](../features/assert-002-field-level-failure-output.md) | Replace raw JSON diffs with per-field pass/fail lines showing expected vs actual. |
| [ASSERT-003] Soft Assertions | Continue evaluating all assertions after first failure; report all failures at once. |
| [DSL-001] Snapshot / Record Mode | First run captures real responses as expected baselines. No hand-written expected bodies needed. |

<!-- END GENERATED -->
