# GraphQL Support

A `graphql` block lets a test send a GraphQL query or mutation without hand-building the request envelope. Declare the query and variables; ConfIT composes the `{ query, variables, operationName }` JSON body, sets the HTTP method, and sets `Content-Type` — the same request pipeline (matchers, `extract`, `{{inject}}`, mocking) applies afterward exactly as it does for any other test.

`graphql` is sugar over the existing `body` / `method` / `headers` fields — it is resolved once, at load time, into the same request a hand-written test would produce. It works in both `api.request` and `mock.interactions[].request`.

---

## Structure

```json
"request": {
  "path": "/graphql",
  "graphql": {
    "query": "query { userById(id: 1) { id name } }"
  }
}
```

| Field | Required | Description |
|---|---|---|
| `query` | one of `query` / `queryFromFile` | Inline query or mutation text |
| `queryFromFile` | one of `query` / `queryFromFile` | Load query text from a file in `RequestBodyFolder` |
| `variables` | — | Inline JSON object passed as GraphQL variables |
| `operationName` | — | Named operation to run, when the document defines more than one |

Exactly one of `query` / `queryFromFile` must be set — a test with neither fails to load with `Test case's 'graphql' block must set either 'query' or 'queryFromFile'.`

---

## What gets composed

- **`request.body`** becomes `{ "query": "...", "variables": {...}, "operationName": "..." }`. The `variables` and `operationName` keys are included only when set — an inline query with no variables produces a body with just `query`.
- **`request.method`** defaults to `POST` only if not already set. An explicit `method` in the DSL is never overridden.
- **`Content-Type: application/json`** is added only if no `Content-Type` header is already present (checked case-insensitively) — set your own to override it.

---

## `queryFromFile`

Loads query text from a file in the same `RequestBodyFolder` used by `bodyFromFile` — see [Test File Format](./test-file-format.md). If both `query` and `queryFromFile` are set, `queryFromFile` wins silently, mirroring `bodyFromFile` / `body` precedence.

```yaml
GetUserById_QueryFromFile:
  api:
    request:
      method: POST
      path: /graphql
      graphql:
        queryFromFile: user-by-id.graphql
        variables:
          id: "{{graphqlUserId}}"
```

`Request/user-by-id.graphql`:

```graphql
query GetUserById($id: Int!) {
  userById(id: $id) {
    id
    name
    email
    age
  }
}
```

📄 Live example: [`User.ComponentTests/TestCase/05-graphql.yaml` — `GetUserById_QueryFromFile`](../example/User.ComponentTests/TestCase/05-graphql.yaml)

---

## Mutations and `extract`

A GraphQL mutation is just another response body — `extract` and `matcher.semantic` apply to `data.<field>` the same way they apply anywhere else.

```yaml
CreateUserMutation:
  api:
    request:
      method: POST
      path: /graphql
      graphql:
        query: |
          mutation CreateUser($input: CreateUserCommandInput!) {
            createUser(input: $input) {
              id
            }
          }
        variables:
          input:
            name: graphql-user
            email: graphql-user@test.com
            age: 25
    response:
      statusCode: 200
      body:
        data:
          createUser: {}
      extract:
        graphqlUserId: $.body.data.createUser.id
      matcher:
        semantic:
          data__createUser__id: greaterThan(0)
```

📄 Live example: [`User.ComponentTests/TestCase/05-graphql.yaml` — `CreateUserMutation`](../example/User.ComponentTests/TestCase/05-graphql.yaml)

---

## Inline query with `{{inject}}`

Since `query` is plain text, `{{varName}}` injection works inside it exactly as it does in `path` or `body`.

```yaml
GetUserById_InlineQuery:
  depends:
    - CreateUserMutation
  api:
    request:
      method: POST
      path: /graphql
      graphql:
        query: |
          query {
            userById(id: {{graphqlUserId}}) {
              id
              name
              email
              age
            }
          }
```

📄 Live example: [`User.ComponentTests/TestCase/05-graphql.yaml` — `GetUserById_InlineQuery`](../example/User.ComponentTests/TestCase/05-graphql.yaml)

---

## Matching GraphQL errors

A GraphQL response can return `200 OK` with a populated `errors` array. Since `errors` is a normal JSON array, the standard matchers apply — including the array-wildcard `*` segment for asserting on every entry regardless of count. See [Matchers and Patterns — array-wildcard segments](./matchers-and-patterns.md#array-wildcard-segments).

```yaml
GetUserById_MultipleErrors:
  api:
    request:
      method: POST
      path: /graphql
      graphql:
        query: |
          query {
            a: userById(id: 99998) { id }
            b: userById(id: 99999) { id }
          }
    response:
      statusCode: 200
      body:
        data:
          a: null
          b: null
        errors:
          - message: "User doesn't exist with Id : 99998"
          - message: "User doesn't exist with Id : 99999"
      matcher:
        ignore:
          - errors__*__extensions
          - errors__*__path
```

📄 Live example: [`User.ComponentTests/TestCase/05-graphql.yaml` — `GetUserById_MultipleErrors`](../example/User.ComponentTests/TestCase/05-graphql.yaml)

---

## Mocking a GraphQL dependency

`mock.interactions[].request` accepts `graphql` the same way `api.request` does — useful when the service under test itself calls a downstream GraphQL API. WireMock still matches on the composed `body`, so overlapping stubs on the same path are disambiguated by query/variables content exactly as they are for REST mocks — see [Mock Interactions](./mock-interactions.md).

---

## Quick reference

| Field | Where | Required | Description |
|---|---|---|---|
| `request.graphql.query` | `api` / `mock.interactions[]` | one of `query` / `queryFromFile` | Inline query or mutation text |
| `request.graphql.queryFromFile` | `api` / `mock.interactions[]` | one of `query` / `queryFromFile` | Load query text from `RequestBodyFolder` |
| `request.graphql.variables` | `api` / `mock.interactions[]` | no | Inline JSON object of GraphQL variables |
| `request.graphql.operationName` | `api` / `mock.interactions[]` | no | Named operation to run |
