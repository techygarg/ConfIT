---
feature: Declarative Auth Profiles
requirement_doc: .lattice/requirements/features/env-001-declarative-auth-profiles.md
created: 2026-06-04
---

# Declarative Auth Profiles

> Declares bearer, OAuth2, and API-key auth in `suite.config.yaml`; ConfIT resolves an `IAuthTokenProvider` internally — no C# boilerplate needed for the common cases.

## Decisions Log

<!-- Add new at bottom. Never remove. -->

| Date | Decision | Reasoning | Alternatives Considered |
|------|----------|-----------|------------------------|
| 2026-06-04 | `IAuthTokenProvider` extended with `string HeaderKey() => "Authorization"` default interface method. Design approved at Level 4. Blueprint complete — ready for implementation. | Single uniform path in `TestHttpClient` for all auth types — bearer/OAuth2 use default, API-key overrides with the configured header name. Default impl means existing custom providers need no change. | (A) Internal `IAuthHeadersProvider` second interface with runtime `is` check — adds indirection, two code paths; (B) Custom token format string encoding header+value — brittle coupling |
| 2026-06-04 | `headerKey:` optional on all types (defaults to `"Authorization"`), required on api-key | Consistent single field across all three types. For api-key `headerKey` is the header name (required); for bearer/oauth2 it overrides the default. `api-key` uses `headerKey` + `value` — no separate `header:` field needed. | `header:` only on api-key, `headerKey:` only on bearer/oauth2 — two names for the same concept, inconsistent |

## Design: Level 1 — Capabilities

1. Declare auth in `suite.config.yaml` — three types (`bearer`, `oauth2-client-credentials`, `api-key`); sensitive values via `${ENV_VAR}`.
2. Auth provider resolved at suite startup — `ToAuthTokenProvider()` returns `IAuthTokenProvider`; misconfiguration throws before first test.
3. OAuth2 token fetched once — single POST at provider construction; `access_token` cached for the suite run.
4. Explicit C# provider always wins — config-driven auth ignored when fixture supplies its own provider.
5. Documentation ships with the feature — Auth Profiles section in `doc/suite-setup.md` with examples and migration guide.

## Design: Level 2 — Components

| Component | Location | Responsibility |
|---|---|---|
| `IAuthTokenProvider` | `Contract/IAuthTokenProvider.cs` | Extended with `string HeaderKey() => "Authorization"` default method |
| `AuthConfig` DTO | `Config/DTO.cs` | Auth type + per-type fields; added to `ComponentConfig` and `IntegrationEnvironmentConfig`; `ApiConfig.AuthToken` removed (dead code) |
| Auth provider implementations | `Config/AuthProviders.cs` (new) | 3 internal sealed classes — `BearerAuthTokenProvider`, `ApiKeyAuthTokenProvider`, `OAuth2ClientCredentialsProvider` |
| `ToAuthTokenProvider()` extension | `Extension/SuiteConfigurationExtensions.cs` | Factory: reads `AuthConfig?`, builds correct provider; returns `null` when no auth block |
| `TestHttpClient` | `Server/Http/TestHttpClient.cs` | Uses `provider.HeaderKey()` + `provider.Token()` — replaces hardcoded `"Authorization"` |

Validation for auth fields added to `Config/SuiteConfiguration.cs` (not a new component).

## Design: Level 3 — Interactions

**Flow A — Suite startup (config → provider)**

1. `SuiteConfiguration.Load*` parses YAML, expands `${ENV_VAR}` in all auth fields, deserializes `AuthConfig?` into the config DTO, validates auth fields (type-specific required fields).
2. Consumer calls `cfg.ToAuthTokenProvider()` — extension method switches on `auth.type`, constructs the correct internal provider.
3. `OAuth2ClientCredentialsProvider` constructor POSTs to `tokenUrl` with form-encoded credentials, caches `access_token`; throws `InvalidOperationException` naming URL + HTTP status on non-2xx.
4. `null` returned when no `auth:` block declared — preserves existing no-auth behavior.

**Flow B — Per request (provider → HTTP header)**

1. `TestHttpClient.AddRequestHeaders` calls `_tokenProvider.Token()` and `_tokenProvider.HeaderKey()`.
2. Header added as `_client.DefaultRequestHeaders.Add(HeaderKey(), Token())`.
3. Bearer/OAuth2 produce `Authorization: Bearer {token}`; API key produces `{headerKey}: {value}`; null provider adds nothing.

**Key data flows**

- `SuiteConfiguration` → config DTOs: `AuthConfig?` with all env vars already resolved.
- `SuiteConfigurationExtensions` → `OAuth2ClientCredentialsProvider`: tokenUrl, clientId, clientSecret, scope?, headerKey?.
- `OAuth2ClientCredentialsProvider` → token endpoint: POST `grant_type=client_credentials` form body.
- Token endpoint → provider: JSON `{ "access_token": "..." }`.
- Any provider → `TestHttpClient`: `HeaderKey()` (header name) + `Token()` (header value).

**No new libraries required** — `HttpClient`, `FormUrlEncodedContent`, `Newtonsoft.Json`, `YamlDotNet` all already present.

## Design: Level 4 — Contracts

**`Contract/IAuthTokenProvider.cs`**
```csharp
public interface IAuthTokenProvider
{
    string HeaderKey() => "Authorization";
    string Token();
}
```

**`Config/DTO.cs` — additions**
```csharp
public sealed class AuthConfig
{
    public string? Type         { get; set; }  // "bearer" | "oauth2-client-credentials" | "api-key"
    public string? HeaderKey    { get; set; }  // optional for bearer/oauth2, required for api-key
    public string? Token        { get; set; }  // bearer
    public string? TokenUrl     { get; set; }  // oauth2
    public string? ClientId     { get; set; }  // oauth2
    public string? ClientSecret { get; set; }  // oauth2
    public string? Scope        { get; set; }  // oauth2, optional
    public string? Value        { get; set; }  // api-key
}
// ComponentConfig gains: public AuthConfig? Auth { get; set; }
// IntegrationEnvironmentConfig gains: public AuthConfig? Auth { get; set; }
// ApiConfig.AuthToken removed (dead code, superseded by AuthConfig)
```

**`Config/AuthProviders.cs` (new, all internal)**
```csharp
internal sealed class BearerAuthTokenProvider : IAuthTokenProvider
internal sealed class ApiKeyAuthTokenProvider : IAuthTokenProvider
internal sealed class OAuth2ClientCredentialsProvider : IAuthTokenProvider
// OAuth2 ctor: FetchToken() POSTs to tokenUrl, caches access_token
// throws InvalidOperationException("OAuth2 token request to '{url}' failed with {status}") on non-2xx
```

**`Extension/SuiteConfigurationExtensions.cs`**
```csharp
public static IAuthTokenProvider? ToAuthTokenProvider(this ComponentConfig config);
public static IAuthTokenProvider? ToAuthTokenProvider(this IntegrationEnvironmentConfig config);
// null when config.Auth is null; throws on unknown type
```

**`Config/SuiteConfiguration.cs`**
```csharp
private static void ValidateAuth(AuthConfig? auth, string section, string filePath);
// bearer: token required; oauth2: tokenUrl+clientId+clientSecret required; api-key: headerKey+value required
```

**`Server/Http/TestHttpClient.cs`**
```csharp
// AddRequestHeaders: "Authorization" → _tokenProvider!.HeaderKey()
```

## Design Summary

**Components and layer assignments**
- `IAuthTokenProvider` — Contract layer; gains `HeaderKey()` default method
- `AuthConfig` DTO — Config layer; new class in `DTO.cs`; added to `ComponentConfig` and `IntegrationEnvironmentConfig`
- `AuthProviders.cs` — Config layer; 3 internal sealed providers
- `ToAuthTokenProvider()` — Extension layer; factory in `SuiteConfigurationExtensions.cs`
- `TestHttpClient` — Server/Http layer; one-line change to use `HeaderKey()`

**Key contracts**
- `IAuthTokenProvider.HeaderKey()` defaults to `"Authorization"` — existing custom providers need no change
- `AuthConfig.HeaderKey` optional for bearer/oauth2 (falls back to `"Authorization"`), required for api-key
- `ToAuthTokenProvider()` returns `null` when no `auth:` block — preserves existing no-auth behavior
- `OAuth2ClientCredentialsProvider` throws at construction on non-2xx — failure surfaces before first test

**Architectural constraints**
- `IAuthTokenProvider` public interface unchanged in signature — `HeaderKey()` added with default impl (non-breaking)
- `ApiConfig.AuthToken` removed — was dead code, never read by any extension method
- No new NuGet dependencies — `HttpClient`, `FormUrlEncodedContent`, `Newtonsoft.Json` all BCL or existing deps
- `EnvironmentKeys.cs` unchanged — no new ConfIT-defined env var names in this feature

**Design status: Approved — ready for implementation**

## Open Questions

## Constraints

## Key Files

| Path | Role |
|---|---|
| `src/ConfIT/Contract/IAuthTokenProvider.cs` | Contract layer — `HeaderKey()` default method added |
| `src/ConfIT/Config/DTO.cs` | `AuthConfig` DTO; `Auth` added to `ComponentConfig` and `IntegrationEnvironmentConfig`; `ApiConfig.AuthToken` removed |
| `src/ConfIT/Config/AuthProviders.cs` | Config layer — 3 internal providers: `BearerAuthTokenProvider`, `ApiKeyAuthTokenProvider`, `OAuth2ClientCredentialsProvider` |
| `src/ConfIT/Config/SuiteConfiguration.cs` | `ValidateAuth` added; called from `ValidateComponent` and `ValidateIntegrationEnv` |
| `src/ConfIT/Extension/SuiteConfigurationExtensions.cs` | `ToAuthTokenProvider()` on both config types; `BuildAuthProvider` factory |
| `src/ConfIT/Server/Http/TestHttpClient.cs` | `AddRequestHeaders` uses `_tokenProvider!.HeaderKey()` instead of hardcoded `"Authorization"` |
| `test/ConfIT.UnitTest/Config/AuthProvidersTests.cs` | Unit tests for all 3 providers |
| `test/ConfIT.UnitTest/Config/SuiteConfigurationTests.cs` | Auth validation + `ToAuthTokenProvider` tests added; `AuthToken` test replaced |
| `test/ConfIT.UnitTest/Server/Http/HttpClientTests.cs` | `HeaderKey()` mock setup added; API key header test added |
| `example/User.IntegrationTests/suite.config.yaml` | `qa` block uses `auth: { type: bearer, token: ... }` |
| `example/User.IntegrationTests/TestSuiteFixture.cs` | Uses `cfg.ToAuthTokenProvider()` |
| `doc/suite-setup.md` | Auth Profiles section added; integration fixture example updated |
| `doc/extending-confit.md` | `IAuthTokenProvider` section updated with `HeaderKey()` and pointer to declarative path |
