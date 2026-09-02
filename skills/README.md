# ConfIT Agent Skills

Three [Agent Skills](https://docs.claude.com/en/docs/claude-code/skills) that teach a coding agent
how to use ConfIT. They split by the job being done, not by the feature being used.

| Skill | Who, and when | Works from |
|---|---|---|
| [`confit-suite-setup`](./confit-suite-setup) | anyone adding ConfIT to a project | the startup mode you need |
| [`confit-component-tests`](./confit-component-tests) | a developer, while implementing | **the controller, plus the mocks behind it** |
| [`confit-integration-tests`](./confit-integration-tests) | QA or a peer, after deployment | **the API spec**, black box, no source |

The two authoring skills are genuinely different jobs. A component test is written from your own
code, mocks the dependencies, runs against fresh state, and can force a dependency to fail. An
integration test is written from a spec or a collection, hits a real deployed service, shares
persistent data with everyone else, and cannot make a dependency misbehave.

---

## Install

They ship as agent plugins. Installing a plugin brings the whole repository along, so each skill
sits next to the live [`example/`](../example) suites and [`doc/`](../doc) it reads — no templates
to go stale.

**Claude Code**

```
/plugin marketplace add techygarg/ConfIT
/plugin install confit@confit
```

**Codex** — the repository carries a [`.codex-plugin/`](../.codex-plugin) manifest.

Manifests for further agents are added over time; each is a directory at the repository root, so
adding one never touches the skills themselves.

Working inside this repository, load it as a local plugin:

```bash
claude --plugin-dir .
```

> Do not copy a skill folder on its own. Each one reads `example/` and `doc/` from the repository
> around it and refuses to run without them — by design, so it can never generate tests from a
> stale template.

---

## Then just ask

The agent picks the right skill from how you phrase it.

> Set up a ConfIT component test suite for `MyService.Api`.
>
> I just implemented `CreateOrder` — write component tests for it.
>
> The mock isn't matching and my test gets a 404.
>
> Here's our OpenAPI spec — write tests against staging.
>
> Convert this Postman collection into ConfIT tests.

---

## Two scripts you can run yourself

Both live in [`../tools/`](../tools), need no build, and exit non-zero on error, so either can gate
CI. `make skills` runs them against every suite in `example/`.

```bash
python3 ../tools/check-testcases.py path/to/TestProject
bash    ../tools/verify-suite.sh    path/to/TestProject
```

The first validates test definitions — missing expected bodies, bad `depends:`, unresolvable
`{{variables}}`, unknown or malformed matcher names, `mock:` blocks in an integration suite,
unregistered files. It reads the built-in matcher list from `src/ConfIT/Matching/SemanticMatcher.cs`
rather than a hardcoded copy, so it stays correct as the library gains matchers.

The second checks project wiring — package references, config registration, fixture shape,
framework mismatch.
