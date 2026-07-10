# Operational Learnings

Experiential patterns from practice. Complements standards (what should be) with experience (what we keep learning).

## Design Patterns

- 2026-07-10 [design] When a pipeline step must mutate both "actual" and "expected" sides of a comparison, inline it at the point where both are already in scope (e.g. `MatchResponseBody`) rather than threading the second value into a lower-level helper.
- 2026-07-10 [design] When a new declarative DSL block is sugar that ultimately compiles down to an already-existing primitive (e.g. a JSON body + method + headers), do the compilation at the hydration/resolve stage of the pipeline, not the execution stage — verify by checking how the primitive is actually consumed before assuming new call sites are needed.

## Implementation Craft

- 2026-07-10 [implementation] Newtonsoft.Json auto-parses ISO 8601 strings to `JTokenType.Date` (`DateParseHandling.DateTime` default) — format validators must handle both `JTokenType.String` and `JTokenType.Date` tokens.
- 2026-07-10 [design] Extending an exact-match string comparison to support a wildcard segment: prefer normalize-and-compare (collapse the variable part to a fixed marker on both sides, then plain equality) over building/escaping a regex from user-supplied literal segments — avoids metacharacter-escaping bugs entirely.
- 2026-07-10 [implementation] A JSON serialize/deserialize round-trip (e.g. for deep-cloning a DTO) does not preserve C# `null` for `JToken?`-typed properties — it produces a `JValue` with `Type == JTokenType.Null` instead. A plain `is not null` check silently misses this; check `Type: not JTokenType.Null` when the value passed through a JSON round-trip.

## Quality Signals

- 2026-07-10 [design] A config/DTO field with no consuming extension method or validation path is inert dead code — worse than absent, since it silently accepts input and does nothing. Grep for a consuming call site before assuming a new field works.

## Reliability

<!-- Bug root causes, failure modes, fragile areas, boundary condition gaps -->

## Structural Health

- 2026-07-10 [design] Context docs and CLAUDE.md can go stale after a structural refactor — cross-check git log for refactor commits and verify file paths against actual source before trusting a context doc's claims.
