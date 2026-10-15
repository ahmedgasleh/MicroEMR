# Step 84: PC10.02 Evidence Closure and Next Mandatory Gap

Date: 2026-10-11. Documentation-only assessment, continuing [Step 83](117-step83-pc10-02-referral-list-evidence-assessment.md).

**PC10.02 — SATISFIED — VERIFIED, with documented qualifications (outcome A).** The existing mapped implementation, reused automated evidence and eight user-confirmed manual PASS results support internal functional closure. Audit verification remains DEFERRED. This is not OntarioMD acceptance. Remaining inventory: **12 mandatory functional-gap IDs / three original packages**; PC10.02 was tracked separately from that count.

## 1. Git checkpoint

Branch: **main**. HEAD: `a1645aeaad7f9f5dd889590c57b99bd45ba5c21e`, `pc10-02 referral list evidence assessment`. Starting working tree is **clean**. Steps 82 and 83 are now committed, superseding their previously reported untracked state. The preceding relevant commit is `4c06681`, `fix referral preview errors`.

Evidence-only work stays on main under the [established policy](95-step60-pc10-01-alternative-contact-evidence-closure-next-gap.md). No branch switch, staging, commit or merge is performed. Only this report is created; existing files and user changes are preserved.

## 2. Requirement scope and existing functionality

Reuse the Version 1.7 Final PC10.02 pp.35–36 interpretation in [Step 45](77-step45-baseline-1.7-reconciliation.md#i-pc10-reconciliation), [Step 46](78-step46-pc10-02-referral-list-reminders.md) and Step 83. Required content is letter date, referring clinician, referred clinician, status and letter notes, with access to the selected letter. Distinct patient-chart outstanding reminders identify both clinicians; automatic outstanding logic is vendor-defined and users can turn reminders off. Specialty is optional; interactive sorting/filtering is not an established mandatory requirement.

Step 83 records patient/tenant-scoped lists, preserved original sent dates, stored-letter access, Sent/past-due reminders, due-date clearing/suppression and persisted response/closure information. Existing permissions, patient ownership, resolved actors, RowVersion checks and audit controls remain intact. No new implementation inspection or development is performed here.

## 3. Reused evidence and historical gate

- **Step 46 automated evidence:** five frontend list checks passed; affected TypeScript compilation passed; filtered .NET run **33 passed / one failed / zero skipped**. The pre-existing migration-manifest length assertion failed and was reproduced separately. This historical automated gate was not wholly green; no current repair or rerun is claimed. Its passing cases support list projection, dates/identities/notes, patient-scoped routes, reminders and existing follow-up/artifact behavior.
- **[Step 81 follow-up](115-step81-pc10-01-referral-letter-evidence-assessment.md#6-subsequent-user-reported-preview-failure-and-targeted-fix):** 70 focused .NET passes and 13 frontend passes, with affected TypeScript/build checks passed. These overlap referral evidence; the 13 selection/preview checks are distinct from Step 46's five list checks. Counts are not added into a new PC10.02 suite.
- **[Step 82](116-step82-pc10-01-evidence-closure-next-gap.md):** reuse accepted PC10.01 content/output, artifact preservation, immutability and lifecycle/actor/audit evidence. Earlier general referral acceptance and PC10.01 results do not independently establish every PC10.02 list scenario.

No automated tests, builds or browser checks were executed during Step 84.

## 4. Exact new manual results

Supplied by the user, not executed by Codex: **eight PASS and one DEFERRED**. No individual subscenario, fixture, deployed-version or event-level outcome is inferred beyond these confirmations.

| # | Result | User-confirmed scenario |
| --- | --- | --- |
| 1 | PASS | Required referral-list fields and dates display correctly. |
| 2 | PASS | Referral records are associated with the correct patient. |
| 3 | PASS | Selected referral letters open correctly from the list. |
| 4 | PASS | Outstanding referral reminders are correctly identified. |
| 5 | PASS | Reminder clearing behaves correctly. |
| 6 | PASS | User-controlled reminder suppression works correctly. |
| 7 | PASS | Referral lifecycle information persists correctly. |
| 8 | PASS | Unauthorized, cross-patient and cross-tenant access restrictions passed manual verification. |
| 9 | DEFERRED | Relevant referral-list audit behavior. |

## 5. Closure decision and qualifications

Outcome A is supported because the recorded mandatory list/access/reminder capabilities now have explicit manual acceptance, with supporting persistence and access-restriction acceptance. The qualified-closure convention in Step 82 permits explicit supporting integrity deferrals where mapped mandatory functionality is established. PC10.02's available mapping does not establish an additional mandatory audit-display field. The deferred audit observation and unrelated historical manifest assertion therefore qualify this internal closure rather than demonstrate a missing mapped capability.

Audit verification remains outstanding: correlate actual referral-list follow-up/clear/response/close events with their resolved actor, patient and referral when authorized verification resumes. Preserve the existing read-audit policy and separate artifact-download audit limitation; neither implementation nor earlier PC10.01 audit acceptance converts item 9 into PASS.

The new security PASS applies to the confirmed PC10.02 scenarios only. It does not backfill deferred PC10.01 runtime security or PC08.02 unauthorized/cross-patient/cross-tenant/forged-identity checks. PC08.02, PC08.04, PC08.06, PC09.06, PC09.12 and PC10.01 statuses and qualifications remain unchanged. Preserve the Baseline 5.5 folder / Version 1.7 / tracked-release qualification. No new actual defect or missing mapped PC10.02 capability is established; no speculative fix is proposed.

## 6. Inventory reconciliation

Steps 82–83 document these **12 functional-gap IDs / three original packages**, with PC10.02 tracked separately for verification:

| Package | Recorded unresolved IDs | Count |
| --- | --- | --- |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | 7 |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 | 4 |
| Scheduling | PC09.03 | 1 |

Arithmetic is 7 + 4 + 1 = 12. PC10.02 is not a member of that recorded count, so its closure alone cannot yield 11 without removing another unresolved ID. The user withdrew the earlier response offering a corrected inventory. Reconciliation therefore uses the documented ID list: **12 mandatory functional-gap IDs / three original packages remain**. The requested 12 → 11 decrement is unsupported by that list; PC10.02's separately tracked verification status changes to qualified closure. No unrelated requirement is removed or reclassified, and prior reports are not rewritten.

## 7. Proposed next mandatory requirement

Propose **PC04.16 — independent refill quantity/days-supply** from the remaining inventory. Existing mapped status is **MISSING**: initial quantity/unit/repeat count does not establish independent refill quantity/duration authoring. [Step 45](77-step45-baseline-1.7-reconciliation.md) requires Medication CDS-S mapping before design; the dictionary/supplier dependencies carried through Step 82 are unresolved. This is a functionality gap, not merely an unexecuted manual test.

Smallest next step: a bounded Step 85 scope/dependency assessment of the applicable Medication CDS-S definitions and existing refill capability before proposing any implementation. Preserve finalized prescription immutability; do not declare this target implementation-ready or expand to a prescribing redesign. Proposed branch, if appropriate for later authorized work: `feature/step-85-pc04-16-refill-scope-assessment`. It is not created. Evidence-only assessment stays on the current branch.

## 8. Documentation checks and next action

Documentation checks confirm exactly eight PASS/one DEFERRED rows, valid local report paths, no NUL characters and no whitespace errors. Evidence provenance and inventory arithmetic were reviewed; final Git scope contains only this untracked report. No application/database changes, migrations, browser operations, test/build reruns or automatic commits/merges.

Step 84 is complete with the inventory discrepancy explicitly reconciled. Recommended next action is the bounded Step 85 PC04.16 scope/dependency assessment described above; it has not started. Audit remains explicitly deferred after PC10.02 closure.
