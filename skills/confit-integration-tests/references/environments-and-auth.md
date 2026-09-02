# Environments and Auth

The two things a black-box suite must settle before any test is written. Depth:
`<root>/doc/auth-profiles.md` and `<root>/doc/suite-setup.md`.

---

## Environments

Named blocks under `integration:`, each carrying its own `api`, `auth`, `folders` and `filter`:

```yaml
integration:
  default: local
  local:
    api: { url: http://localhost:<your port> }
    filter: { strategy: tags, envVariable: TEST_TAGS }
  qa:
    api:  { url: ${QA_API_URL} }
    auth: { type: bearer, token: ${QA_API_TOKEN} }
    filter: { strategy: tags, envVariable: TEST_TAGS }
```

**Selection order**, highest first:

1. the `environment` argument to `SuiteBootstrapper.ForIntegration(configFile, environment)`
2. the `TEST_ENVIRONMENT` process variable
3. the `default:` key
4. none of the above → the suite fails to load

```bash
TEST_ENVIRONMENT=qa TEST_TAGS=smoke dotnet test
```

An unknown name fails loudly (`No environment 'staging' found in the integration section`), and a
hardcoded `environment:` argument in the fixture beats `TEST_ENVIRONMENT`, which makes CI unable
to switch — prefer leaving it unset.

Only the **active** environment's block is parsed. A `${VAR}` in an inactive block is never
resolved, so unused environments cannot break a run.

## Auth profiles

| `type` | Required | Optional |
|---|---|---|
| `bearer` | `token` | `headerKey` (default `Authorization`) |
| `oauth2-client-credentials` | `tokenUrl`, `clientId`, `clientSecret` | `scope`, `headerKey` |
| `api-key` | `headerKey`, `value` | — |

ConfIT adds the `Bearer ` prefix for bearer and OAuth2; do not include it in the value.

### Two constraints that shape what you can test

**Auth is suite-level.** The provider is asked for a token before every request, and there is no
per-test override in the DSL.

**A per-test `Authorization` header does not replace it — both are sent.** Headers declared on a
test are added first, then the provider's header is appended to the same header's value list:

```
Authorization: Bearer per-test-bad-token, Bearer suite-level-token
```

So **you cannot write a 401 test by putting a bad token on one test** while the suite has auth
configured. The server sees two credentials and will behave unpredictably. To test rejection
paths, use an environment block with **no `auth:`**, and run those tests against it:

```yaml
  qa-noauth:
    api: { url: ${QA_API_URL} }
    filter: { strategy: tags, envVariable: TEST_TAGS }
```

```bash
TEST_ENVIRONMENT=qa-noauth TEST_TAGS=auth dotnet test
```

Now a test in that run genuinely sends no credential, or exactly the one it declares.

### OAuth2: fetched once, never refreshed

The token request is a single blocking `POST` in the **provider's constructor** — at suite
startup, before the first test. The `access_token` is cached for the entire run.

- **There is no refresh and no expiry handling.** A suite that runs longer than the token's TTL
  starts returning 401s partway through, and ConfIT will not recover. For a long QA suite, either
  keep the run short, or supply a custom `IAuthTokenProvider` that refreshes.
- **A token failure fails the whole suite, not one test.** An unreachable endpoint, a non-2xx
  response, or a missing `access_token` field throws before any test runs, naming the URL and
  status.

### Anything else

Request signing, rotating credentials, per-tenant tokens: implement `IAuthTokenProvider`
(`HeaderKey()` + `Token()`) and wire it through the adapter-chain setup. `Token()` is called per
request, so it can vary. See `<root>/doc/extending-confit.md`.

## Auth tests worth writing

Given the constraint above, split them by run:

| Test | Where |
|---|---|
| happy path carries a valid credential | the normal authenticated run — implicit in every test |
| no credential → `401` | an environment block with no `auth:` |
| malformed or expired credential → `401` | same, with the bad token declared on the test |
| valid credential, insufficient scope or role → `403` | an environment block whose `auth:` uses the lesser-privileged credential |
| wrong tenant or another user's resource → `403`/`404` | the normal run, requesting a resource the credential should not reach |

The last one is the highest-value and most often missed: it needs no special environment, only a
resource identifier the token should not be able to see.

## Secrets

Every credential comes from `${ENV_VAR}` — never a literal in a committed file.

Two interpolation mechanisms exist, and they behave differently:

| | In `suite.config.yaml` | In test definitions |
|---|---|---|
| Applies to | every string in the active section | `path`, `params`, `headers`, `body`, expected bodies |
| Name pattern | uppercase and underscore only | permissive |
| Fails | at suite load, naming the field | when that test runs |

Both fail loudly; neither substitutes an empty string. `${ENV}` and `{{extracted}}` are separate
namespaces — the first is the process environment at load, the second is data captured from an
earlier response.
