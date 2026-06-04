---
feature: Declarative Auth Profiles
epic: Environment Setup
status: approved
priority: P1
depends_on:
  - ENV-004
personas:
  - integration-test-author
  - platform-engineer
source_docs: []
---

# Declarative Auth Profiles

## Problem Statement

Test suite authors must implement `IAuthTokenProvider` in C# to inject auth headers, even when auth is a static Bearer token, an env-var-backed secret, or an OAuth2 client credentials flow — the cases that cover the overwhelming majority of real APIs. This forces boilerplate C# infrastructure on every team before a single test can run against a protected endpoint. The `suite.config.yaml` introduced by ENV-004 already owns suite wiring; auth configuration belongs there too.

## User / Personas

**Integration test author** — runs tests against a real API that requires authentication. Wants to declare a Bearer token or OAuth2 config in `suite.config.yaml` without writing a C# provider class.

**Platform/infra engineer** — standardises test setup across teams. Auth should live in config files that can be templated, linted, and diffed — not scattered across custom C# provider implementations.

## Scope

**In scope:**
- `auth:` block in `suite.config.yaml` under both `component` and `integration` sections
- `type: bearer` — static token value or `${ENV_VAR}` reference; formats `Authorization: Bearer {token}`
- `type: oauth2-client-credentials` — token endpoint URL, client ID, client secret, optional scope; token fetched once at suite startup
- `type: api-key` — key value with a `header:` name (custom request header) or `param:` name (query parameter)
- `ToAuthTokenProvider()` extension method on the loaded config object; wires the resolved provider into `TestHttpClient.Create`
- Clear startup errors when required auth fields are missing or `${ENV_VAR}` references are unresolved
- Dedicated Auth Profiles section in `doc/suite-setup.md` covering all three auth types and migration from `IAuthTokenProvider`

**Out of scope:**
- Per-test or per-request auth override — auth is suite-level only
- OAuth2 authorization code or device flows — browser redirect required; not automatable in this model
- OAuth2 token refresh during a suite run — token is fetched once at startup, no refresh loop
- mTLS, NTLM, Kerberos, or Windows integrated auth
- `IAuthTokenProvider` interface changed or removed — it remains the underlying contract; config-driven auth generates an implementation internally
- Central auth profile registry shared across multiple suite config files
- Warning when a literal token value (not an env var reference) is committed to source control

## Boundary Conditions

- OAuth2 token is fetched once at provider instantiation (suite startup), not per-request.
- If the OAuth2 token endpoint is unreachable or returns a non-2xx response, suite startup throws before any test runs, naming the endpoint URL and the HTTP status received.
- Unresolved `${ENV_VAR}` references in auth fields are caught during `LoadComponent` / `LoadIntegration` — same point and same error format as all other env var references in the config.
- If both a config `auth:` block and an explicit `IAuthTokenProvider` are supplied by the fixture, the explicit C# provider wins — explicit code takes precedence over implicit config.
- `scope` in OAuth2 config is optional; if omitted, no `scope` parameter is included in the token request body.

## Assumptions

- Bearer token provider formats the full header value as `Bearer {token}` — matching what `TestHttpClient` currently expects from `IAuthTokenProvider.Token()`.
- OAuth2 token response uses the standard `access_token` field from the JSON response body.
- `IAuthTokenProvider` contract is unchanged — all three auth types implement it internally as non-public classes.
- `${ENV_VAR}` interpolation for auth values reuses the same resolver introduced by ENV-004.
- The `auth:` block is valid in both `component` and `integration` sections; in practice it is most commonly used in the `integration` section.

## Scenarios

### Scenario 1: Bearer token declared in config

A developer authenticates integration tests against an API using a token stored as an env var — no C# provider class needed.

**Acceptance Criteria:**
- Given a `suite.config.yaml` with `auth: { type: bearer, token: "${API_TOKEN}" }` in the active section
- And `API_TOKEN=secret-value` is set in the environment
- When `LoadComponent` or `LoadIntegration` is called
- Then the returned config provides an auth provider whose `Token()` returns `Bearer secret-value`
- And `TestHttpClient` sends `Authorization: Bearer secret-value` on every request during the suite run
- And a static literal token value (no env var reference) is also accepted and used as-is

### Scenario 2: OAuth2 client credentials declared in config

A developer authenticates against an API using OAuth2 client credentials without writing a token-fetching provider.

**Acceptance Criteria:**
- Given `auth: { type: oauth2-client-credentials, tokenUrl: "https://auth.example.com/token", clientId: "${CLIENT_ID}", clientSecret: "${CLIENT_SECRET}", scope: "api:read" }`
- And `CLIENT_ID` and `CLIENT_SECRET` are set in the environment
- When the suite starts and the auth provider is instantiated
- Then a POST is sent to `tokenUrl` with `grant_type=client_credentials`, the resolved `client_id`, `client_secret`, and `scope` in the request body
- And the `access_token` field from the JSON response is used as the Bearer token for all subsequent test requests
- And the token is fetched exactly once at provider instantiation — not per-request
- And when `scope` is omitted from the config, no `scope` parameter is sent in the token request

### Scenario 3: API key declared in config

A developer authenticates using a custom header API key.

**Acceptance Criteria:**
- Given `auth: { type: api-key, header: "X-API-Key", value: "${API_KEY}" }` in the active section
- And `API_KEY` is set in the environment
- When tests run
- Then every request carries `X-API-Key: {resolved value}` as a request header
- And no `Authorization` header is added by the auth provider

### Scenario 4: Auth configuration fails at suite startup

Auth misconfiguration is surfaced before any test runs, not mid-suite.

**Acceptance Criteria:**
- Given `auth: { type: bearer, token: "${MISSING_TOKEN}" }` and `MISSING_TOKEN` is not set in the environment
- When `LoadComponent` or `LoadIntegration` is called
- Then an exception is thrown before any test runs, naming `MISSING_TOKEN` and the config field it appeared in
- Given a valid OAuth2 auth config where the token endpoint returns a non-2xx HTTP response
- When the suite starts and the provider attempts to fetch a token
- Then an exception is thrown before any test runs, naming the token endpoint URL and the HTTP status code received

### Scenario 5: Documentation published for all auth types

Feature is not shipped until the documentation exists, covers all three auth types, and guides migration from the C# provider approach.

**Acceptance Criteria:**
- Given ENV-001 is implemented
- When a developer reads `doc/suite-setup.md`
- Then it contains a dedicated Auth Profiles section with working `suite.config.yaml` examples for `bearer`, `oauth2-client-credentials`, and `api-key`
- And every sensitive value in examples uses `${ENV_VAR}` references, not literal secrets
- And a migration sub-section shows how to replace an existing `IAuthTokenProvider` C# class with the equivalent declarative config for each auth type
- And the expected error messages for missing env vars and unreachable token endpoints are documented

*(Scenarios ordered chronologically — natural implementation sequence.)*

## Implementation Notes

1. **`AuthConfig` DTO hierarchy** — `AuthType` enum (Bearer, OAuth2ClientCredentials, ApiKey); type-specific config classes (`BearerAuthConfig`, `OAuth2AuthConfig` with tokenUrl/clientId/clientSecret/scope, `ApiKeyAuthConfig` with header/value); parse `auth:` block in `SuiteConfiguration.LoadComponent` and `LoadIntegration` alongside existing fields.

2. **Bearer and API key providers** — internal `BearerAuthTokenProvider` wraps a resolved token string, returns `Bearer {token}` from `Token()`; `ApiKeyAuthTokenProvider` injects into a named header, returns empty string from `Token()` and adds the header directly through a new injection point.

3. **OAuth2 client credentials provider** — internal `OAuth2ClientCredentialsProvider` POSTs to token endpoint at instantiation using `HttpClient`, deserializes `access_token` from JSON response, caches value; `Token()` returns `Bearer {access_token}`; throws on non-2xx with URL and status in message.

4. **`SuiteConfiguration` wiring** — `ToAuthTokenProvider()` extension method on loaded config constructs the correct internal provider based on `auth.type`; env var resolution for all auth fields reuses existing resolver; explicit `null` returned when no `auth:` block declared (preserves existing behavior where no provider is set).

5. **Examples and documentation** — add `auth:` block demonstrating bearer type to `example/User.IntegrationTests/suite.config.yaml`; write Auth Profiles section in `doc/suite-setup.md` with examples for all three auth types, `${ENV_VAR}` usage, and `IAuthTokenProvider` migration guide.

## Open Questions

*(none)*

## Links

- Design: [env-001-declarative-auth-profiles.md](../../context/env-001-declarative-auth-profiles.md)
- Epic index: [index.md](../index.md)
