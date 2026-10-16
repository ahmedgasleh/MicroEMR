# Step 91: PC04.05 Authoritative Scope and Bounded Output-Fix Design

Date: 2026-10-15. Specification confirmation/design only, continuing [Step 90](124-step90-pc04-05-prescription-print-scope-evidence-assessment.md). **PC04.05 — PARTIAL — IMPLEMENTED.** Inventory remains **11 mandatory-gap IDs / three packages**. PC04.16 retains [Step 89's qualified closure](123-step89-pc04-16-evidence-closure-next-gap.md).

## 1. Actual Git checkpoint

Observed branch **main**, HEAD **1530f8e**, `independent refill instruction`; starting working tree **clean**. This supersedes the previous reported branch/dirty-tree checkpoint. No branch switch, staging, commit or merge is performed. Existing committed implementation/reports remain untouched. Only this new untracked report is added. No application or database changes, browser operations, tests/builds or manual executions.

## 2. Authoritative clause confirmed

Source: [Primary Care Baseline — 1.7 Requirements.pdf](</D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), **PDF/printed p.18 of 53**, PC04.05 row, mandatory column **M**. Cover identifies **March 20, 2026; Document Version & Status: 1.7 — Final**.

The local PDF's page tree, content streams and ordinary text runs were directly extracted read-only using Python standard-library decompression after the unavailable parser-install attempt. Page 18 identifies its printed page number and contains the complete PC04.05 row. This is direct source-text inspection, not a new visual-layout review or OCR. Adjacent pp.17/19 were checked to distinguish their CDS-S/COTS references from this clause. Paragraphs below normalize line wrapping only; no electronic-signature wording is supplied by inference.

**Requirement column:**

> The EMR Offering MUST provide clinicians with the functionality to print the prescription.

**Guidelines column, normative content:**

> Printed prescription MUST be able to include:
>
> a) Clinician information (name, clinic address, phone number)
>
> b) Patient information (name, HCN, address, phone number)
>
> c) Name of medication
>
> d) Strength and strength unit
>
> e) Form
>
> f) Dosage
>
> g) Frequency
>
> h) Duration and/or quantity
>
> i) Refills
>
> j) Refill duration and/or refill quantity
>
> k) Start date
>
> l) Notes to pharmacist

**Remaining guidelines:**

> It is acceptable that prescriptions are printed on a standard 8.5 x 11 sheets of paper.
>
> If the prescription spans multiple pages, all demographic info and clinician signatures MUST be printed on all pages.
>
> Multiple prescriptions can be printed on a single form.
>
> The EMR Offering MUST identify each user and the timestamp of the prescription when printed/re-printed. Accessing the audit log for this information is not an acceptable solution.

### Authority categories and interpretation

1. **Explicit mandatory:** printing capability; ability to include items a–l; all demographic information and clinician signatures on every page when multipage; user/prescription timestamp identification on print/reprint without audit-log lookup. “MUST be able to include” establishes field support, not unconditional population of every field for every patient/order.
2. **Directly referenced definitions:** PC04.05 itself contains **no explicit external requirement/definition citation** in this row. The PC04.03 CDS-S reference on p.17 and PC04.06 COTS requirement are adjacent clauses, not new PC04.05 dependencies. Existing Step 86 CDS-S mapping informs the already closed refill interpretation; it does not add signature or numeric rules to PC04.05.
3. **Engineering choices:** PDF as the printing vehicle; Letter media (explicitly acceptable, not uniquely mandatory); separate labelled fields; snapshot versioning; authenticated print-request metadata; fixed page furniture; test fixture design and numeric bounds. “Multiple prescriptions can be printed on a single form” has no MUST/MAY modal; it does not by itself justify a new mandatory batch-print feature.

No separate draft preview, PDF technology, cryptographic signature, handwriting-capture technology, signature image, physical-printer integration, print-event audit record or guaranteed printer completion is explicitly prescribed. Signature **repetition** is explicit; signature **modality and creation workflow** are not specified. Current prescriber identity text is attribution, not proof of signing. The user/timestamp sentence leaves the exact lifecycle timestamp and treatment of multiple users insufficiently defined: do not silently equate it only with download time or only with audit time.

The source-folder 5.5/document-1.7/tracked-PCON-2024-02 Stage 3 applicability qualification remains; source-text access does not establish release acceptance.

## 3. Requirement-to-implementation matrix

Source anchors: [prescription DTO/service/template](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs), [0073 final snapshot](../../db/tenant-clinical/migrations/0073-prescription-refill-instructions.sql), [0050 schema/artifact](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql), [PDF renderer](../../src/MicroEMR.Infrastructure/ClinicalOutput/PlaywrightPdfRenderer.cs). Step 90's findings were checked against these contracts; no broad rescan.

| Mandatory element | Current classification | Required change location |
| --- | --- | --- |
| Print capability | **SATISFIED** within existing PDF/View/Print implementation and accepted output evidence; no new physical-print result claimed | Preserve Web/API artifact actions and prescription API client. |
| Clinician name, clinic address/phone | **PARTIAL**: Provider display/credential printed; clinic contacts absent | Finalize snapshot, Application output model/template; existing `ClinicProfile` supplies contacts. |
| Patient name/HCN/address/phone | **PARTIAL**: name/DOB printed; other listed data absent | Finalize snapshot/template; reuse existing `Patient` columns. |
| Medication name | **SATISFIED**: ProductDisplayText printed | Preserve authoritative recorded text; do not derive form/dose from it. |
| Strength/unit, form, dosage | **PARTIAL**: strength/dose stored in snapshot but not explicitly rendered; no dedicated form contract | DTO/service/template; new optional form input/storage proposal below. |
| Frequency, duration/quantity, repeats, refill duration/quantity | **SATISFIED** for established implementation/qualified PC04.16 evidence | Reuse; no repeat PC04.16 verification. |
| Start date | **PARTIAL**: stored on prescription, absent from frozen JSON/template | Finalize snapshot/template; no new StartDate column. |
| Notes to pharmacist | **MISSING** distinct supported field; Directions and Indication are different concepts | Optional draft input/DTO/storage/correction mapping and output. |
| Demographics on all pages | **MISSING** repeated page furniture | Prescription-specific renderer/PDF options; reserve space on every page. |
| Clinician signatures on all pages | **MISSING** representation; **UNCERTAIN** acceptable modality/workflow | Signature source/authorization decision needed before implementation. Provider name is insufficient. |
| Visible user and prescription timestamp on print/reprint | **PARTIAL**: prescriber and prescribed date printed; current print user not supplied and no full prescription timestamp shown | Thin API resolves trusted subject; Application output context/template. Exact timestamp semantics remain a clarification, with conservative proposal below. |

## 4. Minimal implementation proposal (not authorized or executed)

### Data and layer plan

| Proposed change / requirement | Existing source and DTO/Application/PDF changes | Database impact | Integrity, history and verification |
| --- | --- | --- | --- |
| Patient/clinic contact fields, a–b | Existing `Patient.HealthCardNumber`, address components and `PhoneNumber`; tenant-local `ClinicProfile.LegalName`, address components and `Phone`. Add only these properties to a new prescription output snapshot version. Render explicit labelled identity blocks, repeat demographics on each page. | New additive migration replaces relevant finalization procedure for **future** snapshots; no new patient/clinic columns. ClinicProfile singleton is tenant-local; never use another tenant's settings. | No live-contact fallback for old frozen artifacts. Missing values must not be fabricated. Escaping/tenant-source tests; future A1/A3 below. |
| Strength/dose/start, d/f/k | Existing snapshot strength/dose/unit and prescription StartDate. Explicit output lines; add StartDate to future snapshot. | No new columns; future finalization procedure change only. | Preserve optionality; don't parse SIG. Composition/snapshot tests; future A1. |
| Medication form and pharmacist notes, e/l | No distinct field in inspected prescription contract. Propose optional `MedicationForm` and `NotesToPharmacist` inputs, typed DTOs, service validation, edit/correction copying and labelled output. Reuse Directions unchanged; never relabel Indication as notes. | Smallest reliable independent capture: **two nullable columns** on `PatientPrescription`, not new tables. Extend existing create/update/read/finalize procedures and repository mappings in one new additive migration. Length bounds (proposed 100/1000) are engineering choices, not OntarioMD mandates; no mandatory dictionary/COTS integration inferred. | No backfill. Draft changes retain existing lifecycle audit/RowVersion. Correction creates a new record and copies values. New mapping/validation/rollback tests; future A1. Confirm separately authorized scope before adding columns. |
| Multipage demographic identity | New snapshot identity block drives header/footer, PDF page numbering and sufficient margins. Prefer a prescription-specific renderer option/contract so other documents retain current behavior. Existing `PlaywrightPdfRenderer` supports PDF options, but currently has no header/footer contract. | None beyond new snapshot values. | Keep JavaScript/network disabled; encode all values; don't expose HCN in logs. Inspect rendered multi-page output; future A3. |
| Print/reprint user/timestamp | New Application print-request context resolved from server-authenticated `sub`/NameIdentifier (`AuthenticatedSubjectAccessor`) and trusted display identity; `TimeProvider` supplies request UTC. Also include stable prescription creation/finalization actor/time from existing row on future snapshot. Distinguish “Finalized by/at” from “Print requested by/at”; never accept actor/time from query/body. | No new columns required for visible transient print-request metadata; existing CreatedBy/CreatedAtUtc/FinalizedBy/FinalizedAtUtc available. Snapshot carries stable metadata for new artifacts. | Conservative engineering proposal pending semantic sign-off. It identifies request/rendering, not confirmed physical printing. Do not rewrite artifact JSON for each request. Future A2. |
| Signature repetition | Repeat an **actually authorized, truthfully represented** clinician signature on every page only after modality/source is agreed. No scanned signature or “electronically signed” text fabricated from provider name or finalization. | Undetermined pending accepted signature source; do not create signature columns/workflow speculatively. | Blocks full signing/multipage completion. Future A3 signature portion stays DEFERRED until resolved. |

Existing clinic data source verified in [ClinicConfigurationModels](../../src/MicroEMR.Application/ClinicConfiguration/ClinicConfigurationModels.cs), `IClinicProfileRepository`/Infrastructure `ClinicProfileRepository`, and [0028 clinic schema](../../db/tenant-clinical/migrations/0028-clinic-configuration-foundation.sql). Patient sources verified in `db/initial.sql`; no clinic/profile setup permission is added to normal prescription readers. Future finalization reads the tenant-local profile under existing trusted context, not via an admin-only HTTP endpoint. Missing clinic configuration needs an actionable message/policy, not permission escalation or a guessed address.

Changes would be bounded to prescription DTO/service/output, repository mappings, affected stored procedures/new additive migration, Web shared prescription form/edit/correction copy, API correction copy, and prescription-specific PDF options. Keep controllers thin, data access in Infrastructure and composition in Application. Preserve existing Bootstrap design and token/auth configuration. Do not rewrite 0050/0073 or introduce EF migrations.

### Signature and reprint safety decisions

- A blank signature line is not evidence that a clinician signed, nor proof that actual signatures were printed on all pages. It may be an explicitly labelled unsigned-paper aid after workflow approval, but cannot close the source sentence by itself.
- The clause does not mandate a particular electronic signing workflow. No signing implementation is authorized. Obtain the accepted representation/workflow from the specification owner/certification reviewer and product owner; identify the actual signature source and its permitted reuse.
- Print/reprint information must be available without audit lookup; put truthful attribution visibly on output. PrescribedDate alone is not sufficient to identify each relevant user. Clarify which users/timestamps the reviewer expects; the proposed stable clinical and current request metadata avoids hiding either event.
- A separate print/reprint audit event is **not explicit in PC04.05**. Existing clinical mutation auditing remains mandatory for new form/notes saves. A new disclosure/print event is a separate security-policy decision, not invented compliance scope.
- The inspected `dbo.AuditLog_RecordStructuredRead` procedure (0044) accepts only EncounterViewed/Encounter and PatientDocumentViewed/PatientDocument at that migration stage; it is not a proven prescription-print contract. Do not pass a fabricated event to it. Any later approved print audit needs its own verified governed procedure/allowlist design, with no clinical/token contents in logs.

### Historical preservation

Use a **new snapshot schema version** for newly finalized prescriptions; keep the version-1 and legacy render branches unchanged. No original JSON/artifact UPDATE, demographic enrichment from current tables, signature backfill or batch regeneration of historical signed artifacts. Where new attribution is desired for historical records, preserve existing retrieval behavior until a separate approved derivative/cover-sheet policy exists; do not silently alter their clinical page content. Full PC04.05 retroactive historical coverage is not claimed by this bounded future-artifact proposal.

## 5. Focused proposed automated checks — not executed

1. New form/notes optionality, bounds, JSON and SQL parameter round-trip, edit/correction copying, and existing successful lifecycle audit transactionality for the changed payload.
2. New snapshot includes exact recorded patient/clinic identity, strength/dose/start/form/notes; later live demographic/profile changes cannot change frozen content. Old JSON/HTML branches remain unchanged without regeneration.
3. Explicit HTML labels and escaping; no raw script from names/notes, no SIG parsing, missing values not fabricated. Reuse already recorded independent-refill tests; rerun only affected regressions when an implementation is separately authorized.
4. Trusted print subject/request time versus stable finalizer/time separation; request-body actor spoofing not introduced; artifact JSON/RowVersion unchanged by new rendering.
5. Prescription-specific two-plus-page PDF checks for repeated demographics, approved signature representation, safe margins and every-page attribution. Generic shared-layout tests alone are insufficient; avoid changing unrelated layouts.

Step 87's recorded build and 49/0/0 selection remain reused historical evidence; no test execution is claimed in Step 91. Step 46's historical **33 passed / one failed / zero skipped** gate remains qualified.

## 6. Future manual acceptance plan — do not execute in Step 91

All procedures below are **designs for separately authorized implementation verification**, not present functionality or claimed results. If proposed controls do not yet exist, record DEFERRED rather than improvising fields or SQL writes. No completed PC04.16/security/concurrency scenario is repeated.

### Shared prerequisites, mock fixtures and supported setup

Use only existing isolated **Tenant A's development clinical database** and matching Web/API/Auth deployment. Record its actual database name/TenantUid from the SELECT below, deployed build and eventual new migration ID; no database name/ID is invented here. Use normal tenant selection. A read-only SQL observer is needed only for the database assertions. Do not use Auth/platform databases for clinical queries.

Reuse established Prescriber A (active clinical user, active linked Provider, effective Patients.View + Prescriptions.Prescribe) and the established view-only account (Patients.View allowed, Prescriptions.Prescribe denied). Record actual display names/Auth IDs as expected output, not invented identities. If Prescriber A is unavailable, an authorized administrator may later create `rx.print.a@example.invalid` at `/TenantUserAdministration/Add`, select Physician and provision the clinical user; at User Details → Manage Access grant the two effective permissions through individual overrides. At `/Providers/Add` create FirstName `Rowan`, LastName `Print-Test`, DisplayName `Dr. Rowan Print-Test`, ProviderType `Physician`, Phone `416-555-0102`; save, then Providers → Link User → select the actual newly provisioned account. Leave optional billing number blank. Administrators need existing Users.Manage/Users.ManageAccess and Providers.View/Providers.Manage; do not change shared profiles.

Locate existing fictional Avery Refill-Test, DOB 1990-01-01, through Patients search; verify its actual tenant/UID before reuse. To avoid changing completed fixtures, preferably create a dedicated fictional patient at `/Patients/Create`: FirstName `Avery`, LastName `Print-Test`, DOB `1990-01-01`, Sex at birth `Unknown`, Phone `416-555-0101`, Address `10 Mock Print Lane`, City `Toronto`, Province `ON`, PostalCode `M5V 0A1`, HCN `TEST-PC0405-ONLY` (deliberately non-issued test marker), optional identifiers/email blank. Record actual returned UID/chart number. If local validation disallows that synthetic HCN, use an approved non-production fixture value supplied through the established test-data process or defer the HCN check; do not use a real HCN or bypass validation. Patient setup requires Patients.Edit.

An authorized ClinicSettings.Manage administrator records original settings and, only in this isolated tenant, sets `/ClinicConfiguration`: LegalName `Step92 Mock Clinic — TEST ONLY`, AddressLine1 `20 Mock Clinic Lane`, City `Toronto`, ProvinceState `ON`, PostalCode `M5V 0A1`, Country `Canada`, Phone `416-555-0100`. Save through the UI. Reuse existing matching test settings rather than changing them unnecessarily.

### A1 — New mandatory content (one prescription)

**Prerequisites:** shared setup; content/snapshot changes deployed and proposed form/notes controls available. Signature completion is evaluated only in A3.

1. Sign in as Prescriber A; open the dedicated fictional patient's chart → Medications → New Prescription.
2. Enter Product name `Step92 Mock Medicine — TEST ONLY`; Product display `Step92 Mock Medicine` (deliberately omit strength/form/dose here); strength **5**, unit **mg**; dose **1**, unit **tablet**; proposed Medication form **tablet**; Route **Oral**; Frequency **Once daily**; Directions `TEST ONLY — Take one mock tablet daily.`; Indication `TEST ONLY — display fixture`.
3. Use initial quantity/unit/days **10/tablets/10**, authorized refills **2**, refill quantity/unit/days **30/tablets/30**. These reuse accepted refill values and are not a new PC04.16 test. Prescribed date = actual execution date; Start date = the next calendar date, recorded explicitly. Proposed Notes to pharmacist: `TEST ONLY — Dispense in mock packaging; no real supply.`
4. Save Draft; capture actual prescription UID. Reopen Edit to confirm new form/notes/start/strength/dose inputs persisted; finalize once using the normal confirmation. Select View/Print; retain the actual PDF as A1 output.
5. Inspect PDF: exact synthetic patient/clinic contacts, actual prescriber name, explicit 5 mg, tablet form, 1 tablet dose, frequency, next-day Start date and separate pharmacist note. Product display alone must not be the only source of these elements. Chart remains the correct patient's record; output includes the existing dispensing fields without changing them.
6. Authorized observer runs the read-only batch below for this patient/prescription and captures the new snapshot and corresponding create/update-if-performed/finalize audit rows. Do not rerun earlier security or audit suites.

**PASS:** new mandatory content equals recorded inputs, is frozen in the new snapshot, and changed-payload saves have the expected existing lifecycle audit. **FAIL:** omission, substitution, parsing from free text, wrong actor/patient/tenant or false signing claim. **DEFERRED:** undeployed controls, missing approved test HCN or observer access (identify affected assertion separately). **Cleanup:** retain synthetic record/PDF/audit; restore temporary clinic settings after the sequence. Do not delete clinical records.

### A2 — Print/reprint visible identity and timestamp

**Prerequisites:** A1 finalized artifact; trusted request-metadata implementation and user/timestamp semantic decision approved; existing active view-only account. No new role/permission grant for rendering.

1. As Prescriber A open A1's View/Print, record UTC request window and save the returned PDF.
2. In a separate authenticated Web session sign in as the established view-only account, select Tenant A normally and open the exact same artifact link from the same patient's chart. Record UTC window and save this second output.
3. Compare visible metadata: each rendering identifies the actual requesting account and request time under the proposed labels; stable prescriber/finalizer and prescription timestamp remain unchanged. User/timestamp information must be visible without opening audit UI. Treat physical printer completion as unproven.
4. Observer compares artifact JSON, ArtifactUid and prescription RowVersion before/after with the same SELECT; they remain unchanged. Only the separately labelled runtime rendering metadata may differ. This tests new attribution, not previously accepted view-only authorization.

**PASS:** approved user/timestamp semantics are met visibly, with stable frozen clinical content. **FAIL:** wrong actor, substituted clinical timestamp, request values trusted from client, altered stored artifact, or false claim of completed printing. **DEFERRED:** semantic decision, implementation, account or comparison access unavailable. **Cleanup:** sign out and retain PDFs; no record mutation or deletion.

### A3 — Every-page demographics and genuine signature representation

**Prerequisites:** A1-like dedicated fixture and approved signature source/workflow; repeated-page implementation deployed. If signature representation is unresolved, that part is DEFERRED; do not create a fake image or mark an unsigned prescription signed.

1. As Prescriber A create a second prescription for the same fictional patient with A1 values and proposed Notes to pharmacist set to `TEST ONLY — MULTIPAGE ` followed by the text `Mock note line for page review. ` repeated **25 times** (within proposed 1000-character notes limit). Keep Directions from A1; do not change prescribed instructions to create artificial clinical content.
2. Save/reopen and finalize. Use the separately approved genuine test signature workflow only if available and authorized; otherwise stop the signature portion as DEFERRED.
3. Open View/Print, open the PDF viewer's print preview at **Letter, 100%**, with browser-added headers/footers disabled. Record page count and inspect every page: all recorded demographic information and the authorized clinician signature representation must repeat, remain legible and not overlap medication text. Check visible attribution as designed.
4. If the valid fixture remains one page, record multipage DEFERRED with the page count; have the next verification design supply a supported longer fixture within approved limits rather than editing PDF bytes, shrinking paper or inventing a batch-print feature. Normal PDF generation does not by itself prove a multipage condition.

**PASS:** actual two-plus-page output satisfies every-page demographics/signatures without fabrication. **FAIL:** missing/clipped identity/signature on any page or unauthorized/false signature. **DEFERRED:** approved signature, adequate valid multipage fixture or viewer unavailable. **Cleanup:** preserve finalized fixture/output; no physical clinical supply, deletion or signature reuse outside approved tests.

### A4 — New snapshot freezing after contact changes

**Prerequisites:** A1 saved new-version JSON/PDF; isolated synthetic patient and original clinic settings recorded; administrator and observer access. This evaluates newly added contact snapshots, not repeats of legacy artifact verification.

1. Administrator edits only the dedicated synthetic patient's Phone to **416-555-0103** through patient Edit, saving normally; changes isolated clinic Phone to **416-555-0104** at `/ClinicConfiguration`, saving normally. Record UTC windows and original values.
2. As Prescriber A reopen **the existing A1 artifact**. Original frozen phone numbers **416-555-0101 / 416-555-0100** remain in its clinical content even though current chart/settings show new values. Current print-request metadata may update separately.
3. Observer confirms A1 ArtifactJson, ArtifactUid and prescription RowVersion unchanged using the SELECT below. Do not regenerate/backfill historical signed artifacts or rerun correction/concurrency tests.

**PASS:** new contact fields are frozen without blocking legitimate UI contact edits. **FAIL:** original artifact silently picks up live values or stored JSON changes. **DEFERRED:** no isolated fixture, expected baseline or authorized comparison access. **Cleanup:** restore patient/clinic values via their supported UI, retaining both changes' audit history; retain A1.

### Complete read-only SQL for A1/A2/A4 (future use only)

Run in **Tenant A's actual clinical database**, using actual captured fixture UIDs. Do not run in platform/Auth. No Tenant B query is needed: isolation tests are already accepted and not proposed again. Every selected column exists in the currently inspected repository schema; proposed new form/notes values are inspected inside ArtifactJson until a later migration is authorized, not queried as invented existing columns.

```sql
DECLARE @PatientUid uniqueidentifier = NULL; -- replace NULL with quoted actual patient UID
DECLARE @PrescriptionUid uniqueidentifier = NULL; -- actual prescription UID
IF @PatientUid IS NULL OR @PrescriptionUid IS NULL
    THROW 50000, 'Supply actual synthetic fixture UIDs.', 1;

SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey
FROM dbo.TenantDatabaseIdentity;

SELECT PatientUid, HealthCardNumber, PhoneNumber, AddressLine1,
       AddressLine2, City, Province, PostalCode
FROM dbo.Patient WHERE PatientUid = @PatientUid;

SELECT LegalName, Phone, AddressLine1, AddressLine2, City,
       ProvinceState, PostalCode
FROM dbo.ClinicProfile WHERE ClinicProfileId = 1;

SELECT x.PatientUid, x.PrescriptionUid, x.Status, x.RowVersion,
       x.StartDate, x.StrengthValue, x.StrengthUnit, x.DoseAmount, x.DoseUnit,
       x.CreatedBy, x.CreatedAtUtc, x.FinalizedBy, x.FinalizedAtUtc,
       a.ArtifactUid, a.CreatedBy AS ArtifactCreatedBy,
       a.CreatedAtUtc AS ArtifactCreatedAtUtc, a.MimeType, a.ArtifactJson
FROM dbo.PatientPrescription AS x
LEFT JOIN dbo.PatientPrescriptionArtifact AS a
  ON a.PatientUid = x.PatientUid AND a.PrescriptionUid = x.PrescriptionUid
WHERE x.PatientUid = @PatientUid AND x.PrescriptionUid = @PrescriptionUid;

SELECT l.AuditLogId, l.UserId, u.AuthSubjectId, u.Username,
       p.PatientUid, l.ActionName, l.EntityName, l.EntityId, l.CreatedAt
FROM dbo.AuditLog AS l
LEFT JOIN dbo.ApplicationUser AS u ON u.UserId = l.UserId
LEFT JOIN dbo.Patient AS p ON p.PatientId = l.PatientId
WHERE p.PatientUid = @PatientUid
  AND l.EntityName = N'PatientPrescription'
  AND TRY_CONVERT(uniqueidentifier, l.EntityId) = @PrescriptionUid
  AND l.ActionName IN (N'PrescriptionDraftCreated',
                       N'PrescriptionDraftUpdated', N'PrescriptionFinalized')
ORDER BY l.CreatedAt, l.AuditLogId;
```

Expected: correct tenant row, one patient/profile, one scoped finalized prescription/artifact. Compare saved ArtifactJson byte-for-byte before/after A2/A4; it contains the recorded A1 clinical values and future-version field names documented by the eventual implementation. Artifact storage MimeType remains JSON; response is PDF. For A1, expect one create and finalize event and one update per actual edit-save, correlated by UID, actual actor AuthSubjectId and recorded UTC action windows; exclude prior baseline events. No print event is invented or expected from this query. No rows is not PASS; identify missing/wrong-scope fixture. No SQL is executed in Step 91.

## 7. Step 92 readiness and retained qualifications

**Not ready for an unqualified complete PC04.05 output/signature implementation.** Exact clause text is now available; remaining blockers are the acceptable signature representation/source and interpretation of “each user and the timestamp of the prescription” for print/reprint. Smallest clarification: record reviewer/product-owner decisions on those two points, including whether a valid handwritten-paper route is acceptable and how signatures actually appear on every page. No new authoritative signature method can be inferred from this clause.

The confirmed **content/snapshot subset is design-ready** for separately authorized implementation: reuse existing contact/start/strength/dose data; add only independent form/notes support; version future snapshots; preserve old artifacts; render explicit content and repeated demographics. Full Step 92 scope must incorporate the clarified signature/attribution decisions before claiming whole-requirement readiness. Suggested later branch: `feature/step-92-pc04-05-prescription-print`; not created. No implementation starts here.

Preserve Step 89's qualified PC04.16 closure and accepted manual security/audit/concurrency evidence; no repetition. Preserve specification release applicability uncertainty, numeric/default engineering distinctions, historical PDF byte-identity limitation, historical automated-gate qualifications, deferred PC10.02 audit and all prior PC08/PC09/PC10.01 limitations. OntarioMD acceptance remains external. Inventory stays **11 mandatory-gap IDs / three packages**; PC04.05 is not SATISFIED — VERIFIED.

Documentation checks are limited to clause transcription against extracted source text, existing source/schema/link consistency, whitespace and Git scope. Temporary source extraction is not a test/build or new PDF acceptance result. Stop after this report; no manual execution requested.
