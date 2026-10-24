#!/usr/bin/env python3
"""Disposable ConfIT workspace for the verifier experiment.

    workspace.py init  --ws DIR --src CONFIT_CHECKOUT --commit SHA --exp EXPERIMENT_DIR --runner PATH
    workspace.py reset --ws DIR
    workspace.py apply --ws DIR PATCH [PATCH ...]

The pristine copy is exported with `git archive`, which only reads the source
repository. `reset` restores the working copy from pristine by content, not
by timestamp: a restored file gets a fresh mtime so MSBuild recompiles it,
while untouched files keep theirs and the build stays warm. bin/, obj/ and
tmp/ are never touched, so build output and verification run directories
survive resets. The JSON that reset prints lists every file it had to restore
or delete, which doubles as a check that a session left the tree alone.
"""
import argparse
import datetime
import difflib
import filecmp
import hashlib
import json
import os
import shutil
import subprocess
import sys

SKIP_DIRS = {"bin", "obj", "tmp"}


def files_under(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            yield os.path.relpath(os.path.join(dirpath, name), root)


def sha256(path):
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


def init(args):
    pristine = os.path.join(args.ws, "pristine")
    if os.path.exists(pristine):
        sys.exit(f"workspace: {pristine} already exists; delete it to re-export")
    os.makedirs(pristine)

    archive = subprocess.Popen(["git", "-C", args.src, "archive", args.commit], stdout=subprocess.PIPE)
    subprocess.run(["tar", "-x", "-C", pristine], stdin=archive.stdout, check=True)
    archive.stdout.close()
    if archive.wait() != 0:
        sys.exit("workspace: git archive failed")

    lattice = os.path.join(pristine, ".lattice")
    os.makedirs(os.path.join(lattice, "scripts"), exist_ok=True)
    shutil.copyfile(os.path.join(args.exp, "inputs", "config", "verification.yaml"),
                    os.path.join(lattice, "verification.yaml"))
    shutil.copyfile(args.runner, os.path.join(lattice, "scripts", "run-verification.sh"))

    meta = {
        "commit": args.commit,
        "source": args.src,
        "exportedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
        "runnerSha256": sha256(args.runner),
        "verificationYamlSha256": sha256(os.path.join(lattice, "verification.yaml")),
    }
    with open(os.path.join(args.ws, "pristine.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    print(json.dumps(meta))


def reset(args):
    pristine = os.path.join(args.ws, "pristine")
    work = os.path.join(args.ws, "confit")
    if not os.path.isdir(pristine):
        sys.exit("workspace: no pristine copy; run init first")

    expected = set(files_under(pristine))
    restored, deleted = [], []

    for rel in sorted(expected):
        src, dst = os.path.join(pristine, rel), os.path.join(work, rel)
        if os.path.isfile(dst) and filecmp.cmp(src, dst, shallow=False):
            continue
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        shutil.copymode(src, dst)
        restored.append(rel)

    for rel in sorted(set(files_under(work)) - expected):
        os.remove(os.path.join(work, rel))
        deleted.append(rel)

    print(json.dumps({"restored": restored, "deleted": deleted}))


def diff(args):
    """Unified diff of the working copy against pristine, i.e. the net change left behind.

    Files named in --exclude (those the config patches change) are skipped, so a
    session that reverts a scenario regression exactly leaves an empty diff.
    """
    pristine = os.path.join(args.ws, "pristine")
    work = os.path.join(args.ws, "confit")
    exclude = set(args.exclude or [])
    out = []
    for rel in sorted((set(files_under(pristine)) | set(files_under(work))) - exclude):
        a, b = os.path.join(pristine, rel), os.path.join(work, rel)
        if os.path.isfile(a) and os.path.isfile(b) and filecmp.cmp(a, b, shallow=False):
            continue
        old = open(a, errors="replace").readlines() if os.path.isfile(a) else []
        new = open(b, errors="replace").readlines() if os.path.isfile(b) else []
        out.extend(difflib.unified_diff(old, new, f"a/{rel}", f"b/{rel}"))
    sys.stdout.write("".join(out))


def apply(args):
    work = os.path.join(args.ws, "confit")
    for patch in args.patches:
        with open(patch, "rb") as fh:
            result = subprocess.run(["patch", "-p1", "-s", "-d", work], stdin=fh)
        if result.returncode != 0:
            sys.exit(f"workspace: patch failed: {patch}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p_init = sub.add_parser("init")
    p_init.add_argument("--ws", required=True)
    p_init.add_argument("--src", required=True)
    p_init.add_argument("--commit", required=True)
    p_init.add_argument("--exp", required=True)
    p_init.add_argument("--runner", required=True)
    p_init.set_defaults(func=init)

    p_reset = sub.add_parser("reset")
    p_reset.add_argument("--ws", required=True)
    p_reset.set_defaults(func=reset)

    p_diff = sub.add_parser("diff")
    p_diff.add_argument("--ws", required=True)
    p_diff.add_argument("--exclude", nargs="*")
    p_diff.set_defaults(func=diff)

    p_apply = sub.add_parser("apply")
    p_apply.add_argument("--ws", required=True)
    p_apply.add_argument("patches", nargs="+")
    p_apply.set_defaults(func=apply)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
