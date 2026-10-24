Make sure this repository's build and tests pass, fixing the code if they don't. Work only through subagents:
1. Spawn `verifier-haiku` to run the checks. It returns the run's summary.json.
2. If it reports a failure, spawn `fixer-sonnet` and give it that summary.json. It diagnoses from the existing logs and fixes the production code without rerunning anything.
3. Then spawn `verifier-haiku` again to confirm. Repeat steps 2 and 3 at most twice.
Do not run builds or tests, open logs, or edit files yourself. Finish with: the final status, what failed, the root cause, and what was changed.
