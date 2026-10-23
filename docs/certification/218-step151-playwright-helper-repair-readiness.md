# Step 151 — Playwright helper repair and Gate 2 readiness

2026-10-23. **Helper repair/startup verification PASS. STOPPED before login and clinical writes.** Renewed login AND Gate 2 execution authorization are required by the Step 151 request. **PC07.01 PARTIAL; inventory unchanged at 10 mandatory-gap IDs / three packages.**

## 1. Git checkpoint and bounded scope

Read AGENTS.md and the failure/disposition in [report 217](217-step150-pc07-01-gate2-controlled-clinical-smoke.md). Recorded once: branch `main`, HEAD `c92ec5c35b65e0f3ba8615203d70c79790d619c3`; only report 217 was untracked. Preserved it unchanged.

Before editing any helper, created/switched to the explicitly requested LOCAL branch **`feature/step-151-playwright-smoke-helper-repair`**. Initial sandbox reference-write denial was resolved through approved tool escalation; no automatic-review rejection. No commit, merge, push, reset, clean, stash or remote operation. No product-code changes, builds, service restarts, migrations, backup execution, integrity-verification rerun or restore. No previous passing suites or certification analysis repeated.

Inspected only the failed helper, its command/result files, existing Playwright/Chromium dependencies, immediate SQL snapshot helper, and scoped process metadata. Only this report is a new tracked deliverable; helper edits/evidence remain ignored under `.tmp`.

## 2. File-lock diagnosis

Exact retained Step 150 error:

```text
Error: EBUSY: resource busy or locked, open 'D:\MasterProgram_Ahmed\MicroEMR\.tmp\step150\command.json'
```

The failure occurs at Node's synchronous read of the shared **command JSON**, outside the per-command exception handler. The outer handler therefore closes the authenticated browser instead of waiting. The publisher used PowerShell `Set-Content` against the same file while Node polled every 400 ms. Checking existence first does not prevent a subsequent sharing violation or partial JSON read. The static filename also permits replay of a leftover command in a later run.

**Confirmed helper defect:** an unhandled transient command-file read failure terminates the session. A focused reproduction held a new command file with PowerShell `FileShare.None`; the original unguarded Node read produced the same **EBUSY** error. The repaired reader tolerated that lock and recovered the same command after release.

**Historical lock-holder qualification:** Step 150 did not capture the owning OS handle. Concurrent PowerShell publication is consistent with the recorded sequence and reproduced mechanism, but the exact historical handle owner cannot be conclusively attributed. This corrects any stronger inference in report 217; no antivirus/indexer attribution is established.

| Potential cause | Evidence/disposition |
|---|---|
| Concurrent command access / PowerShell–Node handoff | Shared file read/write identified; exclusive PowerShell writer reproduces exact EBUSY; missing read recovery confirmed |
| Browser profile reuse | Helper launches a fresh browser and isolated context; error names command JSON, not a profile |
| Screenshot/trace output | Failed before queued command execution; no screenshot/trace output implicated |
| Antivirus/file indexing | No historical lock-owner evidence; neither asserted nor disabled |
| Another running test process | Scoped current process inspection found no matching Step 150/151 Node helper or installed-headless-shell process; does not prove historical absence |

Initial sandbox process-metadata access was denied; approved read-only escalation completed the scoped check. No processes were stopped. No environment/service/security configuration was changed.

## 3. Small local repair and files

- **Modified `.tmp/step150/browser.cjs`:** replace unguarded read with shared tolerant reader; allocate a GUID command filename per run and print it at READY; preserve existing command-ID ordering, execution, request guards, browser context and clinical form behavior. Add `--startup-check` before selection/credential loading, and require explicit `--authorized-smoke` for the authenticated path. That flag is an operational guard, not authorization by itself.
- **Added `.tmp/step151/command-reader.cjs`:** return no command on missing file, transient EBUSY/EACCES/EPERM or incomplete JSON, allowing the existing next poll to retry without closing Chromium. Validate complete envelopes; unexpected I/O or invalid envelopes still fail explicitly. A permanently malformed file waits without executing it and requires operator correction.
- **Added `.tmp/step151/startup-check.cjs` and `hold-command.ps1`:** bounded, credential-free test using the same reader and real installed Chromium. PowerShell holds only a unique local test command file; it releases the handle in finally, with a 15-second maximum hold. No application route is visited.
- **Added this report.** `.tmp/step151/browser-before.cjs` preserves the exact original helper for diff review. Existing report 217, product files and Step 150 clinical evidence were preserved.

## 4. Focused verification — PASS

Used the existing Playwright-bundled node executable; no install or dependency update. Ran Node syntax check on the repaired helper, then the actual helper entry point with **`--startup-check`**, which does not load credentials or enter the login path.

**One focused startup/recovery run PASS, six checks PASS:** missing-command wait; partial-JSON wait; exclusive PowerShell lock tolerated while real Chromium remains usable; same command recovered after release; subsequent BOM-prefixed command read; invalid complete envelope rejected.

The unguarded read reproduced `EBUSY` on `.tmp/step151/startup-40f1XT/command.json`. Four repaired reads while locked returned no command without terminating the context. A Chromium input remained usable during the lock, then the same file was read successfully after release.

Evidence: **`.tmp/step151/startup-40f1XT/result.json`**. Observed counts: **network requests 0; login attempts 0; clinical writes 0**. The browser rendered only a local in-memory probe page and closed normally. No screenshots, traces, cookies, credentials or tokens saved. No full API/Auth/Playwright suites or Step 143/144 reruns.

## 5. Read-only record/backup readiness — PASS

Reused the existing bounded snapshot helper, without changing it, with `STEP150_SNAPSHOT=step151-readiness`. At **2026-10-23T16:35:02.8926792+00:00**, it confirmed:

- Endpoint **`192.168.50.1,50013`**, database **`MicroEMR_LocalDev_Fresh`**, server **`EC2AMAZ-TC26CQK\TELEMED52`**; tenant **`local-dev-fresh`**, UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`**.
- Patient **`9f65d4ca-c347-406a-bb32-c709a8941f80`**, plan **`2521d27e-172f-47a1-9326-72eea8b0f571`**, Draft **`ffd80110-e565-46de-b660-3d3f9dc7e463`**, finalized **`f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`** retain the same associations/states as the Step 150 after-snapshot.
- Exact serialized comparisons of all previously selected patient/plan/Draft/finalized/artifact/history columns are unchanged, including stored RowVersion values. Assessment remains NULL with **0 revisions**; Draft paper/interval remain NULL. Finalized record and artifact are unchanged. Scoped audit remains **79 entries**, also unchanged.
- The exact Step 149 backup path from report 216 remains a file: `file_exists=1`, `file_is_a_directory=0`, `parent_directory_exists=1`. Prior checksum/VERIFYONLY PASS reused; only availability rechecked, no backup operation or integrity rerun.

Snapshot evidence: `.tmp/step150/step151-readiness.json`; comparison `.tmp/step151/readiness-comparison.json`. Queries used existing schema and read-only permission procedure; no direct SQL clinical writes. Existing active account/prescriber binding and required effective permission assignment remain present, but **no current authenticated session is claimed**. Exact comparison is scoped to these records, not the entire database.

## 6. Authorization boundary and Step 152

**Helper technically ready; clinical smoke NOT EXECUTED.** The Step 151 instruction explicitly requires renewed **login and Gate 2 execution** authorization. No further login or clinical write was attempted. Prior permission/actor evidence does not replace current-session checks after renewed authorization.

Recommended **Step 152:** explicitly authorize one normal login and resumption of only report 217 section 4 for the exact listed account/records/values. Refresh necessary safety snapshot and normal form RowVersion reads, confirm current tenant/actor/permissions, then use the repaired helper's newly printed unique command filename. Preserve evidence separately before any new run. Complete only the bounded compliance/revision and Draft paper Yes/7/clear scope, optional single omission save, no-write validations, desktop/mobile/keyboard/visual checks and read-only SQL persistence/audit verification. No stale-write submission, restricted-account use without approval, extra records or mutations. Do not treat the CLI flag as permission to run now.

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Existing passing evidence retained; no clinical acceptance closure. Prescription, Medication Plan and Allergy owner wording/layout checks remain pending.

Reviewed the repaired helper against its preserved before-copy and this report for scope; focused syntax/startup checks passed. No unrelated tracked files changed. Stop here for renewed authorization.
