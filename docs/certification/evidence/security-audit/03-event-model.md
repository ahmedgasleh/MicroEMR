# Security denial event model

## Proposed minimal event

| Field | Rule |
|---|---|
| `SecurityEventUid` | Server-generated stable GUID |
| `EventType` | Controlled value such as `SensitiveAccessDenied` |
| `DenialReason` | Required governed value |
| `Outcome` | Always `Denied` for this model |
| `Capability` | Stable semantic identifier, never raw query string |
| `AuthSubject` | Opaque authenticated subject when known; null when unauthenticated |
| `ClinicalUserId` | Resolved tenant-local actor when available; otherwise null |
| `TrustedTenantUid` | Only after trusted resolution; otherwise null |
| `RequestedTenantUid` | Optional and explicitly untrusted; never used for routing |
| `RequestedPatientUid` | Optional requested identifier |
| `AuthoritativePatientUid` | Optional, only after safe ownership resolution |
| `ResourceType` / `ResourceUid` | Controlled type and safely known/requested UID |
| `OccurredAtUtc` | Server UTC time |
| `CorrelationId` | Required bounded request correlation |
| `SourceApplication` | Controlled server application |

No clinical content, report rows, filenames, document titles, routes with query strings, tokens, cookies, authorization headers, connection strings, secrets, database names, stack traces, arbitrary JSON or free-text denial details belong in the event.

## Controlled denial reasons

- `MissingPermission`
- `InvalidTenantClaim`
- `InvalidTenantMembership`
- `CrossTenantAccess`
- `CrossPatientOwnership`
- `UnresolvedClinicalActor`
- `SensitiveResourceAccessDenied`

`Unauthenticated` should initially remain operational telemetry, not a durable security event. `ResourceNotFound`, `ValidationFailed` and `ServiceUnavailable` are deliberately not denial reasons in the first contract.

## Capability identifiers

Initial controlled capabilities should include `PatientChartView`, `EncounterView`, `PatientDocumentView`, `PatientFileDownload`, `AppointmentReportRun`, `AppointmentReportExport`, `ClinicalMutation`, `TenantUserAdministration` and `TenantSelection`. Map endpoint metadata/policies to these values. Do not store raw URLs or query strings.

## Governance

Unknown reasons/capabilities must be rejected, not converted to arbitrary strings. Adding a capability or reason requires a versioned migration/configuration change and tests. Whether source IP, device or user-agent is required remains a privacy/security interpretation question; none is recommended until proxy trust, minimization and retention are approved.
