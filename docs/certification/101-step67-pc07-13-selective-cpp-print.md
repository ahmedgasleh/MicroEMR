# Step 67 — PC07.13 Single-Operation Category-Selective CPP Printing

Date: 2026-10-08. Resume [Step 66](100-step66-deferred-verification-next-mandatory-gap.md). Focused implementation only.

## A. Feature branch

Created and switched to `feature/step-67-pc07-13-selective-cpp-print` before implementation changes. The branch remains available for the user's manual verification.

## B. Initial Git state

Started on `main` with a clean working tree. There were no existing uncommitted changes to move or overwrite. No stash, reset, rebase, discard, commit, merge, push or branch deletion was performed. Historical certification reports remain unchanged.

## C. Authoritative requirement

Reuse the established OntarioMD Primary Care Baseline Version 1.7 Final mapping for **PC07.13, pp. 29–30**, recorded in Step 66 and its Step 45 reference: one operation to print selected CPP categories; clinician/clinic letterhead; patient **name, HCN, address and phone**; print date; and actual **x/y** page numbering. Individual-record removal and alternative sorting choices remain optional.

This describes the existing authoritative mapping, not a newly retrieved verbatim quotation from the baseline PDF. No new requirement or certification-wide analysis is introduced.

## D. Existing infrastructure reused

Reuse Step 61's `CppDisplayCatalog` and CPP projections, existing patient/source services and tenant repositories, active provider records, clinic configuration, clinical actor resolution, permission constants, chart-read audit, shared clinical print layout and existing `IPdfRenderer`. The existing Web API token/refresh configuration remains in use. PdfPig was already installed; no package or document engine is added.

## E. Category selection and one operation

The patient CPP has one **Print CPP** panel with the established eleven category choices and an authoritative clinician selector. One **Print selected categories** submission produces one PDF containing all selected sections. The PDF opens in a new tab for the viewer's Print command; categories never require separate requests or separate outputs.

The server rejects empty, unknown, incorrectly cased or duplicate category identifiers and missing/invalid clinicians. Section order follows the shared catalog. Print choices are independent of saved Step 61 category/field visibility preferences; the form sits outside the display-preference card container, and print composition never reads those preferences. No parallel category catalog is maintained.

## F. Complete selected content and existing semantics

The print read reuses each CPP source, eligibility filter and projection, removing screen list caps for selected list categories. It prints every clinical field in each established CPP projection, excluding internal UIDs. Description text is HTML encoded, retains line breaks and has no substring/length truncation. It does not print the screen DOM or a five-record summary.

| Category | Eligible CPP content printed |
| --- | --- |
| Problems | All active problem projection rows: name, status and onset date. |
| Allergies | All active allergy projection rows, including reaction/severity; explicitly documented No Known Allergies is preserved. |
| Medications | All active medication projection rows, including strength, route, frequency and start date. |
| Prescriptions | All finalized prescription CPP rows, including full directions and prescribed date. |
| Results | All current result CPP rows, including type, date, value/unit, abnormality, status and established provenance. |
| Vitals | The latest vital set with all established CPP measurements; this category explicitly means Latest Vitals. |
| Immunizations | All completed immunization CPP rows, including administration date and source type. |
| Encounters | The latest signed encounter CPP projection, including provider/reason; this category explicitly means Latest Signed Encounter. |
| Referrals | All eligible Draft/Sent/ResponseReceived referral CPP context rows. |
| Documents | All existing document CPP context rows returned by the patient-scoped source. |
| History | All active medical/surgical history entries, with full description, history type and relevant date. |

Source date ordering is reused; history uses relevant date descending and UID for ties. Latest-only category semantics remain intact. The print retains source clinical values and dates, including nulls represented as `Not recorded`. Empty categories explicitly state `No eligible records documented`; empty allergies do not falsely become No Known Allergies. A failed or restricted selected category blocks output rather than producing an apparently complete PDF.

Completeness here concerns existing CPP fields and eligible entries. Referral/document/encounter sections remain their established CPP context projections; this step does not print underlying document attachments, referral report bodies or full encounter histories. Those separate certification capabilities and missing CPP data categories are not claimed closed by PC07.13.

## G. Letterhead and authoritative sources

The existing clinic configuration supplies legal name (fallback clinic name), address, phone, fax and email where recorded. The selected active, tenant-scoped provider supplies clinician display name, provider type and specialty where recorded. The server independently resolves/validates the supplied clinician UID. An absent clinic name or unavailable clinician prevents printing with an actionable message. Missing configuration is never replaced with fabricated clinic/provider data.

The selected clinician is identified as the letterhead clinician; this read operation does not claim they signed the CPP. The authenticated actor is resolved separately for permission/audit purposes.

## H. Patient identification/contact

The existing tenant-scoped patient details source supplies full name, HCN/version, postal address and primary phone. The retrieved patient UID must match the requested patient. Address/phone are optional additions to the shared layout and leave other print callers' default output unchanged. CPP output suppresses DOB, chart number and internal UIDs to avoid extra identifying fields. Missing HCN/address/phone is shown as `Not recorded`; an orphan HCN version is suppressed.

## I. Print date and actual pagination

The print date is the current server instant formatted by the shared layout in the configured clinic time zone, rather than a historical clinical record date. The existing renderer generates the complete Letter-size PDF once with its existing 0.65-inch margins. A small infrastructure adapter copies the generated pages using the already installed PdfPig library and adds `1/y` through `y/y` based on the actual rendered page count.

Nine-point page labels occupy the reserved bottom margin at y=22 points. The adapter does not count categories or guess page totals. Content uses wrapping, preserved line breaks and splittable records. Real HTML pagination, page breaks and printed readability remain manual evidence; the synthetic PDF tests establish actual page counts, preserved page text and footer coordinates, not browser clipping behavior.

## J. Security and tenant isolation

Web and API endpoints require authentication and Patients.View; the Web POST additionally requires antiforgery. The API uses the existing sensitive PatientChartView capability. The Application validates category IDs, patient/clinician GUIDs, active clinician identity and the resolved clinical actor. Selected Results/Encounters/Referrals/Documents also require their existing source-view permissions before clinical content reads. No unauthorized category is silently omitted.

Source access continues through the existing tenant-scoped repositories/services. The print path additionally rejects mismatched patient/source ownership before producing output. Client-supplied names, HCN, address, provider text and clinical content are never accepted. API and Web responses use NoStore. Options disclose catalog/provider selection metadata only, not patient clinical records.

Focused tests use tenant-store doubles and mismatched IDs to establish rejection; live multi-tenant SQL and role behavior remain manual deployment evidence. No new global/unscoped SQL access is introduced.

## K. Audit behavior

The full CPP read records the existing `PatientChartOpened` event once, with patient/correlation context and the existing resolved actor/tenant infrastructure, before selected clinical-source reads. Audit failure prevents output. This follows CPP's established sensitive-read convention; options alone do not read patient clinical data.

No separate print/download event or clinical mutation event is invented. The existing read audit proves a patient CPP read attempt, not a dedicated successful-printer/download event or an immutable artifact snapshot. Source services retain their existing audit behavior. Printing performs no clinical create/update/delete and does not persist new display preferences.

## L. Files changed

| Area | Files |
| --- | --- |
| Application CPP | `src/MicroEMR.Application/PatientCpp/CppPrintModels.cs`, `CppPrintService.cs`, `PatientCppService.Print.cs`, and modified `PatientCppService.cs` |
| Shared output/contracts | `src/MicroEMR.Application/ClinicalOutput/IPdfPageNumberer.cs`, modified `ClinicalPrintLayoutRenderer.cs` |
| Infrastructure output | `src/MicroEMR.Infrastructure/ClinicalOutput/PdfPageNumberer.cs` |
| API | `src/MicroEMR.Api/Controllers/CppPrintController.cs` |
| Web transport/controller | `src/MicroEMR.Web/Services/Patients/CppPrintApiClient.cs`, `src/MicroEMR.Web/Controllers/CppPrintController.cs` |
| UI | `src/MicroEMR.Web/Views/Patients/_CppPrint.cshtml`, modified `Details.cshtml`, `src/MicroEMR.Web/ClientApp/patients/cpp-print.ts` |
| Compiled UI | `src/MicroEMR.Web/wwwroot/dist/patients/cpp-print.js` and `.js.map` |
| Registration | Application/Infrastructure `DependencyInjection.cs` and Web `Program.cs` |
| Focused tests | `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs`, `tests/pc07-selective-cpp-print.test.cjs` |
| Evidence | This report; ignored local build/TRX outputs under `artifacts/step67` |

## M. Schema/migrations

**None.** No schema change, stored-procedure change, migration, EF migration, clinical storage change or historical migration edit. Existing read-audit persistence remains in use. No new dependency.

## N. Focused test results

The Step 67 .NET filter `FullyQualifiedName~CppSelectivePrintTests` passed **28/28**, zero failed/skipped. Evidence: `artifacts/step67/step67-focused.trx`. It covers one/multiple selected categories, exclusions, full projection fields and a 12-row list beyond the screen cap, a 600-line history with encoding, required header/date, invalid selection, unavailable/restricted content, clinician/patient/source ownership, resolved actor, audit refusal/once, read-only source calls, empty/explicitly-none states, authenticated routes/antiforgery metadata, controller output/failure mapping, actual 1/1, 3/3 and 12/12 synthetic PDF numbering and malformed-PDF rejection.

`node tests/pc07-selective-cpp-print.test.cjs` passed: lazy option loading, safe clinician labels, required selections, one/multiple-category submission, unavailable options and retry. This exercises the compiled module with a DOM fixture; native browser POST binding, token refresh, PDF viewing and physical printing remain manual evidence.

The final focused filter was repeated after preserving Latest Signed Encounter semantics and cancellation propagation. Runs overlap and are not summed as additional tests. No unrelated CPP, API/Auth, referral, scheduling, full-solution, certification-wide or Playwright tests were run.

## O. Build and TypeScript checks

Affected Application, Infrastructure, API and Web projects compiled through the focused test project's dependency build using isolated `artifacts/step67/build`, `--no-restore`, `-m:1`, `-nr:false` and `UseArtifactsOutput=true`. Core and DatabaseTool compiled as existing referenced dependencies; no tests from those areas ran. The initial near-completion build passed; a final incremental build accompanies the focused post-review rerun.

The new TypeScript file compiled successfully with the installed compiler, strict mode, ES2020 modules, Bundler module resolution, skipLibCheck and source maps, generating only its own distributed JS/map.

Restore succeeded from available packages with `--ignore-failed-sources --disable-parallel`. **NU1900** warns that the NuGet feed was unreachable for vulnerability metadata. This limits vulnerability-feed verification, not the successful compilation/test result. Final diff review and `git diff --check` passed; all 15 new files also passed a trailing-whitespace check. The final working-tree scope is six modified files and fifteen new files, including this report.

## P. Bounded manual verification

1. Use an authorized clinical test account, an active configured provider, complete clinic configuration and a patient with HCN/address/phone. Capture CPP source values before printing.
2. Open **Print CPP**, select one category and clinician, submit once. Confirm one PDF, complete eligible rows/fields and correct empty/No Known Allergies distinctions where applicable.
3. Select multiple categories, including a list with more than five rows and long history/directions. Submit once; compare all selected contents, catalog order and absence of unselected content.
4. Verify clinic/clinician letterhead, designation/specialty where configured, correct patient name/HCN/address/phone and today's clinic-local print date. Missing values must remain honest.
5. Check a one-page output shows `1/1`. Produce several pages, including one category spanning pages; inspect every `x/y`, total, ending content, wrapping, page breaks, footer separation and readability. Use the viewer's Print command and inspect actual printed output.
6. Hide selected categories/fields with Customize CPP, save, then explicitly select them for print. Confirm hidden screen preferences do not remove print content.
7. Where authorized fixtures permit, check a restricted role, a source-specific permission restriction and other-patient/other-tenant IDs. Confirm rejection without data disclosure. Inspect the patient read audit for the correct actor/tenant/patient; an audit-write failure must prevent output.
8. Compare clinical records/statuses/dates and preferences before/after printing: unchanged. Audit reads may add audit records. Retain PDF/screenshots and observed results for certification closure.

No broad regression checklist is required. Browser/renderer readiness must already be available in the deployed environment. Missing clinical/configuration data, real pagination and runtime permission/tenant evidence need user verification before closure.

## Q. Certification status and remaining qualifications

**PC07.13 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** No SATISFIED — VERIFIED claim is made. Preserve **19 outstanding mandatory requirement IDs across 7 original packages** until the required runtime acceptance is confirmed.

Retain **PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING**, with runtime verification deferred at the user's request. Retain **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION**. Neither is reopened or closed by this CPP change. Existing missing CPP clinical categories and unrelated mandatory gaps retain their status.

## R. Report location

`docs/certification/101-step67-pc07-13-selective-cpp-print.md`. Historical reports are preserved.

## S. Git confirmation

The Step 67 feature branch remains active with reviewable, uncommitted implementation/test/documentation changes. No automatic commit, merge, push or branch deletion. The user controls verification, commit and merge.

## T. Resource and stop-condition compliance

Inspection was limited to established Step 66/61 evidence, CPP reads/catalog, affected print/patient/provider/configuration/auth/audit integration and directly relevant tests. No repository-wide scan, certification-wide reanalysis, unrelated implementation or broad test suite. Existing changes were preserved. **Stop after Step 67; Step 68 was not begun.**
