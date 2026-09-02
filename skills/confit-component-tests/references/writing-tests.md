# Writing Component Test Definitions

Compact reference. For depth, read the `doc/` page named in each section — they are on disk at
`<root>/doc/`.

---

## The rule that catches most people

**The expected body must account for every field in the actual response.** Matchers remove the
fields they claim, then whatever is left is compared structurally. A response field that is
neither listed in `body` nor claimed by a matcher fails as `expected: <absent>`.

Two consequences:

- **Omitting `body` fails the test.** With no expected body the diff compares the response against
  nothing and reports a difference. Use `body: {}` when every field is claimed by a matcher.
- **An empty response body cannot be asserted.** A `204`, or any zero-length body, fails while
  being parsed as JSON. Verify such an endpoint through a follow-up request instead.

## Field reference

`doc/test-file-format.md` for the full version.

| | |
|---|---|
| `tags` | strings; matched against the filter env var. With `strategy: tags` active, an **untagged test is skipped** |
| `depends` | prerequisite test names — same file, defined earlier. Skips instead of failing when one did not pass |
| `mock.interactions` | component only — see `mock-discovery.md` |
| `api.request` | `method`, `path`, `body`, `bodyFromFile`, `override`, `params`, `headers`, `graphql` |
| `api.response` | `statusCode`, `body`, `bodyFromFile`, `override`, `headers`, `matcher`, `extract` |
| `matcher` | `ignore` (list), `pattern` (field→regex), `semantic` (field→matcher) |
| `extract` | name → `$.body.…` / `$.headers['x-…']` / `$.statusCode`. Runs only when the test passes |
| `{{var}}` | injects an extracted value into path, body, headers, params, mock bodies |
| `${ENV}` | a different namespace — process environment, not extracted data |

Nested paths use `__`: `address__city`. A bare key name in `ignore`/`pattern` matches at **every**
depth; a `__` path is anchored to the root.

## Choosing a matcher

Work down; stop at the first line that applies.

1. **Is the value deterministic?** Put the literal in `body`. In component mode state is fresh
   each run, so this covers far more than people expect — assert the real `name`, `email`, `age`.
2. **Does a built-in `semantic` matcher describe it?** Use it:
   `isUuid`, `isIsoDate`, `isIsoDateTime`, `isEmail`, `isNull`, `isNotNull`, `isEmpty`,
   `isNotEmpty`, `greaterThan(n)`, `lessThan(n)`, `hasLength(n)`, `hasLength(min,max)`.
3. **Is the format specific to this system?** `pattern` with an anchored regex.
4. **Does the same domain assertion repeat?** Register a custom matcher in the fixture and give it
   a name. That is C# in the *test* project, which the developer owns.
5. **Nothing to assert at all?** Only then `ignore`.

`ignore` is the weakest option — an ignored field is an untested field. Use it for values that
carry no meaning to the test, not merely for values that change.

`semantic` paths do **not** support wildcards or array indexes. `ignore` and `pattern` accept a
`*` segment to reach across array elements (`errors__*__path`), but never as the final segment.
Depth: `doc/matchers-and-patterns.md`.

## Chaining tests

`extract` → `{{inject}}` → `depends` go together. `extract` runs only on a passing test, so
without `depends` a dependent test fails on a missing variable and hides the real cause. All three
must be in the same file, prerequisites first — files load alphabetically, and `depends` is
file-scoped.

## Which example shows what

All under `<root>/example/`. These are real, CI-verified files — read one rather than working from
a paraphrase.

| Need | File |
|---|---|
| create → read chain, mock anchors, extract/inject/depends | `User.ComponentTests/TestCase/01-user-lifecycle.yaml` |
| 404s, and a 400 driven by a mock response | `User.ComponentTests/TestCase/02-user-errors.yaml` |
| all three matcher types side by side | `User.ComponentTests/TestCase/03-response-matchers.yaml` |
| cascading skips | `User.ComponentTests/TestCase/04-depends.yaml` |
| GraphQL query, mutation, error arrays, wildcards | `User.ComponentTests/TestCase/05-graphql.yaml` |
| OAuth2 against a stubbed token endpoint | `User.ComponentTests.AppLauncher/TestCase/03-oauth2.yaml` |
| `bodyFromFile` + `override` | `User.IntegrationTests/TestCase/04-body-fixtures.yaml` |
| array responses | `User.IntegrationTests/TestCase/05-array-responses.yaml` |
| deeply nested structures | `User.IntegrationTests/TestCase/06-nested-structures.yaml` |

Read `<root>/example/README.md` first — it lists what in those files is demo-specific and must not
be copied.

---

## Diagnosing a failure

**Work the mock angle first.** In component mode it is the most common cause, and it does not
announce itself — an unmatched stub surfaces as an unexpected status code, not as a mock error.
See `mock-discovery.md` §6.

Then the field-level output (`doc/failure-output.md`):

| Output | Meaning | Fix |
|---|---|---|
| both values shown | field present on both sides, values differ | fix the expectation, or claim the field with a matcher if it is legitimately dynamic |
| `actual: <missing>` | expected field absent from the response | the API does not return it — correct the expected body |
| `expected: <absent>` | response carries a field the test does not account for | add it to `body`, or claim it with the right matcher. Blanket-`ignore` last |

Paths use dots for objects and brackets for arrays: `orders[0].lines[2].sku`.

**Zero tests discovered** is never a ConfIT problem — the file is missing its `<None Update>` /
`CopyToOutputDirectory` entry, so it does not exist in the build output.

**Every test skipped** means a tag filter is active and the tests carry no `tags:`.
