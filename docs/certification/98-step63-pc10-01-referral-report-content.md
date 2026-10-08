# Step 63 — PC10.01 Selected Specialist Consultation/External-Report Content Implementation

Date: 2026-10-08. Branch: `feature/step-63-pc10-01-referral-report-content`.

**Report-content sub-gap: IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION. Overall PC10.01: PARTIAL.** Alternative-contact sub-gap remains **SATISFIED — VERIFIED**; PC10.02 remains **IMPLEMENTED — NEEDS MANUAL VERIFICATION**. The Step 62 inventory remains **19 outstanding unique mandatory requirement IDs across 7 original packages**. Implementation alone does not remove PC10.01 from that inventory.

## Requirement and scope

Resume [Step 62](97-step62-pc07-11-evidence-closure-next-gap.md), with the OntarioMD **Primary Care Baseline — Version 1.7 Final** interpretation already established in the referral evidence. The exact selected content target is:

> PC10.01 — Include actual selected specialist consultation/external-report content in generated and preserved referral letters.

This implements the recorded omission of actual selected content beyond titles/types. It retains the existing PatientReferral selection, composer, preview, Mark Sent and immutable artifact architecture from [Step 47B.2](81-step47b2-referral-clinical-selection-ui-preview.md) and [Step 47C](82-step47c-immutable-final-referral-preservation.md). This is the previously established interpretation, not a fresh verbatim extraction of the standard. No certification-wide reanalysis or additional referral requirement was introduced.

## Sources, selection and composition

Linked **PatientDocuments** already persist report identities on the referral. Plain stored report text is HTML-encoded and rendered in full with preserved line breaks. Structured reports use their historical template definition and captured structured values through the existing `ITemplateInstanceRuntime.RenderSnapshot` and clinical print layout. No clinical findings, diagnoses, medication instructions or conclusions are summarized or rewritten.

For signed consultation reports, the composer reuses `IConsultationSigningService.OpenFinalPdfAsync` and its authoritative **ClinicalOutputArtifact** PDF. It does not substitute the mutable text field for that final PDF.

Uploaded external reports are **PatientFiles**, a separate existing source. The old clinical-selection reference shape could not persist a PatientFile UID. Migration 0068 extends the existing selection table and procedures with nullable `FileUid` and kind `FILE`; it creates no second selection table or document store. The Draft editor offers active patient files in “Uploaded External Reports,” restores persisted checked identities and preserves unavailable choices until explicitly removed. Existing linked-document choices remain usable.

Selected uploaded PDFs are included as full pages. Selected UTF-8 text files are rendered in full, including source title/type, author, organization and available report/received dates. Missing metadata is labeled “Not recorded.” Unselected bodies are never enumerated or composed. Existing clinical-selection and document-link order is retained; PDFs receive consecutive appendix numbers in that traversal order.

## Preview, print and preservation

Preview and Mark Sent use the same composition path. Text bodies appear after the selected clinical information. Each PDF is identified in the letter and follows it in a numbered appendix with a source-identifying cover page rendered by the existing `IClinicalPrintLayoutRenderer` / `IPdfRenderer`. Full original report pages follow the cover, in order. The single returned PDF includes all appendices and is printable.

No PDF combination mechanism existed in this path. Infrastructure adds **PdfPig 0.1.12** solely for page import/assembly, using an already cached package with no additional net8 dependencies. This is not a second HTML renderer or general conversion service. Its [primary documentation](https://github.com/UglyToad/PdfPig) describes page import and the exclusion of annotations/forms from that import. Consequently this implementation rejects interactive annotations, AcroForm and optional-content layers rather than silently discard clinical content. Malformed/unreadable sources also fail. Use readable flattened PDFs. Image-only pages inside such PDFs are supported without text extraction.

Mark Sent assembles the complete output **before** the existing atomic `SendWithArtifactAsync` write. The existing PatientReferralArtifact stores the resulting bytes, SHA-256 and snapshot. The snapshot adds selected FileUid references and report provenance: source kind/UID, title/type/status, source row version, MIME type, byte length, source-content hash and appendix number. No redundant full source body or second immutable referral table is added.

Sent open/download continues to return stored artifact bytes. It never resolves current report content. Later edits, source archival, selection changes or demographic changes therefore cannot alter the preserved output. Existing referring-provider/actor/date metadata, alternative-contact composition and finalization audit remain in the same path.

## Failure handling, ownership and audit

Selected missing/archived sources, empty text, unavailable historical templates, missing signed PDFs, inconsistent file identity/type/length/hash, unreadable UTF-8 and unsupported formats block preview and send with actionable selection errors. Storage I/O failures and consultation storage-ownership failures also become actionable errors, without paths or internal details. Nothing falls back to a title-only reference. Audit failure blocks disclosure/finalization.

Uploaded image files and other formats are deliberately unsupported in this referral composition step; use PDF or UTF-8 text, or remove the selection. Images embedded in supported PDFs remain part of the full imported pages. PDFs with annotations/forms/layers require flattening. These qualifications must be checked with the clinic's real reports before acceptance.

Existing authentication, referral access, PatientsView, ReferralsView/Manage and DocumentsView checks remain. Every selected document/file must match the patient and requested source UID. SQL ownership checks and all repository reads use existing tenant connection routing. Uploaded content now additionally verifies the exact tenant/patient/file storage key before storage access; consultation content retains its existing tenant/patient/document artifact-key guard. Source storage is never exposed through a new endpoint.

Report text reads use `PatientDocumentViewed`; signed PDF reads also use `PatientDocumentDownloaded`; uploaded file content uses `PatientFileDownloaded`, with patient/source IDs and correlation identifiers through the existing structured read-audit service. SQL selection replacement retains active clinical actor resolution, audit logging, Draft-only mutation, aggregate row-version locking, same-patient ownership and soft-ending removed selections.

## Schema and deployment

New additive tenant migration: `0068-referral-report-content.sql`, appended to `db/tenant-clinical/manifest.json`. It adds FileUid, its foreign key, reference constraint extension and filtered active-selection unique index. It replaces only `PatientReferralClinicalSelection_Get` and `PatientReferralClinicalSelection_ReplaceDraft` to read/write/validate/audit FILE references alongside existing kinds. The final-artifact/send procedures are unchanged. Historical migrations are unchanged; no EF migration or migration-history rewrite.

**Deploy migration 0068 through the existing tenant migration workflow before running this application version.** The repository now expects the FileUid result column. The migration was reviewed but not executed against live SQL Server during this step. Tenant migration execution, persistence, concurrency, audit rows and real endpoint isolation remain runtime acceptance work. Storage keys must match the existing tenant-prefixed upload convention; mismatched legacy/corrupt keys are refused, not silently reassigned.

## Changed files

| Area | Files |
| --- | --- |
| SQL | `db/tenant-clinical/migrations/0068-referral-report-content.sql`; `db/tenant-clinical/manifest.json` |
| Application registration | `src/MicroEMR.Application/DependencyInjection.cs` |
| Referral composition | `src/MicroEMR.Application/PatientReferrals/ReferralReportContentModels.cs`; `ReferralReportContentService.cs`; `PatientReferralService.cs` |
| Selection/options | `src/MicroEMR.Application/PatientReferrals/ReferralClinicalSelectionModels.cs`; `ReferralClinicalContentService.cs`; `PatientReferralService.ClinicalSelections.cs`; `PatientReferralService.ClinicalOptions.cs` |
| File ownership | `src/MicroEMR.Application/PatientFiles/PatientFileService.cs` |
| Infrastructure | `src/MicroEMR.Infrastructure/DependencyInjection.cs`; `MicroEMR.Infrastructure.csproj`; `PatientReferrals/ReferralPdfAssembler.cs`; `PatientReferrals/PatientReferralRepository.ClinicalSelections.cs` |
| UI | `src/MicroEMR.Web/ClientApp/patients/patient-referrals.ts`; `Views/Patients/Details.cshtml`; generated `wwwroot/dist/patients/patient-referrals.js` and `.js.map` |
| Tests | `tests/MicroEMR.Api.Tests/ReferralReportContentTests.cs`; `ReferralFinalizationTests.cs`; `ReferralLetterCompositionTests.cs`; `ReferralClinicalContentTests.cs` (shared fixture retains FileUid); `tests/pc10-referral-clinical-selection.test.cjs` |
| Evidence | This report only; historical reports unchanged |

## Focused validation

- **54 backend cases passed**, zero failures/skips: new ReferralReportContentTests plus the affected composition-order/final-date and atomic-finalization/send-error/immutable-selection cases. Evidence: `artifacts/step63/step63-focused.trx`.
- Coverage includes full structured/text content, multiple distinguishable PDFs, ordered full pages, image-only pages with unchanged dimensions/image bytes, explicit excluded sources, patient/source identity, tenant storage rejection before reads, permissions/audits, missing/archived sources, unavailable templates, hash/length/UTF-8 errors, unsupported PDFs, preview/send consistency and immutable reopen after source/demographic/selection changes.
- **10 frontend tests passed** via `node tests/pc10-referral-clinical-selection.test.cjs`, including FILE reload/save and explicit unavailable-choice removal.
- **Affected TypeScript compilation passed** using the local compiler on `ClientApp/patients/patient-referrals.ts`, strict ES2020 with source maps; only its generated JavaScript/map changed.
- **Application, Infrastructure, API and Web builds passed** through the focused test project's reference build, with isolated output under `artifacts/step63/build`. Core and DatabaseTool built as project references. No separate solution build.
- Initial tooling attempts encountered parallel restore/Node child-process restrictions. Single-node restore and direct Node execution succeeded. A PDF dictionary-key type mismatch and invalid synthetic PNG fixture were corrected; final checks pass. Rebuilds were limited to those fixes and the final ownership/missing-template checks.
- Final Git diff/whitespace/scope reviewed. No live database, browser, application launch or Playwright verification. Test doubles prove the application decisions, not real SQL transactions or authenticated multi-tenant endpoint behavior.

Reproduction:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -m:1 -nr:false -p:UseArtifactsOutput=true '-p:ArtifactsPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step63/build' --filter 'FullyQualifiedName~ReferralReportContentTests|FullyQualifiedName~ReferralLetterCompositionTests.SelectedContentFollowsNarrative|FullyQualifiedName~ReferralLetterCompositionTests.FinalCompositionUsesSendDate|FullyQualifiedName~ReferralFinalizationTests.FinalizesPersistedChoices|FullyQualifiedName~ReferralFinalizationTests.FailedFinalizationLeavesDraft|FullyQualifiedName~ReferralFinalizationTests.SentSelectionsCannotBeReplaced|FullyQualifiedName~ReferralFinalizationTests.SendApiReports' --logger 'trx;LogFileName=step63-focused.trx' --results-directory artifacts/step63 --verbosity minimal
node tests/pc10-referral-clinical-selection.test.cjs
# Run inside src/MicroEMR.Web:
node node_modules/typescript/bin/tsc ClientApp/patients/patient-referrals.ts --target ES2020 --module ES2020 --moduleResolution Bundler --strict --skipLibCheck --sourceMap --outDir wwwroot/dist/patients
```

## Manual acceptance

1. Apply tenant migration 0068, run the updated application, and open a patient with structured/text and signed/uploaded PDF reports, including a scanned multi-page PDF and an additional report to leave unselected.
2. Create a Draft referral, select one or more reports through existing PatientDocument links and uploaded-file choices, save and reopen. Confirm only the intended choices are checked.
3. Preview. Compare text word-for-word with its stored report; check source titles/types/authors/dates and full PDF appendix page counts, images, orientation, readability and order. Confirm the unselected report body is absent.
4. Mark Sent, open/download/print the preserved PDF and confirm all selected bodies/pages. Verify the existing finalization audit plus new source-read audit events and same-patient source identities.
5. Edit or archive an editable source report, change demographics, and change selections on a later referral. Reopen the original Sent referral and compare its original bytes/hash and content.
6. Probe another patient's source and a second tenant's UID/storage reference through the existing authorized endpoints. Confirm no selection/content disclosure; exercise a role without DocumentsView.
7. In a test environment, try a missing file/final PDF, archived selection and an annotated/form PDF. Confirm the actionable error and that no Sent transition/artifact occurs. Flatten an eligible PDF or remove the unavailable selection, save and retry.

Successful acceptance evidence may support later PC10.01 closure; this report does not automatically declare it SATISFIED. PC10.02 requires its separate retained manual verification.

## Git and resource compliance

Starting branch `main`, starting working tree clean. The dedicated Step 63 branch was created before implementation; existing changes were preserved. No commit, merge, push, stash, reset, rebase or branch deletion. The user reviews, verifies, commits and merges manually.

Read AGENTS.md, the Step 63 request, Step 62 and directly relevant Step 47 evidence/source/test paths. No repository-wide scan, certification-wide reanalysis, broad API/Auth/solution suite, scheduling/CPP suite or alternative-contact revalidation. No unrelated clinical implementation or historical-report rewrite. Stop after Step 63; Step 64 is not started.
