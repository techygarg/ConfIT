# Test File Format

ConfIT test definitions live in `.json` or `.yaml` files — no C# required. Each file is a map of test name → test case. Tests run in the order they appear in the file.

```
TestCase/
  user.json          ← JSON format
  errors.json
  yaml-support.yaml  ← YAML format — identical capabilities, different syntax
```

---

## File Structure

A test file at the top level is a mapping of test names to test case objects. Each test case has up to three sections: `tags`, `mock`, and `api`.

```json
{
  "ShouldCreateAUser": {
    "tags": ["user", "smoke"],
    "mock": { ... },
    "api":  { ... }
  },
  "ShouldReturnNotFound": {
    "api": { ... }
  }
}
```

Only `api` is required. `tags` and `mock` are optional.

---

## `api`

The core of every test: what request to send and what response to expect.

### `api.request`

| Field | Required | Description |
|---|---|---|
| `method` | ✅ | HTTP verb — `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| `path` | ✅ | Request path. Supports `{{varName}}` injection. |
| `body` | — | Inline JSON request body |
| `bodyFromFile` | — | Load request body from a file in `RequestBodyFolder` |
| `override` | — | Deep-merge on top of `bodyFromFile` (per-test variation) |
| `params` | — | Query string parameters as a key/value map |
| `headers` | — | Additional request headers as a key/value map |

```json
"request": {
  "method": "POST",
  "path": "/api/user",
  "body": { "name": "Alice", "email": "alice@example.com", "age": 30 },
  "headers": { "X-Correlation-Id": "test-123" }
}
```

### `api.response`

| Field | Required | Description |
|---|---|---|
| `statusCode` | ✅ | Expected HTTP status code |
| `body` | — | Inline expected response body |
| `bodyFromFile` | — | Load expected body from a file in `ResponseBodyFolder` |
| `override` | — | Deep-merge on top of `bodyFromFile` |
| `headers` | — | Expected response headers as a key/value map |
| `matcher` | — | Dynamic field handling — see [Matchers and Patterns](./matchers-and-patterns.md) |
| `extract` | — | Capture values for use in later tests — see [Variable Extraction + Injection](./variable-extraction-and-injection.md) |

```json
"response": {
  "statusCode": 201,
  "body": { "name": "Alice", "email": "alice@example.com" },
  "matcher": {
    "semantic": { "id": "greaterThan(0)" },
    "ignore": ["createdAt"]
  },
  "extract": {
    "userId": "$.body.id"
  }
}
```

### `bodyFromFile` and `override`

`bodyFromFile` loads a JSON file from the configured folder and sets it as the body. `override` deep-merges on top — use it to share a base fixture across tests while changing a field or two per test.

```json
"request": {
  "method": "POST",
  "path": "/api/user",
  "bodyFromFile": "create-user.json",
  "override": { "email": "different@example.com" }
}
```

📄 Live example: [`User.IntegrationTests/TestCase/user.json` — `ShouldCreateAUser_V2`](../example/User.IntegrationTests/TestCase/user.json)

---

## `mock`

Used in component tests to declare WireMock stubs before the request fires. Omit entirely for integration tests (real services).

### `mock.interactions`

Each interaction declares what incoming request to match and what response to return.

```json
"mock": {
  "interactions": [
    {
      "request": {
        "method": "GET",
        "path": "/api/external/resource",
        "params": { "filter": "active" },
        "headers": { "Accept": "application/json" }
      },
      "response": {
        "statusCode": 200,
        "body": { "isValid": true },
        "headers": { "Content-Type": "application/json" }
      }
    }
  ]
}
```

Mock interactions also support `bodyFromFile` and `override` on both the request and response sides.

📄 Live example: [`User.ComponentTests/TestCase/user.json` — `ShouldCreateAUser`](../example/User.ComponentTests/TestCase/user.json)

---

## `tags`

Tag a test with one or more labels. At runtime, `RUN_POOLS` env var filters tests by tag — only tests whose tags intersect the env var value will run. Untagged tests always run when no filter is active.

```json
"tags": ["smoke", "user"]
```

Tests without tags run regardless of `RUN_POOLS`. Tests with tags are skipped if none of their tags appear in `RUN_POOLS`.

---

## JSON Format

Standard JSON. Test files must be valid JSON — no comments, no trailing commas.

```json
{
  "ShouldReturnNotFound": {
    "api": {
      "request": {
        "method": "GET",
        "path": "/api/user/notexist@test.com"
      },
      "response": {
        "statusCode": 404,
        "body": {
          "error": {
            "status": 404,
            "code": "NotFound"
          }
        }
      }
    }
  }
}
```

📄 Live example: [`User.ComponentTests/TestCase/errors.json`](../example/User.ComponentTests/TestCase/errors.json)

---

## YAML Format

The same DSL, written in YAML. ConfIT loads `.yaml` and `.yml` files alongside `.json` — both formats are discovered by `TestReader.GetTestsForAFolder`. The YAML file is converted to the same internal structure before the test pipeline sees it. All matchers, mocks, extractors, and injectors behave identically.

YAML adds two things JSON cannot do:

**Comments** — annotate intent inline.

```yaml
ShouldReturnNotFound:
  # No mock needed — user doesn't exist in the DB
  api:
    request:
      method: GET
      path: /api/user/notexist@test.com
    response:
      statusCode: 404
```

**Anchors and aliases** — define a block once, reference it many times. Use this to avoid repeating identical mock responses across interactions.

```yaml
ShouldCreateUser:
  mock:
    interactions:
        # Define the response body once...
      - request:
          method: GET
          path: /api/demo/alice@example.com
        response:
          statusCode: 200
          body: &validation_ok   # anchor
            isValid: true
        # ...alias it in subsequent interactions
      - request:
          method: GET
          path: /api/demo
          params:
            email: alice@example.com
        response:
          statusCode: 200
          body: *validation_ok   # alias
  api:
    request:
      method: POST
      path: /api/user
      body:
        name: Alice
        email: alice@example.com
        age: 30
    response:
      statusCode: 201
      body: {}
      matcher:
        semantic:
          id: greaterThan(0)
```

**Important:** Anchors must be defined within a test case, not at the top level. Top-level keys are treated as test names — a bare anchor entry at the top level would be yielded as a (broken) test case.

YAML scalars map to the same JSON types the DSL expects: unquoted integers become numbers, `true`/`false` become booleans, `~` becomes null, everything else stays a string.

📄 Live example: [`User.ComponentTests/TestCase/yaml-support.yaml`](../example/User.ComponentTests/TestCase/yaml-support.yaml)

---

## Multi-File Suites

When using `TestReader.GetTestsForAFolder`, all `.json`, `.yaml`, and `.yml` files in the folder are loaded in filesystem order (typically alphabetical). Tests that depend on state from another file (e.g., a GET that needs an ID created by a previous POST) must live in the **same file** as their prerequisite — cross-file ordering is not guaranteed.

```
TestCase/
  errors.json       ← runs first (e < u < y)
  user.json         ← runs second — creates users, extracts IDs
  yaml-support.yaml ← runs third — can reference IDs extracted by user.json
```

Tests that depend on each other belong in the same file.
