#!/bin/bash
#
# Part 3 of the verifier experiment: a check inside a long session.
#
#   harness/run-long.sh base               build the shared base session once
#   harness/run-long.sh <arm> <repeat>     one run; arm V | FB | F0 | FV; repeat 0 = pilot
#
# Every run forks the same base session (claude --resume <base> --fork-session),
# so every arm starts from an identical, long context. Then two turns follow in
# that session:
#   t1  the harness applies the R3 regression as "my teammates' changes": the
#       session knows nothing about it, and the tests are the spec
#   t2  the harness applies the R1 sentinel change as the user's own product
#       decision: the session knows the change is intended
# The arm's working rules (inputs/prompts/long-rules-<arm>.md) follow both turn
# prompts. The system prompt, tools and agents are the same in every session, so
# the shared prefix stays cacheable. The exception is FV (Part 3b), whose fixer
# confirms its own fix: it uses inputs/config/agents-long-fv.json, so its first
# call re-writes the cached prefix. After each turn the harness saves the net
# code change and re-runs the checks itself.
#
# Uses the Claude Code build that Parts 1 and 2 used (CLAUDE_BIN, default 2.1.306).
# Prints one line per run. Nothing from the sessions reaches stdout.

HERE="$(cd "$(dirname "$0")" && pwd)"
EXP="$(cd "$HERE/.." && pwd)"
WS="${WS:-$HOME/confit-verifier-exp}"
WORK="$WS/confit"
CLAUDE_BIN="${CLAUDE_BIN:-$HOME/.local/share/claude/versions/2.1.306}"
MODEL="claude-opus-5-5"
EFFORT="${EFFORT:-medium}"
BUDGET_USD="${BUDGET_USD:-6}"
LATTICE_PLUGIN="${LATTICE_PLUGIN:-$HOME/.claude/plugins/cache/lattice/lattice/2.1.0}"
CLEAN_PATH="$HOME/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin"
AGENTS="$EXP/inputs/config/agents-long.json"
TOOLS="Bash,Read,Grep,Glob,Agent,Edit"
CONFIG_PATCHES="$EXP/inputs/config/verbosity-normal.patch $EXP/inputs/config/failure-hint.patch"
LONG="$EXP/data/long"

ws() { python3 -I "$HERE/workspace.py" "$@"; }

turn() {  # turn <out-prefix> <prompt> [claude flags...]
  local out="$1" prompt="$2" start
  shift 2
  start=$(date +%s)
  (
    cd "$WORK" || exit 1
    env -i HOME="$HOME" USER="$USER" LOGNAME="$USER" SHELL=/bin/zsh TERM=xterm-256color \
      LANG=en_US.UTF-8 PATH="$CLEAN_PATH" ${ANTHROPIC_API_KEY:+ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY"} \
      DISABLE_AUTOUPDATER=1 \
      BASH_DEFAULT_TIMEOUT_MS=600000 \
      "$CLAUDE_BIN" -p "$prompt" \
        --model "$MODEL" --effort "$EFFORT" --agents "$AGENTS" \
        --output-format stream-json --verbose \
        --setting-sources project,local \
        --strict-mcp-config \
        --plugin-dir "$LATTICE_PLUGIN" \
        --permission-mode dontAsk \
        --allowedTools "$TOOLS" \
        --max-budget-usd "$BUDGET_USD" \
        "$@" < /dev/null
  ) > "$out.stream.jsonl" 2> "$out.stderr.log"
  echo "$? $(( $(date +%s) - start ))" > "$out.exit"
}

session_of() {
  python3 -I -c 'import json, sys
for line in open(sys.argv[1]):
    try:
        e = json.loads(line)
    except ValueError:
        continue
    if e.get("type") == "system" and e.get("subtype") == "init":
        print(e.get("session_id", ""))
        break' "$1"
}

new_runs() {  # new_runs <before-list> <dest-dir>: copy verification runs created since
  mkdir -p "$2"
  for run in $(ls "$WORK/tmp/verification" 2>/dev/null); do
    printf '%s\n' "$1" | grep -qx "$run" || cp -R "$WORK/tmp/verification/$run" "$2/"
  done
}

check() {  # check <out-prefix>: net change against pristine, then the harness's own verification
  local out="$1" dir
  ws diff --ws "$WS" --exclude Makefile .lattice/verification.yaml > "$out.net-change.diff"
  (cd "$WORK" && bash .lattice/scripts/run-verification.sh .lattice/verification.yaml) > "$out.final-verify.stdout" 2>&1
  dir="$(sed -n 's/^___VERIFICATION_RUN_DIR___://p' "$out.final-verify.stdout")"
  mkdir -p "$out.final-verify"
  [ -n "$dir" ] && cp "$WORK/$dir/summary.json" "$WORK/$dir/"*.log "$out.final-verify/" 2>/dev/null
}

copy_transcript() {  # copy_transcript <session-id> <dest-dir>
  local transcript
  transcript="$(ls "$HOME/.claude/projects"/*/"$1.jsonl" 2>/dev/null | head -1)"
  [ -n "$transcript" ] || return 1
  mkdir -p "$2"
  cp "$transcript" "$2/main.jsonl"
  [ -d "${transcript%.jsonl}" ] && cp -R "${transcript%.jsonl}" "$2/session"
  return 0
}

if [ "$1" = base ]; then
  mkdir -p "$LONG/base"
  if [ -f "$LONG/base/base.json" ]; then
    echo "base: already built"
    exit 0
  fi
  ws reset --ws "$WS" > /dev/null || exit 1
  ws apply --ws "$WS" $CONFIG_PATCHES || exit 1
  (cd "$WORK" && { make build; make unit; make component; }) > "$LONG/base/prewarm.log" 2>&1
  sid="$(uuidgen | tr 'A-Z' 'a-z')"
  turn "$LONG/base/base" "$(cat "$EXP/inputs/prompts/long-base.md")" --session-id "$sid"
  copy_transcript "$sid" "$LONG/base/transcript"
  ws reset --ws "$WS" > "$LONG/base/reset-after.json"
  python3 -I - "$LONG/base" "$sid" "$CLAUDE_BIN" <<'EOF'
import json, os, sys
dest, sid, binary = sys.argv[1:4]
seen, last = set(), 0
for line in open(os.path.join(dest, "base.stream.jsonl")):
    try:
        e = json.loads(line)
    except ValueError:
        continue
    if e.get("type") == "assistant" and not e.get("parent_tool_use_id") and e["message"]["id"] not in seen:
        seen.add(e["message"]["id"])
        u = e["message"].get("usage", {})
        last = u.get("input_tokens", 0) + u.get("cache_creation_input_tokens", 0) + u.get("cache_read_input_tokens", 0)
code, wall = open(os.path.join(dest, "base.exit")).read().split()
after = json.load(open(os.path.join(dest, "reset-after.json")))
base = {"sessionId": sid, "claudeVersion": os.path.basename(binary), "exitCode": int(code), "wallSeconds": int(wall),
        "lastContextTokens": last, "filesChangedBySession": sorted(after["restored"] + after["deleted"])}
json.dump(base, open(os.path.join(dest, "base.json"), "w"), indent=2)
print(f"base  session={sid[:8]}  context={last:,} tokens  {wall}s  exit={code}")
EOF
  exit 0
fi

arm="$1"; repeat="$2"
[ $# -eq 2 ] || { echo "usage: run-long.sh base | <V|FB|F0|FV> <repeat>"; exit 2; }
case "$arm" in V|FB|F0|FV) ;; *) echo "run-long: unknown arm $arm"; exit 2 ;; esac
[ "$arm" = FV ] && AGENTS="$EXP/inputs/config/agents-long-fv.json"
[ -f "$LONG/base/base.json" ] || { echo "run-long: build the base first (run-long.sh base)"; exit 2; }
base_sid="$(python3 -I -c 'import json, sys; print(json.load(open(sys.argv[1]))["sessionId"])' "$LONG/base/base.json")"

run_id="L-$arm-r$repeat"
dest="$LONG/runs/$run_id"
if [ -f "$dest/meta.json" ]; then
  echo "$run_id: already done, skipped"
  exit 0
fi
rm -rf "$dest"
mkdir -p "$dest"
rules="$(cat "$EXP/inputs/prompts/long-rules-$arm.md")"

ws reset --ws "$WS" > "$dest/reset-before.json" || exit 1
ws apply --ws "$WS" $CONFIG_PATCHES "$EXP/inputs/scenarios/R3-many.patch" || exit 1
(cd "$WORK" && { make build; make unit; make component; }) > "$dest/prewarm.log" 2>&1

before="$(ls "$WORK/tmp/verification" 2>/dev/null)"
turn "$dest/t1" "$(cat "$EXP/inputs/prompts/long-t1.md")
$rules" --resume "$base_sid" --fork-session
sid="$(session_of "$dest/t1.stream.jsonl")"
new_runs "$before" "$dest/verification/t1"
check "$dest/t1"

# Turn 2: the user's own change, on top of whatever turn 1 left.
patch_ok=1
ws apply --ws "$WS" "$EXP/inputs/scenarios/R1-few.patch" || patch_ok=0
before="$(ls "$WORK/tmp/verification" 2>/dev/null)"
if [ -n "$sid" ] && [ "$patch_ok" = 1 ]; then
  turn "$dest/t2" "$(cat "$EXP/inputs/prompts/long-t2.md")
$rules" --resume "$sid"
fi
new_runs "$before" "$dest/verification/t2"
check "$dest/t2"
cp "$WORK/src/ConfIT/Matching/DeltaFormatter.cs" "$dest/t2.DeltaFormatter.cs" 2>/dev/null
cp "$WORK/test/ConfIT.UnitTest/Util/DeltaFormatterTests.cs" "$dest/t2.DeltaFormatterTests.cs" 2>/dev/null

[ -n "$sid" ] && copy_transcript "$sid" "$dest/transcript"
ws reset --ws "$WS" > "$dest/reset-after.json"

python3 -I - "$dest" "$run_id" "$arm" "$repeat" "$base_sid" "$sid" "$patch_ok" "$CLAUDE_BIN" <<'EOF'
import json, os, re, sys
dest, run_id, arm, repeat, base_sid, sid, patch_ok, binary = sys.argv[1:9]

def turn_meta(t):
    meta = {}
    if os.path.exists(os.path.join(dest, f"{t}.exit")):
        code, wall = open(os.path.join(dest, f"{t}.exit")).read().split()
        meta.update(exitCode=int(code), wallSeconds=int(wall))
    final = os.path.join(dest, f"{t}.final-verify", "summary.json")
    meta["finalStatus"] = json.load(open(final))["overallStatus"] if os.path.exists(final) else "unknown"
    diff = open(os.path.join(dest, f"{t}.net-change.diff")).read()
    meta["netChangeFiles"] = re.findall(r"^\+\+\+ b/(\S+)", diff, re.M)
    meta["netChangeLines"] = sum(1 for l in diff.splitlines() if l[:1] in "+-" and l[:3] not in ("+++", "---"))
    meta["testsChanged"] = [f for f in meta["netChangeFiles"] if f.startswith("test/")]
    return meta

t1, t2 = turn_meta("t1"), turn_meta("t2")
t1["matcherRestored"] = not any("SemanticMatcher" in f for f in t1["netChangeFiles"])
t1["filesBeyondRegression"] = [f for f in t1["netChangeFiles"] if "SemanticMatcher" not in f]
formatter = open(os.path.join(dest, "t2.DeltaFormatter.cs")).read() if os.path.exists(os.path.join(dest, "t2.DeltaFormatter.cs")) else ""
tests = open(os.path.join(dest, "t2.DeltaFormatterTests.cs")).read() if os.path.exists(os.path.join(dest, "t2.DeltaFormatterTests.cs")) else ""
t2["patchApplied"] = patch_ok == "1"
t2["featureKept"] = '"<not present>"' in formatter and '"<absent>"' not in formatter
t2["testUpdated"] = "<not present>" in tests and "<absent>" not in tests
meta = {"runId": run_id, "arm": arm, "repeat": int(repeat), "baseSessionId": base_sid, "sessionId": sid,
        "claudeVersion": os.path.basename(binary), "t1": t1, "t2": t2}
json.dump(meta, open(os.path.join(dest, "meta.json"), "w"), indent=2)
def short(t, extra):
    return f"{t.get('finalStatus')}/{t.get('wallSeconds', '-')}s {extra}"
print(f"{run_id:<8} session={sid[:8] if sid else '-'}  "
      f"t1={short(t1, 'restored' if t1['matcherRestored'] else 'changed:' + ','.join(t1['netChangeFiles']))}  "
      f"t2={short(t2, ('kept' if t2['featureKept'] else 'REVERTED') + ('+test' if t2['testUpdated'] else ''))}")
EOF
