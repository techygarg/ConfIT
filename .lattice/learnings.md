# ConfIT — Operational Learnings

Patterns worth preserving from past implementation sessions. Each entry is a specific, actionable insight — not a general principle.

---

## Newtonsoft.Json auto-parses ISO 8601 strings to JTokenType.Date

**Context:** Implementing semantic matchers (`isIsoDate`, `isIsoDateTime`) in ASSERT-001.

**Pattern:** `JToken.Parse` uses `DateParseHandling.DateTime` by default. Any string that looks like an ISO 8601 date or datetime is automatically parsed into a `JTokenType.Date` token, not `JTokenType.String`. This affects both production response parsing in `BaseTest.Execute` and JSON in unit tests.

**How to apply:** Any validator that inspects a field's format must handle *both* `JTokenType.String` and `JTokenType.Date`. For date-only matchers (`isIsoDate`), check `TimeOfDay == TimeSpan.Zero` on the `Date` token to reject datetime strings that auto-converted. For datetime matchers (`isIsoDateTime`), a `Date` token is always valid — Newtonsoft already parsed it successfully.

**Alternative:** Disable date parsing with `reader.DateParseHandling = DateParseHandling.None` before calling `JToken.Load`. Not used here to avoid touching the response parsing path in `BaseTest`.

---

## When a pipeline step must mutate both `actual` and `expected`, inline it in `MatchResponseBody`

**Context:** Designing where to call `SemanticMatcher.Apply` in ASSERT-001.

**Pattern:** `ApplyMatcher` only receives `actual`. When a new matching step needs to remove validated fields from *both* sides (to prevent diff noise on the expected), threading `expected` into `ApplyMatcher` is invasive. The better place is `MatchResponseBody` itself — both `actual` and `expected` are in scope there, and `ApplyMatcher` stays unchanged.

**How to apply:** Any future matcher type that needs to modify the expected response (not just actual) should be called inline at the top of `MatchResponseBody`, before delegating to `ApplyMatcher`.
