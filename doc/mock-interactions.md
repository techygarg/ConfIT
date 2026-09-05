# Mock Interactions

Component tests run the service under test in-process, but real services still call external dependencies. Without a way to stand in for those dependencies, the test would fail the moment it hits a live HTTP call. **Mock interactions** let you declare what those outbound calls look like and what each one should return — all inside the test definition file, without writing a line of C#.

Under the hood ConfIT uses WireMock.Net. Before each test, ConfIT registers your declared interactions as stubs. When the service makes an outbound request that matches a stub, WireMock returns the configured response. When the test ends, the stubs are cleared.

---

## When to use mocks

Use the `mock` section in **component tests only**. Integration tests run against real services — the `mock` section should be omitted entirely and WireMock is not involved.

If a component test's service makes no outbound HTTP calls, you can omit `mock` from that test too. It is always optional.

---

## Structure

The `mock` key sits at the top level of a test, alongside `api`. It contains a single key, `interactions`, which is an array. Each entry in the array declares one stubbed exchange: a **request** to match and a **response** to return.

```json
{
  "ShouldDoSomething": {
    "mock": {
      "interactions": [
        {
          "request": {
            "method": "GET",
            "path": "/api/dependency/resource"
          },
          "response": {
            "statusCode": 200,
            "body": { "result": "ok" }
          }
        }
      ]
    },
    "api": {
      "request": { "method": "POST", "path": "/api/service/action" },
      "response": { "statusCode": 201 }
    }
  }
}
```

The order of interactions in the array does not matter — WireMock matches each inbound request against all registered stubs simultaneously.

---

## Request matching

The `request` object inside each interaction describes the inbound call that WireMock should match. All fields are optional except `method` and `path`.

| Field | Type | Description |
|---|---|---|
| `method` | string | HTTP method — `GET`, `POST`, `PUT`, `PATCH`, or `DELETE` |
| `path` | string | Exact URL path |
| `params` | object | Query string parameters — key/value pairs, all matched exactly |
| `headers` | object | Request headers — key/value pairs, all matched exactly |
| `body` | object | Request body — matched structurally (partial, order-insensitive) |
| `bodyFromFile` | string | Load the match body from a file — see [test-file-format.md](./test-file-format.md) |

**`method` and `path` are always required.** All other fields narrow the match further — WireMock only fires the stub when every declared field matches the incoming request.

**Example — match on path and query parameter:**

```json
{
  "request": {
    "method": "GET",
    "path": "/api/demo",
    "params": { "email": "test@test.com" }
  },
  "response": {
    "statusCode": 200,
    "body": { "isValid": true }
  }
}
```

**Example — match on path and request body:**

```json
{
  "request": {
    "method": "POST",
    "path": "/api/demo",
    "body": { "email": "test@test.com" }
  },
  "response": {
    "statusCode": 200,
    "body": { "isValid": true }
  }
}
```

📄 Live example: [`User.ComponentTests/TestCase/01-user-lifecycle.yaml` — `CreateUser`](../example/User.ComponentTests/TestCase/01-user-lifecycle.yaml)

---

## Response definition

The `response` object declares what WireMock returns when the request matches.

| Field | Type | Description |
|---|---|---|
| `statusCode` | integer | HTTP status code to return |
| `body` | object | Response body as inline JSON |
| `bodyFromFile` | string | Load the response body from a file — see [test-file-format.md](./test-file-format.md) |
| `override` | object | Deep-merge on top of `bodyFromFile` — see [test-file-format.md](./test-file-format.md) |
| `headers` | object | Additional response headers to include |

`Content-Type: application/json` is always added automatically. Headers declared in `headers` are merged on top of it.

**Example — return a specific status with a body and custom header:**

```json
{
  "request": {
    "method": "GET",
    "path": "/api/dependency/resource"
  },
  "response": {
    "statusCode": 200,
    "body": { "data": "value" },
    "headers": { "X-Source": "mock" }
  }
}
```

📄 Live example: [`User.ComponentTests/TestCase/01-user-lifecycle.yaml` — `CreateUser`](../example/User.ComponentTests/TestCase/01-user-lifecycle.yaml)

---

## `bodyFromFile` for mocks

Both the request `body` and the response `body` in a mock interaction can be loaded from a file using `bodyFromFile`. The mechanics are identical to the `api` section — the filename is resolved against the folder configured in `SuiteConfig`. The `override` field merges on top of the loaded body after the file is read.

See [test-file-format.md](./test-file-format.md) for the full `bodyFromFile` and `override` reference.

---

## Multiple interactions

One test can declare as many interactions as the service needs. This is the common case: a service that calls three downstream endpoints before responding will need three stub entries.

Each interaction is independent. WireMock matches each inbound request against all registered stubs and selects a matching one. If the same outbound endpoint is called more than once, WireMock reuses the same stub for every call.

**Example — three interactions for a single user-creation flow:**

```json
{
  "ShouldCreateAUser": {
    "mock": {
      "interactions": [
        {
          "request": {
            "method": "GET",
            "path": "/api/demo/test@test.com"
          },
          "response": {
            "statusCode": 200,
            "body": { "isValid": true }
          }
        },
        {
          "request": {
            "method": "GET",
            "path": "/api/demo",
            "params": { "email": "test@test.com" }
          },
          "response": {
            "statusCode": 200,
            "body": { "isValid": true }
          }
        },
        {
          "request": {
            "method": "POST",
            "path": "/api/demo",
            "body": { "email": "test@test.com" }
          },
          "response": {
            "statusCode": 200,
            "body": { "isValid": true }
          }
        }
      ]
    },
    "api": {
      "request": {
        "method": "POST",
        "path": "/api/user",
        "body": { "name": "test", "email": "test@test.com", "age": 10 }
      },
      "response": {
        "statusCode": 201,
        "body": { "id": 1 }
      }
    }
  }
}
```

📄 Live example: [`User.ComponentTests/TestCase/01-user-lifecycle.yaml` — `CreateUser`](../example/User.ComponentTests/TestCase/01-user-lifecycle.yaml)

---

## YAML anchor reuse

When several interactions return the same response body, repeating it verbatim is noisy and fragile. In YAML test files, **anchors** (`&`) and **aliases** (`*`) solve this: define the value once on the first interaction, then reference it on the rest.

```yaml
ShouldCreateUser_InYamlFormat:
  mock:
    interactions:
      - request:
          method: GET
          path: /api/demo/yaml@test.com
        response:
          statusCode: 200
          body: &valid_body      # anchor — define the value here
            isValid: true

      - request:
          method: GET
          path: /api/demo
          params:
            email: yaml@test.com
        response:
          statusCode: 200
          body: *valid_body      # alias — reuse the same value

      - request:
          method: POST
          path: /api/demo
          body:
            email: yaml@test.com
        response:
          statusCode: 200
          body: *valid_body      # alias again
```

The anchor name (`valid_body`) is local to the file — it never appears in the parsed test data. Any value can be anchored: a response body, a full `response` block, or an entire `interaction` entry.

This is the primary reason to prefer YAML when a test has several interactions sharing a common response.

📄 Live example (YAML with anchor reuse): [`User.ComponentTests/TestCase/01-user-lifecycle.yaml` — `CreateUser`](../example/User.ComponentTests/TestCase/01-user-lifecycle.yaml)

---

## What happens when no mock matches

If the service makes an outbound call that does not match any declared interaction, WireMock returns a `404` response with no body. The service will handle that response however it is coded — but in most cases the test will fail on the `api.response` assertion, reporting an unexpected status code or body.

This is the intended behaviour: an unmatched call surfaces immediately as a test failure rather than silently passing or hanging. To diagnose it, set `enableLogs` on the `mock:` block — WireMock will print each incoming request and whether it matched a stub.

```yaml
component:
  mock:
    url: http://localhost:8888
    enableLogs: true
```

### Discovering what a service calls

The same switch turns an unknown dependency surface into a listing. Write the test with **no** `mock` block, turn logging on, and run it — every outbound call comes back `404` and is logged:

```
[Warn] : HttpStatusCode set to 404 : No matching mapping found
    "Path": "/api/demo/test@test.com",
    "Url": "http://localhost:8888/api/demo/test@test.com",
    "Status": "No matching mapping found"
```

Each unmatched entry is one outbound call, with its method, path, query and body — enough to write the interactions from. This works whatever language the service under test is written in, because it observes HTTP rather than code, which makes it the practical way to build mocks for an [AppLauncher](./app-launcher.md) suite.

**Ignore the test's pass/fail during this loop and read the log.** A test can pass with every mock missing: the unmatched `404`s make the service take its dependency-failure path, which may be exactly the error the test expected.

---

## Quick reference

| Field | Where | Required | Description |
|---|---|---|---|
| `mock.interactions` | test root | — | Array of stub entries |
| `request.method` | interaction | yes | HTTP method to match |
| `request.path` | interaction | yes | Exact path to match |
| `request.params` | interaction | no | Query parameters to match (all exact) |
| `request.headers` | interaction | no | Request headers to match (all exact) |
| `request.body` | interaction | no | Request body to match (structural) |
| `request.bodyFromFile` | interaction | no | Load match body from file |
| `response.statusCode` | interaction | yes | Status code to return |
| `response.body` | interaction | no | Response body to return |
| `response.bodyFromFile` | interaction | no | Load response body from file |
| `response.override` | interaction | no | Merge on top of `bodyFromFile` |
| `response.headers` | interaction | no | Additional headers to return |
