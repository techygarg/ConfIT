---
feature: Field-Level Failure Output
epic: Remove the Capability Ceiling
status: draft
priority: P1
depends_on: []
personas:
  - test-engineer
  - debugging-developer
source_docs: []
---

# Field-Level Failure Output

## Problem Statement

When a ConfIT body assertion fails, the test runner surfaces the raw jsondiffpatch delta format:

```
Expected string to be <null> or white space, but found
"{"name":["alice","bob"],"role":["admin",0,0],"age":[30,31]}"
```

Developers must manually decode the `["actual","expected"]` array encoding to identify which fields differ and by how much. With three or more mismatching fields this becomes significant friction — especially for someone who did not write the original test.

## User / Personas

**Test engineer** — writes and maintains ConfIT test suites. Needs to understand failures at a glance without manual delta decoding.

**Debugging developer** — investigates a failing test, often not its original author. Needs immediate clarity on what changed in the response without reading two full JSON bodies and deriving the diff mentally.

## Scope

**In scope:**
- Format the body assertion failure message as per-field lines: field path, expected value, actual value
- Report all mismatching fields at once (not just the first)
- Handle all three jsondiffpatch delta change types: modification, addition (unexpected field in actual), deletion (missing field in actual)
- Nested field paths in dot notation: `user.address.city`
- Array element paths using index notation: `items[1].id`
- Formatted output carried in the assertion exception message — visible directly in the test runner failure view

**Out of scope:**
- Header comparison formatting (separate concern)
- Status code failure formatting (already readable via FluentAssertions)
- Showing passing fields alongside failing ones (noise)
- Configurable output format options
- Continue-after-failure / soft assertions (that is ASSERT-003)
- Introducing a new NuGet dependency

## Boundary Conditions

- Fields removed by the `ignore`, `pattern`, or `semantic` matcher pipeline are never seen by the formatter — those pipelines run before the diff
- A null diff (all fields match) produces no output and no failure
- The formatter is a pure function over the jsondiffpatch `JToken` delta — no side effects, no I/O

## Assumptions

- JsonDiffPatch.Net remains the diff engine; only the output formatting stage changes
- The jsondiffpatch delta encoding is stable: `[a, b]` = modification, `[v]` = addition, `[v, 0, 0]` = deletion, `_t: "a"` = array diff marker
- Pre-assertion full-body logging via `BaseTest.Log()` already provides raw context; the formatted diff in the exception message is sufficient without an additional pre-assertion log call

## Scenarios

### Scenario 1: Single field mismatch produces readable failure line

A test where one response field differs from expected. The failure message names the field and shows expected and actual values.

**Acceptance Criteria:**
- Given actual response `{"name":"alice"}` and expected `{"name":"bob"}`
- When the assertion runs
- Then the test fails with a message containing:
  ```
  name
    expected: "bob"
    actual:   "alice"
  ```
- And no other field lines appear (only failing fields are shown)

### Scenario 2: Multiple field mismatches all reported at once

All differing fields appear in a single failure — no fix-and-rerun cycle to discover subsequent mismatches.

**Acceptance Criteria:**
- Given actual `{"name":"alice","age":30}` and expected `{"name":"bob","age":31}`
- When the assertion runs
- Then the failure message contains one entry per mismatching field:
  ```
  name
    expected: "bob"
    actual:   "alice"
  age
    expected: 31
    actual:   30
  ```

### Scenario 3: Nested field mismatch uses dot-notation path

**Acceptance Criteria:**
- Given actual `{"user":{"address":{"city":"London"}}}` and expected `{"user":{"address":{"city":"Paris"}}}`
- When the assertion runs
- Then the failure message contains:
  ```
  user.address.city
    expected: "Paris"
    actual:   "London"
  ```

### Scenario 4: Field present in actual but absent in expected

A field the test did not expect to exist appears in the response.

**Acceptance Criteria:**
- Given actual `{"id":"123","debug":"trace"}` and expected `{"id":"123"}`
- When the assertion runs
- Then the failure message contains:
  ```
  debug
    expected: <absent>
    actual:   "trace"
  ```

### Scenario 5: Field expected but missing from actual

An expected field is not returned by the service.

**Acceptance Criteria:**
- Given actual `{"id":"123"}` and expected `{"id":"123","name":"alice"}`
- When the assertion runs
- Then the failure message contains:
  ```
  name
    expected: "alice"
    actual:   <missing>
  ```

### Scenario 6: Array element mismatch uses index path

**Acceptance Criteria:**
- Given actual `{"items":[{"id":1},{"id":99}]}` and expected `{"items":[{"id":1},{"id":2}]}`
- When the assertion runs
- Then the failure message contains:
  ```
  items[1].id
    expected: 2
    actual:   99
  ```

### Scenario 7: All fields match — passes cleanly

**Acceptance Criteria:**
- Given actual equals expected after the matcher pipeline
- When the assertion runs
- Then the test passes
- And no diff output appears anywhere

### Scenario 8: Matcher pipeline fields are not seen by the formatter

Fields removed by `ignore`, `pattern`, or `semantic` matchers are not surfaced in diff output.

**Acceptance Criteria:**
- Given actual `{"id":"abc-123","name":"alice"}` and expected `{"id":"abc-123","name":"alice"}` with `pattern: {"id": "^[a-z-]+$"}`
- When the assertion runs
- Then `id` is removed by the pattern pipeline before diffing
- And the test passes with no failure output

*(Scenarios ordered chronologically — natural implementation sequence.)*

## Implementation Notes

1. `DeltaFormatter` static class (`src/ConfIT/Util/DeltaFormatter.cs`) — `Format(JToken delta) → string`. Walks the delta JToken recursively, tracks the current dot-notation path, emits one block per leaf-level change. Array diffs detected via `_t: "a"` marker; element paths rendered as `field[n]`.
2. Replace the single assertion in `ResultMatcher.MatchResponseBody` — swap `diff?.ToString().Should().BeNullOrWhiteSpace()` with: when diff is non-null, call `DeltaFormatter.Format(diff)` and throw an assertion failure carrying the formatted string as the message.
3. Unit tests for `DeltaFormatter` (`test/ConfIT.UnitTest/Util/DeltaFormatterTests.cs`) — one test per scenario covering all change types and path formats.
4. Extend `ResultMatcherTests` with cases asserting the exception message contains expected field-level lines rather than raw delta JSON.

## Open Questions

- [ ] Should the failure message include a header line (e.g. `Response body mismatch — N field(s) differ:`) for readability, or start directly with the field list?

## Links

- Design: *(updated when design-blueprint creates a context anchor doc for this feature)*
- Epic index: [index.md](../index.md)
