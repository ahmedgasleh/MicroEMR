# Step 154 — Browser Unicode transport verification

2026-10-23. **Browser automation READY for a separately authorized clinical attempt. Final credential-free run: 9 focused checks PASS, 0 FAIL. Exact returned Unicode PASS.** No login, credential retrieval, clinical request, clinical write or database access. **PC07.01 PARTIAL; inventory unchanged at 10 mandatory-gap IDs / three packages.**

## Git checkpoint and scope

Read AGENTS.md and the result-transport failure in [220](220-step153-browser-command-publisher-repair.md). Recorded once: branch `feature/step-153-browser-command-publisher-repair`, HEAD `c92ec5c35b65e0f3ba8615203d70c79790d619c3`; reports 217–220 were untracked and preserved unchanged.

Before edits, created/switched to the requested LOCAL branch **`feature/step-154-browser-unicode-result-repair`**, using approved local reference-write escalation. No commit, merge, push, reset, clean, stash, remote operation, product-code change, build, migration, restore, service restart or prior clinical/certification suite rerun. Only this report is a new tracked deliverable; test infrastructure and evidence remain ignored under `.tmp`.

Inspected only the publisher, its result-return/caller path, shared transport/reader dependencies and existing anonymous verification harness. No development database preflight or record access was needed.

## Exact loss boundary

A credential-free diagnostic read only Step 153's saved synthetic result and compared UTF-16 code units after PowerShell file decoding and after JSON serialization/reparsing. Both retained the exact expected values:

```text
Expected/helper/decoded/serialized: مرحبا Ω 🧪
Legacy redirected stdout result:  ????? � ??
UTF-16 units: 1605,1585,1581,1576,1575,32,937,32,55358,56810
```

Measured settings were **Console.OutputEncoding = IBM437**, **$OutputEncoding = us-ascii**. The saved helper JSON was correct UTF-8; PowerShell decoding and JSON serialization were correct. Loss occurred when normal PowerShell pipeline output was encoded to redirected standard-output bytes, before the caller's JSON parsing. No console/code-page setting was changed to hide the issue.

Diagnostic helpers/evidence: `.tmp/step154/diagnose.ps1`, `diagnose.cjs`, `decoding.json`, `diagnosis.json`, `legacy-stdout.bin`. The diagnostic intentionally reproduces the prior corruption; it is not the corrected acceptance run.

## Minimal repair

**Modified `.tmp/step153/publish-command.ps1`:**

- Decode result bytes explicitly with strict UTF-8; reject invalid bytes, malformed JSON and invalid result schema with payload-free actionable errors. A result must have a positive safe-integer ID and boolean `pass`; only the matching ID completes the request. No command retry occurs on errors.
- Serialize the response with the existing JSON serializer, encode it with `UTF8Encoding(false,true)`, and write those bytes directly through `Console.OpenStandardOutput()`. This bypasses console/pipeline code-page defaults rather than relying on them.
- Archive any previous result to a unique local filename before publishing the next command, preventing an old schema rejection from being mistaken for the new command's result. Existing command IDs, plain-string command serialization, shared validation, GUID paths and atomic command move/replace remain in place.

**Modified `.tmp/step153/verify.cjs`:** collect complete stdout byte buffers and decode with a strict UTF-8 `TextDecoder`, avoiding both default encodings and per-chunk multibyte splits. Extend the existing end-to-end harness with explicit returned-value equality and malformed-result cases. Preserve the unchanged shared dispatcher, Step 151 lock-tolerant reader and actual helper entry point.

Before-copies retained as `.tmp/step154/publisher-before.ps1` and `verifier-before.cjs`. No product code or authentication path changed.

## Focused execution and results

Ran the actual helper entry point with **`--anonymous-transport-check`** using the existing Playwright-bundled Node and installed headless Chromium. The harness invokes the actual PowerShell publisher as a subprocess, consumes the generated command through the real shared dispatcher, executes Chromium actions, writes the result, returns it through PowerShell stdout and parses it in the caller. No mocked browser or replacement serialization-only path.

An initial run, `.tmp/step154/run-cgsemX`, passed Unicode and malformed-result checks but exposed stale schema-rejection feedback during recovery: 4 checks had passed before that failure. The targeted previous-result archive correction above resolved it. Both runs are retained; the final run supersedes the initial readiness outcome.

**Final evidence: `.tmp/step154/run-YmL2rN/results.json`. Final result: 9 PASS / 0 FAIL.**

| Focused check | Final result |
|---|---|
| Actual PowerShell publisher → command file → shared helper → Chromium DOM/screenshot → returned publisher result | PASS |
| Quotes, backslashes, Unicode, multiline text/code and nested JSON round-trip | PASS — exact deep equality of returned payload |
| Explicit UTF-8 stdout decoding and valid JSON parsing | PASS |
| Invalid JSON, invalid UTF-8 and invalid result schema | PASS — three expected rejections; no execution retry |
| Malformed command rejection and subsequent corrected publication in the same browser session | PASS — actionable error; session retained |
| Separate generated command filenames | PASS — distinct GUID paths |
| Same-directory atomic command replacement | PASS — 71 concurrent observations, no partial payload |
| Exclusive command-file lock/retry | PASS — reproduced EBUSY; reader waited, Chromium survived, matching result returned after release |
| Published CLOSE and clean shutdown | PASS — acknowledgment received, context/browser closed, disconnected state verified |

### Exact Unicode round-trip

The result received **through PowerShell stdout**, not merely the saved file or browser-side assertion:

```text
Expected: مرحبا Ω 🧪
Actual:   مرحبا Ω 🧪
Equality: true
```

ASCII title `Command transport probe`, quotation marks, literal backslashes, LF/CRLF content and nested JSON also matched. The result evidence records `unicodeRoundTrip.pass=true` and `cleanShutdown=true`.

### Real Chromium and screenshot

The final browser made exactly **one GET to `http://127.0.0.1:62436/probe`**, an ephemeral anonymous local test page. The route guard permitted only this exact GET; no application, Auth, API or database endpoint was accessed. Chromium checked the heading, filled/read the synthetic sample and captured **`.tmp/step154/run-YmL2rN/anonymous-transport.png`**. Inspected screenshot: heading and sample control visible with the expected synthetic content. No screenshot-regression or clinical-layout acceptance is claimed.

Counts throughout: **0 login attempts, 0 credential/token retrievals, 0 clinical requests, 0 clinical writes**. The helper's anonymous mode runs before authenticated-path setup. Browser context and temporary local server shut down normally. No credentials, cookies or authentication tokens printed or saved.

## Readiness, limits and disposition

**Browser automation READY:** serialization, real publication, Chromium execution, exact publisher-received results and clean shutdown all passed in the final run. This closes the helper transport blocker, not Gate 2 clinical acceptance. The test uses an isolated anonymous local page; current OIDC session, clinical permissions, record state, live clinical UI and SQL persistence were intentionally not retested. Atomic/lock evidence applies to this local Windows filesystem test, not every filesystem or workload.

A future clinical attempt still requires **renewed explicit normal-login and execution authorization**, then only the bounded previously specified records/values and required current-session safety checks. No login approval requested or inferred here. Stop after credential-free verification and documentation.

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Prior passing evidence retained. Prescription, Medication Plan and Allergy subjective owner wording/layout checks remain pending.

Reviewed publisher/verifier diffs against the preserved before-copies; `git diff --check` passed. No further testing after the final passing run because no unresolved transport concern or further change remained.
