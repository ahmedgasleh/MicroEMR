# Step 26A — Clinical Data Migration Validation Foundation

Date: 2026-08-24

Branch: `feature/ontariomd_certification_step26a_data_migration_validation_foundation`

Status: **Internal validate-only foundation implemented; no clinical import or export**

## Scope and specification boundary

This slice implements the validate, stage, report, and idempotency foundation designed in Step 26. Exact OntarioMD Data Migration 5.1 clauses, schemas, dictionaries, examples, and validation material remain unavailable. The canonical model is internal and transport-neutral; it is not an OntarioMD interchange format or conformance claim. No vendor adapter, archive upload, UI, background import, clinical import, or export is included.

## Tenant migration 0048

`0048-clinical-data-migration-validation-foundation` creates only tenant-local migration operational/evidence structures:

- `ClinicalDataMigrationBatch`
- `ClinicalDataMigrationStagedPatient`
- `ClinicalDataMigrationStagedProblem`
- `ClinicalDataMigrationValidationIssue`
- governed begin, match, stage, issue, complete, report, and paged-issue procedures

The batch states are `Created`, `Validating`, `ValidationFailed`, and `Validated`; this slice creates batches directly in `Validating`. The only permitted mode is `ValidateOnly`. Import states do not exist.

The batch records source system/version, package UID/schema version, SHA-256 fingerprint, initiating tenant-local actor, validation times, counts, status, and row version. It contains no package title or description likely to leak PHI.

Patient and Problem staging are domain-specific rather than EAV. Every staged row retains batch, bounded source-system/object/patient identities, source timestamps, source-author text snapshot, record type, validation state, and issue counts. Patient staging additionally records the mapping decision and optional target patient candidate. External authors are never mapped to or provisioned as `ApplicationUser` accounts.

## Canonical model and adapter boundary

`ClinicalMigrationPackageV1` is a versioned internal DTO containing package metadata, patients, and problems. It carries only current MicroEMR demographic and Problem List concepts. It does not expose SQL entities or assume XML, CSV, JSON, ZIP, or any vendor syntax.

`IClinicalDataMigrationPackageAdapter<TPackage>` defines the future external-to-canonical conversion boundary. Step 26A does not register or expose a production adapter. The validation API accepts the canonical administrative contract directly.

## Fingerprint and replay contract

The fingerprint is lowercase SHA-256 over a deterministic UTF-8 canonical JSON representation generated internally. It includes canonical schema version, package UID, normalized package metadata, and every supported patient/problem value and source-provenance field. Records are sorted by normalized source patient ID and source object ID. Strings are trimmed, blank strings normalize to null, dates use `yyyy-MM-dd`, and source timestamps use UTC round-trip form. Validation/server timestamps and batch UIDs are excluded.

Within one tenant, `(SourceSystem, PackageFingerprint)` and `(SourceSystem, PackageUid)` are unique. An exact replay returns the existing batch/report and does not duplicate staging or audit. Reuse of a package UID with changed content fails. A different source-system namespace may use the same source IDs/package UID independently. Batch/source-object indexes support duplicate detection and evidence queries; intentional duplicate input rows can both be staged and marked invalid instead of causing an unreported constraint failure.

## Demographic validation and matching

Patient staging supports source identity/timestamps/author, chart number, health card/version, first/middle/last/preferred names, DOB, sex at birth, gender identity, primary/alternate phones, email, address lines, city, province, postal code, and country code.

Required validation is source patient identity, first/last name, and non-future DOB, with current field bounds. Mapping classifications are:

- `ReadyToCreate`: no deterministic or demographic candidate; no patient is created.
- `MappedExisting`: an existing prior source mapping or exactly one strong health-card match.
- `RequiresReview`: ambiguous strong match or name/DOB demographic candidate.
- `Invalid`: required or identity validation failed.

Name plus DOB is warning-only candidate evidence and never automatically maps or merges a patient. A package-supplied tenant or target UID cannot select the database.

## Problem validation and relationships

Problem staging supports source identity/timestamps/author, patient source ID, problem name/description, onset date, current `Active`/`Resolved` status, and resolved date. It does not invent diagnostic coding.

A Problem is valid only when its source patient staged as `ReadyToCreate` or `MappedExisting`. Missing, invalid, or review-pending patient relationships produce `UnknownSourcePatient`. Problem name, supported status, and date consistency are validated. No `PatientProblem` row is created.

## Governed validation codes

Package codes include `UnsupportedSchemaVersion`, `MissingSourceSystem`, `MissingPackageUid`, `PatientLimitExceeded`, and `ProblemLimitExceeded`.

Patient codes include `MissingSourcePatientId`, `DuplicateSourcePatientId`, `MissingRequiredPatientField`, `InvalidDateOfBirth`, `AmbiguousPatientMatch`, and `PossibleDemographicMatch`.

Problem codes include `MissingSourceProblemId`, `DuplicateSourceProblemId`, `UnknownSourcePatient`, `MissingProblemDescription`, `InvalidProblemStatus`, and `InvalidProblemDate`.

Persisted issues contain only code, severity, record type, bounded source object reference, a redacted message, and timestamp. Canonical content, names, health cards, problem text, SQL, stack traces, and secrets are not copied into error messages or operational logs.

## Structured report and API

The report returns batch UID, source system, package UID, fingerprint, batch status, total/valid/warning/failed counts, counts per Patient/Problem record type, coded issue summary, and whether an existing batch was reused. Issue details are paged and the staged PHI set is never returned by default.

Validation-only API routes:

- `POST /api/data-migration/validate`
- `GET /api/data-migration/batches/{batchUid}`
- `GET /api/data-migration/batches/{batchUid}/issues?page=1&pageSize=50`

The canonical request is limited to 5 MiB. Configurable `ClinicalDataMigration:MaxPatients` and `MaxProblems` default to 1,000 and 5,000; startup bounds cap configured values at 10,000 and 50,000. Validation is synchronous and bounded in this slice. Real import still requires durable background execution.

## Authorization, tenant isolation, and audit

All routes require authenticated tenant permission `Users.ManageAccess`. This existing tenant-administrative boundary avoids a new platform migration or IAM redesign and ensures `ClinicalData.Manage` alone is insufficient. A dedicated future `DataMigration.Manage` permission remains recommended once its catalog/bootstrap design is approved.

All persistence uses `ITenantSqlConnectionFactory`; no tenant UID or connection selector exists in the canonical request. Batch, staging, issues, and PHI therefore remain inside the already resolved tenant database. Platform storage is unchanged.

The initiating authenticated tenant-local clinical actor is stored on the batch and used for exactly one tenant `AuditLog` event: `DataMigrationValidated` or `DataMigrationValidationFailed`. Audit JSON contains batch UID through entity identity plus source system, fingerprint, status, and counts; it contains no staged patient/problem payload. There is no per-record audit storm. A non-interactive migration service actor is deliberately deferred until clinical writes exist.

## Dry-run guarantee

The validation service depends only on `IClinicalDataMigrationRepository` and options. It has no patient, Problem List, or other clinical mutation repository dependency. Migration 0048 contains no insert, update, or delete against Patient, PatientProblem, PatientAllergy, PatientMedication, PatientImmunization, PatientEncounter, PatientResult, PatientDocument, or PatientFile. It only reads Patient for deterministic candidate matching and writes migration-specific structures plus one administrative audit event.

Tests enforce that boundary and verify service dependency shape, migration SQL, source provenance, deterministic fingerprints, replay reuse, source-system namespacing, patient matching, relationship validation, governed codes, counts, limits, tenant repository construction, administrative authorization, and absence of an invented OntarioMD/file adapter.

## Retention and security boundary

Staging contains PHI and relies on the tenant database's data-at-rest, backup, access-control, and monitoring protections. Package contents are not operationally logged. Automatic staging cleanup is intentionally absent; an approved evidence/PHI retention policy is still required before production use. The API does not accept file paths, archives, source credentials, connection strings, or tenant selectors.

## Runtime verification

Automated service/runtime simulation uses non-production canonical sample data and verifies valid, mapped/review, malformed, relationship-failure, count, provenance, and replay behavior without clinical repository access.

Repository migration-source verification loads and parses all 49 manifest entries through 0048 and verifies stable hashes. Live SQL fresh provisioning and 0047-to-0048 upgrade were attempted as an environment precondition but could not begin: DatabaseTool reported `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.` No database was changed. These two live gates remain **NOT VERIFIED** and require the existing disposable-tenant connection/configuration issue to be resolved.

## Verification record

| Gate | Result |
|---|---|
| Focused Step 26A tests | PASS — 8/8 |
| API regression tests | PASS — 694/694 |
| Auth regression tests | PASS — 30/30 |
| Release build | PASS — 0 warnings, 0 errors |
| Manifest/source/parser/hash | PASS — 49 ordered migrations through 0048 |
| Prior tenant migrations | PASS — 0000–0047 unchanged |
| Platform migrations | PASS — unchanged through 020 |
| Live fresh provisioning | NOT VERIFIED — configured SQL/TLS connection unavailable |
| Live 0047-to-0048 upgrade | NOT VERIFIED — configured SQL/TLS connection unavailable |
| Live API/database sample | NOT VERIFIED — migration could not be applied to a disposable tenant |

## Explicit exclusions and next step

There is no clinical write, import service actor, external file parser, upload workflow, UI, export, background job, cleanup/delete, fuzzy automatic merge, platform PHI storage, platform migration, or migration 0049.

The preferred next step is specification acquisition and evidence clarification before a production import format is selected. After the exact Data Migration 5.1 direction, format, mandatory domains, and audit expectations are known, Step 26B may implement the first controlled demographics/Problem List import using this staged evidence and a dedicated migration service actor.

**Internal clinical data migration validation foundation implemented. OntarioMD Data Migration 5.1 compliance remains interpretation-dependent because exact specification/validation materials are not currently available.**
