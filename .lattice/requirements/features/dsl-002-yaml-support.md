---
feature: YAML Support
epic: Reduce Team Friction
status: draft
priority: P1
depends_on: []
personas:
  - api-test-author
  - platform-infra-engineer
source_docs: []
---

# YAML Support

## Problem Statement

ConfIT test files are JSON-only. JSON has no comment syntax, no reuse mechanism, and unforgiving syntax (mandatory quotes, no trailing commas). Teams who use YAML for all other configuration — Kubernetes, Helm, CI pipelines — face a context-switch friction and lose the ability to annotate test intent inline. YAML anchors and aliases would allow common mock setups to be defined once and referenced across test cases, eliminating verbatim repetition that JSON forces today.

## User / Personas

**API test author** — writes and maintains ConfIT test suites. Wants to annotate tests with comments explaining intent, and to avoid duplicating common mock interaction blocks across test cases via YAML anchors.

**Platform/infra engineer** — already writes YAML for all infrastructure config. Wants to write ConfIT tests in the same format without context-switching to JSON syntax rules.

## Scope

**In scope:**
- `.yaml` and `.yml` files readable by `TestReader.GetTestsForAFile` and `TestReader.GetTestsForAFolder`
- Mixed JSON+YAML folders: both formats discovered and yielded in the same call
- YAML anchors and aliases — expanded transparently before the DSL pipeline sees the content
- YAML comments (`#`) — stripped during parse, no error
- Correct YAML scalar type mapping: unquoted strings → `string`, integers → `number`, `true`/`false` → `boolean`, `null`/`~` → `null`
- All existing DSL features (matchers, semantic matchers, `ignore`, `pattern`, `bodyFromFile`, `tags`, variable `extract`) work identically in YAML — no new handling required

**Out of scope:**
- `bodyFromFile` target files in YAML — body fixture files remain JSON
- `SuiteConfig` in YAML — library configuration is not part of the test DSL
- YAML multi-document files (`---` separator for multiple docs in one file) — single-document only
- YAML type tags (`!!str`, `!!int` coercions) — not needed by the DSL
- Any change to DTO classes, matcher logic, variable extraction, or test execution behaviour

## Boundary Conditions

- The YAML-to-`JObject` conversion happens at the file-read boundary inside `TestReader`. Once converted, the token enters the existing deserialization pipeline (`JToken` → `ToTestCase()`) identically to a JSON-sourced token.
- `bodyFromFile` paths inside a YAML test file still resolve to `.json` files in `RequestBodyFolder` / `ResponseBodyFolder`. The YAML format applies only to the top-level test definition file.
- If a folder contains both a `tests.json` and a `tests.yaml` file with overlapping test names, both are yielded — duplicate detection is not part of this feature.
- `GetTestsForAFolder` file ordering remains consistent: all files (`.json` and `.yaml`/`.yml`) are included in the same enumeration with no format-based sorting preference.

## Assumptions

- `YamlDotNet` (MIT licence, ~200M NuGet downloads) is the YAML parser. No other YAML library is evaluated.
- The conversion strategy is YAML → `Dictionary<object, object>` via YamlDotNet → JSON string via `JsonConvert.SerializeObject` → `JObject.Parse`. This round-trip is the entire adapter; no custom converters are needed.
- YAML anchors and aliases are resolved by YamlDotNet during deserialization — the DSL pipeline never sees YAML syntax.
- Existing JSON test files are not touched. All changes are purely additive.

## Scenarios

### Scenario 1: Single YAML file loaded by `GetTestsForAFile`

A `.yaml` test file can be passed directly to `GetTestsForAFile` in place of `.json`.

**Acceptance Criteria:**
- Given a `.yaml` file containing one or more test cases in the standard DSL structure
- When `TestReader.GetTestsForAFile(folder, "tests.yaml")` is called
- Then each top-level YAML key is yielded as a test name with its body as a `JToken`
- And YAML scalar types map correctly: unquoted strings → `string`, integers → `number`, `true`/`false` → `boolean`, `null`/`~` → `null`
- And the yielded `JToken` passes through `ToTestCase()` without modification or error

### Scenario 2: YAML folder discovery by `GetTestsForAFolder`

A folder containing only YAML test files is scanned and all tests are yielded.

**Acceptance Criteria:**
- Given a folder containing `.yaml` and/or `.yml` test files (no `.json` files)
- When `TestReader.GetTestsForAFolder(folder)` is called
- Then all tests from all YAML files in the folder are yielded
- And both `.yaml` and `.yml` extensions are discovered

### Scenario 3: Mixed JSON+YAML folder — both formats run

JSON and YAML coexist in the same folder; neither format shadows the other.

**Acceptance Criteria:**
- Given a folder containing both `.json` and `.yaml` files
- When `TestReader.GetTestsForAFolder(folder)` is called
- Then tests from all files regardless of format are yielded
- And the total test count equals the sum of tests across all files in the folder

### Scenario 4: YAML anchors and aliases expand correctly

Common blocks defined with `&anchor` and referenced with `*alias` expand to their full content before the DSL sees them.

**Acceptance Criteria:**
- Given a YAML test file that defines a block with `&anchorName` and references it with `*anchorName` in one or more test cases
- When the file is loaded
- Then each referencing test has the anchor content fully expanded
- And the resulting `TestCase` is structurally identical to writing the content explicitly inline

### Scenario 5: YAML comments are stripped transparently

Test authors can annotate intent with `#` comments; comments do not reach the DSL parser.

**Acceptance Criteria:**
- Given a YAML test file with `#` comments (both inline and on standalone lines)
- When the file is loaded
- Then the parse succeeds without error
- And no comment text appears in any deserialized test field value

### Scenario 6: Invalid YAML syntax raises a descriptive error

A malformed YAML file fails fast with a locatable error rather than silently producing empty or corrupt tests.

**Acceptance Criteria:**
- Given a `.yaml` file with a syntax error (e.g., inconsistent indentation, tab character where space is required)
- When the file is loaded
- Then an exception is thrown before any tests are yielded
- And the exception message identifies the file and the location of the error

### Scenario 7: All existing DSL features work identically in YAML

YAML is a declaration format only. Every DSL capability available in JSON works unchanged when the same test is written in YAML.

**Acceptance Criteria:**
- Given a YAML test file that uses `tags`, `mock.interactions`, `api.request.bodyFromFile`, `api.response.matcher.ignore`, `api.response.matcher.pattern`, `api.response.matcher.semantic`, and `api.response.extract`
- When the test executes
- Then every field is interpreted identically to the equivalent JSON test
- And no matcher type, extractor, or injector requires YAML-specific handling

*(Scenarios ordered chronologically — natural implementation sequence.)*

## Implementation Notes

1. **`YamlConverter` (`src/ConfIT/Util/YamlConverter.cs`)** — static helper with a single method `ToJObject(string yaml) → JObject`. Uses `YamlDotNet.Serialization.DeserializerBuilder` to deserialize the YAML string to a `Dictionary<object, object>` graph (anchors/aliases expanded automatically), then `JsonConvert.SerializeObject` → `JObject.Parse` to produce a `JObject`. No knowledge of test structure — pure format adapter.
2. **`TestReader` changes** — `GetTestsForAFile`: detect `.yaml`/`.yml` extension, call `YamlConverter.ToJObject` instead of `JObject.Parse`. `GetTestsForAFolder`: extend the file filter from `.json`-only to include `.yaml` and `.yml`. No other changes to `TestReader`.
3. **NuGet** — add `YamlDotNet` to `src/ConfIT/ConfIT.csproj` for both `net9.0` and `net10.0` target frameworks (no version condition needed — single version covers both).
4. **Unit tests (`test/ConfIT.UnitTest/Util/YamlConverterTests.cs` and `TestReaderTests.cs`)** — cover all 7 scenarios; scenario 7 uses a YAML fixture exercising every matcher type to confirm zero behavioural delta.
5. **Example coverage** — add one `.yaml` test file to `example/User.ComponentTests/TestCase/` and `example/User.IntegrationTests/TestCase/`; register the component test file under `<None Update>` with `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` in `User.ComponentTests.csproj`.

## Open Questions

- [ ] Should `GetTestsForAFile` accept a filename with no extension and probe for `.json` then `.yaml` automatically, or should callers always pass the full filename including extension? Recommendation: require full filename — explicit is safer and matches current JSON behaviour.

## Links

- Design: [dsl-002-yaml-support.md](../../context/dsl-002-yaml-support.md)
- Epic index: [reduce-team-friction.md](../epics/reduce-team-friction.md)
