# Documentation Strategy

This file tracks which documents exist, which are missing, and the conventions that keep them consistent. It is a working planning document — not published to users.

---

## Current State

| File | Status | What it covers |
|---|---|---|
| `matchers-and-patterns.md` | ✅ complete | `ignore`, `pattern`, `semantic`, nested paths, custom matchers |
| `variable-extraction-and-injection.md` | ✅ complete | `extract`, `{{inject}}`, `${ENV}`, error cases, migration from `ITestProcessor` |
| `test-file-format.md` | ✅ complete | DSL structure, all fields, JSON and YAML format |
| `suite-setup.md` | ✅ complete | SuiteConfig, fixture, BaseTest, TestFilter, TestResultCollector |
| `mock-interactions.md` | ✅ complete | WireMock stubs, request matching, YAML anchor reuse |
| `test-filtering.md` | ✅ complete | RUN_POOLS, RUN_TESTS, CI patterns |
| `extending-confit.md` | ✅ complete | IAuthTokenProvider, ITestOutputLogger, ITestProcessor, custom matchers |
| `failure-output.md` | ✅ complete | Field-level failure messages, path notation, suite summary, debugging tips |
| `doc-strategy.md` | ✅ this file | Planning only |

---

## Document Inventory — What Should Exist

### Tier 1 — Foundation (everyone needs these)

| Document | Status | One-liner |
|---|---|---|
| `suite-setup.md` | ✅ | How to install, configure, and wire ConfIT into an xUnit project |
| `test-file-format.md` | ✅ | Full DSL reference — all fields, JSON and YAML |
| `matchers-and-patterns.md` | ✅ | Asserting on dynamic fields without writing code |
| `variable-extraction-and-injection.md` | ✅ | Passing data between tests declaratively |

### Tier 2 — Features (go deeper once foundation is read)

| Document | Status | One-liner |
|---|---|---|
| `mock-interactions.md` | ✅ complete | Declaring WireMock stubs inline, `bodyFromFile`, request/response matching |
| `test-filtering.md` | ✅ complete | `RUN_TESTS`, `RUN_POOLS`, `TestFilter` factory methods, CI usage |

### Tier 3 — Reference (for advanced use or extension)

| Document | Status | One-liner |
|---|---|---|
| `extending-confit.md` | ✅ complete | `ITestProcessor`, `IAuthTokenProvider`, custom semantic matchers |
| `failure-output.md` | ✅ complete | Reading per-field failure messages, debugging a failing suite |

---

## README Restructure (complete)

README rewritten as:

1. **3-paragraph intro** — what ConfIT is, who it's for, one sentence on component vs integration tests
2. **Quick start** — install NuGet, point to `suite-setup.md`
3. **Feature index** — one-line entry per doc, linked

The current README has inline DSL reference, inline matcher tables, inline examples — all of that moves into individual docs and the README becomes the entry point.

---

## Conventions

**One concept per file.** If a document needs to say "for X, see the Y section of Z", that's a signal to split.

**Feature-first, not API-first.** Lead with the user problem and the DSL, not with the class or method name. Save implementation details for the `extending-confit.md` doc.

**Live examples are mandatory.** Every significant code snippet must end with a `📄 Live example:` link pointing to a real test file in `example/`. Broken links are caught in review.

**Prefer component tests for examples.** They are self-contained (no external services). Mention integration tests only when the behaviour differs.

**Code snippet format.** Use JSON as the primary snippet format. When YAML is relevant, show it as an alternative after the JSON, not instead of it.

**Length target.** Each doc should be readable in under 10 minutes. If a page scrolls longer than `matchers-and-patterns.md`, consider splitting.
