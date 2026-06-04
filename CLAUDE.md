# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

**ConfIT** is a .NET library for declarative API integration testing. Tests are defined in JSON files (YAML support planned) and executed against real or mocked HTTP services. The library handles request execution, mock setup, response matching, and test filtering — consumers only write JSON test definitions and minimal C# glue code.

Two test modes:
- **Component tests** — service under test runs in-process (`TestSuiteInitializer`), external dependencies mocked via WireMock
- **Integration tests** — service runs out-of-process, all services real

---

## Repository Layout

```
src/ConfIT/              # Core library
  Contract/              # Public interfaces (extension points)
  Extension/             # Extension methods
  Server/
    Dto/                 # JSON-deserializable test definition DTOs
    Http/                # HTTP client + in-process server initializer
    Mock/                # WireMock.Net integration
  Util/                  # ResultMatcher, TestReader

test/ConfIT.UnitTest/    # Unit tests for the library itself

example/
  User.Api/              # Sample ASP.NET Core service (net8.0)
  JustAnotherService/    # Sample dependency service (net8.0)
  User.ComponentTests/   # Component test suite (net8.0)
  User.IntegrationTests/ # Integration test suite (net8.0)
```

---

## Build and Test Commands

**Always use `make` targets for building and testing — do not use `dotnet build` / `dotnet test` directly.**
The Makefile handles multi-target builds, service lifecycle, and DB resets correctly. Direct `dotnet` commands below are for reference only.

**Makefile — primary local workflow:**
```bash
make              # build + unit + component tests (default)
make test         # unit + component tests only
make unit         # unit tests only
make component    # component tests only
make integration  # wipe DB, start services, run integration tests, stop services
make ci           # full pipeline: build + unit + component + integration
make clean        # stop services, remove build artefacts and SQLite DB
make services-stop# kill running User.Api / JustAnotherService processes
make help         # list all targets with descriptions
```

**Core library (uses modern `.slnx` format):**
```bash
dotnet build './src/ConfIT.slnx' --configuration Release
dotnet test './test/ConfIT.UnitTest/ConfIT.UnitTest.csproj' --configuration Release
dotnet pack './src/ConfIT' --configuration Release
```

**Example projects:**
```bash
dotnet build './example/User.sln' --configuration Release

# Component tests (in-process, no external services needed)
cd example/User.ComponentTests && dotnet test

# Integration tests — use make integration; it handles service lifecycle and DB reset automatically
```

**Filter test runs at runtime:**
```bash
export RUN_TESTS="ShouldCreateAUser,ShouldReturnUserForGivenId"  # by name
export RUN_POOLS="errors,user"                                     # by tag
```

---

## Target Frameworks

- **ConfIT library**: net10.0, net9.0
- **Unit tests**: net10.0, net9.0
- **Example projects**: net9.0

---

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| FluentAssertions | 7.2.0 | Assertions in unit tests |
| JsonDiffPatch.Net | 2.5.0 | JSON structural diff in ResultMatcher |
| Newtonsoft.Json | 13.0.4 | JSON parsing throughout |
| WireMock.Net | 1.16.0 | HTTP mock server for component tests |
| Microsoft.AspNetCore.TestHost | framework-matched | In-process server for component tests |

---

## Architecture

### Core Components

**`BaseTest` (`src/ConfIT/BaseTest.cs`)**  
Abstract base class. Orchestrates test execution via template method pattern:
1. Filter check (`ShouldSkipTheTest`)
2. Mock server setup (`HttpMockServer.Initialize`)
3. `ITestProcessor.Before(testApi)` — pre-request hook
4. HTTP call (`TestHttpClient.Execute`)
5. `ITestProcessor.After(testApi, actualResponse)` — post-response hook
6. Response body saved (`SaveApiResponse`)
7. Status code and body validation (`Verify`)

**`SuiteConfig` (`src/ConfIT/SuiteConfig.cs`)**  
Configuration bag passed to `BaseTest`:
- `MockServerUrl` — WireMock base URL (omit to disable mocking)
- `EnableMockServerLogs` — toggle WireMock request logs
- `ApiServerUrl` — base URL of service under test
- `ApiResponseFolder` — where actual responses are persisted (for dynamic linking)
- `RequestBodyFolder` — folder for external request body JSON files
- `ResponseBodyFolder` — folder for external expected response JSON files

**`TestFilter` (`src/ConfIT/TestFilter.cs`)**  
Controls which tests run. Factory methods:
- `TestFilter.CreateForTests(string names)` / `CreateForTestsFromEnvVariable(string key)`
- `TestFilter.CreateForTags(string tags)` / `CreateForTagsFromEnvVariable(string key)`

**`TestHttpClient` (`src/ConfIT/Server/Http/TestHttpClient.cs`)**  
Executes HTTP calls. Static factory: `TestHttpClient.Create(serverUrl, IAuthTokenProvider)`.  
Supports GET, POST, PUT, PATCH, DELETE. Injects auth header if provider returns a value.

**`TestSuiteInitializer<TStartup>` (`src/ConfIT/Server/Http/TestSuiteInitializer.cs`)**  
Bootstraps an in-process `TestServer` from a `Startup` class. Use for component tests.  
Exposes `TestServer` and `TestHttpClient` after construction.

**`HttpMockServer` (`src/ConfIT/Server/Mock/HttpMockServer.cs`)**  
Wraps WireMock.Net. Called by `BaseTest` to register mock interactions before each test.

**`ResultMatcher` (`src/ConfIT/Util/ResultMatcher.cs`)**  
Static response validation. `MatchResponseBody(actual, expected, matcher)`:
1. Applies `ignore` — removes listed fields from both sides
2. Applies `pattern` — validates each field with regex, then removes before diff
3. Runs `JsonDiffPatch` on the remaining structure

**`TestReader` (`src/ConfIT/Util/TestReader.cs`)**  
Reads test definitions. Returns `IEnumerable<object[]>` for xUnit `[MemberData]`:
- `GetTestsForAFile(folder, filename)`
- `GetTestsForAFolder(folder)` — all `.json` files in a folder

---

## Extension Points (Interfaces)

All in `src/ConfIT/Contract/`:

| Interface | Purpose | Key Method |
|-----------|---------|-----------|
| `IAuthTokenProvider` | Inject auth into every request | `Token() → string` |
| `ITestOutputLogger` | Redirect test logs | `Log(string msg)` |
| `ITestProcessor` | Hook into test lifecycle | `Before(TestApi)`, `After(TestApi, JToken)` |
| `ITestProcessorFactory` | Resolve processor per test | `GetTestProcessor(string testName) → ITestProcessor` |

`ITestProcessor.Before` can mutate the `TestApi` (e.g., inject IDs from a previous test).  
`ITestProcessor.After` receives the actual response for data extraction / state storage.

---

## Test Definition DSL (JSON)

Full structure reference:

```json
{
  "TestName": {
    "tags": ["tag1", "tag2"],

    "mock": {
      "interactions": [
        {
          "request": {
            "method": "GET",
            "path": "/some/path",
            "params": { "queryParam": "value" },
            "headers": { "X-Header": "value" },
            "body": {},
            "bodyFromFile": "filename.json"
          },
          "response": {
            "statusCode": 200,
            "body": {},
            "bodyFromFile": "filename.json",
            "override": { "field": "merged-value" },
            "headers": { "Content-Type": "application/json" }
          }
        }
      ]
    },

    "api": {
      "request": {
        "method": "POST",
        "path": "/api/endpoint",
        "params": { "queryParam": "value" },
        "headers": { "X-Custom": "value" },
        "body": { "field": "value" },
        "bodyFromFile": "request-body.json",
        "override": { "field": "overrides-file-field" }
      },
      "response": {
        "statusCode": 201,
        "body": { "field": "expected-value" },
        "bodyFromFile": "expected-response.json",
        "override": { "field": "overrides-file-field" },
        "headers": {},
        "matcher": {
          "ignore": ["id", "createdAt", "nested__child__field"],
          "pattern": {
            "id": "^[0-9a-f-]{36}$",
            "nested__field": "\\d+"
          }
        }
      }
    }
  }
}
```

### DSL Field Notes

- **`bodyFromFile`** — loads JSON from `RequestBodyFolder` or `ResponseBodyFolder`; merged with `body` if both present
- **`override`** — deep-merges on top of whatever `bodyFromFile` loaded; allows per-test variation over shared fixture files
- **`matcher.ignore`** — fields removed from both sides before comparison; use `__` as path separator for nesting (e.g., `address__city`)
- **`matcher.pattern`** — field validated against regex, then excluded from structural diff; same `__` separator for nesting
- **`tags`** — matched against `RUN_POOLS` env var; tests without matching tags are skipped

---

## xUnit Integration Pattern

```csharp
public class UserTests : BaseTest, IClassFixture<TestSuiteFixture>
{
    private readonly string _requestFolder;
    private readonly string _responseFolder;

    public UserTests(TestSuiteFixture fixture, ITestOutputHelper output)
        : base(fixture.TestHttpClient, fixture.Config, fixture.ProcessorFactory,
               new TestOutputLogger(output), TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS"))
    {
        _requestFolder = "path/to/requests";
        _responseFolder = "path/to/responses";
    }

    public static IEnumerable<object[]> TestCases =>
        TestReader.GetTestsForAFolder("TestCase");

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ExecuteTest(string testName, JToken test)
        => await Execute(testName, test.ToTestCase(_requestFolder, _responseFolder));
}
```

---

## Design Patterns in Codebase

| Pattern | Where |
|---------|-------|
| Template Method | `BaseTest.Execute` orchestrates the test workflow |
| Strategy | `ITestProcessor` — swap pre/post logic per test |
| Factory | `ITestProcessorFactory`, `TestHttpClient.Create`, `TestSuiteInitializer` |
| Fluent Builder | `BuilderExtension` wrapping WireMock DSL |
| Adapter | `TestOutputLogger` wraps xUnit's `ITestOutputHelper` |

---

## Active Development Direction

The library is being extended toward:
- **YAML test definition support** alongside existing JSON
- **Additional response matchers** beyond `ignore` and `pattern` (e.g., partial body, array matchers, type assertions)
- Broader declarative capabilities to reduce the C# glue code consumers need to write

When adding matchers: `ResultMatcher.cs` is the single point of change for matching logic. When adding format support: `TestReader.cs` handles deserialization entry points.

---

## Development Conventions

- `example/` projects are reference implementations — validate any library changes against them before shipping
- All new code must compile and behave consistently across net9.0 and net10.0
- NuGet release is triggered by a git tag — no manual publish steps
- Use `#region` / `#endregion` for logical sections within a class — never comment banners (`// ── Section ───`)
- Test method naming: `Method_Condition_ExpectedOutcome` — e.g. `LoadComponent_MissingApiUrl_Throws`, `Apply_IsUuidWithValidUuid_Passes`
  - No noise prefixes: no `Should`, `When`, `With`, `Given`
  - Condition must be concrete: `_MissingApiUrl_`, `_EmptyString_`, `_PortAlreadyInUse_` — never vague like `_Invalid_`, `_BadValue_`, `_WhenThingsGoWrong_`
  - BDD flat naming (`Given_X_When_Y_Then_Z`) and nested class BDD were explicitly considered and rejected — this library tests parsing/validation/matching logic, not a business domain; BDD ceremony has no payoff here
- Test structure: Given / When / Then with blank line separation; `// Given`, `// When`, `// Then` inline comments on non-trivial tests
- Shared test helpers (builders, port allocation, file helpers) extracted to file-level static helpers — never duplicated across classes in the same file

## Keeping Examples in Sync

The `example/` projects are not just demos — they run in CI as regression gates for the library at the component and integration level. When making any library change, the examples must stay current:

**When adding a new DSL feature** (new JSON/YAML fields, new matcher types, extraction, etc.):
- Add test cases in **both** `example/User.IntegrationTests/TestCase/` and `example/User.ComponentTests/TestCase/`. Component + integration coverage together is the primary confidence gate beyond unit tests — not optional.
- **New test definition files must be written in YAML (`.yaml`), not JSON.** JSON remains supported for existing files and backward compatibility; all new example test files use YAML.
- New YAML/JSON files in `User.ComponentTests/TestCase/` must be registered in `User.ComponentTests.csproj` under `<None Update>` with `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` or they will not be discovered at runtime.
- Component tests run JSON files alphabetically via `GetTestCasesForFolder`. Tests that depend on prior state (e.g., a created user) must live in the same file as their prerequisite — not a separate file.

**When changing existing behaviour** (response matching, request execution, filter logic):
- Review existing example test cases — update any that relied on the old behaviour
- Run `make component` to validate component-level behaviour

**When removing or deprecating an extension point** (interfaces, config fields):
- Check both example projects for usages and update or remove them before the library change ships

**Filters in fixtures**: Test filters in example fixtures (`TestFilter.CreateForTagsFromEnvVariable`) should not be hardcoded with `Environment.SetEnvironmentVariable` — that hides tests in CI. Let env vars be set externally; unset means all tests run.

**CI coverage**: The CI pipeline builds and tests both `User.ComponentTests` and `User.IntegrationTests` (integration requires running services). A library change that breaks example compilation or component tests must be fixed before merge.

---

## Curated References

- [.NET API docs](https://learn.microsoft.com/en-us/dotnet/api/) — official Microsoft reference
- [WireMock.Net docs](https://github.com/WireMock-Net/WireMock.Net/wiki) — mock server configuration
- [Newtonsoft.Json docs](https://www.newtonsoft.com/json/help/html/Introduction.htm) — JSON serialization
- [FluentAssertions docs](https://fluentassertions.com/introduction) — assertion patterns
- [xUnit docs](https://xunit.net/docs/getting-started/v3/cmdline) — test framework integration
- [NuGet packaging docs](https://learn.microsoft.com/en-us/nuget/create-packages/creating-a-package-dotnet-cli) — library publishing
