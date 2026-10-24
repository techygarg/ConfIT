#!/usr/bin/env python3
"""Turn raw session transcripts into per-run metrics.

    analyze.py EXPERIMENT_DIR

For every data/runs/<id>/ with a transcript it writes data/runs/<id>/metrics.json;
harness/report.py turns those into everything under results/. Token figures
come only from the transcripts' usage fields: the context of one model call is
input + cache_creation + cache_read, deduplicated by message id. Nothing is
taken from a session's description of itself; the stream's own cost report is
kept only as a cross-check.
"""
import datetime
import glob
import json
import os
import re
import sys

FAILED_TEST = re.compile(r"^\s*Failed ([A-Za-z_][\w.]*(?:\(.*\))?) \[[^\]]*\]\s*$", re.M)
TRUNCATION_HINT = re.compile(r"truncat|characters? (?:were )?(?:omitted|removed)|output too large|lines? omitted", re.I)
VERIFY_COMMAND = re.compile(r"\bmake\b|dotnet\s+test|run-verification")
MARK = re.compile(r"MARK-(\d{5})")


def load_jsonl(path):
    entries = []
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            try:
                entries.append(json.loads(line))
            except json.JSONDecodeError:
                pass
    return entries


def text_of(content):
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "\n".join(text_of(c.get("text") if c.get("type") == "text" else c.get("content", ""))
                         for c in content if isinstance(c, dict))
    return ""


def model_calls(entries):
    """One record per API request, in order, with the usage of its final chunk."""
    calls = {}
    for e in entries:
        msg = e.get("message")
        if e.get("type") != "assistant" or not isinstance(msg, dict) or not msg.get("usage"):
            continue
        if msg.get("model") == "<synthetic>":
            continue
        key = msg.get("id") or e.get("requestId") or e.get("uuid")
        u = msg["usage"]
        record = {
            "id": key,
            "ts": e.get("timestamp"),
            "model": msg.get("model"),
            "input": u.get("input_tokens", 0),
            "cacheWrite": u.get("cache_creation_input_tokens", 0),
            "cacheWrite1h": (u.get("cache_creation") or {}).get("ephemeral_1h_input_tokens", 0),
            "cacheWrite5m": (u.get("cache_creation") or {}).get("ephemeral_5m_input_tokens", 0),
            "cacheRead": u.get("cache_read_input_tokens", 0),
            "output": u.get("output_tokens", 0),
        }
        record["context"] = record["input"] + record["cacheWrite"] + record["cacheRead"]
        if key not in calls:
            calls[key] = record
        elif record["output"] >= calls[key]["output"]:
            record["ts"] = calls[key]["ts"]
            calls[key] = record
    return sorted(calls.values(), key=lambda c: c["ts"] or "")


def tool_events(entries):
    """Tool uses in order, each joined with what came back to the model."""
    uses, results = [], {}
    for e in entries:
        msg = e.get("message")
        if not isinstance(msg, dict) or not isinstance(msg.get("content"), list):
            continue
        for block in msg["content"]:
            if not isinstance(block, dict):
                continue
            if e.get("type") == "assistant" and block.get("type") == "tool_use":
                msg_key = msg.get("id") or e.get("requestId") or e.get("uuid")
                uses.append({"id": block.get("id"), "name": block.get("name"), "input": block.get("input") or {},
                             "callId": msg_key})
            elif e.get("type") == "user" and block.get("type") == "tool_result":
                seen = text_of(block.get("content"))
                full = e.get("toolUseResult") if isinstance(e.get("toolUseResult"), dict) else {}
                full_out = (full.get("stdout") or "") + (full.get("stderr") or "")
                results[block.get("tool_use_id")] = {"seen": seen, "full": full_out,
                                                     "isError": bool(block.get("is_error")),
                                                     "persistedPath": full.get("persistedOutputPath"),
                                                     "persistedSize": full.get("persistedOutputSize")}
    for u in uses:
        r = results.get(u["id"], {"seen": "", "full": ""})
        u["seenChars"] = len(r["seen"])
        u["fullChars"] = len(r["full"])
        u["seen"] = r["seen"]
        u["full"] = r["full"]
        u["persistedPath"] = r.get("persistedPath")
        u["persistedSize"] = r.get("persistedSize")
        u["isError"] = r.get("isError", False)
        u["cut"] = bool(re.search(r"\[\d+ characters truncated\]", r["seen"]))
    return uses


def spill_behaviour(uses):
    """Outputs Claude Code saved to disk instead of showing, and what the model did about them."""
    spilled = [u for u in uses if u["persistedPath"]]
    later_reads = []
    for i, u in enumerate(uses):
        target = json.dumps(u["input"])
        if any(s["persistedPath"] and os.path.basename(s["persistedPath"]) in target for s in spilled if uses.index(s) < i):
            later_reads.append({"tool": u["name"], "input": {k: v for k, v in u["input"].items() if k in ("command", "file_path", "pattern", "offset", "limit")},
                                "seenChars": u["seenChars"]})
    return {"spilled": [{"tool": s["name"], "size": s["persistedSize"], "previewChars": s["seenChars"]} for s in spilled],
            "readsOfSpilledFiles": later_reads}


def final_text(entries):
    for e in reversed(entries):
        msg = e.get("message")
        if e.get("type") == "assistant" and isinstance(msg, dict) and isinstance(msg.get("content"), list):
            texts = [b.get("text", "") for b in msg["content"] if isinstance(b, dict) and b.get("type") == "text"]
            if any(t.strip() for t in texts):
                return "\n".join(texts)
    return ""


def touches_log(use):
    i = use["input"]
    target = " ".join(str(i.get(k, "")) for k in ("file_path", "path", "command", "pattern", "glob"))
    return bool(re.search(r"\.log\b", target))


def summarise_calls(calls):
    return {
        "calls": len(calls),
        "contextFirst": calls[0]["context"] if calls else 0,
        "contextLast": calls[-1]["context"] if calls else 0,
        "contextPeak": max((c["context"] for c in calls), default=0),
        "processed": sum(c["context"] + c["output"] for c in calls),
        "output": sum(c["output"] for c in calls),
        "cacheRead": sum(c["cacheRead"] for c in calls),
        "cacheWrite1h": sum(c["cacheWrite1h"] for c in calls),
        "cacheWrite5m": sum(c["cacheWrite5m"] for c in calls),
        "uncachedInput": sum(c["input"] for c in calls),
        "models": sorted({c["model"] for c in calls if c["model"]}),
    }


def subagents(run_dir):
    found = []
    for path in sorted(glob.glob(os.path.join(run_dir, "transcript", "session", "subagents", "agent-*.jsonl"))):
        meta_path = path[:-len(".jsonl")] + ".meta.json"
        meta = json.load(open(meta_path)) if os.path.exists(meta_path) else {}
        entries = load_jsonl(path)
        uses = tool_events(entries)
        found.append({
            "agentType": meta.get("agentType"),
            **summarise_calls(model_calls(entries)),
            "tools": [u["name"] for u in uses],
            "touchedLogs": any(touches_log(u) for u in uses),
            "spill": spill_behaviour(uses),
            "finalText": final_text(entries),
            "replyHasDiff": bool(re.search(r"^@@ |^\+\+\+ |^--- a/|```diff", final_text(entries), re.M)),
            "bashOutput": "\n".join(u["full"] or u["seen"] for u in uses if u["name"] == "Bash"),
        })
    return found


def verification_runs(run_dir):
    runs = []
    for summary_path in sorted(glob.glob(os.path.join(run_dir, "verification", "*", "summary.json"))):
        summary = json.load(open(summary_path))
        failing = set()
        for log in glob.glob(os.path.join(os.path.dirname(summary_path), "*.log")):
            failing |= {m.group(1) for m in FAILED_TEST.finditer(open(log, errors="replace").read())}
        runs.append({"runId": summary.get("runId"), "summary": summary, "failingTests": sorted(failing)})
    return runs


def verifier_fidelity(agents, vruns):
    """Did each verifier subagent hand back its summary.json unchanged?"""
    by_id = {r["runId"]: r["summary"] for r in vruns}
    checks = []
    for a in agents:
        if not a["agentType"] or "verifier" not in a["agentType"]:
            continue
        try:
            returned = json.loads(a["finalText"].strip())
        except json.JSONDecodeError:
            checks.append({"verbatim": False, "reason": "final message is not bare JSON"})
            continue
        on_disk = by_id.get(returned.get("runId"))
        checks.append({"verbatim": returned == on_disk,
                       "reason": "matches summary.json" if returned == on_disk else "differs from summary.json",
                       "touchedLogs": a["touchedLogs"]})
    return checks


def probe_marks(uses):
    for u in uses:
        if u["name"] == "Bash" and "MARK-" in u["seen"]:
            marks = [int(m) for m in MARK.findall(u["seen"])]
            gaps = [(a, b) for a, b in zip(marks, marks[1:]) if b != a + 1]
            gap_text = ""
            if gaps:
                a, b = gaps[0]
                i, j = u["seen"].find(f"MARK-{a:05d}"), u["seen"].find(f"MARK-{b:05d}")
                gap_text = u["seen"][i + 51:j][:400]
            return {"seenChars": u["seenChars"], "fullChars": u["fullChars"], "marksSeen": len(marks),
                    "firstMark": marks[0] if marks else None, "lastMark": marks[-1] if marks else None,
                    "gaps": gaps[:5], "textAtFirstGap": gap_text}
    return None


def answer_signals(text, scenario):
    signals = {"mentionsFailure": bool(re.search(r"\bfail", text, re.I))}
    if scenario == "R1":
        signals["namesTest"] = "ShowsAbsentSentinel" in text
        signals["quotesAssertion"] = "<absent>" in text
    if scenario == "R3":
        signals["namesCause"] = bool(re.search(r"unknown semantic matcher", text, re.I))
        signals["count39or78"] = bool(re.search(r"\b(39|78)\b", text))
    signals["mentionsFlakyProbe"] = "TryProbe_ServerReturns500" in text
    return signals


def analyse_run(run_dir):
    meta = json.load(open(os.path.join(run_dir, "meta.json")))
    main_path = os.path.join(run_dir, "transcript", "main.jsonl")
    if not os.path.exists(main_path):
        return None
    entries = load_jsonl(main_path)
    calls = model_calls(entries)
    call_index = {c["id"]: i for i, c in enumerate(calls)}
    uses = tool_events(entries)
    agents = subagents(run_dir)
    vruns = verification_runs(run_dir)
    main = summarise_calls(calls)
    sub_processed = sum(a["processed"] for a in agents)

    full_outputs = "\n".join([u["full"] or u["seen"] for u in uses if u["name"] == "Bash"] +
                             [a["bashOutput"] for a in agents])
    observed_failing = sorted({m.group(1) for m in FAILED_TEST.finditer(full_outputs)} |
                              {t for r in vruns for t in r["failingTests"]})

    stream_result = {}
    stream_path = os.path.join(run_dir, "stream.jsonl")
    if os.path.exists(stream_path):
        for e in load_jsonl(stream_path):
            if e.get("type") == "result":
                stream_result = {k: e.get(k) for k in ("total_cost_usd", "num_turns", "duration_ms", "is_error", "subtype")}

    longest_gap = 0.0
    for path in [main_path] + glob.glob(os.path.join(run_dir, "transcript", "session", "subagents", "agent-*.jsonl")):
        stamps = [e["timestamp"] for e in load_jsonl(path) if e.get("timestamp")]
        for a, b in zip(stamps, stamps[1:]):
            gap = (datetime.datetime.fromisoformat(b.replace("Z", "+00:00")) -
                   datetime.datetime.fromisoformat(a.replace("Z", "+00:00"))).total_seconds()
            longest_gap = max(longest_gap, gap)

    metrics = {
        **meta,
        "longestGapSeconds": round(longest_gap),
        "main": main,
        "footprint": main["contextLast"] - main["contextFirst"],
        "contextSeries": [c["context"] for c in calls],
        "mainTools": [u["name"] for u in uses],
        "agentsSpawned": [u["input"].get("subagent_type") for u in uses if u["name"] in ("Agent", "Task")],
        "verificationCommands": sum(1 for u in uses if u["name"] == "Bash" and VERIFY_COMMAND.search(u["input"].get("command", ""))),
        "mainTouchedLogs": sum(1 for u in uses if touches_log(u)),
        "biggestToolResultSeen": max((u["seenChars"] for u in uses), default=0),
        "mainSpill": spill_behaviour(uses),
        "mainToolCalls": [{"tool": u["name"], "callIndex": call_index.get(u.get("callId")),
                           "seenChars": u["seenChars"], "isError": u["isError"],
                           "spilled": bool(u["persistedPath"]), "cut": u["cut"],
                           "input": {k: (v[:160] if isinstance(v, str) else v) for k, v in u["input"].items()
                                     if k in ("command", "file_path", "pattern", "path", "subagent_type", "offset", "limit")}}
                          for u in uses],
        "truncationHintInMain": any(TRUNCATION_HINT.search(u["seen"]) for u in uses),
        "seenShorterThanFull": [(u["name"], u["seenChars"], u["fullChars"]) for u in uses if u["fullChars"] > u["seenChars"] + 200],
        "subagents": [{k: v for k, v in a.items() if k not in ("finalText", "bashOutput")} for a in agents],
        "subagentProcessed": sub_processed,
        "totalProcessed": main["processed"] + sub_processed,
        "verificationRuns": [{"runId": r["runId"], "overallStatus": r["summary"].get("overallStatus"),
                              "headline": r["summary"].get("headline")} for r in vruns],
        "observedFailingTests": observed_failing,
        "verifierFidelity": verifier_fidelity(agents, vruns),
        "answer": final_text(entries),
        "signals": answer_signals(final_text(entries), meta["scenario"]),
        "streamReported": stream_result,
        "probe": probe_marks(uses) if meta["arm"].startswith("P") else None,
    }
    with open(os.path.join(run_dir, "metrics.json"), "w") as fh:
        json.dump(metrics, fh, indent=2)
    return metrics


def main():
    exp = sys.argv[1]
    done = probes = 0
    for run_dir in sorted(glob.glob(os.path.join(exp, "data", "runs", "*"))):
        if os.path.exists(os.path.join(run_dir, "meta.json")):
            m = analyse_run(run_dir)
            if m:
                done += 1
                if m["arm"].startswith("P") and m["probe"]:
                    probes += 1
                    print(f'{m["arm"]} probe:', json.dumps(m["probe"]))
    print(f"analysed {done} runs ({probes} probes); per-run metrics written to data/runs/*/metrics.json; "
          f"run harness/report.py for results/")


if __name__ == "__main__":
    main()
