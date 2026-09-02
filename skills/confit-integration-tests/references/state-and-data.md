# State and Test Data

The thing that breaks black-box suites. A component suite gets a fresh process and an empty
database every run; a deployed environment remembers everything the last run did.

**The characteristic bug: the suite passes the first time and fails the second.** A `CreateUser`
with a hardcoded email succeeds on run 1 and collides on a uniqueness constraint on run 2 — and
the failure looks like a bug in the service, not in the test.

---

## Make created data unique per run — no C# required

`${ENV_VAR}` interpolation resolves inside test definitions, not just in `suite.config.yaml`. Put
a run-scoped value into any created identifier:

```yaml
CreateOrder:
  tags: [orders, smoke]
  api:
    request:
      method: POST
      path: /api/order
      body:
        reference: "qa-${RUN_ID}-001"
        email: "qa-${RUN_ID}@example.com"
    response:
      statusCode: 201
      extract:
        orderId: $.body.id
      matcher:
        semantic:
          id: isNotNull
```

Set `RUN_ID` from whatever the runner already has:

```bash
RUN_ID=$GITHUB_RUN_ID dotnet test          # CI
RUN_ID=$(date +%s) dotnet test             # locally
```

An unset variable fails loudly at the point the test runs, so a forgotten export never silently
degrades into a collision.

This also makes the data **identifiable**: everything the suite created is greppable by run, which
matters when someone has to clean up a shared environment by hand later.

## Cleanup — a trailing test

There are no teardown hooks. What works instead: tests in a file run in definition order, and a
test with **no `depends:`** runs regardless of whether earlier tests failed. So a final delete is
a legitimate declarative teardown.

```yaml
# ... create / read / update tests above ...

DeleteOrder_Cleanup:
  tags: [orders, cleanup]
  api:
    request:
      method: DELETE
      path: "/api/order/{{orderId}}"
    response:
      statusCode: 200
      body: { deleted: true }
```

Caveats worth stating to the user rather than hiding:

- It needs `{{orderId}}`, which only exists if the create passed. If the create failed there is
  nothing to delete — and nothing was created, so nothing leaks.
- If the create passed but an assertion in the middle failed, this still runs. That is the point.
- If the run is killed outright, it does not run. Unique data is what limits the damage.

Where the environment offers a bulk cleanup endpoint, a single trailing call scoped to
`${RUN_ID}` beats one delete per entity.

## Read-only and shared environments

ConfIT has no read-only mode: `POST` and `DELETE` behave identically in every environment. If a
suite must be safe against a production-like target, that is a discipline in the test definitions
— tag write tests separately and run only reads there:

```bash
TEST_TAGS=readonly dotnet test
```

Make that split explicit when proposing the matrix; do not assume a QA URL is safe to write to.

## Assertions against data you do not control

Prefer what the contract guarantees over what the environment happens to hold:

| Instead of | Use |
|---|---|
| `hasLength(3)` on a collection | `isNotEmpty`, or a `greaterThan` bound |
| an exact `id` | `isUuid` / `isNotNull` / `greaterThan(0)` |
| an exact `createdAt` | `isIsoDateTime` |
| an exact total or count | `greaterThan(0)` |
| a full list body | assert the shape of one known item you created this run |

The best expected body in this mode is one built from a **response you actually captured**, then
loosened where values are volatile.

---

## What ConfIT does not support

Verified against the library. Do not design a suite around any of these.

| Capability | Supported | What to do instead |
|---|---|---|
| cleanup / teardown hooks | **no** | trailing delete test, above |
| retry / polling for eventual consistency | **no** | no honest workaround — say so; do not add sleeps to the DSL, there is nowhere to put them |
| per-request timeout config | **no** | inherits `HttpClient`'s 100-second default |
| unique / random data generation | **no** | `${RUN_ID}`, above |
| read-only guard | **no** | tag-based separation, above |
| latency assertions | **no** | durations appear in the summary but cannot be asserted |
| parallel execution controls | **no** | only "sequential within a file" is guaranteed |

Two traps:

- **`timeoutSeconds` is not a request timeout.** It exists only under `startup.readiness`, for
  AppLauncher process startup — a component-mode concern that does not apply here at all.
- **`ITestProcessor.After` is not a `finally`.** It runs before assertions, and is skipped
  entirely when the request throws or the body fails to parse, so it cannot be repurposed as
  reliable cleanup. The interface is documented as legacy and may be removed.

## The shipped example is not a template for this

`<root>/example/User.IntegrationTests` is worth reading for DSL mechanics — matchers, `depends`,
`bodyFromFile`, arrays, GraphQL. **Do not copy its data strategy.** It uses hardcoded emails
(`test@test.com`) and relies on cross-file ordering, which only works because `make integration`
runs `rm -f` on the database before every run. It is a freshly-provisioned-environment suite
wearing the "integration" label.

Against a deployed environment, that same file fails on its second run.
