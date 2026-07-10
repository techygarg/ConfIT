---
feature: GraphQL Support
epic: Platform Play
status: draft
priority: P2
depends_on: []
personas:
  - api-test-author
  - graphql-api-adopter
source_docs: []
---

# GraphQL Support

## Problem Statement

ConfIT test authors have no declarative way to define GraphQL query/mutation tests. GraphQL rides on ConfIT's existing HTTP pipeline (a single POST endpoint carrying a JSON body), so raw HTTP testing is technically possible today — but three GraphQL-specific realities make that path painful, and one of them cannot be cleanly asserted at all with the current matcher engine:

1. Query/mutation text is long, multi-line GraphQL syntax; embedding it as a JSON string value is a maintenance burden with no editor tooling.
2. A GraphQL server typically exposes one endpoint for every operation, so mocking two different operations against the same path requires differentiating by request body content — the DSL has no ergonomic support for that today.
3. GraphQL signals failure via an `errors` array in an HTTP 200 response body, not via HTTP status. Today's `ignore`/`pattern` matcher can only target one fixed field path (confirmed by reading `ResultMatcher.ExtractKeyAndParentPath`/`IsParentMatching`, which compares against a single literal parent path — array elements get distinct bracket-indexed paths in Json.NET, e.g. `errors[0]`, `errors[1]`). Any GraphQL error scenario with more than one error entry cannot be asserted cleanly without new matcher capability.

## User / Personas

**API test author extending an existing ConfIT suite** — already tests REST endpoints with ConfIT; their org is adding or already runs a GraphQL service, and wants the same declarative JSON/YAML + matcher workflow instead of adopting a second, GraphQL-specific test tool.

**New ConfIT adopter with a GraphQL-first API** — discovers ConfIT specifically because their team's primary surface is GraphQL, and wants a .NET-native declarative testing option — directly serving Platform Play's goal of growing ConfIT's user base beyond REST.

## Scope

**In scope:**
- `graphql` request block (`query`, `variables`, `operationName`) — compiled internally to the existing POST + JSON-body path through `TestHttpClient`; no new HTTP transport
- `queryFromFile` — loads raw GraphQL text from an external `.graphql`/`.gql` file, mirroring `bodyFromFile`'s external-file pattern
- Automatic `POST` method and `Content-Type: application/json` header applied when the `graphql` block is used — author does not restate them
- `graphql` mock block — same shape on the mock side, for declaratively mocking a GraphQL operation
- Matcher engine: an array-wildcard segment for `ignore`/`pattern` so a field can be targeted across every element of an array (e.g., every entry in an `errors[]` array), not just one fixed path
- Works in both JSON and YAML test definitions (no format-specific behavior)

**Out of scope:**
- GraphQL subscriptions (WebSocket transport — a fundamentally different model from the request/response POST flow this feature targets)
- Schema introspection or validation against a GraphQL SDL
- A fluent/generated query builder — queries remain hand-authored text via `query` or `queryFromFile`
- Batched GraphQL requests (multiple operations in a single HTTP call)
- File uploads via the GraphQL multipart request spec
- A separate "GraphQL success/failure" concept independent of HTTP status + body matching — tests still declare an expected HTTP status exactly as REST tests do today
- End-to-end GraphQL mock chain (`User.Api` making a real outbound GraphQL call to `JustAnotherService`) — deferred; `JustAnotherService` stays REST-only for this feature

## Boundary Conditions

- `graphql` is sugar over the same `TestHttpClient.Execute` path — `path` (e.g. `/graphql`) is still required from the author; ConfIT does not assume a fixed GraphQL endpoint.
- The array-wildcard matcher segment applies to `ignore`/`pattern` only for this feature; extending it to `semantic` matchers is an open question, not silently assumed.
- The wildcard token/syntax must not collide with the existing `__`-joined nested-path separator convention already used by `ignore`/`pattern`.
- `queryFromFile` resolves relative to the existing `RequestBodyFolder` — the same folder `bodyFromFile` already reads from; no new config path.
- `BaseTest.Verify`'s status-code assertion is unchanged — GraphQL tests still declare an expected HTTP status explicitly.

## Assumptions

- No new NuGet dependency in `src/ConfIT` — the `graphql` block is DTO/DSL mapping onto the existing HTTP pipeline, same technique as every other DSL field; the array-wildcard matcher change is plain Newtonsoft.Json/JsonDiffPatch manipulation.
- `example/User.Api` will take on a new GraphQL server library dependency to expose a real, spec-compliant GraphQL endpoint — a hand-rolled fake endpoint would not validate genuine GraphQL semantics (query parsing, field resolution, error shape) and would undercut confidence in the feature.
- WireMock's existing `JsonMatcher`-based body matching (`BuilderExtension.WithBodyIfProvided`) is sufficient to disambiguate mocked GraphQL operations by `query`/`operationName` content — no GraphQL-aware mock matcher is being built.
- GraphQL execution is always a single HTTP POST for this feature's scope (no batching, no subscriptions).
- JSON remains fully supported for GraphQL tests; YAML/`queryFromFile` are the ergonomic answer to multi-line query readability, not a requirement.

## Scenarios

### Scenario 1: GraphQL query executed via the `graphql` request block

A test defines a GraphQL query using the new `graphql` request block; ConfIT executes it as a `POST` call and matches the `data` field in the response using the existing matcher model — no new matching behavior needed for the success path.

**Acceptance Criteria:**
- Given a test case with a `graphql` request block containing a `query` string and no `variables`, when the test executes, then ConfIT sends a `POST` request to the configured `path` with a JSON body `{"query": "..."}` and header `Content-Type: application/json`
- Given the GraphQL server responds with HTTP 200 and a body `{"data": {...}}`, when the response is verified, then the `data` object is matched against `api.response.body` using the existing `ignore`/`pattern`/`semantic` matcher pipeline, unchanged from REST behavior
- Given no `variables` or `operationName` are specified in the `graphql` block, when the request body is built, then the JSON body omits those keys rather than sending them as `null`

### Scenario 2: GraphQL mutation executed with variables

A test defines a GraphQL mutation with input variables using the `graphql` request block; ConfIT sends both `query` and `variables` in the POST body and matches the mutation's result.

**Acceptance Criteria:**
- Given a test case with a `graphql` request block containing a mutation `query` string and a `variables` object, when the test executes, then ConfIT sends a `POST` request with a JSON body `{"query": "...", "variables": {...}}`
- Given an `operationName` is specified alongside a `query` string containing multiple named operations, when the request is built, then the JSON body includes `"operationName": "..."` so the server executes the correct operation
- Given the GraphQL server responds with HTTP 200 and a body containing the mutation's result under `data`, when the response is verified, then the `data` object is matched against `api.response.body` using the existing matcher pipeline

### Scenario 3: GraphQL query loaded from an external file via `queryFromFile`

Large or reused GraphQL queries are stored in a `.graphql`/`.gql` file and referenced by name, mirroring `bodyFromFile`, keeping test definitions readable.

**Acceptance Criteria:**
- Given a `.graphql` file in the configured `RequestBodyFolder` and a test case with `graphql.queryFromFile` set to that filename, when the test executes, then the file's raw text is sent as the `query` value in the POST body
- Given the `.graphql` file contains a multi-line query with GraphQL-native formatting (newlines, indentation), when the file is loaded, then the query text is sent byte-for-byte as the `query` string value with no JSON-escaping artifacts
- Given `variables` are also specified directly in the `graphql` block alongside `queryFromFile`, when the request is built, then the JSON body includes both the file-loaded `query` and the inline `variables`

### Scenario 4: GraphQL response returns multiple errors

A GraphQL server returns HTTP 200 with an `errors` array describing one or more failures; the new array-wildcard segment in `ignore`/`pattern` lets a test target a field across every error entry without knowing how many errors exist or their order.

**Acceptance Criteria:**
- Given a GraphQL response body with an `errors` array containing two entries, each with a `message` and an `extensions` object, when a matcher rule uses an array-wildcard segment to ignore `extensions` on every entry, then `extensions` is removed from every entry before diffing, regardless of array length
- Given the same response and an expected body listing only the `message` field for each error in order, when the response is verified, then the test passes because `extensions` was excluded from the comparison on both sides consistently
- Given a `pattern` rule targets a field across every array element (e.g. matching `message` against a regex on every error entry) and one entry's field fails the regex, when the response is verified, then the test fails and the failure identifies which array index did not match
- Given the `errors` array is empty (a successful GraphQL response with no errors), when a wildcard `ignore`/`pattern` rule targets a field inside `errors[]`, then the rule has no effect and no exception is thrown for the absent array elements

### Scenario 5: Two GraphQL operations share the same mocked endpoint

A single `/graphql` path serves multiple distinct operations; the `graphql` mock block lets a test stub two different operations against the same endpoint without them colliding.

**Acceptance Criteria:**
- Given two mock interactions, each with a `graphql` block specifying a different `query`/`operationName` against the same `path`, when both are registered via `mock.interactions`, then each is set up as a distinct WireMock stub differentiated by request body content
- Given the system under test sends a request matching the first operation's `query`/`operationName`, when the mock server receives it, then it returns the first interaction's configured response, not the second's
- Given the system under test sends a request matching the second operation, when the mock server receives it, then it returns the second interaction's configured response, not the first's

*(Scenarios ordered chronologically — natural implementation sequence. Scenario count deliberately held at 5, the framework's default max, after evaluating and declining to split the array-wildcard matcher change into a separate feature — see Open Questions and Boundary Conditions for how the two pieces stay coherent.)*

## Technical Constraints

- No new NuGet dependency in `src/ConfIT` — the `graphql` DSL block and the array-wildcard matcher segment are both implementable on top of existing dependencies (Newtonsoft.Json, JsonDiffPatch.Net).
- Per this repo's convention (CLAUDE.md, "Keeping Examples in Sync"), example coverage is required in both `User.ComponentTests` and `User.IntegrationTests`. This requires `example/User.Api` (the example system under test) to expose a real, spec-compliant GraphQL endpoint — a new GraphQL server library dependency in the example app is expected and necessary.
- `example/JustAnotherService` is not modified by this feature and stays REST-only. The `graphql` mock block is validated at the unit level (`test/ConfIT.UnitTest`, against `HttpMockServer` directly) rather than via a live outbound dependency call from the example SUT.
- The array-wildcard segment must compose with the existing `__`-joined nested-path convention without ambiguity — it is a new matcher engine capability, not a GraphQL-specific one, and its design must not change the behavior of existing single-path `ignore`/`pattern` rules.
- `BaseTest.Verify`'s HTTP status-code assertion path is a fixed existing interface — GraphQL tests use it unchanged.

## Open Questions

- [x] Which GraphQL server library and schema shape should `example/User.Api`'s new GraphQL endpoint use? **Resolved:** HotChocolate (example-only dependency, never touches `src/ConfIT`); schema exposes `userById(id)` query + `createUser(input)` mutation, both reusing existing `UserController` MediatR handlers. See [graphql-support.md](../../context/graphql-support.md).
- [x] If both `query` and `queryFromFile` are set on the same `graphql` block, should one take precedence or should this be a validation error? **Resolved:** `queryFromFile` wins silently, mirroring the existing `bodyFromFile`/`body` precedent.
- [x] Should the array-wildcard matcher segment extend to `semantic` matchers as well, or stay scoped to `ignore`/`pattern` for this feature? **Resolved:** stays scoped to `ignore`/`pattern` for this feature, per original framing.
- [x] What token denotes the wildcard segment (e.g. `*`, `[]`, `#`) in the `__`-joined path syntax? **Resolved:** `*` (e.g. `errors__*__extensions`).

- Design alignment: L4 consistent with requirement spec — no overrides.

## Links

- Design: [graphql-support.md](../../context/graphql-support.md)
- Epic index: [platform-play.md](../epics/platform-play.md)
