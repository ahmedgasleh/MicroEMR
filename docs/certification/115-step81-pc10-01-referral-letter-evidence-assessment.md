# Step 81: PC10.01 Referral-Letter Evidence Assessment

Date: 2026-10-11. Evidence assessment and verification planning only. **PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Inventory remains **13 mandatory IDs / four original packages**. No new manual PASS, automated execution or implementation is claimed.

## 1. Git checkpoint and post-Step-80 changes

Starting branch: **main**; working tree: **clean**. HEAD: `ba6136f824978af905ce86ea129a1b9fc07d7b2a`, `step 80 encouter note update`. This follows Step 80's recorded `1056e27` checkpoint and commits the [Step 80 report](114-step80-pc08-02-evidence-closure-next-gap.md) plus encounter view/contribution TypeScript and generated output fixes: hidden baseline inputs, removal of repeated field guidance/actions, initial narrative-field save controls and unsaved-text sign/preview guards. These are encounter changes, not new PC10.01 acceptance evidence.

Bounded referral-path history identifies `79729d6` (report content), `d083ae1` (referral letter) and `d3df4a0` (immutable final referral preservation); the latest commit changes no referral paths. No current staged/unstaged fixes need relocation. Stay on main for this evidence-only step under the permanent branch policy in [Step 60](95-step60-pc10-01-alternative-contact-evidence-closure-next-gap.md). The suggested `feature/step-81-pc10-01-referral-letter-evidence-closure` is not created; a separately approved code fix would need an implementation branch.

## 2. Existing functionality and requirement boundary

Reuse the Version 1.7 Final p.35 mapping from [Step 64](99-step64-pc10-01-complete-evidence-verification-next-gap.md): generate/edit/preserve/print referral letters with demographics, referrer letterhead, available recipient information, selected CPP/results/specialist consultations/external reports and automatic date. Encounter-note selection and page-number/print-date options are not promoted to mandatory criteria. Preserve the Baseline 5.5 folder / Version 1.7 / tracked-release qualification; no new standards retrieval.

Targeted inspection confirms the documented paths in Application `PatientReferralService` and `ReferralReportContentService`, Infrastructure `PatientReferralRepository`, API `PatientReferralsController`, Web `patient-referrals.ts` and migration 0056's referral procedures:

| Capability | Current implementation |
| --- | --- |
| Create/edit | Patient-chart referral editor saves recipient information, reason, summary and referring Provider UID. Draft text, clinical references and document links save sequentially with refreshed RowVersions; partial-save failures instruct reopening. |
| Patient/referrer | Composer loads the patient by referral PatientUID and the selected Provider record, then clinic letterhead. Header includes name, DOB/age, HCN/version, separately labelled gender/sex-at-birth and designated alternative contacts. Provider identity is distinct from the authenticated mutation actor and from the encounter's free-text ProviderName. SQL requires an active provider; an omitted provider may resolve from the creator's linked active provider. |
| Selected clinical content | Existing CPP Problems/Allergies/Medications and Current results are selectable. Linked structured/plain-text reports render full content; signed consultations use their authoritative final PDF. Uploaded PDF/UTF-8 reports use persisted FILE selections, with complete PDF pages in identified appendices. Unselected bodies are excluded. Broader CPP completeness is not reassessed here. |
| Preview/print | Saved Draft choices feed Preview Letter. Preview and Mark Sent share composition/assembly; final date is assigned at send in clinic timezone, separately from the Draft preview date. View Referral Letter returns a printable final PDF. Actual rendering/paper readability still requires manual evidence. |
| Final preservation | Mark Sent builds the complete PDF before the transactional send. Artifact bytes, SHA-256, snapshot, source provenance and referring-provider metadata are preserved. Reopening reads stored bytes, not current patient/source composition. |
| Lifecycle/integrity | Draft → Sent → ResponseReceived → Closed remains the recorded workflow. Draft edits/selection membership require Draft status and expected RowVersion; send couples artifact insertion, status change and ReferralSent audit. Response/follow-up handling does not replace the original letter. Final referral immutability is not a newly inferred digital-signature requirement. |

Tenant-bound connections, compound patient/referral reads, source ownership, ReferralsView/Manage and selected-content DocumentsView checks, trusted clinical actor resolution and existing audit are retained. Report reads record PatientDocumentViewed/Downloaded or PatientFileDownloaded as applicable. Preserve Step 47C's explicit artifact-download read-audit policy limitation: source-read auditing does not prove a separate referral-artifact download event.

## 3. Evidence reused and outstanding verification

[Step 64](99-step64-pc10-01-complete-evidence-verification-next-gap.md) supplies the complete element matrix; it is not duplicated here. Supporting reports are [47A](79-step47a-pc10-01-referral-letter-composition.md), [47B.2](81-step47b2-referral-clinical-selection-ui-preview.md), [47C](82-step47c-immutable-final-referral-preservation.md), [59](94-step59-pc10-01-patient-alternative-contact.md) and [63](98-step63-pc10-01-referral-report-content.md).

| Evidence | Previously recorded result / scope |
| --- | --- |
| Automated | 47A: final 14 focused cases; 47B.2: 31 .NET + seven frontend; 47C: 28 focused, including one SQL integration case; 59: 11 .NET + three frontend; 63: 54 backend + ten frontend. Recorded affected builds/TypeScript passed. Runs overlap and are not summed. |
| Coverage | Demographic/date composition, selected/excluded bodies, synthetic PDF page/image preservation, scoped source/permission/audit failures, concurrency/finalization rollback and stored-artifact preservation. Earlier renderer doubles and synthetic PDFs do not establish actual clinic pagination or deployed tenant behavior. |
| General manual acceptance | Step 64 records the user's confirmation that Step 63 was stable and its main functionality manually verified. It does not identify formats/pages, printed comparisons, source edits/archival, hashes, audit rows or security scenarios. |
| Verified sub-gap | Step 60 retains designated alternative-contact content as SATISFIED — VERIFIED, based on the user's observed snapshot inclusion. Its live persistence/security and extensive layout qualifications remain. Do not reopen or expand that acceptance. |
| Still outstanding | Explicit deployed selection persistence, complete readable final output, report-specific historical before/after preservation and live ownership/permission/tenant/actor/audit/concurrency evidence. No newer itemized PC10.01 confirmation is supplied in Step 81. |

Deployment prerequisites remain existing migrations **0056** (artifact), **0065** (selection), **0066** (contacts) and **0068** (FILE references). Step 63 did not execute 0068 against live SQL; neither general acceptance nor Step 80's unrelated 0072 confirmation establishes its deployment. No database is queried here.

## 4. Smallest practical acceptance checklist — NOT PERFORMED

Use one representative test-patient referral and one acceptance session, reusing any already itemized evidence rather than repeating it. Record deployed commit, tenant/migration provenance and actual outcomes:

1. **Save/reopen and identities:** clinician A creates a Draft; authorized clinician B reopens/edits it. Check correct patient, selected referring provider, clinic letterhead, available recipient details and retained choices. Select CPP/result content, structured/text consultation, signed consultation PDF and a multi-page scanned/text PDF or UTF-8 report; leave one distinguishable report unselected.
2. **Complete final output:** compare Preview, Mark Sent and downloaded/printed final PDF with the sources. Confirm required demographics, automatic final date, selected text and all appendix pages/count/order/orientation/readability; confirm excluded content is absent. Retain the original final download and SHA-256.
3. **History and final-state protection:** safely edit an editable source, archive an eligible uploaded source, change test demographics and choices on a later referral. Reopen the original letter; compare unchanged bytes/hash and original content. Confirm ordinary Draft-content changes are denied after Sent and the original artifact remains available through applicable response/closure transitions.
4. **Failure and recovery:** exercise one stale Draft save/send and one unavailable or unsupported selected source. Confirm actionable errors and no partial Sent transition/artifact; reopen or flatten/remove the unsuitable selection and retry with current RowVersion. Draft saves are sequential, so do not assume all editor save stages form one transaction.
5. **Live security/audit group:** under authorized fixtures, test another patient's/tenant's identifiers and a restricted role lacking source access. Check no disclosure/mutation and actual source-read/ReferralSent audit rows with the resolved actor. Record unavailable scenarios explicitly as DEFERRED, not PASS. Existing deferred checks remain qualified until individually evidenced.

## 5. Defects, status and next action

**No new actual defect or missing mapped capability is established by this bounded inspection.** Known supported-source limits remain: uploaded standalone images/other formats are unsupported; PDFs with forms/annotations/layers require flattening. Missing, archived, corrupt or inaccessible selected content blocks output rather than falling back to titles. Verify these limits against real clinic reports; if a required source cannot be safely represented, identify that concrete gap before proposing development.

Keep **PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING**. General main-function acceptance and automation do not supply the missing complete-print and report-history observations. No speculative fix, new schema or repeated test run is justified now. Recommended next action: collect the focused acceptance outcomes above, then assess qualified closure; scope only a demonstrated defect for separate approval.

Inventory stays **13 IDs / four packages**: referral PC10.01 (1), medication/prescribing (7), CPP (4), scheduling PC09.03 (1). PC08.02 remains SATISFIED — VERIFIED with Step 80's qualifications; PC10.02 remains IMPLEMENTED — NEEDS MANUAL VERIFICATION. All other historical statuses, dependencies and security qualifications are unchanged. No ID/package is removed for this assessment.

Only this report is added. Documentation checks cover source links, evidence provenance, inventory arithmetic, whitespace and final change scope. No application changes, migration/database/browser operation, tests/builds, branch creation/switch, staging, commit, merge, reset or discard. Stop after Step 81 documentation.

## 6. Subsequent user-reported preview failure and targeted fix

The user subsequently reported Preview Letter opening a JSON conflict and confirmed the referral was Draft and still failed after reopening. This new bug report authorizes targeted repair beyond the completed evidence-only assessment above. Created `feature/step-81-referral-preview-errors` from `ba6136f`, preserving this untracked report; the sandbox ref-write denial was followed by an authorized successful retry. No staging, commit or merge.

Two concrete code findings:

- The Web API client replaced every conflict with a generic stale/status message, hiding selected-source errors. It now preserves bounded JSON `message` values for expected 400/409 responses, retaining safe fallbacks for malformed/oversized or server-error responses. Preview fetches the PDF and displays failures inside Referral Details rather than navigating to JSON; successful output opens as a PDF. Blocked pop-ups and non-PDF session responses are actionable.
- Selected encounter composition validated only baseline structured JSON, excluding field-linked narrative introduced in Steps 79/80. A completed required Note could therefore be rejected. It now validates/renders the existing effective-field composition, retains contribution identities and includes additional narrative once. Plain encounters also include their new narrative contributions alongside preserved legacy text. Missing required content is still rejected; no stored baseline is rewritten.

The encounter compatibility defect is established by focused reproduction; the exact live API cause for this user's referral was not captured, so do not assert that every preview conflict was caused by it. Other source/deployment errors remain possible and will now be visible.

Changed paths: Application `ReferralClinicalContentService.cs`; Web `PatientReferralApiClient.cs`, `ClientApp/patients/patient-referrals.ts` and its generated JavaScript/map; `ReferralClinicalContentTests.cs`, `ReferralClinicalSelectionTransportTests.cs`, `tests/pc10-referral-clinical-selection.test.cjs`; this report. No migration or database operations; tenant, ownership, permissions, audit, RowVersion and immutable final-artifact rules remain intact.

Verification: **70 focused .NET tests passed / 0 failed / 0 skipped**, including content/composition/transport/Web/finalization checks; **13 frontend checks passed / 0 failed / 0 skipped**; full TypeScript type checking and targeted emission passed; diff/whitespace checks passed. The earlier 19 transport/Web passes overlap the final run and are not added. The first isolated-output attempt lacked its assets file and executed no tests; corrected output isolation reused existing restore assets. Affected project references compiled under ignored `artifacts/step81-preview/bin`; no full solution suite or live browser run.

Rebuild/restart API and Web, refresh the chart and retry Draft Preview Letter. Confirm actual PDF content, including any selected attributed encounter; if it still fails, record the now-specific message before further diagnosis. Live acceptance remains outstanding. **PC10.01 stays PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING; inventory stays 13 IDs / four packages.** This follow-up does not retrospectively close the pending manual checklist.
