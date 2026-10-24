#!/usr/bin/env python3
"""Part 3 results: a check inside a long session (run harness/run-long.sh first).

    analyze_long.py EXPERIMENT_DIR [--include-pilot]

Writes results/long-session.md and results/long-session.csv. Where a run's raw
streams are present (one `claude -p` invocation per turn), it first derives
data/long/runs/<run>/metrics.json from them, from meta.json (the harness's own
checks after each turn) and from the copied verification logs. Where they are
not, as in the shareable copy of this folder, it reads metrics.json instead,
so the results can be rebuilt from either.

Per turn:
  calls        main-session (orchestrator) model calls
  start        context of the turn's first orchestrator call (the long session it runs in)
  footprint    context of the turn's last orchestrator call minus its first
  cost         what the turn added to Claude Code's total_cost_usd, with the per-model split from
               modelUsage. A resumed or forked session carries its cost state forward, so both
               are cumulative: a turn's cost is its total minus the previous invocation's (the
               base session for turn 1, turn 1 for turn 2)
  warm cost    cost with any cache write of the inherited prefix on the turn's first
               call priced as a cache read, as it would be in a session that never paused
  projected    warm cost + footprint carried for 100 later orchestrator calls at the
               cache-read price (the rule frozen in PROTOCOL.md section 5c)
"""
import csv
import glob
import json
import os
import re
import statistics
import sys

READ = 0.20e-6                         # Opus 5.5 cache read, $/token
WRITE = {"5m": 5.00e-6, "1h": 8.00e-6}  # Opus 5.5 cache writes, $/token
NEW_TOKENS_ALLOWANCE = 4000            # a turn's own new tokens on its first call (prompt, rules)
LATER_CALLS = 100
ARMS = ["V", "FB", "F0", "FV"]
PART3 = ["V", "FB", "F0"]   # the arms P-20 to P-25 were frozen for; FV (Part 3b) has its own predictions
ARM_NAME = {"V": "self-fix", "FB": "fixer with brief", "F0": "fixer, no brief", "FV": "fixer confirms its own fix"}
TURN_NAME = {"t1": "turn 1: teammates' change, 39 failing tests", "t2": "turn 2: own intended change, 1 failing test"}
INTENT = re.compile(r"intend|product decision|not present|new wording|keep", re.I)
# Running the suite: a make target or dotnet test as a command, not the word "make" inside a grep pattern.
RUNS_SUITE = re.compile(r"(^|[;&|]\s*|\bthen\s+)(make(\s+(build|unit|component))?\s*($|[;&|>])|dotnet\s+test\b|bash\s+\S*run-verification)", re.M)
FAILED_TEST = re.compile(r"^\s*Failed ([A-Za-z_][\w.]*(?:\(.*\))?) \[[^\]]*\]\s*$", re.M)
FLAKY = "ReadinessProbe"


def context(usage):
    return usage.get("input_tokens", 0) + usage.get("cache_creation_input_tokens", 0) + usage.get("cache_read_input_tokens", 0)


def final_result(path):
    result = {}
    for line in open(path, encoding="utf-8", errors="replace"):
        if line.startswith("{") and '"type":"result"' in line.replace(" ", ""):
            result = json.loads(line)
    return result


def parse_turn(path, previous):
    seen, calls, agents, actions, briefs, verdicts = set(), [], {}, [], [], []
    nested_ids, nested_verdicts = set(), []   # verifier runs the fixer starts itself (FV), and what they returned
    result = {}
    for line in open(path, encoding="utf-8", errors="replace"):
        if not line.startswith("{"):
            continue
        e = json.loads(line)
        if e.get("type") == "result":
            result = e
        if e.get("type") == "user" and e.get("parent_tool_use_id"):
            for block in e["message"].get("content", []):
                if isinstance(block, dict) and block.get("tool_use_id") in nested_ids:
                    text = block.get("content")
                    text = (text if isinstance(text, str) else json.dumps(text)).replace('\\"', '"')
                    nested_verdicts += re.findall(r'"headline"\s*:\s*"([^"\\]*)', text)
        if e.get("type") == "assistant" and e.get("parent_tool_use_id"):
            for block in e["message"].get("content", []):
                if block.get("type") == "tool_use" and block["name"] in ("Agent", "Task") and \
                        "verifier" in block.get("input", {}).get("subagent_type", ""):
                    nested_ids.add(block["id"])
        if e.get("type") == "user" and not e.get("parent_tool_use_id"):
            for block in e["message"].get("content", []):
                text = block.get("content") if isinstance(block, dict) else None
                text = text if isinstance(text, str) else json.dumps(text)
                text = (text or "").replace('\\"', '"')
                verdicts += re.findall(r'"headline"\s*:\s*"([^"\\]*)', text)
        if e.get("type") != "assistant" or e.get("parent_tool_use_id"):
            continue
        msg = e["message"]
        if msg["id"] not in seen:
            seen.add(msg["id"])
            calls.append(msg.get("usage", {}))
        for block in msg.get("content", []):
            if block.get("type") != "tool_use":
                continue
            name, inp = block["name"], block.get("input", {})
            if name in ("Agent", "Task"):
                kind = inp.get("subagent_type", "?")
                agents[kind] = agents.get(kind, 0) + 1
                if "fixer" in kind:
                    briefs.append(inp.get("prompt", ""))
            target = " ".join(str(inp.get(k, "")) for k in ("command", "file_path", "pattern", "path"))
            if name == "Bash" and RUNS_SUITE.search(inp.get("command", "")):
                actions.append("ran checks")
            elif re.search(r"\.log\b|tmp/verification", target) and "summary.json" not in target:
                actions.append("log read")
            elif name in ("Edit", "Write", "MultiEdit"):
                actions.append("edit " + os.path.basename(inp.get("file_path", "")))
            elif name == "Read" and target.strip().endswith(".cs"):
                actions.append("code read")
    if not calls:
        return None
    first = calls[0]
    created = first.get("cache_creation") or {}
    excess = max(0, first.get("cache_creation_input_tokens", 0) - NEW_TOKENS_ALLOWANCE)
    if created.get("ephemeral_1h_input_tokens", 0) >= created.get("ephemeral_5m_input_tokens", 0):
        write = WRITE["1h"]
    else:
        write = WRITE["5m"]
    cost = result.get("total_cost_usd", 0.0) - previous.get("total_cost_usd", 0.0)
    warm = cost - excess * (write - READ)
    def model_costs(r):
        usage = r.get("modelUsage", {})
        return {key: sum(v.get("costUSD", 0) for k, v in usage.items() if key in k) for key in ("opus", "haiku", "sonnet")}
    now, before = model_costs(result), model_costs(previous)
    by_model = {key: now[key] - before[key] for key in now}
    footprint = context(calls[-1]) - context(first)
    return {
        "calls": len(calls), "start": context(first), "footprint": footprint,
        "firstCallCacheWrite": first.get("cache_creation_input_tokens", 0),
        "cost": cost, "warmCost": warm, "projected": warm + footprint * LATER_CALLS * READ,
        "opusCost": by_model["opus"], "haikuCost": by_model["haiku"], "sonnetCost": by_model["sonnet"],
        "agents": agents, "actions": actions, "briefs": briefs,
        "briefCarriesIntent": any(INTENT.search(b) for b in briefs) if briefs else None,
        "ranChecksItself": "ran checks" in actions,
        "logReads": sum(1 for a in actions if a == "log read"),
        "codeReads": sum(1 for a in actions if a == "code read"),
        "edits": [a[5:] for a in actions if a.startswith("edit ")],
        "durationSeconds": round(result.get("duration_ms", 0) / 1000),
        "verdicts": verdicts,
        "fixerVerifies": len(nested_ids), "nestedVerdicts": nested_verdicts,
    }


def workspace_neutral(text):
    """Write the disposable workspace as $WS, so excerpts read the same here and in the shareable copy."""
    ws = os.environ.get("WS")
    if ws:
        text = text.replace(os.path.realpath(ws), "$WS").replace(ws, "$WS")
    return re.sub(r"(?:/[^\s/`'\"]+)+/confit-verifier-exp", "$WS", text)


def derive(exp, meta):
    """Raw streams and files -> the per-turn metrics and outcomes that metrics.json keeps."""
    base_stream = os.path.join(exp, "data", "long", "base", "base.stream.jsonl")
    previous = final_result(base_stream)
    derived = {"runId": meta["runId"]}
    for t in ("t1", "t2"):
        stream = os.path.join(meta["dir"], f"{t}.stream.jsonl")
        m = parse_turn(stream, previous) if os.path.exists(stream) else None
        previous = final_result(stream) if os.path.exists(stream) else previous
        meta[t]["m"] = m
        if m:
            ok, exact = outcome(t, meta)
            m.update(correct=ok, exact=exact, noise=noise(meta, t),
                     recheckFlaked=meta[t]["finalStatus"] != "passed" and final_green(meta, t))
            m["briefs"] = [workspace_neutral(re.sub(r"\{\s*\"schemaVersion\".*\}", "{summary.json}", b, flags=re.S))
                           for b in m["briefs"]]
            if t == "t1":
                m["change"] = ("exact revert" if meta["t1"]["matcherRestored"] else "revert, one space dropped") if exact else \
                    f"{meta['t1']['netChangeLines']} changed lines in " + (", ".join(os.path.basename(f) for f in meta["t1"]["netChangeFiles"]) or "nothing")
        derived[t] = m
    json.dump(derived, open(os.path.join(meta["dir"], "metrics.json"), "w"), indent=2)


def load(exp, include_pilot):
    base_path = os.path.join(exp, "data", "long", "base", "base.json")
    base_stream = os.path.join(exp, "data", "long", "base", "base.stream.jsonl")
    if os.path.exists(base_path) and os.path.exists(base_stream):
        base = json.load(open(base_path))
        base["costUsd"] = round(final_result(base_stream).get("total_cost_usd", 0.0), 4)
        json.dump(base, open(base_path, "w"), indent=2)
    runs = []
    for meta_path in sorted(glob.glob(os.path.join(exp, "data", "long", "runs", "*", "meta.json"))):
        meta = json.load(open(meta_path))
        if meta["repeat"] == 0 and not include_pilot:
            continue
        meta["dir"] = os.path.dirname(meta_path)
        if os.path.exists(os.path.join(meta["dir"], "t1.stream.jsonl")):
            derive(exp, meta)
        else:
            derived = json.load(open(os.path.join(meta["dir"], "metrics.json")))
            for t in ("t1", "t2"):
                meta[t]["m"] = derived[t]
        runs.append(meta)
    return runs


def med(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


def k(n):
    return "-" if n is None else (f"{n / 1000:.1f}K" if abs(n) >= 1000 else f"{n:.0f}")


def usd(x):
    return "-" if x is None else f"${x:.2f}"


def spread(values, fmt):
    values = [v for v in values if v is not None]
    if not values:
        return "-"
    if len(values) == 1:
        return fmt(values[0])
    return f"{fmt(med(values))} [{fmt(min(values))}–{fmt(max(values))}]"


def failing_tests(folder):
    names = set()
    for log in glob.glob(os.path.join(folder, "*.log")):
        names |= set(FAILED_TEST.findall(open(log, encoding="utf-8", errors="replace").read()))
    return names


def final_green(run, t):
    """Passed, or failed only on the known flaky readiness-probe tests in the harness's own re-check."""
    if run[t]["finalStatus"] == "passed":
        return True
    names = failing_tests(os.path.join(run["dir"], f"{t}.final-verify"))
    return bool(names) and all(FLAKY in n for n in names)


def noise(run, t):
    """What, other than the change itself, the turn had to deal with."""
    flags = []
    if any(FLAKY in n for d in glob.glob(os.path.join(run["dir"], "verification", t, "*")) for n in failing_tests(d)):
        flags.append("flaky test")
    m = run[t]["m"] or {}
    if any(v.startswith("Failed at build") for v in m.get("verdicts", []) + m.get("nestedVerdicts", [])):
        flags.append("transient build failure")
    return flags


def whitespace_only(diff_path):
    removed, added = [], []
    for line in open(diff_path, encoding="utf-8", errors="replace"):
        if line.startswith("-") and not line.startswith("---"):
            removed.append(re.sub(r"\s+", "", line[1:]))
        elif line.startswith("+") and not line.startswith("+++"):
            added.append(re.sub(r"\s+", "", line[1:]))
    return bool(removed) and removed == added


def outcome(t, run):
    turn = run[t]
    if t == "t1":
        ok = final_green(run, t) and not turn["testsChanged"]
        exact = ok and not turn["filesBeyondRegression"] and (
            turn["matcherRestored"] or whitespace_only(os.path.join(run["dir"], "t1.net-change.diff")))
        return ok, exact
    ok = final_green(run, t) and turn["featureKept"] and turn["testUpdated"]
    return ok, ok


def turn_table(runs, t, title):
    lines = [title, "",
             "| Arm | n | Correct | Exact revert | Orchestrator calls | Context at start | Footprint | Opus cost | Sonnet cost "
             "| Turn cost (warm) | Projected, 100 later calls | Time (s) |",
             "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for arm in ARMS:
        rs = [r for r in runs if r["arm"] == arm and r[t]["m"]]
        if not rs:
            continue
        m = [r[t]["m"] for r in rs]
        correct = sum(1 for x in m if x["correct"])
        exact = f"{sum(1 for x in m if x['exact'])}/{len(rs)}" if t == "t1" else "-"
        lines.append(
            f"| {ARM_NAME[arm]} ({arm}) | {len(rs)} | {correct}/{len(rs)} | {exact} | {spread([x['calls'] for x in m], lambda v: f'{v:g}')} "
            f"| {spread([x['start'] for x in m], k)} | {spread([x['footprint'] for x in m], k)} "
            f"| {spread([x['opusCost'] for x in m], usd)} | {spread([x['sonnetCost'] for x in m], usd)} "
            f"| {spread([x['warmCost'] for x in m], usd)} | {spread([x['projected'] for x in m], usd)} "
            f"| {spread([x['durationSeconds'] for x in m], lambda v: f'{v:.0f}')} |")
    return lines + [""]


def predictions(runs):
    def med_of(arm, t, key):
        return med([r[t]["m"][key] for r in runs if r["arm"] == arm and r[t]["m"]])
    def count(arm, t, test):
        rs = [r for r in runs if r["arm"] == arm and r[t]["m"]]
        return sum(1 for r in rs if test(r)), len(rs)
    rows = []
    v, fb = med_of("V", "t1", "footprint"), med_of("FB", "t1", "footprint")
    rows.append(("P-20", v >= 1.5 * fb, f"V {k(v)} against FB {k(fb)} ({v / fb:.1f} times)"))
    v, fb = med_of("V", "t1", "projected"), med_of("FB", "t1", "projected")
    rows.append(("P-21", fb < v, f"FB {usd(fb)} against V {usd(v)}"))
    v, fb, f0 = med_of("V", "t2", "warmCost"), med_of("FB", "t2", "warmCost"), med_of("F0", "t2", "warmCost")
    rows.append(("P-22", v < fb and v < f0, f"V {usd(v)}, FB {usd(fb)}, F0 {usd(f0)}"))
    kept = {arm: count(arm, "t2", lambda r: r["t2"]["featureKept"]) for arm in PART3}
    rows.append(("P-23", kept["F0"][1] - kept["F0"][0] >= 2 and kept["V"][0] == kept["V"][1] and kept["FB"][0] == kept["FB"][1],
                 "; ".join(f"{a} kept the wording in {kept[a][0]} of {kept[a][1]}" for a in PART3)))
    ok = {arm: count(arm, "t1", lambda r: r["t1"]["m"]["correct"]) for arm in PART3}
    ex = {arm: count(arm, "t1", lambda r: r["t1"]["m"]["exact"]) for arm in PART3}
    rows.append(("P-24", all(ok[a][0] == ok[a][1] for a in PART3) and all(ex[a][0] >= 2 for a in PART3),
                 "; ".join(f"{a} green {ok[a][0]}/{ok[a][1]}, exact {ex[a][0]}/{ex[a][1]}" for a in PART3)))
    ran = sum(1 for r in runs if r["arm"] in PART3 for t in ("t1", "t2") if r[t]["m"] and r[t]["m"]["ranChecksItself"])
    logs = sum(r[t]["m"]["logReads"] for r in runs if r["arm"] in ("FB", "F0") for t in ("t1", "t2") if r[t]["m"])
    rows.append(("P-25", ran == 0 and logs == 0, f"orchestrator ran the checks itself in {ran} turns; FB and F0 orchestrators opened logs {logs} times"))
    return ["## Predictions (PROTOCOL.md section 5c)", "", "| ID | Status | Evidence |", "|---|---|---|"] + \
        [f"| {pid} | {'held' if held else 'did not hold'} | {ev} |" for pid, held, ev in rows] + [""]

def predictions_3b(runs):
    fv = [r for r in runs if r["arm"] == "FV"]
    if not fv:
        return []
    def med_of(arm, t, key):
        return med([r[t]["m"][key] for r in runs if r["arm"] == arm and r[t]["m"]])
    turns = [(r, t) for r in fv for t in ("t1", "t2") if r[t]["m"]]
    rows = []
    a, b = med_of("FV", "t1", "footprint"), med_of("FB", "t1", "footprint")
    rows.append(("P-26", a <= b - 500, f"FV {k(a)} against FB {k(b)}"))
    c1, c2 = med_of("FV", "t1", "calls"), med_of("FV", "t2", "calls")
    rows.append(("P-27", c1 <= 3 and c2 <= 3,
                 f"FV {c1:g} (t1) and {c2:g} (t2); FB {med_of('FB', 't1', 'calls'):g} and {med_of('FB', 't2', 'calls'):g}"))
    a, b = med_of("FV", "t1", "warmCost"), med_of("FB", "t1", "warmCost")
    rows.append(("P-28", a <= b, f"FV {usd(a)} against FB {usd(b)}"))
    correct = sum(1 for r, t in turns if r[t]["m"]["correct"])
    rows.append(("P-29", correct == len(turns) == 2 * len(fv), f"{correct} of {len(turns)} FV turns correct"))
    held = [r[t]["m"]["agents"].get("verifier-haiku", 0) == 1 and r[t]["m"]["fixerVerifies"] >= 1 for r, t in turns]
    rows.append(("P-30", all(held) and len(held) == 2 * len(fv), "verifier runs, orchestrator/fixer: " + "; ".join(
        f"{r['runId']} {t} {r[t]['m']['agents'].get('verifier-haiku', 0)}/{r[t]['m']['fixerVerifies']}" for r, t in turns)))
    return ["## Predictions (PROTOCOL.md section 5d, Part 3b)", "", "| ID | Status | Evidence |", "|---|---|---|"] + \
        [f"| {pid} | {'held' if ok else 'did not hold'} | {ev} |" for pid, ok, ev in rows] + [""]


def report(exp, runs):
    lines = ["# Part 3: a check inside a long session", "",
             "Generated by `harness/analyze_long.py` from `data/long/`. Every session forks the same base session, so each "
             "arm starts from an identical long context. All arms run an Opus 5.5 orchestrator with the Haiku verifier and "
             "the failure hint; they differ only in who diagnoses and fixes a failure, and in FV (Part 3b, run a day later) in who "
             "confirms the fix: there the fixer runs the verifier itself. Median [min–max] per cell. Costs are "
             "each turn's increment of the session's cumulative cost; Haiku (the verifier) cost under $0.01 a turn in every run.", ""]
    base_path = os.path.join(exp, "data", "long", "base", "base.json")
    if os.path.exists(base_path):
        base = json.load(open(base_path))
        cost = f", ${base['costUsd']:.2f} to build" if "costUsd" in base else ""
        lines += [f"Base session: {k(base['lastContextTokens'])} tokens of context{cost}, Claude Code {base['claudeVersion']}.", ""]
    for t in ("t1", "t2"):
        lines += turn_table(runs, t, f"## {TURN_NAME[t]}")
        clean = [r for r in runs if r[t]["m"] and not r[t]["m"]["noise"]]
        if len(clean) < len(runs):
            lines += turn_table(clean, t, f"### {TURN_NAME[t]}, without turns that hit noise")
    lines += predictions(runs)
    lines += predictions_3b(runs)
    lines += ["## Outcomes and behaviour, per run", "",
              "| Run | Turn 1 | Turn 1 change | Turn 2 | Turn 2: wording kept / test updated | Turn 2 brief carries the intent "
              "| Orchestrator log reads (t1, t2) | Verifier runs, orchestrator/fixer (t1; t2) | Noise the agents hit (t1; t2) |",
              "|---|---|---|---|---|---|---|---|---|"]
    for r in sorted(runs, key=lambda r: (ARMS.index(r["arm"]), r["repeat"])):
        m1, m2 = r["t1"]["m"] or {}, r["t2"]["m"] or {}
        def status(m):
            return ("green" if m.get("correct") else "FAILED") + (" (re-check flaked)" if m.get("recheckFlaked") else "")
        brief = "-" if m2.get("briefCarriesIntent") is None else ("yes" if m2["briefCarriesIntent"] else "no")
        lines.append(
            f"| {r['runId']} | {status(m1)} | {m1.get('change', '-')} | {status(m2)} "
            f"| {'yes' if r['t2']['featureKept'] else 'NO'} / {'yes' if r['t2']['testUpdated'] else 'no'} "
            f"| {brief} | {m1.get('logReads', '-')}, {m2.get('logReads', '-')} "
            f"| {m1.get('agents', {}).get('verifier-haiku', 0)}/{m1.get('fixerVerifies', 0)}; "
            f"{m2.get('agents', {}).get('verifier-haiku', 0)}/{m2.get('fixerVerifies', 0)} "
            f"| {', '.join(m1.get('noise', [])) or '-'}; {', '.join(m2.get('noise', [])) or '-'} |")
    lines += ["", "Turn 2 is correct only if the checks pass, DeltaFormatter keeps `<not present>`, and the test asserts it. "
              "Turn 1 is correct if the checks pass with no test changed. A re-check that failed only on the known flaky "
              "readiness-probe tests counts as passing, as in Part 2.", ""]
    briefs = [(r["runId"], t, b) for r in runs for t in ("t1", "t2") for b in ((r[t]["m"] or {}).get("briefs") or [])]
    if briefs:
        lines += ["## What the orchestrator told the fixer (summary.json elided)", ""]
        for run_id, t, b in briefs:
            lines.append(f"- **{run_id}, {t}:** " + re.sub(r"\s+", " ", b)[:600] + ("…" if len(b) > 600 else ""))
        lines.append("")
    return "\n".join(lines)


def main():
    exp = sys.argv[1]
    runs = load(exp, "--include-pilot" in sys.argv)
    out = os.path.join(exp, "results")
    os.makedirs(out, exist_ok=True)
    open(os.path.join(out, "long-session.md"), "w").write(report(exp, runs))
    with open(os.path.join(out, "long-session.csv"), "w", newline="") as fh:
        w = csv.writer(fh, lineterminator="\n")
        w.writerow(["runId", "arm", "repeat", "turn", "correct", "exact", "calls", "start", "footprint", "cost", "warmCost",
                    "projected", "opusCost", "haikuCost", "sonnetCost", "firstCallCacheWrite", "logReads", "codeReads",
                    "edits", "agents", "briefCarriesIntent", "seconds", "fixerVerifies"])
        for r in sorted(runs, key=lambda r: r["runId"]):
            for t in ("t1", "t2"):
                m = r[t]["m"]
                if not m:
                    continue
                w.writerow([r["runId"], r["arm"], r["repeat"], t, m["correct"], m["exact"], m["calls"], m["start"], m["footprint"],
                            round(m["cost"], 4), round(m["warmCost"], 4), round(m["projected"], 4), round(m["opusCost"], 4),
                            round(m["haikuCost"], 4), round(m["sonnetCost"], 4), m["firstCallCacheWrite"], m["logReads"],
                            m["codeReads"], ";".join(m["edits"]), json.dumps(m["agents"]), m["briefCarriesIntent"],
                            m["durationSeconds"], m.get("fixerVerifies", 0)])
    print(f"{len(runs)} long-session runs -> {out}/long-session.md")


if __name__ == "__main__":
    main()
