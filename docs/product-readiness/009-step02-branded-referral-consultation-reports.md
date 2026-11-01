# Product Improvement 009 — Step 02: Branded Referrals and Consultations

2026-10-31 (America/Toronto). **BLOCKED — IMPLEMENTATION INCOMPLETE.** Stopped at the request's explicit branding-permission gate before changing either generator. Step 02 is not implemented, built, deployed or runtime verified.

Branch: `main`. Inspected Git status once; working tree was clean at task start. Only this new report and ignored in-memory diagnostic evidence were added. Existing 007/008/009 Step 01 code, migrations, generated assets, deployed services and Step 01B report remain unchanged.

## Confirmed blocker

The existing `ClinicBrandingService.GetPrintHeaderAsync` calls `RequireAsync(false)`. That requires a nonempty authenticated tenant and **Patients.View OR ClinicSettings.Manage** before resolving clinic configuration or logo data.

Consultation generation has a different existing authorization boundary:

| Operation | Existing API permissions |
| --- | --- |
| `PatientDocumentsController.PreviewPdf` | Class `Documents.View` plus action `Documents.Manage` |
| `PatientDocumentsController.SignConsultation` | Class `Documents.View` plus action `Documents.Sign` |

Neither requires Patients.View or ClinicSettings.Manage. `ClinicalPdfPreviewService`, `ConsultationSigningService` and the internal patient lookup do not introduce that missing permission as a prerequisite. Effective access supports independent permission sets/overrides; it does not automatically expand Documents permissions into Patients.View.

Therefore, directly integrating the existing branding resolver would reject supported consultation users who can currently preview or sign documents. This would add a new document-generation authorization requirement, contrary to Step 02's requirement to preserve existing authorized access. It would also deny the no-logo case before the resolver could return text-only fallback.

The task explicitly instructs: **“If the new document-generation path cannot access branding using existing authorized permissions, STOP and report the exact missing permission or configuration.”** That gate applies here. This is an Application permission-boundary mismatch, not an established SQL or Chromium failure. No runtime configuration, database permission or user membership was changed to work around it.

## Narrow entry-point and lifecycle findings

### Referral letters

- Web `PatientReferralsController.Letter(patientUid, referralUid, preview)` forwards draft previews or stored-letter reads.
- API `PatientReferralsController.PreviewLetter`: `GET api/patients/{patientUid}/referrals/{referralUid}/letter/preview`, protected by Referrals.View and Referrals.Manage.
- Application `PatientReferralService.PreviewLetterAsync` accepts Draft only and calls `BuildArtifactAsync(..., isDraftPreview: true)` without persisting a sent artifact.
- `PatientReferralService.MarkSentAsync` retains transition/concurrency checks and authenticated actor resolution, generates through `BuildArtifactAsync`, then persists via `IPatientReferralRepository.SendWithArtifactAsync`.
- `BuildArtifactAsync` gets current clinic information through `IClinicConfigurationService`, authoritative patient/provider data, linked documents and selected clinical/report composition. It renders through `IClinicalPrintLayoutRenderer` and `IPdfRenderer`; existing appendix assembly and clinical snapshot/hash creation follow. Recipient/referrer/date/patient/body fields are explicit and must remain intact.
- `ReferralClinicalContentService.Access` already requires Patients.View, Referrals.View and Referrals.Manage. Thus its existing generation boundary satisfies branding read access; it is not the blocker.
- API `Letter` / Application `OpenArtifactAsync` read preserved repository PDF bytes. Branding integration must not change that historical retrieval path.

### Consultation reports

- Web `PatientDocumentsController.PreviewPdf` forwards to API `POST api/patient-documents/{documentUid}/pdf-preview`.
- `ClinicalPdfPreviewService.PreviewPatientDocumentAsync` permits Draft schema-driven documents, checks template provenance and processes draft data, then uses the common clinical layout/PDF renderer. Signed documents are rejected with the instruction to use the preserved final PDF.
- API `POST api/patients/{patientUid}/documents/{documentUid}/sign` calls `ConsultationSigningService.SignAsync`. Its existing signing repository callback supplies the authoritative `ConsultationSigningContext` to `ClinicalPdfPreviewService.RenderConsultationFinalAsync`.
- `RenderConsultationFinalAsync` preserves template/data processing, saved recipient snapshots, signer display name and trusted signing timestamp while rendering through the common layout and existing PDF engine.
- `ConsultationSigningService` creates the new tenant-owned artifact through the existing storage/artifact repository workflow. `OpenFinalPdfAsync` verifies Signed/finalized state, patient ownership and tenant storage prefix, then opens stored bytes. It does not regenerate historical signed PDFs.

Both generators use `ClinicalPrintLayoutRenderer` and the existing Playwright PDF engine. The Step 01 `ClinicPrintHeaderRenderer` can provide a shared fragment/CSS, but an opt-in integration must preserve the existing patient context/body/signature layout, avoid duplicate clinic headers/titles, and leave encounter/other output callers unchanged. No adapter or layout change was implemented because the consultation permission gate failed first.

## Focused diagnostic verification

Ran **two in-memory diagnostic cases** against the existing compiled Step 01 Application assembly, with a nonempty synthetic tenant context:

1. Effective permissions `{Documents.View, Documents.Manage}` → `GetPrintHeaderAsync("Consultation Report")` throws UnauthorizedAccessException.
2. Effective permissions `{Documents.View, Documents.Sign}` → the same branding read throws UnauthorizedAccessException.

**2/2 reproduced the expected blocking denial.** These are permission-gate diagnostics, not passing Step 02 integration tests. No database, patient, logo, artifact or actor repository access occurred; denial happens before those dependencies are accessed. Evidence: `.tmp/product-improvement-009/step02/permission-gate.fsx` and `permission-gate.json`.

Reused existing lifecycle/read-audit tests and Step 01 findings by inspection; did not rerun the previous 37 tests, full suites or certification. No affected build ran because no production/test source changed and the task requires stopping at this gate. No disposable PDF samples were generated; visual layout, pagination, logo/no-logo output and Step 02 persistence assertions remain **NOT RUN**.

## Files changed and preservation

- New report: `docs/product-readiness/009-step02-branded-referral-consultation-reports.md`.
- Ignored diagnostic files under `.tmp/product-improvement-009/step02/`.

No referral/consultation generator, renderer, authorization policy, database migration/procedure/grant, clinical record, saved artifact, signing/recipient snapshot, timestamp, read-audit behavior or service deployment was modified. Existing historical artifacts were not read from persistent storage or regenerated. The five previously missing artifact files were not investigated or repaired. Step 01B runtime evidence remains in its original report.

## Required owner direction and remaining work

Before implementing both generators, obtain authorization for a narrowly defined branding-read boundary that supports existing document-authorized users without adding broad patient-view or clinic-management access. Requiring Patients.View for current Documents-only users, bypassing `ClinicBrandingService`, or silently dropping branding on authorization failure is not an approved fix. No new SQL grant is justified by the current evidence.

After that boundary decision is explicitly authorized, implementation can proceed with the existing resolver/shared header and opt-in referral/consultation layout integration. Then run R1–R7, C1–C8 and S1–S8 coverage using synthetic/disposable fixtures, verify historical-byte/signing/read-audit preservation, create the four requested branded/text-only samples and a multipage sample, and perform PDF visual review. These implementation/acceptance items remain pending.

Deployment prerequisites remain future owner-authorized compatible API/Web deployment after successful local verification; this task neither performed nor prepared a deployment. Migration 0104/Step 01 infrastructure is the baseline, and no new schema requirement was found. Clinical-only and cross-tenant runtime qualifications from Step 01B remain unresolved and are not promoted to PASS here.

**Recommended next action:** authorize the narrowly scoped branding-read permission-boundary decision, then resume Step 02 implementation. **BLOCKED — IMPLEMENTATION INCOMPLETE.** No deployment/restart, Git state change, persistent database operation or Step 03 work occurred.

## Step 02A — Branding-Read Permission Correction

2026-10-31 (America/Toronto). The owner separately authorized only the narrow local authorization correction and focused verification. **SATISFIED — LOCALLY VERIFIED.** The resolver blocker is resolved for a future Step 02B integration. The referral/consultation generators remain unmodified and are not yet branded; this does not supersede that implementation boundary or claim runtime deployment.

### Root cause and selected correction

The original general-purpose branding resolver recognizes Patients.View or ClinicSettings.Manage, while existing consultation preview/signing policies recognize different document-operation permissions. Reusing that general read entry point would have added an unintended patient-view or administration prerequisite.

Added `ClinicBrandingService.GetPrintHeaderForDocumentAsync(ClinicBrandingDocumentOperation operation, string title, CancellationToken token)` as a **server-side Application service method, not an HTTP endpoint**. It rechecks the exact existing permission set for the specified operation before accessing clinic configuration or logo data:

| Operation | Required effective permissions |
| --- | --- |
| ConsultationPreview | Documents.View **and** Documents.Manage |
| ConsultationSigning | Documents.View **and** Documents.Sign |
| ReferralGeneration | Patients.View **and** Referrals.View **and** Referrals.Manage |

The consultation combinations match `PatientDocumentsController` class/action policies. Referral requirements include the existing `ReferralClinicalContentService.Access` patient-view prerequisite as well as the API referral permissions. **P3 without Patients.View is not applicable:** the present referral generation architecture already requires it, and this correction does not remove it.

The method uses the existing `IAuthenticatedSubjectAccessor` to require a current subject, rejects missing trusted tenant context, rejects undefined operations, and requires **every** operation permission. ClinicSettings.Manage alone does not bypass document-operation requirements; Documents.View alone authorizes neither consultation preview nor signing through this method. Existing API subject-accessor registration is reused; no new authentication service, role, user permission, policy name or database grant was introduced.

Extracted the unchanged header-model construction into private `LoadPrintHeaderAsync`, shared by the existing and new authorized read entry points. Existing general `GetPrintHeaderAsync`/`GetLogoAsync` still require Patients.View or ClinicSettings.Manage. Existing state and logo upload/replace/remove still require ClinicSettings.Manage. API/Web branding controllers were not changed: Documents-only callers remain denied at the general header/logo/state endpoints, and no operation selector was added to a public request.

### Document authorization and tenant safeguards

This method retrieves only current-tenant header data. It grants no document/patient access and performs no signing, document lookup, mutation, persistence or audit operation. Tenant selection remains server-owned `ITenantContext`; the method accepts no tenant ID, patient ID, document ID or storage key. The existing tenant-scoped repository/factory and SQL permissions are unchanged. Header failures propagate; authorization/context failures are not swallowed or converted into a silent no-logo fallback.

The new check supplements the document workflow rather than authorizing it. **Step 02B must hardcode the appropriate operation at each server call site, after authenticated tenant resolution, document/patient ownership/state checks and existing document-operation authorization.** Branding must not be consulted to decide document access. No document call site invokes the new method in this step; accordingly, no circular dependency or new reachable document-access route was introduced.

Existing wrong-patient/tenant final-artifact rejection, stored-byte retrieval and mandatory read-audit behavior remain under their original services/controllers. Their focused tests passed. End-to-end ordering of a document request followed by this new method is not reachable until Step 02B and must be tested there; no artificial end-to-end PASS is claimed here.

### Exact files changed

- `src/MicroEMR.Application/ClinicConfiguration/ClinicBrandingService.cs` — operation enum, subject dependency, guarded server-side read method and private shared model loader.
- `tests/MicroEMR.Api.Tests/ClinicBrandingTests.cs` — extended the existing fixture with permission sets, real subject-accessor injection and configuration-read/failure observation; retained all nine existing scenarios.
- `tests/MicroEMR.Api.Tests/ClinicBrandingDocumentAuthorizationTests.cs` — focused positive, negative, general-endpoint and real document-policy-handler checks.
- `docs/product-readiness/009-step02-branded-referral-consultation-reports.md` — this appended record; original blocked assessment retained.

Branch remains `main`. Git status was inspected once; the pre-existing untracked Step 02 report was preserved and extended. No Step 01/01B report, generator, print-header layout, migration, stored procedure, SQL grant, clinical record, artifact, signing logic, authentication configuration or deployment output was changed.

### Focused verification

**43 PASS / 0 FAIL / 0 SKIPPED**, one focused test execution. Receipt: `.tmp/product-improvement-009/step02a/results/branding-document-authorization.trx`.

| Coverage | Count | Actual result |
| --- | ---: | --- |
| New positive service cases | 4 | The two original Documents-only diagnostic combinations now succeed through the appropriate new method, with logo/clinic data. Existing referral prerequisites plus no-logo fallback succeed. Switching trusted synthetic tenant contexts selects independent logos/clinic identity. |
| New negative service/endpoint cases | 21 | Missing/incorrect operation permissions, Documents.View-only signing, preview/sign cross-use, missing referral prerequisites, undefined operation, missing authenticated request/subject, missing tenant, non-admin upload/replace/remove/state access, and direct general API header/logo/state access all remain denied before unauthorized reads/writes. Authorized configuration failures propagate; failed reads do not bypass a later permission check. |
| Existing document endpoint policy evaluation | 4 | Real combined class/action policies and authorization handlers: preview and signing permit their own exact combination; Documents.View alone and preview permissions cannot authorize signing. Two allow and two deny cases. |
| Existing branding regressions | 9 | Patients.View reads, ClinicSettings.Manage administration, tenant separation, replacement/removal/versioning, private responses and existing negative checks retained. |
| Existing final-PDF/read-audit regressions | 5 | Original bytes and read-audit actions, audit failure preventing disclosure, edit permission not authorizing signing, and wrong-patient/tenant-key rejection retained. |

Equivalently, the newly evaluated authorization cases comprise **6 positive and 23 negative** checks, plus **14 existing regressions**. Permission sets are test inputs to the real branding service; tests also exercise the real API subject accessor, general branding controller and document authorization-policy handlers. No new access decision is represented solely by an always-true permission mock.

P4/P5 remain covered by the existing branding scenarios; P6/N6 by tenant-selected service reads. P3 without Patients.View is N/A for the existing referral architecture. N7/N9 retain the existing wrong-patient/tenant and read-audit regressions; full document-to-new-reader integration remains pending rather than manufacturing a path that Step 02A explicitly forbids integrating. The test suite used in-memory fixtures only, without database access, clinical writes, image processing or persistent artifact access. No Scheduling, certification, full solution or complete 37-test Step 01 suite was run.

### Build and commands

Affected Application/Infrastructure/API/Web/Razor references compiled in **one isolated build pass**. The initial test-project compilation reported a test-only reference to a nonexistent `DenyAnonymousAuthorizationHandler`; corrected it to the existing built-in `DenyAnonymousAuthorizationRequirement` handler, then compiled **only the test project** using `--no-dependencies -p:BuildProjectReferences=false`. Final test compilation: **PASS, zero warnings/errors**. Referenced production projects were not rebuilt after that test-only correction. No runtime artifacts were replaced.

```powershell
dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/product-improvement-009/step02a/artifacts --ignore-failed-sources -p:NuGetAudit=false
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/product-improvement-009/step02a/artifacts
# After correcting only the test's built-in handler type reference:
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --no-dependencies -p:BuildProjectReferences=false --artifacts-path .tmp/product-improvement-009/step02a/artifacts
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-009/step02a/artifacts --filter 'FullyQualifiedName~ClinicBrandingDocumentAuthorizationTests|FullyQualifiedName~ClinicBrandingTests|FullyQualifiedName~ConsultationFinalPdfTests.FinalPdfReadAuditsCorrectActionBeforeReturningBytes|FullyQualifiedName~ConsultationFinalPdfTests.AuditFailurePreventsPdfDisclosureAndDisposesStream|FullyQualifiedName~ConsultationFinalPdfTests.EditPermissionDoesNotAuthorizeSigning|FullyQualifiedName~ConsultationSigningTests.FinalReadUsesStoredBytesWithoutRenderingAndRejectsWrongPatientOrTenantKey' --logger 'trx;LogFileName=branding-document-authorization.trx' --results-directory .tmp/product-improvement-009/step02a/results
```

Cached packages were reused with NuGet vulnerability-feed checks disabled for this offline restore only. No package/version/dependency or TypeScript change was introduced. Build outputs remain under `.tmp/product-improvement-009/step02a/artifacts`. Final scoped diff/whitespace checks passed.

### Readiness and remaining checks

**No unresolved local branding-read permission blocker remains for Step 02B.** No new SQL permission requirement was identified. Under the existing architecture, referral generation continues to need Patients.View; Documents-only consultation operations use the dedicated server-side path instead of gaining that broader permission.

Step 02B still requires separate instruction to integrate both document generators, place the new read after ownership/state/operation checks, preserve historical bytes and clinical/signature/recipient/audit behavior, and produce/test the required PDF samples and pagination. Step 02A generated no PDFs and makes no visual-layout claim. Live clinical-only reader and cross-tenant checks from Step 01B remain runtime-deferred; deployment/runtime validation of this local correction has not occurred.

**SATISFIED — LOCALLY VERIFIED. STOPPED before PDF integration, deployment/restarts, database/permission changes, commit/push or Step 03 work.**

## Step 02B — Referral and Consultation PDF Integration

2026-10-31 (America/Toronto). **SATISFIED — LOCALLY VERIFIED.** Both newly generated document types now use the shared clinic header and Step 02A operation-specific branding reads. This is local implementation/verification only; the running development deployment has not been updated.

Branch remains `main`. Inspected Git status once and preserved the pre-existing uncommitted Step 02A service/test changes and report. No branch or other Git state change was made. Step 01/01B history above and in the separate Step 01 report remains unchanged.

### Integration and exact code paths

**Shared layout:** added optional `ClinicalPrintContext.Branding`. `ClinicalPrintLayoutRenderer.Render` uses `ClinicPrintHeaderRenderer.Render` and its existing CSS only when that property is supplied; otherwise its original clinic identity markup/CSS is retained. The branded header is a sibling before the existing patient/record context header, avoiding nested headers. Patient metadata, clinical body, authorship/signature footer, Letter margins and existing pagination rules remain in place. `ClinicPrintHeaderRenderer` itself was not changed, duplicated or replaced.

**Referrals:** `PatientReferralService.BuildArtifactAsync`, used by Draft `PreviewLetterAsync` and the original `MarkSentAsync` generation/persistence boundary, requests `ReferralGeneration` branding after authoritative patient/provider lookup, selected clinical access/composition and selection version validation. It attaches the returned model to the print context. Removed only the redundant body `<h1>Referral Letter</h1>` because the shared header now provides that title. All date/draft labels, patient/recipient/referrer information, clinical selections, narrative, report references, appendix assembly and snapshot/hash/storage logic remain intact. Original imported report PDF pages remain unstyled and preserved by the existing assembler.

**Consultations:** `ClinicalPdfPreviewService.RenderConsultationFinalAsync` requests `ConsultationSigning` branding after template provenance/data validation and authoritative patient/signer/recipient context construction. The existing signing repository callback, artifact creation and finalization logic were not changed. `PreviewPatientDocumentAsync` identifies the tenant template through existing `GetTemplateByUidAsync` metadata and the established type code `CONSULTATION_REPORT`; only that document type opts into `ConsultationPreview` branding. Generic documents retain their original layout. Encounter preview/signed-rendering methods do not opt in and were not changed.

The new service dependencies are supplied by existing scoped DI registrations. A missing branding dependency fails eligible generation rather than silently discarding branding. A genuinely absent logo returns the shared text-only header. Authorization, tenant, database and other resolution errors propagate under existing error conventions; none is converted into a missing-logo result.

### Authorization, tenant and lifecycle safeguards

Existing API document-operation authorization runs before the generation service. Step 02A independently rechecks the appropriate operation before reading current-tenant branding:

- Consultation preview: Documents.View **and** Documents.Manage.
- Consultation signing: Documents.View **and** Documents.Sign.
- Referral generation: Patients.View **and** Referrals.View **and** Referrals.Manage.

The generator selects the operation and document title in server code. Clients receive no tenant/branding/operation selector. Existing trusted tenant context, subject resolution and tenant-verified database repository remain authoritative; no SQL capability, role, permission or membership was added. General branding reads and ClinicSettings.Manage administration are unchanged. Documents.View alone still cannot sign; preview rights cannot substitute for signing rights.

Draft previews still do not persist. Sent referral reads still use `OpenArtifactAsync`; signed consultation reads still use `ConsultationSigningService.OpenFinalPdfAsync`. Neither historical retrieval method was changed or turned into generation. Original state/version, patient ownership, tenant-key checks, signer/recipient snapshots, trusted signing timestamps, signing transitions and read-audit gates remain intact. Branding updates add no regeneration trigger.

### Focused tests and historical integrity

**112 distinct checks PASS; zero remaining failures or skipped cases.** Coverage comprises 14 new integration/native-PDF cases and 98 directly affected regressions. Only five relevant Step 02A cases were selected, not the complete 43-test suite. No full solution, Auth, certification, Scheduling, SQL or persistent clinical-data test suite ran.

| Group | Distinct checks | Result / coverage |
| --- | ---: | --- |
| BrandedClinicalPdfTests | 14 | Actual PNG-bearing and text-only referral/consultation PDFs; Documents-only consultation preview/signing; correct clinic/clinical/recipient/signer text; no duplicate header/title; missing optional contacts; long-name/address layout; complete multipage content; operation denials before branding/PDF; generic/wrong document behavior; resolver failure preventing referral commit; exact stored-byte preservation after branding changes. |
| ReferralLetterCompositionTests | 18 | Existing patient/demographic, recipient/referrer, date/draft, narrative escaping, alternative contacts and historical-read behavior. |
| ReferralFinalizationTests | 19 | Existing concurrency, selected-source/audit failures, artifact hash/persistence/state transitions, no artifactless sends and frozen historical output. |
| ReferralReportContentTests | 35 | Selected source completeness, original PDF appendices/page order, preserved stored output, restricted/missing sources, patient/tenant storage boundaries and failure behavior. |
| ConsultationSigningTests | 13 | Existing locked snapshots, signing/persistence order, stale/failed signing, historical stored reads, wrong patient/tenant and clinical status protections. |
| ConsultationFinalPdfTests | 5 | Saved signer/recipient/time output, exact final download bytes, original read-audit actions, audit failure blocking disclosure and signing authorization. |
| ClinicalPdfPreviewTests — selected | 3 | Existing unbranded layout, patient metadata and signed encounter/footer rendering retained. |
| Step 02A — selected | 5 | Real preview/sign endpoint policy allow/deny combinations and trusted-tenant logo selection. |

Scenario mapping: R1–R5 use native referral PDF assertions plus existing composition checks; R6 uses byte comparisons and finalization/report regressions; R7 uses three new missing-referral-permission cases. C1–C5 use native consultation PDFs/draft preview and saved snapshot assertions; C6/C7 use signing/stored-byte regressions; C8 uses new operation-denial cases and real endpoint policies. S1/S2 use trusted-tenant selection and existing wrong-patient/tenant tests; S3/S4 run actual generation with Documents-only permission sets; S5/S7 retain signing/referral denials; S6 reuses unchanged Step 02A administration evidence; S8 executes existing audit success/failure tests. L1–L3 use actual multipage PDFs and glyph bounds; L4/L5 use exact-byte preserved reads after branding changes; L6 uses native text-only generation.

**Historical integrity:** new in-memory referral and consultation artifacts are frozen, clinic branding is subsequently changed, and the unchanged stored-read services return **byte-for-byte identical arrays** without another render or branding read. Referral SHA256 is checked against stored artifact metadata. Existing immutable final-PDF fixtures and wrong-owner/audit-failure cases also passed. This is fixture-based preservation verification, not a claim that persistent historical files were opened or rehashed. No existing artifact was replaced or regenerated, and the five previously missing files were untouched.

The new fixtures reuse existing referral finalization, template/runtime, branding, dispatch-proxy and Playwright/PdfPig infrastructure. All patients, clinicians, clinic identity and images are synthetic test inputs. In-memory fake finalization changes are confined to test objects; no persistent clinical write or SQL connection occurred.

### Test/build execution and corrections

Affected Application/Infrastructure/API/Web/Razor references compiled in one isolated production build pass. Initial test compilation needed a missing `Templates.Variables` import and test assertion-style corrections; fixed these and compiled only the test project. The first native integration run then reported **11 PASS / 3 FAIL**, all three caused by a fixture returning `DocumentTemplateListItemResponse` where the repository contract returns `DocumentTemplateDetailsResponse`. Corrected that test-only return type, compiled only tests again and reran the affected methods: **4 PASS** (three failing cases plus the second case in the same theory). The other 11 integration outcomes remained valid. All **98** affected regressions passed on their first run. Original receipts were preserved; latest outcomes give **14 + 98 = 112 distinct PASS**.

No production rendering change was required after the initial build, no referenced project was rebuilt for the fixture corrections, and final test compilation had **zero warnings/errors**. No new dependency, package version, renderer library or TypeScript asset was introduced. Cached packages were restored with vulnerability-feed checks disabled for that offline restore only.

```powershell
dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/product-improvement-009/step02/artifacts --ignore-failed-sources -p:NuGetAudit=false
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/product-improvement-009/step02/artifacts
# Test-only compilations after the import/assertion and fixture-type corrections:
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --no-dependencies -p:BuildProjectReferences=false --artifacts-path .tmp/product-improvement-009/step02/artifacts
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-009/step02/artifacts --filter 'FullyQualifiedName~BrandedClinicalPdfTests' --logger 'trx;LogFileName=branded-clinical-pdfs.trx' --results-directory .tmp/product-improvement-009/step02/results
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-009/step02/artifacts --filter 'FullyQualifiedName~BrandedClinicalPdfTests.DraftConsultationPreview|FullyQualifiedName~BrandedClinicalPdfTests.WrongDocumentAndGeneric|FullyQualifiedName~BrandedClinicalPdfTests.UnauthorizedConsultationOperation' --logger 'trx;LogFileName=branded-clinical-pdfs-corrected.trx' --results-directory .tmp/product-improvement-009/step02/results
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-009/step02/artifacts --filter 'FullyQualifiedName~ReferralLetterCompositionTests|FullyQualifiedName~ReferralFinalizationTests|FullyQualifiedName~ReferralReportContentTests|FullyQualifiedName~ConsultationSigningTests|FullyQualifiedName~ConsultationFinalPdfTests|FullyQualifiedName~ClinicalPdfPreviewTests.PrintLayout_|FullyQualifiedName~ClinicalPdfPreviewTests.PatientDocumentLayout_|FullyQualifiedName~ClinicalPdfPreviewTests.SignedEncounterLayout_|FullyQualifiedName~ClinicBrandingDocumentAuthorizationTests.Existing_DocumentEndpointPolicies|FullyQualifiedName~ClinicBrandingDocumentAuthorizationTests.Positive_TrustedTenantContext' --logger 'trx;LogFileName=branded-print-regressions.trx' --results-directory .tmp/product-improvement-009/step02/results
```

### Disposable samples and visual verification

Generated through the actual referral/consultation services and existing Playwright PDF engine, under `.tmp/product-improvement-009/step02/`:

| PDF | Pages | Purpose |
| --- | ---: | --- |
| `referral-branded.pdf` | 3 | Synthetic sent-letter generation with logo/contact header and selected clinical content |
| `referral-text-only.pdf` | 2 | No logo and missing optional clinic contacts |
| `consultation-branded.pdf` | 2 | Synthetic signing-context output with logo, frozen recipients/signer/time |
| `consultation-text-only.pdf` | 2 | No logo and missing optional clinic contacts |
| `referral-multipage.pdf` | 6 | Long clinic name/address and 150 numbered clinical narrative lines |
| `consultation-multipage.pdf` | 5 | Same stress cases, complete findings/assessment/recommendations and final signature |

Each sample has a companion HTML file for diagnosis. All **20 PDF pages** were rasterized with the already available **Windows.Data.Pdf** engine, using ignored `render-pdfs.ps1`, to `{sample}-page-{number}.png`; no visual-testing package was installed or added to the application.

Automated PDF checks verify actual embedded image count/proportional bounds, clinic/title/clinical/snapshot text, all 150 numbered lines in each stress document, glyph positions inside page margins and the consultation signature on the final page. This is not HTML-only proof.

Visually inspected rendered PDF images in this session: page 1 of all four ordinary samples; page 2 of the branded consultation; page 1 and last page of both multipage samples. Checked logo proportions, clinic/title spacing, readable contacts, long-name wrapping, absence of overlap/clipping, continuation and signature/footer placement. No defect was found in inspected images. Automated completeness/bounds checks cover the multipage documents; not every intermediate raster page was visually reviewed, and this is not owner/live-browser acceptance. Existing first-page-only letterhead, body pagination and footer behavior were preserved; no full document redesign or page-numbering change was made.

### Files changed in Step 02B

- `src/MicroEMR.Application/ClinicalOutput/ClinicalPrintLayoutRenderer.cs` — opt-in header model/rendering; original default path retained.
- `src/MicroEMR.Application/ClinicalOutput/ClinicalPdfPreviewService.cs` — operation-specific consultation preview/final generation branding.
- `src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs` — referral generation branding and redundant presentation-only title removal.
- `tests/MicroEMR.Api.Tests/BrandedClinicalPdfTests.cs` — new integration/native-PDF/sample coverage and shared fixture helpers.
- `tests/MicroEMR.Api.Tests/ReferralLetterCompositionTests.cs` — existing fixture receives authorized branding; patient-context assertion accommodates the new preceding clinic header.
- `tests/MicroEMR.Api.Tests/ReferralFinalizationTests.cs` — existing fixture supports branding/native renderer/synthetic long narrative while retaining original lifecycle checks.
- `tests/MicroEMR.Api.Tests/ConsultationFinalPdfTests.cs` — existing saved-snapshot rendering fixture receives authorized branding.
- `docs/product-readiness/009-step02-branded-referral-consultation-reports.md` — this appended implementation/verification record.

Existing Step 02A source/test/report edits were preserved. No Step 01 deployment evidence, migration, stored procedure, SQL permission, signing/storage/read-audit service, prescription/encounter generator, Scheduling code or authentication configuration was changed. Only disposable local test artifacts were written. Scoped final diff and whitespace checks passed.

### Remaining runtime acceptance and deployment prerequisites

**No critical local blocker or remaining failed test.** Future owner-authorized rollout must deploy compatible Step 02A/02B Application/API/Web dependencies together and retain the existing 0104/read permissions and Chromium installation. No new migration, SQL grant, schema/configuration change or client asset is required. This task did not repeat deployment preflight, restart services or replace running artifacts.

After separate deployment authorization, verify actual Documents-only preview/signing, referral permissions, correct tenant branding/logo/no-logo output, real long/multipage examples, existing read audits and immutable historical downloads. Cross-tenant and clinical-only runtime qualifications from Step 01B remain runtime-deferred until suitable approved fixtures are exercised. The five existing missing artifact files remain outside this task. PDF samples are available for owner review now; no persistent artifact restoration or regeneration is needed for this local verification.

**SATISFIED — LOCALLY VERIFIED. STOPPED after implementation, focused verification, samples and documentation. No deployment, service restart, database operation, Git state change/commit/push or Step 03 work occurred.**

## Step 02C — Authorized Development Deployment and Runtime Verification

2026-11-01 (America/Toronto). The owner authorized **local development deployment and runtime verification**. **DEPLOYED — PARTIALLY RUNTIME VERIFIED.** This section supersedes the preceding pending-deployment status. It does not promote the deferred identity, tenant or successful persisted-finalization scenarios to runtime PASS.

### Preflight, deployment and recovery

- Verified the existing active `local-dev-fresh` catalog assignment, tenant `f94c6ecd-4417-483e-8a94-0507cf8384c2`, and actual development database identity `MicroEMR_LocalDev_Fresh`. Existing migration 0104 ledger/hash, schema, constraints and both procedure definitions match source. Runtime logo Get and administrative Save EXECUTE access are already present. No migration, grant, role or configuration change was needed or made.
- Today's running binaries differed from the October 31 rollout hashes. Preserved today's actual **14** DLL/PDB/static-asset metadata files, with verified SHA256 backups and a copy/restart rollback script. Did not overwrite yesterday's rollback evidence or assume its process identities remained current.
- Matched **747 current source-file checksums** to the tested Step 02 portable PDBs before deployment, and repeated the check at closure. Reused that tested build; no rebuild, package installation or client-asset regeneration was needed.
- Deployed compatible API/Web and Application/Infrastructure dependencies at **16:56:12 UTC / 11:56:12 America/Toronto**. Restarted only API/Web. Auth was not restarted. The new API/Web PIDs are **6844 / 30316**. Verified loaded managed-module paths and hashes, all 14 deployed and 14 backup hashes, Development environment/content roots, and preserved configuration and Scheduling assets. Startup stderr was empty. API health returned **200**; unauthenticated Web Clinic Settings returned **302**.
- Rollback is prepared against today's pre-deployment binaries; **rollback was not performed**. No database rollback is required for this rollout.

| Loaded component | SHA256 |
| --- | --- |
| API | `E005406427FBB82C2F143A670B189FC0C38447A2C4900F01086C0AA9BB80D3B3` |
| Web | `159230BF900B465541EB620A9648FA83DE5D12842C1DB3BE6AFA06E0EFF9F5FB` |
| Application, both hosts | `1F8ACA1412C735FBE4755888DB162D95F1099D1A0C904D37FCD7C38ADB89EA69` |
| Infrastructure, both hosts | `4FA5FEF9C94E1852BD62BFC23AE6CBEBEEFDD15CD2A8F6E60F763B8BF68378C3` |

### Runtime results

The owner signed in normally through the isolated Chrome acceptance session as the existing development administrator. Verified the authenticated tenant and identity before operating. Used existing synthetic patient, consultation, referral and provider fixtures; no clinical fixture, user or membership was created. Browser operations used existing Web/API authorization, antiforgery and concurrency handling.

| Scenario | Result and evidence |
| --- | --- |
| Consultation draft, text-only | **PASS** — actual authenticated Web/API preview returned PDF, 2 pages, clinic/title/patient/clinical text present and no embedded image. |
| Referral draft, text-only | **PASS** — actual draft-letter endpoint returned PDF, 3 pages, clinic/title/patient/referrer/recipient/clinical content retained and no embedded image. |
| Consultation/referral with logo | **PASS** — temporarily uploaded the existing disposable `DEV TEST A` PNG through Clinic Settings. Both deployed generators returned PDFs with one embedded logo, correct clinic/title and retained content. |
| Multipage consultation | **PASS** — submitted 150 numbered synthetic finding lines to preview only; actual deployed output has 5 pages and every `RUNTIME-001` through `RUNTIME-150` line. Submitted values were not saved to the existing draft. |
| Historical referral | **PASS** — stored referral downloaded before the temporary logo, with the logo installed, and after removal. Every download has SHA256 `4C58D85E11D43DF20632ED3CE5DC96BD8E138CE0652631FC258E399F23DAA16C`, matching the pre-existing stored bytes. |
| Wrong-patient referral download | **PASS** — a different existing patient UID with the stored referral UID returned **404**. This is patient ownership verification within the approved tenant, not a second-tenant test. |
| Stale consultation signing | **PASS** — submitted a deliberately stale all-zero 8-byte row version through the real signing action. API returned concurrency conflict; Web reloaded with the expected conflict message. No signing, artifact or clinical state change occurred. This is a negative signing check, not successful finalization acceptance. |
| Original logo state | **PASS** — removed the temporary logo through the normal UI, verified logo endpoint **404** and final stored no-logo state. Logo row version and audit history correctly advance; they were not reverted. Clinic contacts were not edited. |

All **15 pages** of the five new preview PDFs were rasterized with the existing Windows.Data.Pdf engine. Inspected page 1 of both branded and both text-only outputs, plus the last multipage consultation page. Header proportions, contacts, context, narrative continuation and footer were readable in those images. PdfPig checks on all pages verified clinic/title text, expected image counts, glyphs inside margins and complete numbered narrative. Not every raster page received visual inspection. These outputs are draft previews; they do not establish live signed-output signature placement.

### Focused tests, audit and integrity

Reran the focused native-PDF, branding document-authorization, referral composition/finalization/report-content, consultation signing and preserved-final-PDF regressions against the source-matched build: **133 PASS, 0 FAIL, 0 SKIP**. Receipt: `.tmp/product-improvement-009/step02c/results/deployment-regressions.trx`.

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-009/step02/artifacts --filter 'FullyQualifiedName~BrandedClinicalPdfTests|FullyQualifiedName~ClinicBrandingDocumentAuthorizationTests|FullyQualifiedName~ReferralLetterCompositionTests|FullyQualifiedName~ReferralFinalizationTests|FullyQualifiedName~ReferralReportContentTests|FullyQualifiedName~ConsultationSigningTests|FullyQualifiedName~ConsultationFinalPdfTests' --logger 'trx;LogFileName=deployment-regressions.trx' --results-directory .tmp/product-improvement-009/step02c/results
```

Final read-only integrity comparisons match the baseline for **12 hash groups**: clinic profile, patient documents, encounters, referrals, artifact metadata, stored referral PDF bytes, physical historical artifact availability/bytes, tenant memberships, appointments, appointment history, resources and saved scheduling preferences. No persistent clinical record or artifact was created, changed, signed, sent or regenerated.

Audit evidence contains **14 expected ClinicalRead events** and the **two intended branding Update/Remove events**. There are no unexpected clinical-write audit events. The clinical audit aggregate changes because normal authorized reads are logged; this is expected and is not represented as an unchanged audit hash. The five previously missing historical filesystem artifacts remain **5 → 5**, untouched. Their unavailable bytes remain unverified.

Initial diagnostic/helper failures were corrected without product patches: sandbox network denial was followed by authorized read-only development access; fixture SQL column/parameter and multi-row JSON handling were corrected; command serialization and Node-only local certificate trust issues were resolved by using the authenticated browser's fetch; helper syntax/array and managed-module filtering issues were corrected. Passing receipts are retained separately from failed helper receipts. None of these establishes a product regression or required changing application authorization or TLS configuration.

### Remaining acceptance and files

- **Documents-only authenticated preview/signing remains pending.** The current session uses an administrator and cannot establish this permission boundary in live operation. Asked whether an existing suitable development account is available; none has been supplied at this checkpoint. The 133-test run includes operation-specific positive/negative permission coverage. No account, role or permission was modified to manufacture runtime coverage.
- **Second-tenant authenticated branding isolation remains pending.** No second-tenant session or fixture was exercised, migrated or modified. Prior automated isolation evidence remains applicable but is not promoted to a live PASS.
- **Successful persisted consultation signing and referral sending remain unexercised in this runtime pass.** Existing drafts were retained. Fresh automated tests cover generation, finalization, immutable reads and authorization; runtime evidence here covers preview generation and stale signing rejection. A dedicated approved finalization fixture/session is still needed for full runtime acceptance.
- **Five existing missing historical files remain outside repair scope.** No replacement or regeneration was attempted.

The only repository document edited during this deployment task is this report. Existing Step 02A/02B production/test changes were preserved; no production source, test source, migration or generated client asset was changed. New ignored helpers, rollback copies, logs, JSON/TRX receipts, PDFs and raster images are under `.tmp/product-improvement-009/step02c/`, with read-only SQL helpers in `.tmp/status-acceptance-preparation/009-step02c-*.fsx`. `closure.json`, `versions.json`, `health.json`, `pdf-verification.json`, runtime scenario receipts and baseline/final integrity/audit receipts provide the acceptance evidence. Final diff/whitespace checks passed.

**DEPLOYED — PARTIALLY RUNTIME VERIFIED.** Local services remain on the verified Step 02 build. No production access, schema/grant change, clinical finalization, historical regeneration, Git commit/push, remote Git operation or Step 03 work occurred.

### Documents-only account qualification follow-up

2026-11-01 (America/Toronto). The owner offered an existing account and signed into a new isolated acceptance session, then confirmed the intended username is `admin@microemr.local`. The authenticated tenant matches `local-dev-fresh`. Read-only live probes returned **200** from `/ClinicConfiguration` and `/ClinicBranding/Preview`; `/ClinicBranding/Logo` returned the expected **404** for the restored no-logo state. These results establish that the account has clinic-management/general-branding access, so it does **not** qualify for the Documents-only boundary test. No effective-permission list was fetched and no narrower permission set is inferred.

**Documents-only runtime acceptance remains pending a suitable existing account.** No account, role, access profile or permission was created or changed, and no signing, clinical write, logo mutation, deployment or restart was performed during this follow-up. The initial probe ran before sign-in redirect completion and was retried successfully after authentication. Evidence is retained in `.tmp/product-improvement-009/step02c/clinical-account/`; the isolated acceptance browser was closed. The prior 133-test result, deployed build and remaining acceptance qualifications stand unchanged.
