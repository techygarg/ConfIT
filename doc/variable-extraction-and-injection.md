# Variable Extraction + Injection

When testing multi-step API flows, later tests often need data produced by earlier ones — an ID returned by a create endpoint, a token from a login call, a resource URL from a redirect. ConfIT provides a declarative way to capture these values and pass them forward without writing any C#.

---

## How It Works

**Extraction** — after a test passes, values are read from the HTTP response using JSONPath expressions and stored under a name you choose.

**Injection** — any subsequent test can reference those values with `{{varName}}` anywhere in its request or expected response definition.

The variable store is shared across all tests in a suite run. Tests within a file execute in definition order, so extraction from an earlier test is always available to a later one.

**Variable names are case-sensitive.** `{{userId}}` and `{{UserId}}` are distinct variables. Whatever name you define in `extract`, use exactly the same casing when injecting. The convention across ConfIT examples is `camelCase` — `userId`, `userEmail`, `requestId`.

---

## Extracting Values

Add an `extract` block inside the `response` section of any test. Each entry maps a variable name to a JSONPath expression.

```json
"ShouldCreateAUser": {
  "api": {
    "request": {
      "method": "POST",
      "path": "/api/user",
      "body": { "name": "Alice", "email": "alice@example.com" }
    },
    "response": {
      "statusCode": 201,
      "extract": {
        "userId":    "$.body.id",
        "userEmail": "$.body.email"
      }
    }
  }
}
```

Extraction only runs when the test **passes**. If assertions fail, the `extract` block is skipped and nothing is stored.

### Source paths

All paths use the unified response model:

| Prefix | What it targets |
|--------|----------------|
| `$.body.*` | JSONPath on the response body |
| `$.headers['header-name']` | Response header (use bracket notation for headers with hyphens) |
| `$.statusCode` | HTTP status code as a number |

Examples:
- `$.body.id` — top-level field
- `$.body.address.city` — nested field
- `$.body.items[0].id` — first element of an array
- `$.headers['x-request-id']` — response header (all header names are lowercased)
- `$.statusCode` — e.g. `201`

---

## Injecting Values

Reference a stored variable anywhere in a test using `{{varName}}`:

```json
"ShouldGetUserById": {
  "api": {
    "request": {
      "method": "GET",
      "path": "/api/user/{{userId}}"
    },
    "response": {
      "statusCode": 200,
      "body": {
        "email": "{{userEmail}}"
      }
    }
  }
}
```

### Where injection works

| Location | Example |
|----------|---------|
| Request path | `"path": "/api/user/{{userId}}"` |
| Request body | `"body": { "id": "{{userId}}" }` |
| Request headers | `"headers": { "X-User-Id": "{{userId}}" }` |
| Query params | `"params": { "filter": "{{userEmail}}" }` |
| Mock response bodies | `"body": { "ownerId": "{{userId}}" }` |
| Expected response body | `"body": { "email": "{{userEmail}}" }` |

### Type preservation

Injection is JSON-aware. If the extracted value is a number and the injection target is a JSON body field, the value stays a number — not `"42"`. In string contexts (path, headers, params) all values are converted to string.

---

## Naming and Disambiguation

**Short name** `{{userId}}` — scans all extracted variables. Works when the name is unique across the test run.

**Full prefix** `{{ShouldCreateAUser.userId}}` — direct lookup by test name and variable name. Use this when two different tests extract a variable with the same name.

If two tests have both extracted a variable named `userId` and a third test injects `{{userId}}` (short form), the runner raises an `AmbiguousVariableException` with suggested full-prefix alternatives.

---

## Environment Variables

Use `${VAR_NAME}` (dollar-brace syntax) to inject static values from the process environment:

```json
"headers": { "Authorization": "Bearer ${API_TOKEN}" }
```

`${...}` and `{{...}}` are distinct namespaces and resolve at different times:
- `${VAR}` — resolved from environment at suite start (static)
- `{{varName}}` — resolved from extracted test data at execution time (dynamic)

---

## Working Examples

### Component tests

`example/User.ComponentTests/TestCase/01-user-lifecycle.yaml` demonstrates a three-test chain:

1. **`CreateUser`** — creates a user, extracts `userId` from the `201` response body.
2. **`GetUserById`** — injects `{{userId}}` into the path; declares `depends: [CreateUser]` so it skips cleanly when the create fails rather than erroring on the missing variable.
3. **`GetUserByEmail`** — independent GET by email; no injection needed.

```yaml
CreateUser:
  api:
    response:
      statusCode: 201
      extract:
        userId: $.body.id
      matcher:
        semantic:
          id: greaterThan(0)

GetUserById:
  depends:
    - CreateUser
  api:
    request:
      method: GET
      path: "/api/user/{{userId}}"
    response:
      statusCode: 200
      body:
        name: test
        email: test@test.com
        age: 10
      matcher:
        ignore:
          - id
```

📄 Live example: [`User.ComponentTests/TestCase/01-user-lifecycle.yaml`](../example/User.ComponentTests/TestCase/01-user-lifecycle.yaml)

### Integration tests

`example/User.IntegrationTests/TestCase/01-user-lifecycle.yaml` uses the same pattern against a real running service. `GetUserById` and `GetUserByEmail` both declare `depends: [CreateUser]` and inject `{{userId}}`, without any C# processor code.

📄 Live example: [`User.IntegrationTests/TestCase/01-user-lifecycle.yaml`](../example/User.IntegrationTests/TestCase/01-user-lifecycle.yaml)

---

## Error Cases

| Situation | Error raised |
|-----------|-------------|
| `{{userId}}` referenced but no test has extracted it yet | `UndefinedVariableException` — points to the likely source test |
| Same short name extracted by two different tests and used without a prefix | `AmbiguousVariableException` — suggests `{{TestName.userId}}` |
| Same test tries to extract the same variable name twice | `VariableCollisionException` — variable names per test must be unique |
| `${ENV_VAR}` referenced but the environment variable is not set | `UndefinedVariableException` — check your environment |

---

## ITestProcessor — The Previous Approach

Before variable extraction was available, the same cross-test data flow was achieved by implementing `ITestProcessor` and `ITestProcessorFactory` in C#:

```csharp
public class ShouldGetUserByIdProcessor : ITestProcessor
{
    private readonly SuiteConfig _config;

    public ShouldGetUserByIdProcessor(SuiteConfig config) => _config = config;

    public void Before(TestApi testApi)
    {
        // Read the saved response from the create test and inject the ID manually
        var json = "ShouldCreateAUser".ReadJsonResponse(_config.ApiResponseFolder);
        testApi.Request.Path = $"/api/user/{json["id"]}";
    }

    public void After(TestApi testApi, JToken actualResponse) { }
}
```

This required:
- A C# class per test that needed dynamic data
- A factory to map test names to processors
- Wiring through `TestSuiteFixture`

**`ITestProcessor` is still available** for scenarios that genuinely need code — computing request signatures, side effects, complex conditional logic. For straightforward data flow between tests, the declarative `extract` + `{{inject}}` approach replaces it entirely.

---

## Related: Test Dependency Graph

`extract:` and `depends:` are designed to work together. When `CreateUser` fails, its `extract` block never runs — meaning `{{userId}}` is never set. Without `depends:`, `GetUserById` then fails with `UndefinedVariableException`, which obscures the actual root cause.

Declaring `depends: [CreateUser]` on `GetUserById` short-circuits this: the test is skipped with a clear reason rather than failing confusingly.

See [Test Dependency Graph](./test-dependency-graph.md) for the full reference.
