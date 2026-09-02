---
name: confit-component-tests
description: This skill should be used when a developer asks to "write component tests", "add ConfIT tests for this controller", "I just implemented X, test it", "test this endpoint against mocks", "mock this dependency", "my component test is calling the real service", "the mock isn't matching", or when adding or editing test definitions in a ConfIT suite whose config has a `component:` section. Works from the developer's own source — the controller, its handlers and the outbound clients behind them — to produce test definitions plus the WireMock interactions they need. For black-box tests against a deployed environment, use confit-integration-tests instead.
---

# ConfIT Component Tests

Written by the developer, from their own code, while the service still runs on their machine or
CI. **Two inputs: the controller, and the mocks behind it.** Nothing else is needed and nothing
else should be read.

Setting up the suite itself is a different job — use `confit-suite-setup`.

## Step 0 — Resolve the reference, then confirm the mode

```bash
bash <this skill>/scripts/reference-path.sh
```

It prints the ConfIT repository root, `<root>`. Everything below is relative to it. If it exits
non-zero, stop: the install is broken. Do not write tests from memory — the DSL and matcher set
move between releases.

Then open the target project's `suite.config.yaml`:

- Top-level key is **`component:`** → continue here.
- Top-level key is **`integration:`** → wrong skill. Integration suites cannot mock; hand over to
  `confit-integration-tests`.
- **No suite at all** → hand over to `confit-suite-setup` first.

Note whether `component.mock.url` is set. Without it no mock server runs, and every outbound call
will hit the real dependency.

Read one or two neighbouring files in `TestCase/` before writing anything — match the project's
own file numbering, tag vocabulary and error-body shape. The house style beats any example.

## Step 1 — The controller is the anchor

Identify the controller or endpoint under test: named by the developer, or located in the repo.
Read it for the contract — routes, methods, request DTOs, response types, status codes. Expand
route tokens; a class-level `[Route("api/[Controller]")]` on `OrderController` means `/api/order`.

Do not derive scope from `git diff` by default. The change may already be pushed and the tests
written on a later branch. Use the diff only when the developer says "test what I just changed".

## Step 2 — Go down the call path, for the mocks

This is the half of the job that no spec could give you. Follow controller → handler → outbound
client, and extract for every outbound call: **method, path, query params, headers, body**.

On the way, note the **branch points** — the conditions under which the handler throws or returns
an error. They are usually driven by what a dependency *returned*, which is what makes error
tests writable at all. In ConfIT's own example, `CreateUserCommandHandler` throws
`BadRequestException("Invalid Email.")` when any of three dependency checks comes back
`isValid: false`; the test for that 400 drives it by changing the **mock response**, not the
request body.

Then confirm the dependency's base-URL config key actually resolves to `mock.url` in the test
settings. If it does not, every component test silently calls the real service and may pass for
the wrong reason — that is a suite-setup defect, so hand it over.

Full procedure, including what to do when there is no source to read:
`references/mock-discovery.md`.

## Step 3 — Propose the matrix, confirm once

For the endpoints on that controller:

- the happy path
- each error branch found in Step 2, driven by the mock or by the request
- each `4xx` the controller itself owns (missing resource, invalid input)
- **dependency failure** — a `5xx` or a timeout from a stubbed dependency. This is the matrix
  entry that only component tests can have; integration cannot force it.

Show the list and get one confirmation before writing files. Do not generate a wall of tests
unasked.

## Step 4 — Write

Field reference, matcher decision order and an index of which example file demonstrates which
feature: `references/writing-tests.md`.

Component-specific instincts:

- **State is fresh each run**, so exact-value assertions are safe and preferred. Assert the real
  `name`, `email`, `age`; reach for `semantic`/`ignore` only for genuinely server-generated
  fields such as an id or a timestamp.
- **Fixed literal test data is fine here** — the in-memory database resets per process. Do not
  copy that habit into an integration suite.
- Mocks go in the `mock:` block of the same test. Reuse a repeated response body with a YAML
  anchor rather than restating it.

## Step 5 — Verify

```bash
python3 <root>/tools/check-testcases.py <path to test project>
```

Catches what ConfIT would otherwise raise at load time or on first failure: a response with no
expected body, `depends:` naming an unknown or later test, unresolvable `{{variables}}`, unknown
or malformed matcher names, unregistered test files.

Then run the suite. Every new test file needs a `<None Update>` entry with
`<CopyToOutputDirectory>Always</CopyToOutputDirectory>`, or it will not exist at runtime and the
theory will simply not see it.

When a test fails, work the mock angle first — it is the most common cause in this mode. See
"Diagnosing" in `references/writing-tests.md`.

## When the service is not .NET

An AppLauncher suite runs the app as an external process, which may be Go, Node, Python or
anything else. The controller-reading step becomes "find the route declaration in whatever
framework this is" — the shape of the job does not change: find the route, find the handler, find
the outbound calls.

When the source is unreadable or absent, skip straight to the traffic-observation loop in
`references/mock-discovery.md`. It watches HTTP rather than code, so it works in any language.

## Additional resources

- **`references/mock-discovery.md`** — trace the call path, branch points, the narrowest-match
  rule, and the observation loop for when there is no source.
- **`references/writing-tests.md`** — DSL field table, matcher decision order, the example index,
  and diagnosing failures.
- **`<root>/example/README.md`** — what in the examples is structural and what is demo-specific.
- **`<root>/doc/`** — `test-file-format.md`, `matchers-and-patterns.md`, `mock-interactions.md`,
  `graphql-support.md` for depth.
