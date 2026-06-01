---
feature: YAML Support
requirement_doc: .lattice/requirements/features/dsl-002-yaml-support.md
created: 2026-06-01
---

# YAML Support

> Format adapter that allows ConfIT test files to be written in YAML (.yaml/.yml) alongside existing JSON, with zero behavioural change to the test execution pipeline.

## Design: Level 2 — Components

| Component | Type | Layer | Responsibility |
|---|---|---|---|
| `YamlConverter` | New static class | `Util` | Single method — YAML string → `JObject`. Pure adapter: no knowledge of test DSL, matchers, or any ConfIT concept. |
| `TestReader` | Existing, extended | `Util` | Add `.yaml`/`.yml` to file discovery and route through `YamlConverter` before `JObject.Parse`. Public API unchanged. |

```
Consumer (xUnit test class)
         │  GetTestsForAFile / GetTestsForAFolder
         ▼
    ┌─────────────┐
    │  TestReader │   ← file extension switch added here
    └──────┬──────┘
           │
     ┌─────┴──────┐
     │             │
  .json          .yaml/.yml
     │             │
     ▼             ▼
 JObject.Parse  YamlConverter   ← new: YAML string → JObject
                    │
                    ▼
               JObject.Parse
                    │
     └─────────────┘
           │
           ▼
    [JToken pipeline]   ← ToTestCase(), Initialize(), matchers — all unchanged
```

## Design: Level 3 — Interactions

**Flow 1 — `GetTestsForAFile` (single file)**
```
TestReader reads file → checks extension
  .json       → JObject.Parse(content)
  .yaml/.yml  → YamlConverter.ToJObject(content)
               └─ YamlDotNet.Deserialize<object> → JsonConvert.SerializeObject → JObject.Parse
→ iterate JObject.Properties() → yield (name, JToken)
```

**Flow 2 — `GetTestsForAFolder`**
```
Directory.GetFiles filtered to {.json, .yaml, .yml}
→ for each file: same format-detection branch as Flow 1
→ yield (name, JToken) per property per file
```

**Flow 3 — Invalid YAML error path**
```
TestReader calls YamlConverter.ToJObject(bad content)
→ YamlDotNet throws YamlException (has line/column, no filename)
→ TestReader catches YamlException
→ TestReader throws InvalidDataException(
     $"YAML parse error in '{filePath}': {ex.Message}")
```

## Design: Level 4 — Contracts

**`YamlConverter` (new, `internal`)**
```csharp
namespace ConfIT.Util;

internal static class YamlConverter
{
    // Throws YamlException on invalid YAML. Anchors/aliases resolved before return.
    internal static JObject ToJObject(string yaml);
}
```

**`TestReader` (existing — signatures unchanged)**
```csharp
namespace ConfIT.Util;

public static class TestReader
{
    // Now also accepts .yaml / .yml filenames.
    // Throws InvalidDataException (wraps YamlException) on YAML syntax error — message includes filePath.
    public static IEnumerable<object[]> GetTestsForAFile(string testFolderName, string fileName);

    // Now discovers .yaml and .yml files alongside .json.
    // Throws InvalidDataException on any malformed YAML file discovered.
    public static IEnumerable<object[]> GetTestsForAFolder(string testFolderName);
}
```

**Extension detection (internal)**
```
IsYaml(path) → extension.Equals(".yaml", OrdinalIgnoreCase)
             || extension.Equals(".yml",  OrdinalIgnoreCase)
```

---

## Design Summary

**Components and layer assignments**
- `YamlConverter` — new, `internal static class`, `Util` layer (`src/ConfIT/Util/YamlConverter.cs`)
- `TestReader` — existing, `public static class`, `Util` layer — two methods extended, signatures unchanged

**Key contracts and interfaces**
- `YamlConverter.ToJObject(string yaml) → JObject` — single internal method, pure adapter
- `TestReader` public API: no signature changes; `.yaml`/`.yml` accepted transparently alongside `.json`
- `InvalidDataException` thrown (not `YamlException`) when a YAML file is malformed — filename and line/column in message

**Architectural constraints**
- Format conversion happens exclusively at the `TestReader` file-read boundary
- No downstream component (DTOs, matchers, extractors, `BaseTest`) may be aware of source format
- `YamlConverter` is `internal` — not part of the public library API

**Domain model decisions**
- Not applicable — this is a pure infrastructure/utility change with no domain concepts

**Open questions resolved during design**
- `YamlConverter` visibility → `internal` (Level 2)
- Invalid YAML error handling → `TestReader` wraps `YamlException` as `InvalidDataException` with filename (Level 3, Option B)

**Design status: Approved — ready for implementation**

---

## Decisions Log

<!-- Add new at bottom. Never remove. -->

| Date | Decision | Reasoning | Alternatives Considered |
|------|----------|-----------|------------------------|
| 2026-06-01 | `YamlConverter` visibility: `internal` | It is an implementation detail of `TestReader`; no consumer needs direct YAML parsing. Widening later is non-breaking; narrowing would be. | `public` — rejected: premature API surface, no current use case outside `TestReader` |
| 2026-06-01 | Invalid YAML: `TestReader` catches `YamlException` and re-throws as `InvalidDataException` with filename | `YamlDotNet` provides line/column but not filename; `TestReader` is the only scope where the filename is known. Requirement Scenario 6 AC requires the filename in the error message. | Option A (let `YamlException` propagate as-is) — rejected: fails Scenario 6 AC, no filename in message |
| 2026-06-01 | Design approved at Level 4. Blueprint complete — ready for implementation. | All four levels walked and approved. Contracts defined. No open questions remain. | — |
| 2026-06-01 | `Deserialize<object>` returns all scalars as strings by default in YamlDotNet 16.x | Without a type resolver, integers and booleans become strings in the object graph, producing wrong JTokenType after round-trip. Fixed with a private `ScalarTypeResolver` nested in `YamlConverter` that infers `bool`/`long`/`double` from plain (unquoted) scalars only. | Deserializing to a strongly-typed model — rejected: DTOs use JToken which can't be a YamlDotNet target |
| 2026-06-01 | YAML 1.1 merge keys (`<<:`) do not expand when deserializing to `object` | `<<` becomes a literal dictionary key rather than merging. YAML core aliases (`*anchor` replacing a whole node) work correctly. Example files use direct aliases; `<<:` merge keys are out of scope. | Adding a MergeKey deserializer — rejected: complexity for a feature not required by the DSL |
| 2026-06-01 | Example response for create-user tests must include `body: {}` alongside semantic matcher | When `body` is absent, `expected` is null. After semantic matcher removes `id` from actual, `Diff({}, null)` fires a root-level delta. `body: {}` makes `Diff({}, {})` return null after matcher removal. | Leaving `body` absent and skipping body diff — rejected: breaks contract for all tests without expected body |
| 2026-06-01 | YAML anchors must be defined inline within a test case, not at the top level | Top-level YAML keys are yielded as test cases by TestReader. A bare anchor key (e.g. `validation_ok: &anchor`) would be treated as a test case and fail. Anchors defined on nested nodes within the first test case are safe to alias from subsequent cases in the same file. | Top-level anchor definitions — rejected: polutes test list |

## Open Questions

<!-- When resolved, capture as decision above and remove from here. -->

*(none — all questions resolved during design)*

## Constraints

<!-- Non-negotiable once recorded. Add only when confirmed. -->

- Zero behavioural change for existing JSON users. The YAML path is purely additive.
- YAML-to-JObject conversion must happen at the `TestReader` file-read boundary only. No downstream component (DTOs, matchers, extractors) may require knowledge of the source format.
- No new public API surface beyond what `TestReader` already exposes.

## Key Files

- `src/ConfIT/Util/YamlConverter.cs` — new internal static class; YAML string → JObject adapter
- `src/ConfIT/Util/TestReader.cs` — extended: `.yaml`/`.yml` discovery and routing
- `src/ConfIT/GlobalUsings.cs` — `InternalsVisibleTo("ConfIT.UnitTest")` added
- `src/ConfIT/ConfIT.csproj` — YamlDotNet 16.3.0 added
- `test/ConfIT.UnitTest/Util/YamlConverterTests.cs` — 7 unit tests for YamlConverter
- `test/ConfIT.UnitTest/Util/TestReaderTests.cs` — 6 new YAML tests added
- `example/User.ComponentTests/TestCase/yaml-support.yaml` — component test YAML example
- `example/User.IntegrationTests/TestCase/yaml-support.yaml` — integration test YAML example
