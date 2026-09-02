# Suite Troubleshooting

Symptom → cause → fix, for failures that happen *around* the tests rather than inside them. For a
test that runs and fails its assertions, read the field-level output instead.

Working configurations for every case below live in `<root>/example/`; the prose reference is
`<root>/doc/`.

---

## Discovery and startup

### The run reports zero tests

`TestReader` reads from the **build output directory**, not the source tree. A test file with no
`.csproj` entry does not exist at runtime.

```xml
<None Update="TestCase\01-lifecycle.yaml">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</None>
```

Confirm what actually shipped by listing the `TestCase` folder under the project's build output.
The same applies to `suite.config.yaml`, the in-process settings file, and every `bodyFromFile`
fixture. `PreserveNewest` is fine until an edit lands with an older timestamp — prefer `Always`
for test data.

### `Suite config file not found`

Same cause, for the config file. It is read by filename relative to the working directory, which
is the output directory during a test run.

### Every test is skipped

A tag filter is active and the tests carry no `tags:`. With `strategy: tags`, an untagged test is
skipped whenever the filter's env var is set. Either tag the tests, or unset the variable.

Check that no fixture calls `Environment.SetEnvironmentVariable` for the filter variable — a
hardcoded filter in fixture code hides tests from CI while the suite still reports green.

### The app never becomes ready (command mode)

`AppLauncher` throws with the elapsed timeout, the command it ran, and the readiness target it
was polling. Read those three values first, then work down:

1. The command fails immediately — run it by hand **from the test output directory**, since
   relative paths in `startup.command` resolve from there.
2. The app listens on a different port or path than the probe checks.
3. First run includes a build. Raise `timeoutSeconds`, or pre-build and pass a no-build flag.
4. The app is waiting on a dependency that is not up yet.

The probe checks on each interval whether the process already exited, and reports the exit code
and captured output when it has — read that before raising the timeout.

### Port already in use

A previous run's process survived. Add a `stopCommand` that kills by port; `<root>/doc/app-launcher.md`
has platform-specific forms, and `<root>/example/User.ComponentTests.AppLauncher/suite.config.yaml`
shows one in place. `AppLauncher` verifies the port is free before starting, so this surfaces at
startup rather than mid-run.

### The suite summary never prints

The fixture is not disposing `BootstrappedSuite`. `Dispose()` prints the summary and then shuts
infrastructure down — a fixture that swallows it loses both.

---

## Mocking

### Outbound calls reach the real dependency

The application, not ConfIT, decides where its dependencies point. Its test configuration must
aim the dependency's base URL at the `mock.url` from `suite.config.yaml`. In in-process mode that
is the settings file named by `startup.settings`; in command mode the launched app reads its own
configuration, usually selected by an environment variable the command sets.

### A test fails with an unexpected status and the mock looks correct

WireMock returns `404` for any outbound request matching no stub, and the service then fails in
its own way. Set `EnableMockServerLogs` on `SuiteConfig` to see what actually arrived, then
compare against the interaction: method, path, and **every** declared query parameter and header
must match exactly; body matching is structural. An interaction that over-specifies — a header
the client does not send — never matches.

### Mock interactions are ignored entirely

`mock.url` is missing from `suite.config.yaml`, so no mock server was created. In an integration
suite this is by design — remove the `mock:` blocks from the test definitions.

---

## Auth

### The `Authorization` header is missing

The `auth:` block must sit inside the section actually in use — `component:`, or the *active*
integration environment. An `auth:` block under an inactive environment does nothing.

### OAuth2 fails at startup

The token request happens once, eagerly, when the provider is constructed. When the token
endpoint is itself a stub started by the fixture, that server must be running **before**
`SuiteBootstrapper` is called. `<root>/example/User.ComponentTests.AppLauncher/SetUp/TestSuiteFixture.cs`
shows the ordering, and `<root>/doc/auth-profiles.md` explains it.

### `Unresolved env var`

Export it before the run. The failure is deliberate — a missing secret fails loudly at load
rather than producing 401s test by test.

---

## State and ordering

### Tests pass alone and fail in a full run

Shared state. Files load alphabetically and share one variable store, so a test that depends on
data created earlier is coupled to that ordering. Two fixes, in order of preference:

1. Make the test self-sufficient — create what it needs in its own file, chained with
   `extract` / `{{inject}}` / `depends`.
2. Move it into the file that creates the state, positioned after its prerequisite.

`depends:` cannot cross files, so ordering alone gives no protection when a prerequisite fails.

### `UndefinedVariableException` for a variable that clearly exists

The producing test did not pass — `extract` runs only on success — or it runs later in the
alphabetical file order. Add `depends:` on the producer so the dependent skips with a readable
reason instead of failing on the missing variable.

### `AmbiguousVariableException`

Two tests extracted the same short name. Reference it with the full prefix,
`{{TestName.varName}}`, or rename one of them.

---

## Environments

### The wrong environment is used

Resolution order: the `environment:` argument to `ForIntegration`, then `TEST_ENVIRONMENT`, then
the `default:` key in the YAML. An argument hardcoded in the fixture wins over the environment
variable and makes CI unable to switch — prefer leaving it unset.

---

## Building and running

### The suite builds locally but not in CI

Check the target framework against the `TargetFrameworks` in `<root>/src/ConfIT/ConfIT.csproj`.
Also confirm every file the suite reads is committed *and* registered — a locally present file
that was never added to git produces "no tests discovered" only on the CI machine.

### Where to look first

```bash
bash <root>/tools/verify-suite.sh <path to test project>
```

It reports package references, config presence and registration, fixture and test-class shape,
hardcoded filters, and framework mismatch — before any of them turn into a confusing test
failure.
