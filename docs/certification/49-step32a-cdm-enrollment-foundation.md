# Step 32A — CDM Technical Program Enrollment Foundation

Date: 2026-08-26

Certification baseline: `PCON-2024-02`; CDM 4.4

Completion classification: **CDM technical enrollment foundation implemented and controlled `0052 → 0053` migration runtime verified. Fresh-from-blank provisioning and Patient Chart browser verification remain outstanding. No production disease-specific CDM program is active.**

This is not CDM certification completion. Exact CDM 4.4 interpretation, clinical approval, approved disease definitions, terminology/mapping, measurements, goals, intervals, and reporting remain dependencies.

## Migration 0053 and tenant schema

Additive tenant migration `0053-cdm-enrollment-foundation.sql` follows `0052-cds-foundation`; migrations `0000`–`0052` and all platform migrations are unchanged. The manifest contains 54 tenant migrations. No `0054` exists and no platform migration is required.

`ChronicDiseaseEnrollment` stores opaque enrollment UID, `PatientUid`, linked `PatientProblemUid`, immutable program key/version/name snapshot, `Active/Inactive` status, enrollment actor/time, optional inactivation actor/time/reason, update timestamp, and row version. It contains no target, measurement, interval, care gap, CDS result, or task state.

The filtered unique index allows no more than one active enrollment per patient and program key while retaining inactive history. New enrollment after inactivation creates a new record; `Inactive → Active` is unsupported. There is no hard-delete procedure.

Stored procedures provide patient list, compound `PatientUid + EnrollmentUid` item lookup, explicit creation, and concurrency-protected inactivation. Creation verifies that the patient is active and the selected Problem exists, belongs to that patient, and is active. It never matches program keys to Problem text.

## Code-defined program registry

`ICdmProgramDefinition` exposes immutable compiled `CdmProgramMetadata`: program key, positive integer version, name, and description. `CdmProgramRegistry` validates controlled keys and metadata and rejects duplicate `ProgramKey + ProgramVersion` registrations.

Normal Application/API composition registers the registry but registers zero `ICdmProgramDefinition` implementations. Production program count is zero. No hypertension, diabetes, COPD, asthma, CKD, or other clinical program exists.

The sole `TEST_CDM_PROGRAM` version 1 definition is private to the API test assembly. It is not present in a production assembly, configuration, or DI registration. It provides architecture testing without referring to a real disease.

Enrollment records retain the exact program version selected. A later version cannot silently rewrite active or historical enrollment. Version migration is deferred to explicit clinical governance.

## Application, API, actor, and authorization

The Application service resolves the exact registered program before persistence, so arbitrary/unregistered keys are rejected even if a caller knows the route. The repository uses only the trusted tenant connection factory and stored procedures.

Patient-scoped API routes are:

- `GET /api/patients/{patientUid}/cdm`;
- `GET /api/patients/{patientUid}/cdm/enrollments/{enrollmentUid}`;
- `POST /api/patients/{patientUid}/cdm/enrollments`; and
- `POST /api/patients/{patientUid}/cdm/enrollments/{enrollmentUid}/inactivate`.

Reads require `Patients.View`; mutations additionally require `ClinicalData.Manage`. No CDM permission was added. Mutation actor identity comes only from centralized `ClinicalUserActorContext`; request contracts expose no actor field. Unresolved actor middleware therefore denies mutation before controller persistence.

Item operations are compound patient/resource lookups. The tenant database owns all clinical CDM state; there is no platform persistence or cross-tenant query.

## Lifecycle, concurrency, and audit

The only transition is `Active → Inactive`. A repeated inactivation is rejected as a conflict, and a stale row version is rejected without a success audit. Inactivation never deletes the row and does not alter the linked Problem, CDS alerts, tasks, Results, vitals, or any other clinical domain.

Creation and inactivation write `CdmEnrollmentCreated` and `CdmEnrollmentInactivated` respectively to tenant `AuditLog` in the same transaction. Audit payloads contain controlled program key/version or status only; they do not copy Problem text or descriptions. Failed transactions create no success audit. Patient Chart CDM reads remain inside the existing `PatientChartOpened` read-audit boundary.

## Patient Chart

One Chronic Disease card was added to the Patient Chart; no disease tabs were added. It lists active enrollment before inactive history, showing only program/version, linked Problem, status, and enrollment/inactivation time. When the default registry has no programs and the patient has no enrollments, the truthful state is:

`No approved chronic disease programs are currently configured.`

Enrollment controls appear only when the caller can manage clinical data, at least one compiled program is registered, and an active Problem is available. The clinician explicitly selects both program and Problem. No diagnosis text inference or automatic suggestion occurs.

## Separation boundaries

- Step 31A CDS code and migration are unchanged. Enrollment creates/evaluates no CDS alert or rule.
- Enrollment creates no task, recall, notification, appointment, or monitoring interval.
- Results and vitals are not queried or mapped into CDM.
- No target, goal, measurement, care gap, population list, report, or quality indicator exists.
- Problem creation, encounters, migration, dashboard, tasks, and CDS never auto-enroll a patient.

## Automated verification

Focused Step 32A tests cover canonical migration sequencing, table/constraints/indexes, no delete, patient/problem scoping, active Problem validation, duplicate prevention, version retention, concurrency, atomic minimal audit, production-empty registry, synthetic-only injection, duplicate registry rejection, arbitrary program rejection, authorization metadata, absence of client actor fields, and CDS/task/measurement separation.

Full API, Auth, TypeScript, whitespace, and Release gates are recorded in the final branch report.

## Runtime verification

Before upgrade, controlled non-production tenant `local-dev-fresh` reported 53 applied migrations, latest `0052-cds-foundation`, missing only `0053`, valid database identity, and no hash mismatch/failure. Provisioning applied exactly one migration:

`0053-cdm-enrollment-foundation`

Afterward it reported 54/54 applied, `Current: YES`, valid identity, no missing/unexpected migrations, no hash mismatches, latest `0053`, and no failure.

Fresh-from-blank verification could not be completed: existing disposable profile `local-dev-new` fails database identity with an SSPI target-principal error, and `provisioning-test` has invalid SQL credentials. The in-app browser runtime also failed to initialize, so normal Chart visual verification and the controlled synthetic UI lifecycle remain outstanding. These are explicitly runtime evidence gaps, not claimed passes.

## Remaining clinical and certification dependencies

Obtain and map exact CDM 4.4 clauses and mandatory scope. Before registering any production program, obtain named clinical/specification approval for diagnosis terminology/mapping, eligibility, exclusions, measurements, targets, monitoring intervals, follow-up semantics, CDS wording/rules, and reporting. Hypertension remains only a potential next candidate; it is not implemented or approved here.
