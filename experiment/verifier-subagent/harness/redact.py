#!/usr/bin/env python3
"""Build a shareable copy of the experiment without private data.

    redact.py EXPERIMENT_DIR OUT_DIR [--alias OLD_PATH=NAME ...]

Copies the documents, inputs/, harness/, results/, calibration summaries,
batch logs and per-run derived data (meta.json, metrics.json, verification
summary.json files, and for fix runs net-change.diff and the independent
re-check) into OUT_DIR. Part 3 (data/long/) gets the same treatment: the base
session's base.json, the batch script and log, and per run meta.json,
metrics.json, each turn's net change and re-check summary, and the
verification summary.json files. Raw session transcripts and
stream output are left out: they carry Claude Code's system prompt, the
account email and organisation id. Every copied text file has the home
directory, the email and any UUID-shaped account or organisation id replaced.
Paths are normalised too. Absolute paths into the experiment folder become
relative (in the current layout), each --alias path becomes its NAME (for
example the workspace becomes $WS), and Claude Code project-folder names
that embed the user name become <claude-project>. The final scan fails if
anything private survives, including the user name or the experiment
folder's real location.
"""
import os
import re
import shutil
import sys

HOME = os.path.expanduser("~")
USER = os.environ.get("USER") or os.path.basename(HOME)
PROJECT_KEY = re.compile(r"-Users-" + re.escape(USER) + r"-[A-Za-z0-9-]+")
PATH_MAP = []            # (absolute prefix, replacement), longest first; filled in main()
OLD_LAYOUT = [("config/", "inputs/config/"), ("scenarios/", "inputs/scenarios/"), ("prompts/", "inputs/prompts/"),
              ("runs/", "data/runs/"), ("calibration/", "data/calibration/")]
EMAIL = re.compile(r"[\w.+-]+@[\w-]+\.[\w.]+")
ID_FIELDS = re.compile(r'"(?:organizationUuid|ownerOrganizationUuid|ownerAccountUuid|accountUuid)"\s*:\s*"([0-9a-f-]{36})"')
ACCOUNT_FIELDS = re.compile(r'("(?:organizationUuid|ownerOrganizationUuid|ownerAccountUuid|accountUuid)"\s*:\s*")[^"]+(")')

INCLUDE_TOP = ["README.md", "PROTOCOL.md", "RESULTS.md", "inputs", "harness", "results"]
INCLUDE_CALIBRATION = ["calibration.md", "calibration.csv", "env.txt", "flake-check/runs.csv", "R3-component-if-run.log",
                       "R1-component-if-run.log"]
INCLUDE_RUN = ["meta.json", "metrics.json", "net-change.diff", "final-verify/summary.json"]
INCLUDE_LONG = ["base/base.json", "batch.sh", "batch.log", "batch-fv.sh", "batch-fv.log", "probe-fv/summary.json"]
INCLUDE_LONG_RUN = ["meta.json", "metrics.json", "t1.net-change.diff", "t2.net-change.diff",
                    "t1.final-verify/summary.json", "t2.final-verify/summary.json"]


PRIVATE_IDS = set()


def collect_private_ids(exp):
    """Account and organisation ids, read from the local transcripts so none is written in this file."""
    for root, _, files in [w for d in ("runs", "long") for w in os.walk(os.path.join(exp, "data", d))]:
        for name in files:
            if name.endswith(".jsonl"):
                with open(os.path.join(root, name), encoding="utf-8", errors="replace") as fh:
                    PRIVATE_IDS.update(ID_FIELDS.findall(fh.read()))


def scrub(text):
    for prefix, replacement in PATH_MAP:
        text = text.replace(prefix, replacement)
    text = PROJECT_KEY.sub("<claude-project>", text)
    text = ACCOUNT_FIELDS.sub(r"\1<redacted>\2", text)
    text = text.replace(HOME, "~")
    text = EMAIL.sub("<email>", text)
    for private_id in PRIVATE_IDS:
        text = text.replace(private_id, "<id>")
    return text


def copy(src, dst):
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    try:
        text = open(src, encoding="utf-8").read()
    except UnicodeDecodeError:
        shutil.copyfile(src, dst)
        return
    open(dst, "w", encoding="utf-8").write(scrub(text))


def copy_tree(src, dst):
    for root, _, files in os.walk(src):
        for name in files:
            if name.endswith((".pyc", ".DS_Store")):
                continue
            path = os.path.join(root, name)
            copy(path, os.path.join(dst, os.path.relpath(path, src)))


def main():
    exp, out = sys.argv[1], sys.argv[2]
    aliases = [a.split("=", 1) for a in sys.argv[3:] if "=" in a and not a.startswith("--alias")]
    aliases += [sys.argv[i + 1].split("=", 1) for i, a in enumerate(sys.argv) if a == "--alias" and i + 1 < len(sys.argv)]
    real_exp = os.path.realpath(exp)
    for old, new in OLD_LAYOUT:
        PATH_MAP.append((os.path.join(real_exp, old), new))
    PATH_MAP.extend([(real_exp + "/", ""), (real_exp, ".")])
    PATH_MAP.extend((os.path.realpath(old), new) for old, new in aliases)
    PATH_MAP.sort(key=lambda pair: len(pair[0]), reverse=True)
    collect_private_ids(exp)
    if os.path.exists(out):
        shutil.rmtree(out)
    for item in INCLUDE_TOP:
        path = os.path.join(exp, item)
        if os.path.isdir(path):
            copy_tree(path, os.path.join(out, item))
        elif os.path.isfile(path):
            copy(path, os.path.join(out, item))
    for item in INCLUDE_CALIBRATION:
        path = os.path.join(exp, "data", "calibration", item)
        if os.path.isfile(path):
            copy(path, os.path.join(out, "data", "calibration", item))
    calibration = os.path.join(exp, "data", "calibration")
    for cell in sorted(os.listdir(calibration)):
        if re.fullmatch(r"(quiet|normal|detailed)-(G|R1|R3)", cell):
            for name in sorted(os.listdir(os.path.join(calibration, cell))):
                if name == "summary.json" or name == "runner.stdout" or name.endswith(".log"):
                    copy(os.path.join(calibration, cell, name), os.path.join(out, "data", "calibration", cell, name))
    logs = os.path.join(exp, "data", "batch-logs")
    if os.path.isdir(logs):
        copy_tree(logs, os.path.join(out, "data", "batch-logs"))
    runs = os.path.join(exp, "data", "runs")
    for run in sorted(os.listdir(runs)):
        for item in INCLUDE_RUN:
            path = os.path.join(runs, run, item)
            if os.path.isfile(path):
                copy(path, os.path.join(out, "data", "runs", run, item))
        for root, _, files in os.walk(os.path.join(runs, run, "verification")):
            for name in files:
                if name == "summary.json":
                    path = os.path.join(root, name)
                    copy(path, os.path.join(out, "data", "runs", run, os.path.relpath(path, os.path.join(runs, run))))

    long_dir = os.path.join(exp, "data", "long")
    for item in INCLUDE_LONG:
        path = os.path.join(long_dir, item)
        if os.path.isfile(path):
            copy(path, os.path.join(out, "data", "long", item))
    for run in sorted(os.listdir(os.path.join(long_dir, "runs"))) if os.path.isdir(os.path.join(long_dir, "runs")) else []:
        src = os.path.join(long_dir, "runs", run)
        for item in INCLUDE_LONG_RUN:
            if os.path.isfile(os.path.join(src, item)):
                copy(os.path.join(src, item), os.path.join(out, "data", "long", "runs", run, item))
        for root, _, files in os.walk(os.path.join(src, "verification")):
            for name in files:
                if name == "summary.json":
                    path = os.path.join(root, name)
                    copy(path, os.path.join(out, "data", "long", "runs", run, os.path.relpath(path, src)))

    leaks = []
    for root, _, files in os.walk(out):
        for name in files:
            path = os.path.join(root, name)
            try:
                text = open(path, encoding="utf-8").read()
            except UnicodeDecodeError:
                continue
            found = [label for label, hit in (("home path", HOME in text), ("email", EMAIL.search(text)),
                                              ("account or org id", any(i in text for i in PRIVATE_IDS)),
                                              ("user name", re.search(r"\b" + re.escape(USER) + r"\b", text)),
                                              ("experiment location", os.path.dirname(real_exp) in text),
                                              ("aliased path", any(os.path.realpath(o) in text for o, _ in aliases))) if hit]
            leaks += [f"{os.path.relpath(path, out)}: {label}" for label in found]
    files = sum(len(f) for _, _, f in os.walk(out))
    if leaks:
        print(f"LEAKS FOUND in {len(leaks)} places:\n" + "\n".join(leaks[:20]))
        sys.exit(1)
    print(f"shareable pack: {files} files in {out}; scan clean (no home path, user name, email, account id or local path)")


if __name__ == "__main__":
    main()
