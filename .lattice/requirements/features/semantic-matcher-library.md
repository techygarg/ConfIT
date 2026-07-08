---
feature: Semantic Matcher Library
epic: Remove the Capability Ceiling
status: draft
priority: P0
depends_on: []
personas:
  - Backend engineer
  - QA engineer
source_docs:
  - .lattice/index.md
---

# Semantic Matcher Library

## Problem Statement

ConfIT's `matcher.pattern` requires raw regex strings for every non-exact assertion. This creates three gaps:

1. **Verbosity**: common format checks (UUID, ISO date, email) need long, error-prone regexes that obscure test intent.
2. **Type-blind ceiling**: numeric comparisons (`greaterThan`) and size checks (`hasLength`) cannot be expressed as regex at all — forcing authors to either hardcode exact values (brittle) or use `ignore` (no assertion at all).
3. **Opaque failures**: a pattern mismatch leaves the field in the actual response, producing a generic JSON diff error rather than a readable "expected `id` to be a UUID, got `foobar`" message.

## User / Personas

- **Backend engineer** — writes component and integration tests against a JSON REST API; authors the JSON test definition files; wants concise, intent-revealing assertions.
- **QA engineer** — maintains and scales ConfIT test suites; reads test files to understand coverage; readable assertions reduce maintenance overhead.

## Scope

**In scope:**
- New `semantic` key under `matcher` (alongside existing `pattern` and `ignore`)
- 12 named matchers: `isUuid`, `isIsoDate`, `isIsoDateTime`, `isEmail`, `isNull`, `isNotNull`, `isEmpty`, `isNotEmpty`, `greaterThan(n)`, `lessThan(n)`, `hasLength(n)`, `hasLength(min,max)`
- Nested field paths using `__` separator (consistent with existing `pattern` / `ignore` behaviour)
- Clear, field-specific failure messages naming the matcher, field path, and actual value
- Example test cases in `example/User.IntegrationTests/TestCase/`

**Out of scope:**
- Composable matchers (`and:`, `or:` combinators)
- Array element targeting (apply matcher to every element in an array)
- Custom/user-defined matcher registration (deferred to `TOOL-005`)
- Semantic matchers on mock response bodies — applies to API responses only
- `ASSERT-002` (field-level failure output) — adjacent but separate feature
- `ASSERT-004` (JSON Schema validation) — structural schema, not field-level semantics

## Boundary Conditions

- Matcher names are case-sensitive (`isUuid` is valid; `isuuid` is not)
- An unrecognized matcher name throws `ArgumentException` before any HTTP call, naming the unknown matcher and the field path
- `greaterThan(n)` / `lessThan(n)` require the actual field value to be a JSON number; a non-numeric field fails with a type-mismatch message
- `hasLength` applies to strings (character count), arrays (element count), and objects (property count); fails with a type error for JSON numbers and booleans
- `isEmpty` / `isNotEmpty` apply to strings (length == 0), arrays (no elements), objects (no properties); a null field fails — use `isNull` instead
- A field listed under `semantic` that is absent from the actual response fails the assertion explicitly; it does not silently pass
- Semantic matchers remove the validated field from both actual and expected before the structural diff — consistent with `pattern` mechanic

## Assumptions

- The `semantic` key is optional; omitting it leaves existing `pattern` and `ignore` behaviour unchanged
- Parametrized matcher syntax: `matcherName(arg)` and `matcherName(min,max)` — no spaces inside parentheses
- Test file format remains JSON; YAML support (`DSL-002`) is a separate concern

## Scenarios

### Scenario 1: Validate a field using a format-named matcher
An author asserts that a response field matches a well-known format without writing a regex.

**Acceptance Criteria:**
- Given a response contains `"id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"`, when the test has `"semantic": { "id": "isUuid" }`, then the test passes and `id` is excluded from the structural diff
- Given a response contains `"date": "2026-05-31"`, when `"semantic": { "date": "isIsoDate" }`, then the test passes
- Given a response contains `"createdAt": "2026-05-31T10:30:00Z"`, when `"semantic": { "createdAt": "isIsoDateTime" }`, then the test passes
- Given a response contains `"contact": "user@example.com"`, when `"semantic": { "contact": "isEmail" }`, then the test passes

### Scenario 2: Produce a clear failure message when a format check fails
A test author can immediately identify which field failed and why — not from a JSON diff.

**Acceptance Criteria:**
- Given a response contains `"id": "not-a-uuid"`, when `"semantic": { "id": "isUuid" }`, then the test fails with a message naming the field (`id`), the matcher (`isUuid`), and the actual value (`not-a-uuid`)
- The failure does not surface as a generic JSON diff error

### Scenario 3: Validate null and non-null constraints

**Acceptance Criteria:**
- Given a response contains `"deletedAt": null`, when `"semantic": { "deletedAt": "isNull" }`, then the test passes
- Given a response contains `"deletedAt": "2026-05-31"`, when `"semantic": { "deletedAt": "isNull" }`, then the test fails with a message naming the field and actual value
- Given a response contains `"id": 1`, when `"semantic": { "id": "isNotNull" }`, then the test passes

### Scenario 4: Validate emptiness constraints

**Acceptance Criteria:**
- Given a response contains `"items": []`, when `"semantic": { "items": "isEmpty" }`, then the test passes
- Given a response contains `"name": ""`, when `"semantic": { "name": "isEmpty" }`, then the test passes
- Given a response contains `"meta": {}`, when `"semantic": { "meta": "isEmpty" }`, then the test passes
- Given a response contains `"items": [1, 2]`, when `"semantic": { "items": "isNotEmpty" }`, then the test passes

### Scenario 5: Validate numeric ordering constraints

**Acceptance Criteria:**
- Given a response contains `"count": 5`, when `"semantic": { "count": "greaterThan(0)" }`, then the test passes
- Given a response contains `"count": 0`, when `"semantic": { "count": "greaterThan(0)" }`, then the test fails with a message naming the field, matcher, and actual value
- Given a response contains `"age": 17`, when `"semantic": { "age": "lessThan(18)" }`, then the test passes
- Given a response contains `"score": "high"`, when `"semantic": { "score": "greaterThan(0)" }`, then the test fails with a type-mismatch message indicating the field is not numeric

### Scenario 6: Validate size constraints

**Acceptance Criteria:**
- Given a response contains `"zip": "12345"`, when `"semantic": { "zip": "hasLength(5)" }`, then the test passes
- Given a response contains `"name": "Al"`, when `"semantic": { "name": "hasLength(1,50)" }`, then the test passes (within inclusive range)
- Given a response contains `"tags": ["a", "b", "c"]`, when `"semantic": { "tags": "hasLength(3)" }`, then the test passes
- Given a response contains `"zip": "1234"`, when `"semantic": { "zip": "hasLength(5)" }`, then the test fails with a message stating expected length 5, actual 4

### Scenario 7: Apply a semantic matcher to a nested field

**Acceptance Criteria:**
- Given a response contains `{ "user": { "profile": { "id": "some-uuid" } } }`, when `"semantic": { "user__profile__id": "isUuid" }`, then the matcher is applied to the nested field using the `__` path separator — consistent with `pattern` and `ignore` behaviour
- A `__` path that does not resolve to a field in the actual response fails explicitly (see Scenario 10)

### Scenario 8: Combine semantic, pattern, and ignore in the same test

**Acceptance Criteria:**
- Given a test defines all three keys under `matcher`, then: `ignore` fields are removed before diff, `pattern` fields are validated by regex and removed, `semantic` fields are validated by named matcher and removed — no interference between the three modes
- The structural diff only covers fields not claimed by any matcher type

### Scenario 9: Reject an unrecognized matcher name at test time

**Acceptance Criteria:**
- Given a test defines `"semantic": { "id": "isWeird" }`, when the test starts, then an `ArgumentException` is thrown before any HTTP call, with a message naming the unrecognized matcher (`isWeird`) and the field path (`id`)

### Scenario 10: Fail explicitly when a semantic field is absent from the response

**Acceptance Criteria:**
- Given a test defines `"semantic": { "id": "isUuid" }`, when the actual response does not contain `id` at the specified path, then the test fails with a message indicating the expected field was absent — it does not silently pass

*(Scenarios ordered chronologically — natural implementation sequence.)*

## Implementation Notes

1. Add `Dictionary<string, string> Semantic` to `Matcher.cs` — DSL entry point, no breaking change
2. New semantic matcher registry: maps name strings to validation delegates; parses `matcherName(args)` syntax; throws `ArgumentException` for unknown names at lookup time
3. Add `ApplySemanticMatcher` to `ResultMatcher.cs`, called after `ApplyPatternMatcher`; validates unknown names first (fail fast), then resolves paths, asserts field presence, validates value, removes field from both sides
4. Unit tests: each matcher type (pass + fail), nested `__` paths, type-mismatch errors, unknown matcher name, absent field, mixed `semantic` + `pattern` + `ignore`
5. Example test cases in `example/User.IntegrationTests/TestCase/` covering at least one format matcher, one numeric matcher, and one size matcher

## Open Questions

- [ ] Should `hasLength(min,max)` be inclusive on both bounds? Recommendation: yes (e.g., `hasLength(1,50)` passes for lengths 1 through 50). Confirm before implementation.
- [ ] Should `isIsoDateTime` accept both UTC (`Z` suffix) and offset formats (`+05:30`)? Recommendation: yes — accept any ISO 8601 datetime with or without timezone.

## Links

- Design: [assert-001-semantic-matcher-library.md](../../context/assert-001-semantic-matcher-library.md)
- Epic index: [remove-the-capability-ceiling.md](../epics/remove-the-capability-ceiling.md)
