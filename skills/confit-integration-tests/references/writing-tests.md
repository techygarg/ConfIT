# Writing Integration Test Definitions

Compact reference. For depth, read the `doc/` page named in each section — they are on disk at
`<root>/doc/`.

---

## The rule that catches most people

**The expected body must account for every field in the actual response.** Matchers remove the
fields they claim, then whatever is left is compared structurally. A response field that is
neither listed in `body` nor claimed by a matcher fails as `expected: <absent>`.

This bites harder in black-box mode than anywhere else: a deployed service often returns fields
the spec never mentioned. When the spec and the response disagree, **the response wins** — correct
the test against reality, and tell the user the spec is out of date.

Two consequences:

- **Omitting `body` fails the test.** Use `body: {}` when every field is claimed by a matcher.
- **An empty response body cannot be asserted.** A `204`, or any zero-length body, fails while
  being parsed as JSON. Verify such an endpoint through a follow-up request instead.

## Field reference

`doc/test-file-format.md` for the full version.

| | |
|---|---|
| `tags` | strings; matched against the filter env var. With `strategy: tags` active, an **untagged test is skipped**. Also how you separate read-only from write tests |
| `depends` | prerequisite test names — same file, defined earlier. Skips instead of failing when one did not pass |
| `mock` | **not valid here** — an integration suite has no mock server |
| `api.request` | `method`, `path`, `body`, `bodyFromFile`, `override`, `params`, `headers`, `graphql` |
| `api.response` | `statusCode`, `body`, `bodyFromFile`, `override`, `headers`, `matcher`, `extract` |
| `matcher` | `ignore` (list), `pattern` (field→regex), `semantic` (field→matcher) |
| `extract` | name → `$.body.…` / `$.headers['x-…']` / `$.statusCode`. Runs only when the test passes |
| `{{var}}` | injects an extracted value into path, body, headers, params |
| `${ENV}` | process environment — secrets, and run-scoped values like `${RUN_ID}` |

Nested paths use `__`: `address__city`. A bare key name in `ignore`/`pattern` matches at **every**
depth; a `__` path is anchored to the root.

`bodyFromFile` + `override` is worth reaching for here more than in component mode: response
bodies from a real service are large, and one fixture with per-test overrides beats repeating it.

## Choosing a matcher

The instinct is the opposite of a component suite's. **You do not control the data**, so assert
what the contract guarantees rather than what the environment holds today.

1. **Did this suite create the value, this run?** Then it is deterministic and you may assert the
   literal — that is what `${RUN_ID}`-scoped data buys you.
2. **Otherwise, does a built-in `semantic` matcher describe it?** This is the default here:
   `isUuid`, `isIsoDate`, `isIsoDateTime`, `isEmail`, `isNull`, `isNotNull`, `isEmpty`,
   `isNotEmpty`, `greaterThan(n)`, `lessThan(n)`, `hasLength(n)`, `hasLength(min,max)`.
3. **A system-specific format?** `pattern` with an anchored regex.
4. **A repeated domain assertion?** Register a custom matcher in the fixture. That is C# in the
   *test* project — which you own even with no access to the service's source.
5. **Nothing to assert?** Only then `ignore`.

Avoid exact counts on collections you did not create — `hasLength(3)` is a promise about someone
else's data. Use `isNotEmpty`, or assert the shape of one item you created this run.

`semantic` paths do **not** support wildcards or array indexes. `ignore` and `pattern` accept a
`*` segment to reach across array elements (`errors__*__path`), but never as the final segment.
Depth: `doc/matchers-and-patterns.md`.

## Chaining tests

`extract` → `{{inject}}` → `depends` go together. `extract` runs only on a passing test, so
without `depends` a dependent test fails on a missing variable and hides the real cause. All three
must be in the same file, prerequisites first — files load alphabetically, and `depends` is
file-scoped.

In this mode the chain usually starts with a create that owns its own `${RUN_ID}`-scoped data, so
the rest of the file operates on something the run definitely owns.

## Which example shows what

All under `<root>/example/`. Real, CI-verified files — read one rather than working from a
paraphrase. Read `<root>/example/README.md` first for what is demo-specific, and see
`state-and-data.md` for why the data strategy in these files does not transfer to a deployed
environment.

| Need | File |
|---|---|
| create → read chain with `extract` / `{{inject}}` / `depends` | `User.IntegrationTests/TestCase/01-user-lifecycle.yaml` |
| error responses | `User.IntegrationTests/TestCase/02-user-errors.yaml` |
| all three matcher types side by side | `User.IntegrationTests/TestCase/03-response-matchers.yaml` |
| `bodyFromFile` + `override` | `User.IntegrationTests/TestCase/04-body-fixtures.yaml` |
| array responses | `User.IntegrationTests/TestCase/05-array-responses.yaml` |
| deeply nested structures | `User.IntegrationTests/TestCase/06-nested-structures.yaml` |
| cascading skips | `User.IntegrationTests/TestCase/07-depends.yaml` |
| GraphQL query, mutation, error arrays | `User.IntegrationTests/TestCase/08-graphql.yaml` |
| multi-environment config with per-environment auth | `User.IntegrationTests/suite.config.yaml` |

---

## Diagnosing a failure

**Work the data and environment angle first** — in this mode that is the usual cause, not the
assertions.

| Symptom | Likely cause |
|---|---|
| passes once, fails on the next run | created data is not unique per run — see `state-and-data.md` |
| `409`/`400` on a create that used to work | leftover data from an earlier run |
| everything returns `401` partway through a long run | the OAuth2 token expired; it is fetched once and never refreshed |
| everything returns `401` from the first test | auth block is on an inactive environment, or the secret is unset |
| passes locally, fails in CI | a different `TEST_ENVIRONMENT`, or an unexported `${VAR}` |
| a field the spec documents is missing | the spec is out of date — trust the response |

Then the field-level output (`doc/failure-output.md`):

| Output | Meaning | Fix |
|---|---|---|
| both values shown | values differ | fix the expectation, or claim the field with a matcher if it is environment-dependent |
| `actual: <missing>` | expected field absent from the response | the service does not return it here — correct the expected body |
| `expected: <absent>` | response carries a field the test does not account for | add it, or claim it with a matcher |

Paths use dots for objects and brackets for arrays: `orders[0].lines[2].sku`.

**Zero tests discovered** is never a ConfIT problem — the file is missing its `<None Update>` /
`CopyToOutputDirectory` entry, so it does not exist in the build output.
