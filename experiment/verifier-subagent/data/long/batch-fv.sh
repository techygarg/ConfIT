#!/bin/bash
# Part 3b: three runs of FV (the fixer confirms its own fix), run a day after Part 3. Set WS (and CLAUDE_BIN) as for harness/run-long.sh.
cd "$(dirname "$0")/../.."
for repeat in 1 2 3; do
  bash harness/run-long.sh FV "$repeat"
done
