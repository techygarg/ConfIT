#!/bin/bash
#
# Phase 1 calibration for the verifier experiment. Runs no agent sessions.
#
# For each verbosity (quiet = Makefile default, normal, detailed) and scenario
# (G clean, R1 few failures, R3 many failures) it runs the vendored Lattice
# runner once in a disposable ConfIT workspace and keeps summary.json, the
# runner's stdout and every stage log. report_calibration.py turns those into
# what each arm would put in front of the model:
#   A (raw, main)     the stage logs concatenated; the && chain stops at the
#                     same first failure the runner does
#   B (script, main)  runner stdout + summary.json
#   D (verifier)      summary.json
# It also extracts the failing tests for R1 and R3 as ground truth.
#
# The ConfIT checkout is only read (git archive). Everything runs in $WS.

HERE="$(cd "$(dirname "$0")" && pwd)"
EXP="$(cd "$HERE/.." && pwd)"
CONFIT_SRC="${CONFIT_SRC:-$(git -C "$EXP" rev-parse --show-toplevel 2>/dev/null)}"
[ -n "$CONFIT_SRC" ] || { echo "calibrate: set CONFIT_SRC to a ConfIT checkout"; exit 2; }
COMMIT="${COMMIT:-a4f219e}"
WS="${WS:-$HOME/confit-verifier-exp}"
RUNNER="${RUNNER:-$HOME/.claude/plugins/cache/lattice/lattice/2.1.0/scripts/run-verification.sh}"
OUT="${OUT:-$EXP/data/calibration}"
WORK="$WS/confit"

ws() { python3 -I "$HERE/workspace.py" "$@"; }

patches_for() {   # patches_for <verbosity> <scenario>: one path per line
  [ "$1" = quiet ] || echo "$EXP/inputs/config/verbosity-$1.patch"
  case "$2" in
    R1) echo "$EXP/inputs/scenarios/R1-few.patch" ;;
    R3) echo "$EXP/inputs/scenarios/R3-many.patch" ;;
  esac
}

summary_field() {   # summary_field <summary.json> <key>
  sed -n "s/^  \"$2\": \"\{0,1\}\([^\"]*\)\"\{0,1\},\$/\1/p" "$1"
}

run_case() {   # run_case <verbosity> <scenario> <dest-dir>
  local verbosity="$1" scenario="$2" dest="$3" patch run_dir
  mkdir -p "$dest"
  ws reset --ws "$WS" > "$dest/reset.json" || exit 1
  for patch in $(patches_for "$verbosity" "$scenario"); do
    ws apply --ws "$WS" "$patch" || exit 1
  done

  (cd "$WORK" && bash .lattice/scripts/run-verification.sh .lattice/verification.yaml) > "$dest/runner.stdout" 2>&1
  run_dir="$(sed -n 's/^___VERIFICATION_RUN_DIR___://p' "$dest/runner.stdout")"
  if [ -z "$run_dir" ]; then
    echo "$(basename "$dest"): runner printed no run directory"
    return
  fi

  cp "$WORK/$run_dir/summary.json" "$WORK/$run_dir/"*.log "$dest/"
  printf '%-16s %-7s %4ss  %s\n' "$(basename "$dest")" \
    "$(summary_field "$dest/summary.json" overallStatus)" \
    "$(summary_field "$dest/summary.json" durationSeconds)" \
    "$(summary_field "$dest/summary.json" headline)"
}

mkdir -p "$OUT"
{
  echo "date_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "confit_commit: $COMMIT"
  echo "claude_code: $(claude --version 2>/dev/null)"
  echo "dotnet_sdk: $("$HOME/.dotnet/dotnet" --version 2>/dev/null)"
  echo "macos: $(sw_vers -productVersion)"
  echo "runner_sha256: $(shasum -a 256 "$RUNNER" | cut -d' ' -f1)"
} > "$OUT/env.txt"

if [ ! -d "$WS/pristine" ]; then
  ws init --ws "$WS" --src "$CONFIT_SRC" --commit "$COMMIT" --exp "$EXP" --runner "$RUNNER" > /dev/null || exit 1
fi

echo "warm-up (quiet G, discarded)"
run_case quiet G "$OUT/_warmup"
for verbosity in quiet normal detailed; do
  for scenario in G R1 R3; do
    run_case "$verbosity" "$scenario" "$OUT/$verbosity-$scenario"
  done
done
ws reset --ws "$WS" > "$OUT/final-reset.json"

python3 -I "$HERE/report_calibration.py" "$OUT" "$EXP/inputs/scenarios/ground-truth"
