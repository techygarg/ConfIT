# Suite Setup

This guide covers how to install ConfIT and wire it into an xUnit project. Examples use the component test setup — an in-process server with mocked dependencies. Integration tests follow the same pattern without the mock server and `TestSuiteInitializer`.

---

## Install

```bash
dotnet add package ConfIT
```

---

## The Three Moving Parts

Every ConfIT test suite needs:

1. **`TestSuiteFixture`** — created once per test class, holds shared infrastructure (HTTP client, config, collector)
2. **A test class** extending `BaseTest` — defines where test files live and drives xUnit's `[Theory]`
3. **Test definition files** — `.json` or `.yaml` files in your project

---

## `SuiteConfig`

`SuiteConfig` is the configuration bag passed to `BaseTest`. Set it up in your fixture.

| Property | Description |
|---|---|
| `ApiServerUrl` | Base URL of the service under test |
| `MockServerUrl` | WireMock base URL. Omit (or leave empty) to disable mocking. |
| `EnableMockServerLogs` | Print WireMock request logs to the console. Useful during debugging. |
| `RequestBodyFolder` | Folder to resolve `bodyFromFile` paths in request definitions |
| `ResponseBodyFolder` | Folder to resolve `bodyFromFile` paths in expected response definitions |
| `ApiResponseFolder` | Folder where actual responses are written after each test (used by `ITestProcessor`) |
| `CustomMatchers` | Additional named matchers — see [Matchers and Patterns](./matchers-and-patterns.md#custom-matchers) |

For component tests, `ApiServerUrl` can be left empty — the in-process `TestServer` handles routing.

---

## `TestSuiteFixture`

The fixture is shared across all tests in a class (via xUnit's `IClassFixture<T>`). It should:
- Start and configure the test server
- Build `SuiteConfig`
- Create a `TestResultCollector` and dispose it when the suite ends

```csharp
public class TestSuiteFixture : IDisposable
{
    public TestSuiteFixture()
    {
        // Spin up an in-process TestServer using your service's Startup class
        var initializer = new TestSuiteInitializer<Startup>("appsettings.Tests.json");
        TestHttpClient = initializer.TestHttpClient;

        SuiteConfig = new SuiteConfig
        {
            MockServerUrl      = "http://localhost:8888",
            ApiResponseFolder  = Directory.CreateDirectory("responses").FullName
        };

        // Optional: filter by tags (RUN_POOLS) or names (RUN_TESTS) at runtime
        // Filter = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");
    }

    public TestHttpClient       TestHttpClient  { get; private set; }
    public SuiteConfig          SuiteConfig     { get; private set; }
    public TestFilter           Filter          { get; private set; }
    public TestResultCollector  ResultCollector { get; } = new TestResultCollector();

    public void Dispose() => ResultCollector.Dispose();
}
```

`TestSuiteInitializer<TStartup>` boots your service in-process using ASP.NET Core's `TestServer`. The `TestHttpClient` it exposes routes requests through that in-process server — no network required.

📄 Live example: [`User.ComponentTests/SetUp/TestSuiteFixture.cs`](../example/User.ComponentTests/SetUp/TestSuiteFixture.cs)

---

## The Test Class

Extend `BaseTest`, implement `IClassFixture<T>`, and define a `[Theory]` that feeds test cases from your files.

```csharp
public class UserTests : BaseTest, IClassFixture<TestSuiteFixture>
{
    public UserTests(TestSuiteFixture fixture, ITestOutputHelper output)
        : base(
            fixture.TestHttpClient,
            fixture.SuiteConfig,
            null,                          // ITestProcessorFactory — null if not needed
            new TestOutputLogger(output),
            fixture.Filter,
            fixture.ResultCollector)
    {
    }

    [Theory]
    [MemberData(nameof(GetTestCasesForFolder), "TestCase")]
    public async Task ExecuteTest(string testName, JToken test, string sourceFile) =>
        await Execute(testName, test.ToTestCase(null, null), sourceFile);

    public static IEnumerable<object[]> GetTestCasesForFolder(string folder) =>
        TestReader.GetTestsForAFolder(folder);
}
```

`TestReader.GetTestsForAFolder("TestCase")` discovers all `.json` and `.yaml` files in the `TestCase` output directory and yields `(testName, testBody, sourceFileName)` tuples. xUnit feeds each tuple as a theory row.

`test.ToTestCase(requestFolder, responseFolder)` deserialises the raw token into a typed `TestCase`. Pass `null` for both folders if your tests use inline bodies (no `bodyFromFile`).

📄 Live example: [`User.ComponentTests/UserComponentTests.cs`](../example/User.ComponentTests/UserComponentTests.cs)

---

## Test Files Must Be Copied to Output

Test files in your project need `CopyToOutputDirectory` set, otherwise `TestReader` won't find them at runtime.

```xml
<!-- User.ComponentTests.csproj -->
<ItemGroup>
  <None Update="TestCase\user.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
  <None Update="TestCase\user-flows.yaml">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

---

## `TestFilter`

Controls which tests run. Pass a `TestFilter` to the `BaseTest` constructor. If `null`, all tests run.

```csharp
// Run only tests tagged "smoke"
Filter = TestFilter.CreateForTags("smoke");

// Read tag list from RUN_POOLS env var — unset means all tests run
Filter = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");

// Run specific tests by name
Filter = TestFilter.CreateForTests("ShouldCreateAUser,ShouldGetUserById");

// Read test names from RUN_TESTS env var
Filter = TestFilter.CreateForTestsFromEnvVariable("RUN_TESTS");
```

At runtime, set the env var before running:

```bash
RUN_POOLS=smoke dotnet test
RUN_TESTS=ShouldCreateAUser,ShouldGetUserById dotnet test
```

Tests without tags always run when a tag filter is active. Tests not in the name list are skipped when a name filter is active.

---

## `TestResultCollector`

`TestResultCollector` accumulates the pass/fail/skip result of every test and prints a grouped summary table when the suite ends. Wire it up in your fixture — create it, pass it to `BaseTest`, dispose it in `Dispose()`.

```csharp
// In TestSuiteFixture
public TestResultCollector ResultCollector { get; } = new TestResultCollector();
public void Dispose() => ResultCollector.Dispose();

// In the test class constructor
: base(..., fixture.ResultCollector)
```

The summary prints once after xUnit calls `Dispose()` on the fixture:

```
══════════════════════════════════════════════════════
  Suite Summary
══════════════════════════════════════════════════════

  errors.json
    ✓  ShouldReturnErrorIfUserNotExist               43ms
    ✗  ShouldNotCreateAUser_WhenValidationFails       89ms

  user.json
    ✓  ShouldCreateAUser                            118ms
    ✓  ShouldReturnUserForGivenEmailId               45ms

──────────────────────────────────────────────────────
  Total: 4   ✓ 3 passed   ✗ 1 failed   ⏭ 0 skipped
──────────────────────────────────────────────────────
```

Results are grouped by source file — useful when a suite spans several test files.

📄 Live example: [`User.ComponentTests/SetUp/TestSuiteFixture.cs`](../example/User.ComponentTests/SetUp/TestSuiteFixture.cs)

---

## `TestOutputLogger`

ConfIT's `BaseTest` accepts an `ITestOutputLogger` for routing log messages to xUnit's `ITestOutputHelper` (so they appear in the IDE test output pane alongside the test). The example projects provide a thin adapter:

```csharp
public class TestOutputLogger : ITestOutputLogger
{
    private readonly ITestOutputHelper _output;
    public TestOutputLogger(ITestOutputHelper output) => _output = output;
    public void Log(string msg) => _output.WriteLine(msg);
}
```

Pass it in the constructor: `new TestOutputLogger(output)`.

📄 Live example: [`User.ComponentTests/SetUp/TestOutputLogger.cs`](../example/User.ComponentTests/SetUp/TestOutputLogger.cs)

---

## Minimal Integration Test Setup

Integration tests use the same structure but without `TestSuiteInitializer`. The service runs out-of-process; `TestHttpClient.Create` points at its URL.

```csharp
public class TestSuiteFixture : IDisposable
{
    public TestSuiteFixture()
    {
        SuiteConfig = new SuiteConfig
        {
            ApiServerUrl       = "http://localhost:5170",
            RequestBodyFolder  = "TestCase/Request",
            ResponseBodyFolder = "TestCase/Response",
            ApiResponseFolder  = "ApiResponses"
        };
        TestHttpClient  = TestHttpClient.Create(SuiteConfig.ApiServerUrl);
        Filter          = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");
    }

    public TestHttpClient       TestHttpClient  { get; }
    public SuiteConfig          SuiteConfig     { get; }
    public TestFilter           Filter          { get; }
    public TestResultCollector  ResultCollector { get; } = new TestResultCollector();

    public void Dispose() => ResultCollector.Dispose();
}
```

📄 Live example: [`User.IntegrationTests/TestSuiteFixture.cs`](../example/User.IntegrationTests/TestSuiteFixture.cs)
