# Suite Setup

There are three ways to wire a ConfIT test suite, ordered from least to most code:

- **Bootstrapped (recommended)** — `SuiteBootstrapper` reads `suite.config.yaml` and returns a fully-wired, disposable suite in one call. Fixtures are 5–10 lines.
- **Adapter chain** — call `SuiteConfiguration.Load*` and run the `.ToSuiteConfig()`, `.ToTestFilter()`, `.ToAuthTokenProvider()` adapters manually. Use when you need control the bootstrapper doesn't expose.
- **Manual wiring** — construct `SuiteConfig`, `TestFilter`, and `TestSuiteInitializer` directly. Use for advanced scenarios or custom config sources.

All three paths produce the same objects and work identically at runtime.

---

## `suite.config.yaml`

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
    envVariable: TEST_TAGS
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
    envVariable: TEST_TAGS
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
      envVariable: TEST_TAGS

  qa:
    api:
      url: ${QA_API_URL}
    auth:
      type: bearer
      token: ${QA_API_TOKEN}
    folders:
      response: ApiResponses
      requestBody: TestCase/Request
      responseBody: TestCase/Response
    filter:
      strategy: tags
      envVariable: TEST_TAGS
```

#### `${ENV_VAR}` interpolation

Any scalar string value can reference an environment variable with `${VAR_NAME}`. Variables are resolved at load time. If a referenced variable is not set, `SuiteConfiguration` throws with a clear message identifying the field and file.

#### Copy to output

`suite.config.yaml` must be present in the test output directory at runtime. Add this to your `.csproj`:

```xml
<None Update="suite.config.yaml">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</None>
```

---

## Bootstrapped Setup (Recommended)

`SuiteBootstrapper` reads `suite.config.yaml`, starts any required infrastructure, creates all ConfIT objects, and returns a `BootstrappedSuite`. Pass its `Context` property to `BaseTest`. Dispose the suite in the fixture's `Dispose()` — it prints the summary and shuts down infrastructure in the right order.

### Component fixture — in-process mode

```csharp
public class TestSuiteFixture : IDisposable
{
    private readonly BootstrappedSuite _suite;

    public TestSuiteFixture() =>
        _suite = SuiteBootstrapper.ForComponent<Startup>("suite.config.yaml",
            onStarted: services =>
            {
                // Optional: project-specific initialisation after the host is ready.
                // Use this for DB seeding, schema setup, or any setup that needs DI.
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                db.Database.EnsureCreated();
            });

    public TestSuiteContext Context => _suite.Context;

    public void Dispose() => _suite.Dispose();
}
```

`onStarted` fires after `WebApplicationFactory` boots the service and before any tests run. If your app seeds its own test data on startup (self-seeding pattern — see [AppLauncher](./app-launcher.md#self-seeding-pattern)), omit the callback entirely:

```csharp
_suite = SuiteBootstrapper.ForComponent<Startup>("suite.config.yaml");
```

📄 Live example: [`User.ComponentTests/SetUp/TestSuiteFixture.cs`](../example/User.ComponentTests/SetUp/TestSuiteFixture.cs)

---

### Integration fixture

```csharp
public class TestSuiteFixture : IDisposable
{
    private readonly BootstrappedSuite _suite;

    public TestSuiteFixture() =>
        _suite = SuiteBootstrapper.ForIntegration("suite.config.yaml");

    public TestSuiteContext Context => _suite.Context;

    public void Dispose() => _suite.Dispose();
}
```

Pass `environment: "qa"` to override the YAML `default` and env var lookup:

```csharp
_suite = SuiteBootstrapper.ForIntegration("suite.config.yaml", environment: "qa");
```

Select the environment at runtime:

```bash
TEST_ENVIRONMENT=qa TEST_TAGS=smoke dotnet test   # QA smoke run
dotnet test                                        # falls back to 'default' (local)
```

📄 Live example: [`User.IntegrationTests/TestSuiteFixture.cs`](../example/User.IntegrationTests/TestSuiteFixture.cs)

---

### Command mode (AppLauncher)

```csharp
public class TestSuiteFixture : IDisposable
{
    private readonly BootstrappedSuite _suite;

    public TestSuiteFixture() =>
        _suite = SuiteBootstrapper.ForCommand("suite.config.yaml");

    public TestSuiteContext Context => _suite.Context;

    public void Dispose() => _suite.Dispose();
}
```

`ForCommand` starts the external process from the `startup.command` in your YAML, waits for the readiness probe, and builds the HTTP client. Dispose shuts down the process and waits for port release. See [AppLauncher](./app-launcher.md) for full coverage: readiness probe options, stopCommand, the self-seeding pattern, language-agnostic testing, and the Makefile pre-build pattern.

📄 Live example: [`User.ComponentTests.AppLauncher/SetUp/TestSuiteFixture.cs`](../example/User.ComponentTests.AppLauncher/SetUp/TestSuiteFixture.cs)

---

### Custom matchers

Pass custom semantic matchers as the `customMatchers` parameter:

```csharp
_suite = SuiteBootstrapper.ForIntegration("suite.config.yaml",
    customMatchers: new Dictionary<string, SemanticMatcherFunc>
    {
        ["isDomainId"] = (token, _) =>
            token.Value<string>()?.StartsWith("DOM-") == true
                ? null
                : $"Expected domain ID starting with 'DOM-' but got: {token}"
    });
```

For full custom matcher documentation — signature, naming constraints, DSL usage — see [Extending ConfIT](./extending-confit.md#domain-specific-matchers).

---

### Auth profiles

Add an `auth:` block at the `component` or environment level. `SuiteBootstrapper` reads it, constructs the provider, and injects the header on every request — no C# required for bearer, OAuth2 client credentials, and API key auth. For full reference see [Auth Profiles](./auth-profiles.md).

---

### The test class

```csharp
public class UserTests : BaseTest, IClassFixture<TestSuiteFixture>
{
    public UserTests(TestSuiteFixture fixture, ITestOutputHelper output)
        : base(fixture.Context, new TestOutputLogger(output))
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

`test.ToTestCase(requestFolder, responseFolder)` deserialises the raw token into a typed `TestCase`. Pass `null` for both folders when tests use inline bodies only. Pass `Config.RequestBodyFolder` and `Config.ResponseBodyFolder` for integration tests that load bodies from files:

```csharp
public async Task ExecuteTest(string testName, JContainer test, string sourceFile) =>
    await Execute(testName, test.ToTestCase(Config.RequestBodyFolder, Config.ResponseBodyFolder), sourceFile);
```

`Config` is available as a protected property on `BaseTest` — it reads from the context passed to the constructor.

`TestOutputLogger` is a thin adapter; implement it in your test project:

```csharp
public class TestOutputLogger : ITestOutputLogger
{
    private readonly ITestOutputHelper _output;
    public TestOutputLogger(ITestOutputHelper output) => _output = output;
    public void Log(string msg) => _output.WriteLine(msg);
}
```

📄 Live example: [`User.ComponentTests/UserComponentTests.cs`](../example/User.ComponentTests/UserComponentTests.cs)

---

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

## Adapter Chain (Custom Wiring)

Use the adapter chain when the bootstrapper doesn't expose what you need — for example, when combining auth with a custom `ITestProcessorFactory` in the same fixture, or loading config from a non-standard source.

```csharp
public class TestSuiteFixture : IDisposable
{
    public TestSuiteFixture()
    {
        var cfg = SuiteConfiguration.LoadComponent("suite.config.yaml");
        var initializer = new TestSuiteInitializer<Startup>(cfg.Startup.Settings!);

        // Project-specific: seed the database via DI
        using var scope = initializer.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        db.Database.EnsureCreated();

        var suiteConfig = cfg.ToSuiteConfig();
        suiteConfig.ApiResponseFolder = Directory
            .CreateDirectory(Path.Combine(Environment.CurrentDirectory,
                cfg.Folders?.Response ?? "responses")).FullName;
        suiteConfig.CustomMatchers = new Dictionary<string, SemanticMatcherFunc>
        {
            ["isDomainId"] = (token, _) =>
                token.Value<string>()?.StartsWith("DOM-") == true ? null
                    : $"Expected domain ID starting with 'DOM-' but got: {token}"
        };

        Context = new TestSuiteContext(
            HttpClient:      initializer.TestHttpClient,
            Config:          suiteConfig,
            ProcessorFactory: new MyTestProcessorFactory(suiteConfig),
            Filter:          cfg.ToTestFilter(),
            ResultCollector: new TestResultCollector());

        _initializer = initializer;
    }

    public TestSuiteContext Context { get; }

    private readonly TestSuiteInitializer<Startup> _initializer;

    public void Dispose()
    {
        Context.ResultCollector?.Dispose();
        _initializer.Dispose();
    }
}
```

`SuiteConfiguration.LoadComponent` reads the `component` section, validates all keys, and resolves `${ENV_VAR}` tokens. `SuiteConfiguration.LoadIntegration` reads the `integration` section and selects the active environment by checking, in order: the `environment` argument, the `TEST_ENVIRONMENT` env var, the `default` key in the file.

### `SuiteConfig` properties

| Property | Description |
|---|---|
| `ApiServerUrl` | Base URL of the service under test. Can be empty for in-process component tests — the `TestServer` handles routing. |
| `MockServerUrl` | WireMock base URL. Omit or leave empty to disable mocking. |
| `EnableMockServerLogs` | Print WireMock request logs to the console. Useful during debugging. |
| `RequestBodyFolder` | Folder to resolve `bodyFromFile` paths in request definitions. |
| `ResponseBodyFolder` | Folder to resolve `bodyFromFile` paths in expected response definitions. |
| `ApiResponseFolder` | Folder where actual responses are written after each test. |
| `CustomMatchers` | Additional named matchers — see [Extending ConfIT](./extending-confit.md#domain-specific-matchers). |

### `TestSuiteInitializer`

`TestSuiteInitializer<TProgram>` boots the service in-process using ASP.NET Core's `WebApplicationFactory`. Pass the app's `Startup` or `Program` class as the type argument.

```csharp
var initializer = new TestSuiteInitializer<Startup>(
    "appsettings.Tests.json",
    services =>
    {
        // Optional: override services registered by the app.
        var descriptor = services.Single(
            s => s.ServiceType == typeof(DbContextOptions<MyDb>));
        services.Remove(descriptor);
        services.AddDbContext<MyDb>(o => o.UseInMemoryDatabase("test"));
    });
```

`initializer.TestHttpClient` routes requests through the in-process server — no network required. `initializer.Services` exposes the DI container for seeding.

---

## Manual Wiring

For cases where config doesn't come from a file at all, or where you need properties not reachable through either the bootstrapper or the adapter chain.

```csharp
public class TestSuiteFixture : IDisposable
{
    public TestSuiteFixture()
    {
        var initializer = new TestSuiteInitializer<Startup>("appsettings.Tests.json");

        var suiteConfig = new SuiteConfig
        {
            MockServerUrl     = "http://localhost:8888",
            ApiResponseFolder = Directory.CreateDirectory("responses").FullName
        };

        Context = new TestSuiteContext(
            HttpClient:      initializer.TestHttpClient,
            Config:          suiteConfig,
            Filter:          TestFilter.CreateForTagsFromEnvVariable("TEST_TAGS"),
            ResultCollector: new TestResultCollector());
    }

    public TestSuiteContext Context { get; }

    public void Dispose() => Context.ResultCollector?.Dispose();
}
```

The test class constructor is identical for all three paths:

```csharp
public UserTests(TestSuiteFixture fixture, ITestOutputHelper output)
    : base(fixture.Context, new TestOutputLogger(output))
{
}
```

---

## Shared Concepts

### `TestFilter`

Controls which tests run. Passed inside `TestSuiteContext` — the bootstrapper builds it from the `filter:` section automatically. If no `filter:` section is present, the filter is `null` and all tests run.

When constructing manually:

```csharp
// Read tag list from TEST_TAGS — unset means all tests run
Filter = TestFilter.CreateForTagsFromEnvVariable("TEST_TAGS");

// Read test names from TEST_NAMES
Filter = TestFilter.CreateForTestsFromEnvVariable("TEST_NAMES");

// Hardcode tags or names (useful for local debugging)
Filter = TestFilter.CreateForTags("smoke");
Filter = TestFilter.CreateForTests("ShouldCreateAUser,ShouldGetUserById");
```

At runtime:

```bash
TEST_TAGS=smoke dotnet test
TEST_NAMES=ShouldCreateAUser,ShouldGetUserById dotnet test
```

### `TestResultCollector`

Accumulates pass/fail/skip results and prints a grouped summary table when the suite ends. The bootstrapper creates it automatically — dispose the `BootstrappedSuite` and the summary prints. When wiring manually, create it in the fixture and dispose it in `Dispose()`.

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

### `TestOutputLogger`

`BaseTest` accepts an `ITestOutputLogger` to route log messages into xUnit's `ITestOutputHelper`. Implement a thin adapter in your test project:

```csharp
public class TestOutputLogger : ITestOutputLogger
{
    private readonly ITestOutputHelper _output;
    public TestOutputLogger(ITestOutputHelper output) => _output = output;
    public void Log(string msg) => _output.WriteLine(msg);
}
```

📄 Live example: [`User.ComponentTests/SetUp/TestOutputLogger.cs`](../example/User.ComponentTests/SetUp/TestOutputLogger.cs)
