#!/bin/bash
# Part 3 matrix: three repeats of V, FB and F0, interleaved. Set WS (and CLAUDE_BIN) as for harness/run-long.sh.
cd "$(dirname "$0")/../.."
for repeat in 1 2 3; do
  for arm in V FB F0; do
    bash harness/run-long.sh "$arm" "$repeat"
  done
done
