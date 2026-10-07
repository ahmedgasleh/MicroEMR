# Step 59: PC10.01 designated patient alternative-contact content

Date: 2026-10-07. Scope: the designated patient alternative-contact omission selected in [Step 58](93-step58-pc09-08-evidence-closure-next-gap.md).

## Result

**DESIGNATED PATIENT ALTERNATIVE-CONTACT CONTENT — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION.**

**PC10.01 remains PARTIAL.** Selected specialist consultation/external-report **content beyond titles/types remains OPEN**, untouched by this step. No whole requirement/package closes; Step 58's 20 open mandatory IDs across 7 packages are not reduced. Earlier reports and their release-applicability qualifications remain intact. This is not full CDS-S or OntarioMD product acceptance.

## Authoritative requirement and exact fields

The local OntarioMD **Primary Care Baseline - 1.7 Requirements.pdf**, Version 1.7 Final, PC10.01 (p.35), requires patient alternative-contact information in the referral letter, alongside the previously implemented required content, and preservation of the original letter despite subsequent changes. PC01.04 (p.11) defines a contact as a person designated by the patient for specific situations, requires multiple contacts with one or more purposes, and explicitly refers to the CDS-S Patient Alternative Contact elements. The patient's alternate telephone number is not this source.

Baseline source: `D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf`.

Only the directly referenced DE03 section was read from the local **EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx**, in `Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10`, under the same specification root. Baseline text was extracted locally; the dictionary was inspected as read-only ZIP/XML. No certification-wide source inventory or standards reanalysis was performed.

| Data element | Supported contact attribute |
| --- | --- |
| DE03.001 | Contact First Name |
| DE03.002 | Contact Last Name |
| DE03.003 | Contact Purpose, one or more; Emergency Contact and Substitute Decision Maker supported |
| DE03.004 | Contact Residence Phone |
| DE03.005 | Contact Cell Phone |
| DE03.006 | Contact Work Phone |
| DE03.007 | Contact Work Phone Extension |
| DE03.008 | Contact E-Mail Address |
| DE03.009 | Contact Note |

These elements are marked mandatory for support in the dictionary. This does not establish that every patient must have a contact or that every contact must have every phone/email/note populated. No relationship element is specified here, so no relationship field was invented. Patient contact collection remains optional. Once an entry is added, the editor requires first name, last name and a supported purpose to identify the designated person; telephone/email/note values remain optional. Validation applies bounded lengths and existing-style phone/email annotations. There is no separate emergency-contact module.

## Patient source, UI and persistence

Targeted inspection found no existing structured designated-person source in Patient contracts/schema or demographic contact workflow. Existing PhoneNumber and AlternatePhoneNumber are patient numbers. A patient-level collection was therefore added to the Application and Web registration, demographic edit and patient-details DTOs.

The existing registration and demographic forms share a compact optional contact editor with add/remove controls. Each person can have both supported purposes. TypeScript keeps posted collection indices, field IDs and labels aligned when contacts are added or removed. Existing Web -> API -> Application -> Infrastructure paths carry the structured collection; no new endpoint or controller business logic was added.

New additive tenant migration **0066-patient-alternative-contacts.sql**, registered after inspected manifest sequence 0065, adds only nullable `dbo.Patient.AlternativeContactsJson NVARCHAR(MAX)` and a valid-JSON-array check. JSON provides multiple structured contacts and multiple purposes without a separate contact subsystem or unrelated schema changes. Existing records require no backfill.

The new migration replaces only these procedure definitions:

- `dbo.Patient_GetByUid`: returns the contact source with existing patient context and non-deleted filtering.
- `dbo.Patient_Create`: accepts and stores contacts, includes them in the existing creation audit.
- `dbo.Patient_UpdateDemographics`: accepts contacts, includes old/new structured contacts in the existing demographic audit, retains patient UID/non-deleted/RowVersion predicates and concurrency error 51021.

The optional new procedure parameter is appended to preserve existing parameter positions. The Infrastructure repository passes a typed JSON parameter through the existing tenant SQL connection factory and deserializes the patient-details result. Search/list DTOs and queries do not expose additional contact information. A null/omitted update value preserves the existing contacts for older API clients; explicit `[]` clears the live collection through the same audited update. Historical audit records and referral artifacts remain preserved. Existing migrations and root historical procedure scripts were not edited. No physical deletion or EF migration was introduced.

**Deployment prerequisite:** apply migration 0066 through the existing tenant migration workflow before using the new application against that tenant. No database was connected to or migrated during this step.

## Referral composition and historical preservation

The existing referral composition pipeline reads the current patient collection for Draft previews and Mark Sent. It prints each contact's name and purpose(s), then populated labelled residence/cell/work phones, work extension, email and note. Multiple contacts are retained. The shared print layout HTML-encodes the text; no separate renderer or print-only implementation was introduced. Patient-source UID is checked against the referral's patient before composition.

Mark Sent includes the same contact text in generated output and the structured collection in the existing `SnapshotJson`. The existing atomic finalization/artifact storage path and SHA-256 generation remain unchanged. Opening/downloading a historical sent letter still reads preserved artifact bytes rather than current demographics or a freshly rendered letter. Later patient edits affect a new Draft, while the previous letter and serialized contact snapshot remain unchanged.

A patient with no contacts shows the existing **Not recorded** representation. Patient AlternatePhoneNumber is never substituted. Registration, editing, preview and sending remain possible without a contact. Clinical selections, recipient/provider fields, follow-up, response/close workflow and supporting-document title/type composition were not redesigned.

## Security, audit and concurrency

Patient and referral permissions, authentication and clinical actor resolution remain on existing routes. Contact mutations use the existing create/update actor and stored-procedure audit transaction. The original RowVersion conflict behavior is retained. Tenant separation continues through `ITenantSqlConnectionFactory`; no alternate connection or cross-tenant query was added. Referral access remains scoped by patient/referral UID, and the composer additionally rejects a mismatched patient source. Existing supporting-document permission checks remain intact.

The focused tests use repository/service doubles and SQL contract inspection. They establish argument/ownership/concurrency behavior and preservation of source contracts, not live tenant isolation or actual SQL audit execution. Live database persistence, audit, stale-write rejection, permissions and cross-tenant checks remain runtime verification items.

## Focused checks actually run

Final .NET command:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~AlternativeContact|FullyQualifiedName~MissingDemographicsDoNotSubstituteSexOrPatientPhoneForGenderOrAlternativeContact|FullyQualifiedName~PreviewDoesNotReadAnotherPatientsReferral|FullyQualifiedName~SentSelectionsCannotBeReplacedAndRepeatedSendCannotCreateAnotherArtifact' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step59/bin/'
```

**11 passed, 0 failed, 0 skipped.** Five patient-contact tests cover create/read/update contract round trips, actor/RowVersion forwarding, omitted-versus-empty contact updates, invalid/optional contacts, Web mapping/JSON transport, concurrency exception propagation and additive SQL audit/history contracts. Three new referral tests cover all nine contact attributes, multiple contacts/purposes, HTML encoding, source ownership, frozen snapshot/bytes/download, post-send changes and new Draft behavior. Three existing focused regressions cover missing demographics/alternate-phone exclusion, wrong-patient preview and rejection of repeated sends/selection replacement after finalization.

The final test command successfully compiled Core, Application, Infrastructure, API and Web/Razor, plus the test project's DatabaseTool reference and test assembly. No standalone solution build or full test suite was run. Final output contained no reported warnings/errors. Earlier focused build attempts caught an accidental contact assignment in the list-item mapper and an init-only Status assignment in the test fixture; both were corrected before the passing run. The list-item contract remains unchanged.

Frontend command: `node tests/pc10-patient-alternative-contacts.test.cjs` — **3 passed, 0 failed**. Executed generated JavaScript with a small DOM double to verify add, removal/reindexing/value retention and removal of all contacts followed by re-addition. No browser or Playwright run is claimed.

Affected TypeScript compilation, run from `src/MicroEMR.Web`:

```powershell
node node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --rootDir ClientApp --outDir wwwroot/dist --sourceMap ClientApp/patients/alternative-contacts.ts
```

**Passed, exit 0.** Generated JavaScript and source map included. Final diff inspection and `git diff --check` completed without whitespace errors. No additional application dependency was installed. An optional temporary PDF-library install failed; standard-library local extraction supplied the required source text instead.

## Files changed

- Database: `db/tenant-clinical/manifest.json`; new `db/tenant-clinical/migrations/0066-patient-alternative-contacts.sql`.
- Application patients: `src/MicroEMR.Application/Patients/Contracts/PatientAlternativeContact.cs` (new); `CreatePatientRequest.cs`, `UpdatePatientDemographicsRequest.cs`, `PatientDetailsResponse.cs` in the same contracts folder; `src/MicroEMR.Application/Patients/Services/PatientService.cs`.
- Application referral: `src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs`.
- Infrastructure: `src/MicroEMR.Infrastructure/Patients/PatientRepository.cs`.
- Web: `src/MicroEMR.Web/Controllers/PatientsController.cs`; `src/MicroEMR.Web/Models/Patients/PatientAlternativeContact.cs` (new), `CreatePatientRequest.cs`, `UpdatePatientDemographicsRequest.cs`, `EditPatientDemographicsViewModel.cs`, `PatientDetailsResponse.cs` in the same models folder.
- Views: `src/MicroEMR.Web/Views/Patients/Create.cshtml`, `Edit.cshtml`; new `_AlternativeContacts.cshtml`, `_AlternativeContactFields.cshtml` in the same views folder.
- Frontend: new `src/MicroEMR.Web/ClientApp/patients/alternative-contacts.ts`, `src/MicroEMR.Web/wwwroot/dist/patients/alternative-contacts.js` and `.js.map`.
- Tests: new `tests/MicroEMR.Api.Tests/PatientAlternativeContactTests.cs`, existing `tests/MicroEMR.Api.Tests/ReferralLetterCompositionTests.cs`, new `tests/pc10-patient-alternative-contacts.test.cjs`.
- Report: this new file, `docs/certification/94-step59-pc10-01-patient-alternative-contact.md`.

## Manual/runtime verification path

1. Apply tenant migration 0066 through the existing migration workflow and run the updated application.
2. Create or edit a test patient. Add a designated contact with first/last name, one or both purposes, residence/cell/work phone, work extension, email and note. Add a second person to check multiple contacts.
3. Save, reopen demographics and verify all values persisted.
4. Create/open a Draft outgoing referral and preview the letter. Verify the correct patient's contacts, purpose(s) and every populated labelled field. Check the printed/PDF content as well.
5. Mark Sent; open/download the preserved final letter and verify the original contact values.
6. Change the patient's contacts and save. Reopen the Sent letter: it must still show the original values. Compare downloaded bytes/hash if available.
7. Create a new Draft for that same patient: preview must use updated contact values.
8. Remove all contacts and save/reopen. A new Draft must show Not recorded; registration, editing and sending without a contact must still work.
9. Confirm the existing demographic audit includes actor and old/new contact values; make a stale concurrent demographic save and verify rejection without overwriting contacts or adding a successful mutation audit.
10. Check another patient and an authorized second tenant: neither may display the first patient's contacts. Verify existing patient/referral permissions with a restricted account.

Pending evidence: live SQL migration/persistence/audit/concurrency, real PDF layout/printing (including long notes/multiple contacts), authorization/tenant isolation and user-observed post-send historical preservation. Automated rendering doubles use UTF-8 HTML bytes rather than a real PDF engine; they establish composition and preserved-output flow, not visual PDF acceptance.

## Resource/scope confirmation

No repository-wide scan, certification-wide reanalysis, referral redesign, second PC10.01 gap implementation, unrelated test suites or stable PC09 rechecks. The starting working tree was clean; no existing manual/uncommitted edits were overwritten. Earlier certification reports and immutable migrations are preserved. Step 59 stops here; the specialist/external-report content omission is not started.
