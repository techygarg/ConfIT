# Answers digest

Verdict and correctness flags per run, with the opening of the answer. For review.

- **G-normal-A-r1** verdict=pass correct=yes (verdict=y)  
  All checks pass. I ran `make build && make unit && make component` and it exited cleanly, with no failing tests. - **Build:** succeeded. The only output was NU1902 warnings: `Scriban.Signed` 7.2.0 has two known moderate-severity vulnerabilities (GHSA-6q7j-xr2
- **G-normal-A-r2** verdict=pass correct=yes (verdict=y)  
  All checks pass. I ran `make build && make unit && make component` from the repository root. It finished without a failure, and I changed no files. - **Build:** 0 errors. NuGet warning NU1902 appeared repeatedly: `Scriban.Signed` 7.2.0 has a known moderate-se
- **G-normal-A-r3** verdict=pass correct=yes (verdict=y)  
  All checks pass. I ran `make build && make unit && make component` from the repository root, and none of the three steps failed. - **Build:** succeeded. It printed only NU1902 warnings, which flag a known moderate-severity vulnerability in `Scriban.Signed` 7.
- **G-normal-B-r1** verdict=pass correct=yes (verdict=y)  
  All checks pass. The summary reports `overallStatus: "passed"` with the headline "All 3 stage(s) passed". \| Stage \| Command \| Result \| Duration \| \|---\|---\|---\|---\| \| build \| `make build` \| passed (exit 0) \| 2s \| \| unit \| `make unit` \| passed (exit 0) \| 2s \| \|
- **G-normal-B-r2** verdict=pass correct=yes (verdict=y)  
  All checks pass. `summary.json` for run `20261021T071033Z-630091` reports `overallStatus: "passed"` and the headline "All 3 stage(s) passed". Nothing halted early. \| Stage \| Command \| Result \| Duration \| \|---\|---\|---\|---\| \| build \| `make build` \| passed (exit
- **G-normal-B-r3** verdict=pass correct=yes (verdict=y)  
  All checks pass. The summary reports `overallStatus: "passed"` with the headline "All 3 stage(s) passed". \| Stage \| Command \| Result \| Duration \| \|---\|---\|---\|---\| \| build \| `make build` \| passed (exit 0) \| 2s \| \| unit \| `make unit` \| passed (exit 0) \| 2s \| \|
- **G-normal-C-r1** verdict=pass correct=yes (verdict=y)  
  All checks pass. `make build && make unit && make component` exited 0, and no files were modified. \| Stage \| Result \| Passed \| Failed \| Skipped \| \|---\|---\|---\|---\|---\| \| build \| Passed \| n/a \| n/a \| n/a \| \| unit \| Passed \| 272 \| 0 \| 0 \| \| component \| Passed \|
- **G-normal-C-r2** verdict=pass correct=yes (verdict=y)  
  All three checks passed, according to the subagent's report. I didn't modify any files. - **`make build`**: It succeeded with 0 errors and 26 warnings. All the warnings are NuGet vulnerability advisories (NU1902/NU1903) for `Scriban.Signed` 7.2.0, `SQLitePCLR
- **G-normal-C-r3** verdict=pass correct=yes (verdict=y)  
  All checks pass. I based this on the subagent's report and didn't run anything myself. - **`make build`:** passed with 0 errors and 26 warnings. The warnings are all NuGet vulnerability notices, none of them compile errors. They cover `Scriban.Signed` 7.2.0, 
- **G-normal-D-r1** verdict=pass correct=yes (verdict=y)  
  All checks pass. The verifier ran three stages and each one exited 0: \| Stage \| Command \| Result \| \|---\|---\|---\| \| build \| `make build` \| passed \| \| unit \| `make unit` \| passed \| \| component \| `make component` \| passed \| The whole run took about 7 seconds. N
- **G-normal-D-r2** verdict=pass correct=yes (verdict=y)  
  All checks pass. The verifier ran 3 stages and all 3 passed (run `20261021T070941Z-f9e994`, 8 seconds in total): \| Stage \| Command \| Result \| Duration \| \|---\|---\|---\|---\| \| build \| `make build` \| passed (exit 0) \| 3s \| \| unit \| `make unit` \| passed (exit 0) \|
- **G-normal-D-r3** verdict=pass correct=yes (verdict=y)  
  All checks pass. The verifier ran 3 stages, and all of them succeeded with exit code 0: \| Stage \| Command \| Result \| Duration \| \|---\|---\|---\|---\| \| build \| `make build` \| passed \| 2s \| \| unit \| `make unit` \| passed \| 2s \| \| component \| `make component` \| pass
- **G-normal-T-r1** verdict=pass correct=yes (finalGreen=y, noTestEdits=y)  
  **Final status: passed.** The build and tests already pass, so I didn't need to fix anything. `verifier-haiku` ran the three stages set up in `.lattice/verification.yaml` (run `20261021T111042Z-5a27f3`): \| Stage \| Command \| Result \| Duration \| \|---\|---\|---\|-
- **G-normal-T-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y)  
  **Final status: passed.** The build and tests already pass, so I didn't change anything. I ran `verifier-haiku` once (run `20261021T111700Z-15ce87`). Since it reported no failure, I didn't spawn `fixer-sonnet` or run a confirmation pass. \| Stage \| Command \| 
- **G-quiet-A-r1** verdict=pass correct=yes (verdict=y)  
  All checks pass. I ran `make build && make unit && make component` and no files were modified. - **Build:** succeeded with 0 errors and 26 warnings. The warnings are NuGet vulnerability advisories (NU1902, NU1903), not compile problems: - `Scriban.Signed` 7
- **G-quiet-A-r2** verdict=pass correct=yes (verdict=y)  
  All checks pass. I ran `make build && make unit && make component` from the repository root. I didn't modify any files. - **Build:** succeeded with 0 errors. - **Unit tests:** all 272 passed on each of net10.0 and net9.0, with none failed or skipped. - **Comp
- **G-quiet-D-r1** verdict=pass correct=yes (verdict=y)  
  All checks pass. The verifier ran 3 stages, and none failed: \| Stage \| Command \| Result \| Duration \| \|---\|---\|---\|---\| \| build \| `make build` \| passed (exit 0) \| 2s \| \| unit \| `make unit` \| passed (exit 0) \| 3s \| \| component \| `make component` \| passed (exit 
- **G-quiet-D-r2** verdict=pass correct=yes (verdict=y)  
  All checks pass. The verifier ran 3 stages in 7 seconds, and each one exited with code 0: \| Stage \| Command \| Result \| \|---\|---\|---\| \| build \| `make build` \| passed \| \| unit \| `make unit` \| passed \| \| component \| `make component` \| passed \| No stage failed, 
- **R1-normal-A-r1** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The chained command stops at `make unit`, so `make component` never runs in the chain. \| Target \| Result \| \|---\|---\| \| `make build` \| Passes. Both net9.0 and net10.0 build. The only warnings are NU1902 advisories for `Scriban.Signed` 
- **R1-normal-A-r2** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The build succeeded, but `make unit` failed, so `make component` never ran. The `&&` chain stopped at `make: *** [unit] Error 1`. I didn't modify any files. - **Build:** passed, for net9.0 and net10.0. It printed repeated NU1902 warni
- **R1-normal-A-r3** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not everything passes.** `make build && make unit && make component` stops at `make unit`, so `make component` never runs in the chain. I ran the three steps separately to see each result. \| Step \| Result \| \|---\|---\| \| `make build` \| Passes. It prints NU190
- **R1-normal-B-r1** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  Not all checks pass. The build stage passed, but the unit stage failed, so the component stage was skipped. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed** (exit code 2) \| \| component
- **R1-normal-B-r2** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The run is `tmp/verification/20261021T071244Z-402e20`. The summary headline is "Failed at unit: exit code 2; skipped: component". \| Stage \| Status \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**: 271 of
- **R1-normal-B-r3** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The run halted at the unit stage, so the component stage never ran. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, exit code 2 \| \| component (`make component`) \| skipped because of th
- **R1-normal-C-r1** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  Not all checks pass. `make build` passed, `make unit` failed, and `make component` never ran because the `&&` chain stopped at the unit failure. This comes from the subagent's report, and I didn't check it independently. **`make build`: passed.** It had 0 err
- **R1-normal-C-r2** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  Not all checks pass. `make unit` fails on one test, so `make build && make unit && make component` would have stopped there. The subagent ran the three stages separately to see every result. \| Stage \| Result \| Counts \| \|---\|---\|---\| \| `make build` \| Passed \| 
- **R1-normal-C-r3** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  Not everything passes. The build succeeded, but `make unit` failed, so `make component` never ran. That means I have no component-test result. \| Step \| Result \| \|---\|---\| \| `make build` \| Passed with 0 errors and 26 warnings. The warnings are NuGet vulnerabil
- **R1-normal-D-r1** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  Not all checks pass. The build passed, but the unit stage failed, so the component stage was skipped. The run halted early, and the integration stage wasn't part of it. **Failing unit test:** 1 of 272 failed (271 passed). The test is `ConfIT.UnitTest.Util.Del
- **R1-normal-D-r2** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  Not all checks pass. The build passed, but the unit stage failed, and the component stage was skipped because the run halted early. I didn't modify any files. \| Stage \| Status \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| failed (exit c
- **R1-normal-D-r3** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The build passed, but the unit stage failed, so the component stage was skipped. The run halted early after one failing test. \| Stage \| Status \| \|---\|---\| \| build \| passed \| \| unit \| **failed** (exit code 2) \| \| component \| skipped \| 
- **R1-normal-H-r1** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The unit stage failed, so the component stage was skipped. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, exit code 2 \| \| component (`make component`) \| ski
- **R1-normal-H-r2** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The build passed, but the unit stage failed, so the component stage was skipped. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed** (exit 2): 271 of 272 tests pa
- **R1-normal-H-r3** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The run halted at the unit stage, so the component stage was skipped. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, exit code 2 (271 of 272 tests passed) \| \| component (`make compone
- **R1-normal-O-r1** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **The build and tests now pass.** The second verification run (`20261021T115126Z-3a1d32`) passed build, unit and component. It took one fix cycle. **What failed:** On the first run the build passed, but the unit stage failed (exit code 2), so component tests 
- **R1-normal-O-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passed.** The second verification run (`20261021T141514Z-497c5f`) passed all three configured stages: build, unit and component. It took one fix cycle. **What failed:** On the first run (`20261021T135228Z-886063`), the build passed but the uni
- **R1-normal-S-r1** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passing.** It took two runs: the first run failed and the run after the fix passed. - `make build`: 0 errors. The warnings are only NuGet vulnerability advisories, unrelated to this. - `make unit`: 272 of 272 passed, on both target frameworks.
- **R1-normal-S-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passing.** `make build && make unit && make component` now exits 0. That's 272/272 unit tests and 20/20 component tests. It took one fix and one re-run. **What failed:** one unit test, `DeltaFormatterTests.Format_FieldInActualNotInExpected_Sho
- **R1-normal-T-r1** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passed.** The second run (`20261021T110721Z-03b06c`) passed all three configured stages: build, unit and component. It took one fix cycle. **What failed:** On the first run (`20261021T110637Z-d075c7`), the unit stage failed on one test, `Delta
- **R1-normal-T-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passing.** On the second verification run (`20261021T111206Z-873876`), build, unit and component tests all passed. It took one fix. **What failed:** On the first run, the unit tests failed with exit code 2. The only failing test was `DeltaForm
- **R1-normal-T-r3** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passing.** One fix was needed. On the second run, build, unit and component tests all passed (run `20261021T111825Z-dd1bde`, summary at `tmp/verification/20261021T111825Z-dd1bde/summary.json`). **What failed:** On the first run the build passe
- **R1-normal-T2-r1** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passing.** On the second verification run (`20261021T114945Z-ac1d69`), all three configured stages passed: build, unit and component. It took one fix cycle. **What failed:** On the first run (`20261021T114859Z-5bf7d9`) the build passed but the
- **R1-normal-T2-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesTest=y)  
  **Final status: passing.** In the second verification run, all three configured stages passed: build, unit and component (run `20261021T141700Z-0f60dc`). It took one fix. **What failed:** On the first run, the unit stage failed with exit code 2 and the compon
- **R1-normal-T2-r3** verdict=fail correct=yes (finalGreen=y, noTestEdits=y, namesTest=y) FLAKY: ConfIT.UnitTest.Server.Http.HttpReadinessProbeTests.TryProbe_ServerReturns500_ReturnsFalse; answer mentions a known flaky test  
  **Final status: passed.** The second verification run (`20261021T142229Z-3a6d2d`) passed build, unit and component. The fix took one round. **What failed:** The first run (`20261021T142131Z-e141f1`) failed at the unit stage, so the component stage never ran. 
- **R1-quiet-A-r1** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** One unit test fails, on both target frameworks. I changed no files. \| Step \| Result \| \|---\|---\| \| `make build` \| Passed. It printed only NU1902 warnings about `Scriban.Signed` 7.2.0. \| \| `make unit` \| **Failed**: 1 failed and 271 pass
- **R1-quiet-A-r2** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** `make build` succeeded, but `make unit` failed on 1 test, so the chain stopped there. I ran `make component` separately, and it passed. No files were modified. \| Step \| Result \| \|---\|---\| \| `make build` \| Passed on net9.0 and net10.0.
- **R1-quiet-D-r1** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  Not all checks pass. The build passed, but the unit stage failed, so the component stage was skipped. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, exit code 2 \| \| component (`make
- **R1-quiet-D-r2** verdict=fail correct=yes (verdict=y, namesTest=y, quotesAssertion=y)  
  **Not all checks pass.** The unit stage failed, and the run stopped there. \| Stage \| Result \| \|---\|---\| \| build \| passed \| \| unit \| **failed** (exit code 2) \| \| component \| skipped because the run stopped after the unit failure \| Integration and `make skills
- **R3-normal-A-r1** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not all checks pass.** `make build` succeeded, but `make unit` failed with exit code 2, so `make component` never ran. Its result is unknown. **Build:** it succeeded for net9.0 and net10.0. The only warnings were NU1902, a known moderate-severity vulnerabil
- **R3-normal-A-r2** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  Not all checks pass. The build succeeded, but `make unit` failed, so `make component` never ran and I have no component results. **Build:** `make build` passed with 0 errors. It printed repeated `NU1902` warnings for `Scriban.Signed 7.2.0`, which has known mo
- **R3-normal-A-r3** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not all checks pass.** `make build` succeeded, but `make unit` failed, so `make component` never ran. **Unit tests:** 272 total per target framework, 233 passed, 39 failed. The same 39 fail on net9.0 and net10.0. I didn't modify any files. **Cause (all 39 
- **R3-normal-B-r1** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not all checks pass.** The build passed, but the unit stage failed, so the component stage was skipped. The run halted early (run `20261021T070604Z-99ba37`). \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, e
- **R3-normal-B-r2** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  Not all checks pass. The build stage passed, but the unit stage failed (`make unit`, exit 2), so the component stage was skipped and never ran. I changed no files. **Unit stage:** 39 of 272 tests failed and 233 passed. The log shows 78 failure lines, which is
- **R3-normal-B-r3** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  Not all checks pass. The build passed, but the unit stage failed, so the component stage was skipped. I didn't modify any files. **Run:** `tmp/verification/20261021T072252Z-cb49c2` - **build** (`make build`): passed - **unit** (`make unit`): failed with exit 
- **R3-normal-C-r1** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  The checks do not all pass. `make build` succeeded, but `make unit` failed, so `make component` never ran. `make` exited with status 2 (`make: *** [unit] Error 1`). The subagent changed no files, and its full output is in `/tmp/confit_run.log`. **Build:** it 
- **R3-normal-C-r2** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not everything passes.** `make build` succeeds, but `make unit` and `make component` both fail. The `&&` chain would have stopped at `make unit`, so the subagent ran each step separately. I didn't modify any files. \| Step \| Result \| \|---\|---\| \| `make build`
- **R3-normal-C-r3** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not everything passes.** The build succeeds, but the unit and component tests fail. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| `make build` \| Passed, with 26 NU1902/NU1903 vulnerable-package warnings and no errors \| \| `make unit` \| Failed: 23
- **R3-normal-D-r1** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not all checks pass.** The build passed, but the unit stage failed, so the component stage was skipped. I didn't open the component log or run it. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`)
- **R3-normal-D-r2** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not all checks pass.** The build passed, but the unit stage failed, so the component stage never ran. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, exit code 2 \| \| component (`ma
- **R3-normal-D-r3** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  Not all checks pass. The build passed, but the unit tests failed, and the component stage was skipped because the run stopped at the failure. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, exit code 2 \| \| com
- **R3-normal-H-r1** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  Not all checks pass. The build passed, but the unit tests failed, so the component stage was skipped. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| `build` \| passed \| \| `unit` \| **failed** (exit code 2): 39 of 272 tests failed, 233 passed \| \| `com
- **R3-normal-H-r2** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  **Not all checks pass.** The build passed, but the unit stage failed, so the component stage was skipped. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed** (exit code 2) \| \| component (
- **R3-normal-H-r3** verdict=fail correct=yes (verdict=y, namesCause=y, rightCount=y)  
  Not all checks pass. The build passed, but the unit tests failed, so the component stage was skipped. I didn't modify any files. \| Stage \| Result \| \|---\|---\| \| build (`make build`) \| passed \| \| unit (`make unit`) \| **failed**, exit code 2 \| \| component (`make
- **R3-normal-O-r1** verdict=fail correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  **Final status: passed.** The second verification run (`20261021T131158Z-a23f2a`) passed all three configured stages: build, unit and component. It took one fix cycle. **What failed:** The first run (`20261021T122110Z-836fe4`) failed at the `unit` stage with 
- **R3-normal-O-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  **Final status: passing.** Build, unit and component all pass in the confirming run (`tmp/verification/20261021T142041Z-ccca93`). It took one fix cycle. **What failed:** The first run passed the build but failed the unit stage (exit code 2), so the component 
- **R3-normal-S-r1** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesCause=y) FLAKY: ConfIT.UnitTest.Server.Http.HttpReadinessProbeTests.TryProbe_ServerReturns500_ReturnsFalse; answer mentions a known flaky test  
  `make build`, `make unit` and `make component` all pass now. It took one fix, on the second attempt. **Final status** - `make build`: 0 errors. The 26 warnings are NuGet vulnerability notices for package dependencies (Scriban.Signed, SQLitePCLRaw, System.Secu
- **R3-normal-S-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  **Final status: passing.** `make build && make unit && make component` exits 0: the build has 0 errors, unit tests pass 272/272 and component tests pass 20/20. It took one fix attempt, then two confirmation runs. **What failed:** `make build` and `make compon
- **R3-normal-T-r1** verdict=fail correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  The build and tests pass now. The second verification run (`20261021T111001Z-637c12`) passed all three configured stages: build, unit and component. It took one fix. **What failed:** The first run passed the build, then failed at the unit stage with exit code
- **R3-normal-T-r2** verdict=fail correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  **Final status: passing.** On the second run, build, unit and component all passed (run `20261021T111618Z-37d304`). It took one fix. **What failed:** The first run passed the build, then failed at the unit stage (exit code 2), so the component stage was skipp
- **R3-normal-T-r3** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  **Final status: passed.** Build, unit and component all pass in run `20261021T111948Z-bf62ff`. It took one fix, so only one of the two allowed fix rounds was used. **What failed:** On the first run, the build passed and the unit stage failed (exit code 2). Co
- **R3-normal-T2-r1** verdict=fail correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  **Final status: passing.** On the second verification run (`20261021T121443Z-02080b`), all three configured stages passed: `build`, `unit` and `component`. It took one fix cycle. **What failed:** The first run (`20261021T115835Z-343ce6`) failed at `unit`, so 
- **R3-normal-T2-r2** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesCause=y) FLAKY: ConfIT.UnitTest.Server.Http.HttpReadinessProbeTests.TryProbe_ServerReturns500_ReturnsFalse; answer mentions a known flaky test  
  **Final status: passing.** On the re-run (`20261021T141827Z-9baf83`), all three configured stages passed: build, unit and component. It took one fix cycle. **What failed:** In the first run (`20261021T141742Z-7ca267`) the build passed, but the unit stage exit
- **R3-normal-T2-r3** verdict=pass correct=yes (finalGreen=y, noTestEdits=y, namesCause=y)  
  **Final status: passed.** The second verification run (`tmp/verification/20261021T142402Z-c047dc`) passed build, unit and component. One fix cycle was enough. **What failed:** The first run passed build but failed the unit stage with exit code 2, so component
