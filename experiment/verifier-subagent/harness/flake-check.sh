#!/bin/bash
#
# Measures the HttpReadinessProbeTests port race seen during calibration
# ("Address already in use" from TestPort.GetFree(): pick a free port,
# release it, bind it later). Runs the green unit stage N times as committed
# (both target frameworks in parallel) and N times with
# -p:TestTfmsInParallel=false, and records every failing test per run.
#
#   harness/flake-check.sh [N]      (default 15)

HERE="$(cd "$(dirname "$0")" && pwd)"
EXP="$(cd "$HERE/.." && pwd)"
WS="${WS:-$HOME/confit-verifier-exp}"
WORK="$WS/confit"
N="${1:-15}"
OUT="$EXP/data/calibration/flake-check"
BASE_OPTS='--logger "console;verbosity=normal" -nologo -p:WarningLevel=0 -p:NoWarn=NU1510'

mkdir -p "$OUT"
python3 -I "$HERE/workspace.py" reset --ws "$WS" > /dev/null || exit 1
echo "mode,run,exitCode,seconds,failingTests" > "$OUT/runs.csv"

for mode in parallel serial; do
  opts="$BASE_OPTS"
  [ "$mode" = serial ] && opts="$BASE_OPTS -p:TestTfmsInParallel=false"
  for i in $(seq 1 "$N"); do
    log="$OUT/$mode-$i.log"
    start=$(date +%s)
    (cd "$WORK" && make unit TEST_OPTS="$opts") > "$log" 2>&1
    code=$?
    secs=$(( $(date +%s) - start ))
    failing="$(sed -n 's/^ *Failed \(.*\) \[[^]]*\] *$/\1/p' "$log" | sort -u | tr '\n' ';')"
    echo "$mode,$i,$code,$secs,\"$failing\"" >> "$OUT/runs.csv"
  done
done

python3 -I - "$OUT/runs.csv" <<'EOF'
import csv, sys, statistics
rows = list(csv.DictReader(open(sys.argv[1])))
for mode in ("parallel", "serial"):
    r = [x for x in rows if x["mode"] == mode]
    bad = [x for x in r if x["exitCode"] != "0"]
    names = sorted({n for x in bad for n in x["failingTests"].split(";") if n})
    print(f"{mode:<8} failed {len(bad)}/{len(r)} runs, median {statistics.median(int(x['seconds']) for x in r)}s, tests: {names or '-'}")
EOF
