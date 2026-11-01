# Product Improvement 009 — Step 04B: Branded Encounter and Chart PDFs

2026-11-01 (America/Toronto). **SATISFIED — LOCALLY VERIFIED.** The three approved PDF paths now opt into tenant clinic branding. **79 distinct focused cases pass**, the affected build passes with zero warnings/errors, and nine synthetic PDFs were visually reviewed. Ready for separately authorized Step 04C development deployment and runtime acceptance. No deployment or deployed-runtime verification was performed.

Baseline: [Step 04A assessment](009-step04-encounter-chart-print-assessment.md). Existing header and operation authorization patterns come from [Step 02](009-step02-branded-referral-consultation-reports.md); [Step 03B](009-step03b-versioned-prescription-branding.md) remains unchanged.

## Implementation and exact paths

| Existing path | Bounded change | Preserved behavior |
| --- | --- | --- |
| `CppPrintService.PrintAsync` | Resolve current tenant header with title **Cumulative Patient Profile** after existing access, clinician, patient, selection and source checks; set `ClinicalPrintContext.Branding`. | Complete selected categories in catalog order, clinical projections, identifiers, timestamps, selected clinician/provider type/specialty, source audits and CPP page numbering. Existing CPP intentionally omits DOB/chart; these identifiers were not added. |
| `PatientImmunizationSummaryService.GenerateAsync` | Resolve current tenant header with title **Immunization Summary** after existing patient ownership/read audit/history checks; set `Branding`. Move the existing summary title to the shared header by removing its duplicate body H1. | **Immunization History** heading, patient identifiers, complete history, ordering/tie breakers, refusal/error distinctions, partial dates, dose, administration details, empty state, wrapping and repeating table headings. |
| `ClinicalPdfPreviewService.PreviewEncounterAsync` | Set `Branding` immediately before its existing `RenderAsync` call using the new internal `EncounterPreview` operation and existing template/type title. | Saved Open eligibility, historical template provenance, processed submitted/saved equality, patient lookup, provider/date context, field and narrative contribution attribution, authorship, clinical values and transient output. |

All three reuse the existing `ClinicPrintHeaderRenderer` through the existing layout opt-in. No shared header, layout default, PDF engine, clinical body projection, endpoint or dependency-registration change was needed. Existing scoped DI registers `ClinicBrandingService` and both summary services. The two summary constructors accept that registered dependency; generation fails explicitly if a manually constructed caller omits it. There is no unbranded fallback on resolver failure.

CPP and immunization are current-state summaries. Open previews use current clinic branding. None creates a branding snapshot or changes stored clinical data. Logos and text-only output use the same existing header implementation; optional absent contact fields remain absent. Clinic identity is emitted once, with patient/record context retained below it.

## Open eligibility, authorization and audit

Source-defined eligibility remains a case-insensitive **`Status == "Open"`** check, followed by schema-driven provenance requirements and equality of processed submitted and saved values. “Draft” and “Completed” are not substituted for Open. Unsaved values, Signed records, missing patient/template context and incorrect template provenance fail before header resolution. No new status label is introduced.

`ClinicBrandingDocumentOperation.EncounterPreview` requires **Encounters.View AND Encounters.Edit**, a trusted nonempty tenant context and an authenticated subject, following the existing document-operation pattern. It needs neither Patients.View nor ClinicSettings.Manage. The existing API class/action authorization still requires both encounter permissions before invoking the preview service. Record eligibility and tenant-scoped encounter/patient reads remain ahead of header resolution. The request carries structured values, with no client-supplied tenant or patient selector.

General branding reads, clinic-branding administration and other document operations retain their original permission requirements. There is no new public branding route. CPP and immunization retain their existing Patients.View endpoint requirements, source permissions and governed read-audit ordering.

The preview path previously had **no explicit structured-read-audit event** of its own; that behavior is preserved rather than claimed as a newly audited print operation. Focused preview fixtures restrict repository calls to reads and assert the encounter → historical version → patient → clinic sequence. CPP audit/source-denial and immunization audit-before-history/fail-closed tests remain passing. No read-audit, signing or clinical mutation endpoint was executed against a database.

## Historical signed PDF protection

`RenderSignedEncounterAsync`, shared `RenderAsync`, signing, artifact Ensure/retry/storage and final-download implementations are unchanged. Only the Open preview context receives Branding; the signed context retains its original null Branding and clinic-identity layout.

Local tests explicitly exercise original signed rendering with branding access configured to fail if attempted: it succeeds with its original layout, signer/content and contribution attribution, with zero branding reads. A separate `ClinicalArtifactService.OpenEncounterFinalPdfAsync` test returns identical synthetic preserved bytes and SHA-256 before/after current branding changes and an Open preview. Renderer/repair and artifact/storage mutation calls throw in that fixture. Preview repositories also reject mutation calls; no artifact service is part of the preview render dependency path.

These are disposable in-memory fixtures, not regeneration or download of a persistent development artifact. No existing historical PDF was opened, repaired, replaced or regenerated.

## Focused verification

Existing coverage was mapped before adding assertions: CPP selection/content/audit/ownership/page-numbering and immunization retained-history/empty/audit/endpoint authorization already had suitable fixtures. Those fixtures now supply the real branding service. New coverage is confined to `BrandedChartPdfTests`.

| Requirements | Coverage and result |
| --- | --- |
| CPP-01–02 | Native logo/text-only CPP samples; single header/identity, logo bounds and image count. PASS. |
| CPP-03–04 | Existing `OneOperationPrintsCompleteSelectedCategoriesInCatalogOrderWithRequiredContext`, `OneCategoryIncludesEveryCppFieldAndPreferencesCannotSuppressIt`, family/risk projection tests, plus native patient/clinician/clinical assertions. PASS. |
| CPP-05 | Existing label alignment/history continuation and real page-total tests; new 16-page branded CPP, bounds on every extracted word, last content marker and numbered pages. PASS. |
| CPP-06–07 | Existing permission/actor/source/audit and wrong-patient/summary/history/tenant-store denial cases. PASS. |
| IMM-01–03 | Native logo/text-only samples plus retained standalone history tests: names, dates, refusal, correction, dose, DIN/manufacturer, partial dates, patient and historical clinician. PASS. |
| IMM-04–05 | Existing empty state and 120-row HTML tests; native 14-page history with repeated table headings, final clinical markers and word bounds. PASS. |
| IMM-06–07 | Existing API/Web patient-read policy checks, API denial propagation, foreign patient/store and cross-patient clinical-row rejection. PASS. |
| ENC-01–03 | Native branded/text-only previews use exactly Encounters.View + Encounters.Edit, without patient/admin permissions. PASS. |
| ENC-04–06 | Real endpoint policy/handler evaluation, incomplete-operation denial, missing trusted subject/tenant, patient-unavailable rejection and scoped-repository unowned UID denial before branding; trusted tenant header/logo switch. PASS locally. |
| ENC-07–08 | Native saved content, tracked-value original author and revision editor, narrative author, historical version, dates/provider and long multi-contribution output. PASS. |
| ENC-09–11 | Open status/signature fields unchanged, read-only repository guards, no signing/artifact/repair dependency invocation in preview. PASS. |
| ENC-12–13 | Preserved download bytes/hash and original signed render tested explicitly as described above. PASS. |
| ENC-14 | Existing preview read sequence and absence of new mutations/audit behavior retained; summary audit regression tests pass. No new preview audit event claimed. |
| HDR-01–06 | All nine native samples assert trusted clinic name, present contacts/country, missing contacts/logo, one title, Letter dimensions, image bounds/aspect and content bounds; long-name samples visually reviewed. PASS. |
| HDR-07–08 | Tenant-context switching uses distinct logo storage; empty tenant/subject denied; request has no tenant/patient input; no public operation endpoint, no controller changes. PASS locally. |

Tenant/patient isolation cases use scoped repository fakes and the real branding authorization service. They do not establish authenticated cross-tenant runtime acceptance; that remains a Step 04C check.

Execution:

- `CppSelectivePrintTests`: **33 passed**.
- `ImmunizationSummaryTests`: **14 passed**.
- `BrandedChartPdfTests`: **32 passed**, including nine native Chromium PDF cases.
- Initial run: 74 passed / 5 failed, all failures in native-sample assertions. Corrections were test-only: reconstruct extracted words across line wraps; measure logo ratio in print layout and allow one CSS pixel of PDF image-transform rounding. No production correction or shared-renderer change was required. Only the affected native group was rerun: **9/9 passed**. The 70 other distinct cases retain their passing first-run results; total **79/79 distinct cases**, zero skipped.
- TRX receipts: `.tmp/product-improvement-009/step04/results/focused.trx` and `native-recheck.trx`.

Affected build command:

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/product-improvement-009/step04/artifacts -v:minimal
```

**PASS, 0 warnings / 0 errors**; affected Application/Infrastructure/API/Web and test dependencies built in isolated artifacts. After the assertion correction, only the test project was rebuilt with `--no-dependencies -p:BuildProjectReferences=false`: PASS, 0 warnings/errors. No full solution, OntarioMD or unrelated Step 01–03 suites ran. No service was started or restarted.

## Synthetic samples and visual results

Output directory: `.tmp/product-improvement-009/step04/`. Each basename has `.html` and `.pdf` files plus `-page-N.png` images.

| Sample | Pages |
| --- | ---: |
| `cpp-logo` | 3 |
| `cpp-no-logo` | 3 |
| `cpp-long` | 16 |
| `immunization-logo` | 2 |
| `immunization-no-logo` | 2 |
| `immunization-long` | 14 |
| `encounter-logo` | 1 |
| `encounter-no-logo` | 1 |
| `encounter-long` | 9 |

All **51 pages** rendered with the existing Windows.Data.Pdf utility, adapted only for the sample list/directory; receipt `pdf-image-rendering.json`. All pages inspected in five contact sheets, with all nine first pages and all three long-sample last pages examined at full image size. Header placement, logo proportions, identifiers, clinical section sequence, attribution, page transitions, repeating immunization headings and footer clearance were readable, with no observed overlap or clipped text. Extracted-word bounds were also checked on every native PDF page.

The long encounter intentionally retains existing template/contribution grouping: large blocks can leave whitespace before continuing on subsequent pages. CPP fields can continue across pages as before. No pagination redesign or repeated-letterhead feature was introduced. Long fixtures contain 600 CPP history lines, 65 ordinary immunization rows plus refusal/correction rows, and a 55-line saved encounter field plus multiple separately attributed narrative contributions.

## Files and scope review

Production files changed:

- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Application/PatientImmunizations/PatientImmunizationSummaryService.cs`
- `src/MicroEMR.Application/ClinicalOutput/ClinicalPdfPreviewService.cs`
- `src/MicroEMR.Application/ClinicConfiguration/ClinicBrandingService.cs`

Test/document files changed or added:

- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs` — fixture supplies branding.
- `tests/MicroEMR.Api.Tests/ImmunizationSummaryTests.cs` — reusable fixture supplies branding.
- `tests/MicroEMR.Api.Tests/BrandedChartPdfTests.cs` — new focused checks/samples.
- `docs/product-readiness/009-step04b-branded-encounter-chart-pdfs.md` — this report.

Disposable build/results/image/scope helpers remain under `.tmp/product-improvement-009/step04/`.

Git status was read **once initially**, as requested: ten existing modified/untracked owner files, including migration 0105, prescription v3 implementation/tests/docs and the Step 04A assessment. They were preserved. No further Git operation was performed; this report does not present a fresh Git-status result.

Final scope review compared **748 production source checksums** with the pre-Step04 build's portable PDBs. Exactly the four approved production files differ. Reversing only the precise new header/constructor/enum/title edits in memory reproduces each original checksum, proving no additional edits in those files, including signed/consultation/shared render methods. All other production source bytes match. Evidence/helper: `.tmp/product-improvement-009/step04/prove-production-diff.fsx`. No SQL, manifest, migration, browser print, prescription or other output code was changed.

## Deferred issues, deployment prerequisites and rollback

**Unresolved and outside Step 04B:**

1. Signing commits before signed artifact creation; existing retry behavior can create a missing artifact afterward. No transaction, Ensure, retry or historical-generation eligibility fix was made. Handle in a separate narrowly scoped lifecycle task.
2. Chronology exposes prescription snapshot properties, including version-3 branding/base64 data. No chronology or prescription snapshot change was made. Recommend a separate narrow projection/data-minimization correction preserving clinical fields, legacy compatibility and attachment references.

Existing Step 04A runtime/audit qualifications remain qualified. This task changes no certification status.

No migration or grant is required for this change. Future deployment requires explicit authorization, compatible Application/API/Web binaries retaining existing prescription-v3 support, existing tenant branding configuration, existing Chromium availability and normal scoped branding DI. Runtime acceptance needs existing suitable synthetic records and authenticated sessions with the required permissions. Existing migration-0105 deployment qualifications belong to Step 03B and are not revalidated here.

Rollback should revert only these four production opt-ins/authorization additions and their focused fixture/test changes using the owner's normal local release process. Retain unrelated prescription-v3 work and database history. No persisted Step04B data or artifact needs reversal; no database rollback is proposed. This report does not authorize a Git rollback operation.

## Proposed Step 04C boundary

After separate authorization, deploy compatible development binaries and verify these three paths using existing fixtures in the selected development tenant: logo/text-only behavior, long pagination, patient/content/attribution and selected CPP clinician. Qualify an Encounters.View + Encounters.Edit account lacking Patients.View/ClinicSettings.Manage, denied accounts, and a second tenant. Compare an existing readable signed artifact's bytes/hash before/after Open preview/current branding reads; missing artifacts must remain missing without repair. Preserve current read-audit behavior and verify existing summary audit events.

No fixture creation, logo write, permission change, clinical edit, signing, finalization, missing-artifact repair, production access or browser-print expansion is included in that proposed default boundary. Any such operation needs its own explicit task authorization. **Step 04C has not started.**
