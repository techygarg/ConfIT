---
name: confit-integration-tests
description: This skill should be used when someone asks to "write API tests for our deployed service", "here's our OpenAPI/Swagger spec, write tests", "test the staging/QA environment", "write integration tests", "convert this Postman collection to ConfIT", "add end-to-end API tests", "point the suite at a different environment", or when adding or editing test definitions in a ConfIT suite whose config has an `integration:` section. Black box: works from a spec, a collection or a live endpoint plus a token, assuming no access to the service's source code. For tests written against your own code with mocked dependencies, use confit-component-tests instead.
---

# ConfIT Integration Tests

Black-box API tests against a service that is already running — a deployed environment, or one
brought up by CI. **Assume no access to the service's source, and that this test project may live
in a different repository entirely.** Nothing is mocked.

Setting up the suite itself is a different job — use `confit-suite-setup`.

## Step 0 — Resolve the reference, then confirm the mode

```bash
bash <this skill>/scripts/reference-path.sh
```

It prints the ConfIT repository root, `<root>` — this skill's own reference material, not the
service under test. If it exits non-zero, stop: the install is broken.

Then open the target project's `suite.config.yaml`:

- Top-level key is **`integration:`** → continue here. Note which environments exist and which is
  active (`default:`, or `TEST_ENVIRONMENT`).
- Top-level key is **`component:`** → wrong skill. That suite mocks its dependencies; hand over to
  `confit-component-tests`.
- **No suite at all** → hand over to `confit-suite-setup`.

Read one or two neighbouring files in the test-case folder to match the house style.

## Step 1 — Establish the contract

Any of these works. Later rows are often *better* than a spec, because they carry values someone
already proved against the real service.

| Source | What to take from it |
|---|---|
| OpenAPI / Swagger | operations, parameters, request and response schemas, documented status codes. Schemas are shapes — invent realistic values, and prefer any `example` the document supplies |
| Postman collection | real request bodies, headers and auth that are known to work; often the fastest path to a correct test |
| `.http` / `.rest` files | the same, and usually already in a repository you can read |
| curl examples | a captured response is the most reliable expected body there is |
| live probe | when nothing else exists, call the endpoint and build the expectation from what actually comes back |

Converting an existing Postman or `.http` suite is a first-class path: same target format, same
matchers, same grouping rules. Take the requests as given, then decide the assertions.

**Never invent endpoints, fields or status codes the source does not show.** Ask, or probe.

## Step 2 — Settle the environment and auth

Before any test: which environment is this suite pointed at, and how does it authenticate?
Environments are named blocks under `integration:`, selected by the `ForIntegration` argument,
then `TEST_ENVIRONMENT`, then `default:`. Auth is a declarative `auth:` block per environment —
bearer, OAuth2 client credentials, or API key — and every secret comes from `${ENV_VAR}`.

Two things that bite in this mode specifically: auth is **suite-level**, applied to every request
with no per-test override; and an OAuth2 token is fetched **once at startup and never refreshed**.
Details, and the auth-focused tests worth writing: `references/environments-and-auth.md`.

## Step 3 — Propose the matrix, confirm once

Default depth, when the user does not say otherwise: **the happy path for each operation, plus
every non-2xx the source documents.** Roughly one test per operation plus its documented errors.

Offer deeper coverage — pagination, array shapes, boundary values, auth edge cases — as a
follow-up rather than generating it unasked.

**Say what is not achievable here.** A dependency returning `503`, a timeout, a partial outage —
none of these can be forced black box. If the user wants them, that is a component test; hand over.

Show the list and get one confirmation before writing files.

## Step 4 — Write

Field reference, matcher decision order and the example index:
`references/writing-tests.md`.

Two instincts that are the opposite of the component mode's:

- **You do not control the data**, so lean on `semantic` and `ignore`. Assert what the contract
  guarantees — types, formats, ranges, presence — not values a shared environment happens to hold
  today. Exact counts (`hasLength(3)`) are usually wrong against live data.
- **State persists between runs.** Anything the suite creates is still there next time. Make
  created data unique per run and identifiable, or the second run collides with the first.
  `references/state-and-data.md` has the recipe, which needs no C#.

## Step 5 — Verify

```bash
python3 <root>/tools/check-testcases.py <path to test project>
```

Catches a response with no expected body, `depends:` naming an unknown or later test,
unresolvable `{{variables}}`, unknown or malformed matcher names, `mock:` blocks that do not
belong in an integration suite, and unregistered test files.

Then run it against the environment — twice. **A suite that passes once and fails the second time
is the characteristic integration-suite bug**, and it means data is not unique per run.

Every test file needs a `<None Update>` entry with
`<CopyToOutputDirectory>Always</CopyToOutputDirectory>`, or it will not exist at runtime.

## What ConfIT does not do for you

Do not design tests around features that are not there. There are no cleanup hooks, no retry or
polling for eventual consistency, no per-request timeout configuration, no unique-data helpers,
no read-only guard, and no latency assertions. The workarounds that do work — and the ones that
do not exist at all — are in `references/state-and-data.md`.

## Additional resources

- **`references/environments-and-auth.md`** — environment selection and precedence, the three auth
  profiles, the OAuth2 refresh caveat, `${ENV_VAR}` secrets, auth tests worth writing.
- **`references/state-and-data.md`** — shared persistent state, per-run unique data, cleanup,
  the verified list of what the library does not support.
- **`references/writing-tests.md`** — DSL field table, matcher decision order, example index,
  diagnosing failures.
- **`<root>/doc/`** — `test-file-format.md`, `matchers-and-patterns.md`, `auth-profiles.md`,
  `test-filtering.md` for depth.
