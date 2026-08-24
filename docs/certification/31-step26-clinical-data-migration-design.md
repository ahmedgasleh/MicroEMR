# Step 26 — Clinical Data Migration Foundation Design

Date: 2026-08-24

Branch: `feature/ontariomd_certification_step26_data_migration_design`

Certification baseline: `PCON-2024-02`; EMR Data Migration 5.1

Status: **DESIGN ONLY — NO CLINICAL DATA MIGRATION IMPLEMENTED**

## Decision summary

MicroEMR has SQL schema deployment and tenant provisioning, but those facilities do not import or export patient clinical content and therefore do not satisfy clinical data migration. The smallest safe foundation is a tenant-scoped, validate-first, resumable migration pipeline with an external-format adapter boundary, an internal canonical model, stable source identities, explicit provenance, and persisted batch/evidence records. Import and export should be separate implementation phases.

No repository-held Data Migration 5.1 specification package, exact clauses, schema, data dictionary, examples, or validation scripts were found. The repository identifies Data Migration 5.1 as applicable to `PCON-2024-02` and records the capability as missing, but it cannot establish the mandated directions, domains, format, or audit behavior. All such conclusions below are marked **NEEDS SPECIFICATION INTERPRETATION**.

The requested prior artifact `docs/certification/28-step24-certification-readiness-reassessment.md` is not present on current `main`. The available reassessment evidence is the certification scope, current-state inventories, preliminary gap map, verification backlog, and readiness source-gap documents. This absence must be reconciled before requirement traceability is finalized.

## Repository-held specification evidence

| Source | Evidence relevant to migration | Limit |
|---|---|---|
| `00-certification-scope.md` | Names EMR Data Migration 5.1 as a Stage 3 Foundation specification for `PCON-2024-02`; separates 5.2 DFU material. | No clauses or validation rules. |
| `readiness/01-source-gap-inventory.md` | Records no local 5.1 package, clauses, scenarios, definitions, dictionary, schema, or examples. | Explicit source gap. |
| `readiness/02-interpretation-questions.md` | Requests exact 5.1 packages, dictionaries, code systems, schemas, examples, and tools. | Questions, not normative answers. |
| `04-preliminary-gap-map.md` | Distinguishes schema migration from clinical migration and identifies general import/export, attachment, mapping, validation, reconciliation, error reporting, and source/target control gaps. | Coarse assessment only. |
| `05-verification-backlog.md` | Requires whole-patient/clinic export/import validation and migration reconciliation evidence. | Backlog, not exact requirement text. |
| `readiness/05-certification-workstreams.md` | Records no certification-grade Data Migration 5.1 capability and warns against speculative detailed implementation. | Planning evidence only. |
| `30-step25a-basic-immunization-history.md` | Establishes the current immunization domain, provenance distinction, audit behavior, and migration maximum 0047. | Explicitly excludes bulk migration. |

Exact Data Migration 5.1 requirement text availability: **NOT AVAILABLE**.

## Specification interpretation table

| Question | Evidence | Decision | Status |
|---|---|---|---|
| Required interchange format | No local 5.1 schema, example, or dictionary. | Do not select XML, JSON, CSV, or ZIP as the certification format. Define an adapter-neutral canonical model. | NEEDS SPECIFICATION INTERPRETATION |
| Mandatory domains | Current repository has many clinical domains; no 5.1 domain list is available. | Use the provisional domain matrix below; do not claim completeness. | NEEDS SPECIFICATION INTERPRETATION |
| Incoming requirement | Gap documents refer to import/export, but contain no normative direction rule. | Architect import separately; do not claim it is mandated until 5.1 is obtained. | NEEDS SPECIFICATION INTERPRETATION |
| Outgoing requirement | Readiness work explicitly identifies Data Migration export as a roadmap gap, but no clause is present. | Architect export separately and assume it may be required for planning only. | NEEDS SPECIFICATION INTERPRETATION |
| Audit-history handling | Native `AuditLog` represents MicroEMR events; no source-audit mapping rule exists. | Never insert source events as native events. Retain a separate archive/evidence reference if required. | NEEDS SPECIFICATION INTERPRETATION |
| Attachment requirement | Files/documents exist; no 5.1 package rule is available. | Canonical model supports binary manifests and checksums without choosing packaging. | NEEDS SPECIFICATION INTERPRETATION |
| Source-provider identity | Current actor model uses tenant-local `ApplicationUser`. | Preserve source author snapshots and optional approved mappings; never auto-create accounts. | NEEDS SPECIFICATION INTERPRETATION |
| Original timestamps | Current domains vary in timestamp support. | Preserve source clinical and authored dates explicitly; never disguise import time as original time. | NEEDS SPECIFICATION INTERPRETATION |
| Duplicate semantics | No 5.1 replay rule found. | Enforce package fingerprint and source-system/source-object uniqueness. | NEEDS SPECIFICATION INTERPRETATION |
| Rollback expectation | No normative rule found. | Require backup, validate first, process atomically per patient, and resume; do not promise whole-clinic rollback. | NEEDS SPECIFICATION INTERPRETATION |
| Validation report requirements | Gap map mentions reconciliation and exception reporting without a schema. | Produce structured counts, errors, warnings, mappings, and a retained evidence summary. | NEEDS SPECIFICATION INTERPRETATION |
| Migration evidence retention | No retention period or evidence schema found. | Retain batch metadata, hashes, reports, counts, and administrative audit under policy; do not retain temporary PHI indefinitely. | NEEDS SPECIFICATION INTERPRETATION |

## Domain inventory and provisional migration scope

These classifications are architecture recommendations, not Data Migration 5.1 conclusions.

| Domain | Migration classification | Rationale |
|---|---|---|
| Patient demographics | REQUIRED MIGRATION DOMAIN | Root identity and ownership for every patient resource. Exact 5.1 field set remains interpretation-blocked. |
| Problem List | LIKELY REQUIRED | Core longitudinal clinical list with current structured lifecycle. |
| Allergies | LIKELY REQUIRED | Safety-critical longitudinal clinical list. |
| Medications | LIKELY REQUIRED | Current list can carry medication name, indication, prescriber snapshot, dates, and status; it is not a prescribing domain. |
| Immunizations | LIKELY REQUIRED | Step 25A supplies basic structured administration history. Terminology and non-administration semantics are excluded. |
| Encounters | LIKELY REQUIRED | Important clinical chronology and parent for addenda/documents, but current authorship/provenance needs extension. |
| Encounter addenda | LIKELY REQUIRED | Must remain ordered under their source encounter; never flatten into the base note. |
| Results/Labs | LIKELY REQUIRED | Current generic result fields support manual results, not laboratory interfacing or complete coded lab semantics. |
| Patient Documents | LIKELY REQUIRED | Authored clinical content and finalized artifacts may form part of the legal record. Template linkage needs careful mapping. |
| Patient Files | LIKELY REQUIRED | External reports and attachments may be clinically material; bytes require controlled storage. |
| Referrals | NEEDS SPECIFICATION INTERPRETATION | Clinically relevant, but outbound workflow and required transfer semantics are not established. |
| Appointments/history | OPTIONAL / OPERATIONAL | Scheduling continuity can help a transition, but historical appointments may not be certification clinical content. |
| Tasks/notifications | OPTIONAL / OPERATIONAL | Workflow state is target-system operational data and may be unsafe or meaningless after transition. Open-item conversion needs policy. |
| User/provider references | DO NOT MIGRATE as accounts | Only approved identity mappings or historical provider snapshots may migrate. Credentials, roles, memberships, and login accounts do not. |
| Native MicroEMR audit history | DO NOT MIGRATE | It belongs to the target system and must not be overwritten. |
| Source EMR audit history | NEEDS SPECIFICATION INTERPRETATION | Preserve as separate evidence/archive if required, never as fabricated native `AuditLog` rows. |
| Schema-migration ledger, tenant metadata, secrets, permissions | DO NOT MIGRATE | Deployment/security internals are outside a clinical transfer package. |

Vitals and chart alerts also exist in the current schema and should be evaluated when the exact mandatory domain list is obtained. Vitals are likely clinical; alerts are likely operational unless the source specification says otherwise.

## Current schema suitability

“Ready” means a safe canonical mapping can be designed around current clinical fields; it does not mean the table currently has sufficient migration provenance.

| Domain | Suitability | Key gap |
|---|---|---|
| Patient demographics | REQUIRES ADDITIVE PROVENANCE FIELDS | Stable source patient identity, batch, imported time, source timestamps, and deterministic match decision are absent. |
| Problems | REQUIRES ADDITIVE PROVENANCE FIELDS | Source object ID, batch, original author, import time, and source timestamps are absent. |
| Allergies | REQUIRES ADDITIVE PROVENANCE FIELDS | Same provenance gap; external terminology mapping is unresolved. |
| Medications | REQUIRES ADDITIVE PROVENANCE FIELDS | List fields are usable, but migration provenance and coded/product semantics are absent. |
| Immunizations | REQUIRES ADDITIVE PROVENANCE FIELDS | `SourceType`/description help, but batch/source object/original author/import time are not explicit. |
| Encounters | REQUIRES ADDITIVE PROVENANCE FIELDS | Original authorship, external identity, and source timestamps cannot be safely conflated with tenant actors. |
| Encounter addenda | REQUIRES ADDITIVE PROVENANCE FIELDS | Requires source encounter mapping, source author snapshot, ordering, and provenance. |
| Results | REQUIRES ADDITIVE PROVENANCE FIELDS | Existing value/unit/range/date/status fields are usable; coding, source identity, provenance, and abnormal semantics remain incomplete. |
| Patient Documents | REQUIRES ADDITIVE PROVENANCE FIELDS | Source identity/authorship and original template metadata are absent; local template FK must not be fabricated. |
| Patient Files | READY FOR MIGRATION for bytes/metadata through storage abstraction; REQUIRES ADDITIVE PROVENANCE FIELDS for certification | Current metadata includes name, MIME type, size, SHA-256, category, storage key, actor, and time. Source/batch/original-time fields remain absent. |
| Referrals | REQUIRES INTERPRETATION | Current internal workflow may not represent source transmission artifacts or statuses. |
| Appointments | REQUIRES INTERPRETATION | Useful operationally, but target scheduling semantics and certification inclusion are unknown. |
| Tasks/notifications | NOT READY | Assignment and workflow meaning cannot safely cross systems without mapping and policy. |
| Provider/user records | NOT READY | External providers are not authenticated tenant-local users. |
| Source audit history | NOT READY | No separate historical-audit/evidence domain exists. |

The preferred future schema pattern is a tenant-local migration provenance relation keyed to `(DomainType, MicroEmrObjectUid)` plus `(SourceSystemKey, SourceObjectId)` rather than adding a different collection of migration columns to every clinical table. Domain-native clinical dates should still remain in their clinical tables. Exact physical design must account for relational integrity and query performance before migration 0048 is authored.

## Patient identity and matching

An imported patient receives a MicroEMR `PatientUid`; arbitrary source identifiers must not replace it. Matching is an explicit, reviewable decision:

1. Reuse an existing mapping for `(SourceSystemKey, SourcePatientId)` when present.
2. Otherwise consider an exact normalized Health Card Number plus version only under approved validation and privacy rules.
3. Chart number may be supporting evidence but is tenant-local and may collide with the source.
4. Demographic comparison can produce candidate matches for human review; name and date of birth alone must never auto-match.
5. If no safe match exists, create a new patient only after required-field validation and duplicate review.

The stable model is `MicroEmrPatientUid + SourceSystemKey + SourcePatientId + MigrationBatchUid + match method/decision metadata`. A package-supplied target patient or tenant UID is never authoritative.

## Source identity and provenance

Every imported object needs a stable source key independent of mutable clinical text:

- target-generated MicroEMR UID;
- normalized, administrator-approved `SourceSystemKey` and human-readable source system name/version;
- immutable source object ID and source patient ID;
- migration batch UID and package UID/fingerprint;
- original created/updated/clinical dates where supplied;
- source author/provider identifier and display snapshot where supplied;
- imported timestamp and executing/import service actor;
- mapping/transformation version and warnings.

The unique replay key should be `(SourceSystemKey, SourceObjectType, SourceObjectId)` within a tenant, with source patient identity included in relationship validation. Source IDs are untrusted bounded strings, not target primary keys.

## Clinical actors and original authorship

Migration execution must retain the existing resolved tenant-local actor boundary. Use a dedicated, disabled-for-interactive-login tenant-local migration service actor for governed clinical writes, with the authorized initiating operator recorded separately on the batch and administrative audit. This avoids falsely representing the operator as the historical clinician while keeping database foreign keys and audit attribution valid.

For encounters, documents, problems, results, immunizations, and medications, preserve source authorship as immutable provenance fields: source provider ID, name snapshot, role/specialty snapshot if supplied, and optional mapping to an existing tenant-local provider approved during validation. An unresolved provider remains historical text. The package must never create an `ApplicationUser`, login, role, membership, or permission.

## Trusted tenant boundary

- The authorized operator selects exactly one target tenant through trusted platform administration context.
- The batch is created inside that tenant database; the trusted context supplies tenant identity.
- Package tenant identifiers are informational and may be compared to expected source metadata, but cannot select a connection or override target tenant.
- No cross-tenant lookup, package-driven connection string, tenant switch, or source UID trust is allowed.
- Every patient/resource repository continues through `ITenantSqlConnectionFactory`; relationship procedures use patient and resource identifiers together.
- Authorization and audit failures fail closed before clinical parsing or writes.

## Canonical migration model

Use a layered boundary:

```text
External package adapter
    -> canonical package and domain DTOs
    -> structural/semantic/relationship validation
    -> reviewed patient/provider mappings
    -> tenant migration application services
    -> governed domain import procedures and file storage
```

Canonical DTOs must be versioned, streaming-friendly, independent from SQL rows and transport syntax, and use explicit source identities and provenance. They should include package metadata, patients, resources, relationships, attachments, warnings, and source audit evidence references. External adapters own OntarioMD/vendor syntax; clinical repositories never parse files.

This model must not imply that a proprietary canonical JSON representation is the certification interchange format.

## File and package format

Final external format decision: **NEEDS SPECIFICATION INTERPRETATION**. No available evidence justifies choosing XML, JSON, CSV, or ZIP. Obtain Data Migration 5.1 schemas, code systems, examples, package rules, and validation tools first.

Regardless of transport, a canonical package envelope should model package UID, source system/vendor and version, export timestamp, external schema/version, content inventory/counts, attachment manifest, per-content checksums, package fingerprint, and optional source-tenant identity. Checksums support integrity and replay detection; no claim is made that cryptographic signing is required.

## Batch and staging design

Future tenant-local concepts should include:

- `ClinicalDataMigrationBatch`: UID, source system, package identity/fingerprint, canonical/adapter version, requested/validated/started/completed times, initiating operator, execution actor, mode, status, counts, report/evidence reference, and bounded failure summary.
- `ClinicalDataMigrationRecord`: batch, source type/ID/patient ID, target domain/UID, status, attempts, error/warning codes, and timestamps.
- patient/provider mapping decisions and immutable source-provenance mappings;
- staged normalized records or controlled staging payload references.

This belongs in the tenant database because it contains PHI, clinical relationships, and tenant-specific evidence. Platform storage should contain only minimal orchestration/security audit metadata if a future central operator workflow truly requires it.

Staging is recommended. It enables format normalization, validate-only operation, mapping review, relationship checks, counts reconciliation, record error reporting, replay safety, and resumability before production inserts. Staging must be bounded, access-controlled, retention-governed, and never become an alternate clinical record.

## Idempotency and replay protection

Use three layers:

1. Reject or explicitly resume an existing completed/in-progress package fingerprint for the same source system and target tenant.
2. Enforce unique source-object provenance keys so a repackaged/reordered file cannot duplicate records.
3. Make each per-patient/domain application operation idempotent against the staged record state and recorded target UID.

`MigrationBatchUid` alone is insufficient because replay can create a new batch. Free-text clinical equality, dates, names, and file names are never idempotency keys. Conflicting content for an existing source key is a validation conflict requiring an explicit, audited resolution; it is not silently overwritten.

## Relationships and import order

Validate a dependency graph before writes and process in deterministic order:

1. package, source identities, and provider mappings;
2. patient match/create decisions;
3. independent patient lists: problems, allergies, medications, immunizations, results, and vitals;
4. encounters;
5. encounter addenda and encounter-linked documents;
6. document metadata/content and patient files through controlled binary storage;
7. referrals and approved supporting links;
8. optional operational records only under an explicit policy.

Every child resolves through its staged source parent mapping and the trusted target patient. Missing, ambiguous, cyclic, or cross-patient references fail validation. No orphan record is created.

## Validation, dry run, and errors

Validate-only must be part of the first implementation slice and must perform the same parsing, normalization, source-key uniqueness, patient/provider mapping, relationship, domain-field, attachment inventory/hash, authorization, tenant, and replay checks as execution, without clinical writes.

Error classes:

- **Fatal package**: unreadable/malformed envelope, unsupported schema/adapter version, absent required package/source identity, checksum failure, duplicate package, unsafe size/type, or structurally impossible inventory. Stop the batch.
- **Patient-fatal**: ambiguous patient mapping, invalid root patient, or broken required relationship. Skip that patient atomically while allowing other patients if policy permits.
- **Record-level**: invalid required value/date/status, unsupported code, unresolved optional provider, or duplicate/conflict. Fail or warn for that record according to a versioned rule; never silently coerce clinically meaningful values.
- **Infrastructure/security**: authorization loss, tenant mismatch, storage/SQL failure, or integrity failure. Stop safely and retain a redacted failure event.

The structured report includes package/batch identity, validation rule version, totals by domain and patient, imported/skipped/failed/warning counts, coded errors with source record references, mapping decisions, attachment reconciliation, replay/conflict findings, start/end times, and final status. User-facing output contains no raw SQL exception, stack trace, connection information, or unrestricted PHI.

## Transaction, scale, and execution model

Use resumable staged import with a transaction per patient (and bounded attachment coordination), not one clinic-wide transaction. Patient-level atomicity best protects cross-domain relationships while limiting locks and log growth. Exceptionally large patients may require domain checkpoints only after exact consistency rules are defined.

Parsing, hashing, validation, and import should stream or page records and attachments. Do not load the tenant or package into memory, return it in one API response, or process a clinic-sized upload synchronously in a browser request.

Production execution should be a durable background operation with explicit state and cancellation/checkpoint semantics. The repository does not establish a general-purpose durable job framework suitable for migration; selecting or implementing one is a later architecture decision. Step 26 does not add Quartz or another scheduler.

## Attachments and documents

For each binary preserve source object identity, patient relationship, original filename, MIME type, byte length, content checksum, category/description, original authored/received timestamp, source author snapshot, and package entry reference. Never accept an imported filesystem path or storage key. Validate allowed type, actual content signature where practical, configured size/count limits, checksum, and malware controls before saving through `IPatientFileStorage`. The target assigns an opaque storage key outside `wwwroot`.

SQL metadata and binary storage are not one transaction. Import needs compensating cleanup/quarantine and reconciliation for storage-first/database-second failures, following the current Patient File design boundary.

Migrated documents retain original rendered content and source template name/version metadata when supplied. They link to a local `TemplateVersionUid` only through an explicit reviewed mapping. Otherwise they remain imported historical documents without a fake local template dependency. Finalized PDFs and structured source content should be distinct artifacts when both exist.

## Domain-specific boundaries

### Results

Map only fields supported by the current result domain: type, name/code when representable, result/collection dates when distinguishable, summary/value, units, reference range, status, review metadata, and provenance. Current schema does not establish complete structured laboratory codes, abnormal flags, specimen/order workflow, or external lab integration; unsupported meaning must remain in canonical source metadata or block the record rather than be invented.

### Medications

Migrate only medication-list concepts supported today: medication name snapshot, dosage/instructions and existing dates/status/indication/prescriber snapshot where present. Do not introduce prescription orders, dispense events, renewals, drug-interaction results, formulary data, or pharmacy transmission semantics through migration.

### Immunizations

Map Step 25A fields without inventing terminology: vaccine name snapshot, administration date, dose/route/site/lot where supplied, `HistoricalExternal` source, source description/original provider snapshot, encounter only when safely mapped, notes, and completed/entered-in-error status when semantically supported. Source provider and import operator remain separate. Refusal, forecast, coded vaccine, registry, and inferred series semantics remain blocked.

## Timestamps

Preserve clinical event dates in their native fields and retain source-created/source-updated timestamps separately where supplied. Add `ImportedAtUtc` and batch provenance independently. Current `CreatedAt`/`CreatedBy` fields often describe native insertion and actors; setting all historical records to the migration date destroys provenance, while assigning an external timestamp to a native audit event misrepresents target-system history. Future domain import procedures must define each timestamp explicitly.

## Audit strategy

Source audit history must not be loaded into native `AuditLog` or `PlatformAuditEvent`. If required, retain it as a read-only migration evidence archive or a future separate historical-audit domain, linked to source/package/batch and clearly labelled as source-provided evidence.

Administrative events should include `DataMigrationValidated`, `DataMigrationStarted`, `DataMigrationCompleted`, and `DataMigrationFailed`, plus explicit mapping/conflict overrides. Events record trusted tenant, batch/package fingerprint, operator, outcome, counts, and correlation ID without clinical payloads or raw package data.

For native clinical audit, the recommended provisional design is one batch administrative event plus immutable per-record provenance and batch-record outcomes, with governed aggregate domain mutation audit entries rather than pretending each imported historical record was interactively created. Whether Data Migration 5.1 requires one native event per row is **NEEDS SPECIFICATION INTERPRETATION**. Whatever model is selected must permit record-level traceability without overwhelming or falsifying native audit semantics.

## Authorization and PHI security

Introduce a future dedicated `DataMigration.Manage` tenant-administrative permission and, if central cross-tenant orchestration is later required, a separately reviewed platform entitlement. `ClinicalData.Manage` alone and ordinary clinical roles are too broad a grant for clinic-scale transfer. Do not add the permission in this design step.

Packages contain PHI and require authenticated/authorized controlled ingestion, explicit target selection, request and expanded-size limits, extension/MIME/content validation, malware scanning, opaque temporary names, storage outside web roots, encryption at rest consistent with the production hosting design, least-privilege worker access, redacted logs, no direct browser retrieval, access audit, and deterministic deletion/quarantine under retention policy. Archive evidence and temporary package retention are separate decisions. Secrets, passwords, connection strings, target tenant internals, and native audit internals are never exported.

## Import and export separation

Implement import and export as separate phases sharing canonical domain definitions, provenance vocabulary, attachment abstractions, and validation rules. They have different threat models, authorization, audit, volume, reconciliation, and certification evidence. Do not require byte-for-byte symmetry or expose database representation.

Outgoing export should stream a point-in-time, single-tenant package; apply a defined patient/cohort scope; include supported clinical domains, relationships, source/target-neutral identities, provenance, attachment manifest/bytes, schema/version, counts, and checksums; reconcile exported counts; and audit the disclosure. It must exclude credentials, secrets, local storage paths, authorization configuration, internal row versions, tenant connection metadata, and native audit internals unless an exact requirement states otherwise. Required direction and format remain interpretation-blocked.

## Operational workflow

1. An authorized operator selects the trusted target tenant and source profile.
2. Create a tenant-local batch and allocate controlled temporary storage.
3. Upload/select the package; calculate its fingerprint and inventory without trusting its paths or tenant target.
4. Parse through the versioned adapter into staging/canonical records.
5. Run validate-only, patient/provider matching, relationship checks, and replay checks.
6. Review the structured validation report and resolve allowed mappings/conflicts.
7. Confirm a recent tested tenant SQL/file backup and explicitly authorize execution.
8. Execute asynchronously in deterministic, per-patient transactions with checkpoints.
9. Reconcile domain/attachment counts and inspect failures/warnings.
10. Complete or fail the batch, retain required evidence, and remove/quarantine temporary package content according to policy.

## Backup and rollback

A verified tenant database and file-store backup is a mandatory operational precondition for production import. Record backup reference/time and operator acknowledgement on the batch without storing backup credentials. Restore procedures must be rehearsed before a clinic migration.

Do not promise automatic whole-clinic rollback. Validate first, preserve source mappings, commit atomically per patient, stop on integrity/security failures, and resume idempotently. A controlled cleanup feature, if later justified, must be migration-specific, batch/source-key constrained, audited, protect subsequent native edits, and use governed clinical correction/retention semantics—not bulk delete or reset. Catastrophic reversal uses coordinated database/file backup restoration under an approved runbook.

## Certification evidence plan

Later evidence should include the authoritative requirement mapping; supported-domain/field matrix; canonical and external schema versions; representative package with attachments; validate-only report; successful import/export as applicable; patient/provider mapping evidence; malformed/oversized/checksum rejection; tenant and authorization denial; replay prevention; provenance/original timestamp preservation; parent-child and patient ownership checks; attachment byte/hash reconciliation; domain and total counts; interruption/resume; redacted administrative audit; backup prerequisite; and retained sign-off/results.

## Implementation slices

### Step 26A — Tenant-local batch, canonical model, staging, and dry-run foundation

- Obtain or explicitly disposition Data Migration 5.1 format/domain blockers before defining any external adapter as conformant.
- Add tenant-local batch, staged-record, source-identity/provenance, mapping-decision, and structured-error persistence in the next tenant migration.
- Add versioned canonical package/domain DTOs and streaming adapter interfaces, but no speculative OntarioMD parser.
- Add package fingerprinting, source-key uniqueness, tenant binding, relationship graph validation, and deterministic validation reports.
- Support validate-only for demographics and problems as the first two structured mappings; make no production patient/clinical writes.
- Add dedicated authorization and administrative audit only after their exact control design is reviewed.
- Prove malformed package rejection, wrong-tenant fail-closed behavior, ambiguous patient matching, duplicate replay detection, provenance capture, and bounded/redacted errors.

Step 26A requires tenant migration **0048** for durable batch/staging/evidence and idempotency state. It requires no platform migration under the recommended tenant-local design. It creates no clinical import endpoint, export endpoint, UI, production clinical writes, or background execution in the present Step 26 documentation task.

### Step 26B — First clinical import

After 26A evidence and specification interpretation, import patient demographics and problems through governed stored procedures, patient-level transactions, native ownership checks, source provenance, and migration-specific audit semantics. Add allergies only if its terminology and correction mapping are resolved; do not silently broaden the slice.

### Step 26C — Expanded structured domains

Add allergies if deferred, medication-list data, basic immunizations, results, and likely vitals with domain-specific mapping/validation and source author handling.

### Step 26D — Encounters, documents, files, and complex relationships

Add encounters/addenda, documents/content, patient files, attachment storage/reconciliation, and explicitly interpreted referrals. This follows structured-domain stabilization because binary and authorship/template relationships carry higher operational risk.

### Step 26E — Outgoing export

Implement separately once exact outgoing direction, mandated domains, packaging, evidence, and disclosure-audit requirements are known. Reuse canonical domain mappers without exposing SQL representation.

Appointments and tasks/notifications remain optional operational conversion work outside certification scope unless specification or customer transition requirements establish otherwise.

## Interpretation blockers before conformant implementation

Obtain the exact Data Migration 5.1 package, schemas, data dictionary, code/value sets, examples, validation tools/scenarios, and interpretation notes. Confirm incoming versus outgoing obligations; required patient/clinical domains and cardinalities; attachment packaging; source audit treatment; provider identity; original timestamps; duplicate/update semantics; trial/dry-run and reconciliation outputs; error tolerance; package security/signature requirements; rollback expectations; evidence retention; and whether CDS-S 5.1 defines the migrated data representation.

Until those answers exist, this architecture is a safe foundation recommendation, not a certification mapping or claim of conformance.

## Scope and migration safety statement

This Step 26 artifact changes documentation only. It creates no migration, table, stored procedure, endpoint, importer, exporter, UI, parser, background job, permission, tenant switch, clinical mutation, reset/delete facility, or historical migration edit. Current expected maxima remain tenant migration `0047-patient-immunization-history` and platform migration `020`.
