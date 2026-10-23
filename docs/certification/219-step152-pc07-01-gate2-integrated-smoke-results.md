# Step 152 — PC07.01 Gate 2 integrated smoke results

2026-10-23. **Single normal login, authenticated tenant, effective permissions and clinical actor 2 PASS. Clinical smoke BLOCKED before writes by an invalid helper command envelope.** No assessment or Draft save occurred. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

## 1. Authorization, Git and scope

Read AGENTS.md, the Step 152 owner instruction and relevant sections of reports [217](217-step150-pc07-01-gate2-controlled-clinical-smoke.md) and [218](218-step151-playwright-helper-repair-readiness.md). The owner explicitly authorized one new normal login for `admin@microemr.local` and only the two specified compliance assessments and bounded existing correction-Draft paper/interval saves. Finalized prescription remains strictly read-only. The instruction additionally prohibits credential extraction, retries, stale-write submissions and direct SQL clinical writes.

Recorded once: branch **`feature/step-151-playwright-smoke-helper-repair`**, HEAD **`c92ec5c35b65e0f3ba8615203d70c79790d619c3`**. Reports 217 and 218 were untracked and preserved unchanged. No product code, Git mutation, commit, remote operation, build, migration, backup operation, restore, service restart, fixture/permission change or broad suite execution. Only this report added outside ignored local helpers/evidence.

## 2. Mandatory checks and single interactive login

Reused the existing read-only snapshot helper and inspected relevant 0089/0090 schema definitions before verification. At **2026-10-23T16:41:21.3092892+00:00**, fixed endpoint `192.168.50.1,50013` confirmed database `MicroEMR_LocalDev_Fresh`, actual server `EC2AMAZ-TC26CQK\TELEMED52`, tenant key `local-dev-fresh`, tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`. Comparison against the Step 151 snapshot confirmed unchanged identity, patient, plan, Draft, finalized prescription, artifact, history and scoped audit.

The exact Step 149 backup file from report 216 remains available (`file_exists=1`, not a directory). Prior checksum/VERIFYONLY PASS reused; no integrity rerun or backup operation.

| Authorized record | Identifier / expected state verified |
|---|---|
| Patient | `9f65d4ca-c347-406a-bb32-c709a8941f80`; not deleted |
| Medication plan | `2521d27e-172f-47a1-9326-72eea8b0f571`; owned by patient, Active, assessment NULL, 0 revisions |
| Correction Draft | `ffd80110-e565-46de-b660-3d3f9dc7e463`; owned by patient, Draft, paper/interval NULL, original prescriber 2, linked to finalized record below |
| Finalized prescription | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`; owned by patient, Finalized, unchanged artifact |

The repaired helper still had a source-seeded credential read in its authenticated path. **That path was not executed.** To honor Step 152's prohibition on credential extraction, the owner was asked to choose an interactive login and answered **“Use interactive login (Recommended)”**. A local `.tmp/step152/browser.cjs` copy removes credential-source loading, uses a separate Step 152 evidence directory and current before-snapshot, and retains the repaired shared reader, per-run GUID command filename and existing request guards. It opens installed Google Chrome through the existing Playwright Chromium driver, with a fresh isolated context and no saved session state. Only a syntax check was run; Step 151's passing startup checks were reused.

The first visible-mode browser launched inside the sandbox was inaccessible to the owner, who confirmed **“I cannot see the login window.”** No login had been submitted. Scoped process checks identified only its Node PID 11792 and direct Chrome child PID 6780. `Stop-Process` failed with a PowerShell NullReferenceException; exact identity-checked .NET process termination subsequently closed that obsolete helper/browser. No application service or unrelated browser was targeted. A replacement interactive helper was launched outside the sandbox with tool approval; the owner confirmed the window was visible and subsequently **“singned in.”** These were two browser launches but **only one credential submission**. No source extraction, automatic credential entry, second login or password reset.

Observed single-login results:

- `loginAttempts=1`, `loginSucceeded=true`, explicit clinic selection and rendered tenant meta match the fixed UID.
- Actual protected Draft Edit **200**; medication Edit **200** with exact patient association. Real Web/API reads, no mocked transports. Both forms provided nonempty fresh RowVersion values. Initial compliance/paper/interval controls were blank as expected.
- Read-only current `AccessProfile_GetEffective` results show active membership and **Patients.View, ClinicalData.Manage, Prescriptions.Prescribe** for Auth subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`.
- Existing clinical account binding is active user **2**, `admin@microemr.local`, original-prescriber provider UID `4432ffba-69f1-4d68-9969-202022051ab0`. New session read-audit entries all attribute to **UserId 2**, corroborating the trusted runtime actor. Pre-write snapshot again confirmed unchanged clinical states and current permission assignment before any command was published.

Credentials were entered only by the owner in the normal login form. No credential, cookie or authentication token was printed or saved in evidence. The helper checks only the submitted username and one-attempt counter, without retaining form data. Browser context stayed in memory and closed on the failure below.

## 3. Exact failure and executed mutations

First command publication failed before any clinical action. The publisher assigned `Get-Content -Raw` output directly to the `code` member before `ConvertTo-Json`. Windows PowerShell serialized its extended string properties into an object:

```json
{"id":1,"code":{"value":"const results=[]; ...","PSPath":"...","ReadCount":1}}
```

The repaired reader correctly requires `code` to be a string and raised:

```text
Error: Invalid helper command envelope
```

Inspection confirms parsed `code` type `PSCustomObject`, with `value`, `PSPath`, `PSParentPath`, `PSChildName`, `PSDrive`, `PSProvider`, `ReadCount`. The helper's outer error handler then closed the signed-in context. The unpublished clinical commands were never executed. The first queued command itself contained only DOM/keyboard inspections, not writes. No recurrence of EBUSY was observed; this is a separate command-publishing error, not an application defect.

**Executed clinical mutations: NONE.** Browser `clinicalPosts=[]`. No assessment, revision, Draft save, invalid-input submission, finalize, cancel, correction creation, deletion or cleanup. No second login or automatic mutation retry after failure. No product fix attempted.

## 4. Automated browser and visual results

**0 completed clinical smoke cases; 1 command-dispatch infrastructure failure.** Prerequisite assertions passed as listed above; no test-runner suite PASS count is inferred from those assertions. The inaccessible first browser was abandoned before login. The authenticated replacement failed on its first command dispatch.

| Planned acceptance | Result |
|---|---|
| Compliant assessment/save/reload | NOT EXECUTED |
| Unknown revision/NULL assessment instant/history display | NOT EXECUTED |
| Draft paper Yes + 7/save/reload; clear/save/reload | NOT EXECUTED; 0 of maximum 3 Draft saves |
| Optional omission-preservation save | NOT EXECUTED |
| Interval 0, -1, 1.5 validation; No paper-option exclusion | NOT EXECUTED |
| Desktop/mobile labels, keyboard, visibility, overflow | NOT EXECUTED; first inspection command was rejected |
| Finalized read-only browser presentation | NOT EXECUTED |
| Screenshots and visual inspection | 0 screenshots; no visual or screenshot-regression PASS |
| Restricted-account denial | DEFERRED; no approved restricted session |

Only limited initial desktop Edit form loading and control values were observed; no mobile or broader accessibility conclusion. The localhost-only guard blocked external Bootstrap icon CSS, as in earlier evidence; no denial-test claim from that block. No screenshots, traces or session-state export.

## 5. Read-only SQL/audit/RowVersion results

The post-failure snapshot at **2026-10-23T16:48:59.3551916+00:00** confirms the same development identity and available backup. Exact before/after comparisons of all selected columns show **patient, plan, correction Draft, finalized prescription, finalized artifact and compliance history unchanged**. These include RowVersion values and all selected unrelated plan/order fields. Assessment remains NULL, revision count 0, paper and interval NULL; correction relationship preserved. No database-wide unrelated-record scan was performed.

Scoped audit count **79 → 82**. The only new entries are the expected session read effects on the approved Draft: **PrescriptionPrivateNoteViewed** twice and **PrescriptionPrivateNoteHistoryViewed** once, all **UserId 2**. No clinical mutation audit exists from this attempt because no mutation occurred. Audit history preserved.

Fresh RowVersion values came through normal application Edit reads before the rejected inspection command. Persisted tokens remained unchanged; no save progression, stale-write or concurrent-write scenario was executed. Reuse prior Step 143/144 concurrency evidence without claiming a new live concurrency PASS.

Evidence: `.tmp/step152/browser-results.json`, `initial-checks.json`, `before.json`, `preservation.json`; `.tmp/step150/step152-before.json`, `step152-session.json`, `step152-after.json`. Failed command retained as `.tmp/step152/command-8483bff8-ec70-492d-8f7f-a1e7deac3854.json`. Existing helper/evidence copies and prepared command text remain local/ignored. No SQL clinical writes or production access.

## 6. Disposition and proposed Step 153

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** All prior passing evidence/closures retained. The same three subjective owner checks remain pending: **Prescription wording/layout, Medication Plan wording/layout, Allergy wording/layout**.

Proposed Step 153: repair only the local command publisher by reading a plain string (for example `[IO.File]::ReadAllText`) and validating the exact serialized envelope through the real shared reader **before any login**. Test the PowerShell-to-Node handoff, rather than only constructing commands in Node. Consider retaining the browser while reporting an invalid envelope so a publisher error cannot consume another session. No product change is indicated. A further single normal interactive login requires renewed approval because this attempt consumed Step 152's allowance; do not launch another authenticated session automatically. Resume only the still-unperformed exact approved smoke after necessary current-session safety checks and authorization.

Reviewed the local helper adaptation and report for scope; syntax check passed, read-only preservation checks passed. Reports 217/218 and product files remain unchanged. Stop after documentation.
