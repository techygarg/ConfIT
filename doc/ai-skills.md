# AI Agent Skills

ConfIT ships three [Agent Skills](https://docs.claude.com/en/docs/claude-code/skills) that teach a
coding agent how to use this library. They live in [`skills/`](../skills) at the repository root.

| Skill | Who, and when | Works from |
|---|---|---|
| [`confit-suite-setup`](../skills/confit-suite-setup) | anyone adding ConfIT to a project | the startup mode you need — in-process, command/AppLauncher, or integration |
| [`confit-component-tests`](../skills/confit-component-tests) | a developer, while implementing | the controller, plus the mocks behind it |
| [`confit-integration-tests`](../skills/confit-integration-tests) | QA or a peer, after deployment | an API spec, a collection, or a live endpoint — black box |

They are **not** part of the `ConfIT` NuGet package. `dotnet add package ConfIT` gives you the
test runner; an agent plugin gives you the know-how to drive it.

---

## Why authoring is two skills

The two authoring skills produce nearly the same artifact — the same lifecycle scenario is 89
lines as a component test and 64 as an integration test, differing only by a `mock:` block. But
they are different jobs:

| | Component | Integration |
|---|---|---|
| Primary input | the controller in code | the API spec |
| Reads deeper? | yes — far enough to find the mocks | no; stays at the contract |
| Repo access | full | often none; may be a separate repository |
| Dependencies | mocked; discovering them is half the work | real |
| State | fresh per process | shared, persistent |
| Matcher instinct | exact values are safe | lean on `semantic` / `ignore` |
| Can force a dependency failure? | yes — that is the point | no |

Splitting on the persona rather than the feature means each skill can be direct rather than
hedged. The integration skill contains no advice that assumes repo access; the component skill
contains no advice about shared-environment data hygiene.

What they share — the DSL field table and matcher decision order — is roughly 500 words, and each
carries its own copy tailored to its persona. Depth routes to `doc/` for both.

---

## Install

**Claude Code**

```
/plugin marketplace add techygarg/ConfIT
/plugin install confit@confit
```

Update with `/plugin update confit`, remove with `/plugin uninstall confit`. This repository is
its own marketplace — [`.claude-plugin/marketplace.json`](../.claude-plugin/marketplace.json)
declares it, and [`.claude-plugin/plugin.json`](../.claude-plugin/plugin.json) declares the plugin
whose `skills/` folder is the one documented here.

**Codex** — [`.codex-plugin/plugin.json`](../.codex-plugin/plugin.json) carries the equivalent
manifest, pointing at the same `skills/` folder.

**Other agents** are onboarded by adding one more manifest directory at the repository root. That
is the whole integration: a manifest names `./skills/`, and nothing about the skills changes.

**Working inside this repository**, load it as a local plugin rather than copying anything:

```bash
claude --plugin-dir .
```

---

## Why plugins, and not an install script

A plugin install clones the **whole repository** into the agent's plugin cache — not just
`skills/`. That single fact removes an entire category of problem.

These skills need to show a consumer *correct, current* configuration, fixtures and test
definitions. Anything they carried as a template would be a copy that drifts: `example/` took 21
commits in twelve months, and the files such templates mirror churned hardest of all — one
`.csproj` alone changed 13 times, and the fixture's shape changed materially when
`SuiteBootstrapper` was introduced. A stale sample is worse than none, because an agent trusts it
over the real repository.

Because the plugin brings the repository, each skill reads [`example/`](../example) — suites
verified by `make ci` — and [`doc/`](../doc) directly. Nothing to sync, nothing to version.

### Locating the reference

Each skill resolves the repository root by walking up from its own physical location, looking for
a directory that holds `example/`, `doc/` and `skills/`:

```bash
bash skills/<skill>/scripts/reference-path.sh          # prints the root
bash skills/<skill>/scripts/reference-path.sh --check  # and what it found
```

Symlink-safe, and works from a plugin cache, a clone, or a local `--plugin-dir` load. When it
cannot find the reference it exits non-zero with reinstall instructions and the skill stops — it
will not fall back to generating tests from memory.

Consequence worth stating plainly: copying a skill folder on its own, away from the repository,
gives you a skill that refuses to run. That is the intended failure.

---

## The bundled scripts

Both live in [`../tools/`](../tools) rather than inside a skill, since two skills share the first
one. Neither needs a build, both exit non-zero on error, and `make skills` runs them against every
suite in `example/` as part of `make ci`.

**Validate test definitions before running them:**

```bash
python3 tools/check-testcases.py path/to/TestProject
```

Reports what ConfIT would otherwise raise at load time or on the first failing assertion: a
response with no expected body, `depends:` naming an unknown or later test, `{{variables}}` that
are never extracted or are ambiguous, `bodyFromFile` pointing at a missing file, `mock:` blocks in
an integration suite, unregistered test files, and matcher problems — an unknown or misspelled
matcher name, a missing closing parenthesis, a wildcard or array index in a `semantic` path, or a
trailing `*`. The built-in matcher list is read from `src/ConfIT/Matching/SemanticMatcher.cs`, and
names registered as custom matchers in the project's own C# are accepted, so the check stays
correct as the library and the project evolve.

**Check a suite's wiring:**

```bash
bash tools/verify-suite.sh path/to/TestProject
```

Reports missing package references, unregistered config or test files, a fixture that does not
match the config section, a hardcoded filter env var, and framework mismatches — reading the
supported frameworks from `src/ConfIT/ConfIT.csproj` rather than a hardcoded list.

---

## What is inside a skill

```
skills/
  confit-suite-setup/
    SKILL.md                             locate reference → decide mode → read → adapt → verify
    references/troubleshooting.md
  confit-component-tests/
    SKILL.md                             controller → mocks → matrix → write → verify
    references/mock-discovery.md         trace the call path, branch points, narrowest match,
                                           the observation loop for when there is no source
    references/writing-tests.md          DSL table, matcher order, example index, diagnosing
  confit-integration-tests/
    SKILL.md                             contract → environment/auth → matrix → write → verify
    references/environments-and-auth.md  environment precedence, the three auth profiles,
                                           the OAuth2 no-refresh caveat, auth tests worth writing
    references/state-and-data.md         shared state, ${RUN_ID} uniqueness, trailing-DELETE
                                           cleanup, what the library does not support
    references/writing-tests.md          the same skeleton, tailored to black-box instincts
```

Every skill also carries `scripts/reference-path.sh`. Only `SKILL.md` enters the agent's context
when a skill triggers; reference files load when needed.

---

## Maintaining them

**No skill may gain template files.** New setup or test patterns belong in `example/`, where CI
runs them. If a skill needs to teach something new, teach it by pointing at the example that
demonstrates it.

`skills/` must stay at the repository root: the plugin manifests name `./skills/`, and the
reference resolver expects `example/`, `doc/` and `skills/` to be siblings.

`example/README.md` is the curation layer — it names what is structural and what is
demo-specific. Keep it accurate when the example projects change; it is what stops an agent
copying `UserDbInitializer` or a port number into someone else's project.

One caveat that is easy to forget: `example/User.IntegrationTests` is not a template for a
deployed environment. It uses hardcoded data and relies on `make integration` wiping the database
first. The integration skill says so explicitly — keep that warning in place.
