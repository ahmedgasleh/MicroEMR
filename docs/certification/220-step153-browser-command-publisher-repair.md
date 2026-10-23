# Step 153 — Browser command publisher repair

2026-10-23. **Browser automation BLOCKED.** Original extended-string serialization defect corrected, but the first credential-free end-to-end test failed at the **publisher result-return stage: Unicode corruption in redirected PowerShell output**. Chromium navigation, DOM assertions and screenshot capture succeeded. Stopped at the first failing stage as instructed; no repair retry, login request or clinical execution.

## Git checkpoint and scope

Read AGENTS.md and relevant helper/failure sections of [218](218-step151-playwright-helper-repair-readiness.md) and [219](219-step152-pc07-01-gate2-integrated-smoke-results.md). Recorded once: branch `feature/step-151-playwright-smoke-helper-repair`, HEAD `c92ec5c35b65e0f3ba8615203d70c79790d619c3`. Reports 217–219 were untracked; preserved unchanged.

Before modifying infrastructure, created/switched to requested LOCAL branch **`feature/step-153-browser-command-publisher-repair`**, using approved tool escalation for the local Git reference write. No commit, push, merge, reset, clean, stash, remote operation or product-code change. No database preflight/query, migration, backup operation, restore, service restart, credential retrieval, login, clinical request or clinical write. Previous passing suites/certification assessments were not repeated.

Inspection was limited to the rejected Step 152 command, helper and immediate reader/transport/startup dependencies. Only this report is a new tracked deliverable; infrastructure and evidence remain ignored under `.tmp`.

## Original root cause and expected schema

Actual rejected payload, abbreviated to exclude irrelevant metadata:

```json
{"id":1,"code":{"value":"const results=[];\n...","PSPath":".../controls-command.txt","ReadCount":1}}
```

`Get-Content -Raw` supplied an extended PowerShell string; `ConvertTo-Json` encoded its provider metadata as an object. The expected schema is exactly:

```json
{"id":1,"code":"JavaScript command text"}
```

`id` must be a positive safe integer; `code` must be a nonempty string; extra fields are rejected. The prior command's quotes, Unicode escapes, nested JSON and line breaks were legal JSON. The wrong **type of code**, not those characters or the Step 151 lock-retry fix, caused the rejection. Prior publication used direct `Set-Content` on the watched filename and was not atomic.

## Targeted infrastructure changes

- **Added `.tmp/step153/publish-command.ps1`:** reads script text with `[IO.File]::ReadAllText`, serializes a plain `{id,code}` object with `ConvertTo-Json`, writes UTF-8 to a unique temporary file, and invokes the shared Node schema validator before publication. Generates a GUID command filename unless given the helper's existing unique filename. Same-directory `File.Move` publishes a new command; `File.Replace` replaces an existing one while retaining a uniquely named previous payload. Waits for a matching result ID and returns an actionable timeout without retrying execution.
- **Updated `.tmp/step151/command-reader.cjs`:** exports one shared schema validator and a completed-file validation CLI with actionable, payload-free errors. Preserves the existing missing-file, partial-JSON and EBUSY/EACCES/EPERM retry behavior. Updated the immediate startup check's error-message assertion to match the new diagnostic; did not rerun that passing suite.
- **Added `.tmp/step153/command-transport.cjs`:** shared dispatch/result handling, atomic result publication and explicit schema rejection responses. Unexpected malformed envelopes return an error without closing the browser; execution errors still stop the real helper workflow. No raw command body is logged.
- **Updated `.tmp/step152/browser.cjs`:** uses the shared dispatcher and adds `--anonymous-transport-check` before any authenticated-path setup. Existing authenticated workflow is not invoked by that mode. Command ordering and save/request guards remain in place; no clinical behavior change exercised.
- **Added `.tmp/step153/verify.cjs`:** bounded PowerShell-publisher/shared-helper/Chromium/result acceptance harness, with only an anonymous loopback test page. Before-copies of helper and reader retained for diff review.

## Credential-free execution and first failure

Node syntax checks passed for the modified helper and new verification harness. Ran the actual helper entry point once with **`--anonymous-transport-check`**, using the existing Playwright-bundled Node and installed headless Chromium. No installation or build.

Evidence directory: **`.tmp/step153/run-YzVqN4`**. The harness starts an ephemeral local HTTP page and permits only GET to its exact `/probe` URL. Recorded browser traffic contains **one GET to `http://127.0.0.1:54718/probe`**; no clinical/API/Auth routes. Counts: **login attempts 0, clinical requests 0, clinical writes 0**.

The real publisher generated **`command-275f0604768149fb8e905a719208d327.json`**. Inspection confirms `code` is a string and the helper consumed it successfully. Chromium navigated to the local page, asserted its heading, filled/read back quoted text, backslashes, Unicode, multiline content and nested JSON, and captured a screenshot. The helper returned `pass:true`, title `Command transport probe` and the correct sample to the UTF-8 result file.

**The end-to-end test then failed:** the PowerShell publisher read that result and emitted it through redirected stdout. The receiver obtained corrupted Unicode, so the exact returned-result assertion failed before the first case could PASS:

```text
Expected Unicode: مرحبا Ω 🧪
Received Unicode: ????? � ??
Failing stage: publisher-browser-result
```

The saved `.json.result.json` still contains **`مرحبا Ω 🧪`** correctly. Thus the new defect is localized to the publisher's redirected result-output encoding, not the command JSON, browser execution or saved result. The exact console/code-page configuration was not separately measured. No encoding fix or test rerun was attempted after failure, honoring the instruction to report the failing stage and stop.

## Results and limits

| Required verification | Outcome |
|---|---|
| Valid serialized command and shared schema validation | Observed PASS in the first publication; code is now a string |
| Quotes/backslashes/Unicode/multiline/nested JSON in Chromium | DOM assertions PASS; saved result retains correct values |
| Publisher receives/returns exactly correct result | **FAIL — Unicode corrupted in redirected output** |
| Anonymous local navigation and DOM assertion | PASS — real Chromium, one permitted local GET |
| Screenshot capture/inspection | PASS — `anonymous-transport.png`, 17,335 bytes; inspected, local heading/sample visible |
| Malformed rejection/recovery acceptance cases | NOT EXECUTED — stopped before later cases |
| Distinct generated filenames across publications | NOT EXECUTED — only first unique GUID filename observed |
| Atomic replacement observation test | NOT EXECUTED — first new-file move succeeded; replacement not reached |
| Lock/retry end-to-end case | NOT EXECUTED — retain earlier Step 151 evidence, no new PASS |
| Published CLOSE command and clean-shutdown acknowledgment | NOT EXECUTED — failure cleanup awaited browser close and server close instead |

**Completed end-to-end cases: 0 PASS / 1 FAIL.** Successful substeps above do not satisfy the readiness gate. Later cases were prepared but not run. Screenshot inspection is not screenshot-regression acceptance; no approved baseline exists.

Evidence: `results.json`, the generated command/result pair, `edge-values.txt` and `anonymous-transport.png` in the run directory. The helper exited after failure cleanup; no authenticated context ever existed. Reviewed the helper/reader diffs against preserved copies and ran `git diff --check`; no unrelated tracked edits. No credentials, cookies or tokens logged or saved.

## Readiness and next bounded work

**Browser automation BLOCKED; renewed Gate 2 execution is not ready.** Do not request another clinical login until the full credential-free gate passes. The remaining infrastructure correction is to make the publisher's result channel explicitly UTF-8 (or use an equivalently lossless result channel), then run the complete planned publisher-to-browser acceptance including malformed, unique-file, replacement, lock/retry and CLOSE cases. This report authorizes no further action by itself.

Another clinical login would require renewed explicit authorization after readiness passes; none was requested or attempted here. No clinical acceptance closure. **PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Prescription, Medication Plan and Allergy owner wording/layout checks remain pending. Stop after this report.
