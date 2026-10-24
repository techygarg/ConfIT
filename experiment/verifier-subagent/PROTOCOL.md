# Protocol and pre-registration

Written 21 Oct 2026, after calibration and probe P1 and before any pilot or matrix result was seen. The Part 1 predictions in section 5 are frozen from this point; the Part 2 and Part 2b predictions in section 5b were added, and frozen, before their runs. Any later change to this protocol is logged in section 7 with a reason.

## 1. Question

When does a verifier subagent protect the orchestrator's context, and what does that protection cost?

## 2. Setup

| Item | Value |
|---|---|
| Repository | this repository at `a4f219e`, exported read-only with `git archive` into a disposable workspace outside any git repository (`$WS/confit`). |
| Verification config | `inputs/config/verification.yaml`: stages `make build`, `make unit`, `make component`. Runner: Lattice 2.1.0 `run-verification.sh` (sha256 `741a7a9a…`). |
| Host | Claude Code 2.1.306, headless (`claude -p`), auto-update disabled |
| Model (Part 1) | `claude-sonnet-5-5` for the main session and every subagent (`CLAUDE_CODE_SUBAGENT_MODEL`), effort `medium` (the default observed in P1) |
| Environment | `env -i` with a fixed PATH; `--setting-sources project,local`; `--strict-mcp-config`; Lattice loaded with `--plugin-dir`; no project CLAUDE.md gate block. The operator's global `~/.claude/CLAUDE.md` (a short git-commit rule) still loads in every run and is left in as a constant. |
| Permissions | `--permission-mode dontAsk`, allowed tools `Bash, Read, Grep, Glob, Agent` for every arm |
| Bash timeout | `BASH_DEFAULT_TIMEOUT_MS=600000` for every arm, matching what the verifier requests for itself |
| Warm start | Before each session the harness runs every stage once and discards the result, so each arm starts from the same build state |
| Isolation between runs | The workspace is reset by content after each run. Files the session changed are recorded. Any memory the session wrote is moved into the run's evidence. |

## 3. Arms, scenarios, configs

Prompts are in `inputs/prompts/arm-*.md`. They share one information need: pass or fail; if it fails, the failing tests (count and grouping when there are many) and the assertion for each cause; no file changes.

| Arm | Mechanism |
|---|---|
| A | raw `make build && make unit && make component` in the main session |
| B | `run-verification.sh` in the main session, then read summary.json; may open a failed stage's log |
| C | a general-purpose subagent runs the raw command; the main session answers from its report |
| D | the `lattice:verifier` subagent; may open a failed stage's log |

| Scenario | Patch | Ground truth (calibration) |
|---|---|---|
| G | none | all 3 stages pass |
| R1 | `inputs/scenarios/R1-few.patch` (rename the `<absent>` sentinel) | 1 test fails on both TFMs: `DeltaFormatterTests.Format_FieldInActualNotInExpected_ShowsAbsentSentinel` |
| R3 | `inputs/scenarios/R3-many.patch` (lowercase semantic matcher names) | 39 tests fail on both TFMs (78 failure lines), all `Unknown semantic matcher` |

| Config | What changes | Use |
|---|---|---|
| normal | Makefile `TEST_OPTS` logger `verbosity=normal` (one line per test) | primary: every arm, every scenario, n = 3 |
| quiet | the Makefile as committed (`verbosity=minimal`) | control: arms A and D on G and R1, n = 2 |

Order: repeats interleaved over time; arm order shuffled within each scenario block with a fixed seed.

## 4. What was known before predicting

**Calibration** (runner only, no agent): bytes each arm would face.

| Scenario | Config | Raw output | summary.json | Raw / summary |
|---|---|---|---|---|
| G | quiet | 33,313 | 1,093 | 30x |
| R1 | quiet | 29,666 | 957 | 31x |
| R3 | quiet | 131,532 | 957 | 137x |
| G | normal | 144,138 | 1,093 | 132x |
| R1 | normal | 97,591 | 957 | 102x |
| R3 | normal | 286,437 | 957 | 299x |

- 22,881 bytes of every run is the build stage: 76 NuGet vulnerability-audit warnings (NU1902, NU1903) for the transitive `Scriban.Signed` 7.2.0, repeated per project and TFM. The same warnings appear in this repository's ordinary builds, outside the experiment.
- Warm stage times are seconds (the whole suite takes 4 to 7 s), so the Bash timeout is not a factor.
- A flaky port test exists. `TestPort.GetFree()` releases a port before the test binds it. It caused 2 failures in 40 unit runs, in two different tests. Running the TFMs serially did not remove it (1 of 15 serial runs failed, 0 of 15 parallel), so the repository is left as committed.

**Probe P1**: Claude Code 2.1.306 does not cut the middle out of large outputs. An output of 127 KB was saved to a file and the model saw a 2,248-character head preview plus the path. The transcript's own copy is capped at 30,000 characters. The commonly described behaviour (a 30,000-character cap that keeps the head and the tail) does not match this version.

## 5. Predictions (frozen)

The spill changes the baseline. Arm A no longer swallows a 144 KB log. It gets a 2 KB head, which for ConfIT is NuGet warnings, plus a path, and then has to decide how to read the rest. So the expected main-context gaps between arms are in the low thousands of tokens, not the 30x to 300x of the calibration table.

| ID | Prediction |
|---|---|
| P-1 | Main-context footprint, normal config, median per cell: C lowest; B and D within about 1.5K tokens of each other; A highest on R1 and R3. Every arm stays under 15K tokens on G and R1. |
| P-2 | Arm A opens the spilled file selectively (grep, tail, or a ranged Read) in at least 2 of 3 runs on each normal scenario. Whole-file reads are the exception. |
| P-3 | On normal G, A's footprint is within 3K tokens of D's. The verifier's main-context advantage on green is small in absolute terms. |
| P-4 | On normal R3, A's footprint exceeds D's by more than 3K tokens, because finding 39 failing tests in a spilled 286 KB file takes several searches. |
| P-5 | Quiet R1 (29.7 KB, under the spill threshold) is where the verifier shows its clearest saving: A takes the whole output inline (about 7K+ tokens), and D stays under 4K. |
| P-6 | Quiet G (33 KB, just over the threshold) may make A the cheapest arm, if it trusts the exit code and never opens the spilled file. |
| P-7 | Total tokens processed (main plus subagents): D > A and C > D on every scenario. The verifier costs more in total than it saves in main context. |
| P-8 | The verifier returns summary.json unchanged and opens no log in every run. |
| P-9 | Answers are correct (status, R1 test named with `<absent>` quoted, R3 cause named with the right count) in at least 5 of 6 runs per arm across R1 and R3. If anything is wrong, it is C's prose summary. |
| P-10 | Added after the pilot, before any H run. Arm H (D plus a `failureHint` that says to grep the failed log instead of reading it whole; its greps return about 0.9 KB on R1 and 11.7 KB on R3) has a median main footprint at most half of D's on R1 and below D's on R3, with answers as correct as D's. H reaches the log only through grep in at least 5 of 6 runs. |

## 5b. Part 2: the fix workflow (predictions frozen before any T or S run)

Part 2 asks the follow-on question: does splitting verify, diagnose and fix across models make the whole loop cheaper, without repeating work? Both arms get the same job: make the checks pass by fixing production code, never the tests. Both use Opus 5.5 (`claude-opus-5-5`, effort medium) as the main session, at normal verbosity.

| Arm | Flow |
|---|---|
| T (tiered) | Opus orchestrator → `verifier-haiku` (Lattice's verifier prompt verbatim; Read and Bash; Haiku) → if red, `fixer-sonnet` (Read, Grep, Glob and Edit only, so it has no shell and cannot rerun anything; Sonnet), given the summary.json → `verifier-haiku` again. The `failureHint` is applied. |
| S (solo) | Opus alone: runs `make build && make unit && make component`, diagnoses, fixes and re-runs, with at most three attempts. No subagents, no hint. |

Agent definitions are in `inputs/config/agents-tiered.json`; prompts in `inputs/prompts/arm-T.md` and `inputs/prompts/arm-S.md`. Runs: T on R1 and R3, n = 3, plus G, n = 2. S on R1 and R3, n = 2. After each session the harness saves the net code change and re-runs the checks itself, independently of anything the session reported.

| ID | Prediction |
|---|---|
| P-11 | No repeated work in T. In every red T run: the fixer runs no command; the orchestrator runs no build or test, opens no log and edits no file; and the checks run exactly twice (verify, then confirm) in at least 5 of 6 runs. S runs the checks at least three times in at least 3 of 4 red runs. |
| P-12 | Outcome. The independent final check is green in at least 5 of 6 T red runs and at least 3 of 4 S red runs. No run edits a test file. |
| P-13 | The T orchestrator's median footprint per red run is at most half of S's. |
| P-14 | T's median total cost per red run is lower than S's. |
| P-15 | Green path: on G, T runs one verification, and its orchestrator footprint stays under 2K tokens at a cost under $0.05. |
| P-16 | The Haiku verifier returns summary.json unchanged in at least 85% of its calls. |

**Part 2b** (frozen before any O or T2 run). Two arms, each changing one thing relative to T. The orchestrator prompt is byte-identical in all three.

| Arm | Change from T | Config |
|---|---|---|
| O (all Opus) | every role runs on Opus; the agent prompts are byte-identical to T's | `inputs/config/agents-tiered-opus.json` |
| T2 (tightened) | same models as T; the verifier gets a strict output rule, and the fixer gets a "fix only what the tests need, prefer undoing, return a diff" rule | `inputs/config/agents-tiered-v2.json` |

Runs: O on R1 and R3, n = 2; T2 on R1 and R3, n = 3.

| ID | Prediction |
|---|---|
| P-17 | Separation, not tiering, delivers the context and rerun gains: O's orchestrator footprint is within 25% of T's, and O runs the suite exactly twice in every red run. Tiering delivers the money: O's median cost per red run is at least S's. |
| P-18 | Under the original instruction, the Opus fixer reverts R3 exactly (no added case-insensitivity) in 2 of 2 runs. If it does not, the instruction, not the model, invited the extra change. |
| P-19 | Instructions fix both misses. In T2 the Haiku verifier is verbatim in at least 11 of 12 calls; the fixer leaves an exact revert in 6 of 6 red runs and shows a diff in its reply; T2's orchestrator footprint, suite runs and cost stay within 25% of T's. |

## 5c. Part 3: a check inside a long session (frozen after the pilot, before any measured run)

**Question.** In a long session the orchestrator already holds the code and the user's intent. When a check fails there, who should diagnose and fix it: the orchestrator itself, or a separate fixer? Verifier + hint is taken as given.

**Setup:**
- **Host and model.** Claude Code 2.1.306, pinned (`CLAUDE_BIN`). The orchestrator is Opus 5.5 at medium effort. Normal verbosity, with the hint applied.
- **Agents** (`inputs/config/agents-long.json`). The tightened Haiku verifier, and a Sonnet fixer whose contract adds one rule: if a brief says a behaviour change is intended, update the test that asserts the old behaviour.
- **Base session,** built once with `harness/run-long.sh base`. The orchestrator reads the matching code and its tests and explains them. The result is 104.6K tokens of context, and no files changed.
- **Forks.** Every run forks the base with `--resume <base> --fork-session`, so every arm starts from the same context. The system prompt, tools and agents are identical in every session; the arm's rules are in the turn prompts.
- **Two turns per run,** in the same session:
  1. **t1, a failure the orchestrator didn't cause.**
     - The harness applies R3.
     - Prompt: "I just pulled my teammates' latest changes. Run the checks and get them green, treating the tests as the spec."
     - Correct: green, with no test changed. An exact revert is preferred.
  2. **t2, a failure from the user's own intended change.**
     - The harness applies the R1 patch.
     - Prompt: "Product decision: … `<not present>` … I've just made that change in DeltaFormatter. Run the checks and get them green, keeping the new wording."
     - Correct: green, DeltaFormatter keeps `<not present>`, and the test asserts it.
- **Arms.** The rules are in `inputs/prompts/long-rules-<arm>.md`:

  | Arm | On a failure |
  |---|---|
  | V (self-fix) | the orchestrator diagnoses with the hint and fixes it itself |
  | FB (fixer with brief) | the orchestrator hands the summary to the fixer with a short brief: what changed, and whether a behaviour change was intended |
  | F0 (fixer, no brief) | the orchestrator hands the summary to the fixer, as in Part 2 |

- **Runs.** n = 3 per arm, interleaved V, FB, F0. One pilot (FB, r0) is kept as evidence and not counted.
- **Measures per turn:**
  - orchestrator calls;
  - context at the start;
  - footprint;
  - cost, taken as the turn's increment of the session's cumulative cost and split by model;
  - outcome, from the harness's own checks.
- **Projection rule:** projected = turn cost + footprint × 100 later orchestrator calls × $0.20 per million tokens, the Opus 5.5 cache-read price.

| ID | Prediction |
|---|---|
| P-20 | On t1, the self-fixing orchestrator carries more: V's median turn footprint is at least 1.5 times FB's. |
| P-21 | On t1, delegation is cheaper once carrying counts: FB's median projected cost is below V's. |
| P-22 | On t2, self-fix is cheapest: V's median turn cost is below both FB's and F0's. |
| P-23 | On t2, delegation without the intent breaks the change: F0 reverts the intended wording in at least 2 of 3 runs, while V and FB keep it in 3 of 3. |
| P-24 | On t1, every arm ends green with no test changed in 3 of 3 runs, and at least 2 of 3 per arm are exact reverts. |
| P-25 | The rules hold: no orchestrator runs the checks itself, and in FB and F0 the orchestrator opens no log. |

## 5d. Part 3b: the fixer confirms its own fix (frozen before any FV run)

**Question.** In the same long session, what changes if the fixer confirms its own fix, so that the second verdict never reaches the orchestrator? The route becomes orchestrator → verifier → orchestrator → fixer → verifier, with the last verification inside the fixer. Part 3 tested orchestrator → verifier → orchestrator → fixer → orchestrator → verifier.

**Setup.** As in 5c, with one new arm. It runs a day after Part 3 and is not interleaved with V, FB and F0.

| Arm | On a failure |
|---|---|
| FV (fixer confirms its own fix) | As in FB, the orchestrator hands the summary and a brief to the fixer. The fixer fixes, then spawns `verifier-haiku` itself, and repeats fix and confirm until green, at most twice. The orchestrator does not run the verifier again. |

- **Agents** (`inputs/config/agents-long-fv.json`).
  - The verifier is the same as in 5c.
  - The fixer's contract is FB's plus a confirm step. It gets the `Agent` tool so that it can spawn the verifier; the pinned Claude Code 2.1.306 lets a subagent spawn subagents.
  - The fixer's description keeps "and, if you have one, a brief".
  - Because the agent definitions differ from the base session's, each FV fork's first call re-writes the cached prefix. The warm cost prices that write as a read, as in 5c.
- **Rules** are in `inputs/prompts/long-rules-FV.md`.
- **Runs.** n = 3, a cap set before any run. A mechanics probe comes first. It only checks that the fixer can spawn the verifier and that the stream records it, and it is not counted.

| ID | Prediction |
|---|---|
| P-26 | On t1, FV's orchestrator carries less than FB's: its median footprint is at least 0.5K below FB's (5.1K). |
| P-27 | FV's orchestrator makes fewer calls than FB's: a median of 3 or fewer on both turns (FB: 4). |
| P-28 | On t1, FV's median warm turn cost is no higher than FB's ($0.30). |
| P-29 | FV is as correct: every turn green in the harness's re-check, no test changed on t1, and the wording kept on t2, in 3 of 3. |
| P-30 | The arrangement holds: in every FV turn the orchestrator calls the verifier exactly once, and the fixer calls it at least once. |

Exploratory, with no prediction: whether FV widens the fix more or less often than FB (2 of 3), and its wall time.

## 6. Analysis rules

- Footprint = context of the last main call minus context of the first. Context = `input + cache_creation + cache_read`, deduplicated per message id.
- Report median and range per cell, never a mean of three.
- A run whose observed failing tests differ from the scenario's ground truth is marked flaky, kept as evidence and repeated. Both versions are reported.
- A run that changed files, hit a permission denial or failed to produce a transcript is a deviation. It is repeated and reported.
- Cost figures are Claude Code's own API-equivalent estimate for each session (`total_cost_usd`, which includes subagents). They are labelled as estimates.
- A claim reaches RESULTS.md only if a row in `results/` supports it.

## 7. Deviations log

| When | Change | Reason |
|---|---|---|
| before pilot | Dropped the planned serial-TFM mitigation | The flake check showed it does not remove the flake |
| before pilot | Added `--strict-mcp-config` and pinned effort `medium` | P1 showed a claude.ai MCP connector loading, and recorded medium as the default effort |
| after pilot | Added probe P2 (the P1 output from a command that exits 1) | Pilot run R1-normal-A showed a failing command's output cut to about 10K characters with no file saved; P2 isolates that behaviour |
| after pilot | Analysis fix: a "log read" now means a path or command naming a `.log` file | Reading summary.json under `tmp/verification` had been counted as a log read. Analysis only; no run is affected |
| after pilot | Added arm H (`inputs/config/failure-hint.patch`, `inputs/prompts/arm-H.md` identical to arm D), batch `hint`: R1 and R3, normal, n = 3. Run after core and control, so it is not interleaved with them | In the pilot, the red-path cost came from how the main session read the log (B read all 74 KB). The runner already supports `failureHint`; H tests whether one line of config gives the caller a cheap way to diagnose |
| after matrix | R3 ground truth extended with the 8 component tests that fail whenever the component stage runs (`data/calibration/R3-component-if-run.log`) | Two C runs ran the component stage after the unit stage failed, and reported these failures. Calibration had stopped at the unit stage, so they were missing from ground truth. They are deterministic, not flaky. Analysis only |
| after matrix | Answer scoring: "without a failure" no longer counts as a failing verdict | One correct green answer had been mis-scored. All 50 answers were also read by hand, and all are correct. Analysis only |
| after Part 2b | Analysis fix: a failing-test name must start with a letter | In R3-normal-O-r1, interleaved output from the two target frameworks merged a `Failed` line with an xUnit stack-trace line, and the parser counted it as a 40th failing test. Analysis only |
| after Part 2b | Wall-time medians exclude runs with any single wait over 120 s | Four Part 2b runs stalled on API requests that hung for 6 to 25 minutes. Token and cost figures are unaffected |
| after Part 2b | Three runs that hit the known flaky test (R1-normal-T2-r3, R3-normal-T2-r2, R3-normal-S-r1) were kept and not repeated, which departs from the rule in section 6 | No further sessions were run. In each run the flaky test failed in the first verification and passed on confirmation, and no agent changed it. Excluding the three runs changes no conclusion; RESULTS.md gives the sensitivity |
| for publication | The folder was reorganised into `inputs/`, `harness/`, `data/` and `results/`, and the paths in this document were updated | Packaging only. No run or measurement changed |
| after review | Analysis fix: a whole-file `Read` of a log that Claude Code refused ("exceeds maximum allowed size (256KB)") no longer counts as a whole-file read; the tables show refused attempts separately | An independent review found the 371-character error replies counted as reads. On R3, D made 0 whole-file reads plus 2 refused (not 2 reads), and B 0 plus 1 refused (not 1). Analysis only |
| after review | Wording fix: R3 is a two-line regression (both return paths in `ParseSpec` lowercase the matcher name), not a one-line one | The patch was always two lines; RESULTS.md and README.md described it wrongly. No run is affected |
| after review | Wording fix: Claude Code did not cut Opus alone's output; it trimmed its own first run with `tail` and re-ran to find the failing tests. In the 2 default-verbosity raw-command runs the failure was visible, and the second run was the skipped component stage | RESULTS.md had attributed every re-run to the cut. No run or count is affected |
| Part 3 pilot | Analysis fix: a turn's cost is its increment of the session's cumulative `total_cost_usd` and `modelUsage` | A resumed or forked session carries its cost state forward, so turn 1's raw total included the base session's $1.17. Found in the pilot (L-FB-r0), before the predictions in 5c were frozen. Analysis only |
| after Part 3 | Analysis fix: the "orchestrator ran the checks itself" detector needs `make <target>`, `dotnet test` or the runner as a command | It had matched `make:` inside a grep pattern in L-V-r3 t2. No orchestrator ran the suite. Analysis only; P-25 holds either way |
| after Part 3 | A harness re-check that failed only on the known flaky readiness-probe tests counts as passing, as in Part 2 (L-V-r3 t1). Turns where an agent itself hit the flaky test (L-V-r1 t2, L-FB-r1 t2) or a transient build failure (L-V-r3 t2) are flagged, and the tables are shown with and without them | No further sessions were run. Analysis only |
| after Part 3 | A turn-1 revert that differs from pristine only in whitespace counts as an exact revert (L-FB-r2 dropped a space after a comma, as one fix did in Part 2b) | P-24 does not hold either way: FB's exact reverts are 0 of 3 byte-exact and 1 of 3 behaviourally exact |
| for publication | `data/long/batch.sh` takes `WS` from the environment instead of a hard-coded workspace path, and `analyze_long.py` writes the workspace as `$WS` in the stored briefs | Packaging only. The runs used the same workspace, and the results rebuild identically from the shareable copy |
| after review | Reporting fix: F0 was not a no-brief condition. The `fixer-sonnet` description in `inputs/config/agents-long.json`, used by every Part 3 arm, says to give it the summary "and, if you have one, a brief"; only the turn rules (`long-rules-F0.md`) leave the brief out. RESULTS.md now says F0 was nudged, and P-23's no-brief case was not cleanly tested | An independent review of the configs after the runs. No run or count changed; the wording "unprompted" was wrong |
| after review | Reporting fix: in the three widened turn-1 fixes (L-FB-r1, L-FB-r3, L-F0-r2), the orchestrator flagged the extra change to the user as a decision, and in L-FB-r3 it read the file first and offered to have the fixer take it out. RESULTS.md had said no orchestrator caught the changes; it now says none sent them back | Found by reading the turn-1 transcripts during review. No run or count changed |
| after Part 3 | Added Part 3b (section 5d): a fourth arm, FV, in which the fixer confirms its own fix by spawning the verifier. The predictions were frozen before any FV run. A probe (`data/long/probe-fv/summary.json`, not counted) first checked that the pinned 2.1.306 lets the fixer spawn `verifier-haiku` and that the stream records it; it did, nested under the fixer's call, for $0.30 | To test the other route, orchestrator → verifier → orchestrator → fixer → verifier, in the same long session, with at most three runs |
| after Part 3 | FV is not interleaved with V, FB and F0; its three runs came a day later, on the same base session, CLI and workspace. FV's agent definitions (`agents-long-fv.json`) differ from the base session's, so each FV fork's first call re-writes the cached prefix, which the warm cost prices as a read. The analysis now also counts the verifier runs a fixer starts itself, and scores P-20 to P-25 on the three arms they were frozen for | Measures for V, FB and F0 are unchanged |
