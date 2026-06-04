---
feature: FLOW-001 Variable Extraction + Injection
status: implemented — 86/86 tests passing
requirements: doc/features/flow-001.md
---

# FLOW-001 — Variable Extraction + Injection

## Summary

Declarative variable extraction from HTTP responses and injection into subsequent tests. Eliminates ~80% of `ITestProcessor` usage for multi-step test flows.

## Design: Level 1 — Capabilities

1. Declare extraction via `extract` block on any response (name → JSONPath)
2. Auto-inject `{{varName}}` into subsequent test path, body, headers, params, mock bodies, expected assertions
3. Full-prefix `{{TestName.varName}}` for disambiguation when short names collide
4. Type-aware injection — typed in JSON body, always string in path/headers/params
5. Fail fast on undefined variable reference with actionable error
6. Fail fast on ambiguous short name with actionable error
7. Extraction skipped when a test fails — store unchanged
8. `${ENV_VAR}` (static, env) vs `{{varName}}` (dynamic, runtime) — distinct namespaces, mixing is a load-time error
9. Format parity — identical behaviour in `.json` and `.yaml` files

## Design: Level 2 — Components

### New — `ConfIT.Variable` namespace (`src/ConfIT/Variable/`)

| Class | Responsibility |
|-------|---------------|
| `VariableStore` | Static singleton. Suite-wide `ConcurrentDictionary<testName, ConcurrentDictionary<varName, JToken>>`. Write-once per test bucket. Short-name scan with ambiguity detection. Full-prefix direct lookup. |
| `VariableExtractor` | Static utility. Reads `extract` spec from `HttpTestResponse`, builds unified response object `{ body, headers, statusCode }`, runs JSONPath via `JToken.SelectToken()`, writes to `VariableStore`. |
| `VariableInjector` | Static utility. Walks `TestCase` before execution, resolves `{{varName}}` and `${ENV_VAR}`, returns resolved copy. |

### Modified

| Class | Change |
|-------|--------|
| `HttpTestResponse` | Add `Extract: Dictionary<string, string>?` (varName → JSONPath) |
| `BaseTest` | Wire `VariableInjector` before mock setup; wire `VariableExtractor` after `Verify` passes |

### Key decisions
- **Store is suite-wide static** (not per-file): enables cross-file `{{TestName.varName}}` access
- **Write-once per test bucket**: same test extracting same variable name twice → suite error
- **Same name across different tests**: legal write; forces full-prefix at resolution time
- **Thread-safe**: `ConcurrentDictionary` throughout

## Design: Level 3 — Interactions

```
Suite starts → VariableStore.Instance created (static, one per process lifetime)

BaseTest.Execute(testName, testCase)
  1. INJECT   resolvedCase = VariableInjector.Inject(testCase, VariableStore.Instance)
              {{varName}} / {{Test.varName}} / ${ENV_VAR} all resolved on deep copy
              Undefined or ambiguous ref → exception, test fails, extraction skipped

  2. EXECUTE  HttpMockServer.Initialize(resolvedCase.Mock)
              testProcessor?.Before(resolvedCase.Api)
              response = HttpClient.Execute(resolvedCase.Api)
              actualBody = JToken.Parse(...)
              testProcessor?.After(resolvedCase.Api, actualBody)

  3. VERIFY   Verify(response, actualBody, resolvedCase.Api)
              throws on mismatch → extraction naturally skipped

  4. EXTRACT  VariableExtractor.Extract(testName, response, actualBody,
                resolvedCase.Api.Response.Extract, VariableStore.Instance)
              Builds { body, headers, statusCode } unified JToken
              SelectToken(path) per extract entry → Set(testName, varName, value)
              Collision on same testName+varName → VariableCollisionException

  5. SAVE     SaveApiResponse(...) — unchanged
```

Edge cases:
- Skipped test → injector and extractor never run; downstream refs → UndefinedVariableException
- Null/empty Extract → VariableExtractor is no-op

## Design: Level 4 — Contracts

### VariableStore (`ConfIT.Variable`)
```csharp
public sealed class VariableStore
{
    public static readonly VariableStore Instance = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JToken>> _store = new();

    public void Set(string testName, string varName, JToken value);   // write-once; throws VariableCollisionException
    public JToken Resolve(string varName);                             // short-name; throws Ambiguous or Undefined
    public JToken Resolve(string testName, string varName);            // full-prefix; throws Undefined
}
```

### VariableExtractor (`ConfIT.Variable`)
```csharp
public static class VariableExtractor
{
    public static void Extract(string testName, HttpResponseMessage response,
        JToken actualBody, Dictionary<string, string>? extractSpec, VariableStore store);
}
```
Builds `{ body, headers, statusCode }` unified JToken, runs `SelectToken()` per entry.

### VariableInjector (`ConfIT.Variable`)
```csharp
public static class VariableInjector
{
    public static TestCase Inject(TestCase testCase, VariableStore store);
}
```
Deep copy. `{{varName}}` → short-name lookup. `{{Test.varName}}` → full-prefix. `${ENV_VAR}` → environment.
Type-preserved when entire JToken value is `{{varName}}`; string-ified when embedded.

### HttpTestResponse (modified)
```csharp
public Dictionary<string, string>? Extract { get; set; }  // added
```

### Exception types (`ConfIT.Variable`)
- `VariableCollisionException` — same testName+varName set twice
- `AmbiguousVariableException` — short name matches multiple test buckets
- `UndefinedVariableException` — no test has extracted the referenced name
- `VariableConfigurationException` — same name used with both `{{}}` and `${}` syntax

### BaseTest wiring
```csharp
var resolvedCase = VariableInjector.Inject(testCase, VariableStore.Instance);  // before mock setup
// ... execution uses resolvedCase throughout ...
Verify(...);
VariableExtractor.Extract(testName, response, actualBody,                       // after Verify passes
    resolvedCase.Api.Response.Extract, VariableStore.Instance);
```

## Design Summary

**Status: Approved — ready for implementation**

### Components
| Class | Type | Namespace |
|-------|------|-----------|
| `VariableStore` | New | `ConfIT.Variable` |
| `VariableExtractor` | New static utility | `ConfIT.Variable` |
| `VariableInjector` | New static utility | `ConfIT.Variable` |
| `HttpTestResponse` | Modified — add `Extract` | `ConfIT.Server.Dto` |
| `BaseTest` | Modified — wire injection + extraction | `ConfIT` |

### Key architectural constraints
- `VariableStore` is a static singleton — suite-wide, one per process lifetime
- Thread-safe via `ConcurrentDictionary` throughout
- Write-once per test bucket — collision is a hard error, not a silent overwrite
- `VariableInjector` always returns a deep copy — original `TestCase` never mutated
- Extraction only runs after `Verify` passes — naturally skipped on test failure

### Files to create
- `src/ConfIT/Variable/VariableStore.cs`
- `src/ConfIT/Variable/VariableExtractor.cs`
- `src/ConfIT/Variable/VariableInjector.cs`
- `src/ConfIT/Variable/VariableCollisionException.cs`
- `src/ConfIT/Variable/AmbiguousVariableException.cs`
- `src/ConfIT/Variable/UndefinedVariableException.cs`
- `src/ConfIT/Variable/VariableConfigurationException.cs`

### Files to modify
- `src/ConfIT/Server/Dto/HttpTestResponse.cs` — add `Extract` property
- `src/ConfIT/BaseTest.cs` — wire `VariableInjector` and `VariableExtractor`

## Decisions Log

| Decision | Rationale |
|----------|-----------|
| `VariableStore` is suite-wide static | Enables cross-file `{{TestName.varName}}` access |
| Write-once per test bucket | Prevents silent overwrites; collision = hard suite error |
| Same short name across different tests is legal | Resolved at read time via ambiguity detection |
| New `ConfIT.Variable` namespace | Cohesive feature group; keeps `Util` from becoming a grab-bag |
| `VariableInjector` returns deep copy | Original `TestCase` untouched; safe for retry/logging |
| `${ENV_VAR}` and `{{varName}}` are distinct namespaces | Prevents mixing static config with dynamic test data |
| v2 static validation utility | Pre-flight CLI scan for collisions and undefined refs; deferred |

## Implementation Notes

**Files created:**
- `src/ConfIT/Variable/VariableStore.cs`
- `src/ConfIT/Variable/VariableExtractor.cs`
- `src/ConfIT/Variable/VariableInjector.cs`
- `src/ConfIT/Variable/VariableCollisionException.cs`
- `src/ConfIT/Variable/AmbiguousVariableException.cs`
- `src/ConfIT/Variable/UndefinedVariableException.cs`
- `src/ConfIT/Variable/VariableConfigurationException.cs`
- `test/ConfIT.UnitTest/Variable/VariableStoreTests.cs`
- `test/ConfIT.UnitTest/Variable/VariableExtractorTests.cs`
- `test/ConfIT.UnitTest/Variable/VariableInjectorTests.cs`

**Files modified:**
- `src/ConfIT/Server/Dto/HttpTestResponse.cs` — added `Extract` property
- `src/ConfIT/BaseTest.cs` — wired `VariableInjector` + `VariableExtractor`
- `test/ConfIT.UnitTest/GlobalUsings.cs` — added `ConfIT.Variable`

**Discovery during implementation:** .NET normalizes HTTP response header names (e.g. `X-Request-Id` → `X-Request-ID`). All header keys are stored lowercase in the unified response JObject to give users predictable, case-insensitive access. Header extraction paths must use lowercase: `$.headers['x-request-id']`.

**Deep clone approach:** `VariableInjector.Inject` uses JSON round-trip (`JsonConvert.SerializeObject` / `DeserializeObject`) to clone the `TestCase` before injection — avoids mutating the original, works with already-initialized `Body` JTokens.

## Open Questions

<!-- None outstanding -->

## Constraints

- Must fit existing codebase structure — no clean architecture or DDD layers imposed
- Multi-target: net9.0, net10.0
- Newtonsoft.Json throughout (no System.Text.Json)
- JSON and YAML test files supported identically
