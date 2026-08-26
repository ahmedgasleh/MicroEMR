# Step 32 — Chronic Disease Management Foundation Design

Date: 2026-08-26

Certification baseline: Ontario Primary Care `PCON-2024-02`; Chronic Disease Management (CDM) 4.4

Classification: analysis, requirements, design, and documentation only

## Decision summary

MicroEMR should define CDM as the deliberate, longitudinal workflow for an enrolled patient with a clinician-established chronic problem: program participation, monitoring context, patient-specific goals, planned follow-up, and presentation of approved care gaps. CDM must not become a second diagnosis list, a generic rules language, or a synonym for CDS.

The smallest safe architecture is:

- **Problem List:** clinical diagnosis/problem truth.
- **CDM Registry:** explicit workflow enrollment and lifecycle linked to one patient and one Problem List entry.
- **Existing clinical domains:** authoritative observations and actions, including Results, vitals, medications, prescriptions, encounters, tasks, and scheduling.
- **CDS:** separately evaluates clinically approved care-gap rules and owns derived CDS findings.

CDM remains **MISSING**. This design does not satisfy CDM 4.4, introduce a disease program, approve clinical content, or change the Step 31A CDS engine. Exact CDM 4.4 clauses are not present in the repository, so mandatory diseases and certification behavior remain interpretation blockers.

## Scope and guardrails

This document does not implement CDM, create migration `0053`, modify the migration manifest, activate a CDS rule, or add diabetes, hypertension, COPD, asthma, CHF, CKD, or another disease workflow. It does not define a clinical threshold, default target, monitoring interval, care-gap rule, treatment recommendation, or automatic task.

Technical program infrastructure and disease-specific clinical content are separate release decisions. Before production use, named clinical governance must approve each program's population, terminology, measurements, targets, intervals, care-gap logic, exclusions, wording, actions, and version transition policy. Exact applicable CDM 4.4 material must also be obtained and mapped.

## Source material and repository evidence

No external source search was performed. The design is intentionally limited to repository-held evidence.

| Source | Evidence found | Limitation |
|---|---|---|
| `docs/certification/00-certification-scope.md` | Names `PCON-2024-02` and CDM 4.4 | No CDM clauses or validation criteria |
| `docs/certification/readiness/01-source-gap-inventory.md` | Records CDM 4.4 as partial source and exact clauses unavailable | Cannot establish mandatory diseases or workflows |
| `docs/certification/01-microemr-current-state-inventory.md` | Finds reusable domains but no CDM registry, flowsheets, reminders, rules, or reports | Inventory evidence, not normative requirements |
| `docs/certification/04-preliminary-gap-map.md` | Records disease registries, flowsheets, prompts, recalls, targets, and reports as likely missing | Detailed requirement review is explicitly pending |
| `docs/certification/45-step30-certification-gap-reprioritization.md` | Classifies CDM as `MISSING` | Does not supply CDM 4.4 wording |
| `docs/certification/46-step31-cds-foundation-design.md` and `47-step31a-cds-technical-foundation.md` | Establish governed, versioned, code-defined CDS and a production-empty rule registry | CDS is not a CDM program or registry |
| `db/patient_problem_stored_procedures.sql` and Problem contracts/repository | Patient-scoped active/resolved Problem List with mutation audit and row version | Problem identity is free-text; no terminology code or chronic/program semantics |
| `db/patient_vital_stored_procedures.sql` and vital contracts/tests | Structured BP, heart rate, respiratory rate, temperature, oxygen saturation, height, weight, computed BMI, timestamp, audit, and row version | No measurement terminology identifiers; BMI is derived only when both inputs are present |
| `db/patient_result_stored_procedures.sql` and Result contracts/repository | Patient-scoped result name, type, date, string value/unit/range, summary, review state, audit, and row version | No stable observation code/system, typed numeric value, comparator, or result grouping |
| `db/patient_task_stored_procedures.sql` and task application/API | Patient tasks with due date, assignment, priority, open/completed lifecycle, overdue dashboard | No CDM enrollment link or governed recall provenance; base procedures do not provide the required CDM mutation audit contract |
| Patient Chart and read-audit implementation | Existing chart aggregation and `PatientChartOpened` audit boundary | No CDM section or disease summary |
| Tenant connection, clinical actor, and permission implementation | Trusted tenant database, clinical actor resolution, compound patient-resource access patterns, `ClinicalData.Manage` | No CDM-specific authorization exists or is proposed here |

### Exact CDM 4.4 material

The repository contains only the name/version, high-level gap statements, and interpretation questions. It contains no exact CDM 4.4 clause text, data dictionary, mandatory-disease list, conformance scenarios, validation scripts, or official interpretation decisions. No requirement below is attributed to OntarioMD unless repository evidence explicitly supports it.

## Current domain assessment

### Problem List as disease identity

An active `PatientProblem` can be the patient-specific clinical record to which an enrollment points. Compound lookup by `PatientUid + PatientProblemUid`, active/resolved lifecycle, row version, actor metadata, and mutation audit are good foundations.

It cannot safely determine program eligibility by itself. `ProblemName` is required free text and the model has no coded diagnosis/system/version, chronicity flag, verification status, or mapping to a program. Text matching would be unsafe. A resolved Problem must not be silently rewritten or replaced by CDM. Future enrollment validation should require the linked Problem to exist for the same active patient and be active at enrollment time.

Problem resolution and CDM discontinuation are distinct clinical/workflow events. A future implementation should prevent unexplained divergence by warning or requiring a deliberate enrollment decision when the linked Problem is resolved; it must not automatically erase history. Exact coupling behavior needs clinical approval.

### Structured vitals

MicroEMR **does have structured vitals**:

- blood pressure systolic and diastolic;
- heart rate and respiratory rate;
- height and weight;
- BMI calculated in the database from height and weight;
- temperature in Celsius; and
- oxygen saturation.

Each record has `RecordedAt`, patient scope, actor/timestamps, row version, and create/update audit. This materially improves data readiness for a future hypertension program. It does not itself define valid diagnostic technique, measurement selection, home versus office context, targets, or care-gap intervals.

### Results

The flat Results domain is sufficient for display and manual review of early CDM evidence, provided a clinician identifies the relevant result. It is not sufficient for reliable automated HbA1c, eGFR, creatinine, spirometry, or other disease-measure evaluation. `ResultName` and `ResultValue` are strings and there is no stable code/system, typed numeric value, comparator, method/specimen context, or panel/member relationship.

Do not match clinical measures by display name or parse arbitrary strings for production decisions. Before a Result-backed program, introduce or validate a governed observation identity and typed-value strategy as its own bounded prerequisite. Existing Results remain the source record; CDM should reference or summarize them, not copy them as authoritative measurements.

### Tasks, notifications, and scheduling

Existing tasks can represent an explicitly chosen follow-up action and already support due dates, assignment, priority, completion, reopening, and overdue display. Future CDM may create or link a task only after a deliberate clinician action or under a separately approved automation policy. A CDM task needs provenance/linkage to the enrollment and planned follow-up so it can be explained and deduplicated.

No unapproved care gap may automatically create a task, notification, appointment, or recall. A next-due date is monitoring metadata, not proof that outreach occurred. Scheduling remains separate and should be linked only when an appointment is actually created.

## Proposed conceptual model

### Versioned program definition

Use a **hybrid** strategy:

- code-defined, immutable reviewed program identity and version, supported measurement keys, and integration contracts;
- tenant-database persistence of patient enrollment and the exact `ProgramKey + ProgramVersion` that governed it; and
- later, narrowly validated database configuration only for approved operational settings that genuinely need tenant variation.

Do not create a generic disease-program expression language, database-authored executable rules, scripting, or an authoring UI. Disease-specific clinical evaluation belongs in reviewed code-defined CDS rules. Like Step 31A, normal production composition may safely contain zero program definitions until one is approved.

Minimum program metadata should conceptually include `ProgramKey`, integer `ProgramVersion`, display name, lifecycle state, clinical owner/source reference, and effective/retired timestamps. Enrollment history must retain the version in force so prior behavior remains explainable. Updating a definition creates a new immutable version; it must not rewrite historical enrollment context. Rules used by a program retain their own independent CDS rule versions.

### ChronicDiseaseEnrollment

The minimum future tenant-clinical entity is conceptually:

| Field | Purpose |
|---|---|
| `EnrollmentUid` | Opaque resource identifier |
| `PatientUid` | Patient scope and compound lookup boundary |
| `PatientProblemUid` | Link to the clinical diagnosis/problem truth |
| `ProgramKey` | Stable governed program identity |
| `ProgramVersion` | Immutable context explaining the enrolled workflow |
| `Status` | `Active` or `Inactive` |
| `EnrolledAtUtc`, `EnrolledBy` | Enrollment event attribution |
| `DiscontinuedAtUtc`, `DiscontinuedBy`, `DiscontinuationReason` | Inactivation attribution and explanation |
| `CreatedAtUtc`, `UpdatedAtUtc` | Persistence metadata |
| `RowVersion` | Optimistic concurrency |

Use only `Active` and `Inactive` in the foundation. `Completed` is clinically ambiguous for chronic disease, while `Discontinued` is an event/reason captured when moving to `Inactive`. Do not physically delete enrollment history. Enforce a patient foreign key, a problem foreign key, patient/problem consistency, controlled status checks, and uniqueness preventing duplicate active enrollment for the same patient/problem/program. Every item operation must use `PatientUid + EnrollmentUid`; enrollment must never be globally fetched and then compared in application code.

### Enrollment strategy and lifecycle

Enrollment must be **explicit**. Automatic inference from an active Problem is unsafe because the Problem is free-text, eligibility is unapproved, and diagnosis presence does not prove consent or program participation.

The future flow is:

1. An authorized clinical actor selects an active Problem belonging to the patient and an approved active program version.
2. The application presents the explicit enrollment decision and program context.
3. A stored procedure atomically creates the active enrollment and minimal patient-linked audit event.
4. Discontinuation requires an explicit actor and governed or free-text reason policy approved for the program; it preserves the record as inactive.
5. Re-enrollment should create a new lifecycle episode or explicitly reactivate under a governed policy; Step 32A should choose and test one explainable history model rather than overwrite prior dates.

Do not auto-enroll on Problem creation/import, auto-discontinue solely on Problem resolution, or infer enrollment during migration. The recommended Step 32A should reject enrollment when the production program registry is empty.

### Measurements and monitoring state

Authoritative measurements remain in their existing domains. A future program-specific mapping should identify a controlled `MeasureKey`, the source domain, stable source identity/code, selection rules, unit expectations, and approved display/evaluation behavior. CDM may cache a source UID or evaluated snapshot only when needed for explainability/performance; it must retain provenance and never replace the source observation.

Do not add a universal key/value measurements table in the foundation. BP can be read from structured vitals. Result-backed measures require a coded/typed Result prerequisite. Spirometry requires an appropriate structured domain or approved representation before COPD/asthma automation.

`last measurement` should normally be computed from approved source selection semantics, not manually stored as clinical truth. `next due date` and a `recall interval` are different:

- a clinician-entered `NextFollowUpAtUtc` is a patient-specific plan;
- a program interval is versioned approved clinical/operational content; and
- a CDS care gap is a derived finding evaluated from approved facts and rules.

No default interval is proposed. Whether next due is calculated, clinician-entered, or both requires clinical/program approval. If both exist, provenance and override reason must be explicit.

### Goals and targets

Persistent patient-specific goals are likely useful but should not be part of disease-neutral Step 32A. A later goal record should link to enrollment, carry a controlled goal/measure key, typed value and unit where applicable, effective dates, status, clinician actor, rationale, and row version; changes must be historical/audited rather than overwritten without explanation.

A **clinician-entered patient-specific goal** records an agreed plan for this patient. A **guideline-derived CDS recommendation** is a versioned rule output. They must be labelled, stored, authorized, and audited separately. No default HbA1c, BP, or other target is defined here.

## CDM and CDS boundary

CDM owns enrollment, program/version context, monitoring-plan metadata, patient-specific goals, planned follow-up, and links to deliberate workflow actions. CDS owns deterministic evaluation of separately approved rules and derived finding lifecycle. CDM may request targeted CDS evaluation and display relevant findings by governed association, but must not copy CDS findings into authoritative enrollment/goal state or treat acknowledgement/dismissal as completion of CDM follow-up.

No change to Step 31A is required for the disease-neutral enrollment foundation. A later disease program can add a targeted fact provider and code-defined rule only after clinical approval, with its own versioning, tests, failure isolation, and production registration decision. No CDS finding should be migrated as an enrollment, goal, due date, or completed action.

## Encounter and Patient Chart experience

During an encounter, CDM should be context, not a forced disease workflow. The future Patient Chart should have one dedicated **Chronic Disease** section with compact summary cards per active enrollment and expandable program-specific panels. Avoid a tab per disease.

A mature card may show program name/version context, linked active Problem, recent approved measurements with source/date, clinician-entered goals clearly labelled, next planned follow-up, and separately labelled outstanding CDS care gaps. Step 32A should show only enrollment identity/lifecycle and linked Problem; it should not manufacture measurements, targets, due dates, or findings.

Clinical documentation remains in the encounter. Viewing this section remains within the existing `PatientChartOpened` read-audit boundary unless the exact specification requires a more granular event.

## Candidate first-program readiness

This ranking assesses current structured-data readiness, not disease importance or OntarioMD mandate. Mandatory diseases cannot be determined without CDM 4.4.

| Candidate | Reusable data now | Material gaps/risks | Readiness |
|---|---|---|---|
| Hypertension | Active Problem plus structured dated systolic/diastolic BP | Problem lacks coded identity; BP context/selection, targets, intervals, exclusions, and diagnostic/control semantics need approval | **Highest technical readiness**, conditional on specification and clinical approval |
| Diabetes | Active Problem plus flat Results, medications, prescriptions, vitals | HbA1c identity is not coded; values are strings; targets, intervals, exceptions, and care-gap logic unapproved | Second; requires Result identity/typed-value prerequisite |
| CKD | Active Problem plus flat Results | eGFR/creatinine identity, numeric typing, units, equation/provenance, staging/thresholds, and intervals absent/unapproved | Lower; requires Result prerequisite and substantial governance |
| COPD/Asthma | Active Problem, medications/prescriptions, oxygen saturation, encounters | No structured spirometry domain; severity/control, symptom/inhaler-use semantics and measures absent/unapproved | Lower; data-model prerequisite required |

CHF and all other programs are unassessed because neither exact mandatory scope nor approved program content is available. Subject to CDM 4.4 mapping, **hypertension is the safest first technical disease-program candidate** because BP is already structured and typed. This is not clinical approval and does not authorize implementation. If OntarioMD mandates another first scope, that requirement governs.

## Security, audit, isolation, and migration

Future CDM clinical data belongs only in the trusted tenant clinical database. There is no platform clinical CDM table. Routes and stored procedures use the resolved tenant connection, a clinical actor, `PatientUid`, and compound patient-resource lookup. Do not accept a tenant connection or tenant database identity from the request.

Use existing `ClinicalData.Manage` for enrollment management in Step 32A and existing chart access for display. Do not create a new permission in this design step. Clinical governance may later decide whether population registry access needs a distinct permission; that is outside the foundation.

Required future mutation audits are:

- `CdmEnrollmentCreated` (enrollment);
- `CdmEnrollmentDiscontinued` (inactivation, with minimal reason code/context); and
- later, `CdmGoalCreated`, `CdmGoalChanged`, and `CdmGoalInactivated`.

Audit and the mutation must be atomic, patient-linked, actor-attributed, and minimal; detailed clinical values remain in the authoritative entity/history. Do not audit every summary read separately. Existing `PatientChartOpened` remains the read boundary unless specification interpretation changes it.

Step 32A would require an additive tenant migration after `0052` to persist versioned enrollments and atomic audit procedures. At implementation time that would normally be the next collision-free migration (currently `0053`), but **this Step 32 design does not create or reserve it**. Every tenant would be expected to apply that additive migration through the manifest/runtime. No platform migration is expected. Existing records require no backfill; imported Problems and CDS findings must not automatically become enrollments. Future imported enrollment/goal/monitoring data needs explicit source semantics, validation, actor/provenance, and clinical approval.

## Reporting and population registry

Patient-level enrollment listing is foundation behavior. Cross-patient registry search, population recall worklists, quality measures, exports, dashboards, aggregates, and certification reports belong in a later bounded step after exact requirements, privacy/authorization, performance, audit, and clinical definitions are known. Do not build reporting into Step 32A.

The enrollment schema should avoid blocking later tenant-local population queries: index stable program key/version, active status, and patient identity without denormalizing demographics or clinical measures. Population queries must use the tenant database and governed read/export audit boundaries. Foundation does not need a separate reporting store.

## Decision register

| Question | Evidence | Proposed decision | Status |
|---|---|---|---|
| Mandatory diseases | Exact CDM 4.4 clauses and validation material are absent | Do not claim or implement a mandatory list; obtain and map the package | **BLOCKED — SPECIFICATION** |
| Meaning of CDM | Existing domains hold facts/actions but no longitudinal program state | Explicit enrolled chronic-care workflow, separate from diagnosis and CDS | **PROPOSED** |
| Registry semantics | Problem List has only clinical active/resolved truth | Add tenant-local workflow enrollment linked to Problem | **PROPOSED** |
| Enrollment inference | Free-text Problems cannot safely establish eligibility/participation | Explicit clinician enrollment only; no automatic inference/import | **PROPOSED; CLINICAL REVIEW** |
| Measurements | Vitals are structured; Results are flat strings without stable codes | Reuse authoritative domains; governed mappings later; no generic value table | **PROPOSED; PROGRAM APPROVAL** |
| Targets/goals | No CDM goal model or approved defaults | Later versioned patient-specific goal records; distinguish from CDS recommendations | **DEFERRED; CLINICAL APPROVAL** |
| Monitoring intervals | No approved intervals or calculation semantics | Store only explicit planned follow-up later; version approved intervals | **BLOCKED — CLINICAL/PROGRAM APPROVAL** |
| Recall behavior | Tasks have due dates but no CDM provenance | Deliberate create/link action only; no unapproved automatic task | **PROPOSED; POLICY APPROVAL** |
| CDS relationship | Step 31A has versioned, production-empty rules/findings | CDM holds workflow state; CDS evaluates approved care gaps | **DECIDED ARCHITECTURAL BOUNDARY** |
| Reporting | No disease/program definitions or exact reporting clauses | Defer population/quality reporting | **BLOCKED — SPECIFICATION** |
| Population registry | Enrollment could support later tenant-local lists | Preserve queryable keys/indexes; do not build population UI/query in 32A | **DEFERRED** |
| Audit | Existing clinical changes use patient-linked audit; chart-open read boundary exists | Audit enrollment/discontinuation and later goal changes atomically; no extra summary-read event | **PROPOSED** |
| Versioning | Step 31A retains rule versions and explainable finding identity | Immutable program versions; enrollment stores exact key/version; CDS versions remain independent | **PROPOSED** |
| Permissions | `ClinicalData.Manage` governs comparable clinical mutations | Reuse it in 32A; add no permission here | **PROPOSED** |
| Tenant/patient isolation | Trusted tenant DB and compound patient-resource patterns are established | Tenant DB only; `PatientUid + EnrollmentUid`; clinical actor required | **DECIDED ARCHITECTURAL BOUNDARY** |
| Data migration | Current imports have no approved enrollment semantics | No inference/backfill; future explicit validated import only | **PROPOSED** |

## Interpretation and approval blockers

Before disease-specific implementation, obtain and decide:

1. Exact applicable CDM 4.4 clauses, mandatory diseases, workflows, data fields, display behavior, reports, test scenarios, and evidence expectations for `PCON-2024-02`.
2. Approved diagnosis terminology and mapping, including whether Problem List needs coded identity before enrollment.
3. For each program: eligibility, exclusions, status transitions, discontinuation/re-enrollment policy, measurements, source selection, units, targets, monitoring intervals, overdue semantics, care gaps, recall/task behavior, and wording.
4. Whether patient consent/decline and reason taxonomy are required program state.
5. Result identity/typed-value requirements for every Result-backed measure.
6. Population access, reporting, export, privacy, and audit requirements.
7. Named clinical owner, review record, effective date, version transition, rollback, and retirement process.

Until these are resolved, no threshold, target, interval, rule, recommendation, automatic recall, or disease program is safe for production.

## Recommended bounded Step 32A

**Technical CDM Program Enrollment Foundation** is valuable without disease-specific clinical content because it establishes one safe diagnosis-to-workflow boundary, explicit lifecycle, version explainability, isolation, authorization, audit, and an empty-state Chart integration before clinical logic is introduced.

Exact scope:

- immutable code-defined program metadata/registry with `ProgramKey + ProgramVersion`, validation, and zero production program registrations;
- additive tenant-clinical enrollment persistence linked to patient and active Problem List record;
- `Active/Inactive` lifecycle with explicit enrollment/discontinuation, reason, actor, timestamps, row version, no delete, and atomic minimal audit;
- Application service validation and Infrastructure stored-procedure access using trusted tenant connection and compound patient lookup;
- API/Patient Chart read and manage boundaries using existing permissions;
- one dedicated Chronic Disease section showing a truthful empty state or enrollment identity/lifecycle and linked Problem;
- focused architecture, migration, lifecycle, concurrency, authorization, audit, isolation, empty-registry, and UI tests.

Explicit exclusions:

- all production disease programs and mandatory-disease claims;
- measurements, goals, targets, monitoring intervals, next-due calculation, recalls, task creation, reports, and population lists;
- Result schema changes, vitals changes, CDS engine changes, fact providers, clinical rules, care-gap evaluation, and automatic enrollment;
- generic expression/configuration languages and platform clinical data.

Step 32A needs one additive tenant migration when authorized. Expected tenant migration: all tenant clinical databases move from `0052` to the then-next collision-free enrollment migration through the canonical manifest, with no data backfill. Platform migration: none. This document neither creates migration `0053` nor implements Step 32A.

## Verification record

The requested verification commands and final results are recorded in the branch handoff after this document is written. Verification proves repository integrity only; it does not validate CDM 4.4 interpretation or clinical content.
