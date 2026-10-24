#!/bin/bash
#
# One measured session of the verifier experiment.
#
#   harness/run-one.sh <arm> <scenario> <config> <repeat>
#     arm       A | B | C | D | H | P1 | P2   (inputs/prompts/arm-<arm>.md; H is D
#               with inputs/config/failure-hint.patch applied to verification.yaml)
#               T | S  fix workflow on Opus: T delegates to a Haiku verifier
#               and a Sonnet diagnose-and-fix agent (inputs/config/agents-tiered.json,
#               hint applied); S verifies, diagnoses and fixes alone;
#               O is T with every role on Opus; T2 is T with tightened agent
#               instructions (inputs/config/agents-tiered-opus.json, -v2.json)
#     scenario  G | R1 | R3               (inputs/scenarios/<scenario>-*.patch; G = none)
#     config    quiet | normal | detailed (inputs/config/verbosity-<config>.patch; quiet = none)
#     repeat    0 for pilot runs, 1..n for measured runs
#
# Resets the disposable ConfIT workspace, applies the config and scenario
# patches, runs every stage once so each arm starts from the same warm build,
# then runs one headless Claude Code session with the arm's prompt in a
# sanitized environment (env -i: nothing leaks in from the calling shell or
# from another Claude Code session). Everything the session produced is
# copied into data/runs/<run-id>/: stream-json output, the session transcript and
# any subagent transcripts, verification run directories created during the
# session, memory the session wrote, and meta.json. The workspace is reset
# afterwards, and any file the session changed is recorded as a deviation.
#
# Prints exactly one line. Nothing from the session reaches stdout.

HERE="$(cd "$(dirname "$0")" && pwd)"
EXP="$(cd "$HERE/.." && pwd)"
WS="${WS:-$HOME/confit-verifier-exp}"
WORK="$WS/confit"
MODEL="${MODEL:-claude-sonnet-5-5}"
EFFORT="${EFFORT:-medium}"
BUDGET_USD="${BUDGET_USD:-5}"
LATTICE_PLUGIN="${LATTICE_PLUGIN:-$HOME/.claude/plugins/cache/lattice/lattice/2.1.0}"
CLEAN_PATH="$HOME/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin"

arm="$1"; scenario="$2"; config="$3"; repeat="$4"
[ $# -eq 4 ] || { echo "usage: run-one.sh <arm> <scenario> <config> <repeat>"; exit 2; }

prompt_file="$EXP/inputs/prompts/arm-$arm.md"
case "$config" in
  quiet)           config_patch="" ;;
  normal|detailed) config_patch="$EXP/inputs/config/verbosity-$config.patch" ;;
  *)               echo "run-one: unknown config $config"; exit 2 ;;
esac
case "$scenario" in
  G)  scenario_patch="" ;;
  R1) scenario_patch="$EXP/inputs/scenarios/R1-few.patch" ;;
  R3) scenario_patch="$EXP/inputs/scenarios/R3-many.patch" ;;
  *)  echo "run-one: unknown scenario $scenario"; exit 2 ;;
esac
[ -f "$prompt_file" ] || { echo "run-one: no prompt for arm $arm"; exit 2; }
arm_patch=""
[ "$arm" = H ] && arm_patch="$EXP/inputs/config/failure-hint.patch"

main_model="$MODEL"; subagent_model="$MODEL"; agents_flag=""; fix_arm=0
tools="Bash,Read,Grep,Glob,Agent"
case "$arm" in
  T|O|T2)
     agents_file="agents-tiered.json"
     [ "$arm" = O ] && agents_file="agents-tiered-opus.json"
     [ "$arm" = T2 ] && agents_file="agents-tiered-v2.json"
     main_model="claude-opus-5-5"; subagent_model=""; agents_flag="--agents $EXP/inputs/config/$agents_file"
     arm_patch="$EXP/inputs/config/failure-hint.patch"; tools="Bash,Read,Grep,Glob,Agent,Edit"; fix_arm=1
     BUDGET_USD="${BUDGET_FIX_USD:-10}" ;;
  S) main_model="claude-opus-5-5"; tools="Bash,Read,Grep,Glob,Agent,Edit"; fix_arm=1
     BUDGET_USD="${BUDGET_FIX_USD:-10}" ;;
esac

run_id="$scenario-$config-$arm-r$repeat"
dest="$EXP/data/runs/$run_id"
if [ -f "$dest/meta.json" ]; then
  echo "$run_id: already done, skipped"
  exit 0
fi
rm -rf "$dest"
mkdir -p "$dest/transcript" "$dest/verification"

ws() { python3 -I "$HERE/workspace.py" "$@"; }

ws reset --ws "$WS" > "$dest/reset-before.json" || exit 1
patches="$(echo $config_patch $scenario_patch $arm_patch)"
[ -z "$patches" ] || ws apply --ws "$WS" $patches || exit 1

# Same warm state for every arm: each stage once, results discarded.
prewarm_start=$(date +%s)
(cd "$WORK" && { make build; make unit; make component; }) > "$dest/prewarm.log" 2>&1
prewarm_seconds=$(( $(date +%s) - prewarm_start ))

runs_before="$(ls "$WORK/tmp/verification" 2>/dev/null)"
session_id="$(uuidgen | tr 'A-Z' 'a-z')"
effort_flag=""
[ -n "$EFFORT" ] && effort_flag="--effort $EFFORT"

start_epoch=$(date +%s)
(
  cd "$WORK" || exit 1
  env -i HOME="$HOME" USER="$USER" LOGNAME="$USER" SHELL=/bin/zsh TERM=xterm-256color \
    LANG=en_US.UTF-8 PATH="$CLEAN_PATH" ${ANTHROPIC_API_KEY:+ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY"} \
    DISABLE_AUTOUPDATER=1 \
    BASH_DEFAULT_TIMEOUT_MS=600000 \
    ${subagent_model:+CLAUDE_CODE_SUBAGENT_MODEL="$subagent_model"} \
    claude -p "$(cat "$prompt_file")" \
      --model "$main_model" $effort_flag $agents_flag \
      --session-id "$session_id" \
      --output-format stream-json --verbose \
      --setting-sources project,local \
      --strict-mcp-config \
      --plugin-dir "$LATTICE_PLUGIN" \
      --permission-mode dontAsk \
      --allowedTools "$tools" \
      --max-budget-usd "$BUDGET_USD" \
      < /dev/null
) > "$dest/stream.jsonl" 2> "$dest/stderr.log"
exit_code=$?
end_epoch=$(date +%s)

transcript="$(ls "$HOME/.claude/projects"/*/"$session_id.jsonl" 2>/dev/null | head -1)"
if [ -n "$transcript" ]; then
  cp "$transcript" "$dest/transcript/main.jsonl"
  [ -d "${transcript%.jsonl}" ] && cp -R "${transcript%.jsonl}" "$dest/transcript/session"
  memory_dir="$(dirname "$transcript")/memory"
  if [ -d "$memory_dir" ]; then
    [ -n "$(find "$memory_dir" -type f)" ] && mv "$memory_dir" "$dest/memory-written" || rmdir "$memory_dir" 2>/dev/null
  fi
fi

for run in $(ls "$WORK/tmp/verification" 2>/dev/null); do
  printf '%s\n' "$runs_before" | grep -qx "$run" || cp -R "$WORK/tmp/verification/$run" "$dest/verification/"
done

# Fix arms: keep the net code change, and re-run the checks ourselves rather
# than take the session's word for the outcome.
if [ "$fix_arm" = 1 ]; then
  ws diff --ws "$WS" --exclude Makefile .lattice/verification.yaml > "$dest/net-change.diff"
  (cd "$WORK" && bash .lattice/scripts/run-verification.sh .lattice/verification.yaml) > "$dest/final-verify.stdout" 2>&1
  final_dir="$(sed -n 's/^___VERIFICATION_RUN_DIR___://p' "$dest/final-verify.stdout")"
  mkdir -p "$dest/final-verify"
  [ -n "$final_dir" ] && cp "$WORK/$final_dir/summary.json" "$WORK/$final_dir/"*.log "$dest/final-verify/" 2>/dev/null
fi

ws reset --ws "$WS" > "$dest/reset-after.json"

python3 -I - "$dest" <<EOF
import json, os, re, sys
dest = sys.argv[1]
patched = set()
for p in "$patches".split():
    patched |= set(re.findall(r"^\+\+\+ b/(\S+)", open(p).read(), re.M))
after = json.load(open(os.path.join(dest, "reset-after.json")))
changed = sorted(set(after["restored"]) - patched) + sorted(after["deleted"])
meta = {
    "runId": "$run_id", "arm": "$arm", "scenario": "$scenario", "config": "$config", "repeat": int("$repeat"),
    "sessionId": "$session_id", "model": "$main_model", "effort": "$EFFORT" or None, "budgetUsd": float("$BUDGET_USD"),
    "patches": "$patches".split(),
    "startEpoch": $start_epoch, "endEpoch": $end_epoch, "wallSeconds": $end_epoch - $start_epoch,
    "prewarmSeconds": $prewarm_seconds, "exitCode": $exit_code,
    "transcriptFound": bool("$transcript"),
    "filesChangedBySession": changed,
    "memoryWritten": os.path.isdir(os.path.join(dest, "memory-written")),
    "fixArm": bool($fix_arm),
}
if meta["fixArm"]:
    final = os.path.join(dest, "final-verify", "summary.json")
    meta["finalStatus"] = json.load(open(final))["overallStatus"] if os.path.exists(final) else "unknown"
    diff_text = open(os.path.join(dest, "net-change.diff")).read()
    meta["netChangeFiles"] = re.findall(r"^\+\+\+ b/(\S+)", diff_text, re.M)
    meta["netChangeLines"] = sum(1 for l in diff_text.splitlines() if l[:1] in "+-" and l[:3] not in ("+++", "---"))
    meta["testsEdited"] = any(re.search(r"(^test/|Tests?/|TestCase/)", f) for f in changed)
json.dump(meta, open(os.path.join(dest, "meta.json"), "w"), indent=2)
flags = [f for f, on in [("NO-TRANSCRIPT", not meta["transcriptFound"]), ("FILES-CHANGED", changed and not meta["fixArm"]),
                         ("final=%s changed=%d net=%d" % (meta.get("finalStatus"), len(changed), meta.get("netChangeLines", 0)), meta["fixArm"]),
                         ("TESTS-EDITED", meta.get("testsEdited")),
                         ("MEMORY", meta["memoryWritten"]), ("EXIT-%d" % meta["exitCode"], meta["exitCode"])] if on]
print(f'{meta["runId"]:<18} {meta["wallSeconds"]:>4}s  session={meta["sessionId"][:8]}  {" ".join(flags) or "ok"}')
EOF
