# Step 16B1 — Patient File download auditing

## Implemented event and trigger

Step 16B1 implements only `PatientFileDownloaded` with resource type `PatientFile`. The trigger is the existing API `PatientFilesController.Content` action behind the Patient Files UI Download link and Web content proxy. No new route or visible workflow was added.

The API first calls `IPatientFileService.OpenContentAsync`. That service uses the existing compound patient/file repository lookup, verifies the storage object exists and opens its stream. The returned content contract now carries the authoritative `FileUid` and `PatientUid` from the resolved repository entity. The controller then persists one structured event before returning the stream.

List and metadata actions do not call the audit service. Each request to the explicit content endpoint creates a separate event; downloads are not coalesced.

## Reused trust and audit infrastructure

The controller reuses `IStructuredReadAuditService`, the Step 14/15 clinical actor resolution model, `IReadAuditRepository`, the trusted tenant connection factory and `dbo.AuditLog_RecordStructuredRead`. Migration `0045` already permits `PatientFileDownloaded`/`PatientFile`.

The audit payload contains only controlled event/resource values, authoritative UIDs, resolved clinical actor, request correlation and source. It contains no filename, file bytes, storage key or clinical content. Tenant/database routing remains entirely server-derived.

## Failure and disclosure ordering

The order is ownership resolution, storage existence/open, synchronous audit persistence, then response release. A missing or cross-patient resource and a missing storage object return the existing not-found result without a successful event. If audit persistence fails after opening storage, the controller disposes the stream, logs the resource UID and correlation identifier, returns 503 and releases no bytes. Request cancellation also disposes the stream before propagation.

Authorization remains the existing `Documents.View` permission on the controller. Middleware denial occurs before the action and therefore creates no successful event. Cross-tenant requests remain constrained by the trusted tenant context and tenant-local repository/audit connection.

## Automated tests

`PatientFileDownloadAuditTests` covers authoritative IDs, controlled event/resource values, exactly one event, repeated downloads, missing ownership/storage behavior, audit failure with stream disposal, list/metadata noise exclusion and content-free audit metadata. Existing permission, tenant-isolation, Step 14/15 view-audit, procedure allow-list, migration and mutation-audit suites remain regression coverage.

## Manual verification

Use test patients only.

1. Open Patient A Files and confirm the list creates no download event.
2. Download one file and confirm exactly one `PatientFileDownloaded` event.
3. Verify actor, PatientUid, FileUid, tenant, correlation, outcome and source.
4. Download the same file again and confirm a second distinct event.
5. Retrieve metadata/details and confirm no download event.
6. Test a user without `Documents.View`; confirm no bytes and no successful event.
7. Request Patient A's FileUid through Patient B; confirm not-found and no successful event.
8. Repeat in another tenant and confirm the event exists only in that tenant database.
9. Make storage content unavailable and confirm no successful event.
10. Make audit persistence unavailable and confirm 503 with no file response.
11. Verify `PatientChartOpened`, `EncounterViewed` and `PatientDocumentViewed` still work without audit flooding.

## Deferred and remaining work

Patient Document download remains deferred because the current product exposes PDF Preview only and has no distinct explicit download action. Preview was not modified or misclassified. Print auditing also remains deferred pending reliable application-controlled triggers. Later slices cover reports/exports, security denials, audit review, retention and operational integrity.
