# Step 25A — Basic Immunization History

Date: 2026-08-23

Branch: `feature/ontariomd_certification_step25a_basic_immunization_history`

Baseline: current `main` at `829d390`

Status: **Basic local immunization history implemented**; no claim of full PC03 satisfaction

## Delivered scope

Step 25A adds one tenant-local clinical vertical slice using established MicroEMR architecture:

- Tenant migration `0047-patient-immunization-history` and manifest entry.
- `dbo.PatientImmunization` with stable GUID, patient ownership, required vaccine/date/source/status, nullable MVP administration/provenance fields, actor timestamps, terminal correction metadata, and row-version concurrency.
- Five governed stored procedures: list, compound detail, create, compound update, and compound mark-entered-in-error.
- Application contracts/service, Infrastructure repository through `ITenantSqlConnectionFactory`, patient-scoped API, Web API client/proxy, and Patient Chart Immunizations tab.
- Existing `Patients.View` reads and `ClinicalData.Manage` mutations.
- Atomic tenant `AuditLog` events and focused automated certification tests.

No existing migration was changed. Platform migrations remain through 020. Tenant migrations advance from 0046 to 0047.

## Schema and semantics

Required clinical fields are `ImmunizationUid`, `PatientUid`, `VaccineName`, `AdministrationDate`, `SourceType`, `Status`, `CreatedBy`, `CreatedAtUtc`, and `RowVersion`.

Nullable MVP fields are `DoseNumber`, `Route`, `Site`, `LotNumber`, `SourceDescription`, `AdministeredByName`, `EncounterUid`, `Notes`, `UpdatedBy`, and `UpdatedAtUtc`. Entered-in-error records additionally retain reason, actor, and UTC time.

`VaccineName` is a trimmed bounded free-text snapshot. No catalogue or DIN/CVC/SNOMED field was introduced.

Governed source values:

- `ClinicAdministered`: requires `AdministeredByName`.
- `HistoricalExternal`: does not require an encounter or known administrator; `SourceDescription` can identify the external/patient/document source.

Governed status values:

- `Completed`.
- `EnteredInError`.

Historical is provenance, not status. Refused/not-given/pending/scheduled are not administrations and are excluded.

## Validation and lifecycle

- Vaccine and administration date are required; future administration dates are rejected.
- Dose number is nullable and positive when supplied. There is no schedule inference.
- Route/site/lot/source/administrator/notes are bounded plain text.
- Optional `EncounterUid` must identify an encounter for the same patient in the trusted tenant database.
- Create always produces `Completed`; clients cannot supply status or actor.
- Completed records can be updated with row version and atomic audit.
- `MarkEnteredInError` requires row version and reason. It is terminal, cannot be restored or normally edited, and leaves the row visible in history.
- No delete endpoint, procedure, or physical-delete behavior exists.

The resolved tenant-local clinical actor supplies `CreatedBy`, `UpdatedBy`, and correction actor. `AdministeredByName` is a separate clinical fact and is never inferred from the record-entering user.

## API and repository

Routes:

- `GET /api/patients/{patientUid}/immunizations`
- `GET /api/patients/{patientUid}/immunizations/{immunizationUid}`
- `POST /api/patients/{patientUid}/immunizations`
- `PUT /api/patients/{patientUid}/immunizations/{immunizationUid}`
- `POST /api/patients/{patientUid}/immunizations/{immunizationUid}/entered-in-error`

The repository calls only stored procedures through the trusted tenant connection. Detail/update/correction pass both patient and immunization UIDs. The optional encounter association is also patient-bound. No platform/central immunization storage or cross-tenant lookup exists.

## Permissions and Patient Chart

The Patient Chart adds an Immunizations tab without redesigning other tabs. The compact list displays date, vaccine, dose, source, status, administered-by, and permitted actions. Completed and entered-in-error rows remain visible; terminal rows use muted/history styling and have no mutation actions.

Users with `Patients.View` may read. Add/Edit/Mark Entered in Error are disabled or absent without `ClinicalData.Manage`; Web and API independently enforce mutation permission. UI state is not treated as the security boundary.

The compact modal supports only approved fields. Mark Entered in Error uses a distinct reason-required confirmation, never Delete.

## Audit and disclosure boundary

Successful mutations atomically write exactly one existing tenant `AuditLog` event:

- `ImmunizationCreated`
- `ImmunizationUpdated`
- `ImmunizationEnteredInError`

Audit metadata records the entity/identifier, actor, timestamp, bounded state metadata, and correction reason. Full Notes are not copied into audit payloads. Failed SQL mutations cannot commit successful audit events because mutation and audit share a transaction.

No new successful-read event was added. Normal tab loading remains inside the governed `PatientChartOpened` scope. Immunization export/print/integration disclosure auditing remains an interpretation question.

## Automated evidence

`BasicImmunizationHistoryTests` covers:

- clinic/historical validation and positive dose behavior;
- actor propagation, concurrency contract, and terminal lifecycle;
- API/Web read/manage permission attributes and independent server enforcement architecture;
- trusted tenant repository construction;
- source/status/dose constraints and patient/resource compound SQL;
- three governed audit events, retained rows, and no hard delete;
- exactly one 0047 manifest entry, expected index, and no 0048;
- exclusion of manufacturer, expiry, terminology coding, next-dose, refusal/not-given, and DHIR.

The canonical migration-source test expects 48 total migrations (0000–0047), loads every manifest script, parses SQL batches, and calculates stable SHA-256 hashes. This proves repository fresh-provisioning inclusion and governed upgrade ordering. It is not a claim that a live SQL Server 0046 database was upgraded during this run.

## Manual runtime checklist and result

The following non-production checklist is prepared for a migrated test tenant:

1. Open a test patient chart and confirm Immunizations empty state.
2. Create `ClinicAdministered` with administrator and optional dose/route/site/lot; confirm list/detail and actor/audit.
3. Create `HistoricalExternal` without encounter/administrator; confirm source display.
4. Edit a completed record and verify new row version, update actor/time, and one update audit.
5. Mark entered in error with reason; verify row retention, muted terminal display, no edit, and one correction audit.
6. With `Patients.View` but no `ClinicalData.Manage`, verify read succeeds, controls are disabled, and direct API mutation is denied.
7. Attempt patient-B access to patient-A immunization and a cross-patient encounter link; verify no disclosure/mutation.
8. Verify no new read-audit event is emitted solely by tab loading.

Manual browser/live-database runtime verification: **NOT PERFORMED in this automated implementation run**. No test tenant/database credentials or browser session were assumed. This checklist must be attached after operator execution; unperformed gates are not called PASS.

## Interpretation boundary and explicit exclusions

Remaining PC03 interpretation items include vaccine terminology/codes, refusal/non-administration semantics, dose/series requirements, manufacturer/expiry, unknown/partial dates, duplicate reconciliation, immunization-specific disclosure audit, DHIR relationship, and forecasting/reminders if applicable.

Explicitly excluded: DHIR, registry connectivity, forecasting, reminders, CDS, refusals/not-given, inventory, manufacturer/expiry, structured terminology, public-health submission, reporting, bulk import/export, Data Migration, billing, barcode/cold-chain, and mass-immunization workflows.

## Verification record

| Gate | Result |
|---|---|
| Tenant migration | PASS — 0047 exists once and follows 0046 |
| Prior tenant migrations | PASS — 0000–0046 unchanged |
| Platform migrations | PASS — unchanged through 020 |
| Manifest/parser/hash | PASS — 48 ordered unique entries; every script parsed and stable SHA-256 calculated |
| Focused Step 25A tests | PASS — 6/6 |
| TypeScript build | PASS |
| API tests | PASS — 686/686 |
| Auth tests | PASS — 30/30 |
| Release build | PASS — 0 warnings, 0 errors |
| Fresh provisioning | PASS at repository migration-source/parser level; live SQL provisioning not performed |
| Live 0046→0047 upgrade | NOT VERIFIED — operator database run required |
| Manual runtime | NOT VERIFIED — checklist supplied |
| Security defects found | None during implementation |
| Safe to commit | YES after review, subject to the explicitly unperformed live/manual gates; no commit performed |
