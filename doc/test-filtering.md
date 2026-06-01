# Test Filtering

By default, every test in a suite runs. Filtering lets you run a targeted subset without touching the test files — useful for CI pipelines that run only a smoke pool on every push, debugging a single failing test, or separating fast and slow test groups.

Filtering works through the `TestFilter` object passed to `BaseTest`. A filtered-out test is **skipped**, not failed — it does not affect the overall pass/fail result of the suite.

---

## Tags in the DSL

Add a `tags` array to any test case. Tags are arbitrary strings; a test can carry as many as you need.

**JSON:**
```json
"ShouldReturnErrorIfUserNotExist": {
  "tags": ["errors", "user"],
  "api": {
    "request": { "method": "GET", "path": "/api/user/notexist@test.com" },
    "response": { "statusCode": 404 }
  }
}
```

**YAML:**
```yaml
ShouldCreateUser_InYamlFormat:
  tags:
    - yaml
    - user
  api:
    request:
      method: POST
      path: /api/user
    response:
      statusCode: 201
```

Tests with **no tags** are unaffected by any active tag filter — they always run. See the [test file format reference](test-file-format.md) for the full DSL.

📄 Live example: [`User.ComponentTests/TestCase/yaml-support.yaml`](../example/User.ComponentTests/TestCase/yaml-support.yaml)

---

## Filtering by Tag (`RUN_POOLS`)

`TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS")` reads a comma-separated list of tags from the `RUN_POOLS` environment variable. Only tests whose `tags` list intersects `RUN_POOLS` run; all others are skipped.

**Fixture setup:**
```csharp
Filter = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");
```

Pass `Filter` to `BaseTest` via the fixture constructor — the integration test fixture already wires this up.

📄 Live example: [`User.IntegrationTests/TestSuiteFixture.cs`](../example/User.IntegrationTests/TestSuiteFixture.cs)

**Runtime usage:**
```bash
# Run only tests tagged "smoke"
RUN_POOLS=smoke dotnet test

# Run tests tagged "smoke" or "user" (union, not intersection)
RUN_POOLS=smoke,user dotnet test
```

The match is **case-insensitive** and ignores surrounding whitespace. `RUN_POOLS=Smoke` matches a test tagged `smoke`.

**What happens to untagged tests?**

When `RUN_POOLS` is set, tests with no `tags` array are **skipped**. If you need a test to run under all tag-filtered runs, give it a tag that is always included — or leave the filter unset.

---

## Filtering by Test Name (`RUN_TESTS`)

`TestFilter.CreateForTestsFromEnvVariable("RUN_TESTS")` reads a comma-separated list of exact test names. Only the named tests run; all others are skipped.

**Fixture setup:**
```csharp
Filter = TestFilter.CreateForTestsFromEnvVariable("RUN_TESTS");
```

**Runtime usage:**
```bash
# Run a single test
RUN_TESTS=ShouldCreateAUser dotnet test

# Run two specific tests
RUN_TESTS=ShouldCreateAUser,ShouldGetUserById dotnet test
```

The match is **case-insensitive**. `RUN_TESTS=shouldcreateauser` matches `ShouldCreateAUser`.

---

## Hardcoded Filters

Use `TestFilter.CreateForTags` and `TestFilter.CreateForTests` when the filter is fixed — no environment variable lookup.

```csharp
// Always run only the smoke pool
Filter = TestFilter.CreateForTags("smoke");

// Always run only these two tests
Filter = TestFilter.CreateForTests("ShouldCreateAUser,ShouldGetUserById");
```

This is convenient during local development to focus on a particular area. **Do not commit hardcoded filters as the default fixture setup** — it hides tests from CI and makes coverage gaps invisible until someone notices.

---

## Skipped vs Failed

A filtered-out test is logged and skipped:

```
  ⏭  Skipping: ShouldReturnErrorIfUserNotExist
```

Skipped tests:
- Do not count as failures
- Do not block the overall suite from passing
- Are grouped clearly in the suite summary

---

## No Filter — Running All Tests

Pass `null` for the `TestFilter` argument in `BaseTest` to run every test unconditionally. This is the correct default for CI when no scoping is needed:

```csharp
// Component tests fixture — no filter by default; all tests run in CI
Filter = null;
```

When `Filter` is `null`, `ShouldSkipTheTest` returns `false` for every test.

---

## CI Pattern

Control which pools run from the outside — do not bake a filter into the fixture for CI environments.

**GitHub Actions:**
```yaml
- name: Run smoke tests
  run: dotnet test
  env:
    RUN_POOLS: smoke

- name: Run full suite
  run: dotnet test
  # No RUN_POOLS set — all tests run
```

**Make target:**
```makefile
smoke:
    RUN_POOLS=smoke dotnet test ./example/User.IntegrationTests
```

This keeps the fixture clean and lets CI orchestrate scope without modifying source files.
