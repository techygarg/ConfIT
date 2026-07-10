---
feature: GraphQL Support
requirement_doc: .lattice/requirements/features/int-002-graphql-support.md
created: 2026-07-10
status: complete
---

# GraphQL Support

> Declarative `graphql` request/mock DSL block riding ConfIT's existing HTTP pipeline, plus an array-wildcard segment for `ignore`/`pattern` matchers.

## Decisions Log

<!-- Add new at bottom. Never remove. -->

| Date | Decision | Reasoning | Alternatives Considered |
|------|----------|-----------|------------------------|
| 2026-07-10 | No clean-architecture/DDD layers imposed; new components map onto existing `ConfIT.*` namespaces (`Model/`, `Matching/`, `Reader/`, `Runner/Http`, `Runner/Mock`) | Matches ASSERT-001 precedent ("must fit existing codebase structure — no clean architecture or DDD layers imposed") and the post-restructuring target architecture recorded in `.lattice/insights/architecture.md` (Buckets 1-4, completed) | Generic clean-architecture Controller/Service/Domain/Repository layering — rejected: doesn't fit a non-persistence test-authoring library |
| 2026-07-10 | Design grounded directly in current source (`src/ConfIT/Model/`, `Matching/`, `Reader/`, `Runner/`), not in `CLAUDE.md` or prior context docs (ASSERT-001, DSL-002) | Those docs predate the 4-bucket architecture restructuring (`ae72bf5`, `83d51bf`) and describe a superseded `Server/Dto`, `Server/Http`, `Util/` layout. Verified current layout directly against source before designing. `CLAUDE.md` sync tracked separately, outside this feature's scope. | Trusting `CLAUDE.md` as-is — rejected: would have designed against a folder structure that no longer exists |
| 2026-07-10 | `graphql` block lands as a new `Graphql` property on `HttpTestRequest`, backed by a new `GraphqlRequest` DTO (`Model/`); no changes to `Matcher`, `HttpTestResponse`, `TestHttpClient`, `HttpMockServer`, or `BuilderExtension` | `TestHttpClient`/`HttpMockServer` only ever consume an already-composed `Body` — confirmed via source that a `graphql` block compiling down to `Body`+`Method`+headers upstream (in `TestCaseResolver`) requires zero downstream changes. WireMock's `JsonMatcher` already disambiguates mock interactions by body content, confirmed via `BuilderExtension.WithBodyIfProvided` | Flattening `Graphql`'s fields directly onto `HttpTestRequest` — rejected: requirement doc's DSL shape is an explicit nested `graphql:` block |
| 2026-07-10 | GraphQL server library for `example/User.Api`: **HotChocolate** | Dominant, actively-maintained .NET GraphQL server; integrates cleanly with `User.Api`'s classic `Startup`-based ASP.NET Core; spec-compliant `errors` array (`message`/`extensions`/`path`) directly serves Scenario 4's multi-error assertions. Dependency confined to `example/User.Api.csproj` only — not a ConfIT library dependency | GraphQL.NET — rejected: more manual schema/resolver wiring, thinner out-of-the-box error shape |
| 2026-07-10 | Example schema: `userById(id)` query + `createUser(input)` mutation, both backed by existing MediatR handlers already used by `UserController` | Reuses existing example domain instead of inventing a parallel one — consistent with example project's existing pattern | — |
| 2026-07-10 | `queryFromFile` wins silently over inline `query` when both are set | Mirrors the already-shipped `bodyFromFile`/`body` precedent in `TestCaseResolver` exactly — no new special-casing | Validation error if both set — rejected: inconsistent with existing `body`/`bodyFromFile` behavior, which has no such check |
| 2026-07-10 | Array-wildcard token: `*` (e.g. `errors__*__extensions`) | Familiar from JSONPath/glob conventions; reads naturally as "any" | `[]` — rejected: risks visual confusion next to Json.NET's own real `[n]` brackets in error messages |
| 2026-07-10 | Wildcard parent-matching implemented via normalize-and-compare (collapse `[n]` to a fixed marker on both sides, then plain string equality), not regex construction | Simpler and safer than building/escaping a regex from arbitrary user-supplied field names — avoids a whole class of metacharacter-escaping bugs | Regex-based matching (escape literal segments, translate `*` to `\[\d+\]`) — rejected: needs `Regex.Escape` on arbitrary field names to stay correct |
| 2026-07-10 | `graphql` block, when present, overwrites `Body` after existing `bodyFromFile`/`body` hydration runs | Deterministic precedence if a test accidentally sets both; avoids ambiguous merge semantics between a JSON body and a GraphQL envelope | — |
| 2026-07-10 | Design approved at Level 4. Status set to approved — ready for implementation. | All four levels walked and approved. Contracts defined. No open questions remain. | — |

## Design: Level 1 — Capabilities

1. Write a GraphQL query/mutation test declaratively — a `graphql` block (`query`, `variables`, `operationName`) instead of hand-assembling a raw JSON body; ConfIT sends it as `POST` with `Content-Type: application/json` automatically.
2. Keep large or reused queries in external `.graphql`/`.gql` files (`queryFromFile`), referenced by filename — same pattern as `bodyFromFile`.
3. Assert on a GraphQL error response without knowing how many errors it has — target a field across every entry of the `errors` array with a single `ignore`/`pattern` rule.
4. Mock two different GraphQL operations behind the same endpoint path, each differentiated by `query`/`operationName` content, not by path.

## Design: Level 2 — Components

**Core library** (all inside existing `ConfIT.*` namespaces — no new namespace, no clean-architecture/DDD layers):

| Component | Type | Namespace | File | Responsibility |
|---|---|---|---|---|
| `GraphqlRequest` | New DTO | `ConfIT.Model` | `Model/GraphqlRequest.cs` | Pure data: `Query`, `QueryFromFile`, `Variables`, `OperationName` |
| `HttpTestRequest` | Modified | `ConfIT.Model` | `Model/HttpTestRequest.cs` | Add one optional `Graphql` property |
| `TestCaseResolver` | Modified | `ConfIT.Reader` | `Reader/TestCaseResolver.cs` | New hydration step alongside existing `bodyFromFile` hydration: resolves query text, composes final `Body`, defaults `Method`/`Content-Type` |
| `ResultMatcher` | Modified | `ConfIT.Matching` | `Matching/ResultMatcher.cs` | Extend path-matching to recognize an array-wildcard segment |

Explicitly unchanged (verified against source): `TestHttpClient`, `HttpMockServer`, `BuilderExtension`, `Matcher` DTO, `HttpTestResponse`.

**Supporting — example app only, not a ConfIT dependency:**

| Component | Type | Responsibility |
|---|---|---|
| `example/User.Api` GraphQL endpoint | New | HotChocolate-backed GraphQL server exposing `userById` query + `createUser` mutation, reusing existing MediatR handlers. Dependency confined to `example/User.Api.csproj` — never touches `src/ConfIT.csproj`. |

## Design: Level 3 — Interactions

**Flow 1 — Request-side `graphql` compilation** (new step in `TestCaseResolver`, applies to `Api.Request` and every `Mock.Interactions[].Request`):
1. If `Graphql` is set: resolve query text — `QueryFromFile` wins over inline `Query` when both set.
2. Compose `Body = { query, variables, operationName }`, omitting absent keys entirely (no nulls sent).
3. Default `Method` to `"POST"` and merge `Content-Type: application/json` into `Headers` only where the author hasn't already set them.
4. Runs after existing `bodyFromFile` hydration, so `graphql` wins if both `graphql` and `body`/`bodyFromFile` are present on the same request.

**Flow 2 — Response side:** unchanged. `data`/`errors` flow through `ResultMatcher.MatchResponseBody` exactly like any REST body.

**Flow 3 — Wildcard path matching** (`ResultMatcher`):
1. `ExtractKeyAndParentPath` splits `"errors__*__extensions"` on `__` exactly as today → `key="extensions"`, `parents="errors.*"`.
2. Parent-matching normalizes both sides — collapses every concrete `[<digits>]` in the actual `Path` to a fixed `[*]` marker, and expands `*` segments in the template to the same marker — then compares normalized strings.
3. `RemoveField`'s existing full-tree recursive walk is untouched — one wildcard rule reaches every array index without knowing array length, and is a no-op on an empty array (no exception).
4. Failure attribution ("which array index didn't match") falls out for free from the existing `JsonDiffPatch`/`DeltaFormatter` diff, which reports the real concrete path of any field a `pattern` rule left in place.
5. **Invariant preserved exactly:** empty `parentsKey` still matches any parent — a hard-coded check before the normalized comparison, not folded into it.

**Flow 4 — Mock disambiguation:** unchanged mechanism. Each mock interaction's `graphql` block hydrates via Flow 1; WireMock's existing `JsonMatcher` on `Request.Body` already disambiguates by body content.

## Design: Level 4 — Contracts

**`Model/GraphqlRequest.cs`** (new): `Query`, `QueryFromFile`, `Variables` (`JToken?`), `OperationName` — plain data.

**`Model/HttpTestRequest.cs`** (modified): add `GraphqlRequest? Graphql { get; set; }`.

**`Reader/TestCaseResolver.cs`** (modified): new `HydrateGraphql(HttpTestRequest, string? requestFolder)` — throws `InvalidOperationException` if `Graphql` is set but neither `Query` nor `QueryFromFile` is set, or if `QueryFromFile` is set but `requestFolder` is null/empty. New `ResolveQueryText(GraphqlRequest, string? requestFolder)` implementing the file-wins precedence. Called after `HydratePayload` in `Resolve`'s per-request flow.

**`Matching/ResultMatcher.cs`** (modified, public signature unchanged): `IsParentMatching` now compares normalized forms instead of raw string equality (empty-`parentsKey` special case preserved as a hard-coded first check). New private helpers `NormalizeArrayIndices(string path)` and `BuildParentTemplate(string parentsKey)`, and `private const string Wildcard = "*"`.

No changes to `Matcher`, `HttpTestResponse`, `TestHttpClient`, `HttpMockServer`, `BuilderExtension`.

## Design Summary

**Status: Approved — ready for implementation**

**Components and layer assignments**
- `GraphqlRequest` — new, `ConfIT.Model` — pure data DTO
- `HttpTestRequest` — modified, `ConfIT.Model` — one new optional property
- `TestCaseResolver` — modified, `ConfIT.Reader` — new hydration step, runs after existing `bodyFromFile` hydration
- `ResultMatcher` — modified, `ConfIT.Matching` — wildcard-aware parent matching, public signature unchanged
- `example/User.Api` — new HotChocolate GraphQL endpoint (example-only, not a ConfIT dependency)

**Key contracts and interfaces**
- `GraphqlRequest { Query, QueryFromFile, Variables, OperationName }`
- `HttpTestRequest.Graphql` (new optional property)
- `TestCaseResolver.HydrateGraphql` / `ResolveQueryText` (new private methods; `InvalidOperationException` on missing query source or missing folder)
- `ResultMatcher.IsParentMatching` / `NormalizeArrayIndices` / `BuildParentTemplate` (new private helpers; `MatchResponseBody` public signature unchanged)

**Architectural constraints**
- No clean-architecture/DDD layers imposed — components map onto existing `ConfIT.*` namespaces
- No new NuGet dependency in `src/ConfIT`; HotChocolate confined to `example/User.Api.csproj`
- Zero changes to `Matcher`, `HttpTestResponse`, `TestHttpClient`, `HttpMockServer`, `BuilderExtension`
- Wildcard matching is provably backward-compatible: `NormalizeArrayIndices` is a no-op on any path with no array indices, so every existing non-wildcard `ignore`/`pattern` rule behaves identically

**Domain model decisions**
- Not applicable — DTOs stay plain POCOs consistent with the rest of `Model/`; no value objects or aggregates introduced (this is a non-persistence test-authoring library, not a business domain)

**Open questions resolved during design**
- GraphQL server library/schema for `example/User.Api` → HotChocolate; `userById` query + `createUser` mutation
- `query` vs `queryFromFile` precedence → `queryFromFile` wins silently
- Wildcard token → `*`
- Wildcard matching mechanism → normalize-and-compare, not regex construction

**Requirement doc drift check:** L4 consistent with requirement spec — no overrides. Recorded in `int-002-graphql-support.md`.

## Implementation Notes

**Status: Complete — all verification green.**

**Files created:**
- `src/ConfIT/Model/GraphqlRequest.cs`
- `example/User.Api/GraphQL/Query.cs`, `Mutation.cs`, `UserErrorFilter.cs`
- `test/ConfIT.UnitTest/Server/Mock/HttpMockServerTests.cs`
- `example/User.ComponentTests/TestCase/05-graphql.yaml` + `TestCase/Request/user-by-id.graphql`
- `example/User.IntegrationTests/TestCase/08-graphql.yaml` + `TestCase/Request/user-by-id.graphql`

**Files modified:** `src/ConfIT/Model/HttpTestRequest.cs`, `src/ConfIT/Reader/TestCaseResolver.cs`, `src/ConfIT/Matching/ResultMatcher.cs`, `test/ConfIT.UnitTest/Reader/TestCaseResolverTests.cs`, `test/ConfIT.UnitTest/Util/ResultMatcherTests.cs`, `example/User.Api/Startup.cs`, `example/User.Api/User.Api.csproj`, `example/User.ComponentTests/User.ComponentTests.csproj`, `example/User.ComponentTests/suite.config.yaml`, `example/User.IntegrationTests/User.IntegrationTests.csproj`.

**Deviations from blueprint found during implementation (all fixes, no design changes):**
- `TestCaseResolver.Resolve` clones via JSON round-trip; an unset `JToken?` property (`GraphqlRequest.Variables`) survives that round-trip as a `JValue` with `Type == JTokenType.Null`, not a true C# `null`. A plain `is not null` check missed this and always emitted `"variables": null`. Fixed by checking `Type: not JTokenType.Null`.
- The `Content-Type` "preserve if already set" check needed case-insensitive key comparison — `Dictionary<string,string>.ContainsKey` is ordinal by default and a lowercase `content-type` would have been treated as absent, inserting a duplicate key.
- HotChocolate 16 has no `[Service]` attribute; DI works via implicit parameter detection. Constructor-injecting `IMediator` into `Query`/`Mutation` root types compiles but throws `"Cannot access a disposed object"` at request time (wrong scope) — fixed by injecting as a resolver method parameter instead. Verified via schema introspection that this doesn't leak `mediator` as a spurious GraphQL argument.
- HotChocolate's default exception-to-error mapping produces no `extensions` object for unhandled resolver exceptions (only for validation errors) — added `UserErrorFilter` (mirrors the existing `UserExceptionFilterAttribute` exception-switch convention) so `errors[].extensions.code` exists, which Scenario 4's wildcard-matcher test needs.

**Verification (this session, all green):**
- `make unit` — 268/268 passed (net9.0 + net10.0)
- `make component` — 20/20 passed
- `make integration` — 29/29 passed
- No debug/temp code left behind (checked explicitly)

## Open Questions

*(none — all resolved during design)*

## Constraints

- No new NuGet dependency in `src/ConfIT` — `graphql` DSL block and array-wildcard matcher segment build on existing dependencies only.
- `example/JustAnotherService` stays REST-only; `graphql` mock block validated at unit level, not via a live outbound dependency call.
- Array-wildcard segment must compose with the existing `__`-joined nested-path convention without ambiguity; must not change behavior of existing single-path `ignore`/`pattern` rules.
- `BaseTest.Verify`'s HTTP status-code assertion path is unchanged.

## Key Files

| Path | Role |
|---|---|
| `src/ConfIT/Matching/ResultMatcher.cs` | `ExtractKeyAndParentPath`/`IsParentMatching`/`RemoveField` — target of array-wildcard segment change |
| `src/ConfIT/Model/HttpTestRequest.cs`, `HttpPayload.cs` | Request DTO — likely home for new `graphql` block |
| `src/ConfIT/Reader/TestCaseResolver.cs` | `HydratePayload` — pattern to mirror for `queryFromFile` |
| `src/ConfIT/Runner/Http/TestHttpClient.cs` | `Execute` — confirmed needs zero changes if `graphql` compiles down to `Body`+`Method`+headers upstream |
| `src/ConfIT/Runner/Mock/HttpMockServer.cs`, `BuilderExtension.cs` | Confirmed WireMock `JsonMatcher` already disambiguates mock interactions by body content — zero changes needed |
| `doc/graphql-support.md` | New Tier 2 user-facing doc — full `graphql` block reference, `queryFromFile`, mutations + `extract`, error-array matching |
| `doc/test-file-format.md`, `doc/matchers-and-patterns.md`, `doc/doc-strategy.md`, `README.md`, `doc/Package.Readme.md`, `CLAUDE.md` | Updated to reference `graphql` field and array-wildcard `*` matcher segment |
