# ConfIT

ConfIT is a .NET library for declarative API integration testing. Tests are defined in JSON or YAML files. ConfIT handles the rest.

---

## Philosophy

Most API test suites consist of the same four things repeated: build a request, send it, assert the response, clean up. Writing that in C# for every test produces hundreds of lines of code that say the same thing differently. It is hard to scan, hard to contribute to, and easy to get wrong.

ConfIT is built on a different premise: **a test should be data, not code**. You describe what to send and what to expect. ConfIT executes it. The majority of real-world scenarios — happy paths, error cases, mock interactions, dynamic fields, data flowing between tests — are expressible without writing any C#.

The result is test files that anyone on the team can read, review, and add to — engineers, QA, and non-engineers alike.

---

## What It Does

**One format, two test levels.** The same JSON or YAML definition works in a component suite (service runs in-process, dependencies mocked via WireMock) and an integration suite (real services, real network). You switch modes in configuration, not in the tests.

**Rich assertions, no assertion code.** The `matcher` block handles dynamic fields declaratively:
- `ignore` — exclude fields from comparison (IDs, timestamps)
- `pattern` — validate a field with a regex, then exclude it from the diff
- `semantic` — type-aware named assertions: `isUuid`, `isIsoDate`, `isEmail`, `greaterThan(n)`, `hasLength(n)`, and more

**Declarative data flow.** `extract` captures values from responses. `{{varName}}` injects them into later tests. The create-then-retrieve pattern needs no C#.

**GraphQL support.** A `graphql` block (`query`/`queryFromFile`/`variables`/`operationName`) composes the request envelope for you — the same matchers, `extract`, and mocking apply to GraphQL endpoints as to REST.

**Declarative auth.** Bearer, OAuth2 client credentials, and API key auth are configured in `suite.config.yaml`. No custom C# provider for the common cases.

**Language-agnostic testing.** The `AppLauncher` mode starts any process from a shell command and speaks HTTP. Test a Go API, a Node.js service, or a Python microservice with the same test files. The service manages its own test environment; ConfIT just invokes the command.

**Minimal fixture setup.** `SuiteBootstrapper.ForComponent`, `ForCommand`, and `ForIntegration` wire the entire suite from a single `suite.config.yaml` call. A typical fixture is five lines.

---

## Quick Look

```yaml
# TestCase/user.yaml
CreateUser:
  tags: [smoke, user]
  api:
    request:
      method: POST
      path: /api/users
      body:
        name: Alice
        email: alice@example.com
    response:
      statusCode: 201
      body: {}
      extract:
        userId: $.body.id
      matcher:
        semantic:
          id: isUuid

GetUser:
  depends: [CreateUser]
  api:
    request:
      method: GET
      path: /api/users/{{userId}}
    response:
      statusCode: 200
      body:
        name: Alice
        email: alice@example.com
      matcher:
        ignore: [createdAt]
```

```csharp
// One-line fixture setup
_suite = SuiteBootstrapper.ForComponent<Startup>("suite.config.yaml");

// One-line test execution
await Execute(testName, test, sourceFile);
```

---

## Full Documentation

→ [github.com/techygarg/ConfIT](https://github.com/techygarg/ConfIT)
