# Step 17B — Report execution and CSV export auditing

## Implemented events and triggers

Step 17B implements `ReportExecuted` and `CsvExported`, both with resource type `Report` and governed report identity `AppointmentStatusDateReport`.

The authoritative triggers are the API `AppointmentReportsController.Get` and `AppointmentReportsController.Csv` actions. The report action executes the query successfully before auditing and returns results only after audit persistence. The CSV action executes the report query, generates CSV bytes, persists only `CsvExported`, and then returns the file.

The existing Web Reports action distinguishes its parameterless initial page load from a submitted date-range execution. It calls the API with `auditExecution=false` for the initial default report rendering and `true` when the user supplies report criteria. Direct API report requests default to explicit execution auditing. Status-option rendering and the initial Reports page therefore create no `ReportExecuted` event.

## Aggregate identity and metadata

Both events use null PatientUid and ResourceUid as required by migration `0046`. No patient from the report is selected or fabricated. The stable `AppointmentStatusDateReport` definition key is supplied through the aggregate procedure contract and becomes the audit `EntityId`.

Date filters, report rows, patient names, appointment details and CSV content are intentionally omitted. The audit records that the governed report was run or exported, not its criteria or results.

## Actor, tenant and authorization

`StructuredReadAuditService.RecordAggregateReportAsync` resolves the clinical actor through the same trusted accessor as earlier read events. `ReadAuditRepository.RecordAggregateReportAsync` uses the current tenant connection and calls only `dbo.AuditLog_RecordStructuredRead`, passing database nulls for patient/resource UIDs and the governed report key. No tenant/database identity comes from the browser.

Existing `Reports.View` and `Reports.Export` permissions remain unchanged. There is no audit-specific permission. Authorization and tenant middleware run before the controller, so denied requests create no successful event.

## Failure and semantic separation

Validation or report-query failure creates no successful event. If audit persistence fails after report generation, the API returns 503 and does not return results. CSV bytes are generated before auditing, but are not released if auditing fails.

Each explicit Run creates one `ReportExecuted`; each Export creates one `CsvExported`. Export internally reuses the report query but never records `ReportExecuted`. Repeated explicit actions are intentionally not coalesced.

## Automated tests

`ReportExportReadAuditTests` covers governed identity, exact event selection, initial-load suppression, successful report/CSV responses, CSV generation-before-audit behavior, audit failure blocking both responses, failed-query/no-event behavior, repeated actions, null patient/resource repository parameters and absence of report/filter/output content.

Existing permission, tenant, Step 14–16 read-audit, aggregate-contract, migration and representative mutation-audit tests remain regression coverage.

## Manual verification

Use test data only.

1. Open Reports and confirm the initial page load creates no `ReportExecuted`.
2. Select a date range and run the appointment-status report.
3. Confirm results display and exactly one `ReportExecuted` exists.
4. Verify actor, tenant, `AppointmentStatusDateReport`, null PatientUid/ResourceUid, correlation, outcome and source.
5. Confirm no report row, patient or filter content is stored.
6. Run again and confirm a second execution event.
7. Export CSV and confirm the file downloads with exactly one `CsvExported`.
8. Verify export did not create another `ReportExecuted` and contains no CSV data in audit.
9. Export again and confirm a second export event.
10. Test without `Reports.View` and `Reports.Export`; confirm denial and no successful events.
11. Repeat in Tenant B and confirm events appear only in Tenant B.
12. Simulate audit failure and verify 503 with no report/CSV disclosure.
13. Verify `PatientChartOpened`, `EncounterViewed`, `PatientDocumentViewed` and `PatientFileDownloaded` still work without audit flooding.
14. Perform a representative mutation and confirm its existing audit remains intact.

## Limitations and remaining work

Filters are not retained. Patient Document download and browser-only print auditing remain deferred. The recommended next slice is security-denial audit design, explicitly separating tenant clinical audit from central security events. Later work includes audit review, retention, tamper protection and immutable operational replication.
