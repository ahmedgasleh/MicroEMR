# Step 17A — Aggregate report audit contract

## Why Step 17 stopped

Migration `0045` allowed only patient-scoped view/download pairs. It required both PatientUid and ResourceUid and validated one patient row. The appointment-status/date report is tenant-wide and may contain multiple patients, so assigning one patient or a random per-execution resource GUID would produce false audit identity. Step 17 correctly stopped before wiring report controllers.

Migration `0046-aggregate-report-audit-events` alters only `dbo.AuditLog_RecordStructuredRead`. Applied migrations `0043`, `0044`, `0045` and all earlier migrations remain immutable.

## Aggregate contract

The procedure now accepts `ReportExecuted`/`Report` and `CsvExported`/`Report`. These pairs require null PatientUid and ResourceUid and skip patient lookup. They require the governed report key `AppointmentStatusDateReport`; unknown report identities remain rejected.

The existing nullable `AuditLog.ResourceUid`, `PatientUid` and `PatientId` fields represent not-applicable aggregate identities. The stable report-definition key is stored in existing string `EntityId`, with `EntityName`/`ResourceType` equal to `Report`. It identifies the report definition, not an execution. AuditEventUid still uniquely identifies each execution event.

No table or column was added. The procedure gains only a trailing optional `@ReportKey NVARCHAR(100) = NULL`, preserving existing callers that omit it.

## Preserved patient-scoped behavior

`EncounterViewed`/`Encounter`, `PatientDocumentViewed`/`PatientDocument`, `PatientDocumentDownloaded`/`PatientDocument`, and `PatientFileDownloaded`/`PatientFile` remain valid. For these pairs, PatientUid and ResourceUid remain mandatory, the active non-deleted patient is resolved, and ReportKey must be null. Actor, correlation, source, controlled-pair rejection and insert-only behavior are unchanged.

## Filter metadata

Step 17A intentionally stores no date range, status filter, report rows, CSV content or arbitrary JSON. The current audit schema has no purpose-built filter fields, and overloading unrelated columns would weaken the contract. Filter metadata can be reconsidered only with a governed requirement and schema design.

## Tests and migration safety

`AggregateReportAuditContractTests` verifies all prior pairs, both aggregate pairs, the sole governed report key, null aggregate patient/resource rules, mandatory patient-scoped identity, invalid-pair rejection, one insert, absence of update/delete/schema/filter payload behavior, manifest uniqueness/order, SQL batch parsing and hashes for migrations `0043`–`0045`.

An existing database at `0045` applies one `CREATE OR ALTER PROCEDURE` batch. Fresh provisioning applies `0045` then `0046` through the canonical manifest. No report or export controller is wired in this step.

## Step 17B readiness

Step 17B can add a small generic repository/service compatibility method that supplies null patient/resource UIDs and the governed report key, then wire `ReportExecuted` and `CsvExported` after successful generation but before response release. No additional migration is required. Report execution and CSV export must remain distinct, synchronous, fail-closed events.
