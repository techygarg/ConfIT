---
feature: ASSERT-002 Field-Level Failure Output
status: implemented
requirements: .lattice/requirements/features/assert-002-field-level-failure-output.md
---

# ASSERT-002 — Field-Level Failure Output

## Summary

Replace the raw jsondiffpatch delta string in assertion failure messages with per-field lines showing field path, expected value, and actual value. Zero new dependencies — custom formatter over the existing delta `JToken`.

## Constraints

- Must fit existing codebase structure — no clean architecture or DDD layers imposed
- Multi-target: net9.0, net10.0
- Newtonsoft.Json throughout (no System.Text.Json)
- No new NuGet dependency permitted
- JsonDiffPatch.Net remains the diff engine — only the output stage changes
- `DeltaFormatter` is a pure function — no side effects, no I/O

## Design: Level 1 — Capabilities

1. Parse a jsondiffpatch delta `JToken` into a structured list of field-level changes — modification, addition (field in actual not in expected), deletion (field in expected not in actual)
2. Produce a human-readable failure string — one block per changed field: path on one line, `expected:` and `actual:` indented below
3. Render nested field paths in dot notation: `user.address.city`
4. Render array element changes with index notation: `items[1].id`
5. Label additions as `expected: <absent>` and deletions as `actual: <missing>`
6. Include a header line `Response body mismatch:` before the field list
7. Replace `diff?.ToString().Should().BeNullOrWhiteSpace()` with a throw carrying the formatted message when diff is non-null
8. Null delta → no output, no failure — existing passing behaviour untouched

## Design: Level 2 — Components

| Component | Change | Namespace | File |
|---|---|---|---|
| `DeltaFormatter` | New — static utility | `ConfIT.Util` | `src/ConfIT/Util/DeltaFormatter.cs` |
| `ResultMatcher` | Modified — swap final assertion | `ConfIT.Util` | `src/ConfIT/Util/ResultMatcher.cs` |

```
ResultMatcher  ──uses──▶  DeltaFormatter
                               │
                               └──reads──▶  JToken (Newtonsoft, global using)
```

No other files touched. BaseTest, Matcher DTO, SuiteConfig, and all contracts unchanged.

## Design: Level 3 — Interactions

**Happy path (no diff):**
```
BaseTest.Execute → Verify() → MatchResponseBody()
  SemanticMatcher.Apply()        // removes semantic fields
  ApplyMatcher()                 // removes pattern + ignore from actual
  ApplyIgnoreMatcher()           // removes ignore from expected
  JsonDiffPatch().Diff()         // → null
  // null → no throw → test passes
```

**Failure path (mismatch):**
```
BaseTest.Execute → Verify() → MatchResponseBody()
  SemanticMatcher.Apply()
  ApplyMatcher()
  ApplyIgnoreMatcher()
  JsonDiffPatch().Diff()         // → JToken delta (non-null)
  DeltaFormatter.Format(delta)   // → formatted string
  throw AssertionFailedException(formatted)
```

**DeltaFormatter.Walk internal logic:**
```
Walk(node JObject, path, lines)
  for each JProperty p:
    value is JArray:
      count == 2  → modification:  path.Name | actual=arr[0] | expected=arr[1]
      count == 1  → field in expected, not in actual → [expectedVal]: expected=arr[0] | actual=<missing>
      count == 3  → field in actual, not in expected → [actualVal,0,0]: actual=arr[0] | expected=<absent>
    value is JObject:
      has "_t"="a" → array context: recurse children with path.Name[N]
      otherwise    → nested object:  recurse with path.Name.
```

Note: `Diff(left=actual, right=expected)` — 1-element means a value was added in `right` (expected); 3-element means it was deleted from `right` (was in actual). The design doc had these labels transposed.

DeltaFormatter is only called when diff is non-null — no null guard needed internally.

## Design: Level 4 — Contracts

**New: `DeltaFormatter`**

```csharp
namespace ConfIT.Util;

public static class DeltaFormatter
{
    public static string Format(JToken delta);
    private static void Walk(JToken node, string path, List<string> lines);
}
```

**`Format` output contract:**
```
Response body mismatch:

user.name
  expected: "bob"
  actual:   "alice"

items[1].id
  expected: 2
  actual:   <missing>
```
- Header `Response body mismatch:` always first
- Blank line after header, blank line between field blocks
- Path on its own line, values indented two spaces
- String values quoted; primitives unquoted; sentinels angle-bracketed and unquoted: `<absent>`, `<missing>`

**Modified: `ResultMatcher.MatchResponseBody` — final two lines**

```csharp
// BEFORE
var diff = new JsonDiffPatch().Diff(response, expectedResponse);
diff?.ToString().Should().BeNullOrWhiteSpace();

// AFTER
var diff = new JsonDiffPatch().Diff(response, expectedResponse);
if (diff is not null)
    Execute.Assertion.FailWith(DeltaFormatter.Format(diff));
```

`Execute.Assertion` is `FluentAssertions.Execution` — already a direct dependency. Goes through FA's assertion infrastructure correctly; test runner receives a clean message with no boilerplate wrapping.

## Design Summary

**Components and layer assignments:**
- `DeltaFormatter` — new static utility, `ConfIT.Util`, `src/ConfIT/Util/DeltaFormatter.cs`
- `ResultMatcher` — existing static utility, `ConfIT.Util`, modified final assertion only

**Key contracts:**
- `DeltaFormatter.Format(JToken delta) → string` — pure function, no side effects
- `Execute.Assertion.FailWith(formatted)` replaces `diff?.ToString().Should().BeNullOrWhiteSpace()`
- Output: header + blank line + one block per mismatching field (path, indented expected/actual)

**Architectural constraints:**
- No new NuGet dependency
- No changes outside `ConfIT.Util`
- `DeltaFormatter` has zero outward dependencies beyond `Newtonsoft.Json.Linq` (global using)

**Open questions resolved:**
- Header line: yes — `Response body mismatch:`
- Throw mechanism: `Execute.Assertion.FailWith` (FA-idiomatic, clean output, no boilerplate)
- Passing fields: not shown (noise reduction)
- Output destination: exception message only (full bodies already logged by `BaseTest.Log`)

**Design status: Approved — ready for implementation**

## Decisions Log

- **L1**: Header line `Response body mismatch:` included — resolves open question from requirements spec.
- **L4**: `Execute.Assertion.FailWith(formatted)` chosen over `throw new AssertionFailedException` and `DeltaFormatter.Format(diff).Should().BeNullOrWhiteSpace()`. Reason: idiomatic FA, clean message without boilerplate wrapping, goes through FA's test-framework detection hook.
- **Impl**: `Execute.Assertion` was removed in FA 8.x. Replaced with `AssertionChain.GetOrCreate().FailWith(formatted)` — equivalent behaviour, correct FA 8.x API. `using FluentAssertions.Execution` in `ResultMatcher.cs`.
- **Impl**: Level 3 context doc had count==1/count==3 sentinel mapping transposed. Implementation follows `Diff(actual, expected)` semantics correctly: count==1 → field in expected, absent in actual (`actual: <missing>`); count==3 → field in actual, absent in expected (`expected: <absent>`). Context doc corrected.
- **L4**: Design approved at Level 4. Blueprint complete — ready for implementation.

## Open Questions

*(none)*
