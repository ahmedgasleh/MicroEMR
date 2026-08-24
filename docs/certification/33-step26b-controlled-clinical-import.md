# Step 26B — Controlled Demographics and Problem List Import

Date: 2026-08-24

Branch: `feature/ontariomd_certification_step26b_controlled_clinical_import`

Status: **Controlled import foundation implemented for Patient Demographics and Problem List only**

## Scope and specification boundary

Step 26B promotes only approved records already persisted by the Step 26A validate-only pipeline. Import cannot accept canonical DTOs or package content. It is a distinct, explicitly authorized operation after staging, validation, and review.

Exact OntarioMD Data Migration 5.1 transport, mandatory-domain, evidence, and validation requirements remain unavailable. This is a MicroEMR internal controlled-import foundation, not a conformance claim. Allergies, medications, immunizations, results, encounters, documents/files, external adapters, uploads, export, bulk clinic processing, and UI remain excluded.

## Migration decision

Tenant migration `0049-clinical-data-migration-import-foundation` is required. Migration 0048 intentionally has no import states, Problem target mapping, durable cross-batch source mapping, import checkpoint/result fields, or migration service actor. Migration 0049 adds only those migration-specific capabilities. It does not alter Patient or PatientProblem schemas and creates no platform migration.

Migration 0049:

- extends batch status to `Importing`, `Imported`, and `ImportFailed`;
- records import start/completion, initiating operator, migration actor, and result counts;
- adds `Pending`/`Imported`/`Failed` checkpoints and redacted failure fields to staged Patient and Problem rows;
- adds target Problem UID to Problem staging;
- creates `ClinicalDataMigrationSourceMapping` for durable Patient and Problem provenance/idempotency;
- provisions one tenant-local `system-data-migration` ApplicationUser with no Auth subject, provider, email, or login identity;
- creates the governed import and result procedures.

If the reserved username is already bound to an interactive Auth subject, migration fails closed.

## Explicit lifecycle and authorization

The only import action is:

`POST /api/data-migration/batches/{batchUid}/import`

The existing controller-level `Users.ManageAccess` tenant permission protects it. `ClinicalData.Manage` alone is insufficient. Import is never triggered by validation completion, GET, page load, or package submission.

Eligible batches must be `Validated`, or `ImportFailed`/`Importing` for controlled resume. `Created`, `Validating`, and `ValidationFailed` are rejected. The procedure also rejects any invalid staged Patient/Problem or Patient mapping still marked `RequiresReview`/`Invalid`. An `Imported` batch returns its existing result as a replay without clinical or audit writes.

The route takes only the batch UID. Tenant context and the tenant database are resolved server-side; no package/body tenant value can switch connections.

## Patient behavior

- `ReadyToCreate`: create exactly one new Patient using a MicroEMR-generated PatientUid and chart number. Current Patient field validation and bounds were already enforced during staging; the governed import procedure rechecks eligibility and required relationships.
- `MappedExisting`: verify and reuse the approved PatientUid. Existing demographics are not updated or merged.
- existing durable source mapping: reuse the mapped target and normalize the staged decision to `MappedExisting`.
- `RequiresReview` or `Invalid`: reject batch import until resolved by a future approved workflow.

There is no name/DOB automatic match, fuzzy merge, package-supplied target UID, source-ID-as-PatientUid behavior, or broad existing-demographics overwrite.

## Problem behavior

Only staged `Valid` Problems with `Active` or `Resolved` status are eligible. Each resolves through its staged source patient to the created/reused target PatientUid. A new source Problem creates a new MicroEMR PatientProblem UID; an existing durable source mapping is reused only when it still belongs to the same target Patient.

Problem name/description, onset date, supported status, and resolved date use the current domain. No diagnosis code or terminology is invented. Existing native Problems are never matched by free text, name, description, or dates.

## Durable mapping and provenance

`ClinicalDataMigrationSourceMapping` uniquely maps `(SourceSystem, RecordType, SourceObjectId)` to target Patient/Object UIDs. It also retains source patient ID, migration batch UID, source created/updated timestamps, source author snapshot, imported timestamp, and migration actor.

Mappings are immutable by contract: existing mappings are reused, and inconsistent target relationships reject the aggregate. Source identifiers never replace target primary keys. Source author remains a text snapshot and never creates an ApplicationUser/provider/login.

## Actor separation

Three identities remain distinct:

- validation requester: Step 26A `RequestedBy`;
- import initiating operator: authenticated authorized user stored as `ImportRequestedBy` and attributed on batch administrative audit;
- migration service actor: tenant-local non-login `system-data-migration`, stored as `MigrationActorUserId`/`ImportedBy` and used for native clinical mutations.

Historical `SourceAuthor` is separate provenance. It is never used as `CreatedBy` or `ResolvedBy`.

## Transaction, failure, and resume semantics

The import procedure takes a SQL application lock whose resource includes only the batch UID. This serializes concurrent requests for one batch without locking other tenant batches.

Each staged Patient and all of that source patient's Problems execute in one SQL transaction. A Problem failure rolls back the newly created/reused aggregate work and mappings from that transaction, then marks the staged aggregate with the governed `PatientAggregateImportFailed` code and a redacted message. Processing continues to other patients. Successful patient aggregates commit independently.

The batch finishes `Imported` when no patient aggregate failed, otherwise `ImportFailed`. A resume processes only staged rows not already `Imported`; existing source mappings are reused. It does not delete partial data or duplicate completed aggregates. A later successful resume can complete the batch.

## Idempotency and replay

- a batch-specific application lock prevents concurrent writers;
- imported stage checkpoints are skipped on resume;
- tenant-local unique source mappings prevent duplicate Patient/Problem identities across batches;
- an already imported batch returns the prior result with `Replayed=true`;
- existing mappings must identify a consistent target patient/resource;
- replay does not duplicate clinical records, mappings, native clinical audit, `DataMigrationStarted`, or `DataMigrationCompleted`.

No free-text clinical comparison participates in idempotency.

## Audit behavior

Administrative audit events are batch-level:

- `DataMigrationStarted`, once on first transition to import;
- `DataMigrationCompleted`, once on successful completion;
- `DataMigrationFailed`, per failed import attempt, allowing attempt evidence without per-record administrative events.

They contain batch identity, source system, package fingerprint, status, and counts, but no names, health cards, problem descriptions, source package data, SQL, or stack traces.

New Patient and Problem clinical records receive one native `MigrationCreate` audit event attributed to the non-login migration service actor. The event states that the record was created through controlled migration and uses the native target UID; it does not impersonate the source author or initiating operator. Reused existing patients receive no clinical mutation audit because their demographics are unchanged.

## API result and logging

The response contains only batch UID, status, attempted/created/reused/failed patient counts, imported Problem count, skipped count, and replay flag. It returns no imported PHI.

Operational warning logs contain only batch UID and governed SQL error number. They do not log staged content, names, health cards, Problem text, or package content.

## Validation-only regression

The Step 26A validation service, endpoint, fingerprint, staging, replay behavior, and report remain separate and unchanged. Validation continues to have no clinical mutation repository dependency and migration 0048 remains unchanged. Only the explicit import procedure in 0049 can promote staged data.

## Verification and runtime boundary

Focused tests inspect migration state/constraints, authorization, explicit POST routing, actor/provenance separation, one-patient transactions, patient create/reuse rules, no demographic overwrite, Problem ownership/status, source mapping uniqueness, application locking, replay/resume, redacted failures/audit, domain exclusions, and service replay contract.

Repository migration-source tests load, parse, order, and hash all migrations through 0049. Live fresh provisioning, 0048-to-0049 upgrade, and disposable-tenant import require a reachable configured SQL Server. Results are reported at branch completion without substituting static inspection for live execution.

The live DatabaseTool precondition was attempted for `local-dev` but failed before migration inspection with `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.` No tenant database was changed. Fresh provisioning, 0048-to-0049 upgrade, and live import/replay/table-count verification therefore remain **NOT VERIFIED**.

| Verification gate | Result |
|---|---|
| Focused Step 26B tests | PASS — 9/9 |
| API regression tests | PASS — 703/703 |
| Auth regression tests | PASS — 30/30 |
| Release build | PASS — 0 warnings, 0 errors |
| Manifest/source/parser/hash | PASS — 50 ordered migrations through 0049 |
| Tenant migrations 0000–0048 | PASS — unchanged |
| Platform migrations | PASS — unchanged through 020 |
| Live fresh provisioning | NOT VERIFIED — configured SQL/TLS connection unavailable |
| Live 0048-to-0049 upgrade | NOT VERIFIED — configured SQL/TLS connection unavailable |
| Live import/replay | NOT VERIFIED — migration could not be applied to a disposable tenant |

## Remaining blockers and next recommendation

Official Data Migration 5.1 direction, external format/schema, mandatory domains/fields, provider and source-audit rules, clinical audit expectations, correction/merge semantics, attachment requirements, reconciliation evidence, and validation scenarios remain interpretation blockers.

Pause domain expansion and obtain the official specification/evidence clarification before Step 26C. Allergies, medication lists, immunizations, and results have different lifecycle, terminology, and provenance risks and should not be added merely because the internal pipeline can be extended.

**Controlled demographics and Problem List clinical import implemented using the internal canonical migration foundation. OntarioMD Data Migration 5.1 compliance remains interpretation-dependent.**
