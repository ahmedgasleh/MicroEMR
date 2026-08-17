# Security denial audit implementation plan

## First slice — permission denials on implemented sensitive disclosures

Start with `MissingPermission` for `PatientChartView`, `EncounterView`, `PatientDocumentView`, `PatientFileDownload`, `AppointmentReportRun` and `AppointmentReportExport`.

This slice has clear semantics, a known authenticated subject, stable permission/capability metadata and a centralized authorization result. Store events centrally because a clinical actor may not be resolved and tenant availability may vary. Preserve existing 403 responses. Do not include patient/resource identifiers in this first slice because authorization fails before safe domain resolution.

Required preparation:

1. Approve the central security-event schema, writer permissions, retention owner and reviewer role.
2. Add controlled reason/capability catalogs and an insert-only procedure.
3. Add a reusable recorder and authorization-result hook with one-event-per-request controls.
4. Map the six sensitive capabilities to actual policies.
5. Prove successful requests create no denial event and existing successful-read events remain independent.

## Second slice — trusted-context ownership and actor denials

Add `CrossPatientOwnership` only at compound lookups that can distinguish mismatch from absence without extra disclosure-causing queries. Add `UnresolvedClinicalActor` where trusted tenant and opaque subject exist. Keep outward 404/403 behavior unchanged. Patient File download is a strong initial ownership candidate because its repository already binds patient and file before opening storage; implementation may need a richer internal result to distinguish missing from mismatched safely.

## Third slice — tenant and administrative security

Add `InvalidTenantClaim`, `InvalidTenantMembership`, tenant-selection violation and user/access-administration permission events to the central stream. Treat tenant infrastructure failures as operational availability events. Cross-tenant resource enumeration should begin with monitoring/correlation design, not foreign-database probing.

## Later work

- Aggregated abuse/rate signals and alerts
- Security audit search/export with separation of duties
- Clinic-visible subsets where governance permits
- Retention/legal hold/destruction automation
- Tamper-evident or immutable centralized replication
- Incident-response integration and review cadence

## Automated test plan

1. Missing permission produces exactly one controlled denial event.
2. Successful access produces no denial event.
3. Cross-patient mismatch produces one event where authoritative ownership is already known.
4. Cross-tenant attempt is recorded without querying/disclosing the foreign tenant.
5. Existing 401/403/404 semantics and response bodies remain unchanged.
6. Opaque subject and ClinicalUserId are populated only when trustworthy.
7. Trusted and requested tenant identities cannot be confused.
8. Event contract contains no clinical content, token, secret or raw query.
9. Multiple failed policy requirements do not duplicate events.
10. Unauthenticated traffic follows the operational-log-only decision.
11. Platform and any future tenant-local streams preserve storage separation.
12. Successful clinical read/mutation audit remains independent.

## Runtime evidence plan

Use test identities and patients only. Retain timestamped request/response, correlation ID, narrowly redacted event row and relevant application log.

| Case | Action | Expected outward behavior | Expected future event |
|---|---|---|---|
| Missing `Patients.View` | Open Patient Chart | existing 403/UI denial | one `MissingPermission` / `PatientChartView`, no clinical read event |
| Missing `Encounters.View` | Open encounter | existing 403 | one `MissingPermission` / `EncounterView` |
| Missing `Documents.View` | Download Patient File | existing 403, no bytes | one `MissingPermission` / `PatientFileDownload` |
| Cross-patient file | Request Patient A file under Patient B | existing concealed 404 | one `CrossPatientOwnership` only if normal resolution establishes owner |
| Invalid membership | Use stale token after membership deactivation | existing 403 | one `InvalidTenantMembership`, platform stream |
| Cross-tenant UID | Tenant A requests known Tenant B UID | existing 404 | no foreign lookup; central correlation evidence according to approved slice |
| Missing `Reports.Export` | Export appointment report | existing 403, no CSV | one `MissingPermission` / `AppointmentReportExport` |
| Unresolved actor | Sensitive audited action with unmapped subject | existing 403/503 according to current path | one `UnresolvedClinicalActor`; ClinicalUserId null |

Exact OntarioMD validation cases, retention and whether every permission denial must be durable remain specification-interpretation questions.
