# Suite Setup

There are two ways to wire a ConfIT test suite:

- **Config-driven (recommended)** — a `suite.config.yaml` file holds all URLs, folders, and filter settings. One API call loads it; extension methods convert it to the objects `BaseTest` needs.
- **Manual wiring** — construct `SuiteConfig`, `TestFilter`, and `TestSuiteInitializer` directly in code. Use this when you need fine-grained control that the YAML format doesn't expose.

Both approaches produce the same objects and work identically at runtime.

---

## Config-Driven Setup (Recommended)

### `suite.config.yaml`

Place `suite.config.yaml` in the test project root. It drives both component and integration suites.

#### Component suite — in-process mode

```yaml
component:
  startup:
    mode: in-process
    settings: appsettings.Tests.json
  api:
    url: http://localhost:5170
  mock:
    url: http://localhost:8888
  folders:
    response: responses
  filter:
    strategy: tags
    envVariable: RUN_POOLS
```

#### Component suite — command mode

```yaml
component:
  startup:
    mode: command
    command: dotnet run --no-build --project ../../../../User.Api --launch-profile ComponentTest
    readiness:
      port: 5170
      timeoutSeconds: 60
  api:
    url: http://localhost:5170
  mock:
    url: http://localhost:8888
  folders:
    response: responses
  filter:
    strategy: tags
    envVariable: RUN_POOLS
```

#### Integration suite — multi-environment

```yaml
integration:
  default: local

  local:
    api:
      url: http://localhost:5170
    folders:
      response: ApiResponses
      requestBody: TestCase/Request
      responseBody: TestCase/Response
    filter:
      strategy: tags
      envVariable: RUN_POOLS

  qa:
    api:
      url: ${QA_API_URL}
      authToken: ${QA_API_TOKEN}
    folders:
      response: ApiResponses
      requestBody: TestCase/Request
      responseBody: TestCase/Response
    filter:
      strategy: tags
      envVariable: RUN_POOLS
```

#### `${ENV_VAR}` interpolation

Any scalar string value can reference an environment variable with `${VAR_NAME}`. Variables are resolved at load time. If a referenced variable is not set, `SuiteConfiguration` throws with a clear message identifying the field and file.

```yaml
api:
  url: ${QA_API_URL}
  authToken: ${QA_API_TOKEN}
```

#### Copy to output

`suite.config.yaml` must be present in the test output directory at runtime. Add this to your `.csproj`:

```xml
<None Update="suite.config.yaml">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</None>
```

---

### Component test fixture (in-process mode)

```csharp
public class TestSuiteFixture : IDisposable
{
    public TestSuiteFixture()
    {
        var cfg = SuiteConfiguration.LoadComponent("suite.config.yaml");
        var initializer = new TestSuiteInitializer<Startup>(cfg.Startup.Settings!);
        InitializeDb(initializer);
        TestHttpClient  = initializer.TestHttpClient;
        SuiteConfig     = cfg.ToSuiteConfig();
        SuiteConfig.ApiResponseFolder = EnsureDirectory(cfg.Folders?.Response ?? "responses");
        Filter          = cfg.ToTestFilter();
        ResultCollector = new TestResultCollector();
    }

    public TestHttpClient       TestHttpClient  { get; private set; }
    public SuiteConfig          SuiteConfig     { get; private set; }
    public TestFilter           Filter          { get; private set; }
    public TestResultCollector  ResultCollector { get; }

    private static void InitializeDb(TestSuiteInitializer<Startup> initializer)
    {
        using var scope = initializer.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        db.Database.EnsureCreated();
    }

    private static string EnsureDirectory(string relativePath) =>
        Directory.CreateDirectory(
            Path.Combine(Environment.CurrentDirectory, relativePath)).FullName;

    public void Dispose() => ResultCollector.Dispose();
}
```

`SuiteConfiguration.LoadComponent` reads the `component` section, validates all keys, and resolves `${ENV_VAR}` tokens. `.ToSuiteConfig()` and `.ToTestFilter()` convert it to the objects `BaseTest` expects.

`ApiResponseFolder` is set after `.ToSuiteConfig()` because the directory must exist on disk — `EnsureDirectory` creates it if absent.

📄 Live example: [`User.ComponentTests/SetUp/TestSuiteFixture.cs`](../example/User.ComponentTests/SetUp/TestSuiteFixture.cs)

---

### Integration test fixture

```csharp
public class TestSuiteFixture : IDisposable
{
    public TestSuiteFixture()
    {
        var cfg = SuiteConfiguration.LoadIntegration("suite.config.yaml");
        SuiteConfig     = cfg.ToSuiteConfig();
        TestHttpClient  = TestHttpClient.Create(cfg.Api.Url!, new AuthTokenProvider());
        Filter          = cfg.ToTestFilter();
        ResultCollector = new TestResultCollector();
        Directory.CreateDirectory(
            Environment.CurrentDirectory + $"/{SuiteConfig.ApiResponseFolder}");
    }

    public TestHttpClient       TestHttpClient  { get; }
    public SuiteConfig          SuiteConfig     { get; }
    public TestFilter           Filter          { get; }
    public TestResultCollector  ResultCollector { get; }

    public void Dispose() => ResultCollector.Dispose();
}
```

`SuiteConfiguration.LoadIntegration` reads the `integration` section. It selects the active environment by checking, in order:

1. The `environment` argument passed directly to `LoadIntegration`
2. The `TEST_ENVIRONMENT` environment variable
3. The `default` key in the config file

Select the environment at runtime:

```bash
TEST_ENVIRONMENT=qa RUN_POOLS=smoke dotnet test   # QA smoke run
dotnet test                                        # falls back to 'default' (local)
```

📄 Live example: [`User.IntegrationTests/TestSuiteFixture.cs`](../example/User.IntegrationTests/TestSuiteFixture.cs)

---

### Command mode (AppLauncher)

Command mode starts the service under test as an external process before tests run and stops it afterwards. The fixture uses `AppLauncher.Start(cfg.ToAppLauncherConfig())` instead of `TestSuiteInitializer`. See [AppLauncher](./app-launcher.md) for full coverage, including readiness probe options and environment variable injection.

---

## Manual Wiring

Use manual wiring when you need properties `suite.config.yaml` doesn't expose (e.g., `EnableMockServerLogs`, `CustomMatchers`) or when loading config from a different source.

### `SuiteConfig`

`SuiteConfig` is the configuration bag passed to `BaseTest`.

| Property | Description |
|---|---|
| `ApiServerUrl` | Base URL of the service under test. Can be empty for in-process component tests — the `TestServer` handles routing. |
| `MockServerUrl` | WireMock base URL. Omit or leave empty to disable mocking. |
| `EnableMockServerLogs` | Print WireMock request logs to the console. Useful during debugging. |
| `RequestBodyFolder` | Folder to resolve `bodyFromFile` paths in request definitions. |
| `ResponseBodyFolder` | Folder to resolve `bodyFromFile` paths in expected response definitions. |
| `ApiResponseFolder` | Folder where actual responses are written after each test (used by `ITestProcessor`). |
| `CustomMatchers` | Additional named matchers — see [Matchers and Patterns](./matchers-and-patterns.md#custom-matchers). |

### `TestSuiteInitializer`

`TestSuiteInitializer<TProgram>` boots the service in-process using ASP.NET Core's `WebApplicationFactory`. Pass the app's `Startup` or `Program` class as the type argument — no `TestServerStartup` subclass is needed.

```csharp
var initializer = new TestSuiteInitializer<Startup>(
    "appsettings.Tests.json",
    services =>
    {
        // Optional: override services registered by the app.
        // Only needed when the app can't configure itself via
        // ASPNETCORE_ENVIRONMENT or a test-specific appsettings file.
        var descriptor = services.Single(
            s => s.ServiceType == typeof(DbContextOptions<MyDb>));
        services.Remove(descriptor);
        services.AddDbContext<MyDb>(o => o.UseInMemoryDatabase("test"));
    });

// Seed the database via DI:
using var scope = initializer.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<MyDb>();
db.Database.EnsureCreated();
```

The first argument is an appsettings JSON file name, resolved relative to the test output directory. The second argument is an optional `Action<IServiceCollection>` for service overrides.

`initializer.TestHttpClient` routes requests through the in-process server — no network required.

### `TestSuiteFixture` (manual)

```csharp
public class TestSuiteFixture : IDisposable
{
    public TestSuiteFixture()
    {
        var initializer = new TestSuiteInitializer<Startup>("appsettings.Tests.json");
        TestHttpClient = initializer.TestHttpClient;

        SuiteConfig = new SuiteConfig
        {
            MockServerUrl     = "http://localhost:8888",
            ApiResponseFolder = Directory.CreateDirectory("responses").FullName
        };

        Filter          = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");
        ResultCollector = new TestResultCollector();
    }

    public TestHttpClient       TestHttpClient  { get; }
    public SuiteConfig          SuiteConfig     { get; }
    public TestFilter           Filter          { get; }
    public TestResultCollector  ResultCollector { get; } = new TestResultCollector();

    public void Dispose() => ResultCollector.Dispose();
}
```

### Test class

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

`test.ToTestCase(requestFolder, responseFolder)` deserialises the raw token into a typed `TestCase`. Pass `null` for both folders when tests use inline bodies only.

📄 Live example: [`User.ComponentTests/UserComponentTests.cs`](../example/User.ComponentTests/UserComponentTests.cs)

### Copy test files to output

Every test definition file needs `CopyToOutputDirectory` set or `TestReader` won't find it at runtime.

```xml
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

## Shared Setup

### `TestFilter`

Controls which tests run. Pass a `TestFilter` to the `BaseTest` constructor. If `null`, all tests run.

```csharp
// Read tag list from RUN_POOLS — unset means all tests run
Filter = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");

// Read test names from RUN_TESTS
Filter = TestFilter.CreateForTestsFromEnvVariable("RUN_TESTS");

// Hardcode tags or names (useful for local debugging)
Filter = TestFilter.CreateForTags("smoke");
Filter = TestFilter.CreateForTests("ShouldCreateAUser,ShouldGetUserById");
```

At runtime:

```bash
RUN_POOLS=smoke dotnet test
RUN_TESTS=ShouldCreateAUser,ShouldGetUserById dotnet test
```

Tests without tags always run when a tag filter is active. Tests not in the name list are skipped when a name filter is active. When using `suite.config.yaml`, `.ToTestFilter()` builds the filter from the `filter` section automatically — no manual construction needed.

### `TestResultCollector`

Accumulates pass/fail/skip results and prints a grouped summary table when the suite ends. Create it in the fixture, pass it to `BaseTest`, and dispose it in `Dispose()`.

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

### `TestOutputLogger`

`BaseTest` accepts an `ITestOutputLogger` to route log messages into xUnit's `ITestOutputHelper` (so they appear in the IDE test output pane alongside each test). The interface has one method; implement a thin adapter in your test project:

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
