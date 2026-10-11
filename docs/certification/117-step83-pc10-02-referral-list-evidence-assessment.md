# Step 83: PC10.02 Referral List Evidence Assessment

Date: 2026-10-11. Evidence-first assessment and manual verification preparation only. **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Inventory remains **12 mandatory IDs / three original packages**. No automated or browser checks were executed.

## Git checkpoint

Current branch: **main**, HEAD `4c066812fa59a341f906916406994e44096a252c`, `fix referral preview errors`. Starting tree contains only the untracked [Step 82 report](116-step82-pc10-01-evidence-closure-next-gap.md); it is preserved unchanged. No staged/unstaged application changes are present. Evidence-only work stays on the current branch under the [established policy](95-step60-pc10-01-alternative-contact-evidence-closure-next-gap.md). Suggested `feature/step-83-pc10-02-referral-list-evidence-closure` is not created or switched to.

## Exact mapped scope

Reuse OntarioMD Primary Care Baseline Version 1.7 Final, PC10.02 pp.35–36, as recorded in [Step 45 PC10 mapping](77-step45-baseline-1.7-reconciliation.md#i-pc10-reconciliation) and implemented in [Step 46](78-step46-pc10-02-referral-list-reminders.md). This is the established interpretation, not a new verbatim specification extraction:

- Referral list exposes letter date, referring clinician, referred clinician, status and letter notes, with access to the selected letter. Specialty is optional.
- Distinct patient-chart reminders identify both clinicians for outstanding referrals. Automatic outstanding logic is vendor-defined; the user can turn the reminder off.

Existing status labels need not match guideline examples exactly. No fixed overdue interval, interactive sorting/filtering, separate history timeline, automatic closure, external response transport or immutable response-document snapshot is established as mandatory by this mapping. Patient association, permissions, tenant isolation and audit remain supporting integrity controls rather than invented new PC10.02 display fields. Preserve the Baseline 5.5 folder / Version 1.7 / tracked-release qualification and external acceptance boundary.

## Existing implementation and bounded inspection

Relevant paths inspected: Web `ClientApp/patients/patient-referrals.ts`; Application `PatientReferralService` list/detail projections; API `PatientReferralsController` permissions; Infrastructure `PatientReferralRepository` scoped reads and migration 0058's list/follow-up/response procedures; relevant list/Web/follow-up test cases. No unrelated domains were inspected.

| Mapped capability | Existing behavior |
| --- | --- |
| Patient referral list | Chart PatientUID drives the list request; tenant connection and patient-scoped procedure return rows ordered by creation date and ID descending. Loading, empty and error states exist. |
| Required list fields | Letter Date, Referring Clinician, Referred Clinician, Letter Notes (`ClinicalSummary`), Status and Actions; Reason is separate. Notes retain line breaks and are escaped. Missing historical clinician/notes data is labelled explicitly. |
| Stable letter date | SentAt is displayed after sending, including ResponseReceived/Closed. Drafts show a labelled creation date. Later lifecycle dates do not replace the original letter date. |
| Record/letter access | Details uses the patient/referral combination. Rows with an artifact provide View Referral Letter through stored-artifact retrieval; Drafts retain Details/preview. List rendering does not eagerly fetch or regenerate PDFs. |
| Outstanding indicator | Application overdue flag means Sent with a past follow-up due date. The list's distinct reminder shows referring and referred clinician names and directs users to that referral's Details. |
| User suppression | Details exposes existing Set/Clear due controls. Clearing suppresses the reminder without changing Sent status or the stored letter. Future/no due dates and Draft/ResponseReceived/Closed do not produce an overdue reminder. |
| Response/lifecycle integrity | Existing Draft → Sent → ResponseReceived → Closed actions retain separate timestamps and artifact identity. Follow-up/response/closure mutations use patient ownership, expected RowVersion, resolved actor and stored-procedure audit events. This is not a new visual timeline capability. |

Authenticated ReferralsView and mutation ReferralsManage checks, tenant routing, patient/referral ownership and trusted actors remain intact. Read/audit policy is reused; the existence of a letter link does not create an extra read event. Preserve the earlier limitation concerning a separate artifact-download audit event.

## Evidence available and what it does not prove

[Step 46](78-step46-pc10-02-referral-list-reminders.md) records **five passing frontend list checks**, successful affected TypeScript compilation, and **33 passed / one failed / zero skipped** in its filtered .NET run. The failure was the pre-existing migration-manifest length assertion, reproduced separately; that historical gate was not wholly green. Passing cases cover list projection/deserialization, patient-scoped routes, notes/identities/dates, original letter date, reminder visibility, explicit legacy values, escaping and existing follow-up/artifact behavior. No current rerun or repair of that historical assertion is claimed.

[Step 81 follow-up](115-step81-pc10-01-referral-letter-evidence-assessment.md#6-subsequent-user-reported-preview-failure-and-targeted-fix) records **70 focused .NET passes and 13 frontend passes**, with TypeScript/affected builds passed. These are overlapping referral evidence, not a newly executed PC10.02-only suite; the 13 frontend checks are selection/preview checks, distinct from Step 46's five list checks.

[Step 82](116-step82-pc10-01-evidence-closure-next-gap.md) provides eight itemized PC10.01 manual outcomes: accurate content, readable output, retrievable/preserved artifacts, restart persistence, immutability and correct status/actor/audit. Those already verified letter behaviors can be reused without reprinting or rehashing them. They do **not** independently verify list fields, the list-to-letter route, overdue display or discretionary suppression. No itemized PC10.02 manual acceptance is supplied here. Earlier general referral acceptance remains general; the live security group remains deferred.

## Focused manual checklist — planned, not executed

Use one test patient's existing Draft, Sent, ResponseReceived and Closed examples where available. Record the deployed version and actual outcome of each applicable row. Do not repeat completed PC10.01 output verification.

| Check | Evidence classification | Smallest needed observation / scope |
| --- | --- | --- |
| 1. List loads | Needs manual verification | Open Referrals; verify readable layout, rows and loading/empty behavior on the supported viewport. |
| 2. Patient association | Needs manual verification | Compare listed referral identities with this test patient's records; another patient's chart shows its own rows. Forged-ID denial remains the separate security check below. |
| 3. Required fields and dates | Needs manual verification | Compare referrer, recipient, status and letter notes to saved details. Confirm original sent date after receipt/closure and labelled Draft date; check explicit legacy missing values if available. |
| 4. Details and letter access | Needs manual verification | Open the intended row's Details and View Referral Letter. Verify that the route opens that referral's existing artifact. Its bytes/print/immutability are already verified under PC10.01; do not repeat those tests. |
| 5. Outstanding and suppression | Needs manual verification | On Sent, set a past due date; reload and confirm a distinct reminder with both clinicians. Clear due; reload and confirm suppression with status/artifact unchanged. Verify future/no due and Draft/received/closed examples do not show it. |
| 6. Response/lifecycle persistence | Needs manual verification | On a synthetic Sent fixture, record response receipt and closure; reopen and compare status, response/closure dates and retained original letter date/artifact reference. Existing PC10.01 status/audit acceptance is supporting evidence, not this itemized sequence. |
| 7. Interactive sorting/filtering | Not applicable to PC10.02 | Not required by the available mapping. Existing creation-date ordering does not establish an interactive control or a missing requirement. |
| 8. Security boundaries | Needs manual verification — prior runtime group deferred | With authorized fixtures, test restricted viewing/managing, foreign patient/referral combinations and second-tenant identifiers. Record unexecuted cases as DEFERRED; infer no PASS from ordinary chart browsing or automation. |
| 9. Follow-up/response audit | Needs manual verification | Correlate resolved actor/patient/referral and actual schedule/clear/response/close events from this session; retain read-audit policy qualifications. Do not invent an artifact-download event. |
| 10. Original artifact content/preservation | Already verified with evidence | Reuse Step 82's PC10.01 results. Only list access to the selected artifact remains check 4. |
| 11. Optional specialty/separate timeline | Not applicable to mandatory PC10.02 closure | Specialty is optional; a new history screen, automatic response transport or response snapshot is not mandated by the recorded clause. |

Checks 1–6 exercise the mapped functionality and supporting consistency; checks 8–9 qualify deployed integrity controls. Check 6 does not demand a new visible audit-history screen. No planned check is represented as a manual PASS.

## Defects, status and next action

**No new actual defect or missing mapped mandatory capability is established.** Honest legacy missing-data labels and the historical test-gate limitation remain qualifications. Live list usability, persisted fields/dates, exact row access, identified outstanding reminders and discretionary suppression still lack itemized PC10.02 acceptance.

Retain **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION**. Recommended next action: capture this bounded checklist's outcomes, then assess qualified closure; scope a targeted fix only if a concrete failure is demonstrated. No implementation or new verification starts here.

Inventory stays **12 IDs / three packages**: medication/prescribing seven, CPP four, scheduling PC09.03 one. PC10.02 remains separately tracked outside that established functional-gap count. PC10.01 and PC08.02 remain SATISFIED — VERIFIED with their qualifications; earlier PC08/PC09 statuses/security deferrals and dictionary/supplier/billing dependencies are unchanged. No count is reduced.

Only `117-step83-pc10-02-referral-list-evidence-assessment.md` is created. Documentation checks cover links, requirement/checklist boundaries, evidence provenance, inventory arithmetic and final Git/whitespace scope. Step 82 and all existing files remain unchanged. No branch creation/switch, application changes, tests/builds, migration/database/browser operations, standards retrieval, staging, commit, merge, reset or discard. Stop after this report.
