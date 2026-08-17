# Step 16A — Structured disclosure-event allow-list

## Why migration 0045 was required

Migration `0044` intentionally allowed only `EncounterViewed`/`Encounter` and `PatientDocumentViewed`/`PatientDocument`. Step 16 download auditing could not use that procedure without receiving controlled SQL error `52210`, and modifying the applied migration would violate migration immutability.

Migration `0045-structured-disclosure-audit-events` uses `CREATE OR ALTER PROCEDURE` to replace only the allow-list definition of `dbo.AuditLog_RecordStructuredRead`. It adds no table, column, index, constraint or data migration. The procedure signature, trusted identity validation, insert, generated event UID, UTC timestamp and rejection error codes are preserved. Migrations `0043`, `0044` and all earlier migrations remain unchanged.

## Controlled combinations

The existing combinations remain valid:

| Event | Resource type |
|---|---|
| `EncounterViewed` | `Encounter` |
| `PatientDocumentViewed` | `PatientDocument` |

The following download combinations are newly valid:

| Event | Resource type |
|---|---|
| `PatientDocumentDownloaded` | `PatientDocument` |
| `PatientFileDownloaded` | `PatientFile` |

Unknown events, unknown resources and mismatched pairs continue to receive SQL error `52210`. The procedure remains insert-only and accepts no filename, file/document content, encounter text, arbitrary JSON, tenant database name or connection string.

## Print decision

`EncounterPrinted` and `PatientDocumentPrinted` remain rejected. Encounter history printing is a browser `window.print()` action over a date range containing multiple encounters, so it is neither server-authoritative nor correctly represented by one EncounterUid. The single-encounter final-PDF action is presented as viewing an already generated final artifact, while encounter and patient-document preview endpoints are previews rather than explicit print actions. No distinct enforceable patient-document print action exists.

Adding print pairs now would therefore authorize semantic values without a reliable trigger. Print auditing remains deferred pending an explicit application-controlled action and an approved resource model for multi-encounter history output.

## Tests and migration verification

`StructuredDisclosureAuditProcedureTests` verifies preservation of both view pairs, acceptance of both download pairs, continued print rejection, controlled mismatch/unknown rejection, the unchanged narrow procedure contract, one insert, absence of update/delete/schema/content behavior, unique manifest ordering, SQL batch parsing and byte-for-byte hashes for migrations `0043` and `0044`.

The existing migration-source and canonical manifest tests are updated only for the appended entry. An upgrade at `0044` applies one `CREATE OR ALTER PROCEDURE` batch. Fresh provisioning applies `0044` followed by `0045` through the canonical manifest.

No endpoint, Web action, API action or download/print trigger is wired in Step 16A.

## Step 16B readiness

Step 16B can implement `PatientDocumentDownloaded` and `PatientFileDownloaded` without another migration, provided each trigger resolves authoritative resource ownership, prepares the artifact, persists the event synchronously and fail-closed, and only then releases bytes. Print auditing still requires design clarification and a reliable server-controlled semantic trigger.
