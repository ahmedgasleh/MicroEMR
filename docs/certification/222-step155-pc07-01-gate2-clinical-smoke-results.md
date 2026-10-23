# Step 155 — PC07.01 Gate 2 controlled clinical smoke results

2026-10-23. **Bounded clinical save/reload smoke PASS: 7 real-browser command groups PASS, 0 FAIL; four approved clinical form submissions completed.** Read-only SQL confirms both compliance revisions, paper/interval persistence and clearing, audit attribution, RowVersion progression and finalized record/artifact preservation. Mobile compliance screenshot has the sidebar qualification below. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

## 1. Authorization, Git and execution boundaries

Read AGENTS.md, the owner’s Step 155 authorization, relevant record/action details of [217](217-step150-pc07-01-gate2-controlled-clinical-smoke.md) and verified transport disposition in [221](221-step154-browser-unicode-transport-verification.md). Owner explicitly authorized ONE normal login as `admin@microemr.local`, the exact synthetic record set and bounded assessment/Draft mutations below, real browser → Web/API → SQL execution and read-only audit verification.

Recorded once: branch **`feature/step-154-browser-unicode-result-repair`**, HEAD **`c92ec5c35b65e0f3ba8615203d70c79790d619c3`**. Reports 217–221 were untracked; preserved unchanged. No branch change, commit, merge, push, reset, clean, stash or remote operation. No product/helper changes, build, service restart, migration, backup operation, restore, fixture creation, permission change or broad/regression-suite execution.

Reused the Step 154 publisher/dispatcher/reader and existing browser helper and prepared smoke commands unchanged. Before/after SHA-256 comparisons of those four infrastructure files match. Existing Step 152 evidence was copied aside before refreshing its runtime snapshot input; this updates evidence, not helper code. Only this report is a new tracked deliverable; execution evidence remains ignored under `.tmp`.

## 2. Pre-write gate — PASS

Fresh targeted snapshot at **2026-10-23T19:23:27.9111542+00:00** confirmed:

| Property | Verified value |
|---|---|
| SQL endpoint / actual server | `192.168.50.1,50013` / `EC2AMAZ-TC26CQK\TELEMED52` |
| Database / tenant key | `MicroEMR_LocalDev_Fresh` / `local-dev-fresh` |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Patient | `9f65d4ca-c347-406a-bb32-c709a8941f80`; not deleted |
| Medication plan | `2521d27e-172f-47a1-9326-72eea8b0f571`; patient-owned, Active, assessment NULL, 0 revisions |
| Correction Draft | `ffd80110-e565-46de-b660-3d3f9dc7e463`; patient-owned, Draft, paper/interval NULL; original prescriber 2 |
| Linked finalized prescription | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`; same patient; strictly read-only |
| Original-prescriber provider | `4432ffba-69f1-4d68-9969-202022051ab0`; active |

The exact Step 149 backup from report 216 remains present (`file_exists=1`, not a directory). Prior checksum/VERIFYONLY PASS reused; no backup operation or verification rerun. Expected clinical states and complete selected rows matched the previous after-snapshot before execution. All development records are fictional under the standing clarification; no additional provenance investigation.

The existing interactive Chromium helper opened outside the sandbox. The owner submitted credentials through the normal Auth login form **once**. Login succeeded; explicit clinic selection and rendered tenant meta matched the fixed UID. Actual Draft/medication Edit pages returned **200** through the running Web/API. Normal Edit reads supplied fresh nonempty RowVersion tokens before saves.

Read-only current `AccessProfile_GetEffective` results confirm active membership with **Patients.View + ClinicalData.Manage + Prescriptions.Prescribe** for the bound Auth subject. A separate pre-write session snapshot confirmed unchanged record state and new read-audit entries attributed to **clinical user 2**, corroborating trusted runtime actor binding. Draft original-prescriber ownership matches user 2. No credential extraction, cookie/token export, impersonation or second login.

One publisher invocation initially omitted the explicit `Directory` argument and failed during PowerShell parameter-default binding (`Join-Path` received an empty `PSScriptRoot`). It published no command and submitted no form. Supplying the explicit directory, as used by the verified Step 154 harness, resolved the invocation without a helper change or login retry. All following invocations used that tested form. This is an invocation limitation, not an application/clinical failure.

## 3. Exact completed mutations

| Save | Record/action | Persisted result verified after reload and SQL |
|---|---|---|
| 1 | Approved medication plan | `Compliant`; `Step 150 synthetic assessment`; assessment instant `2026-10-23T12:00:00-04:00`; revision 1, assessor 2; server RecordedAtUtc `2026-10-23T19:25:20.5100374` |
| 2 | Same medication plan | `Unknown`; `Step 150 synthetic revision`; assessment instant NULL; revision 2, assessor 2; server RecordedAtUtc `2026-10-23T19:25:48.4922321` |
| 3 | Approved correction Draft | Paper **Yes**, minimum dispense interval **7** days |
| 4 | Same Draft | Paper **Not recorded**/NULL and interval NULL explicitly cleared |

**Totals: two medication saves, two Draft saves; four clinical POSTs.** Maximum three Draft saves respected. Optional omission-preservation save was not attempted: the normal form includes both fields, and no form/request manipulation was introduced. Finalization, cancellation, correction creation, deletion, cleanup and direct SQL clinical writes: **none**.

Current plan remains Unknown with both historical revisions intact. Current Draft paper/interval are NULL; prior Yes/7 values remain in audit history. Unrelated selected plan/Draft fields are unchanged except normal actor/update metadata and RowVersion. Correction linkage remains unchanged.

## 4. Actual Chromium results — 7 groups PASS / 0 FAIL

Used the existing real Chromium session against running localhost Web/Auth/API and the fixed SQL database. No fixture authentication, mocked API transport or intercepted success responses. Only the four authorized form posts were armed; the same authenticated session served all phases. The published CLOSE command was acknowledged, and the helper exited **0** after closing Chromium.

| Browser command group | Result |
|---|---|
| Compliance controls at 1440×900 and 390×900 | PASS — associated labels, visible controls, Tab from state to assessment time, no document horizontal overflow |
| Compliant assessment/save/reload | PASS — exact state/explanation/offset time and actor displayed |
| Unknown revision/save/reload/history | PASS — both revisions/explanations, supplied first instant and NULL revised instant displayed; actor/time retained |
| Draft controls and no-write validation, both widths | PASS — options exactly Not recorded/Yes; no No; labels, visibility and Tab to interval; 0 and -1 range-underflow, 1.5 step-mismatch; nonempty native validation messages; no form submit |
| Draft Yes/7 save/reload | PASS — fresh Edit and Detail reads show Yes and 7 whole days at both widths |
| Draft explicit clear/save/reload | PASS — fresh Edit blank fields and Detail Not recorded values at both widths; second Draft save performed at mobile width |
| Finalized read-only presentation, both widths | PASS — Finalized displayed; no paper editor or Edit draft link; read-only note presentation |

These are seven bounded script groups containing **14 retained detailed browser observations**, not a rerun of any prior suite. Login/security prerequisites and CLOSE acknowledgment are separate from the seven groups. All recorded horizontal document-overflow observations are false. The native browser messages include “Value must be greater than or equal to 1” and the fractional nearest-valid-value message; no new API-invalid-input rejection claim is made.

## 5. Screenshot review and accessibility qualification

Captured and inspected all six screenshots, preserved in `.tmp/step155`:

- `compliance-history-1440.png`, `compliance-history-390.png`.
- `draft-yes-seven-1440.png`, `draft-yes-seven-390.png`.
- `finalized-readonly-1440.png`, `finalized-readonly-390.png`.

Desktop history shows both revisions, actor and timestamps clearly. Prescription desktop/mobile captures show Yes/7 values and finalized read-only presentation without document overflow. **The mobile compliance screenshot retains an open sidebar after resizing the desktop page, narrowing the content area and clipping sidebar labels; history continues below the captured viewport.** This limits mobile visual acceptance despite passing control/label/focus/overflow assertions. It is recorded for focused no-write review; no responsive-shell fix or unrelated redesign is authorized or attempted. Do not relabel it as a complete mobile-layout PASS.

Checks establish the specified control labels, focus order and visibility only, not a full accessibility audit. The localhost-only guard blocked external Bootstrap icon CSS; this further qualifies icon-dependent presentation, not clinical persistence. No screenshot-regression PASS without an approved baseline. The same three owner wording/layout checks remain pending; automation is not owner acceptance.

## 6. SQL, audit and concurrency evidence

Inspected existing 0089/0090 definitions and reused the bounded read-only snapshot helper. Snapshots were taken before writes, after session binding, after each clinical phase and after closing the browser. Final snapshot **2026-10-23T19:27:46.5518090+00:00** confirms exact tenant/patient associations and available backup.

**Audit PASS:** scoped count **82 → 111**. All 29 new entries attribute to **UserId 2**:

| Action | Count / interpretation |
|---|---|
| Medication `Update` | 2; audit IDs **1028, 1032**, complete old/new assessment values |
| `PrescriptionDraftUpdated` | 2; audit IDs **1045, 1062**, old/new Yes/7 and explicit NULL transitions |
| `PrescriptionPrivateNoteViewed` | 16; governed reads of approved records |
| `PrescriptionPrivateNoteHistoryViewed` | 7; governed reads |
| `PrescriptionPrivateNoteCleared` | 2; existing full-form blank-note processing, **ContentChanged=false**, PrivateNoteUid and before/after revision UIDs NULL; no note content or revision created/changed |

The no-op private-note audit entries are disclosed separately from the four intended clinical value changes. No clinical history was removed. Compliance revisions preserve original/revised JSON, trusted assessor 2 and server-recorded timestamps; mutation audits independently corroborate both states. Unrelated selected plan and prescription columns remain unchanged. This is a bounded record comparison, not a database-wide claim about all concurrent activity.

| Phase | Plan RowVersion | Draft RowVersion | Finalized RowVersion |
|---|---|---|---|
| Before | `0x000000000000085A` | `0x000000000000271B` | `0x0000000000002718` |
| First assessment | `0x000000000000271E` | unchanged | unchanged |
| Revision | `0x0000000000002720` | unchanged | unchanged |
| Yes/7 | unchanged | `0x0000000000002721` | unchanged |
| Explicit clear / final | unchanged | `0x0000000000002722` | unchanged |

Fresh application-read tokens preceded saves and SQL confirms progression on changed records. RowVersion is database-generated; no consecutive-increment assumption. No intentional stale save or concurrency-conflict probe. Existing Step 143/144 concurrency evidence retained; this run proves normal progression, not a new conflict-rejection test.

**Finalized preservation PASS:** complete selected finalized row, its RowVersion and complete stored artifact match before/after exactly. No finalized mutation endpoint called. Read-only rendering confirmed at both widths.

Evidence: `.tmp/step155/summary.json`, `before.json`, `browser-results.json`, helper hash files and screenshots; `.tmp/step150/step155-{before,session,assessment,revision,draft-set,draft-clear,after}.json`. Unique command channel was `.tmp/step152/command-1c60162f-2e00-44ac-aa28-41e99029dc5a.json`; previous commands/results are retained by the verified publisher. No credentials, cookies or tokens saved or printed.

## 7. Deferred work, inventory and Step 156

- Restricted-account denial **DEFERRED**: no approved restricted session; no extra login, grants or account creation.
- Optional omitted-field save **NOT EXECUTED**; normal form always includes the fields. Prior omission evidence retained.
- Intentional stale-write/finalized-write rejection **NOT EXECUTED**; outside this safe mutation scope. Finalized read-only preservation verified.
- Mobile compliance shell/menu state **QUALIFIED** as above; no blanket visual acceptance or screenshot regression claim.

Only the same subjective owner checks remain **PENDING**: **Prescription wording/layout, Medication Plan wording/layout, Allergy wording/layout**. Preserve prior closures and source qualifications.

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Controlled functional save/reload acceptance now has live existing-tenant evidence; it does not independently close all certification/source/owner obligations.

Recommended **Step 156:** review this completed evidence and obtain only the three existing owner wording/layout decisions, including the mobile compliance sidebar qualification in the Medication Plan review. Any technical follow-up should be a separately scoped no-write responsive/menu-state check; do not repeat the successful four writes or broad suites. No further login or clinical mutation authorized by this report.

Reviewed report/evidence and unchanged helper hashes; no product/helper edits or unrelated tracked changes. Stop after acceptance and documentation.
