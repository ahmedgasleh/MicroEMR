# Step 127R-C - One Draft prescription fixture

## Current result and checkpoint

**Step 127R-C: PASS for the single owner-created Draft and scoped metadata/audit verification.** The owner confirmed the initial empty list, one submission, success banner and exactly one Draft afterward. A bounded read-only query verified the generated record and creation audit. No second prescription, private note or finalized artifact was created by this step.

Date: 2026-10-21. Verified branch `main`, HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Filename availability checked. Existing AGENTS.md, four tracked telemetry source/test modifications, reports 168-185 and correlation diagnostics source/tests are preserved. Entry and final tracked-diff hash `3234c02843caa14f1ab399e18f9121b19b8314ef`. This report and ignored `.tmp/step127rc/` verification script/evidence are the task additions. No source/configuration changes, migrations, builds/tests, service restarts, commits or remote operations.

## Approved scope and reused evidence

The owner authorizes at most **one Draft prescription**, under Actor A, tenant ApplicationUser 2, for the exact patient selected in Step 127Q, through the supported application workflow and its normal audit effects. No private-note creation/editing, signing/finalization, pharmacy transmission, outward document generation, additional patients/fixtures, administrative changes or V1-V3 execution. All development records are test data under AGENTS.md; no provenance investigation.

Approved SQL Server `192.168.50.1,50013`, Platform `MicroEMR_Platform`, tenant `MicroEMR_LocalDev_Fresh` / `F94C6ECD-4417-483E-8A94-0507CF8384C2`; Web `https://localhost:7002`, API `https://localhost:7003`. Normal API SQL identity expected `microemr_dev`, unchanged. Post-creation metadata used the existing separate DatabaseTool reviewer binding to that tenant only, not an API identity substitution. No other tenant/production access or clinical endpoint request by Codex.

[R-A](184-step127ra-pc07-01-private-note-identity-login-readiness.md) establishes A's owner-confirmed normal login, clinical ID 2, tenant and Active membership, with dated active-provider and Patients.View/Prescriptions.Prescribe metadata. [R-B](185-step127rb-pc07-01-private-note-permission-assignment.md) records A/B private read granted through the supported UI, C denied and only those two rows changed. These findings are reused; current displayed actor/tenant/patient must still be checked immediately before creation because browser tabs share sessions. Codex cannot control the owner's authenticated browser; the owner must submit the form.

## Patient reference and duplicate precheck

[Step 127Q](183-step127q-pc07-01-private-note-fictional-fixture-readiness.md) references the saved selection but omits the actual patient identifier. The existing referenced file `.tmp/step127j/selection.json` contains exactly one PatientUid **9f65d4ca-c347-406a-bb32-c709a8941f80**, with the approved TenantUid above. No names or clinical contents were retrieved. The owner replied **Same patient** to the explicit saved-identifier correspondence question. This resolves the missing report reference without selecting another patient. No fictional-provenance investigation was repeated.

Step 127P previously established that selected patient as existing/nondeleted with zero prescriptions. For a fresh check, the owner was instructed to use Actor A and the confirmed patient, Medications -> Prescriptions, without protected Details/history, and subsequently reported **there no prescriptions**. Record **PASS, user-reported empty current list**; this is not independent SQL verification. No existing acceptance Draft is reported. Chart/list navigation may generate normal chart-read audits; exact resulting audit events were not independently inspected. If a suitable Draft appears or current state changes before submission, STOP rather than create a duplicate.

## Existing synthetic field definition

Source: [PrescriptionDatePrecisionTests.Draft](../../tests/MicroEMR.Api.Tests/PrescriptionDatePrecisionTests.cs), lines 78-85, the reusable public-field fixture called by the private-note tests. Use its public values without calling the private-note helper (which adds protected text). No new medication/dose values are invented.

| Form field | Repository fixture value |
|---|---|
| Product/Brand Name | `TEST ONLY date fixture` |
| Product display | `TEST ONLY date fixture tablet` |
| Route | `Oral` |
| Frequency | `Once daily` (`ONCE_DAILY`) |
| Directions | `TEST ONLY instructions` |
| Initial quantity / unit | `1` / `tablet` |
| Number of authorized refills | `0` |
| Written date | Year and month precision: year `2020`, month `2`, no day |
| Start date | Year and month precision: year `2021`, month `3`, no day |
| Private Note | Leave blank; no sentinel or protected content |
| Other optional fields | Leave blank; PRN unchecked |

[PrescriptionDraftRequest](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs) requires product/display, route, approved frequency, directions, positive quantity, unit and valid written date; optional dose/strength need not be fabricated. [Create view](../../src/MicroEMR.Web/Views/PatientPrescriptions/Create.cshtml) states saving creates a Draft only; finalization is separate. [_Form](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml) exposes these exact fields and an optional private textarea. The blank field can be model-bound as explicit null: [0085 procedure](../../db/tenant-clinical/migrations/0085-private-prescription-notes.sql) normalizes blank to null and does not create a protected note/revision when both existing note and supplied text are absent. That is static behavior evidence, not live post-creation verification.

## Conditional creation instructions - NOT EXECUTED

After patient clarification and a fresh duplicate check pass:

1. Verify the displayed account is A/clinical ID 2, approved development clinic and exact selected patient. If not, stop; use normal owner login as necessary without collecting session material.
2. From that patient's Medications -> Prescriptions, select New Prescription. Use the normal standalone New Prescription Draft form if needed to match the inspected field labels. Fill only the synthetic values above. Leave Private Note and unnecessary optional fields blank.
3. Confirm the action is **Save Draft**, not Sign/finalize, View/Print or pharmacy transmission. Submit **once**. If validation, timeout or an ambiguous result occurs, stop and inspect current list/persistence through a bounded read before deciding anything else; do not retry blindly or create a second prescription.
4. Inspect the resulting ordinary prescription list: one intended new row, Draft status, correct patient/author and no finalized artifact. Do not open protected Details/history or test the note/editor. Obtain the generated prescription ID and metadata-only RowVersion/owner/state evidence through an approved bounded read if the list does not expose them. No fabricated ID or inferred RowVersion PASS.

## Execution and verification matrix

| Check | Evidence/status |
|---|---|
| Current actor/tenant/patient | Exact saved patient confirmed by owner; metadata verifies approved tenant, nondeleted patient, active actor 2 with active linked Provider and created/owned prescription attributed to 2 |
| Duplicate precheck | PASS, user reports current prescription list empty; no independent SQL count |
| Owner submission | Owner reports I saved, followed by Prescription draft created. Record user-reported successful submission; do not submit again |
| Created prescription ID | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8` |
| Exactly one new record / original prescriber 2 / Draft | PASS: owner confirms exactly one Draft; bounded current-patient TOP(2) query returns exactly one prescription, Draft, PrescriberUserId=2 and CreatedBy=2 |
| Valid eight-byte RowVersion | PASS for existing eight-byte persisted version; no editing/concurrency exercise performed |
| No finalization timestamp/artifact/transmission | PASS: FinalizedAtUtc, FinalizedBy and ArtifactUid NULL. Supported Draft-create path does not transmit; no signing/print/transmission action instructed or reported. No independent external-system trace claimed |
| No protected note/revision | PASS: counts 0 note records and 0 associated revisions; no protected text selected |
| Clinical audit | PASS: exactly one matching PrescriptionDraftCreated, AuditLogId 737, UserId 2, selected patient and generated prescription match |
| Unintended records | Selected patient has exactly one prescription, no protected child rows or artifact; no broad unrelated clinical-table comparison or whole-database no-write assertion |

Metadata captured **2026-10-21 21:46:00.630 UTC**. Prescription CreatedAtUtc **2026-10-21 21:43:10.000 UTC**; matching audit CreatedAt **21:43:10.276 UTC** (17:43:10.276 America/Toronto). Audit: ID **737**, ActionName `PrescriptionDraftCreated`, EntityName `PatientPrescription`, EntityId matching the generated prescription, UserId **2**, selected PatientId **6**, SourceApplication `MicroEMR.Api`, RequestCorrelationId **0HNPFI0SON5LL:00000001**. AuditEventUid is NULL; no synthetic event UID is invented. Tenant association is verified through TenantDatabaseIdentity and selected patient/record joins, rather than assuming AuditLog contains a populated TenantUid.

Exact SELECT statements and bounded parameters are retained in ignored `.tmp/step127rc/verify-draft.ps1`; raw metadata evidence in `.tmp/step127rc/verification.json`. The script asserts the saved patient/tenant, expected server/database/reviewer binding, current TenantDatabaseIdentity and one nondeleted patient before interpreting results. It reads only selected actor/provider state, at most two selected-patient prescriptions, note/revision counts, and at most five matching creation audits. No note text, general audit OldValue/NewValue, other patients or other databases are read. All eight recorded verification checks are true. Initial local script execution was restricted and sandboxed SQL connection failed; the same read-only script succeeded after approved sandbox escalation, using the existing reviewer identity without changing credentials, grants or connection settings. No SQL writes or audited clinical read procedures were invoked.

## STOP, retained status and next action

The missing patient-reference STOP is resolved by the owner's **Same patient** response; fresh duplicate check is satisfied by the owner-reported empty list. The owner had raised API availability: Codex performed read-only process/listener checks and found MicroEMR.Api PID 24020 and TCP 127.0.0.1:7003 LISTENING. No restart, health/clinical request or configuration change was performed; listening is not a complete health assertion. The owner then reported the empty list. The next interactive instruction is the already approved single Draft submission with repository fixture fields. Further STOPs are wrong actor/tenant/patient, existing suitable Draft, missing functional authorization/provider, unsupported synthetic values, signing/transmission requirement, source/admin changes, ambiguous persistence or need for another prescription. Preserve existing records and audit history; no physical cleanup.

Owner continuation: after the one-save instructions, the owner reported **I saved**, then **Prescription draft created.**, and **yes** to exactly one ordinary-list row with status Draft. Codex then performed only the bounded metadata verification above, confirming the author/state/version/private-record absence and audit independently of the banner. No second submission, editing, protected Details/history, signing or output action is requested. R-C is complete within this scope. Stop here and retain the Draft and audit history.

Recommended Step 127R-D: separately authorize the precise private-note acceptance operations against this same verified Draft, preserving author, version and audit rules. R-C intentionally creates no private note and does not establish the original V1 new-create-with-note criterion; future acceptance must explicitly address that distinction rather than count fixture creation as V1 PASS or silently authorize another prescription. No additional fixture is justified merely to repeat existing coverage.

Prior O PASS, P/Q assessment/planning PASS, R-A PASS, R-B owner-executed permission PASS (audit qualification) are preserved. R-C PASS for one owner-created Draft plus scoped metadata/audit verification. Private-note V1-V3 BLOCKED pending separate authorization/execution; PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages. Checks performed: branch/HEAD/status, relevant prior evidence, saved selection keys/tenant/patient reference, targeted public fixture/form/contract/blank-note behavior, owner UI confirmations, bounded read-only metadata/audit verification, path/link/whitespace checks and tracked-diff preservation. No builds/tests; clinical submission was owner-performed through the supported UI.
