# Test Filtering

By default every test in a suite runs. Filtering lets you run a targeted subset without changing the test files — useful for smoke pipelines, debugging a single failing test, or separating fast and slow groups.

A filtered-out test is **skipped**, not failed. It does not affect the suite's pass/fail result and appears in the summary with a `⏭` marker.

---

## How Filtering Works

Filtering is controlled by a `TestFilter` object passed to `BaseTest` through `TestSuiteContext`. When `SuiteBootstrapper` reads your `suite.config.yaml`, it builds the filter from the `filter:` section automatically — no C# required.

There are **two strategies**:

| Strategy | What it filters on | Filter is active when |
|---|---|---|
| `tags` | The `tags` array on each test case | Env var is set and non-empty |
| `tests` | The test name (exact match) | Env var is set and non-empty |

When the env var is unset or empty, the filter is inactive and every test runs. This is the correct CI default — don't set the env var to run the full suite.

---

## Declaring Filters in `suite.config.yaml`

Add a `filter:` block to the `component` section or to an integration environment block.

```yaml
component:
  filter:
    strategy: tags      # "tags" or "tests"
    envVariable: TEST_TAGS  # the name of the env var this filter reads at runtime
```

```yaml
integration:
  default: local
  local:
    filter:
      strategy: tags
      envVariable: TEST_TAGS
  qa:
    filter:
      strategy: tests
      envVariable: TEST_NAMES
```

**`strategy`** — one of two values:
- `"tags"` — reads the env var and matches it against each test's `tags` array
- `"tests"` — reads the env var and matches it against each test's name

**`envVariable`** — the name of the environment variable to read at runtime. This is a name *you choose* — it is not a library constant. Use whatever makes sense for your project. The examples in this repository use `TEST_TAGS` for tag-based filtering and `TEST_NAMES` for name-based filtering, but these are conventions, not requirements.

`SuiteBootstrapper` reads the filter block automatically — no fixture code needed.

---

## Filtering by Tag

### In `suite.config.yaml`

```yaml
filter:
  strategy: tags
  envVariable: TEST_TAGS
```

### Tags in test definitions

Add a `tags` array to any test case:

```yaml
ShouldReturnErrorIfUserNotExist:
  tags:
    - errors
    - smoke
  api:
    request:
      method: GET
      path: /api/user/notexist@test.com
    response:
      statusCode: 404
```

A test can carry as many tags as needed. Tags are arbitrary strings — use them to group tests by feature, criticality, speed, or any dimension that matters to your pipeline.

### At runtime

```bash
# Run only tests tagged "smoke"
TEST_TAGS=smoke dotnet test

# Run tests tagged "smoke" or "errors" (union, not intersection)
TEST_TAGS=smoke,errors dotnet test

# Run everything — do not set the variable
dotnet test
```

The match is **case-insensitive** and ignores surrounding whitespace. `TEST_TAGS=Smoke` matches a test tagged `smoke`.

### Untagged tests

> **Important:** when a tag filter is active, tests with **no `tags` array are skipped** — they do not run. If you need a test to run under every tag-filtered execution, give it a tag that is always included in the filter value, or run without a filter.

This is intentional: an untagged test has opted out of all tag groups, so it has no claim to run when a specific group is selected.

📄 Live example: [`User.ComponentTests/TestCase/`](../example/User.ComponentTests/TestCase/)

---

## Filtering by Test Name

### In `suite.config.yaml`

```yaml
filter:
  strategy: tests
  envVariable: TEST_NAMES
```

### At runtime

```bash
# Run a single test
TEST_NAMES=ShouldCreateAUser dotnet test

# Run two specific tests
TEST_NAMES=ShouldCreateAUser,ShouldGetUserById dotnet test
```

The match is **case-insensitive**. `TEST_NAMES=shouldcreateauser` matches `ShouldCreateAUser`.

All other tests are skipped. Tests with no matching name are not failed.

---

## No Filter — Running Everything

When `filter:` is absent from `suite.config.yaml`, or when the env var is not set, `TestFilter` is `null` and every test runs. This is the correct default for a full CI run.

```bash
# Run all tests — env var not set
dotnet test

# Run all tests explicitly
TEST_TAGS= dotnet test     # empty value → filter inactive
```

---

## Manual Construction (Without YAML)

When wiring fixtures manually, construct `TestFilter` directly and pass it into `TestSuiteContext`:

```csharp
// Tag-based, reads env var at construction time
context = new TestSuiteContext(
    ...,
    Filter: TestFilter.CreateForTagsFromEnvVariable("TEST_TAGS"));

// Name-based
context = new TestSuiteContext(
    ...,
    Filter: TestFilter.CreateForTestsFromEnvVariable("TEST_NAMES"));

// Hardcoded — useful for local debugging only; do not commit as the fixture default
context = new TestSuiteContext(
    ...,
    Filter: TestFilter.CreateForTags("smoke"));

// No filter — run everything
context = new TestSuiteContext(
    ...,
    Filter: null);
```

**Do not commit a hardcoded filter as the fixture default.** It hides tests from CI and makes coverage gaps invisible.

---

## CI Patterns

Control scope from outside the fixture — the fixture should always be filter-neutral by default.

**GitHub Actions — separate jobs by scope:**

```yaml
- name: Smoke tests
  run: dotnet test
  env:
    TEST_TAGS: smoke

- name: Full suite
  run: dotnet test
  # TEST_TAGS not set — all tests run
```

**Makefile targets:**

```makefile
smoke:
    TEST_TAGS=smoke dotnet test ./example/User.IntegrationTests

full:
    dotnet test ./example/User.IntegrationTests
```

**Environment-specific filtering** — different environments can use different filter strategies:

```yaml
integration:
  local:
    filter:
      strategy: tags
      envVariable: TEST_TAGS    # local: run by feature tag
  ci:
    filter:
      strategy: tests
      envVariable: TEST_NAMES   # CI: run specific regression tests
```

---

## Summary

| Scenario | `strategy` | env var not set | env var = `"smoke"` |
|---|---|---|---|
| Tag filter | `tags` | all tests run | only tests tagged `smoke` run; untagged tests skip |
| Name filter | `tests` | all tests run | only the test named `smoke` runs |
| No `filter:` block | — | all tests run | all tests run (var is ignored) |
