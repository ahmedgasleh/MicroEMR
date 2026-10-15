# Step 90: PC04.05 Prescription Print Scope and Evidence Assessment

Date: 2026-10-15. **PC04.05 — PARTIAL — IMPLEMENTED.** Assessment only. PC04.16 remains **SATISFIED — VERIFIED, with qualifications** under [Step 89](123-step89-pc04-16-evidence-closure-next-gap.md). Outstanding inventory remains **11 mandatory-gap IDs / three packages**. No implementation or certification closure is performed here.

## 1. Git checkpoint and inspection scope

Branch `feature/step-87-pc04-16-independent-refill-instructions`, HEAD `a1645ae`. Starting tree: eight modified tracked files and nine untracked files, exactly the Step 89 final state. Existing code, migration, tests, Auth Postman callback change and reports 118–123 are preserved. Only this new report is added; final tree has eight modified tracked files and ten untracked files. No staging, commits, merges or branch changes.

Scoped reading: Step 89; Step 87 implementation/evidence sections; relevant Step 45/86 mapping; prescription DTO/service renderer, API/Web artifact actions, chart View/Print controls, migration 0050 artifact schema and 0073 finalization snapshot; shared PDF renderer; relevant existing prescription/clinical-output test contracts. No repository-wide scan or browser operation.

## 2. Available requirement wording and authority boundary

Use the existing [Step 45 PC04.05 mapping, PDF p.18](77-step45-baseline-1.7-reconciliation.md), derived there from the user-designated *Primary Care Baseline — 1.7 Requirements.pdf*, Version 1.7 Final. Its exact available repository wording is:

> Prescription print: clinician name/clinic address/phone; patient name/HCN/address/phone; drug name/strength-unit/form/dose/frequency/duration-or-quantity/refills/refill duration-or-quantity/start/pharmacist notes. Multipage demographics/signatures on all pages; user/prescription timestamp visible on print/reprint, not audit lookup.

This is the **repository's mapped summary, not a verbatim quotation of the full authoritative clause**. The PDF exists at the path linked in Step 45, but direct extraction was unavailable in this session: no usable installed PDF parser/Poppler was found, and a temporary parser-install attempt failed without installing a reader. No source PDF page was newly rendered or visually reviewed. Do not invent omitted wording. Exact signature modality, timestamp semantics, conditional wording and physical-print acceptance must be checked against p.18 before implementing those details. This is a precise source-verification limitation, not a reason to repeat PC04.16 tests.

| Mapped obligation | Scope / distinction |
| --- | --- |
| Clinician and clinic identity | Clinician name; clinic address and phone. A provider UID or billing number is not a substitute for clinic contact information. |
| Patient identity | Patient name, HCN, address and phone. DOB is useful existing output, but does not replace the mapped items. Handling genuinely absent identifiers/contact data needs explicit source/policy confirmation; do not fabricate values. |
| Medication instructions | Name, strength/unit, form, dose, frequency, duration or quantity, repeats, refill duration or quantity, start and pharmacist notes. Do not assume free-form product text or Indication automatically meets every distinct element. |
| Initial/refill behavior | Reuse the already verified separate initial/refill fields. Step 86's PC04.16 mapping establishes independent quantity and days' supply; it does not turn every engineering field into an unconditional required value for every record. |
| Formatting and signatures | Mapped multipage demographic/signature conditions apply to all pages. The available summary does not establish an electronic/digital signature mechanism, signature placement, or legal signing process; confirm exact clause before design. Authentication of a user alone does not prove printed signature compliance. |
| Print/reprint attribution | Mapped visible user/prescription timestamp requirement. A stored audit row, prescribed date or finalization timestamp that is absent from output does not satisfy it. Exact event/time semantics require clause confirmation. |
| Preview/PDF/physical printing | Mapped obligation is prescription print capability/content. PDF generation and View/Print are current delivery mechanisms, not independently established mandatory technologies. No separate mandatory draft preview, browser preview architecture or physical-printer execution rule is established by the available summary. Do not claim downloaded PDF bytes alone prove every print condition. |

Dependencies retained from Steps 45/86/89: source folder 5.5 versus document 1.7 and CDS-S publication labels do not prove applicability to PCON-2024-02 Stage 3. OntarioMD acceptance remains external. Licensed catalogue/safety integration is not established as a prerequisite to this bounded output assessment.

## 3. Existing implementation and precise gaps

Prescription output is a **dedicated HTML renderer**, not the richer shared `ClinicalPrintLayoutRenderer` used elsewhere. Shared layout fields/signature tests must not be credited to prescriptions merely because both use `PlaywrightPdfRenderer`.

| Capability | Actual inspected path | Assessment |
| --- | --- | --- |
| View/Print and PDF | Chart exposes View/Print when `ArtifactUid` exists. Web Artifact action calls API `/api/patients/{patientUid}/prescriptions/{prescriptionUid}/artifact`; both return `application/pdf` with a filename. Chromium renders Letter PDF with margins, background and CSS-page-size preference. | Implemented. Endpoint offers a PDF download; embedded browser preview and physical-printer success are not established by source inspection. No separate draft-preview action in this path. |
| Patient identity | Finalization snapshot contains `PatientName` and `DateOfBirth`; HTML prints both. | HCN/address/phone are not in the current snapshot or prescription template. Confirmed mapped content omissions, not merely missing manual evidence. |
| Prescriber/clinic | Snapshot captures active mapped Provider display name and credential string (provider type plus optional billing number); HTML prints these. | No clinic address/phone in snapshot/template. Prescriber attribution exists, but signature and print/reprint user attribution are not implemented by these labels. |
| Product/strength/form/dose | Snapshot stores `ProductDisplayText`, strength/unit and dose/unit. HTML prints only ProductDisplayText for the product; no distinct strength/dose lines. DTO has no dedicated medication-form field. | Structured strength/dose can be lost from output when absent from free text. Form is not independently represented. Do not infer an adequate full product description from every free-text entry. |
| Directions/frequency/route | Snapshot and template retain Directions, route, frequency display and PRN. | Implemented; accepted refill output evidence is reusable within its scope. |
| Initial/refill dispensing | Snapshot version 1 and HTML separate initial quantity/unit/days, authorized repeats, refill quantity/unit/days; missing optional values show `Not recorded`. | Qualified PC04.16 PASS reused; do not repeat. |
| Start / pharmacist notes | DTO stores StartDate but current final snapshot omits it. No dedicated pharmacist-notes DTO/snapshot/template element; Indication is printed. | Start-date output omission confirmed. No established pharmacist-notes mapping; do not relabel Indication as notes without authority/design. |
| Signature / multipage layout | Standalone HTML has a flowing definition list and basic body/header styles. No signature block, repeated demographic/signature page furniture or prescription-specific page-break policy. | Missing explicit mapped multipage support. No new rendered multipage defect is claimed; implementation omission is demonstrated by template inspection. |
| Print/reprint identity/time | Output shows prescribed date, prescriber and UID; snapshot has no current print user/time. Renderer reads existing artifact and returns PDF. | Visible mapped print/reprint attribution is absent. Browser-generated print headers are not an implemented substitute. |
| History and access | Frozen JSON stored transactionally at finalize; read regenerates PDF from JSON, with legacy rendering branch. API/Web require Patients.View; patient/prescription compound scope; prescribing operations require Prescriptions.Prescribe and resolved actor. | Preserve accepted history/security/audit evidence. Do not modify frozen artifacts or weaken access to fill output gaps. |

Evidence anchors: [prescription service/DTO](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs), [finalization snapshot](../../db/tenant-clinical/migrations/0073-prescription-refill-instructions.sql), [artifact schema/read](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql), [Web action](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs), [PDF renderer](../../src/MicroEMR.Infrastructure/ClinicalOutput/PlaywrightPdfRenderer.cs).

The demonstrated gaps are absent snapshot/template content and layout/attribution mechanisms. They are not evidence of a new PDF-generation outage. Provider/patient/clinic data availability and historical versus current contact semantics must be mapped before a fix; this step does not claim every required source is already exposed through the prescription DTO.

## 4. Evidence reused, not executed again

- **Step 87 final relevant Release build:** passed, zero warnings/errors; not a full solution build. Final focused prescription/output selection: **49 passed / zero failed / zero skipped**, including **30 new cases**. Earlier 19-pass/nine-fail development selection and overlapping 10-pass correction rerun remain historical; do not sum counts or hide earlier failures.
- Existing refill tests cover SQL persistence/snapshot, correction, legacy JSON/HTML retention and actual PDF generation. `ClinicalPdfPreviewTests` also includes generic PDF/layout tests; tests for shared patient-document/encounter demographics/signatures do not prove the dedicated prescription template contains those elements.
- **Step 89:** all eight functional manual scenarios, including output separation/legacy compatibility, plus concurrency, access boundaries, ownership and clinical audit are user-reported PASS. They are accepted and will not be repeated. They do not claim inspection of every PC04.05 demographic/notes/signature/reprint field.
- View-only Web chart/artifact access passed after fresh sign-in. Earlier missing-token exception did not recur; root cause is not conclusively established. No authentication repair is proposed here.
- Historical Step 46 **33 passed / one failed / zero skipped** manifest-assertion gate remains qualified. No fresh full suite, build, manual result, physical-print evidence or PC04.05-specific print/reprint audit event is claimed.

## 5. Focused manual plan: no user execution requested now

**No additional manual test is required to establish the demonstrated implementation omissions in this assessment.** Reprinting a known incomplete template cannot close them. Do not repeat PC04.16, access, legacy, concurrency or audit scenarios. No new fixtures, patient/provider changes or SQL execution are requested from the user in Step 90.

Defer new PC04.05-specific execution until the exact clause is checked and the bounded implementation proposal is authorized. The future acceptance checklist should address only missing identity/contact content, explicit medication/start/notes mapping, signature/multipage behavior and print/reprint attribution. This is a planning boundary, not an instruction to run vague manual tests. A later authorized verification step must provide the complete prerequisites, supported fixture creation, numbered UI actions, expected output, actual-schema SELECTs where needed, recording criteria and safe cleanup required by the user's manual-testing standard.

If suitable PC04.05 output captures already exist, they can be reviewed in that later step without regenerating accepted evidence. No physical printer, existing synthetic HCN, provider contact details or database fixture ID is assumed available. Do not manufacture identifiers or issue real prescriptions for certification testing.

## 6. Read-only SQL inspection available (not executed or requested now)

Schema verified from migration 0050, current 0073 snapshot and the previously established tenant metadata schema. An authorized observer can use the following complete batch in **Tenant A's actual clinical database**, not Auth/platform. Its database name must come from the existing tenant configuration; none is invented here. No Tenant B queries are needed because tenant isolation is already accepted and is not being retested.

For later authorized inspection only, replace both NULL values with quoted actual UIDs captured from the existing fictional patient/finalized prescription. The batch changes no database data:

```sql
DECLARE @PatientUid uniqueidentifier = NULL; -- replace NULL with actual patient UID
DECLARE @PrescriptionUid uniqueidentifier = NULL; -- actual finalized prescription UID
IF @PatientUid IS NULL OR @PrescriptionUid IS NULL
    THROW 50000, 'Supply both actual synthetic fixture UIDs before inspection.', 1;

SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey
FROM dbo.TenantDatabaseIdentity;

SELECT
    x.PatientUid, x.PrescriptionUid, x.Status,
    x.ProductDisplayText, x.StrengthValue, x.StrengthUnit,
    x.DoseAmount, x.DoseUnit, x.StartDate,
    x.InitialDaysSupply, x.RefillQuantity, x.RefillQuantityUnit,
    x.RefillDaysSupply, x.AuthorizedRepeats,
    x.FinalizedBy, x.FinalizedAtUtc,
    a.ArtifactUid, a.MimeType, a.CreatedBy, a.CreatedAtUtc,
    a.ArtifactJson
FROM dbo.PatientPrescription AS x
LEFT JOIN dbo.PatientPrescriptionArtifact AS a
    ON a.PrescriptionUid = x.PrescriptionUid
   AND a.PatientUid = x.PatientUid
WHERE x.PatientUid = @PatientUid
  AND x.PrescriptionUid = @PrescriptionUid;
```

Expected for a known finalized fixture: correct tenant identity; one prescription row with non-null artifact UID/JSON. Stored artifact `MimeType` is normally `application/json`; the API separately renders and returns `application/pdf`. With a version-1 snapshot, supplied dispensing values match the accepted fixture. Comparing source StartDate/strength/dose with JSON and template identifies storage-versus-output gaps; no absent fixture values are invented. Legacy JSON may lack version-1 fields. No rows means wrong scope/fixture or missing record, not a print PASS. This query does not prove PDF appearance, signature compliance or print/reprint attribution.

No audit-correlation query is necessary for the Step 90 source assessment: lifecycle audit is already accepted, and no verified print-specific audit event was found in the inspected prescription read path. Do not invent one or substitute lifecycle audit for visible print attribution. No SQL was run against a database.

## 7. Recommendation: Step 91

**Smallest next step: PC04.05 exact-clause confirmation and bounded prescription-output implementation proposal.** Current evidence cannot support IMPLEMENTED — NEEDS MANUAL VERIFICATION or SATISFIED — VERIFIED for the whole requirement because mapped content is missing.

Before separately authorized coding:

1. Read/visually confirm authoritative p.18, especially signature, multipage, print/reprint timestamp/user semantics, conditional notes/start fields and what constitutes print. Record applicability limitations instead of guessing.
2. Map only missing output data to existing patient/clinic/provider/prescription sources, distinguishing frozen clinical content from current print-event metadata. Determine minimum DTO/snapshot additions and whether data retrieval is enough or a smallest additive database change must be proposed. Do not backfill or rewrite finalized historical JSON.
3. Propose focused template/page-furniture/signature/attribution changes. Existing shared layout can inform a solution but is not assumed compliant or automatically substituted. Preserve Clean Architecture, trusted tenant/actor identity and historical artifacts.
4. Define focused automated changes and executable manual checks for those new elements only; reuse existing PC04.16 evidence. Execute nothing until authorized in the next step.

Suggested branch for later authorized work: `feature/step-91-pc04-05-prescription-print`. Not created. No supplier integration, prescribing redesign or unrelated document-layout refactor is proposed.

## 8. Qualifications and documentation checks

Preserve Step 89's internal-qualified PC04.16 closure, numeric/default engineering-policy distinctions, bounded refill interpretation, historical PDF byte-identity limitation, user-reported evidence provenance, release uncertainty, historical automated gates and external OntarioMD acceptance boundary. PC10.01/.02 and prior PC08/PC09/PC10.01 qualifications, including deferred PC10.02 audit, remain unchanged.

Documentation checks: scoped local links, whitespace, SQL column/schema consistency and final Git scope. No application changes, database changes/migrations, browser operations, manual repeats, tests/builds, commits or merges. Inventory remains **11 mandatory-gap IDs / three packages**. Stop after this assessment.
