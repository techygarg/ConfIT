# Auth Profiles

ConfIT supports three declarative auth types — bearer token, OAuth2 client credentials, and API key — configured entirely in `suite.config.yaml`. No C# required for the common cases.

---

## How It Works

Auth is declared as an `auth:` block in the `component` or `integration` environment section of `suite.config.yaml`. `SuiteBootstrapper` (or `SuiteConfiguration.Load*` in the adapter-chain path) reads the block, resolves `${ENV_VAR}` references, validates required fields, and constructs the appropriate internal provider. That provider is wired into `TestHttpClient` — every HTTP request the suite sends automatically carries the configured header and value.

Under the hood, all three auth types implement the `IAuthTokenProvider` interface:

```csharp
public interface IAuthTokenProvider
{
    string HeaderKey() => "Authorization";   // which header; defaults to "Authorization"
    string Token();                           // full header value (e.g. "Bearer xyz")
}
```

`TestHttpClient` calls `HeaderKey()` and `Token()` before each request and adds the result as a request header. Auth is suite-level and applies to every request without any per-test configuration.

---

## Declaring Auth in `suite.config.yaml`

The `auth:` block sits alongside `api:`, `mock:`, `folders:`, and `filter:` in either the `component` section or an integration environment block:

```yaml
# Component suite
component:
  api:
    url: http://localhost:5170
  auth:
    type: bearer
    token: ${API_TOKEN}
  mock:
    url: http://localhost:8888

# Integration suite — per environment
integration:
  default: local
  local:
    api:
      url: http://localhost:5170
  qa:
    api:
      url: https://api.qa.example.com
    auth:
      type: bearer
      token: ${QA_API_TOKEN}
```

Always use `${ENV_VAR}` for sensitive values — secrets never belong in a committed file.

---

## Auth Types

### Bearer Token

```yaml
auth:
  type: bearer
  token: ${API_TOKEN}
```

Every request carries `Authorization: Bearer <resolved token>`. The token value is whatever `${API_TOKEN}` resolves to at load time — no `Bearer` prefix needed in the config, ConfIT adds it.

A static literal value is also accepted (e.g., for local development), though using an env var is strongly preferred.

---

### OAuth2 Client Credentials

```yaml
auth:
  type: oauth2-client-credentials
  tokenUrl: "https://auth.example.com/oauth/token"
  clientId: ${CLIENT_ID}
  clientSecret: ${CLIENT_SECRET}
  scope: "api:read"            # optional
```

ConfIT sends a single `POST` to `tokenUrl` at provider construction time (suite startup) with a `grant_type=client_credentials` form body. The `access_token` field from the JSON response is cached and sent as `Authorization: Bearer <token>` on every subsequent request.

**Required fields**: `tokenUrl`, `clientId`, `clientSecret`. `scope` is optional — if omitted, no `scope` parameter is included in the token request.

**Startup failure**: if the token endpoint returns a non-2xx response or is unreachable, the suite fails before the first test runs with a message naming the endpoint URL and HTTP status.

---

### API Key

```yaml
auth:
  type: api-key
  headerKey: X-API-Key         # which header to set (required)
  value: ${API_KEY}
```

Every request carries `X-API-Key: <resolved value>`. The `headerKey` field controls which header is used — it is required for this auth type.

---

## Custom Header Key

All auth types accept an optional `headerKey:` field that overrides the default `Authorization` header. Bearer and OAuth2 default to `Authorization`; use this for non-standard APIs that expect the token in a different header:

```yaml
auth:
  type: bearer
  token: ${API_TOKEN}
  headerKey: X-Authorization
```

```yaml
auth:
  type: oauth2-client-credentials
  tokenUrl: "https://auth.example.com/token"
  clientId: ${CLIENT_ID}
  clientSecret: ${CLIENT_SECRET}
  headerKey: X-Bearer-Token
```

---

## Failure Behaviour at Startup

All auth errors surface at suite startup — before the first test runs:

| Situation | Error |
|---|---|
| `${ENV_VAR}` in the auth block is not set | `InvalidDataException` naming the variable and the config field |
| `tokenUrl` is not a valid absolute URL | `InvalidOperationException` naming the URL |
| OAuth2 token endpoint returns non-2xx | `InvalidOperationException` naming the endpoint URL and HTTP status |
| OAuth2 response has no `access_token` field | `InvalidOperationException` naming the endpoint URL |
| Unknown `type` value | `InvalidDataException` listing the allowed types |
| Required field missing (e.g. `token` for bearer) | `InvalidDataException` naming the field and config file path |

---

## OAuth2 in AppLauncher (Command) Mode

### Integration tests — just configure and go

For integration tests, there is nothing extra to do. Declare the `auth:` block with your real token endpoint and credentials, and ConfIT fetches the token at startup and injects it into every request:

```yaml
integration:
  qa:
    api:
      url: https://api.qa.example.com
    auth:
      type: oauth2-client-credentials
      tokenUrl: https://auth.example.com/oauth/token
      clientId: ${CLIENT_ID}
      clientSecret: ${CLIENT_SECRET}
```

Set `CLIENT_ID` and `CLIENT_SECRET` as environment variables before running, and the fixture needs no changes beyond the one-liner:

```csharp
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, cfg.ToAuthTokenProvider());
```

That's it. ConfIT handles the token fetch, caching, and header injection automatically.

---

### Component tests (AppLauncher) — self-contained OAuth2 stub

Component tests in AppLauncher mode typically run without any external services — the app starts as a local process and its dependencies are mocked by WireMock. If your component test environment bypasses auth (which is common), you don't need an `auth:` block at all.

If you do want to exercise the OAuth2 token flow in a component test — for example, to verify ConfIT's auth machinery end-to-end or to test an app that validates incoming tokens — you need a local token endpoint. Since no real auth server is available in a self-contained component test, the pattern is to stub one with WireMock on a dedicated port.

> **The `User.ComponentTests.AppLauncher` example in this repository uses this pattern specifically to demonstrate that ConfIT's OAuth2 auth profile works correctly.** It is not a pattern most teams need in their day-to-day component tests. If your component tests don't validate tokens, skip the stub and don't add an `auth:` block.

**The startup ordering constraint:** `OAuth2ClientCredentialsProvider` fetches the token in its constructor. This means the token endpoint stub must be running *before* `cfg.ToAuthTokenProvider()` is called — otherwise the POST fails and the suite aborts at startup.

The solution is to start a dedicated WireMock server for the token endpoint (port `:8887`) before constructing the provider. The existing service-dependency mock continues to run on its own port (`:8888`), unaffected.

```csharp
public class TestSuiteFixture : IDisposable
{
    private readonly BootstrappedSuite _suite;
    private readonly WireMockServer    _oauthServer;

    public TestSuiteFixture()
    {
        // Must start before ForCommand() — OAuth2ClientCredentialsProvider fetches
        // the token eagerly in its constructor, so the stub must be ready first.
        _oauthServer = WireMockServer.Start(8887);
        _oauthServer
            .Given(Request.Create().WithPath("/oauth/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"access_token":"component-test-token","token_type":"Bearer"}""")
                .WithHeader("Content-Type", "application/json"));

        // ForCommand reads suite.config.yaml, starts the process, then builds the
        // OAuth2 provider (which POSTs to the stub above) — ordering is respected.
        _suite = SuiteBootstrapper.ForCommand("suite.config.yaml");
    }

    public TestSuiteContext Context => _suite.Context;

    public void Dispose()
    {
        _suite.Dispose();
        _oauthServer.Stop();
    }
}
```

`suite.config.yaml` points `tokenUrl` at the local stub:

```yaml
component:
  startup:
    mode: command
    command: dotnet run --no-build --project ../../../../User.Api --launch-profile ComponentTest
    readiness:
      port: 5170
  api:
    url: http://localhost:5170
  auth:
    type: oauth2-client-credentials
    tokenUrl: http://localhost:8887/oauth/token   # local WireMock stub
    clientId: component-test-client
    clientSecret: component-test-secret
  mock:
    url: http://localhost:8888                    # service-dependency mock, unchanged
```

📄 Live example: [`User.ComponentTests.AppLauncher/SetUp/TestSuiteFixture.cs`](../example/User.ComponentTests.AppLauncher/SetUp/TestSuiteFixture.cs)

---

## Verifying the Auth Header Reaches Your API

Tests pass even if the auth header is wrong or absent — most APIs only reject tokens at the authorization layer, which may not be enabled in a component test environment. To verify the header is actually being sent, add a lightweight echo endpoint to your API that returns the incoming `Authorization` header:

```csharp
// Example: User.Api/Controller/AuthController.cs
[HttpGet("verify")]
public IActionResult Verify() =>
    Ok(new { authorization = Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty });
```

Then assert it in a YAML test:

```yaml
VerifyAuthorizationHeader:
  tags:
    - oauth2
  api:
    request:
      method: GET
      path: /api/auth/verify
    response:
      statusCode: 200
      body:
        authorization: "Bearer component-test-token"
```

This is end-to-end proof: the test passes only if `TestHttpClient` sent `Authorization: Bearer component-test-token` and the API received it intact.

📄 Live example: [`User.ComponentTests.AppLauncher/TestCase/03-oauth2.yaml`](../example/User.ComponentTests.AppLauncher/TestCase/03-oauth2.yaml)

---

## Wiring in the Fixture

### Bootstrapped path (recommended)

Auth is handled automatically — declare the `auth:` block in `suite.config.yaml` and call the appropriate `SuiteBootstrapper` method. No fixture code needed:

```csharp
// Integration
_suite = SuiteBootstrapper.ForIntegration("suite.config.yaml");

// Component command mode
_suite = SuiteBootstrapper.ForCommand("suite.config.yaml");

// Component in-process mode (auth header injected on every request)
_suite = SuiteBootstrapper.ForComponent<Startup>("suite.config.yaml");
```

When no `auth:` block is present, `SuiteBootstrapper` passes `null` as the provider — requests carry no auth header.

### Adapter-chain path

When using the adapter chain directly, pass `cfg.ToAuthTokenProvider()` to `TestHttpClient.Create`:

```csharp
var cfg = SuiteConfiguration.LoadIntegration("suite.config.yaml");
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, cfg.ToAuthTokenProvider());
```

`ToAuthTokenProvider()` returns `null` when no `auth:` block is declared.

---

## Migration from `IAuthTokenProvider`

If your fixture currently constructs a custom C# auth provider:

```csharp
// Before — custom C# provider
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, new AuthTokenProvider());

// After — declarative config, no C# needed
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, cfg.ToAuthTokenProvider());
```

Add the matching `auth:` block to `suite.config.yaml` for the environment your provider was handling.

---

## Custom Auth — `IAuthTokenProvider`

The declarative types cover the most common cases. For anything they can't express, implement `IAuthTokenProvider` directly:

- **Request signing** — HMAC-SHA256, AWS Signature V4, or any per-request computed value
- **Rotating tokens** — tokens that expire mid-run and require background refresh
- **OAuth2 flows requiring a browser** — authorization code, device code flow
- **Conditional auth** — different credentials per test or per endpoint
- **Token introspection** — fetching a token and storing metadata from the response alongside it

### Interface

```csharp
public interface IAuthTokenProvider
{
    string HeaderKey() => "Authorization";   // which header to set; defaults to "Authorization"
    string Token();                           // full header value, verbatim
}
```

`HeaderKey()` has a default implementation — if you only need to supply the token value, implement `Token()` alone and the header defaults to `Authorization`.

### Implementation example

Bearer token from an environment variable — the canonical example of a case you'd handle in code before the declarative option existed:

```csharp
public class AuthTokenProvider : IAuthTokenProvider
{
    public string Token()
    {
        var token = Environment.GetEnvironmentVariable("API_TOKEN")
            ?? throw new InvalidOperationException("API_TOKEN is not set");
        return $"Bearer {token}";
    }
}
```

A signing provider that computes an HMAC signature per request:

```csharp
public class HmacAuthProvider : IAuthTokenProvider
{
    private readonly string _secret;
    public HmacAuthProvider(string secret) => _secret = secret;

    public string HeaderKey() => "X-Signature";

    public string Token()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeHmac(_secret, timestamp);
        return $"{timestamp}.{signature}";
    }
}
```

### Wiring in fixture setup

Pass the provider to `TestHttpClient.Create`:

```csharp
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, new AuthTokenProvider());
```

Pass `null` when no auth is required:

```csharp
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, null);
```

When using `suite.config.yaml`, the two approaches are interchangeable at the call site — `cfg.ToAuthTokenProvider()` returns an `IAuthTokenProvider?` that you can swap for a custom implementation without touching anything else:

```csharp
// Declarative — reads auth: block from suite.config.yaml
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, cfg.ToAuthTokenProvider());

// Custom C# — for cases the YAML types can't express
TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, new HmacAuthProvider(secret));
```
