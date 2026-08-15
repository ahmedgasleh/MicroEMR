# Data current state

## Database topology

- `MicroEMR_Auth`: ASP.NET Core Identity and OpenIddict users, roles, applications, authorizations, scopes, and tokens (`ApplicationDbContext`).
- `MicroEMR_Platform`: tenants, database assignments, memberships, tenant roles, access profiles/permissions/overrides, platform audit, and provisioning metadata (`db/platform/*.sql`). It is documented as containing no clinical records.
- One SQL Server tenant clinical database per tenant: patient/clinical/scheduling data, clinical actor mapping, `AuditLog`, `SchemaMigration`, and `TenantDatabaseIdentity`.
- External file bytes: `LocalPatientFileStorage`; SQL `PatientFile` stores metadata and storage key.

All clinical repositories inspected are under `MicroEMR.Infrastructure` and obtain connections through `TenantSqlConnectionFactory`. Data changes are implemented through stored procedures in the reviewed feature assets and migrations.

## Major data domains

| Domain | Tables / storage | Procedures / repository evidence |
|---|---|---|
| Demographics | `Patient` | `Patient_*`; `PatientRepository` |
| Allergies | `PatientAllergy`, `AuditLog` | `PatientAllergy_*`; `PatientAllergyRepository` |
| Medications | `PatientMedication`, `AuditLog` | `PatientMedication_*`; `PatientMedicationRepository` |
| Problems | `PatientProblem` | `PatientProblem_*`; `PatientProblemRepository` |
| Vitals | `PatientVital` | `PatientVital_*`; `PatientVitalRepository` |
| Results | `PatientResult` | `PatientResult_*`; `PatientResultRepository` |
| Tasks | `PatientTask` | `PatientTask_*`; `PatientTaskRepository` |
| Chart alerts | `PatientChartAlert` | `PatientChartAlert_*`; corresponding repository |
| Encounters | `PatientEncounter`, history, addendum | `PatientEncounter_*`; `PatientEncounterRepository` |
| Documents/templates | `PatientDocument`, content/structured data, templates/versions, output artifacts | `PatientDocument_*`, `DocumentTemplate_*`; document/template/output repositories and services |
| Patient files | `PatientFile` metadata; external bytes | `PatientFile_*`; `PatientFileRepository`; `LocalPatientFileStorage` |
| Referrals | `PatientReferral`, `PatientReferralDocument` | referral and linkage procedures/repositories |
| Scheduling | resources, appointments, blocked time, `AppointmentHistory` | scheduling procedure set and repositories |
| Clinic settings | `ClinicProfile` | `ClinicProfile_Get/Save`; repository |
| Identity/access | Auth Identity/OpenIddict; platform membership/profile tables; tenant `ApplicationUser` | auth stores, platform procedures/repositories, clinical-user repository |
| Audit | `AuditLog`, `PlatformAuditEvent`, domain history tables | procedure-level writes and platform administration SQL |

## Import and export

The identified user-facing export is appointment-status CSV (`AppointmentReportsController` / `ReportsController`, guarded by `Reports.Export`). No whole-patient, whole-clinic, CDS-S, portable attachment, or audit export facility was located. No bulk patient/clinical import, field mapping, validation, reconciliation, reject report, or migration rehearsal workflow was found.

`MicroEMR.DatabaseTool`, the manifest, and provisioning services apply schema assets and track `SchemaMigration`; these are deployment/schema facilities, not evidence of an EMR data-migration product capability.

## Document and file storage

Structured/generated documents reside in SQL tables with template versioning and structured data. Clinical output artifacts use storage abstractions and migration `0038`. Uploaded patient-file metadata resides in SQL while bytes are written beneath a configured local root with generated storage keys. Path canonicalization prevents rooted/path-escape keys; upload limits are configured. Archive/restore changes metadata status rather than deleting the clinical record.

Runtime/operations must establish storage durability, encryption, content scanning, MIME validation behavior, backup/restore inclusion, orphan cleanup, availability, retention, and artifact integrity.

## Migration sequence health

### Tenant clinical sequence

`db/tenant-clinical/manifest.json` is the authoritative ordered inventory:

- `0000-tenant-metadata` maps to `db/tenant-clinical/migrations/0000-tenant-metadata.sql`.
- `0001`–`0013` map deliberately to root assets from `db/initial.sql` through `db/scheduling_stored_procedures.sql`.
- `0014`–`0038` map to consecutively numbered files in `db/tenant-clinical/migrations`.
- Highest migration ID: `0038-clinical-output-artifacts`.
- Duplicate IDs: none found.
- Manifest numbering gaps: none found (`0000` through `0038`).
- Missing manifest-referenced scripts: none found during repository inspection.

The apparent directory filename jump from `0000` to `0014` is explained by the manifest's root-script mappings and is not an unsafe sequence gap.

### Platform sequence

Platform scripts are consecutively named `001_create_platform_database.sql` through `013_access_security_stabilization.sql`. `003` and `005` are documented optional local seeds, but sequence numbers are unique and complete. Highest platform version: `013`. No duplicate or suspicious numbering gap was found.

No migration asset was edited, renamed, reordered, replaced, or reformatted in this step. Applied-ledger contents and checksums require database/runtime verification; source inspection cannot prove deployed databases match repository assets.

## Retention, deletion, and archive behavior

- Core interfaces include `ISoftDelete`; patient and legacy scheduling reads use `IsDeleted` filters in identified SQL.
- Patient files support `Active`/`Archived` and explicit archive/restore, with audit entries.
- Tenant lifecycle includes `Archived` status.
- Encounter addenda/history and appointment history preserve changes.
- Referral-document unlink uses an HTTP DELETE route, but detailed SQL review is needed to establish whether it removes only linkage rather than clinical content.
- Storage `DeleteAsync` is used for failed-upload/output cleanup; it is not evidence of a user-facing clinical deletion workflow.

System-wide retention schedules, legal holds, purging after retention, backup retention, subject correction handling, and proof that every clinical domain avoids physical deletion require detailed code, runtime, database-permission, and operational review.

