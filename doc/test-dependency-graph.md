# Test Dependency Graph

When one test fails, the tests that depend on its output often fail too — but for the wrong reason. A `GET /api/user/{{userId}}` test that fails because `{{userId}}` was never extracted doesn't tell you anything useful. What you actually want to know is that `CreateUser` failed.

The `depends:` field makes this explicit. When a prerequisite doesn't pass, its dependents are **skipped** — not failed — and the suite summary tells you exactly why.

---

## The `depends:` Field

Add `depends:` to any test with a list of prerequisite test names. All named tests must appear **earlier in the same file**.

**YAML:**
```yaml
CreateUser:
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
        name: Alice
        email: alice@example.com
        age: 30
      matcher:
        ignore:
          - id
```

**JSON:**
```json
{
  "CreateUser": {
    "api": {
      "request": { "method": "POST", "path": "/api/user", "body": { "name": "Alice", "email": "alice@example.com", "age": 30 } },
      "response": {
        "statusCode": 201,
        "extract": { "userId": "$.body.id" },
        "matcher": { "semantic": { "id": "greaterThan(0)" } }
      }
    }
  },
  "GetUserById": {
    "depends": ["CreateUser"],
    "api": {
      "request": { "method": "GET", "path": "/api/user/{{userId}}" },
      "response": {
        "statusCode": 200,
        "body": { "name": "Alice", "email": "alice@example.com", "age": 30 },
        "matcher": { "ignore": ["id"] }
      }
    }
  }
}
```

📄 Live example: [`User.ComponentTests/TestCase/01-user-lifecycle.yaml`](../example/User.ComponentTests/TestCase/01-user-lifecycle.yaml)

---

## What Happens When a Prerequisite Fails

When `CreateUser` fails:

- `GetUserById` is **skipped** (not executed, not failed)
- xUnit records it as a passing test (no assertion ran)
- ConfIT's suite summary shows it as skipped with the reason

```
══════════════════════════════════════════════════════
  Suite Summary
══════════════════════════════════════════════════════

  01-user-lifecycle.yaml
    ✗  CreateUser                                  580ms
    ⏭  GetUserById
         └─ prerequisite 'CreateUser' failed
    ⏭  GetUserByEmail
         └─ prerequisite 'CreateUser' failed

──────────────────────────────────────────────────────
  Total: 3   ✓ 0 passed   ✗ 1 failed   ⏭ 2 skipped
──────────────────────────────────────────────────────
```

One root failure, two skips. The suite result is one failed test — not three. Root-cause analysis is immediate.

---

## Cascading Skip

Skip status propagates automatically. If B depends on A and C depends on B:

- A fails → B is skipped → C is also skipped (because B was skipped)

You do not need to declare `depends: [A, B]` on C. Declaring `depends: [B]` is sufficient — ConfIT tracks that B was skipped and propagates accordingly.

```yaml
CreateUser:
  # ...

GetUserById:
  depends:
    - CreateUser
  # ...

GetUserByEmail:
  depends:
    - GetUserById   # if GetUserById was skipped, this is skipped too
  # ...
```

The skip message for C names its direct prerequisite (B), not the root cause (A):

```
⏭  GetUserByEmail
     └─ prerequisite 'GetUserById' was skipped
```

📄 Live example: [`User.ComponentTests/TestCase/04-depends.yaml`](../example/User.ComponentTests/TestCase/04-depends.yaml)

---

## Multiple Prerequisites

A test can declare multiple prerequisites. It is skipped if **any** of them did not pass.

```yaml
SendNotification:
  depends:
    - CreateUser
    - CreateOrder
  api:
    # ...
```

---

## Load-Time Validation

ConfIT validates `depends:` declarations when it loads the test file — before any test runs. Two errors are caught upfront:

**Unknown prerequisite name** — the named test does not exist in the file:
```
Test 'GetUserById' in '/path/to/user.yaml' declares 'depends: [CraeteUser]'
but no test named 'CraeteUser' exists in this file.
```

**Forward reference** — the named test is defined later in the file:
```
Test 'GetUserById' in '/path/to/user.yaml' declares 'depends: [CreateUser]'
but 'CreateUser' is defined after it.
Dependencies must reference tests defined earlier in the file.
```

Both errors prevent any tests in the file from running, so a misconfigured dependency is never silently ignored.

---

## Scope: Same File Only

`depends:` only references tests within the **same file**. Cross-file dependencies are not supported.

This is a deliberate boundary. The variable store (`extract` / `{{inject}}`) is shared across files in a folder run — variables extracted by a test in file A are available in file B. But for skip control, inter-file dependencies would require the test scheduler to understand ordering across files, which conflicts with xUnit's collection model.

In practice, tests that form a dependency chain belong in the same file. Use numeric filename prefixes (`01-`, `02-`) to express cross-file ordering when needed — ConfIT loads files alphabetically, so `01-user-lifecycle.yaml` always runs before `02-user-errors.yaml`.

---

## Relationship to `extract:` and `{{inject}}`

`depends:` and `extract:` are complementary, not redundant.

Without `depends:`, if `CreateUser` fails, `GetUserById` runs anyway and hits an `UndefinedVariableException` — because the `extract` block on a failed test never executes. The error message names the missing variable but says nothing about why it wasn't set.

With `depends: [CreateUser]`, `GetUserById` is skipped cleanly when its prerequisite fails. The skip message names the prerequisite, not the variable.

**Use them together:**

```yaml
CreateUser:
  api:
    response:
      statusCode: 201
      extract:
        userId: $.body.id       # extracts the ID if the test passes

GetUserById:
  depends:
    - CreateUser                # skipped cleanly if CreateUser fails
  api:
    request:
      path: "/api/user/{{userId}}"   # injects the extracted ID
```

See [Variable Extraction + Injection](./variable-extraction-and-injection.md) for the full `extract` / `{{inject}}` reference.

---

## Filter-Skipped Tests and Dependencies

When a test is skipped by a tag or name filter (`RUN_POOLS`, `RUN_TESTS`), it is recorded as `Skipped` in the dependency tracker. Any test that declares `depends:` on a filter-skipped test is also skipped.

This means the dependency graph is consistent regardless of skip reason: a test that did not pass — for any reason — is treated as a failed prerequisite by its dependents.
