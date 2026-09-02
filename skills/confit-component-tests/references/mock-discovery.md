# Mock Discovery

The half of a component test that a contract cannot give you. An OpenAPI document for
`POST /api/user` says "post a user, get an id or a 400". It cannot tell you that the handler makes
three outbound calls, or that the 400 is triggered by what one of them returned.

Everything here is about the **outbound** contract: what the service under test calls, and how its
answers change the service's behaviour.

---

## 1. Trace the call path

Controller → handler → outbound client. The client is whatever the project calls it: a typed
`HttpClient`, a provider, a gateway, a repository, an SDK wrapper.

```bash
# .NET
grep -rn "AddHttpClient\|HttpClient\|BaseAddress" --include='*.cs' <service source>

# any language — find where a base URL is read from config, then find its callers
grep -rn "http://\|https://" <service config files>
```

For each outbound call, record:

| | |
|---|---|
| method | `GET`, `POST`, … |
| path | including path parameters as they will actually be sent |
| query params | every one the client attaches |
| headers | only those the client actually sets |
| body | the shape, for `POST`/`PUT`/`PATCH` |

**Count matters.** One inbound request can produce several outbound calls, and each needs its own
interaction. In ConfIT's own example, `CreateUserCommandHandler` calls a single dependency three
different ways:

```
VerifyByGet(email)       GET  /api/demo/{email}
VerifyByPost(email)      POST /api/demo          {"email": "..."}
VerifyByGetQuery(email)  GET  /api/demo?email=...
```

Three calls, three interactions. Nothing in the inbound contract hints at that number.

## 2. Find the branch points

While reading the handler, note every condition that changes the outcome — each `throw`, each
guard, each early return — and what feeds it. This is what makes error tests writable.

Same example:

```csharp
if (!isValid1.IsValid || !isValid2.IsValid || !isValid3.IsValid)
    throw new BadRequestException("Invalid Email.");
```

So the 400 is driven by a **mock response**, not by the request body. Flip any one of the three
stubs to `isValid: false` and the API returns 400. That is the entire error test — see
`<root>/example/User.ComponentTests/TestCase/02-user-errors.yaml`.

This is the component mode's unique power: you can make a dependency answer anything, including
things that never happen in a healthy environment.

| To test | Make the stub return |
|---|---|
| a validation/business rejection | the value the guard rejects |
| the dependency being down | `statusCode: 503` |
| the dependency returning nothing | an empty body or empty collection |
| a partial/degraded response | the payload with a field missing |

## 3. Check the wiring before writing anything

The application, not ConfIT, decides where its dependencies point. The dependency's base-URL
config key must resolve to `component.mock.url` in whatever settings the test run uses — the
`startup.settings` file for in-process mode, or the app's own environment-selected config in
command mode.

If it does not, every component test silently calls the real service. Tests may still pass, for
the wrong reason. That is a suite wiring defect: hand it to `confit-suite-setup`.

## 4. Declare the narrowest stub that identifies the call

WireMock matches on **every field the interaction declares**. An over-specified stub — a header
the client does not actually send, a query param it omits — simply never matches, and the request
falls through to a `404`. The test then fails on an unexpected status with nothing pointing at the
mock.

So: declare the least that uniquely identifies the call, and widen only when two stubs collide.
The three `/api/demo` stubs above are distinguished by path shape, query param and body
respectively — nothing more.

```yaml
mock:
  interactions:
    - request:
        method: GET
        path: /api/demo/test@test.com
      response:
        statusCode: 200
        body: &valid { isValid: true }        # anchor the repeated body
    - request:
        method: GET
        path: /api/demo
        params: { email: test@test.com }
      response: { statusCode: 200, body: *valid }
    - request:
        method: POST
        path: /api/demo
        body: { email: test@test.com }
      response: { statusCode: 200, body: *valid }
```

Body matching is structural and order-insensitive, so a partial body is a legitimate narrowing
tool. `Content-Type: application/json` is added to every mock response automatically.

---

## 5. When there is no source to read

An AppLauncher suite against a Go, Node or Python service; a third-party dependency; a binary you
cannot see inside. **Let the service tell you what it calls.**

1. Turn on mock logging in `suite.config.yaml`:

   ```yaml
   component:
     mock:
       url: http://localhost:8888
       enableLogs: true
   ```

2. Write the test with **no `mock:` block at all**, and run it.

3. WireMock answers every unmatched call with `404` and logs it. Read the log:

   ```
   [Warn] : HttpStatusCode set to 404 : No matching mapping found
       "Path": "/api/demo/test@test.com",
       "Url": "http://localhost:8888/api/demo/test@test.com",
       "Status": "No matching mapping found"
   ```

   Every unmatched entry is one outbound call, with its method, path, query and body. That listing
   *is* the outbound contract.

4. Write the interactions from what you observed, remove `enableLogs`, and re-run.

**The test result during this loop is meaningless — read the log, not the verdict.** A test can
easily pass while every mock is missing: unmatched calls return `404`, the service treats that as
a failed dependency check, and returns the very error the test expected. Passing proves nothing
here.

This works in any language, because it observes HTTP rather than code. It is also the fastest way
to *confirm* a hand-written stub set is complete: if any unmatched request appears in the log, an
interaction is missing or over-specified.

---

## 6. When a mock does not match

Symptoms: an unexpected `404` from the dependency, or the service returning its
dependency-failure behaviour when you expected the happy path.

1. Set `enableLogs: true` and re-run. The log shows what actually arrived.
2. Compare it field by field against the interaction. Method, path, **every** declared query
   param and header must match exactly.
3. Suspect over-specification first — remove declared fields until it matches, then add back only
   what is needed to distinguish it from other stubs.
4. Check the call is even reaching WireMock: if the log shows nothing, the dependency's base URL
   is not pointed at `mock.url` (see §3).
