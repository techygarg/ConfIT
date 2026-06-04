---
feature: ASSERT-001 Semantic Matcher Library
status: implemented
requirements: .lattice/requirements/features/semantic-matcher-library.md
---

# ASSERT-001 — Semantic Matcher Library

## Summary

Named, type-aware matchers under `matcher.semantic` as an alternative to raw regex. Validates and removes fields before structural diff. Extensible: built-in set of 12 matchers; consumers can register additional matchers via `SuiteConfig`.

## Constraints

- Must fit existing codebase structure — no clean architecture or DDD layers imposed
- Multi-target: net9.0, net10.0
- Newtonsoft.Json throughout (no System.Text.Json)
- Built-in matchers are immutable at runtime — consumer cannot override them, only extend

## Design: Level 1 — Capabilities

1. Declare `matcher.semantic` alongside `pattern` and `ignore` in the JSON test DSL — additive, non-breaking
2. Resolve 12 built-in named matchers: `isUuid`, `isIsoDate`, `isIsoDateTime`, `isEmail`, `isNull`, `isNotNull`, `isEmpty`, `isNotEmpty`, `greaterThan(n)`, `lessThan(n)`, `hasLength(n)`, `hasLength(min,max)`
3. Parse parametrized matcher syntax `matcherName(arg)` / `matcherName(min,max)` from DSL string value
4. Resolve custom matchers from `SuiteConfig.CustomMatchers` as fallback after built-ins
5. Fail fast on unknown matcher name before any HTTP call — `ArgumentException` naming unknown matcher and field path
6. Fail explicitly when a `semantic`-listed field is absent from actual response — not a silent pass
7. Produce field-specific failure messages on mismatch — names field path, matcher, and actual value
8. Remove validated fields from both actual and expected before structural diff — same mechanic as `pattern`
9. Support nested field paths via `__` separator — consistent with `pattern` / `ignore`
10. Registering a custom matcher name that collides with a built-in throws at `SuiteConfig` construction time

## Design: Level 2 — Components

| Component | Type | Namespace | File |
|---|---|---|---|
| `SemanticMatcherFunc` | New — delegate type | `ConfIT` | `src/ConfIT/SemanticMatcherFunc.cs` |
| `SemanticMatcher` | New — static utility | `ConfIT.Util` | `src/ConfIT/Util/SemanticMatcher.cs` |
| `Matcher` | Modified — add `Semantic` | `ConfIT.Server.Dto` | `src/ConfIT/Server/Dto/Matcher.cs` |
| `SuiteConfig` | Modified — add `CustomMatchers` | `ConfIT` | `src/ConfIT/SuiteConfig.cs` |
| `ResultMatcher` | Modified — add semantic step | `ConfIT.Util` | `src/ConfIT/Util/ResultMatcher.cs` |
| `BaseTest` | Modified — validate specs early + pass custom matchers | `ConfIT` | `src/ConfIT/BaseTest.cs` |

**`SemanticMatcherFunc` delegate:**
```csharp
// Returns null on success; failure message string on failure
public delegate string? SemanticMatcherFunc(JToken value, string? parameter);
```

**`SemanticMatcher` responsibilities:**
- Private static readonly built-in registry: `Dictionary<string, SemanticMatcherFunc>`
- `ValidateSpecs(semantic, customMatchers)` — called before HTTP; throws `ArgumentException` on unknown name
- `Apply(actual, expected, semantic, customMatchers)` — called from `ResultMatcher`; validates, applies, removes fields
- `ParseSpec(string spec)` — splits `"greaterThan(0)"` into `("greaterThan", "0")`
- Individual private validator methods for each built-in
- Resolution order: built-ins first, then `customMatchers`

**`SuiteConfig` addition:**
```csharp
public Dictionary<string, SemanticMatcherFunc> CustomMatchers { get; set; } = new();
```

**`ResultMatcher.MatchResponseBody` change:**
```csharp
// Optional parameter — fully backward compatible
public static void MatchResponseBody(JToken actual, JToken expected, Matcher matcher,
    IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers = null)
```

## Design: Level 3 — Interactions

```
BaseTest.Execute(testName, testCase)
  ├─ ShouldSkipTheTest → return if skip
  ├─ resolvedCase = VariableInjector.Inject(testCase, VariableStore.Instance)
  │
  ├─ SemanticMatcher.ValidateSpecs(                   ← NEW: before HTTP
  │      resolvedCase.Api.Response.Matcher?.Semantic,
  │      Config.CustomMatchers)
  │    • ParseSpec each spec → (name, param)
  │    • built-ins[name] || customMatchers[name] — else ArgumentException
  │    • no-op if Semantic null/empty
  │
  ├─ HttpMockServer?.Initialize / testProcessor?.Before
  ├─ response = await HttpClient.Execute(resolvedCase.Api)   ← HTTP call
  ├─ testProcessor?.After
  │
  └─ Verify → MatchResponseBody(actual, expected, matcher, Config.CustomMatchers)
       ├─ actual = actualResponse.DeepClone()
       ├─ SemanticMatcher.Apply(actual, expected, matcher.Semantic, customMatchers)  ← NEW first
       │    for each (fieldPath, matcherSpec):
       │      1. ParseSpec → (name, param)
       │      2. Resolve field via __ path
       │      3. Absent → fail: "Field '{path}' listed in semantic was absent"
       │      4. fn = built-ins[name] ?? customMatchers[name]
       │      5. result = fn(fieldValue, param)
       │      6. result != null → throw: "Field '{path}': {result}"
       │      7. Remove field from actual; remove from expected if present
       ├─ ApplyPatternMatcher(actual, matcher.Pattern)
       ├─ ApplyIgnoreMatcher(actual, matcher.Ignore)
       ├─ expected = ApplyIgnoreMatcher(expected, matcher.Ignore)
       └─ JsonDiffPatch().Diff(actual, expected) → fail if non-empty
```

Edge cases:
- `Semantic` null/empty → `ValidateSpecs` and `Apply` are no-ops
- Custom name collides with built-in → built-in wins; custom is silently unreachable → `ArgumentException` at `ValidateSpecs`
- Field in both `semantic` and `ignore` → semantic runs first, removes field; ignore entry is a no-op
- Field in both `semantic` and `pattern` → semantic removes field first; pattern entry is a no-op

## Design: Level 4 — Contracts

### `SemanticMatcherFunc` — `src/ConfIT/SemanticMatcherFunc.cs`
```csharp
namespace ConfIT
{
    // Returns null on success; non-null string is the failure message
    public delegate string? SemanticMatcherFunc(JToken value, string? parameter);
}
```

### `SemanticMatcher` — `src/ConfIT/Util/SemanticMatcher.cs`
```csharp
namespace ConfIT.Util
{
    internal static class SemanticMatcher
    {
        private static readonly IReadOnlyDictionary<string, SemanticMatcherFunc> BuiltIns =
            new Dictionary<string, SemanticMatcherFunc>
            {
                ["isUuid"]        = (v, _) => ...,
                ["isIsoDate"]     = (v, _) => ...,
                ["isIsoDateTime"] = (v, _) => ...,
                ["isEmail"]       = (v, _) => ...,
                ["isNull"]        = (v, _) => ...,
                ["isNotNull"]     = (v, _) => ...,
                ["isEmpty"]       = (v, _) => ...,
                ["isNotEmpty"]    = (v, _) => ...,
                ["greaterThan"]   = (v, p) => ...,
                ["lessThan"]      = (v, p) => ...,
                ["hasLength"]     = (v, p) => ...,   // single n or range min,max — inclusive
            };

        // Before HTTP — throws ArgumentException on unknown or malformed spec
        public static void ValidateSpecs(
            Dictionary<string, string>? semantic,
            IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers);

        // Called from ResultMatcher — validates, applies, removes fields from both sides
        public static void Apply(
            JToken actual,
            JToken expected,
            Dictionary<string, string>? semantic,
            IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers);

        // "greaterThan(0)" → ("greaterThan", "0")
        // "isUuid"         → ("isUuid", null)
        // "hasLength(1,50)"→ ("hasLength", "1,50")
        // Malformed        → ArgumentException
        private static (string name, string? param) ParseSpec(string spec, string fieldPath);
    }
}
```

### `Matcher.cs` — addition
```csharp
public Dictionary<string, string>? Semantic { get; set; }
```

### `SuiteConfig.cs` — addition
```csharp
public Dictionary<string, SemanticMatcherFunc> CustomMatchers { get; set; } = new();
```

### `ResultMatcher.MatchResponseBody` — final shape
```csharp
public static void MatchResponseBody(
    JToken actualResponse,
    JToken expectedResponse,
    Matcher matcher,
    IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers = null)
{
    var actual = actualResponse.DeepClone();
    SemanticMatcher.Apply(actual, expectedResponse, matcher?.Semantic, customMatchers);
    actual = ApplyMatcher(actual, matcher);                          // pattern + ignore — unchanged
    expectedResponse = ApplyIgnoreMatcher(expectedResponse, matcher?.Ignore);
    var diff = new JsonDiffPatch().Diff(actual, expectedResponse);
    diff?.ToString().Should().BeNullOrWhiteSpace();
}
```

### `BaseTest` — two-line change
```csharp
// In Execute, before HttpMockServer.Initialize:
SemanticMatcher.ValidateSpecs(resolvedCase.Api.Response.Matcher?.Semantic, Config.CustomMatchers);

// In Verify:
MatchResponseBody(actualResponseBody, expectedResponseBodyJToken,
    testApi.Response.Matcher, Config.CustomMatchers);
```

## Design Summary

**Status: Approved — ready for implementation**

### Files to create
- `src/ConfIT/SemanticMatcherFunc.cs` — public delegate type
- `src/ConfIT/Util/SemanticMatcher.cs` — internal static matcher registry + application logic

### Files to modify
- `src/ConfIT/Server/Dto/Matcher.cs` — add `Semantic` property
- `src/ConfIT/SuiteConfig.cs` — add `CustomMatchers` property
- `src/ConfIT/Util/ResultMatcher.cs` — inline `SemanticMatcher.Apply` at top of `MatchResponseBody`; add optional `customMatchers` param
- `src/ConfIT/BaseTest.cs` — add `ValidateSpecs` call before HTTP; pass `Config.CustomMatchers` in `Verify`

### Key architectural constraints
- `SemanticMatcher` is `internal` — consumers interact only via `SuiteConfig.CustomMatchers` and `SemanticMatcherFunc`
- `ApplyMatcher` private method is unchanged — `SemanticMatcher.Apply` is inlined in `MatchResponseBody` where both `actual` and `expected` are in scope
- Built-ins are immutable — consumer cannot override them; collision at `ValidateSpecs` throws `ArgumentException`
- Resolution order: built-ins first, then `customMatchers`
- `MatchResponseBody` optional param is backward compatible — existing call sites without `customMatchers` continue to work
- `hasLength(min,max)` range is inclusive on both bounds

### Open questions resolved
- `hasLength(min,max)` bounds: **inclusive** on both ends
- `isIsoDateTime` timezone: **accepts both UTC (`Z`) and offset (`+05:30`) formats**
- Expected threading: **inline in `MatchResponseBody`** — `ApplyMatcher` unchanged

## Decisions Log

| Decision | Rationale |
|----------|-----------|
| Custom matchers via `SuiteConfig.CustomMatchers` dictionary | Avoids global mutable state; fits existing config bag pattern; no new interface needed |
| Built-ins are immutable — consumer can only extend, not override | Prevents accidental shadowing of standard behaviour |
| `SemanticMatcher` is `internal` | Implementation detail; public extension surface is `SemanticMatcherFunc` + `SuiteConfig.CustomMatchers` |
| No new namespace — fits `ConfIT.Util` | Feature is a matcher extension, not a new subsystem; `ConfIT.Variable` set the bar for when a namespace is warranted |
| `SemanticMatcher.Apply` inlined in `MatchResponseBody` | Both `actual` and `expected` are in scope there; `ApplyMatcher` stays unchanged |
| `ValidateSpecs` call in `BaseTest.Execute` before HTTP | Honours spec requirement: unknown matcher names fail before any HTTP call |
| `MatchResponseBody` optional `customMatchers` param | Fully backward compatible; existing call sites need no changes |
| `ParseSpec` takes field path as second arg | Enables precise `ArgumentException` messages naming both matcher and field |

## Implementation Notes

**Files created:**
- `src/ConfIT/SemanticMatcherFunc.cs`
- `src/ConfIT/Util/SemanticMatcher.cs`
- `test/ConfIT.UnitTest/Util/SemanticMatcherTests.cs`
- `example/User.IntegrationTests/TestCase/semanticMatchers.json`

**Files modified:**
- `src/ConfIT/Server/Dto/Matcher.cs` — added `Semantic` property
- `src/ConfIT/SuiteConfig.cs` — added `CustomMatchers` property
- `src/ConfIT/Util/ResultMatcher.cs` — inlined `SemanticMatcher.Apply`; added optional `customMatchers` param
- `src/ConfIT/BaseTest.cs` — added `using ConfIT.Util`; `ValidateSpecs` before HTTP; `CustomMatchers` in `Verify`

**Deviation from blueprint:** `SemanticMatcher` is `public`, not `internal`. Codebase has zero `internal` classes and no `InternalsVisibleTo` — making it internal would block unit testing without infrastructure changes.

**Discovery during implementation:** Newtonsoft.Json auto-parses ISO 8601 datetime strings to `JTokenType.Date` via default `DateParseHandling.DateTime`. The `isIsoDate` and `isIsoDateTime` validators handle both `JTokenType.String` and `JTokenType.Date` input. For `isIsoDate` on a `Date` token, validates `TimeOfDay == TimeSpan.Zero` to reject datetime strings that were parsed as Date objects.

**Test results:** 119/119 unit tests passing (net9.0 + net10.0). 6/6 component tests passing. No regressions.

## Open Questions

<!-- None outstanding -->
