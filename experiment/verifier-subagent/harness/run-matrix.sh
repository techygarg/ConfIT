#!/bin/bash
#
# Runs a batch of sessions one at a time. Resumable: finished runs are skipped.
#
#   harness/run-matrix.sh probe | pilot | core | control | hint | tiered-pilot | tiered | tiered2
#
#   probe    P1: what Claude Code shows the model for an output over 30K characters
#            P2: the same output from a command that exits non-zero
#   pilot    A, B, C, D once each on G and R1 (normal config), repeat 0
#   core     A, B, C, D x G, R1, R3 (normal config) x 3 repeats
#   control  A, D x G, R1 (quiet config) x 2 repeats
#   hint     H (D plus a failureHint in verification.yaml) x R1, R3 (normal) x 3 repeats
#   tiered   fix workflow on Opus: T (Haiku verifier + Sonnet fixer) x R1, R3 x 3 and G x 2;
#            S (Opus alone) x R1, R3 x 2. tiered-pilot: one T run on R1
#   tiered2  O (T, all Opus) x R1, R3 x 2; T2 (T, tightened instructions) x R1, R3 x 3
#
# Arm order is shuffled within each scenario block with a fixed seed, so a
# resumed batch runs in the same order. Repeats are interleaved over time
# (all of repeat 1, then repeat 2, ...) rather than run back to back.

HERE="$(cd "$(dirname "$0")" && pwd)"
batch="$1"

shuffled() {   # shuffled <seed> <items...>
  python3 -I -c 'import random, sys; r = random.Random(sys.argv[1]); items = sys.argv[2:]; r.shuffle(items); print(" ".join(items))' "$@"
}

cells() {
  case "$batch" in
    probe)
      echo "P1 G quiet 0"; echo "P2 G quiet 0" ;;
    pilot)
      for s in G R1; do for a in A B C D; do echo "$a $s normal 0"; done; done ;;
    core)
      for r in 1 2 3; do for s in G R1 R3; do
        for a in $(shuffled "core-$s-$r" A B C D); do echo "$a $s normal $r"; done
      done; done ;;
    hint)
      for r in 1 2 3; do for s in R1 R3; do echo "H $s normal $r"; done; done ;;
    tiered-pilot)
      echo "T R1 normal 0" ;;
    tiered)
      for r in 1 2 3; do
        for s in R1 R3; do
          if [ "$r" -le 2 ]; then
            for a in $(shuffled "tiered-$s-$r" T S); do echo "$a $s normal $r"; done
          else
            echo "T $s normal $r"
          fi
        done
        [ "$r" -le 2 ] && echo "T G normal $r"
      done ;;
    tiered2)
      for r in 1 2 3; do
        for s in R1 R3; do
          if [ "$r" -le 2 ]; then
            for a in $(shuffled "tiered2-$s-$r" O T2); do echo "$a $s normal $r"; done
          else
            echo "T2 $s normal $r"
          fi
        done
      done ;;
    control)
      for r in 1 2; do for s in G R1; do
        for a in $(shuffled "control-$s-$r" A D); do echo "$a $s quiet $r"; done
      done; done ;;
    *)
      echo "usage: run-matrix.sh probe | pilot | core | control | hint | tiered-pilot | tiered | tiered2" >&2
      exit 2 ;;
  esac
}

cells | while read -r arm scenario config repeat; do
  bash "$HERE/run-one.sh" "$arm" "$scenario" "$config" "$repeat" < /dev/null
done
