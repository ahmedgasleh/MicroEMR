# Step 93: PC04.05 Bounded Prescription Print Implementation

Date: 2026-10-15. Implements the separately authorized [Step 92 design](126-step92-pc04-05-signature-reprint-semantics-decision.md). PC04.05 is **IMPLEMENTED ? NEEDS MANUAL VERIFICATION**, subject to the executed-check results below. Inventory remains **11 mandatory-gap IDs / three packages**. No manual PASS or OntarioMD acceptance is claimed.

## 1. Git checkpoint and boundary

Started on `main`, HEAD `1530f8e`, with only untracked reports 125/126; both are preserved unchanged. Created `feature/step-93-pc04-05-prescription-print-closure` after a sandbox ref-lock denial and successful approved retry. No staging, commits, merges or application database operations. Only scoped prescription/output/SQL/test/documentation files change; existing history is preserved.

## 2. Implementation

- Optional MedicationForm (100 characters) and NotesToPharmacist (1000 characters) round-trip through draft DTOs, shared form/modal edit loading, repository, stored procedures, response and correction copying. Existing Directions/Indication and initial/refill fields remain distinct; bounds are engineering choices.
- New API `POST /api/patients/{patientUid}/prescriptions/{prescriptionUid}/sign-and-finalize` requires Patients.View plus Prescriptions.Prescribe, explicit `confirmSignature: true`, valid current RowVersion and the server-resolved active clinical owner with linked Provider. The dedicated prescribing permission already governs local prescription finalization; unrelated Documents.Sign/Encounters.Sign permissions are not substituted. Normal finalize remains unsigned and backward compatible.
- Web **Sign and finalize prescription** explicitly confirms electronic signing. A new-version snapshot contains trusted signer identity/UTC time plus patient/clinic contacts, explicit strength/dose/form/start/notes and stable preparer metadata. Finalization, snapshot and existing PrescriptionFinalized audit commit together. Correction copies clinical values, never signature state.
- Prescription-only PDF header/footer uses the existing Playwright renderer with reserved Letter-page margins. Every page receives recorded demographics, true electronic signer/time, prepared identity/time, requester/subject, UTC generation time, unique occurrence reference and page number. Long notes remain escaped and can flow across pages. Other document renderer margins/layout stay unchanged.
- Output requests resolve requester from authenticated API claims; no client actor/time is accepted. A transient occurrence is composed with frozen version-2 content. After bytes are generated, a scoped stored procedure records PrescriptionPrintOutputGenerated before returning output. This is generation, not browser receipt/physical printing. Audit failure fails closed. Repeat requests create new occurrences without changing signed JSON, signer or prescription RowVersion. API and Web PDF actions are no-store.
- Legacy/version-1 content uses the existing renderer unchanged, is not relabelled signed, enriched or backfilled, and is never updated. Legacy retrieval also records truthful current generation audit, but its historical PDF layout is unchanged; new visible metadata applies to version-2 output only. No historical signed documents are regenerated in a batch.

Components: Application prescription model/service plus new PrescriptionOutputService; Infrastructure partial repository, PDF renderer and DI; thin API/Web actions; shared prescription form and chart modal/signing controls. Clinical business/composition stays in Application; SQL stays in Infrastructure/procedures.

## 3. Additive SQL and deployment prerequisite

New `0074-prescription-print-signing.sql`, appended once to the tenant manifest:

- Adds only nullable MedicationForm/NotesToPharmacist columns.
- Replaces prescription list/get/create/update procedures to carry those values, preserving owner/state/RowVersion and existing transactional mutation audit.
- Adds PatientPrescription_SignAndFinalize with required confirmation, existing owner/tenant/patient/provider checks, single UTC signature instant, frozen version-2 JSON, finalization/supersession audit and rollback behavior.
- Adds PatientPrescription_RecordOutputGeneration: scopes patient/prescription/artifact, resolves optional clinical UserId from trusted AuthSubjectId, and writes generation event to existing AuditLog including unique AuditEventUid, minimal requester/occurrence metadata and Generated outcome. A view-authorized reader without a clinical mapping is not forced to gain prescribing rights; its subject remains recorded with nullable UserId.

No old migration is changed, no EF migration is introduced, no audit table/signature-image store is added, and no signed-history UPDATE/backfill occurs. Apply 0074 through the established migration workflow to an isolated test tenant before deploying these binaries; no running tenant was migrated here. Existing unsigned Finalize procedure remains compatible and does not create a signature marker.

## 4. Executed checks

| Check | Exact executed result |
| --- | --- |
| Initial focused Step93 selection, sandboxed | **0 passed / 7 failed / 0 skipped**; all failed during fixture setup because sandbox LocalDB startup was unavailable. No assertion PASS claimed. |
| Same seven cases outside sandbox | **0 passed / 7 failed / 0 skipped** during fixture setup: SQL batch attempted to use newly added AuthSubjectId/DisplayName before a compilation boundary. |
| Focused retry after first fixture correction | **0 passed / 7 failed / 0 skipped** during fixture setup: filtered index referenced newly added AuditEventUid in the same batch. |
| Nine-case selection after fixture correction and added policy cases | **0 passed / 9 failed / 0 skipped** during migration setup: malformed new UpdateDraft assignment. Corrected only new migration; historical migrations untouched. |
| Focused print/signing selection after correction | **9 passed / 0 failed / 0 skipped**. Includes intent/policy/owner/provider/version/patient/tenant checks, signed snapshot, requester/audit, failure rollback, field bounds and actual multipage PDF. |
| New targeted signed-correction case | **1 passed / 0 failed / 0 skipped**. Copies new clinical fields, starts unsigned Draft, requires own signature and preserves original artifact on supersession. |
| One final relevant Release build | **Passed: 0 errors, 10 warnings**. All warnings are existing NU1902/NU1903 advisories for System.Security.Cryptography.Pkcs 9.0.13 in referenced projects; no remaining C# warnings. No dependency updates performed. |
| One final Release prescription/output gate | **59 passed / 0 failed / 0 skipped**, 12-second reported test duration, no-build/no-restore. Includes the established 49-case gate plus ten new cases. Overlapping development runs are not summed into another suite. |

Commands:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Step93 --no-restore --filter FullyQualifiedName~PrescriptionPrintTests --verbosity minimal -m:1 -nr:false
# Initial external retry used --no-build; the new signed-correction case was run separately by exact name.
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-restore --verbosity minimal -m:1 -nr:false
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~PatientPrescriptionFoundationTests|FullyQualifiedName~PrescriptionRefillInstructionsTests|FullyQualifiedName~PrescriptionRefillSqlTests|FullyQualifiedName~ClinicalPdfPreviewTests|FullyQualifiedName~PrescriptionPrintTests" --verbosity minimal
```

LocalDB execution used the existing disposable `MicroEMR_Step87_<32 hex>` fixture with name-validated cleanup, never an application database. The focused implementation configuration avoided running-app output locks. Final relevant build includes referenced projects; it is not a full solution build or full API/Auth suite.

A generated synthetic Step93 PDF had **two pages**. A separate read-only PDF text-stream extraction confirmed Patient/Mock clinic identity, electronic signer, requester attribution, UTC and page labels on **both pages**. The automated PDF case also checks real PDF bytes and page count. This is text/page evidence, not visual acceptance of all viewport, long-value or physical-printer conditions; M3 remains manual.

Documentation/manifest checks: 75 unique migration entries, all script paths exist, 0074 last; whitespace/diff scope reviewed. No existing historical automated-gate qualification is erased. No manual PASS is inferred from this green gate.

## 5. Manual acceptance ? not executed

Use only an isolated development Tenant A with 0074 applied through the existing deployment workflow and matching binaries at Web https://localhost:7002, API https://localhost:7003 and Auth https://localhost:7179. Record actual build, database/TenantUid, UTC windows and fixture IDs. Reuse existing authentication; do not repeat PC04.16 security/concurrency/audit acceptance. Never invent IDs or use production data.

### Shared fictional fixtures / setup

1. Reuse established Prescriber A with Patients.View + Prescriptions.Prescribe, active clinical membership and linked active Provider. Record actual account/Auth ID and Provider display name. Reuse the established view-only account with Patients.View allowed and Prescriptions.Prescribe denied. Do not alter a shared profile.
2. If no prescriber fixture exists, an authorized Users.Manage administrator uses `/TenantUserAdministration/Add` to create `rx.print.a@example.invalid`, Physician with clinical provisioning. Users.ManageAccess administrator confirms effective individual permissions. Providers.View/Providers.Manage administrator opens `/Providers/Add`: Rowan / Print-Test, display `Dr. Rowan Print-Test`, ProviderType Physician, phone 416-555-0102; saves and uses Providers ? Link User to link the newly created actual account. No signature image is needed. Record returned IDs rather than inventing them.
3. A Patients.Edit administrator locates a dedicated existing fictional Avery Print-Test (DOB 1990-01-01), or creates it through `/Patients/Create`: first Avery, last Print-Test, DOB 1990-01-01, phone 416-555-0101, address 10 Mock Print Lane, Toronto, ON, M5V 0A1; optional fields blank. Use HCN `TEST-PC0405-ONLY` as a deliberately non-issued test marker only if supported validation accepts it; otherwise obtain an approved non-production fixture identifier or defer that assertion. Do not use a real HCN or bypass validation. Record actual patient UID.
4. ClinicSettings.Manage administrator records original isolated settings, then `/ClinicConfiguration` ? set LegalName `Step93 Mock Clinic ? TEST ONLY`, phone 416-555-0100, address 20 Mock Clinic Lane, Toronto, ON, M5V 0A1, Canada ? Save. Reuse already matching settings where possible.
5. For SQL assertions arrange an authorized read-only connection to this actual Tenant A clinical database, never Auth/platform. Lack of observer access defers database assertions, not a fabricated PASS. No Tenant B query is needed because cross-tenant acceptance is already preserved and not retested here.

### M1 ? Content, explicit signing and frozen metadata

Prerequisites: shared setup, active prescriber session and deployed new UI.

1. Sign in as Prescriber A, select Tenant A normally, open the dedicated patient chart ? Medications ? New Prescription.
2. Enter ProductName `Step93 Mock Medicine ? TEST ONLY`; Product display `Step93 Mock Medicine` (omit strength/form/dose from display deliberately); strength 5 mg; dose 1 tablet; Medication form tablet; route Oral; frequency Once daily; directions `TEST ONLY ? Take one mock tablet daily.`; initial 10 tablets/10 days; two refills, refill 30 tablets/30 days; Indication `TEST ONLY ? display fixture`; Notes to pharmacist `TEST ONLY ? Mock packaging; no real supply.`; prescribed date actual execution date; Start date next calendar date, recorded explicitly.
3. Save Draft, capture prescription UID and reopen Edit. Verify form/notes/start/strength/dose saved. Close edit; click Sign and finalize prescription and cancel confirmation: remain Draft. Click again and explicitly confirm signing.
4. Open View/Print; save this PDF as the baseline. Inspect exact patient/clinic contacts, medication name, explicit strength/unit/form/dose/frequency/start/notes and separate existing dispense/refill values. Footer must show actual linked clinician as electronically signed, signed UTC time, preparer, requesting account/subject and generation UTC/reference. No real prescription supply occurs.
5. Observer runs the read-only query below. One finalization event and one new-version artifact exist for this actual UID; signature user/time in JSON match actual actor and FinalizedBy/FinalizedAtUtc. Include only actual create/update saves when counting lifecycle events.

PASS: exact new values persist and display, cancellation does not sign, explicit confirmed signing produces trusted frozen signature/content and correlated existing lifecycle audit. FAIL: omitted/mislabelled content, false/wrong signature, mutation on cancelled intent or inconsistent snapshot/audit. DEFERRED: missing fixture/deployment/session or observer; name affected assertion. Cleanup: retain synthetic record/PDF/audit; no deletion or signature backfill.

### M2 ? Repeated generation and different requester

Prerequisites: M1 new-version finalized artifact, baseline JSON/RowVersion, separate existing view-only Web session.

1. Prescriber A requests M1 View/Print; save returned PDF and note UTC window/reference.
2. Sign in as the view-only account in a separate Web session, select Tenant A and open the same actual patient/artifact link. Save the second PDF and note its window/reference. This verifies new requester metadata, not repeats of established access denial/permission tests.
3. Compare: requester/account key and reference differ; UTC generation time belongs to each request. Signed clinician/time, clinical inputs and demographic contacts remain stable. Footer says generation, not physical-print success. Confirm Network responses are no-store.
4. Observer compares original artifact JSON/UID and prescription RowVersion unchanged, and correlates two distinct generated events to visible references/requesters in the recorded window. A view-only reader does not become the signer.

PASS: per-request attribution and unique generation rows with frozen clinical state. FAIL: stale requester, wrong signer/time, rewritten artifact or claim of physical printing. DEFERRED: second account, implementation or observation access unavailable. Cleanup: close sessions and retain outputs; do not mutate prescription.

### M3 ? Multipage demographics/signature

Prerequisites: shared setup and explicit signing available; no external signature capture is required by selected engineering design, but OntarioMD acceptance remains qualified.

1. As Prescriber A create another dedicated prescription with M1 values; set pharmacist notes to `TEST ONLY ? MULTIPAGE` followed by 20 newline-separated repetitions of `TEST ONLY ? mock note line for page review.` (stay within 1000 characters). Save/reopen and explicitly Sign and finalize.
2. Open View/Print in a supported PDF viewer; use Letter, 100%, with viewer/browser-added headers/footers disabled. Record page count and inspect every page.
3. Verify complete recorded patient and clinic demographic/contact information, electronic signer/time and output attribution repeat on every page, with no clipped/overlapping content. Inspect long-note continuation and page numbers. Do not send to a clinical pharmacy.

PASS: actual two-plus-page output retains readable repeated identity/signature/attribution. FAIL: any page omits or clips required content, falsely identifies signer or loses note text. DEFERRED: viewer/deployment unavailable or valid fixture stays one page; record page count rather than manufacturing PDF pages. Cleanup: retain synthetic artifact and output, no clinical deletion.

### M4 ? New contact snapshot immutability

Prerequisites: M1 baseline JSON/PDF, dedicated synthetic fixture, original patient/clinic settings recorded, administrators and observer available.

1. Patients.Edit administrator edits only this synthetic patient's phone to 416-555-0103 via patient Edit; ClinicSettings.Manage administrator changes isolated clinic phone to 416-555-0104 via `/ClinicConfiguration`; save normally.
2. As Prescriber A request the existing M1 artifact again. Clinical PDF contacts must remain original 416-555-0101 / 416-555-0100; chart/current settings show updated contacts. Signer/time remain original; only generation metadata changes.
3. Observer confirms original ArtifactJson, ArtifactUid and prescription RowVersion unchanged with query below. Do not run a historical-artifact regeneration or repeat legacy/concurrency tests.

PASS: new contact snapshot is frozen and output occurrence separate. FAIL: old clinical content/signature changes from live data. DEFERRED: isolation, baseline or comparison access missing. Cleanup: restore synthetic patient/clinic settings through supported UI, preserving their audit history; retain prescription and PDFs. Deactivate temporary users only through existing administration if appropriate; never physically delete clinical data.

### Read-only SQL for M1/M2/M4

Run in Tenant A's **actual clinical database**. Replace NULLs with captured real test fixture UIDs and UTC window values. This does not modify data; no database query was run by Codex against an application tenant.

```sql
DECLARE @PatientUid uniqueidentifier = NULL;
DECLARE @PrescriptionUid uniqueidentifier = NULL;
DECLARE @FromUtc datetime2(7) = NULL;
DECLARE @ToUtc datetime2(7) = NULL;
IF @PatientUid IS NULL OR @PrescriptionUid IS NULL OR @FromUtc IS NULL OR @ToUtc IS NULL
    THROW 50000, 'Supply actual synthetic UIDs and UTC window.', 1;
SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId, AppliedAt FROM dbo.SchemaMigration
WHERE MigrationId=N'0074-prescription-print-signing';
SELECT x.PatientUid,x.PrescriptionUid,x.Status,x.MedicationForm,x.NotesToPharmacist,
       x.StartDate,x.FinalizedBy,x.FinalizedAtUtc,x.RowVersion,
       a.ArtifactUid,a.ArtifactJson
FROM dbo.PatientPrescription x LEFT JOIN dbo.PatientPrescriptionArtifact a
 ON a.PatientUid=x.PatientUid AND a.PrescriptionUid=x.PrescriptionUid
WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid;
SELECT l.AuditLogId,l.AuditEventUid,l.UserId,u.AuthSubjectId,l.ActionName,l.Outcome,
       l.RequestCorrelationId,l.NewValue,l.CreatedAt
FROM dbo.AuditLog l LEFT JOIN dbo.ApplicationUser u ON u.UserId=l.UserId
LEFT JOIN dbo.Patient p ON p.PatientId=l.PatientId
WHERE (p.PatientUid=@PatientUid OR l.PatientUid=@PatientUid)
 AND l.EntityName=N'PatientPrescription'
 AND TRY_CONVERT(uniqueidentifier,l.EntityId)=@PrescriptionUid
 AND l.CreatedAt>=@FromUtc AND l.CreatedAt<=@ToUtc
ORDER BY l.CreatedAt,l.AuditLogId;
SELECT PatientUid,PhoneNumber FROM dbo.Patient WHERE PatientUid=@PatientUid;
SELECT LegalName,Phone FROM dbo.ClinicProfile WHERE ClinicProfileId=1;
```

Expected: correct tenant and 0074 row; one scoped finalized prescription/artifact. JSON has PrescriptionOutputVersion=2, SignatureVersion=1 and actual frozen SignerUserId/ProviderUid/time. Signing uses one PrescriptionFinalized event; generation uses distinct PrescriptionPrintOutputGenerated rows with Outcome Generated and AuditEventUid equal to the PDF reference. NewValue contains requester subject/name and artifact/occurrence IDs. Nullable audit UserId is permitted for an authenticated view-only account with no active clinical mapping; requester subject remains available in NewValue. Count only events in recorded actual windows. No row proves physical printing completed. M2/M4 comparisons must preserve JSON and prescription RowVersion. No Tenant B SQL is requested.

## 6. Changed files / final scope

- Application: `DependencyInjection.cs`, `PatientPrescriptions/PatientPrescriptionModels.cs`, new `PatientPrescriptions/PrescriptionOutputService.cs`.
- Infrastructure: `DependencyInjection.cs`, `ClinicalOutput/PlaywrightPdfRenderer.cs`, `PatientPrescriptions/PatientPrescriptionRepository.cs`, new `PatientPrescriptions/PatientPrescriptionOutputRepository.cs`.
- API: `Controllers/PatientPrescriptionsController.cs`.
- Web: `Controllers/PatientPrescriptionsController.cs`, `Services/PatientPrescriptions/PatientPrescriptionApiClient.cs`, `Views/PatientPrescriptions/_Form.cshtml`, `Views/Patients/Details.cshtml`.
- SQL: `db/tenant-clinical/manifest.json`, new `migrations/0074-prescription-print-signing.sql`.
- Tests: `PrescriptionRefillSqlTests.cs` (disposable schema/migration fixture), new `PrescriptionPrintTests.cs`.
- Documentation: this new report only. Existing untracked reports 125/126 preserved. Final status: 12 modified tracked files and seven untracked files, of which two are pre-existing reports. No staging/commit/merge.

## 7. Qualifications and stop point

Retain electronic-attestation acceptance, print-user/timestamp interpretation and source-release applicability qualifications from Steps 91/92; OntarioMD acceptance remains external. Existing finalized artifacts are not retroactively PC04.05-complete. No PDF byte-identity claim is added for regenerated historical outputs. Keep prior PC04.16 closure, user-reported manual evidence, historical Step 46 33/1/0 qualification, deferred PC10.02 audit and other prior limitations. Manual checks above remain unexecuted. No commits/merges; stop after implementation report.

No implementation blocker remains in the bounded slice. Deployment of 0074 and the four documented manual acceptance procedures remain outstanding; no application process or real tenant was changed.
