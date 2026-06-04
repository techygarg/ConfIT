# Extending ConfIT

ConfIT is intentionally declarative-first. The JSON DSL handles the majority of real-world test scenarios — requests, mocks, matching, variable flow — without any C# code. The extension points in this document exist for the minority of cases that genuinely need code: custom auth schemes, request signing, side effects, domain-specific assertions, conditional logic that can't be expressed in JSON.

Reach for the DSL first. Reach for these when the DSL can't express it.

---

## Auth — declarative and custom

See **[Auth Profiles](./auth-profiles.md)** for the complete auth reference: bearer, OAuth2 client credentials, API key, custom header keys, OAuth2 WireMock testing pattern, YAML-based header verification, `IAuthTokenProvider` interface, custom C# implementations, and when to use each approach.

---

## Routing ConfIT logs to xUnit output — `ITestOutputLogger`

ConfIT emits diagnostic messages (skip notices, matcher details) during test execution. By default these go to `Console`. To route them to xUnit's `ITestOutputHelper` — so they appear in your IDE's test output pane — implement the adapter and pass it to `BaseTest`.

**Interface:**

```csharp
public interface ITestOutputLogger
{
    void Log(string msg);
}
```

**Minimal adapter — the complete implementation:**

```csharp
public class TestOutputLogger : ITestOutputLogger
{
    private readonly ITestOutputHelper _output;

    public TestOutputLogger(ITestOutputHelper output) =>
        _output = output;

    public void Log(string msg) =>
        _output.WriteLine(msg);
}
```

Pass it in your test class constructor:

```csharp
public UserTests(TestSuiteFixture fixture, ITestOutputHelper output)
    : base(fixture.TestHttpClient, fixture.SuiteConfig, null,
           new TestOutputLogger(output), fixture.Filter, fixture.ResultCollector)
{ }
```

Full wiring context is shown in [suite-setup.md](suite-setup.md).

📄 Live example: [`User.ComponentTests/SetUp/TestOutputLogger.cs`](../example/User.ComponentTests/SetUp/TestOutputLogger.cs)

---

## Imperative hooks around each test — `ITestProcessor` and `ITestProcessorFactory`

> **Before reaching for this:** `ITestProcessor` is a legacy escape hatch. The majority of cases it was historically used for — passing a created resource's ID into a later request, injecting dynamic values into paths or bodies — are now fully covered by the `extract` + `{{inject}}` DSL. Try those first. If you can express it declaratively, do so. `ITestProcessor` may be removed or deprecated in a future version.
>
> Only use `ITestProcessor` when you have exhausted the declarative options and genuinely need C# — request signing, writing to an external system, conditional mutations based on runtime state, or side effects that have no DSL equivalent. See [variable-extraction-and-injection.md](variable-extraction-and-injection.md) to confirm `extract` and `{{inject}}` cannot cover your case before proceeding.

`ITestProcessor` lets you run code immediately before a request is sent and immediately after a response is received.

### Interfaces

```csharp
public interface ITestProcessor
{
    void Before(TestApi testApi);
    void After(TestApi testApi, JToken actualResponse);
}

public interface ITestProcessorFactory
{
    ITestProcessor GetTestProcessor(string testName);
}
```

### `Before(TestApi testApi)`

Called after variable injection but before the HTTP request is sent. Mutations to `testApi` — path, body, headers — take effect on the outgoing request. By the time `Before` fires, any `{{variables}}` in the test definition have already been substituted; your mutations layer on top.

**Example — computing a request signature:**

```csharp
public class SignedRequestProcessor : ITestProcessor
{
    public void Before(TestApi testApi)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(testApi.Request.Body, timestamp);
        testApi.Request.Headers["X-Timestamp"] = timestamp;
        testApi.Request.Headers["X-Signature"] = signature;
    }

    public void After(TestApi testApi, JToken actualResponse) { }
}
```

### `After(TestApi testApi, JToken actualResponse)`

Called after the HTTP response is received, before assertions run. `actualResponse` is the parsed JSON body. Use `After` for side effects: writing response data to a shared store, triggering an audit log, or capturing state for diagnostics. It fires regardless of whether the test will pass or fail — do not treat it as a success callback.

### Factory pattern

`ITestProcessorFactory.GetTestProcessor(string testName)` is called once per test. Return the appropriate processor for that test name, or `null` if the test needs no processor.

**Example factory:**

```csharp
public class TestProcessorFactory : ITestProcessorFactory
{
    private readonly SuiteConfig _config;

    public TestProcessorFactory(SuiteConfig config) => _config = config;

    public ITestProcessor GetTestProcessor(string testName) => testName switch
    {
        "ShouldCreateASignedOrder" => new SignedRequestProcessor(),
        "ShouldAuditUserCreation"  => new AuditWriterProcessor(_config),
        _                          => null
    };
}
```

Pass the factory through the fixture and into `BaseTest`:

```csharp
// In fixture setup:
ProcessorFactory = new TestProcessorFactory(SuiteConfig);

// In test class constructor:
: base(fixture.TestHttpClient, fixture.SuiteConfig, fixture.ProcessorFactory,
       new TestOutputLogger(output), fixture.Filter, fixture.ResultCollector)
```

If no test in a suite needs a processor, pass `null` — this is the common case and both example suites do exactly that.

---

## Domain-specific matchers — `SuiteConfig.CustomMatchers`

When the same domain-specific assertion recurs across many tests, register it as a named matcher via `SuiteConfig.CustomMatchers`. Once registered, it is available by name in the `semantic` block of any test in that suite, exactly like built-in matchers.

**`SemanticMatcherFunc` signature:**

```csharp
// Return null on success; return a failure message string on failure
public delegate string? SemanticMatcherFunc(JToken value, string? parameter);
```

The second argument is the parameter parsed from `name(param)` syntax in the DSL — it is `null` for plain `name` usage.

**Example — validating a proprietary domain ID format (`DOM-{n}`):**

```csharp
SuiteConfig = new SuiteConfig
{
    ApiServerUrl = "http://localhost:5170",
    CustomMatchers = new Dictionary<string, SemanticMatcherFunc>
    {
        ["isDomainId"] = (token, _) =>
            token.Value<string>()?.StartsWith("DOM-") == true
                ? null
                : $"Expected domain ID starting with 'DOM-' but got: {token}"
    }
};
```

**Usage in the DSL:**

```json
"response": {
  "statusCode": 201,
  "body": {},
  "matcher": {
    "semantic": {
      "domainRef": "isDomainId"
    }
  }
}
```

**Constraints:**

- Custom matcher names must not conflict with built-in names. An attempt throws `ArgumentException` before any HTTP call.
- Custom matchers are scoped to the suite where they are registered — they are not shared across suites.
- The `parameter` argument is `null` when the DSL uses plain `"isDomainId"` syntax. It receives the parsed string when the DSL uses `"isDomainId(someParam)"`.

For the full matcher catalogue and the built-in names that are reserved, see [matchers-and-patterns.md](matchers-and-patterns.md).
