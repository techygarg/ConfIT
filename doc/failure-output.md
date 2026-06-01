# Reading Failure Output

When a test fails, ConfIT gives you field-level output rather than a raw JSON dump. Each failing field gets its own block showing exactly what was expected and what arrived. This doc explains how to read that output and what common patterns mean.

---

## Field-Level Body Failure

For each field that differs between expected and actual, ConfIT prints a block:

```
user.name
  expected: "alice"
  actual:   "bob"
```

There are three change types:

**Modification** — the field exists in both bodies but the values differ. Both expected and actual are shown:

```
status
  expected: "active"
  actual:   "pending"
```

**Missing field** — the field is in your expected body but absent from the actual response. The actual line shows `<missing>`:

```
email
  expected: "alice@example.com"
  actual:   <missing>
```

This usually means the API is not returning a field your test expects. Check the API response schema or look at the full "Actual:" body printed above the failure.

**Unexpected field** — the field is in the actual response but absent from your expected body. The expected line shows `<absent>`:

```
internalId
  expected: <absent>
  actual:   "srv-9182"
```

This usually means the API is returning extra fields your test didn't account for. Either add the field to `matcher.ignore` to suppress the check, or add it explicitly to your expected body. See [matchers-and-patterns.md](matchers-and-patterns.md) for matcher configuration.

The header line `Response body mismatch:` appears once above all the field blocks for a given test.

---

## Nested Field Paths

Paths use dot notation for object nesting and bracket notation for arrays.

**Object nesting** — each level separated by a dot:

```
user.address.city
  expected: "London"
  actual:   "Paris"
```

**Array elements** — the array name followed by the zero-based index in brackets:

```
items[1].id
  expected: "abc-123"
  actual:   "xyz-456"
```

**Mixed** — arrays and objects can be combined arbitrarily:

```
orders[0].lines[2].sku
  expected: "WIDGET-99"
  actual:   <missing>
```

---

## Status Code Failure

Status code mismatches are reported by FluentAssertions directly, before any body diff runs:

```
Expected response.StatusCode to be NotFound (404), but found OK (200).
```

Nothing unusual to interpret here. If the status code matches, execution continues to the body diff.

---

## What the Matcher Pipeline Means for Failures

Before the diff runs, ConfIT applies any matchers you have configured. Fields handled by `semantic`, `pattern`, or `ignore` matchers are removed from both sides before the diff. This means:

- A field that **passes** its semantic or pattern check is removed before the diff and will not appear in the failure output — even if the values were technically different. That is the intended behaviour.
- A field that **fails** its semantic or pattern check triggers a separate assertion message before the diff runs. You will see that message in the output above the `Response body mismatch:` block.
- A field listed in `ignore` is stripped unconditionally from both sides and never appears in the diff.

If you expected a field to appear in the diff but it does not, check whether it is covered by a matcher. See [matchers-and-patterns.md](matchers-and-patterns.md).

---

## Suite Summary Table

At the end of a suite run, `TestResultCollector` prints a grouped summary:

```
══════════════════════════════════════════════════════
  Suite Summary
══════════════════════════════════════════════════════

  errors.json
    ✓  ShouldReturnErrorIfUserNotExist               43ms
    ✗  ShouldNotCreateAUser_WhenValidationFails       89ms

  user.json
    ✓  ShouldCreateAUser                            112ms
    ✓  ShouldGetUserById                             38ms

──────────────────────────────────────────────────────
  Total: 4   ✓ 3 passed   ✗ 1 failed   ⏭ 0 skipped
──────────────────────────────────────────────────────
```

The table prints once when the suite finishes, grouped by the source file each test came from. Failed test names are highlighted. This lets you see at a glance which file has failures without scrolling through all the per-test output.

See [suite-setup.md](suite-setup.md) for how to wire up `TestResultCollector` in your fixture.

---

## Debugging Tips

**Read the full bodies first.** Before the failure message, ConfIT prints the complete actual and expected bodies:

```
Actual:   {"id": 42, "name": "bob", "status": "pending"}

Expected: {"name": "alice", "status": "active"}
```

These are printed before any matcher processing, so they reflect what the API actually returned and what your test definition says. Check here first before digging into individual field failures.

**Enable mock server logs for component tests.** If a component test fails and the error looks like an unexpected response rather than a field mismatch, the mock may not have matched the incoming request. Set `EnableMockServerLogs = true` in `SuiteConfig` to see WireMock's incoming request log. This shows exactly what request arrived and why the mock responded the way it did.

**`<missing>` usually means the API is not returning a field.** Verify the API response schema. Check the "Actual:" body above the failure to confirm the field is absent.

**`<absent>` usually means the API is returning extra fields.** Add them to `matcher.ignore` to suppress the check, or add them to your expected body to assert their values explicitly.

**Skipped tests appear in the summary but not in the failure output.** If a test count looks off, check the `⏭` skipped count in the summary footer. See [test-filtering.md](test-filtering.md) for how filters interact with which tests run.
