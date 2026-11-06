# Demo prototype readiness — Phase 1 execution

2026-11-04 (America/Toronto). Focused isolated automation; Prompt 2 complete.

## A. Executive summary

- **Workflows: PASS 2 / PARTIAL 7 / FAIL 1 / BLOCKED 0 / NOT EXECUTED 0.**
- **166 distinct automated cases: 165 PASS / 1 FAIL / 0 skipped.**
- Browser cases: **4/4 PASS**; native PDF cases: **14/14 PASS**.
- Other service/API/SQL/contract cases: **147/148 PASS**, including real SQL round trips **25/26 PASS**.
- Confirmed product defect: an uploaded document saves/lists successfully but cannot be reopened through the service.
- Four earlier failures were test defects; narrowly corrected checks now pass.
- Vitals persistence, BMI, ownership, audit and stale-write checks now pass in a new isolated round trip.
- Broad medical-staff evaluation is **not yet ready unconditionally**; fix document retrieval and close the essential connected visit/front-desk gaps.
- No product fix, deployed-runtime acceptance, shared clinical writes, deployment or service restart occurred.

## B. Environment and isolation

Read [Prompt 1 inventory](demo-readiness-phase1-inventory.md); inspected Git status once. Existing modified Step04C report and untracked inventory were preserved. No repeat inventory or certification review.

SQL preflight connected **only** to current-user `(localdb)\MSSQLLocalDB`, integrated authentication, `master`; `SERVERPROPERTY('IsLocalDB') = 1`. LocalDB version is 17.0.4025.3. Sandbox registry access failed; one targeted outside-sandbox retry established the prerequisite and allowed isolated execution. The LocalDB connection auto-started the stopped user-local instance; no MicroEMR service was restarted.

All selected connection overrides were explicitly set to that local master connection: scheduling search, family history, prescription refill and referral finalization. The runner removed live status-acceptance write consent. No production/shared-development secret, administrative account, browser cookie or Postman token was used.

Existing fixtures create their own GUID-named stores (`MicroEMR_Step71_*`, `MicroEMR_Step97_*`, `MicroEMR_Step87_*`, `MicroEMR_ReferralFinalizationTest_*`), seed synthetic patients/actors and exercise production procedures. Family-history/prescription hosts use generated tenant identities; tenant connection factories validate the generated assignments/database identity. Step144 additionally checks actual database name and `IsLocalDB=1`. The new fixture rejects non-local/non-integrated/master overrides before setup and verifies LocalDB through its tenant-scoped connection. These are deliberately created isolated test stores, not databases inferred disposable from a name.

Existing SQL scripts were installed **only in those generated fixture databases**; no deployed migration or grant ran, and no migration/history file changed. Fixture actor rows and test-host authentication are synthetic test setup, not user/role provisioning in the application. Test-host identities do not establish deployed OIDC acceptance. Clinical writes use real procedures/audits where exercised. Teardown disposes the generated databases; new file tests use their own GUID-named temporary storage directory and remove only those harmless fixture bytes. No existing development clinical history, signed PDFs or audit rows were cleaned up.

## C. Ten-workflow execution results

Identifiers below omit the common `MicroEMR.Api.Tests` namespace and `.cs` suffix. **PASS is limited to the stated automated layers**, not complete browser/deployed acceptance.

| Workflow | Result | Executed evidence and remaining boundary |
| --- | --- | --- |
| **W01 Patient search/register/demographics** | **PARTIAL** | `PatientRegistrationServiceTests`, `PatientDemographicCertificationTests`, `PatientAlternativeContactTests`: registration/care-team behavior, validation, actor, demographics/contact transport and concurrency/SQL contracts pass. **No actual patient search → registration → edit/reload browser/SQL journey ran.** |
| **W02 Appointment booking/move/cancel** | **PARTIAL** | `NextAvailableAppointmentSqlTests.FinalBookingRejectsSlotOccupiedOrBlockedSinceAvailabilityReadAndKeepsNormalCreation`; `AdHocAppointmentSqlTests.EditConversionAndDragDropRescheduleRevalidateAndPreserveModePatientAndCritical`, `CancellationStatusAuditAndSeparateTenantDatabaseKeepIdentityAndHistory` pass with real SQL, saved state/history and foreign-store rejection. **No scheduling UI saved-change observation ran.** |
| **W03 Chart review** | **PARTIAL** | Representative `ProblemFieldSqlTests.ApiRoundTripPreservesClinicalFieldsPrecisionAndCompleteAudit`, `AllergyFieldSqlTests.RealApiPersistsDinStagePrecisionAndNativeProvenanceWithCompleteAudit`, `HistoryFieldSqlTests.MedicalAndSurgicalRoundTripPrecisionStageStatusNotesAndAudit` pass across their selected data cases. Step144 also creates/reopens medication details/history. **Whole chart composition/navigation remains untested.** |
| **W04 Appointment → encounter → saved note** | **PARTIAL** | `StartEncounterStatusIntegrationTests` and `EncounterDraftSaveTests` pass for status/link reuse, template validation, processed draft values, contributions and Web conflict mapping using doubles/contracts. Native Open encounter PDFs pass. **No appointment-linked note save/reopen against real encounter persistence/UI ran.** |
| **W05 Vitals** | **PASS — repository/SQL** | Existing `PatientVitalsTests` pass. New `DemoWorkflowRoundTripTests.VitalsSaveReopenPreservesMeasurementsBmiOwnershipAuditAndConcurrency` saves/retrieves BP, height/weight/date/notes; verifies BMI 25 then 27.78, wrong-patient denial, stale rejection, invalid SpO2 rejection, one remaining record and exactly two actor-attributed audits. **No vitals browser form claim.** |
| **W06 Prescription** | **PARTIAL** | `PrescriptionRefillInstructionsTests`, `PrescriptionPrintTests.NewFieldsSignedSnapshotAndRepeatedOutputRemainFrozenAndCorrelated`, `PrescriptionBrandingTests.SignedAndUnsignedFreezeBrandingAndKeepClinicalValues` and native prescription PDF case pass. Real browser Step144 edits/reopens a preseeded Draft and checks validation/permissions. **No connected browser new-Draft → review → print journey ran.** |
| **W07 Referral** | **PARTIAL** | `PatientReferralApplicationApiTests`, `ReferralLetterCompositionTests`, corrected `ReferralFinalizationSqlTests.ExistingSendTransactionGuardsPreservationScopeAndSentComposition` pass. Native referral PDF logo/text-only cases pass. SQL fixture proves immutable artifact/audit/state handling; composition tests verify recipient/patient/provider/content. **Creation, composition and output remain separate tests, not one UI journey.** |
| **W08 Consultation** | **PARTIAL** | `ConsultationReportTemplateTests`, `ConsultationSigningTests`, `ConsultationFinalPdfTests` and native branded consultation PDF cases pass. Includes template processing, stored final bytes, attribution and no render-on-final-read. **No real database/browser create → save → reopen consultation journey ran.** Signing tests here use isolated in-memory fixtures. |
| **W09 Uploaded documents** | **FAIL — PRODUCT DEFECT D01** | Existing storage/route/Web stream/audit tests pass. New `DemoWorkflowRoundTripTests.DocumentUploadListOpenRetainsBytesMetadataPatientOwnershipAndAudit` proves actual upload, SQL list/metadata, stored bytes, attributed audit and wrong-patient/foreign-tenant denial, then **owned content retrieval returns null**. Byte-download assertions cannot run past that failure. |
| **W10 CPP/immunization/Open PDFs** | **PASS — service/native PDF** | All **9** `BrandedChartPdfTests.NativeSamplesPreserveIdentityContentAttributionAndPageBounds` combinations pass: three generators × logo/no-logo/long. Actual production rendering and extracted content/page/image/bounds assertions pass with synthetic service inputs. **No authenticated deployed PDF generation/download or historical persisted-artifact comparison ran.** |

## D. Browser execution

**4 distinct cases, all PASS**, real Chromium → production Razor/Web clients → loopback API → generated SQL:

- `Step144IntegratedAcceptanceTests.BrowserApiSqlSaveReloadHistoryValidationAndPermissions`: **1440 and 390 px**. Medication creation/edit/history, prescription Draft edit/reload, input validation and reader-denied writes with unchanged state/audit. Its chart landing is a placeholder; it does not prove full chart navigation or prescription creation/printing.
- `ImmunizationDoseBrowserTests.RealFormCreateEditValidationReloadHistoryAndScreenshot`: **1440 and 375 px**. Real immunization form save/edit/reload/history and dose/unit validation. This is supporting chart automation, not an additional core workflow or immunization PDF download check.

Existing screenshot capture was reused; no fragile image baseline or human visual inspection was added. No new Playwright framework, browser installation or live login occurred.

## E. Service/API/database/PDF evidence and accounting

Receipts and exact runnable filters are under `.tmp/demo-readiness/phase1/`; `summary.json` deduplicates by full test display name and takes the latest result.

| Exclusive category | Distinct cases | Final PASS / FAIL | Receipt |
| --- | ---: | --- | --- |
| Service/controller/Web transport/SQL-text contracts | 122 | **122 / 0** | `results/core/core.trx`, corrected cases in `results/recheck/recheck.trx` |
| SQL/API/service-storage integration | 26 | **25 / 1** | `results/sql/sql.trx`, `results/recheck/recheck.trx`, `results/document-recheck/document-recheck.trx` |
| Browser/API/SQL | 4 | **4 / 0** | `results/browser/browser.trx` |
| Native PDF test cases | 14 | **14 / 0** | `results/pdf/pdf.trx` |
| **Total** | **166** | **165 / 1** | `summary.json` |

Categories are exclusive for counting although browser cases also exercise API/SQL. The requested non-browser/non-PDF API/integration tally is **148 executed / 147 passed**, explicitly including 122 service/contract cases; it is **not** 148 real database tests. PDF tally counts test cases, not pages or unique files: nine chart cases, two referral cases, two consultation cases and one multi-sample prescription case. Native PDFs use production rendering/PdfPig content/layout checks; referral/consultation tests also check preserved synthetic bytes. Prescription SQL tests exercise v3 snapshots, signed/unsigned branding and independent refill values. No shared historical record was signed or regenerated.

Initial run: **164 cases, 160 pass / 4 fail**. After narrow test corrections plus two additions: **6 cases, 5 pass / 1 new product failure**. Only the failing document case was then rerun with an explicit diagnostic assertion and failed again. **171 execution attempts**, **166 distinct cases**; repeated failures/passes are not counted as additional coverage.

Existing baseline cases ran from the retained Step04B test output without rebuilding. This is **new execution of compiled tests**, not reuse of their historical PASS receipts and not a whole-tree current-build claim. To validate test changes, one affected test-project/dependency build used isolated `.tmp/demo-readiness/phase1/bin/` output: **0 warnings/errors**. After improving the document failure message, only the test project was rebuilt without dependencies: **0 warnings/errors**. Passing cases were not rerun. No full solution build occurred and running deployment output was not replaced.

## F. Confirmed defect and corrected test defects

**D01 — W09 document upload cannot reopen owned content; blocks an upload/view/download demonstration.**

Reproduction: run `DemoWorkflowRoundTripTests.DocumentUploadListOpenRetainsBytesMetadataPatientOwnershipAndAudit` through the isolated runner/LocalDB connection. Upload harmless `demo.txt` through production `PatientFileService` + `PatientFileRepository` + `LocalPatientFileStorage`; list it, then call `OpenContentAsync` with the returned patient/FileUid. Expected: readable original bytes and matching metadata. Actual: upload/list/audit/storage succeed, but content is null. Wrong-patient and foreign-tenant checks correctly deny access.

Cause is established by runtime diagnostics and source: [upload service](../../src/MicroEMR.Application/PatientFiles/PatientFileService.cs) creates GUID A for the storage key; [existing PatientFile_Create procedure](../../db/tenant-clinical/migrations/0040-patient-file-external-report-metadata.sql) generates separate metadata GUID B. The service's existing owned-content check expects a key ending in B and rejects A. Final receipt shows metadata suffix `fa1aff28746640ecab72bdea437b9da4` versus stored suffix `5d2e173cd9f14f129710a314d8b2a344`; both are disposable synthetic identifiers. Bytes actually exist, and the upload has one attributed audit. This is not missing browser authentication or permission 229. The failing assertion intentionally remains; **no product fix or weakened ownership guard** was applied.

Four initial **TEST DEFECTS**, all corrected and verified only with affected reruns:

- `PatientAlternativeContactTests`: assumed migration 0066 was last; now requires its unique presence without rejecting later migrations.
- Two `PatientDemographicCertificationTests`: hard-coded five-parent repository path failed from retained isolated output; now locate the repository using AGENTS.md.
- `ReferralFinalizationSqlTests`: fixture stopped at migration 0065 although current models include FileUid. Added existing file-foundation/0068 scripts to disposable setup; successful rerun retains finalization/audit/immutable-byte assertions. No production migration changed.

## G. Environment limits

No unresolved blocker prevented the selected isolated suites. Installed Chromium and user-local SQL worked outside the sandbox. Deployed OIDC login/session was unavailable to automation and was not requested; no deployed acceptance was attempted. Product009 P3A's earlier health/deployment evidence remains **historical**, not a new result of this session. Its 30 deferred scenarios remain deferred.

## H. Maximum five demo-critical remaining items

1. **P0 defect D01:** align upload storage identity with persisted FileUid while retaining tenant/patient ownership checks, auditing and existing stored history. Fix in a separately authorized product task, then rerun the failing round trip and relevant file tests.
2. **P0 front desk gap:** connected patient search/register/edit and appointment UI save/move/cancel/reload. Backend tests alone do not close it.
3. **P0 visit gap:** actual selected-patient chart → appointment-linked encounter → persisted/reopened notes and vitals UI. Do not substitute service doubles or a placeholder chart destination.
4. **P1 prescription gap:** connected new Draft creation/review/print from the same synthetic order, retaining independent refill values and v3 semantics.
5. **P1 correspondence/output gap:** connect saved referral/consultation drafts to previews; later verify authenticated chart PDF downloads without signing, sending or changing branding in shared development.

## I. Prompt 3 recommendation and changed files

Use Prompt 3 to prioritize **D01 first**, then decide the minimum P0 connected journeys required for the intended prepared demonstration. Existing isolated results support many subsystems, but the document failure and missing front-desk/visit chains prevent an unconditional staff-evaluation readiness claim. No owner manual clinical testing is required; use automated isolated verification and later evaluation by experienced medical staff. Do not turn this into certification work.

Changes: this execution report; new `tests/MicroEMR.Api.Tests/DemoWorkflowRoundTripTests.cs` (two procedure/storage round trips); narrow corrections in `PatientAlternativeContactTests.cs`, `PatientDemographicCertificationTests.cs` and `ReferralFinalizationSqlTests.cs`. Temporary preflight/runner, outputs, logs and TRX receipts are under `.tmp/demo-readiness/phase1/`. Final targeted diff/whitespace review found no application-source changes or unintended tracked edits; prior owner reports remain preserved. **No application fixes, shared clinical writes, role/grant/OAuth changes, service restarts, deployments, remote Git operations or commits. Stop here; Prompt 3 has not started.**
