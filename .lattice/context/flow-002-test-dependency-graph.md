---
feature: Test Dependency Graph
requirement_doc: .lattice/requirements/features/flow-002-test-dependency-graph.md
created: 2026-06-03
---

# Test Dependency Graph

> Declarative `depends:` field in test definitions that causes dependent tests to be skipped (not errored) when a prerequisite fails, with cascading propagation across the chain.

## Design: Level 1 — Capabilities

1. **Declare test prerequisites** — `depends:` field in JSON/YAML names one or more tests from the same file that must have passed before this test runs.
2. **Skip on prerequisite failure** — When any declared prerequisite did not pass (failed or was itself skipped), the dependent is not executed; xUnit reports it as skipped with a reason message naming the direct prerequisite.
3. **Cascading skip propagation** — Skip status flows automatically: if B is skipped and C depends on B, C is also skipped without any extra declaration.
4. **Load-time validation** — A test naming a non-existent prerequisite, or a prerequisite defined later in the file, raises `InvalidDataException` before any test runs.

## Design: Level 2 — Components

| Component | Type | Location | Responsibility |
|---|---|---|---|
| `TestCase` | Extended DTO | `src/ConfIT/Server/Dto/TestCase.cs` | Gains `Depends: List<string>?` — DSL entry point |
| `DependencyValidator` | New internal static | `src/ConfIT/Util/DependencyValidator.cs` | Load-time: validates all `depends` entries exist and appear earlier in the file; throws `InvalidDataException` on violation; returns validated graph for registration |
| `TestDependencyStore` | New public singleton | `src/ConfIT/TestDependencyStore.cs` | Two dictionaries: Dict 1 (test name → status, runtime) and Dict 2 (test name → depends list, load-time); `RecordStatus` updates Dict 1; `CheckPrerequisites` consults Dict 2 then Dict 1 |
| `BaseTest` | Extended | `src/ConfIT/BaseTest.cs` | Gains dependency skip check before filter check; records status in store in all exit paths (pass, fail, filter-skip, dependency-skip) |

```
src/ConfIT/
├── BaseTest.cs               ← extended
├── TestDependencyStore.cs    ← new singleton
├── Server/Dto/TestCase.cs    ← +Depends field
└── Util/
    ├── DependencyValidator.cs ← new
    └── TestReader.cs          ← calls validator, registers graph into store
```

## Design: Level 3 — Interactions

**Flow A — Load time (collection phase, before any test runs)**
```
TestReader.GetTestsFromParsedFile(filePath)   ← new private helper
  1. Read + parse file → List<(name, JToken)>
  2. DependencyValidator.Validate(tests, filePath)
       builds name→position index
       for each test with depends: checks each dep exists + appears earlier
       throws InvalidDataException on first violation (names file, test, dep)
  3. yield [name, token, fileName] for each test

GetTestsForAFile  →  delegates to GetTestsFromParsedFile
GetTestsForAFolder →  loops files, calls GetTestsFromParsedFile per file

All files validated before first test runs (xUnit collects all MemberData upfront).
```

**Flow B — Runtime (each BaseTest.Execute call)**
```
Execute(testName, testCase)
  ① CheckPrerequisites — outside try/catch so SkipException propagates cleanly
      TestDependencyStore.Instance.CheckPrerequisites(testCase.Depends)
        for each dep in testCase.Depends: look up Dict 1
          absent  → throw (invariant violation — load-time validation should prevent this)
          not Passed → return (depName, depStatus)
        all passed → return null
      if blocked:
        TestDependencyStore.Instance.RecordStatus(testName, Skipped)
        TestResultCollector.Record(testName, Skipped)
        Assert.Skip("Skipped: prerequisite '{dep}' failed|was skipped")

  ② ShouldSkipTheTest (existing filter check)
      if skipped by filter:
        TestDependencyStore.Instance.RecordStatus(testName, Skipped)  ← NEW
        TestResultCollector.Record(testName, Skipped)
        return

  ③ [existing: mock setup, Before hook, HTTP call, assertions, Extract, Save]

  ④ success path:
        TestDependencyStore.Instance.RecordStatus(testName, Passed)   ← NEW
        TestResultCollector.Record(testName, Passed)

  ⑤ catch (Exception):
        TestDependencyStore.Instance.RecordStatus(testName, Failed)   ← NEW
        TestResultCollector.Record(testName, Failed)
        throw
```

## Design: Level 4 — Contracts

**`TestCase.cs`** — `public List<string>? Depends { get; set; }` added

**`TestRunStatus.cs`** (new top-level enum)
```csharp
namespace ConfIT;
public enum TestRunStatus { Passed, Failed, Skipped }
```
`TestResultCollector.TestStatus` nested enum removed; `TestResultCollector` updated to use `TestRunStatus`.

**`TestDependencyStore.cs`** (new singleton)
```csharp
public sealed class TestDependencyStore
{
    public static readonly TestDependencyStore Instance = new();
    private readonly ConcurrentDictionary<string, TestRunStatus> _status = new();
    public void RecordStatus(string testName, TestRunStatus status);
    public (string Name, TestRunStatus Status)? CheckPrerequisites(List<string>? depends);
    // Returns first not-passed dep, or null if all passed.
    // Throws InvalidOperationException if dep has no entry (invariant violation).
}
```

**`DependencyValidator.cs`** (new internal static)
```csharp
internal static class DependencyValidator
{
    internal static void Validate(IReadOnlyList<(string Name, JToken Token)> tests, string filePath);
    // Throws InvalidDataException: unknown dep name, or dep defined after current test.
}
```

**`TestReader.cs`** — `GetTestsFromParsedFile(filePath)` private helper added; public signatures unchanged.

**`BaseTest.cs`** — constructor and `Execute` signatures unchanged; `TestDependencyStore.Instance` accessed directly (same pattern as `VariableStore.Instance`).

---

## Design Summary

**Components and locations:**
- `src/ConfIT/Server/Dto/TestCase.cs` — `Depends: List<string>?` added
- `src/ConfIT/TestRunStatus.cs` — new top-level enum (replaces `TestResultCollector.TestStatus`)
- `src/ConfIT/TestDependencyStore.cs` — new singleton, one `ConcurrentDictionary<string, TestRunStatus>`
- `src/ConfIT/Util/DependencyValidator.cs` — new internal static, load-time validation
- `src/ConfIT/Util/TestReader.cs` — `GetTestsFromParsedFile` helper; public API unchanged
- `src/ConfIT/BaseTest.cs` — `Execute` body extended; signatures unchanged

**Key contracts:**
- `TestDependencyStore.Instance.RecordStatus(name, status)` — called in every exit path of `Execute`
- `TestDependencyStore.Instance.CheckPrerequisites(depends)` — called first in `Execute`, outside try/catch
- `DependencyValidator.Validate(tests, filePath)` — called once per file during collection, before first yield

**Architectural constraints:**
- `TestDependencyStore` is a singleton accessed directly, consistent with `VariableStore.Instance`
- Dependency skip check placed outside the `try/catch` block so `Assert.Skip` (`SkipException`) propagates cleanly to xUnit
- Filter-skipped tests record `Skipped` — uniform rule: every test that doesn't pass records its status
- `DependencyValidator` is pure (no side effects into the store) — validates only, throws or returns
- Public `TestReader` API unchanged — consumers see no difference

**Design status: Approved — ready for implementation**

## Decisions Log

<!-- Add new at bottom. Never remove. -->

| Date | Decision | Reasoning | Alternatives Considered |
|------|----------|-----------|------------------------|
| 2026-06-03 | Level 1 capabilities approved | Covers all scenarios in the requirement spec | — |
| 2026-06-03 | Level 2 components approved | Four components following existing patterns | — |
| 2026-06-03 | `TestDependencyStore` is a singleton (like `VariableStore`) | Consistent with existing pattern; cross-file name collision risk is low given explicit test names | Injected instance like `TestResultCollector` — rejected to keep fixture code simple |
| 2026-06-03 | Filter-skipped tests record `Skipped` in Dict 1 | Uniform rule: every test that doesn't pass records its status; dependents cascade consistently regardless of skip reason | Absent entry = not blocked — rejected: silently treats un-run prerequisites as passed |
| 2026-06-03 | Absent Dict 1 entry throws invariant violation | Load-time validation guarantees deps appear before dependents; no entry means execution order broke | Treat absent as not-blocked — rejected, masks violations |
| 2026-06-03 | Dict 2 (dependency graph) dropped — Flow B is sufficient | `testCase.Depends` is already in hand at execute time; no need to pre-register into a second dictionary | Two-dictionary design — rejected as redundant; DTO already carries the graph |
| 2026-06-03 | `TestRunStatus` extracted as top-level enum | `TestResultCollector.TestStatus` is a domain concept not specific to the collector; both classes should use the same type | Keep nested enum in `TestResultCollector` — rejected, creates cross-class coupling |
| 2026-06-03 | `DependencyValidator` is pure — no store side effects | Keeps validation and state mutation as separate concerns; validates only, throws or returns | Validate + register in one step — rejected, mixed concerns |
| 2026-06-03 | Design approved at Level 4. Blueprint complete — ready for implementation. | — | — |

## Open Questions

<!-- When resolved, capture as decision above and remove from here. -->

## Constraints

<!-- Non-negotiable once recorded. Add only when confirmed. -->

## Key Files

- `src/ConfIT/TestRunStatus.cs` — top-level enum (Passed/Failed/Skipped), replaces `TestResultCollector.TestStatus`
- `src/ConfIT/TestDependencyStore.cs` — singleton status tracker; `RecordStatus` + `CheckPrerequisites`
- `src/ConfIT/Util/DependencyValidator.cs` — load-time validation; throws `InvalidDataException` on unknown dep or forward ref
- `src/ConfIT/Util/TestReader.cs` — refactored: `GetTestsFromParsedFile` private helper calls validator; public API unchanged
- `src/ConfIT/BaseTest.cs` — `Execute` extended: dep skip check first, `RecordStatus` in all exit paths
- `src/ConfIT/Server/Dto/TestCase.cs` — `+Depends: List<string>?`
- `src/ConfIT/TestResultCollector.cs` — updated to use `TestRunStatus` (nested enum removed)
- `test/ConfIT.UnitTest/TestDependencyStoreTests.cs` — unit tests for store
- `test/ConfIT.UnitTest/Util/DependencyValidatorTests.cs` — unit tests for validator
- `example/User.ComponentTests/TestCase/depends.json` — component test demonstrating depends chain
- `example/User.IntegrationTests/TestCase/depends.json` — integration test demonstrating depends chain
