#!/usr/bin/env python3
"""Build the experiment's results from data/runs/*/metrics.json (run analyze.py first).

    report.py EXPERIMENT_DIR [--include-pilot]

Writes into EXPERIMENT_DIR/results/:
  tables.md        every results table, computed here
  predictions.md   the frozen predictions from protocol.md, checked against the data
  answers.md       each run's verdict, correctness flags and the opening of its answer
  runs.csv         one row per measured run
  figures/*.svg    fig1 Part 1 context per check; fig2 and fig3 the ladder (context, cost);
                   fig4 where the orchestrator's context went; fig5 context growth per call.
                   Each is 800 px wide, so harness/export_png.py's 1600 px PNGs are exactly 2x.

RESULTS.md quotes these files and adds the interpretation; nothing in it should
be a number that is not produced here.
"""
import csv
import glob
import json
import os
import re
import statistics
import sys

ARMS = ["A", "B", "C", "D", "H"]
ARM_NAME = {"A": "A raw, main", "B": "B script, main", "C": "C raw, subagent",
            "D": "D verifier", "H": "H verifier + hint"}
SCENARIO_NAME = {"G": "Green", "R1": "R1: 1 test fails", "R3": "R3: 39 tests fail"}
FIX_ARMS = ["T", "T2", "O", "S"]
LADDER = ["A", "D", "H", "T", "T2", "O", "S"]
LADDER_NAME = {"A": "A raw, main", "D": "D verifier", "H": "H verifier + hint",
               "T": "T tiered + fix", "T2": "T2 tiered, tightened", "O": "O split, all Opus",
               "S": "S Opus alone + fix"}
SUITE_CMD = re.compile(r"\bmake\b|dotnet\s+test|run-verification")
R1_TEST = "ConfIT.UnitTest.Util.DeltaFormatterTests.Format_FieldInActualNotInExpected_ShowsAbsentSentinel"
KNOWN_FLAKY = ("ReadinessProbe",)

# Chart tokens: reference palette from the dataviz skill (light surface).
SURFACE, INK, INK2, MUTED, GRID, AXIS = "#fcfcfb", "#0b0b0b", "#52514e", "#898781", "#e1e0d9", "#c3c2b7"
FONT = 'system-ui, -apple-system, "Segoe UI", sans-serif'


# ---------------------------------------------------------------------------
# Loading and per-run derivations
# ---------------------------------------------------------------------------

def load(exp, include_pilot):
    measured, archived = [], []
    for path in sorted(glob.glob(os.path.join(exp, "data", "runs", "*", "metrics.json"))):
        m = json.load(open(path))
        if m["arm"].startswith("P"):
            continue
        if os.path.basename(os.path.dirname(path)) != m["runId"]:
            archived.append(m)
        elif m["repeat"] >= 1 or include_pilot:
            measured.append(m)
    return measured, archived


def verdict(answer):
    head = answer[:300]
    if re.search(r"not all|not every|doesn.t pass|don.t (all )?pass|do not pass|is failing|are failing|"
                 r"\bfails\b|failed at|\bfailure", head, re.I) and not re.search(r"\b(0|no|zero) fail|without (a |any )?fail", head, re.I):
        return "fail"
    if re.search(r"\bpass", head, re.I):
        return "pass"
    return "unclear"


def correctness(m, r3_truth):
    a, s = m["answer"], m["scenario"]
    v = verdict(a)
    if m.get("fixArm"):
        checks = {"finalGreen": m.get("finalStatus") == "passed", "noTestEdits": not m.get("testsEdited")}
        if s == "R1":
            checks["namesTest"] = "ShowsAbsentSentinel" in a
        if s == "R3":
            checks["namesCause"] = bool(re.search(r"unknown semantic matcher|ToLowerInvariant|lower-?case|case-?insensitiv", a, re.I))
        return v, checks, all(checks.values())
    checks = {"verdict": v == ("pass" if s == "G" else "fail")}
    if s == "R1":
        checks["namesTest"] = "ShowsAbsentSentinel" in a
        checks["quotesAssertion"] = "<absent>" in a
    if s == "R3":
        checks["namesCause"] = bool(re.search(r"unknown semantic matcher", a, re.I))
        n = len(r3_truth)
        checks["rightCount"] = bool(re.search(rf"\b({n}|{2 * n}|{n + 8}|{2 * n + 8})\b", a))
    return v, checks, all(checks.values())


def flaky_evidence(m, expected, component_names=()):
    explained = lambda name: any(f'testName: "{c}"' in name for c in component_names)
    extra = sorted(t for t in set(m["observedFailingTests"]) - expected if not explained(t))
    if any(k in m["answer"] for k in KNOWN_FLAKY):
        extra.append("answer mentions a known flaky test")
    return extra


def call_kind(call, first_verify_done):
    tool, inp = call["tool"], call["input"]
    target = " ".join(str(inp.get(k, "")) for k in ("command", "file_path", "pattern", "path"))
    if tool in ("Agent", "Task"):
        return "verify (subagent)"
    if tool == "Bash" and re.search(r"\bmake\b|dotnet\s+test|run-verification", inp.get("command", "")):
        return "re-run" if first_verify_done else "verify (command)"
    if "summary.json" in target:
        return "read verdict"
    if re.search(r"\.log\b|tool-results/", target):
        if tool == "Read":
            if call.get("isError"):
                return "log: refused read"  # e.g. a file over Claude Code's read size limit
            return "log: partial read" if ("offset" in inp or "limit" in inp) else "log: whole-file read"
        if tool == "Grep" or re.search(r"\b(grep|rg|awk)\b", target):
            return "log: search"
        if re.search(r"\b(tail|head|sed)\b", target):
            return "log: slice"
        if re.search(r"\bcat\b", target):
            return "log: whole-file read"
        return "log: other"
    return "other"


def breakdown(m):
    """Characters each kind of main-session call put in front of the model."""
    out, verified = {}, False
    for call in m["mainToolCalls"]:
        kind = call_kind(call, verified)
        if kind.startswith("verify"):
            verified = True
        out[kind] = out.get(kind, 0) + call["seenChars"]
    return out


def derive(m, r3_truth, r3_component=()):
    expected = {"G": set(), "R1": {R1_TEST}, "R3": set(r3_truth)}[m["scenario"]]
    v, checks, ok = correctness(m, r3_truth)
    kinds = breakdown(m)
    calls = m["mainToolCalls"]
    return {
        **m,
        "verdict": v, "checks": checks, "correct": ok,
        "flaky": flaky_evidence(m, expected, r3_component if m["scenario"] == "R3" else ()),
        "kinds": kinds,
        "reruns": sum(1 for c in calls if c["tool"] == "Bash") and
                  max(0, sum(1 for c in calls if c["tool"] == "Bash" and re.search(r"\bmake\b|dotnet\s+test|run-verification", c["input"].get("command", ""))) - (1 if m["arm"] in "AB" else 0)),
        "wholeLogReads": sum(1 for k in [call_kind(c, True) for c in calls] if k == "log: whole-file read"),
        "refusedLogReads": sum(1 for k in [call_kind(c, True) for c in calls] if k == "log: refused read"),
        "spills": sum(1 for c in calls if c.get("spilled")),
        "cuts": sum(1 for c in calls if c.get("cut")),
        "cost": (m.get("streamReported") or {}).get("total_cost_usd"),
        "verbatim": all(f["verbatim"] for f in m["verifierFidelity"]) if m["verifierFidelity"] else None,
        "verifierTouchedLogs": any(s["touchedLogs"] for s in m["subagents"] if s["agentType"] and "verifier" in s["agentType"]),
    }


# ---------------------------------------------------------------------------
# Aggregation helpers
# ---------------------------------------------------------------------------

def med(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


def k(n):
    if n is None:
        return "-"
    return f"{n / 1000:.1f}K" if abs(n) >= 1000 else f"{n:.0f}"


def spread(values, fmt=k):
    values = [v for v in values if v is not None]
    if not values:
        return "-"
    if len(values) == 1:
        return fmt(values[0])
    return f"{fmt(med(values))} [{fmt(min(values))}–{fmt(max(values))}]"


def refused_note(rs):
    n = sum(r["refusedLogReads"] for r in rs)
    return f" (+{n} refused)" if n else ""


def cell(runs, scenario, config, arm):
    return [r for r in runs if r["scenario"] == scenario and r["config"] == config and r["arm"] == arm]


# ---------------------------------------------------------------------------
# Tables
# ---------------------------------------------------------------------------

def tables(runs, archived):
    lines = ["# Results tables", "",
             "Generated by `harness/report.py` from `data/runs/*/metrics.json`. Median [min–max] per cell. "
             "Token figures come from transcript usage fields (context = input + cache writes + cache reads, "
             "one value per model call). Characters are what tool results put in front of the model.", ""]

    lines += ["## 1. Main-session footprint per verification (tokens)", "",
              "Context of the last main-session model call minus the first. Normal verbosity unless marked quiet.", "",
              "| Arm | G | R1 | R3 | G quiet | R1 quiet |", "|---|---|---|---|---|---|"]
    for arm in ARMS:
        row = [ARM_NAME[arm]]
        for scenario, config in (("G", "normal"), ("R1", "normal"), ("R3", "normal"), ("G", "quiet"), ("R1", "quiet")):
            row.append(spread([r["footprint"] for r in cell(runs, scenario, config, arm)]))
        lines.append("| " + " | ".join(row) + " |")

    lines += ["", "## 2. Where the footprint came from (characters, median per run)", "",
              "What each kind of main-session call returned to the model. `verify` is the first verification call "
              "(the command, the runner, or the subagent's reply).", ""]
    kinds_order = ["verify (command)", "verify (subagent)", "read verdict", "log: search", "log: slice",
                   "log: partial read", "log: whole-file read", "log: refused read", "re-run", "other"]
    for scenario in ("G", "R1", "R3"):
        lines += [f"**{SCENARIO_NAME[scenario]}**", "", "| Arm | " + " | ".join(kinds_order) + " |",
                  "|---|" + "---|" * len(kinds_order)]
        for arm in ARMS:
            rs = cell(runs, scenario, "normal", arm)
            if not rs:
                continue
            lines.append(f"| {ARM_NAME[arm]} | " + " | ".join(
                k(med([r["kinds"].get(kind, 0) for r in rs])) for kind in kinds_order) + " |")
        lines.append("")

    lines += ["## 3. The bill: tokens processed and cost per verification (median)", "",
              "Tokens processed = sum over model calls of context + output. Cost is Claude Code's own `total_cost_usd` "
              "for the session (API-equivalent, includes subagents).", "",
              "| Scenario | Arm | Main processed | Subagent processed | Total processed | Cost (USD) | Wall (s) |",
              "|---|---|---|---|---|---|---|"]
    for scenario, config in (("G", "normal"), ("R1", "normal"), ("R3", "normal"), ("G", "quiet"), ("R1", "quiet")):
        for arm in ARMS:
            rs = cell(runs, scenario, config, arm)
            if not rs:
                continue
            label = scenario + (" quiet" if config == "quiet" else "")
            lines.append(f"| {label} | {ARM_NAME[arm]} | {spread([r['main']['processed'] for r in rs])} "
                         f"| {spread([r['subagentProcessed'] for r in rs])} | {spread([r['totalProcessed'] for r in rs])} "
                         f"| {spread([r['cost'] for r in rs], lambda x: f'{x:.2f}')} "
                         f"| {spread([r['wallSeconds'] for r in rs], lambda x: f'{x:.0f}')} |")

    lines += ["", "## 4. Correctness and behaviour (counts over runs)", "",
              "Whole-file log reads count the reads that succeeded. Attempts that Claude Code refused, because the log was "
              "over its read size limit, are shown in brackets.", "",
              "| Scenario | Arm | n | Correct | Re-ran tests | Whole-file log reads | Output spilled | Output cut | Verifier verbatim |",
              "|---|---|---|---|---|---|---|---|---|"]
    for scenario, config in (("G", "normal"), ("R1", "normal"), ("R3", "normal"), ("G", "quiet"), ("R1", "quiet")):
        for arm in ARMS:
            rs = cell(runs, scenario, config, arm)
            if not rs:
                continue
            label = scenario + (" quiet" if config == "quiet" else "")
            verb = [r["verbatim"] for r in rs if r["verbatim"] is not None]
            lines.append(f"| {label} | {ARM_NAME[arm]} | {len(rs)} | {sum(r['correct'] for r in rs)}/{len(rs)} "
                         f"| {sum(1 for r in rs if r['reruns'])} | {sum(r['wholeLogReads'] for r in rs)}{refused_note(rs)} "
                         f"| {sum(r['spills'] for r in rs)} | {sum(r['cuts'] for r in rs)} "
                         f"| {f'{sum(verb)}/{len(verb)}' if verb else '-'} |")

    flagged = [r for r in runs if r["flaky"]]
    lines += ["", "## 5. Flaky runs and deviations", ""]
    if not flagged and not archived:
        lines.append("None.")
    for r in flagged:
        lines.append(f"- `{r['runId']}` in the measured set shows extra failures: {', '.join(r['flaky'])}")
    for a in archived:
        lines.append(f"- `{a['runId']}` was archived and repeated (flaky or deviation); kept under runs/ with a suffix")
    deviations = [r for r in runs if r.get("filesChangedBySession") or r.get("exitCode")]
    for r in deviations:
        lines.append(f"- `{r['runId']}`: files changed {r.get('filesChangedBySession')}, exit {r.get('exitCode')}")
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# Predictions
# ---------------------------------------------------------------------------

def predictions(runs):
    def fp(s, a, c="normal"):
        return med([r["footprint"] for r in cell(runs, s, c, a)])

    def tot(s, a, c="normal"):
        return med([r["totalProcessed"] for r in cell(runs, s, c, a)])

    rows = []

    def add(pid, claim, held, evidence):
        rows.append((pid, claim, {True: "held", False: "did not hold", None: "not testable"}[held], evidence))

    # P-1
    sub = []
    for s in ("G", "R1", "R3"):
        vals = {a: fp(s, a) for a in "ABCD"}
        sub.append(f"{s}: " + ", ".join(f"{a} {k(v)}" for a, v in vals.items()))
    c_lowest = all(min("ABCD", key=lambda a: fp(s, a) or 1e12) == "C" for s in ("G", "R1", "R3"))
    bd_close = all(abs((fp(s, "B") or 0) - (fp(s, "D") or 0)) <= 1500 for s in ("G", "R1", "R3"))
    a_highest = all(max("ABCD", key=lambda a: fp(s, a) or -1) == "A" for s in ("R1", "R3"))
    under15 = all((fp(s, a) or 0) < 15000 for s in ("G", "R1") for a in "ABCD")
    add("P-1", "C lowest; B and D within 1.5K; A highest on R1 and R3; all under 15K on G and R1",
        c_lowest and bd_close and a_highest and under15,
        f"C lowest everywhere: {c_lowest}; B≈D: {bd_close}; A highest on red: {a_highest}; all <15K on G/R1: {under15}. " + "; ".join(sub))

    # P-2
    a_runs = [r for r in runs if r["arm"] == "A" and r["config"] == "normal"]
    spilled = [r for r in a_runs if r["spills"]]
    selective = [r for r in spilled if not r["wholeLogReads"]]
    add("P-2", "A reads a spilled file selectively in at least 2 of 3 runs per scenario",
        (len(selective) >= len(spilled) * 2 / 3) if spilled else None,
        f"{len(spilled)} of {len(a_runs)} A runs spilled (failing commands are cut instead); "
        f"{len(selective)} of those read selectively")

    g_gap = (fp("G", "A") or 0) - (fp("G", "D") or 0)
    add("P-3", "On normal G, A within 3K of D", abs(g_gap) <= 3000 if fp("G", "A") else None,
        f"A {k(fp('G', 'A'))} vs D {k(fp('G', 'D'))}")
    r3_gap = (fp("R3", "A") or 0) - (fp("R3", "D") or 0)
    add("P-4", "On normal R3, A exceeds D by more than 3K", r3_gap > 3000 if fp("R3", "A") else None,
        f"A {k(fp('R3', 'A'))} vs D {k(fp('R3', 'D'))}")
    qa, qd = fp("R1", "A", "quiet"), fp("R1", "D", "quiet")
    add("P-5", "Quiet R1: A at least 7K, D under 4K", (qa >= 7000 and qd < 4000) if qa and qd else None,
        f"A {k(qa)} vs D {k(qd)}")
    ga, gd = fp("G", "A", "quiet"), fp("G", "D", "quiet")
    add("P-6", "Quiet G: A cheaper than D", (ga < gd) if ga and gd else None, f"A {k(ga)} vs D {k(gd)}")
    p7 = [(s, tot(s, "A"), tot(s, "C"), tot(s, "D")) for s in ("G", "R1", "R3")]
    add("P-7", "Total processed: D > A and C > D on every scenario",
        all(d > a and c > d for _, a, c, d in p7 if a and c and d),
        "; ".join(f"{s}: A {k(a)}, C {k(c)}, D {k(d)}" for s, a, c, d in p7))
    dv = [r for r in runs if r["arm"] in ("D", "H")]
    add("P-8", "Verifier returns summary.json unchanged and opens no log, every run",
        all(r["verbatim"] and not r["verifierTouchedLogs"] for r in dv) if dv else None,
        f"verbatim {sum(1 for r in dv if r['verbatim'])}/{len(dv)}; opened a log {sum(1 for r in dv if r['verifierTouchedLogs'])}/{len(dv)}")
    p9 = {a: [r for r in runs if r["arm"] == a and r["scenario"] in ("R1", "R3") and r["config"] == "normal"] for a in "ABCD"}
    add("P-9", "At least 5 of 6 correct per arm on R1 and R3",
        all(sum(r["correct"] for r in rs) >= len(rs) * 5 / 6 for rs in p9.values() if rs),
        ", ".join(f"{a} {sum(r['correct'] for r in rs)}/{len(rs)}" for a, rs in p9.items()))
    h1, d1, h3, d3 = fp("R1", "H"), fp("R1", "D"), fp("R3", "H"), fp("R3", "D")
    h_runs = [r for r in runs if r["arm"] == "H"]
    grep_only = [r for r in h_runs if not r["wholeLogReads"] and not r["kinds"].get("log: partial read") and not r["kinds"].get("log: slice")]
    hc = sum(r["correct"] for r in h_runs)
    dc = sum(r["correct"] for r in runs if r["arm"] == "D" and r["scenario"] in ("R1", "R3") and r["config"] == "normal")
    add("P-10", "H at most half of D on R1, below D on R3, as correct as D; grep-only log access in 5 of 6",
        (h1 <= d1 / 2 and h3 < d3 and hc >= dc and len(grep_only) >= 5) if h1 and d1 and h3 and d3 else None,
        f"R1: H {k(h1)} vs D {k(d1)}; R3: H {k(h3)} vs D {k(d3)}; correct H {hc}/{len(h_runs)} vs D {dc}; grep-only {len(grep_only)}/{len(h_runs)}")

    out = ["# Predictions checked against the data", "",
           "Predictions were frozen in `protocol.md` before the runs they describe. Status is computed by `harness/report.py`.", "",
           "| ID | Prediction | Status | Evidence |", "|---|---|---|---|"]
    out += [f"| {pid} | {claim} | {status} | {evidence} |" for pid, claim, status, evidence in rows]
    return "\n".join(out) + "\n"


# ---------------------------------------------------------------------------
# Part 2: the fix workflow
# ---------------------------------------------------------------------------

def suite_runs(r):
    """Times the suite (or a stage of it) was executed during the session, by anyone."""
    raw = sum(1 for c in r["mainToolCalls"] if c["tool"] == "Bash" and SUITE_CMD.search(c["input"].get("command", "")))
    return raw + len(r["verificationRuns"])


def role_processed(r, role):
    return sum(s["processed"] for s in r["subagents"] if s["agentType"] and role in s["agentType"])


def orchestrator_did(r):
    edits = sum(1 for c in r["mainToolCalls"] if c["tool"] in ("Edit", "Write"))
    return f"{r['mainTouchedLogs']} / {edits}"


STALL_SECONDS = 120


def wall_cell(rs):
    """Wall time over runs without an infrastructure stall (any single wait over STALL_SECONDS)."""
    ok = [r["wallSeconds"] for r in rs if r.get("longestGapSeconds", 0) <= STALL_SECONDS]
    stalled = len(rs) - len(ok)
    return spread(ok, lambda x: f"{x:.0f}") + (f" ({stalled} stalled run{'s' if stalled > 1 else ''} excluded)" if stalled else "")


def fix_tables(runs):
    lines = ["", "## 6. Part 2: fix workflow, tiered (T) against Opus alone (S)", "",
             "Same job for both: make the checks pass by fixing production code. Final status comes from the harness "
             "re-running the checks after the session. Suite runs counts every execution of the suite or a stage, by anyone. "
             "Tokens and cost are medians per run.", "",
             "| Scenario | Arm | n | Final green | Exact revert | Suite runs | Orchestrator footprint | Orchestrator processed "
             "| Verifier processed | Fixer processed | Cost (USD) | Wall (s) | Orchestrator log reads / edits |",
             "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for scenario in ("G", "R1", "R3"):
        for arm in FIX_ARMS:
            rs = cell(runs, scenario, "normal", arm)
            if not rs:
                continue
            green = sum(1 for r in rs if r.get("finalStatus") == "passed")
            revert = "-" if scenario == "G" else f"{sum(1 for r in rs if r.get('finalStatus') == 'passed' and not r.get('netChangeLines'))}/{len(rs)}"
            lines.append(f"| {scenario} | {LADDER_NAME[arm]} | {len(rs)} | {green}/{len(rs)} | {revert} "
                         f"| {spread([suite_runs(r) for r in rs], lambda x: f'{x:g}')} | {spread([r['footprint'] for r in rs])} "
                         f"| {spread([r['main']['processed'] for r in rs])} | {spread([role_processed(r, 'verifier') for r in rs])} "
                         f"| {spread([role_processed(r, 'fixer') for r in rs])} | {spread([r['cost'] for r in rs], lambda x: f'{x:.2f}')} "
                         f"| {wall_cell(rs)} | {'; '.join(orchestrator_did(r) for r in rs)} |")

    lines += ["", "## 7. The ladder: from verdict to fix (red runs, medians)", "",
              "A, D and H come from Part 1 (Sonnet main session, diagnose only). T and S come from Part 2 (Opus main session, "
              "diagnose, fix and re-verify), so they do more work than the rows above them.", "",
              "| Arm | Main model | Job | R1 footprint | R3 footprint | R1 cost (USD) | R3 cost (USD) |",
              "|---|---|---|---|---|---|---|"]
    job = {"A": ("Sonnet 5.5", "diagnose"), "D": ("Sonnet 5.5", "diagnose"), "H": ("Sonnet 5.5", "diagnose"),
           "T": ("Opus 5.5", "diagnose + fix + re-verify"), "T2": ("Opus 5.5", "diagnose + fix + re-verify"),
           "O": ("Opus 5.5", "diagnose + fix + re-verify"), "S": ("Opus 5.5", "diagnose + fix + re-verify")}
    for arm in LADDER:
        r1, r3 = cell(runs, "R1", "normal", arm), cell(runs, "R3", "normal", arm)
        if not (r1 or r3):
            continue
        lines.append(f"| {LADDER_NAME[arm]} | {job[arm][0]} | {job[arm][1]} | {spread([r['footprint'] for r in r1])} "
                     f"| {spread([r['footprint'] for r in r3])} | {spread([r['cost'] for r in r1], lambda x: f'{x:.2f}')} "
                     f"| {spread([r['cost'] for r in r3], lambda x: f'{x:.2f}')} |")
    return "\n".join(lines) + "\n"


def fix_predictions(runs):
    red_t = [r for r in runs if r["arm"] == "T" and r["scenario"] in ("R1", "R3")]
    red_s = [r for r in runs if r["arm"] == "S" and r["scenario"] in ("R1", "R3")]
    g_t = [r for r in runs if r["arm"] == "T" and r["scenario"] == "G"]
    if not red_t:
        return ""
    rows = []

    def add(pid, claim, held, evidence):
        rows.append(f"| {pid} | {claim} | {'held' if held else 'did not hold'} | {evidence} |")

    fixer_cmds = sum(1 for r in red_t for s in r["subagents"] if s["agentType"] and "fixer" in s["agentType"] and "Bash" in s["tools"])
    orch_clean = all(not r["mainTouchedLogs"] and not any(c["tool"] in ("Edit", "Write") for c in r["mainToolCalls"])
                     and not any(c["tool"] == "Bash" and SUITE_CMD.search(c["input"].get("command", "")) for c in r["mainToolCalls"]) for r in red_t)
    twice = sum(1 for r in red_t if suite_runs(r) == 2)
    s_three = sum(1 for r in red_s if suite_runs(r) >= 3)
    add("P-11", "T: fixer runs no command, orchestrator runs/reads/edits nothing, checks run exactly twice in 5 of 6; S runs the checks 3+ times in 3 of 4",
        fixer_cmds == 0 and orch_clean and twice >= 5 and s_three >= 3,
        f"fixer commands {fixer_cmds}; orchestrator clean {orch_clean}; T exactly two runs {twice}/{len(red_t)}; S three or more {s_three}/{len(red_s)}")
    t_green = sum(1 for r in red_t if r.get("finalStatus") == "passed")
    s_green = sum(1 for r in red_s if r.get("finalStatus") == "passed")
    tests = sum(1 for r in red_t + red_s if r.get("testsEdited"))
    add("P-12", "Final check green in 5 of 6 T and 3 of 4 S red runs; no test edits", t_green >= 5 and s_green >= 3 and tests == 0,
        f"T {t_green}/{len(red_t)}, S {s_green}/{len(red_s)}, runs editing tests {tests}")
    tf, sf = med([r["footprint"] for r in red_t]), med([r["footprint"] for r in red_s])
    add("P-13", "T orchestrator footprint at most half of S's", bool(sf) and tf <= sf / 2, f"T {k(tf)} vs S {k(sf)}")
    tc, sc = med([r["cost"] for r in red_t]), med([r["cost"] for r in red_s])
    add("P-14", "T total cost below S's", bool(sc) and tc < sc, f"T ${tc:.2f} vs S ${sc:.2f}" if tc and sc else "-")
    if g_t:
        gf, gc = med([r["footprint"] for r in g_t]), med([r["cost"] for r in g_t])
        one = all(suite_runs(r) == 1 for r in g_t)
        add("P-15", "Green: one verification, orchestrator under 2K, cost under $0.05", one and gf < 2000 and gc < 0.05,
            f"one run each {one}; footprint {k(gf)}; cost ${gc:.3f}")
    fid = [f for r in runs if r["arm"] == "T" for f in r["verifierFidelity"]]
    ok = sum(1 for f in fid if f["verbatim"])
    add("P-16", "Haiku verifier verbatim in at least 85% of calls", bool(fid) and ok / len(fid) >= 0.85, f"{ok}/{len(fid)}")

    red = lambda arm: [r for r in runs if r["arm"] == arm and r["scenario"] in ("R1", "R3")]
    red_o, red_t2 = red("O"), red("T2")
    within = lambda a, b: bool(a) and bool(b) and abs(a - b) <= 0.25 * b
    if red_o:
        of, oc = med([r["footprint"] for r in red_o]), med([r["cost"] for r in red_o])
        o_two = sum(1 for r in red_o if suite_runs(r) == 2)
        add("P-17", "O footprint within 25% of T's; O runs the suite exactly twice; O cost at least S's",
            within(of, tf) and o_two == len(red_o) and bool(sc) and oc >= sc,
            f"O {k(of)} vs T {k(tf)}; exactly two runs {o_two}/{len(red_o)}; cost O ${oc:.2f} vs S ${sc:.2f} vs T ${tc:.2f}")
        o_r3 = [r for r in red_o if r["scenario"] == "R3"]
        exact = sum(1 for r in o_r3 if r.get("finalStatus") == "passed" and not r.get("netChangeLines"))
        add("P-18", "Opus fixer reverts R3 exactly under the original instruction", bool(o_r3) and exact == len(o_r3),
            f"exact revert {exact}/{len(o_r3)}; net change files {[r.get('netChangeFiles') for r in o_r3]}")
    if red_t2:
        fid2 = [f for r in red_t2 for f in r["verifierFidelity"]]
        ok2 = sum(1 for f in fid2 if f["verbatim"])
        exact2 = sum(1 for r in red_t2 if r.get("finalStatus") == "passed" and not r.get("netChangeLines"))
        diff2 = sum(1 for r in red_t2 if any(s.get("replyHasDiff") for s in r["subagents"] if s["agentType"] and "fixer" in s["agentType"]))
        t2f, t2c = med([r["footprint"] for r in red_t2]), med([r["cost"] for r in red_t2])
        t2s, ts = med([suite_runs(r) for r in red_t2]), med([suite_runs(r) for r in red_t])
        add("P-19", "T2: verifier verbatim in 11 of 12; exact revert 6 of 6 with a diff in the reply; footprint, suite runs and cost within 25% of T",
            ok2 >= 11 and exact2 == len(red_t2) and diff2 == len(red_t2) and within(t2f, tf) and within(t2c, tc) and within(t2s, ts),
            f"verbatim {ok2}/{len(fid2)}; exact revert {exact2}/{len(red_t2)}; diff in reply {diff2}/{len(red_t2)}; "
            f"footprint {k(t2f)} vs {k(tf)}; cost ${t2c:.2f} vs ${tc:.2f}; suite runs {t2s} vs {ts}")
    return "\n## Part 2\n\n| ID | Prediction | Status | Evidence |\n|---|---|---|---|\n" + "\n".join(rows) + "\n"


# ---------------------------------------------------------------------------
# Answers digest
# ---------------------------------------------------------------------------

def answers(runs):
    out = ["# Answers digest", "", "Verdict and correctness flags per run, with the opening of the answer. For review.", ""]
    for r in sorted(runs, key=lambda r: (r["scenario"], r["config"], r["arm"], r["repeat"])):
        flags = ", ".join(f"{name}={'y' if ok else 'N'}" for name, ok in r["checks"].items())
        out.append(f"- **{r['runId']}** verdict={r['verdict']} correct={'yes' if r['correct'] else 'NO'} ({flags})"
                   f"{' FLAKY: ' + '; '.join(r['flaky']) if r['flaky'] else ''}  \n  "
                   + re.sub(r"\s+", " ", r["answer"][:260]).replace("|", "\\|"))
    return "\n".join(out) + "\n"


# ---------------------------------------------------------------------------
# Figures (hand-built SVG, reference palette, native tooltips via <title>)
# ---------------------------------------------------------------------------
# Colours come from the dataviz reference palette and were checked with its
# validator. The three steps under test (verifier, + hint, + fixer) use an
# ordinal blue ramp that darkens with each step; baselines and controls are
# grey; the composition chart uses categorical slots 1-4 in their fixed order.

STEP_FILL = {"D": "#86b6ef", "H": "#3987e5", "T": "#1c5cab", "T2": "#1c5cab"}
STEP_DOT = {"D": "#184f95", "H": "#184f95", "T": "#0d366b", "T2": "#0d366b"}
BASE_FILL, BASE_DOT, BASE_LINE = "#c3c2b7", "#52514e", "#898781"
STEP_LINE = "#2a78d6"
CATEGORIES = [("verdict", "Verdicts and reports", "#2a78d6"), ("output", "Test output", "#eb6834"),
              ("reading", "Reading logs and code", "#1baf7a"), ("rerun", "Running the tests again", "#eda100"),
              ("other", "Other", "#c3c2b7")]
# Figure labels are plain language so the images can stand alone in other documents; tables add the arm letter.
LABEL = {"A": "Raw test command", "B": "Runner script, main session", "C": "Subagent runs raw command",
         "D": "Verifier", "H": "Verifier + hint", "S": "Opus alone", "O": "Same split, all on Opus",
         "T": "Verifier + hint + fixer", "T2": "… with tightened instructions"}
GROUP_DIAGNOSE = ("group", "Diagnose only · Sonnet 5.5")
GROUP_FIX = ("group", "Diagnose, fix, re-verify · Opus 5.5")
PART1_ROWS = ["A", "B", "C", "D", "H"]
LADDER_ROWS = [GROUP_DIAGNOSE, "A", "D", "H", GROUP_FIX, "S", "O", "T", "T2"]
COMPOSITION_ROWS = [GROUP_DIAGNOSE, "A", "D", "H", GROUP_FIX, "S", "T", "T2"]
RED_FACETS = (("R1", "normal", "1 failing test"), ("R3", "normal", "39 failing tests"))


def esc(s):
    return str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def tick_tokens(v):
    return "0" if v == 0 else (f"{v / 1000:g}K" if v >= 1000 else f"{v:g}")


def tick_dollars(v):
    return "$0" if v == 0 else f"${v:.2f}".rstrip("0").rstrip(".")


def nice_range(v, steps):
    for step in steps:
        if v <= step * 4:
            return step * (int(v // step) + 1), step
    return v, v / 4


TOKEN_STEPS = (1000, 2000, 2500, 5000, 10000, 20000, 25000, 50000)
DOLLAR_STEPS = (0.02, 0.05, 0.1, 0.2, 0.25, 0.5)


def svg_open(w, h, title, subtitles):
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" '
             f'font-family=\'{FONT}\' role="img" aria-label="{esc(title)}">',
             f'<rect width="{w}" height="{h}" fill="{SURFACE}"/>',
             f'<title>{esc(title)}</title>', f'<desc>{esc(" ".join(subtitles))}</desc>',
             f'<text x="16" y="26" font-size="15" font-weight="600" fill="{INK}">{esc(title)}</text>']
    for i, line in enumerate(subtitles):
        parts.append(f'<text x="16" y="{46 + 16 * i}" font-size="12" fill="{INK2}">{esc(line)}</text>')
    return parts


def hbar(x0, y, length, height, fill, tip, round_end=True):
    """Bar from a square baseline to a 4px rounded data-end."""
    if length <= 0:
        return ""
    r = min(4, length / 2, height / 2) if round_end else 0
    x1 = x0 + length
    return (f'<path d="M{x0:.1f},{y:.1f} H{x1 - r:.1f} Q{x1:.1f},{y:.1f} {x1:.1f},{y + r:.1f} V{y + height - r:.1f} '
            f'Q{x1:.1f},{y + height:.1f} {x1 - r:.1f},{y + height:.1f} H{x0:.1f} Z" fill="{fill}"><title>{esc(tip)}</title></path>')


def facet_axis(parts, x0, width, top, bottom, vmax, step, fmt):
    v = 0
    while v <= vmax + 1e-9:
        x = x0 + width * v / vmax
        parts.append(f'<line x1="{x:.1f}" y1="{top}" x2="{x:.1f}" y2="{bottom}" stroke="{GRID if v else AXIS}" stroke-width="1"/>')
        parts.append(f'<text x="{x:.1f}" y="{bottom + 15}" font-size="11" fill="{MUTED}" text-anchor="middle" '
                     f'style="font-variant-numeric: tabular-nums">{fmt(v)}</text>')
        v += step


def layout_rows(rows, top, row_h, group_h):
    placed, y = [], top
    for row in rows:
        placed.append((row, y))
        y += group_h if isinstance(row, tuple) else row_h
    return placed, y


def draw_label_column(parts, placed, width, first_facet):
    for row, y in placed:
        if isinstance(row, tuple):
            if y > placed[0][1]:
                parts.append(f'<line x1="16" y1="{y + 2}" x2="{width - 24}" y2="{y + 2}" stroke="{GRID}" stroke-width="1"/>')
            parts.append(f'<text x="16" y="{y + 18}" font-size="11" font-weight="600" fill="{INK2}">{esc(row[1])}</text>')
        elif first_facet:
            parts.append(f'<text x="26" y="{y + 15}" font-size="12" fill="{INK2}">{esc(LABEL[row])}</text>')


def fig_bars(runs, path, rows, facets, value, fmt, label_fmt, steps, title, subtitles, label_w=236, facet_w=236, gap=36):
    """Median bar plus one dot per run, for each arm row and scenario facet."""
    arms = [r for r in rows if not isinstance(r, tuple)]
    vals = [value(r) for r in runs for (s, c, _) in facets
            if r["arm"] in arms and r["scenario"] == s and r["config"] == c and value(r) is not None]
    vmax, step = nice_range(max(vals) * 1.18, steps)   # headroom so a value label always fits after its bar
    top = 46 + 16 * len(subtitles) + 26
    placed, bottom = layout_rows(rows, top, 30, 28)
    w = 16 + label_w + len(facets) * facet_w + (len(facets) - 1) * gap + 40
    p = svg_open(w, bottom + 40, title, subtitles)
    draw_label_column(p, placed, w, True)
    for fi, (scenario, config, ftitle) in enumerate(facets):
        x0 = 16 + label_w + fi * (facet_w + gap)
        p.append(f'<text x="{x0}" y="{top - 10}" font-size="12" font-weight="600" fill="{INK}">{esc(ftitle)}</text>')
        facet_axis(p, x0, facet_w, top - 4, bottom, vmax, step, fmt)
        for row, y in placed:
            if isinstance(row, tuple):
                continue
            rs = [r for r in cell(runs, scenario, config, row) if value(r) is not None]
            if not rs:
                p.append(f'<text x="{x0 + 4}" y="{y + 15}" font-size="11" fill="{MUTED}">not run</text>')
                continue
            m = med([value(r) for r in rs])
            fill, dot = STEP_FILL.get(row, BASE_FILL), STEP_DOT.get(row, BASE_DOT)
            length = facet_w * m / vmax
            p.append(hbar(x0, y + 4, length, 16, fill, f"{LABEL[row]}, {scenario}: median {label_fmt(m)}"))
            dots = []
            for r in rs:
                cx = x0 + facet_w * value(r) / vmax
                dots.append(cx)
                p.append(f'<circle cx="{cx:.1f}" cy="{y + 12}" r="4" fill="{dot}" stroke="{SURFACE}" stroke-width="2">'
                         f'<title>{esc(r["runId"])}: {label_fmt(value(r))}</title></circle>')
            near = [cx for cx in dots if x0 + length - 6 <= cx <= x0 + length + 44]
            lx = (max(near) if near else x0 + length) + 8
            p.append(f'<text x="{lx:.1f}" y="{y + 16}" font-size="11" fill="{INK2}">{label_fmt(m)}</text>')
    p.append("</svg>")
    open(path, "w").write("\n".join(p))


def step_category(call, verified):
    tool, inp = call["tool"], call["input"]
    target = " ".join(str(inp.get(k, "")) for k in ("command", "file_path", "pattern", "path"))
    if tool in ("Agent", "Task") or "summary.json" in target:
        return "verdict"
    if tool == "Bash" and SUITE_CMD.search(inp.get("command", "")):
        return "rerun" if verified else "output"
    if tool in ("Read", "Grep", "Glob", "Bash"):
        return "reading"
    return "other"


def composition(run):
    """Split a run's footprint by the kind of step that added it. Sums to the footprint exactly."""
    series = run["contextSeries"]
    by_call = {}
    for c in run["mainToolCalls"]:
        by_call.setdefault(c["callIndex"], []).append(c)
    totals = {key: 0.0 for key, _, _ in CATEGORIES}
    verified = False
    for k in range(len(series) - 1):
        delta = series[k + 1] - series[k]
        calls = by_call.get(k, [])
        if not calls:
            totals["other"] += delta
            continue
        kinds = []
        for c in calls:
            kind = step_category(c, verified)
            verified = verified or kind in ("output", "rerun")
            kinds.append((kind, c["seenChars"]))
        weight = sum(wt for _, wt in kinds)
        for kind, wt in kinds:
            totals[kind] += delta * (wt / weight if weight else 1 / len(kinds))
    return totals


def fig_composition(runs, path, rows, facets, title, subtitles, label_w=236, facet_w=236, gap=36):
    """One thin stacked bar per run: how much each kind of step added to the orchestrator's context."""
    arms = [r for r in rows if not isinstance(r, tuple)]
    sel = [r for r in runs if r["arm"] in arms and any(r["scenario"] == s and r["config"] == c for s, c, _ in facets)]
    vmax, step = nice_range(max(r["footprint"] for r in sel) * 1.18, TOKEN_STEPS)
    n_runs = max(len(cell(runs, s, c, a)) for s, c, _ in facets for a in arms)
    bar_h, bar_gap = 7, 3
    row_h = n_runs * (bar_h + bar_gap) + 12
    legend_y = 46 + 16 * len(subtitles) + 8
    top = legend_y + 52
    placed, bottom = layout_rows(rows, top, row_h, 28)
    w = 16 + label_w + len(facets) * facet_w + (len(facets) - 1) * gap + 40
    p = svg_open(w, bottom + 40, title, subtitles)
    lx = 16
    for _, name, color in CATEGORIES:
        p.append(f'<rect x="{lx}" y="{legend_y}" width="12" height="12" rx="2" fill="{color}"/>')
        p.append(f'<text x="{lx + 17}" y="{legend_y + 10}" font-size="12" fill="{INK2}">{esc(name)}</text>')
        lx += 17 + int(6.4 * len(name)) + 22
    draw_label_column(p, placed, w, True)
    for fi, (scenario, config, ftitle) in enumerate(facets):
        x0 = 16 + label_w + fi * (facet_w + gap)
        p.append(f'<text x="{x0}" y="{top - 10}" font-size="12" font-weight="600" fill="{INK}">{esc(ftitle)}</text>')
        facet_axis(p, x0, facet_w, top - 4, bottom, vmax, step, tick_tokens)
        for row, y in placed:
            if isinstance(row, tuple):
                continue
            rs = sorted(cell(runs, scenario, config, row), key=lambda r: r["repeat"])
            if not rs:
                p.append(f'<text x="{x0 + 4}" y="{y + 15}" font-size="11" fill="{MUTED}">not run</text>')
                continue
            ends = []
            for i, r in enumerate(rs):
                by = y + 5 + i * (bar_h + bar_gap)
                parts = composition(r)
                x = x0
                drawn = [(key, name, color, parts[key]) for key, name, color in CATEGORIES if parts[key] > 0]
                for j, (key, name, color, tokens) in enumerate(drawn):
                    seg = facet_w * tokens / vmax
                    last = j == len(drawn) - 1
                    p.append(hbar(x, by, max(seg - (0 if last else 2), 0.5), bar_h, color,
                                  f"{r['runId']}: {name} {tokens:,.0f} tokens", round_end=last))
                    x += seg
                ends.append(x)
            m = med([r["footprint"] for r in rs])
            p.append(f'<text x="{max(ends) + 8:.1f}" y="{y + 5 + (len(rs) * (bar_h + bar_gap)) / 2 + 3:.1f}" '
                     f'font-size="11" fill="{INK2}">median {k(m)}</text>')
    p.append("</svg>")
    open(path, "w").write("\n".join(p))


def fig_growth(runs, path, scenario, groups, title, subtitles):
    """groups: [(label, [arms])]; panels are grouped under their label, with a wider gap between groups."""
    arms = [a for _, members in groups for a in members if cell(runs, scenario, "normal", a)]
    series = {a: [[c - r["contextSeries"][0] for c in r["contextSeries"]] for r in cell(runs, scenario, "normal", a)]
              for a in arms}
    ymax, ystep = nice_range(max(max(s) for ss in series.values() for s in ss), TOKEN_STEPS)
    xmax = max(len(s) for ss in series.values() for s in ss)
    panel_w, panel_h, gap, group_gap, left = 129, 150, 16, 34, 52
    top = 46 + 16 * len(subtitles) + 56
    x_of, x = {}, left
    for gi, (_, members) in enumerate(groups):
        for a in [m for m in members if m in arms]:
            x_of[a] = x
            x += panel_w + gap
        x += group_gap - gap
    w = int(x - group_gap + gap + 5)
    p = svg_open(w, top + panel_h + 52, title, subtitles)
    v = 0
    while v <= ymax + 1e-9:
        y = top + panel_h - panel_h * v / ymax
        p.append(f'<text x="{left - 8}" y="{y + 4:.1f}" font-size="11" fill="{MUTED}" text-anchor="end" '
                 f'style="font-variant-numeric: tabular-nums">{tick_tokens(v)}</text>')
        v += ystep
    for label, members in groups:
        present = [m for m in members if m in arms]
        if present:
            gx0, gx1 = x_of[present[0]], x_of[present[-1]] + panel_w
            p.append(f'<text x="{gx0}" y="{top - 34}" font-size="11" font-weight="600" fill="{INK2}">{esc(label)}</text>')
            p.append(f'<line x1="{gx0}" y1="{top - 28}" x2="{gx1}" y2="{top - 28}" stroke="{AXIS}" stroke-width="1"/>')
    for arm in arms:
        x0 = x_of[arm]
        line = STEP_LINE if arm in STEP_FILL else BASE_LINE
        dot = STEP_DOT.get(arm, BASE_DOT)
        name = LABEL[arm]
        p.append(f'<text x="{x0}" y="{top - 12}" font-size="12" font-weight="600" fill="{INK}">{esc(name)}</text>')
        v = 0
        while v <= ymax + 1e-9:
            y = top + panel_h - panel_h * v / ymax
            p.append(f'<line x1="{x0}" y1="{y:.1f}" x2="{x0 + panel_w}" y2="{y:.1f}" stroke="{GRID if v else AXIS}" stroke-width="1"/>')
            v += ystep
        for s in series[arm]:
            pts = []
            for i, c in enumerate(s):
                x = x0 + panel_w * i / max(xmax - 1, 1)
                y = top + panel_h - panel_h * c / ymax
                pts.append((f"M{x:.1f},{y:.1f}") if not pts else f"H{x:.1f} V{y:.1f}")
            p.append(f'<path d="{" ".join(pts)}" fill="none" stroke="{line}" stroke-width="2" stroke-linejoin="round" '
                     f'stroke-linecap="round" opacity="0.75"/>')
            ex = x0 + panel_w * (len(s) - 1) / max(xmax - 1, 1)
            ey = top + panel_h - panel_h * s[-1] / ymax
            p.append(f'<circle cx="{ex:.1f}" cy="{ey:.1f}" r="4" fill="{dot}" stroke="{SURFACE}" stroke-width="2">'
                     f'<title>{esc(LABEL[arm])}: {s[-1]:,} tokens after {len(s)} calls</title></circle>')
        p.append(f'<text x="{x0 + panel_w / 2}" y="{top + panel_h + 18}" font-size="11" fill="{MUTED}" text-anchor="middle">model calls 1–{xmax}</text>')
    p.append("</svg>")
    open(path, "w").write("\n".join(p))


def composition_table(runs):
    keys = [key for key, _, _ in CATEGORIES]
    lines = ["", "## 8. Where the orchestrator's context went (red runs, tokens)", "",
             "Each run's footprint split by the kind of step that added it: the context growth between two model calls is "
             "attributed to the tool calls the first one made, split by the size of what they returned. Medians per kind, so a "
             "row need not sum to the median footprint. This is the table view of `figures/fig4-where-context-went.svg`.", "",
             "| Scenario | Arm | " + " | ".join(name for _, name, _ in CATEGORIES) + " | Footprint |",
             "|---|---|" + "---|" * len(CATEGORIES) + "---|"]
    for scenario in ("R1", "R3"):
        for arm in ["A", "B", "C", "D", "H", "S", "O", "T", "T2"]:
            rs = cell(runs, scenario, "normal", arm)
            if not rs:
                continue
            comp = [composition(r) for r in rs]
            lines.append(f"| {scenario} | {LABEL[arm]} ({arm}) | " + " | ".join(k(med([c[key] for c in comp])) for key in keys)
                         + f" | {k(med([r['footprint'] for r in rs]))} |")
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------

def main():
    exp = sys.argv[1]
    include_pilot = "--include-pilot" in sys.argv
    r3 = json.load(open(os.path.join(exp, "inputs", "scenarios", "ground-truth", "R3.json")))
    r3_truth, r3_component = r3["failingTests"], r3.get("componentFailingIfRun", [])
    raw, archived = load(exp, include_pilot)
    runs = [derive(m, r3_truth, r3_component) for m in raw]

    out = os.path.join(exp, "results")
    os.makedirs(os.path.join(out, "figures"), exist_ok=True)
    open(os.path.join(out, "tables.md"), "w").write(tables(runs, archived) + fix_tables(runs) + composition_table(runs))
    open(os.path.join(out, "predictions.md"), "w").write(predictions(runs) + fix_predictions(runs))
    open(os.path.join(out, "answers.md"), "w").write(answers(runs))

    with open(os.path.join(out, "runs.csv"), "w", newline="") as fh:
        w = csv.writer(fh, lineterminator="\n")
        w.writerow(["runId", "arm", "scenario", "config", "repeat", "footprint", "mainCalls", "mainProcessed",
                    "subagentProcessed", "totalProcessed", "costUsd", "wallSeconds", "correct", "verdict", "reruns",
                    "wholeLogReads", "spills", "cuts", "verbatim", "flaky"])
        for r in sorted(runs, key=lambda r: r["runId"]):
            w.writerow([r["runId"], r["arm"], r["scenario"], r["config"], r["repeat"], r["footprint"], r["main"]["calls"],
                        r["main"]["processed"], r["subagentProcessed"], r["totalProcessed"], r["cost"], r["wallSeconds"],
                        r["correct"], r["verdict"], r["reruns"], r["wholeLogReads"], r["spills"], r["cuts"], r["verbatim"],
                        ";".join(r["flaky"])])

    figures = os.path.join(out, "figures")
    for stale in glob.glob(os.path.join(figures, "*.svg")):
        os.remove(stale)
    tokens = lambda r: r["footprint"]
    dollars = lambda r: r["cost"]
    fig_bars(runs, os.path.join(figures, "fig1-part1-context.svg"), PART1_ROWS,
             (("G", "normal", "All checks pass"), ("R1", "normal", "1 failing test"), ("R3", "normal", "39 failing tests")),
             tokens, tick_tokens, k, TOKEN_STEPS, "What one check left in the main session",
             ["Diagnose-only task on Sonnet 5.5, normal test verbosity. Tokens added: bar = median, dots = runs.",
              "Blue: the verifier and the verifier + hint. Grey: other ways of running the same check."],
             label_w=212, facet_w=158, gap=29)
    if any(r["arm"] in FIX_ARMS for r in runs):
        fig_bars(runs, os.path.join(figures, "fig2-ladder-context.svg"), LADDER_ROWS, RED_FACETS, tokens, tick_tokens, k,
                 TOKEN_STEPS, "Verifier, then hint, then fixer: what the orchestrator carries on a failing run",
                 ["Tokens added to the orchestrator's context: bar = median, dots = runs. Blue darkens with each step.",
                  "Grey: for comparison. The lower group also fixes the code and re-verifies, so compare within a group."])
        fig_bars(runs, os.path.join(figures, "fig3-ladder-cost.svg"), LADDER_ROWS, RED_FACETS, dollars, tick_dollars,
                 lambda x: f"${x:.2f}", DOLLAR_STEPS, "What a failing run costs at each step",
                 ["API-equivalent cost per run, subagents included: bar = median, dots = runs.",
                  "With tiered models the split fixes the bug for less than Opus alone; all on Opus, it costs the most.",
                  "The tightened row's higher cost on 1 failing test is mostly a cold prompt cache (two of three runs)."])
        fig_composition(runs, os.path.join(figures, "fig4-where-context-went.svg"), COMPOSITION_ROWS, RED_FACETS,
                        "Where the orchestrator's context went on a failing run",
                        ["Tokens each kind of step added to the orchestrator's context; one bar per run.",
                         "Each step's share includes the model's own reasoning around it."])
    fig_growth(runs, os.path.join(figures, "fig5-growth-r1.svg"), "R1",
               [(GROUP_DIAGNOSE[1], ["A", "D", "H"]), (GROUP_FIX[1], ["S", "T"])],
               "How the orchestrator's context grew, call by call, with one failing test",
               ["Context added since the first model call; one line per run. Blue: the verifier path. Grey: for comparison."])
    print(f"{len(runs)} runs ({len(archived)} archived) -> {out}")


if __name__ == "__main__":
    main()
