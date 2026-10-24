# Predictions checked against the data

Predictions were frozen in `protocol.md` before the runs they describe. Status is computed by `harness/report.py`.

| ID | Prediction | Status | Evidence |
|---|---|---|---|
| P-1 | C lowest; B and D within 1.5K; A highest on R1 and R3; all under 15K on G and R1 | did not hold | C lowest everywhere: True; B≈D: False; A highest on red: False; all <15K on G/R1: False. G: A 2.5K, B 1.4K, C 922, D 1.0K; R1: A 7.2K, B 24.0K, C 1.2K, D 9.9K; R3: A 11.8K, B 10.2K, C 1.9K, D 12.9K |
| P-2 | A reads a spilled file selectively in at least 2 of 3 runs per scenario | held | 3 of 9 A runs spilled (failing commands are cut instead); 3 of those read selectively |
| P-3 | On normal G, A within 3K of D | held | A 2.5K vs D 1.0K |
| P-4 | On normal R3, A exceeds D by more than 3K | did not hold | A 11.8K vs D 12.9K |
| P-5 | Quiet R1: A at least 7K, D under 4K | held | A 7.0K vs D 4.0K |
| P-6 | Quiet G: A cheaper than D | did not hold | A 5.2K vs D 1.1K |
| P-7 | Total processed: D > A and C > D on every scenario | did not hold | G: A 92.5K, C 155.4K, D 100.9K; R1: A 137.1K, C 190.8K, D 219.9K; R3: A 182.0K, C 225.9K, D 265.7K |
| P-8 | Verifier returns summary.json unchanged and opens no log, every run | did not hold | verbatim 18/19; opened a log 0/19 |
| P-9 | At least 5 of 6 correct per arm on R1 and R3 | held | A 6/6, B 6/6, C 6/6, D 6/6 |
| P-10 | H at most half of D on R1, below D on R3, as correct as D; grep-only log access in 5 of 6 | held | R1: H 3.7K vs D 9.9K; R3: H 6.5K vs D 12.9K; correct H 6/6 vs D 6; grep-only 6/6 |

## Part 2

| ID | Prediction | Status | Evidence |
|---|---|---|---|
| P-11 | T: fixer runs no command, orchestrator runs/reads/edits nothing, checks run exactly twice in 5 of 6; S runs the checks 3+ times in 3 of 4 | held | fixer commands 0; orchestrator clean True; T exactly two runs 6/6; S three or more 4/4 |
| P-12 | Final check green in 5 of 6 T and 3 of 4 S red runs; no test edits | held | T 6/6, S 4/4, runs editing tests 0 |
| P-13 | T orchestrator footprint at most half of S's | held | T 4.1K vs S 20.2K |
| P-14 | T total cost below S's | held | T $0.17 vs S $0.32 |
| P-15 | Green: one verification, orchestrator under 2K, cost under $0.05 | held | one run each True; footprint 1.2K; cost $0.036 |
| P-16 | Haiku verifier verbatim in at least 85% of calls | did not hold | 11/14 |
| P-17 | O footprint within 25% of T's; O runs the suite exactly twice; O cost at least S's | held | O 4.0K vs T 4.1K; exactly two runs 4/4; cost O $0.49 vs S $0.32 vs T $0.17 |
| P-18 | Opus fixer reverts R3 exactly under the original instruction | held | exact revert 2/2; net change files [[], []] |
| P-19 | T2: verifier verbatim in 11 of 12; exact revert 6 of 6 with a diff in the reply; footprint, suite runs and cost within 25% of T | did not hold | verbatim 12/12; exact revert 5/6; diff in reply 6/6; footprint 4.4K vs 4.1K; cost $0.21 vs $0.17; suite runs 2.0 vs 2.0 |
