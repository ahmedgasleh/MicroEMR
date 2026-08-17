# Step 15A — Reusable structured-read audit procedure

## Why migration 0044 was required

Step 14 migration `0043-patient-chart-read-audit` established the structured nullable `AuditLog` fields but deliberately exposed only `dbo.AuditLog_RecordPatientChartOpened`. Step 15 could not safely add encounter/document events through that chart-specific procedure, and direct application inserts would violate the stored-procedure data-change boundary.

Migration `0043` was already applied/immutable and was not modified. New migration `0044-structured-read-audit-procedure` adds only one stored procedure. It adds no table, column, index, constraint, or data migration.

## Procedure contract

`dbo.AuditLog_RecordStructuredRead` accepts only trusted server-derived metadata:

- `@EventType NVARCHAR(100)`
- `@ResourceType NVARCHAR(100)`
- `@ResourceUid UNIQUEIDENTIFIER`
- `@PatientUid UNIQUEIDENTIFIER`
- `@ClinicalUserId BIGINT`
- `@RequestCorrelationId NVARCHAR(100)`
- `@SourceApplication NVARCHAR(50)`

The procedure validates non-empty resource/patient/correlation identifiers, an active non-deleted patient, and an active clinical user. It inserts one `ClinicalRead`/`Succeeded` row with server-generated event UID and UTC timestamp. Tenant/database identifiers are not parameters because execution occurs through the already-resolved tenant connection.

It accepts no patient name, health card, note, diagnosis, document content, arbitrary JSON, route, connection string, database name, or other clinical content. It performs no audit update/delete and changes no clinical record.

## Controlled values

Only these exact pairs are accepted:

| Event | Resource type |
|---|---|
| `EncounterViewed` | `Encounter` |
| `PatientDocumentViewed` | `PatientDocument` |

Unsupported event names, resource types, and mismatched pairings receive SQL error `52210`. `PatientChartOpened` continues to use its unchanged Step 14 procedure and callers.

## Repository preparation

The existing `IReadAuditRepository`/`ReadAuditRepository` now exposes `RecordStructuredReadAsync`. It executes only `dbo.AuditLog_RecordStructuredRead` through `ITenantSqlConnectionFactory` and returns the generated event UID. Constants were added for the two approved events and resource types.

No Application service, API endpoint, Web action, controller, or domain read trigger calls the new method in Step 15A. `EncounterViewed` and `PatientDocumentViewed` are not implemented yet.

## Tests and migration safety

`StructuredReadAuditProcedureTests` verifies both allow-listed pairs, invalid-pair rejection, narrow/content-free parameters, structured identity fields, insert-only behavior, absence of schema changes, reusable repository signature, manifest ordering/uniqueness, SQL batch parsing for fresh provisioning, unchanged SHA-256 for `0043`, and existing mutation-audit presence.

Existing Step 14, canonical manifest and scheduling migration tests were updated only for the appended manifest entry. The recorded SHA-256 of `0043` remains `4181A3487AA1C5837460AFC389F7C25443216F0C379EB6A781E3264A34461406`.

An upgrade from a database at `0043` executes a single `CREATE OR ALTER PROCEDURE` batch and requires the structured columns already supplied by `0043`. Fresh provisioning applies `0043` followed by `0044` through the canonical manifest.

## Step 15B readiness

Step 15B can now implement `EncounterViewed` and `PatientDocumentViewed` without another schema or migration change, provided it performs authoritative resource/patient ownership validation in the API before invoking the repository and preserves Step 14 synchronous fail-closed semantics.
