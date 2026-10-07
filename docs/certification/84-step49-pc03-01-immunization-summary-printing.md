# Step 49: PC03.01 patient immunization summary printing

Date: 2026-10-07. Status: **PC03.01 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION**. No whole-clause SATISFIED claim.

## Requirement and previous gap

OntarioMD Primary Care Baseline 1.7 — Final, PC03.01, p.15, mandatory (M), as captured in [Step 48](83-step48-certification-evidence-checkpoint.md):

> The EMR Offering MUST provide the functionality to print the Immunization Summary for a patient.
>
> Immunization Summary MUST include:
> a) Patient Name
> b) Patient Date of Birth
> c) Patient HCN
> d) Complete list of Patient’s Immunizations
> e) Immunization Date
> f) Name of the primary Clinician

The accompanying guideline identifies the clinician accountable for administering the specific vaccines, with multiple names where different clinicians administered them. No additional ordering, vaccine catalogue, registry, forecasting or recording requirements are inferred.

Previous confirmed gap: printing, not immunization history. Existing [Step 25A](30-step25a-basic-immunization-history.md) structure and the Step 48 findings remain the baseline; neither report is edited.

## Implementation and reused infrastructure

The existing patient-chart Immunizations tab now has **Print Immunization Summary** beside its status filter. It opens an authenticated inline `application/pdf` response in a new tab using normal browser PDF viewing/printing. The chart filter does not limit the summary.

Web `patients/{patientUid}/immunizations/summary/pdf` forwards the bearer token to API `api/patients/{patientUid}/immunizations/summary/pdf`. Thin controllers delegate to the new Application summary service. Existing IPatientService supplies authoritative demographics. IPatientImmunizationService calls the existing Infrastructure tenant repository and `PatientImmunization_ListByPatient`; no new SQL access or storage is introduced.

Composition reuses IClinicalPrintLayoutRenderer and IPdfRenderer (the existing PlaywrightPdfRenderer in production), existing clinic configuration and TimeProvider. No PDF dependency, reporting framework, output-artifact table or clinical mutation is added. This is a current summary generated on request, not a preserved signed document.

Patient header includes name, DOB and HCN/version. Missing name/HCN display **Not recorded**, without substituting other identities or showing an orphan HCN version. DOB is nonnullable in the existing patient contract; a default/unavailable DOB also displays **Not recorded** using a new optional display value in the print context. Existing callers omit this option and keep their output behavior.

The summary reads **All**, without pagination/date limits, retaining old and historical/external records. Each row includes vaccine, administration date, AdministeredByName and status. EnteredInError records remain clearly labelled **Entered in error**, with an explicit explanation that they are retained history, not valid administrations. No correction record is deleted or silently presented as a valid administration.

Accountable clinician comes exclusively from the immunization's historical **AdministeredByName**. CreatedByDisplayName, logged-in user, current providers and Care Team are not substitutes. Unknown historical administrators display **Not recorded**; this existing data limitation is not filled with fabricated data.

Ordering matches the established stored-procedure history: AdministrationDate descending, CreatedAtUtc descending, ImmunizationUid ascending. Application composition enforces the same deterministic order. A fixed-layout table wraps long names and uses repeating table headers, with existing Letter page margins and row break-avoidance. Actual multi-page rendering still needs manual verification.

No recorded immunizations produces a valid patient-specific summary stating **No immunizations recorded**, without fake rows. Empty renderer output is rejected.

## Security and audit

Both routes inherit existing authenticated **Patients.View** authorization, the established immunization read permission; no ClinicalData.Manage permission is added for this read-only action. The Web client uses the existing access-token mechanism and preserves API 401/403 and missing-patient responses. Both successful PDF responses use **Cache-Control: no-store**.

Existing patient and immunization Infrastructure reads use trusted tenant connections. Missing/deleted/foreign-tenant patient UIDs cannot produce a summary. The service also verifies that returned patient identity and every returned immunization's PatientUid match the requested patient before rendering, preventing mixed-patient output. A user authorized for another patient's chart can request that patient's own summary under the existing access model; no new per-patient access rule is invented.

Nearest established policies inspected: patient-chart read audit, structured-read/output audit contracts and supported SQL event combinations, plus the existing report-output controller pattern. The current dedicated structured-read SQL supports encounter/document/file events, not an immunization-summary print event. No new event or SQL policy is invented. Instead, generation records the existing **PatientChartOpened** through IPatientChartReadAuditService before history retrieval. This reuses clinical actor resolution, patient UID, trace identifier and existing audit persistence. Audit failure prevents clinical read/output. No vaccine names, dates, clinician names or clinical bodies are added to audit details. The event records output access, not confirmation that the user physically printed the PDF.

## Files changed

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/PatientImmunizations/PatientImmunizationSummaryService.cs` | New patient-scoped audited composition service/interface using existing reads/layout/PDF. |
| `src/MicroEMR.Application/DependencyInjection.cs` | Register summary service. |
| `src/MicroEMR.Application/ClinicalOutput/ClinicalPrintLayoutRenderer.cs` | Optional DOB display value for explicit missing data; default behavior preserved. |
| `src/MicroEMR.Api/Controllers/PatientImmunizationsController.cs` | Thin patient-scoped PDF route. |
| `src/MicroEMR.Web/Services/PatientImmunizations/PatientImmunizationApiClient.cs` | Existing authenticated client gains PDF transport. |
| `src/MicroEMR.Web/Controllers/PatientImmunizationsController.cs` | Inline PDF response and access-error mapping. |
| `src/MicroEMR.Web/Views/Patients/Details.cshtml` | One print link in Immunizations. |
| `tests/MicroEMR.Api.Tests/ImmunizationSummaryTests.cs` | Focused summary/transport/access/audit checks. |
| This report | Certification evidence and manual path. |

**Database/schema/migration changes: NONE.** No procedure changes, migration analysis beyond the existing directly relevant list/audit behavior, or database connection/execution.

## Focused validation and actual compilation

One command, successful without retries:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ImmunizationSummaryTests' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step49/bin/'
```

**13 passed, 0 failed, 0 skipped; duration 644 ms; exit code 0.** The same command compiled the affected Application/API/Web and existing Core/Infrastructure/DatabaseTool/test-project references successfully, with no warnings/errors reported. No standalone build followed it. Isolated outputs avoid replacing DLLs used by running development services.

Coverage: required demographics/dates, complete old/current/corrected history, administering snapshots without creator substitution, encoding, missing demographics/clinicians, empty history, 120 long-name rows without truncation, deterministic newest-first output, mismatched-patient rejection, unknown/foreign-tenant UID behavior, audit failure, inherited route permissions, denied API permission handler, Web 401/403 propagation, bearer forwarding and patient-scoped inline PDF/no-store/404 responses.

Limits: tests capture HTML through a fake PDF renderer and stub tenant-scoped patient reads; `%PDF-test` bytes are transport fixtures, not real-PDF validation. No live cross-tenant database test or full HTTP authentication pipeline was executed. Existing tenant routing and renderer are reused; actual PDF readability/pagination/printing remains manual evidence. No Playwright/browser launched. No unrelated tests, full API/Auth/solution suite, referral tests or migration tests. TypeScript unchanged; no TypeScript compilation.

Final document/source diff and whitespace checks passed; only intended files changed. Starting working tree was clean, and historical/manual work was preserved. No commit, merge or push performed.

## Manual verification and resulting status

Rebuild/restart the normal development application to load the updated assemblies; isolated test builds do not update running services.

1. Open a patient with immunizations → Immunizations → Print Immunization Summary.
2. Check name, DOB, HCN, every expected old/current record, dates and recorded administering clinicians. Confirm unknowns and entered-in-error labels; chart status filter must not truncate output.
3. Use PDF print/preview; inspect long names and multiple pages where enough records exist.
4. Open an empty-history patient and confirm patient identity plus No immunizations recorded.
5. With an account lacking Patients.View, confirm direct PDF access is denied; inspect the existing chart-read audit for authorized summary access.

No remaining mandatory functional omission identified in this bounded implementation. Historical unknown data remains explicit. **PC03.01 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION** until the user verifies actual output. No SATISFIED or manual PASS claim.

Resource discipline: no repository-wide scan or certification reanalysis; no referral/PC10, Care Team or Consultation implementation recheck; no unrelated tests or unnecessary database/browser work. Inspection stayed within immunization/demographic reads, existing print/clinic/audit infrastructure and directly affected contracts/UI/tests. Stop after Step 49; no next requirement selected or started.
