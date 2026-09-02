# ConfIT Examples

Working reference implementations of all three ConfIT startup modes. Every suite here runs in
CI (`make ci`), so what you read is what currently passes.

If you are setting up your own suite, read the project matching your mode below, then read
**[What is demo-specific](#what-is-demo-specific)** before copying anything.

---

## The three modes

| Mode | Project | Use it when | Fixture call |
|---|---|---|---|
| **In-process component** | [`User.ComponentTests`](User.ComponentTests) | The service is .NET and the test project can reference it. Fastest start, full DI access for seeding. | `SuiteBootstrapper.ForComponent<Startup>` |
| **Command / AppLauncher** | [`User.ComponentTests.AppLauncher`](User.ComponentTests.AppLauncher) | Any language, or a .NET app you want to exercise as a real process. ConfIT runs a shell command and speaks HTTP — the test project never references the app. | `SuiteBootstrapper.ForCommand` |
| **Integration** | [`User.IntegrationTests`](User.IntegrationTests) | Services are already running — locally, in CI, or in a deployed environment. Nothing is mocked. | `SuiteBootstrapper.ForIntegration` |

Both component modes can stub outbound dependencies with WireMock via a `mock:` block. Integration
suites cannot — a `mock:` block in an integration test definition is an error.

### Supporting projects

| Project | Role |
|---|---|
| [`User.Api`](User.Api) | The service under test — a small user API (REST + GraphQL) on port 5170 |
| [`JustAnotherService`](JustAnotherService) | An external dependency the API calls. Real on port 9999; stubbed by WireMock on 8888 during component tests |

---

## What every suite needs

These four things are structural — ConfIT does not work without them:

1. **`suite.config.yaml`** in the test project root, registered in the `.csproj` with
   `<CopyToOutputDirectory>Always</CopyToOutputDirectory>`. Its top-level key (`component:` or
   `integration:`) selects the loader.
2. **A fixture** implementing `IDisposable` that calls one `SuiteBootstrapper.For*` method,
   exposes `TestSuiteContext Context`, and disposes the suite — disposal prints the summary and
   shuts down infrastructure.
3. **A test class** deriving from `BaseTest`, using `IClassFixture<TestSuiteFixture>` so the suite
   starts once per class, with a `[Theory]` fed by
   `[MemberData(nameof(GetTestCasesForFolder), "TestCase")]` → `TestReader.GetTestsForAFolder`.
   That is the only discovery mechanism ConfIT has.
4. **Every runtime file registered in the `.csproj`** — test definitions, `suite.config.yaml`,
   body fixtures, app settings. `TestReader` reads from the build output directory, so a missing
   `<None Update>` entry is the single most common cause of "no tests discovered".

Two things that look required but are not:

- **`TestOutputLogger`** is optional. It is a three-line adapter onto xUnit's `ITestOutputHelper`;
  `BaseTest`'s logger parameter is nullable. `User.ComponentTests.AppLauncher` passes `null` and
  works fine.
- **`appsettings.Tests.json`** is needed only for `mode: in-process`, where `startup.settings`
  names it. Neither other mode has one.

---

## What is demo-specific

Everything below exists because this example happens to be a user API. **Do not carry it into
your own project.**

| Thing | Where | Why it is here |
|---|---|---|
| `User.*` namespaces, `UserComponentTests` / `UserIntegrationTests` class names | all suites | Naming for this demo |
| `UserDbInitializer` | `User.ComponentTests/SetUp/` | Shows *where* to seed (the `onStarted` hook). Your seeding will look nothing like this — and data a single test needs should be created by that test over HTTP instead |
| `JustAnotherService` | `appsettings.*.json`, mock interactions | This demo's one outbound dependency. Yours will have different ones, or none |
| Ports `5170`, `8888`, `9999`, `8887` | configs, launch profiles | Arbitrary. `5170` is the API, `8888` the WireMock stub, `9999` the real dependency, `8887` a hand-started WireMock serving OAuth2 tokens |
| `IsLocalComponentTests` flag, `Startup.AddDbContexts` branching | `User.Api` | How *this* app selects an in-memory database under test. The pattern (app owns its test environment) transfers; the flag name does not |
| Everything under `TestCase/` | all suites | Test definitions for this API's endpoints. Useful to read for DSL patterns, not to copy |
| `Microsoft.EntityFrameworkCore.InMemory` | `User.ComponentTests.csproj` | This app uses EF Core. Yours may not |
| `WireMock.Net` as a direct package | `User.ComponentTests.AppLauncher.csproj` | Only needed because that fixture hand-starts an extra WireMock server for the OAuth2 token endpoint. ConfIT's own `mock:` block needs no package reference |
| `folders.response: responses` vs `ApiResponses` | component vs integration configs | Two arbitrary names for the same thing. ConfIT imposes neither |

The `protected virtual` hooks on `User.Api.Startup` (`AddDbContexts`, `AddMvcServices`) exist as
extension points but are **not overridden anywhere in this repo** — the in-memory/SQLite choice is
driven entirely by the `IsLocalComponentTests` config flag. Treat them as available, not as
demonstrated.

---

## Running them

```bash
make component               # in-process component suite
make component.applauncher   # command mode — starts User.Api as a real process on 5170
make integration             # wipes the DB, starts both services, runs, tears down
make ci                      # everything
```

Full documentation is in [`doc/`](../doc) — start with
[Suite Setup](../doc/suite-setup.md), then [AppLauncher](../doc/app-launcher.md) for command mode.
