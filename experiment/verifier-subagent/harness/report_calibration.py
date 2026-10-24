#!/usr/bin/env python3
"""Summarise a calibration run: what each arm would put in front of the model.

    report_calibration.py CALIBRATION_DIR GROUND_TRUTH_DIR

Reads <verbosity>-<scenario>/ directories written by calibrate.sh and writes
calibration.csv and calibration.md into CALIBRATION_DIR, plus
<scenario>.json ground truth (failing tests) into GROUND_TRUTH_DIR.
Token figures here are bytes/4 estimates; the agent runs measure real tokens.

The "cut zone" columns assume a 30,000-character tool-output cap that keeps
an equal head and tail. That is the claim under test, not a known fact about
the current Claude Code version; probe P1 settles it.
"""
import csv
import json
import os
import re
import sys

CAP = 30_000
FAILED_TEST = re.compile(r"^\s*Failed ([A-Za-z_][\w.]*(?:\(.*\))?) \[[^\]]*\]\s*$", re.M)
ASSEMBLY_SUMMARY = re.compile(
    r"^(Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+).*?-\s+(\S+)\s+\((\S+)\)\s*$",
    re.M)
VERBOSITY_ORDER = {"quiet": 0, "normal": 1, "detailed": 2}


def read(path):
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def in_cut_zone(offset, total):
    half = CAP // 2
    return total > CAP and half <= offset < total - half


def analyse_case(case_dir):
    summary_path = os.path.join(case_dir, "summary.json")
    summary = json.loads(read(summary_path))
    executed = [s for s in summary["stages"] if s["status"] in ("passed", "failed")]

    stage_bytes, raw = {}, ""
    for stage in executed:
        text = read(os.path.join(case_dir, stage["name"] + ".log"))
        stage_bytes[stage["name"]] = len(text.encode())
        raw += text

    failures = [(m.group(1), m.start()) for m in FAILED_TEST.finditer(raw)]
    visible = [name for name, offset in failures if not in_cut_zone(offset, len(raw))]
    assemblies = [m.groups() for m in ASSEMBLY_SUMMARY.finditer(raw)]

    return {
        "status": summary["overallStatus"],
        "headline": summary["headline"],
        "durationSeconds": summary["durationSeconds"],
        "stageBytes": stage_bytes,
        "rawBytes": len(raw.encode()),
        "rawLines": raw.count("\n"),
        "runnerStdoutBytes": os.path.getsize(os.path.join(case_dir, "runner.stdout")),
        "summaryBytes": os.path.getsize(summary_path),
        "failureLines": len(failures),
        "failingTests": sorted({name for name, _ in failures}),
        "firstFailureOffset": failures[0][1] if failures else None,
        "failureLinesOutsideCutZone": len(visible),
        "assemblies": [
            {"result": r, "failed": int(f), "passed": int(p), "skipped": int(s), "total": int(t), "dll": dll, "tfm": tfm}
            for r, f, p, s, t, dll, tfm in assemblies
        ],
    }


def main():
    out_dir, truth_dir = sys.argv[1], sys.argv[2]
    cases = {}
    for name in os.listdir(out_dir):
        parts = name.split("-", 1)
        if len(parts) == 2 and parts[0] in VERBOSITY_ORDER and os.path.isfile(os.path.join(out_dir, name, "summary.json")):
            cases[(parts[0], parts[1])] = analyse_case(os.path.join(out_dir, name))

    ordered = sorted(cases, key=lambda k: (k[1], VERBOSITY_ORDER[k[0]]))

    with open(os.path.join(out_dir, "calibration.csv"), "w", newline="") as fh:
        writer = csv.writer(fh)
        writer.writerow(["scenario", "verbosity", "status", "durationSeconds", "rawBytes", "rawLines",
                         "runnerStdoutBytes", "summaryBytes", "failureLines", "uniqueFailingTests",
                         "firstFailureOffset", "failureLinesOutsideCutZone", "stageBytes"])
        for key in ordered:
            c = cases[key]
            writer.writerow([key[1], key[0], c["status"], c["durationSeconds"], c["rawBytes"], c["rawLines"],
                             c["runnerStdoutBytes"], c["summaryBytes"], c["failureLines"], len(c["failingTests"]),
                             c["firstFailureOffset"], c["failureLinesOutsideCutZone"], json.dumps(c["stageBytes"])])

    lines = [
        "| scenario | verbosity | status | A: raw bytes (~tok) | B: runner stdout + summary | D: summary | raw / summary | failure lines (unique tests) | first failure at | failure lines outside cut zone |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for key in ordered:
        c = cases[key]
        first = "-" if c["firstFailureOffset"] is None else f'{c["firstFailureOffset"]:,} ({100 * c["firstFailureOffset"] / max(c["rawBytes"], 1):.0f}%)'
        outside = "-" if not c["failureLines"] else f'{c["failureLinesOutsideCutZone"]} of {c["failureLines"]}'
        lines.append(
            f'| {key[1]} | {key[0]} | {c["status"]} | {c["rawBytes"]:,} (~{c["rawBytes"] // 4:,}) '
            f'| {c["runnerStdoutBytes"] + c["summaryBytes"]:,} | {c["summaryBytes"]:,} '
            f'| {c["rawBytes"] / max(c["summaryBytes"], 1):.1f}x | {c["failureLines"]} ({len(c["failingTests"])}) '
            f'| {first} | {outside} |')
    table = "\n".join(lines)

    with open(os.path.join(out_dir, "calibration.md"), "w") as fh:
        fh.write("# Calibration\n\nOne runner pass per cell, no agent involved. Tokens are bytes/4 estimates.\n"
                 "Cut zone assumes a 30,000-character cap keeping an equal head and tail (unverified; probe P1).\n\n")
        fh.write(table + "\n")

    os.makedirs(truth_dir, exist_ok=True)
    for scenario in sorted({k[1] for k in cases if k[1] != "G"}):
        per_verbosity = {k[0]: cases[k] for k in cases if k[1] == scenario}
        reference = per_verbosity.get("quiet") or next(iter(per_verbosity.values()))
        truth = {
            "scenario": scenario,
            "failingTests": reference["failingTests"],
            "assemblies": reference["assemblies"],
            "headline": reference["headline"],
            "sameFailingTestsAtEveryVerbosity": all(
                c["failingTests"] == reference["failingTests"] for c in per_verbosity.values()),
        }
        with open(os.path.join(truth_dir, scenario + ".json"), "w") as fh:
            json.dump(truth, fh, indent=2)

    print(table)


if __name__ == "__main__":
    main()
