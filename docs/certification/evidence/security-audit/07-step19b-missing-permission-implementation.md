# Step 19B centralized MissingPermission implementation

## Scope and outcome

Step 19B records central `SecurityAccessDenied` / `Denied` / `MissingPermission` events for the six approved sensitive capabilities. It reuses `IPlatformSecurityAuditRepository` and `dbo.PlatformSecurityAudit_RecordMissingPermission`. No schema, migration, tenant clinical audit, alert, review UI, retry queue, ownership detection or tenant-denial behavior changed.

## Central trigger and event ownership

Each host registers an `IAuthorizationMiddlewareResultHandler`. It runs only after the final policy result is `Forbidden`, the principal is authenticated, the endpoint has governed sensitive-capability metadata, and the failed policy contains that capability's permission requirement. Controllers and repositories do not record denials.

MicroEMR.Web owns a browser request rejected by its Web permission policy; no API request occurs in that case. MicroEMR.Api owns a direct API request rejected by its API permission policy. If Web permission succeeds, it emits no denial and the downstream API request proceeds normally. A downstream API denial is an independent authoritative authorization attempt and is API-owned. This avoids recording the same failed Web authorization in both applications.

## Controlled capability mapping

`SensitiveCapabilityAttribute` accepts only entries in `SensitiveCapabilityCatalog`; arbitrary strings are rejected. The catalog uses the existing `PermissionKeys` constants.

| Capability | Required permission |
|---|---|
| `PatientChartView` | `Patients.View` |
| `EncounterView` | `Encounters.View` |
| `PatientDocumentView` | `Documents.View` |
| `PatientFileDownload` | `Documents.View` |
| `AppointmentReportRun` | `Reports.View` |
| `AppointmentReportExport` | `Reports.Export` |

Metadata is attached to the corresponding chart/details/download/report endpoints in Web and API. Permission strings, routes and query strings are not used to infer capability. This explicitly separates document view from file download and report execution from CSV export.

## Duplicate prevention

The result handler writes once per capability per `HttpContext`. It marks the capability before persistence, so repeated authorization evaluation or persistence failure cannot create repeated attempts in the same request. Multiple failed requirements are evaluated as one final policy result and produce at most one event. Separate HTTP requests remain separate authorization attempts and retain their own trace identifiers.

## Identity, tenant and correlation

- `ActorSubject` is the authenticated opaque `sub`, with the established name-identifier fallback. It is never parsed as a clinical identifier. If it is absent or blank, no malformed event is attempted; the original denial remains.
- `ClinicalUserId` is null. Authorization occurs before clinical actor resolution, and Step 19B performs no clinical lookup for enrichment.
- API uses only `ITenantContextAccessor.Current`, which was populated by successful tenant resolution. Web has no trusted resolved tenant context at its authorization boundary, so `TargetTenantUid` is null rather than copying a tenant claim.
- `RequestCorrelationId` is the bounded ASP.NET `HttpContext.TraceIdentifier`; it is not converted to a GUID.
- No patient UID, resource UID, raw URL, query string, body, token or clinical content is included in the event contract.

## Persistence and outward behavior

The handler delegates the response to ASP.NET Core's standard `AuthorizationMiddlewareResultHandler` after attempting the audit. API remains a 403. Web retains its existing cookie/access-denied behavior. Audit exceptions are caught and operationally logged with controlled capability, permission and trace identifier; they never grant access or replace the original denial with a 500.

The Web host resolves the platform repository lazily only for an eligible denial. Therefore missing Web platform-database configuration cannot affect successful requests; an eligible denial still preserves its original response and logs the persistence failure. Production Web configuration must provide the same `ConnectionStrings:PlatformDatabase` secret used for the central platform store.

## Automated coverage

Focused tests cover API and Web event fields, trusted/null tenant semantics, null clinical actor, actual source application, correlation, governed mapping, all twelve annotated host endpoints, multiple-requirement and repeated-evaluation deduplication, successful authorization producing no denial, missing-subject suppression, mismatched-policy suppression, persistence-failure denial preservation and the existing Step 19A stored-procedure/repository contract. Existing successful-read audit tests remain the regression evidence for `PatientChartOpened`, `EncounterViewed`, `PatientDocumentViewed`, `PatientFileDownloaded`, `ReportExecuted` and `CsvExported`.

## Manual runtime verification checklist

Use only test users and test patients. For every event, filter the central table by the captured request trace identifier and confirm exactly one row.

1. Without `Patients.View`, open a Patient Chart. Confirm the existing denial, `PatientChartView` / `Patients.View`, correct subject and trusted tenant when API-owned, and no `PatientChartOpened`.
2. Without `Encounters.View`, open an encounter. Confirm one `EncounterView` denial and no `EncounterViewed`.
3. Without `Documents.View`, open a patient document. Confirm one `PatientDocumentView` denial and no `PatientDocumentViewed`.
4. Without `Documents.View`, download a patient file. Confirm no response file bytes, one `PatientFileDownload` denial and no `PatientFileDownloaded`.
5. Without `Reports.View`, execute the appointment status report. Confirm one `AppointmentReportRun` denial and no `ReportExecuted`.
6. Without `Reports.Export`, export appointment CSV. Confirm no CSV bytes, one `AppointmentReportExport` denial and no `CsvExported`.
7. Repeat all six with an authorized user. Confirm normal operation and successful clinical/read events, with no `MissingPermission` rows.
8. Repeat a representative direct API denial in Tenant B. Confirm the resolved Tenant B UID and no Tenant A contamination.
9. Repeat a Web-owned denial. Confirm source `MicroEMR.Web` and null tenant because Web has no resolved trusted tenant context.
10. Temporarily make the platform audit writer unavailable in a controlled environment. Confirm the original denial is unchanged and the operational error includes the trace identifier without database details reaching the caller.

## Known limitations and next slice

Step 19B intentionally omits resource identifiers and does not audit unauthenticated requests, ordinary 404s, validation failures, tenant trust-boundary denials, unresolved clinical actors or ownership denials. It adds no durable retry; a failed audit attempt is operationally logged.

The recommended next work is Step 20, split between analysis and implementation for `CrossPatientOwnership` and `UnresolvedClinicalActor`. Tenant denials remain a later, separate trust-boundary slice and must not be combined automatically.
