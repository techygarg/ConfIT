# [FLOW-001] Variable Extraction + Injection

← [Back to Roadmap](../roadmap.md)

**Tier:** 1 — Remove the Capability Ceiling  
**Status:** Requirements complete

---

## Goal

Allow a test to extract values from its HTTP response and inject them into subsequent tests — with zero C# required for the common case. Eliminates ~80% of `ITestProcessor` usage in real test suites.

---

## Format Support

The `extract` block must work identically in both JSON and YAML test definition files. Format is determined by file extension (`.json`, `.yaml`, `.yml`). The extraction syntax itself (`$.body.id`, `$.headers['X-Request-Id']`) always operates on the HTTP response, which is always JSON regardless of which format the test is written in.

---

## Execution Model

Tests within a single file run **sequentially in definition order** — top to bottom. No parallelism within a file. This is the foundation that makes variable extraction safe: a test can always read variables set by any test that appeared before it in the same file.

Variables are not shared across files. Each file's test sequence is an isolated run context.

---

## Variable Store

**Internal structure:** The store is a two-level map keyed by test name, then variable name:

```
{
  "ShouldCreateUser": {
    "userId":    "abc-123",
    "userEmail": "alice@example.com"
  },
  "ShouldUpdateUser": {
    "updatedAt": "2026-05-30T10:00:00Z"
  }
}
```

This means every extracted value is always associated with the test that produced it. The short-name syntax `{{userId}}` is a convenience layer on top — not a different storage model.

**Lifecycle:** Created at the start of the file's test run, cleared when the file's tests finish. Never persisted to disk.

**Populated by:** `extract` blocks on passing tests only. If a test fails (wrong status code, assertion failure), its `extract` block does not run and no variables are set for that test.

---

## Extraction

Defined in the `response` block of a test. Runs after all assertions pass.

**YAML:**
```yaml
ShouldCreateUser:
  api:
    request:
      method: POST
      path: /api/users
      body: { name: Alice, email: alice@example.com }
    response:
      statusCode: 201
      extract:
        userId:    $.body.id
        userEmail: $.body.email
        requestId: $.headers['x-request-id']
```

**JSON:**
```json
{
  "ShouldCreateUser": {
    "api": {
      "request": {
        "method": "POST",
        "path": "/api/users",
        "body": { "name": "Alice", "email": "alice@example.com" }
      },
      "response": {
        "statusCode": 201,
        "extract": {
          "userId":    "$.body.id",
          "userEmail": "$.body.email",
          "requestId": "$.headers['X-Request-Id']"
        }
      }
    }
  }
}
```

**Source path syntax — unified response model:**

| Prefix | What it targets |
|--------|----------------|
| `$.body.*` | JSONPath on the response body |
| `$.headers.*` | Response header by name |
| `$.statusCode` | HTTP status code (as number) |

Paths are standard JSONPath. Nested body fields: `$.body.address.city`. Array element: `$.body.items[0].id`.

---

## Injection

Variables are injected using `{{variableName}}` syntax. Supported in:

| Location | Example |
|----------|---------|
| Request path | `path: /api/users/{{userId}}` |
| Request body fields | `body: { id: "{{userId}}" }` |
| Request headers | `headers: { X-User: "{{userId}}" }` |
| Query params | `params: { filter: "{{userEmail}}" }` |
| Mock response bodies | `body: { ownerId: "{{userId}}" }` |
| Expected response body | `body: { id: "{{userId}}" }` |

**Type coercion:** Injection is JSON-aware, not string replacement. If the extracted value is a number (`42`) and the injection target is a JSON body field, the value remains `42` — not `"42"`. Same for booleans and nulls. In string contexts (path, headers, params) values are always converted to string.

---

## Short Name vs Full-Prefix Access

**Short name `{{userId}}`**  
The runner scans the variable store across all tests that have run so far. Resolves successfully if exactly one test has extracted a variable named `userId`. This is the common case for simple sequential chains.

**Full prefix `{{ShouldCreateUser.userId}}`**  
Direct lookup into the store — goes to the `ShouldCreateUser` bucket, reads `userId`. Always unambiguous. Use this when two tests have extracted variables with the same name.

**Collision behaviour:** If two tests have both extracted a variable named `userId` and a third test injects `{{userId}}` (short form), the runner errors:

```
ERROR: Ambiguous variable '{{userId}}' in test 'ShouldDeleteUser'.
  Extracted by: ShouldCreateUser, ShouldCreateAnotherUser
  Fix: use full prefix — {{ShouldCreateUser.userId}} or {{ShouldCreateAnotherUser.userId}}
```

---

## Undefined Variable Behaviour

If a test references `{{userId}}` and no prior test has extracted that name, the runner fails immediately with an actionable error:

```
ERROR: Undefined variable '{{userId}}' in test 'ShouldFetchUser'.
  No test in this file has extracted a variable named 'userId'.
  Ensure 'ShouldCreateUser' appears before 'ShouldFetchUser' in the file
  and that 'ShouldCreateUser' has an extract block for this value.
```

---

## Variable Namespaces — Env vs Runtime

Two distinct namespaces, distinct syntax. They do not overlap.

| Syntax | Source | When resolved | Example |
|--------|--------|---------------|---------|
| `${VAR_NAME}` | Environment variable or [ENV-002] profile | At suite start, static for the run | `${API_TOKEN}` |
| `{{varName}}` | Extracted from a prior test's response | At test execution time, dynamic | `{{userId}}` |

Using `{{VAR}}` for an env var, or `${var}` for a runtime variable, is an error caught at load time.

---

## Full Example

**YAML:**
```yaml
ShouldCreateUser:
  api:
    request:
      method: POST
      path: /api/users
      body: { name: Alice, email: alice@example.com }
    response:
      statusCode: 201
      extract:
        userId:    $.body.id
        userEmail: $.body.email

ShouldFetchUser:
  api:
    request:
      method: GET
      path: /api/users/{{userId}}
    response:
      statusCode: 200
      body: { email: "{{userEmail}}" }

ShouldDeleteUser:
  api:
    request:
      method: DELETE
      path: /api/users/{{userId}}
    response:
      statusCode: 204
```

**JSON:**
```json
{
  "ShouldCreateUser": {
    "api": {
      "request": {
        "method": "POST",
        "path": "/api/users",
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
  },

  "ShouldFetchUser": {
    "api": {
      "request": {
        "method": "GET",
        "path": "/api/users/{{userId}}"
      },
      "response": {
        "statusCode": 200,
        "body": { "email": "{{userEmail}}" }
      }
    }
  },

  "ShouldDeleteUser": {
    "api": {
      "request": {
        "method": "DELETE",
        "path": "/api/users/{{userId}}"
      },
      "response": { "statusCode": 204 }
    }
  }
}
```

---

## Out of Scope — v1

Deliberate exclusions. Revisit once usage patterns emerge.

| Excluded | Reason |
|----------|--------|
| `depends:` between tests | Sequential file execution makes explicit ordering unnecessary for v1. Revisit in [FLOW-002] if cross-file or conditional-skip needs arise. |
| Array extraction (`$.body.items[*].id` → list) | Complicates type model and injection syntax significantly. |
| Default values (`{{userId \| default: 0}}`) | Masks misconfigured test chains; fail loudly is safer. |
| Persistence to disk between runs | Variables are test-run state, not fixtures. |
| Template expressions (`{{userId + 1}}`) | Out of scope for a test tool DSL. |

---

## v2 — Static Validation Utility

A CLI command that scans a test folder statically before any test runs and reports anomalies:

- `{{varName}}` references with no matching `extract` in any file
- Short-name collisions — same variable name extracted by two different tests
- Full-prefix references (`{{TestName.varName}}`) where the named test has no `extract` for that name
- Ordering risks — test uses a variable from a test that appears later in execution order

Shifts collision and undefined-reference errors from runtime (mid-run failure) to pre-flight. Pairs naturally with [TOOL-003] CLI tool.
