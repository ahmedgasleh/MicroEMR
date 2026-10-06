# Step 47B.2: referral clinical-selection UI and Draft preview

Date: 2026-10-06. Status: **PARTIAL — STEP 47A + 47B IMPLEMENTED; NEEDS MANUAL/RUNTIME VERIFICATION; FINAL SELECTED-CONTENT PRESERVATION PENDING STEP 47C**. PC10.01 is not SATISFIED.

Basis: Primary Care Baseline 1.7 PC10.01 and the user-authorized Step 47B.2 request, using the stable [Step 47B.1 foundation](80-step47b1-referral-clinical-selection-foundation.md). Earlier reports remain historical.

## A–F. Selectors and persistence

The existing Add/Edit Draft modal now includes compact Clinical Data to Include controls. New Drafts default to no additional clinical selections; existing Drafts restore persisted references without recomputing choices.

| Source | UI behavior |
| --- | --- |
| CPP | Exact supported codes PROBLEMS, ALLERGIES, MEDICATIONS; clinician labels Ongoing Problems / Diagnoses, Allergies, Medications. |
| Encounters | Existing patient-scoped metadata list: date, type, provider and status. Signed entries sort first; unsigned entries remain available because the foundation did not mandate signed-only selection. No encounter bodies loaded for the selector. |
| Results | Existing patient-scoped result list, restricted to Current results; UI receives UID, name, date and status. The existing result-list contract internally supplies additional structured result fields; no new SQL/pagination or report-file retrieval was added. Only metadata crosses the selector endpoint. |
| PatientDocuments | Existing document-list and PatientReferralDocument clients supply type/status/title and linked choices. Signed Consultation Reports and external/scanned documents retain their actual type/status. Documents never enter the clinical-selection table. |

Unavailable persisted clinical references remain checked and visibly identified until explicitly removed; no automatic replacement UID. Source permission loss rejects selection loading rather than silently dropping restricted choices. If document options are unavailable/restricted, document edits are omitted and existing links retained.

## G–H. Draft preview, save stages and concurrency

Only Draft preview calls the new referral clinical-content renderer. It reads persisted references, then selected patient-owned source records through existing domain services. The Step 47A header/demographics/referrer/recipient remain intact. Selected Clinical Information follows Reason/Clinical Summary and precedes the unchanged Supporting Documents title/type list.

- Problems: active names/descriptions from the complete existing domain read, rather than the five-item CPP summary.
- Allergies: active allergen/reaction/severity; empty category says No recorded items and does not infer No known allergies.
- Medications: active name, strength/form, route, frequency and directions.
- Encounters: clinic-local source date, type, provider/status and stored SOAP/legacy notes. Structured encounters reuse the existing historical-template enrichment and TemplateInstanceRuntime snapshot renderer; plain snapshot text is encoded into the referral output. No encounter edits or SOAP rewriting.
- Results: existing date/name/type/review status, value/unit/range/abnormality, summary and available source attribution. Superseded/EnteredInError or missing references cause an explicit preview error; no substituted record.
- Documents: title/type only, preserving existing linked-document behavior. No PDF/scanned-document merge, consultation-signing change or inline document-body inclusion.

Save runs sequentially: existing referral Create/UpdateDraft, foundation ReplaceDraft clinical references, then document-link deltas through the existing Link/Unlink mechanism. Each stage uses the latest returned/refreshed parent RowVersion. The text version must match the persisted-selection version during reload; otherwise editing is disabled until reopening. Document mutations retain existing per-operation concurrency and refresh the referral version before subsequent links.

This sequence is **not one atomic transaction**. A later failure can leave earlier stages saved. The modal identifies partial completion, retains the saved referral identity/version, disables further saves and requires closing/reopening. It does not automatically retry, create another referral or overwrite stale text. After successful save, Details reloads with the current aggregate version for preview/edit/lifecycle actions.

## I. Security and audit

New API options and replace routes require Referrals.Manage in addition to the existing Referrals.View policy; Foundation Application checks remain unchanged. Options/content require Patients.View, Referrals.View and Referrals.Manage. Encounter/result options are omitted without Encounters.View/Results.View; selected sources are checked before any body read. Rendering independently checks patient ownership, Current result state and tenant-scoped source reads. Browser labels/text never supply rendered clinical content. Linked document preview additionally requires Documents.View.

Web adapters forward the authenticated bearer token and preserve forbidden/conflict responses. Selection POST uses the existing antiforgery convention. Selection mutations retain foundation audit. Options use the existing patient-chart read-audit service; CPP/result preview uses one patient-chart event per operation, and each selected encounter uses its existing EncounterViewed structured event. Existing source services do not also audit these body reads, so no duplicate source-read events are added. Audit failure prevents output. Clinical text/values are not written into selection rows or audit text.

## J–K. Files and database scope

| Files | Change |
| --- | --- |
| `src/MicroEMR.Application/PatientReferrals/ReferralClinicalContentService.cs` | Metadata option contracts, source permission/read/audit orchestration and selected Draft HTML. |
| `src/MicroEMR.Application/PatientReferrals/PatientReferralService.ClinicalOptions.cs` | Existing referral service delegates authorized patient options. |
| `src/MicroEMR.Application/PatientReferrals/IPatientReferralService.cs` | Options contract only; foundation selection methods unchanged. |
| `src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs` | Inject content renderer, enforce document-read permission for Draft preview and insert selected HTML only for Draft. |
| `src/MicroEMR.Application/DependencyInjection.cs` | Register referral content service. |
| `src/MicroEMR.Api/Controllers/PatientReferralsController.cs` and `.ClinicalSelections.cs` | Thin options/read/replace endpoints, preview permission/unavailable-source errors. |
| `src/MicroEMR.Web/Controllers/PatientReferralsController.cs` and `.ClinicalSelections.cs` | Authenticated proxies and existing document-client metadata options. |
| `src/MicroEMR.Web/Services/PatientReferrals/PatientReferralApiClient.cs` and `.ClinicalSelections.cs` | Bearer-authenticated transport of existing foundation selection contracts. |
| `src/MicroEMR.Web/Views/Patients/Details.cshtml` | Compact Draft clinical/document selection section and preview-only boundary explanation. |
| `src/MicroEMR.Web/ClientApp/patients/patient-referrals.ts` | Restore explicit choices, sequential saves, version propagation and partial-failure handling. |
| `src/MicroEMR.Web/wwwroot/dist/patients/patient-referrals.js` and `.js.map` | Only affected TypeScript compiled; matching original relative source path. |
| `tests/MicroEMR.Api.Tests/ReferralClinicalContentTests.cs` | Selected-source composition, ownership/permissions/audit and metadata tests. |
| `tests/MicroEMR.Api.Tests/ReferralClinicalSelectionTransportTests.cs` | API/Web status mapping, token/routes/references/new-version and antiforgery metadata. |
| `tests/MicroEMR.Api.Tests/ReferralLetterCompositionTests.cs` | Provide new renderer dependency and test integrated ordering/layout with unchanged sent composition. |
| `tests/pc10-referral-clinical-selection.test.cjs` | Seven compiled-script UI/save tests using a minimal DOM/API fixture. |
| This report | Result, limitations and manual path. |

**Database/schema/migration changes: NONE.** Foundation migration, selection table/procedures, repository adapter and PatientReferralDocument persistence are unchanged. A single read-only prerequisite check confirmed SelectionTablePresent=1 in MicroEMR_LocalDev_Fresh. No migration was applied or SQL data written by this step.

## L. Focused validation

Final .NET result: **31 passed, 0 failed, 0 skipped; duration 134 ms; exit code 0**. This comprises 12 selected-content tests, four transport cases and 15 letter-composition cases. The affected project and existing Core/Application/Infrastructure/API/Web/DatabaseTool references built successfully without warnings/errors reported in the successful pass.

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ReferralClinicalContentTests|FullyQualifiedName~ReferralLetterCompositionTests|FullyQualifiedName~ReferralClinicalSelectionTransportTests' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step47b2/bin/'
node tests/pc10-referral-clinical-selection.test.cjs
```

Node result: **7 passed, 0 failed, 0 skipped**, approximately 35 ms. `node --test` initially hit sandbox child-process EPERM; direct execution ran the same node:test cases in-process successfully. Development .NET checks initially passed 26 preview cases; added transport coverage caught a test's exact-type expectation for derived ConflictObjectResult, which was corrected before the final 31-case pass.

Affected TypeScript compilation passed from `src/MicroEMR.Web`:

```powershell
node node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --rootDir ClientApp --outDir wwwroot/dist --sourceMap ClientApp/patients/patient-referrals.ts
```

Git diff whitespace review passed with normal LF/CRLF notices. No comprehensive foundation persistence tests, broad API/Auth/solution/certification suites or browser regression. Tests capture HTML and simulate UI/API; actual PDF layout and live browser behavior remain manual evidence. Isolated builds do not update running application DLLs.

## M. Manual validation

Rebuild/restart the normal development application first.

1. Open patient → Referrals → create/edit Draft.
2. Open Clinical Data to Include.
3. Select supported CPP categories and a same-patient encounter; inspect provider/date/status.
4. Select a Current result if available and supporting PatientDocuments, including a signed consultation report where present.
5. Save Draft; reopen and verify the exact choices and document links persist.
6. Preview Letter; verify only selected clinical content appears after the narrative, before document titles.
7. Verify Step 47A header/demographics/referrer/recipient and draft date remain intact.
8. Remove a clinical choice/document, save and preview again; verify it is omitted.
9. Verify another patient's records cannot be selected/included and restricted sources are unavailable under an appropriately restricted account.
10. For a Draft changed in another session, verify conflict/reopen behavior; after partial failure, reopen to inspect saved stages before continuing.

Do not use Mark Sent as selected-content acceptance evidence in this step.

## N–O. Remaining certification gaps and scope confirmation

**Selected clinical information is Draft-preview-only. Existing Mark Sent/final artifact composition still omits it.** The modal states this boundary. Step 47C must define and preserve the exact selected final content; no selected-source freezing, immutable snapshots, hashing/signing or sent artifact change was introduced here.

Supporting document titles alone do not establish inline/full required specialist/external report content. No document-merging engine was added. The patient alternative-contact source gap from Step 47A remains. Additional CPP coverage beyond the three supported categories requires its own authoritative scope assessment. These limitations and full printed-output/manual evidence prevent a PC10.01 SATISFIED claim.

Step 47B.1 persistence reused unchanged; PatientReferral remains source of truth; PatientReferralDocument remains the document mechanism; no PC10.02/reminder/lifecycle changes; no broad repository scan or unrelated stable-domain recheck. Inspection was limited to referral UI/contracts/composer and directly needed source reads/permission/audit conventions. The starting tree was clean and prior manual work remains intact. Stop after Step 47B.2; Step 47C was not started.
