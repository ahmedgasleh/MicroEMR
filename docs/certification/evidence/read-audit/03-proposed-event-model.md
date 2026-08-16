# Proposed sensitive-read event model

## Minimal fields

| Field | Classification | Design rule |
|---|---|---|
| `AuditEventUid` | REQUIRED | Server-generated stable GUID for evidence/export/deduplication. |
| `TenantUid` | REQUIRED logically | Derived from trusted tenant context/database identity, never the browser. Store explicitly in central exports; validate or derive in tenant-local storage. |
| `ClinicalUserId` | REQUIRED for successful clinical reads | Resolved by `IAuthenticatedClinicalUserAccessor`; never parse numeric OIDC `sub`. |
| `AuthSubjectId` | RECOMMENDED for platform/security events | Opaque subject from authenticated principal; avoid duplicating it in clinical rows unless investigation requirements justify it. |
| `PatientUid` | REQUIRED for patient-scoped reads | Use authoritative patient/resource ownership resolution. Do not trust an unmatched route value. |
| `ResourceType` | REQUIRED | Stable controlled value: `Patient`, `Encounter`, `PatientDocument`, `PatientFile`, `Referral`, `Report`, `AuditLog`. Never UI route names. |
| `ResourceUid` | REQUIRED when a stable resource exists | Authoritatively resolved UID; nullable for aggregate report execution. |
| `Action` | REQUIRED | Controlled semantic verb such as `ChartOpened`, `Viewed`, `Downloaded`, `Printed`, `Executed`, `Exported`. |
| `EventCategory` | REQUIRED | `ClinicalRead`, `ClinicalDisclosure`, `AdministrativeRead`, or `SecurityDenial`. |
| `OccurredAtUtc` | REQUIRED | Server/database UTC timestamp. |
| `RequestCorrelationId` | REQUIRED | Server trace/correlation identifier, not accepted as authoritative identity. |
| `SourceApplication` | RECOMMENDED | Controlled value such as `MicroEMR.Web` or `MicroEMR.Api`. |
| `Outcome` | REQUIRED | `Succeeded`, `Denied`, or `AuditWriteFailed` in the appropriate store. Do not log a successful view before ownership and response success are known. |
| `Purpose/Reason` | OPTIONAL | Only if a future workflow requires user-entered access purpose/break-glass rationale. |
| IP address | OPTIONAL | Security value versus personal-data/proxy accuracy requires privacy and operational review; use normalized trusted-forwarder value only. |
| User agent | NOT RECOMMENDED initially | High-cardinality, spoofable, storage-heavy; operational request logs can retain it if approved. |
| Safe details JSON | OPTIONAL | Whitelisted report type/date range/filter names or deduplication metadata; bounded length and no results/clinical content. |
| Old/new values | NOT RECOMMENDED for reads | Read events describe access, not copy record content. Existing mutation rows retain their semantics. |

Example semantic event: actor 73 `Viewed` resource `Encounter/{uid}` for `Patient/{uid}`. Never copy encounter notes, diagnoses, document contents, names, health-card numbers, search strings, report rows, file paths, access tokens, or connection data into the event.

## Trusted identity handling

- Successful clinical read: authentication -> validated active tenant membership -> trusted tenant context -> resource ownership lookup -> clinical-user mapping -> audit insert.
- Authenticated platform user without a clinical identity: may generate platform administrative-read events using opaque subject; cannot produce a successful clinical-read event and should remain denied where clinical identity is required.
- Administrator viewing non-clinical platform data: record opaque subject, target tenant/user if applicable, no clinical user or patient.
- Unauthenticated denial: security telemetry may contain correlation, safe endpoint category, outcome, time, and network metadata; no invented actor, tenant, patient, or resource ownership.

For a resource route containing both patient and resource UIDs, capture the patient obtained from the authoritative resource lookup or verify exact compound ownership before auditing. A manipulated route must never create evidence falsely associating another patient's resource with the supplied patient.
