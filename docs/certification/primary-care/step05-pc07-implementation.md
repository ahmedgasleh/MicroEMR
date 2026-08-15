# Step 05 PC07 implementation

## Requirements addressed

| Requirement | Previous status | Status after this branch | Implementation |
|---|---|---|---|
| PC07.01 | PARTIAL | PARTIAL | Added past medical/surgical history to the existing patient CPP summary while preserving existing authoritative cards. |
| PC07.03 | MISSING | NEEDS RUNTIME VERIFICATION | Added a complete structured Medical/Surgical history vertical slice. |
| PC07.10 | PARTIAL | PARTIAL | Added chart-level record/update/archive for the new category; encounter-integrated management remains absent. |

## Authoritative data sources reused

- Problems remain sourced from Patient Problems.
- Allergies/adverse reactions remain sourced from Patient Allergies.
- Medication treatment remains sourced from Patient Medications, including its active/discontinued state.
- Demographics, encounters, documents, alerts, and vitals remain sourced from their existing modules.

No CPP-specific copies of those records were created.

## New clinical domain

`PatientClinicalHistory` is the authoritative structured source for past medical and surgical history. Each entry has a constrained Medical/Surgical type, description, optional relevant date, Active/Archived status, created/updated actor and timestamp, and row version. Archive retains the clinical record; no delete operation exists.

## Repository and database evidence

Migration `0041-patient-clinical-history.sql` creates the table, patient/user foreign keys, constrained type/status, patient-scoped list/get/create/update/archive procedures, row-version conflict handling, and actor-attributed `AuditLog` entries. Update audit includes old/new structured values. `PatientClinicalHistoryRepository` opens connections only through `ITenantSqlConnectionFactory`.

## API and UI

- `GET /api/patients/{patientUid}/clinical-history` lists patient-scoped history.
- Create, update and archive endpoints use the same route patient and never accept tenant or actor selectors.
- The patient Summary adds a Past Medical and Surgical History card.
- A dedicated patient-chart tab supports filtering, create, edit and retained archive using the existing Bootstrap style.
- Patients without history receive a clean empty state.

## Security, audit and concurrency

- Read requires `Patients.View` at API and Web layers.
- Create/update/archive require `ClinicalData.Manage` at API and Web layers.
- UI mutation actions follow the effective manage permission.
- The clinical actor comes from the existing resolved actor context.
- Stored procedures bind every resource operation to patient plus history UID.
- Tenant selection remains centralized in `ITenantSqlConnectionFactory`.
- Update/archive use row-version checks under update/hold locks and do not silently overwrite.
- Create/update/archive are atomically audited; archived records are retained.

## Tests added

`CumulativePatientProfileCertificationTests` verifies structured validation, valid Medical/Surgical values, blank/type/future-date rejection, patient/actor/status propagation, concurrency behavior, API/Web permissions, tenant-scoped repository construction, patient-scoped SQL, immutable audit/retention rules, summary integration, preservation of existing authoritative cards, and empty state. The canonical manifest test now expects migration `0041`.

## Runtime verification required

1. Open an existing patient with problems, allergies and medications; confirm those summary cards remain correct.
2. Confirm a patient with no history shows a clean empty history state.
3. Add Medical and Surgical entries with and without relevant dates.
4. Confirm both appear in the dedicated tab and CPP summary after refresh.
5. Edit an active entry and verify persistence, actor, timestamp and update audit old/new values.
6. Open one entry in two sessions and confirm the stale edit is rejected.
7. Archive an entry and confirm it leaves the active summary but remains available under Archived/All.
8. Verify a `Patients.View` user can read but cannot see or invoke mutations without `ClinicalData.Manage`.
9. Verify a permitted user can create/edit/archive.
10. Substitute Patient B in list/update/archive requests for Patient A's history UID and confirm denial/not-found.
11. Repeat access checks in another tenant context.
12. Confirm existing patient-chart tabs still function normally.

Do not perform destructive production testing.

## Remaining PC07 gaps and dependencies

- PC07.01: family history, immunization summary, risk factors and special needs are still missing.
- PC07.04: structured family history.
- PC07.07: structured risk factors/social history.
- PC07.08: discrete special needs beyond chart alerts.
- PC07.09, PC07.11 and PC07.12: ordering and persistent layout customization.
- PC07.10: remaining categories and encounter-integrated diagnosis/procedure/medication management.
- PC07.13: selectable one-operation CPP print with letterhead and page numbering.
- Immunizations remain a future dedicated certification dependency; this branch does not create an immunization module.
- Medication Management remains authoritative for medication behavior; this branch does not redesign prescribing or reconciliation.

## Recommended follow-up split

- **Step 05B:** family history plus risk factors/social history as separate structured domains.
- **Step 05C:** special needs and remaining CPP summary aggregation.
- **Step 05D:** category ordering/customization and CPP print after all mandatory categories exist.
