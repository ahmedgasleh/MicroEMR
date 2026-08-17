# Security denial classification

This is a product-security and certification-readiness design, not a claim that OntarioMD Privacy & Security 2.1 requires every event below. Exact current OntarioMD denial-audit wording was not found locally and requires interpretation.

| Category | Example | Classification | Reason and event ownership |
|---|---|---|---|
| Authentication | Missing/invalid/expired bearer token on a sensitive API | OPERATIONAL LOG ONLY | Authentication handler owns it before trusted subject/tenant context. Retain bounded operational telemetry; do not create tenant clinical events or durable records for routine anonymous probes. Escalate repeated targeted patterns later. |
| Permission | Authenticated user lacks `Patients.View`, `Encounters.View`, `Documents.View`, `Reports.View` or `Reports.Export` | SECURITY AUDIT REQUIRED | A known subject attempted a protected clinical disclosure. Authorization-result hook owns one event with capability and missing permission; controller must not duplicate it. |
| Mutation permission | Authenticated user lacks a clinical/admin mutation permission | SECURITY AUDIT RECOMMENDED | Significant for privilege probing, especially user/access administration. Begin after sensitive-read permission coverage. |
| Tenant claim | Missing, malformed or duplicate tenant claim | SECURITY AUDIT RECOMMENDED | Known authenticated subject but no trusted tenant. Platform security stream only; requested claim is untrusted. |
| Tenant membership | Inactive/missing exact subject/tenant membership | SECURITY AUDIT REQUIRED | Strong stale-token or cross-tenant signal. `TenantResolutionMiddleware` owns one platform event. |
| Tenant unavailable | Inactive catalog tenant or invalid assignment/identity | OPERATIONAL LOG ONLY | Often configuration/availability rather than hostile action. Alert operationally; classify as security only when evidence shows manipulation. |
| Cross-tenant resource | Tenant A subject supplies a Tenant B resource UID | SECURITY AUDIT REQUIRED | Trusted tenant is A; A's database normally returns not-found. A central security stream is needed to correlate attempts without querying B or disclosing existence. |
| Cross-patient ownership | Resolved resource belongs to Patient A but is requested through Patient B context | SECURITY AUDIT REQUIRED | Trusted tenant, actor and authoritative ownership exist. Domain ownership boundary owns one event while outward 404 remains unchanged. |
| Unresolved clinical actor | Authorized authenticated subject lacks active tenant-local clinical mapping for a sensitive action | SECURITY AUDIT REQUIRED | Opaque subject and trusted tenant exist, but clinical UserId must remain null. Actor-resolution boundary owns it. |
| Resource not found | Random/nonexistent UID | DO NOT AUDIT | A single ordinary 404 is ambiguous and noisy. Future repeated enumeration patterns may be monitoring candidates. |
| Validation | Invalid date, row version, required field or malformed ordinary input | DO NOT AUDIT | Normal user/application error, not a security event. Keep validation response and ordinary diagnostics. |
| Validation abuse | Repeated malformed identifiers or oversized/probing patterns | NEEDS SPECIFICATION INTERPRETATION | Future rate/anomaly layer may aggregate signals; do not create one durable event per invalid input now. |
| Report/export denial | Missing `Reports.View`/`Reports.Export` | SECURITY AUDIT REQUIRED | Explicit aggregate clinical disclosure attempt; authorization hook owns it. |
| User/access administration | Missing `Users.Manage*` or tenant-admin constraint | SECURITY AUDIT REQUIRED | Privilege-management probing is security significant and platform scoped. |
| Tenant selection | User selects tenant without active membership | SECURITY AUDIT REQUIRED | Auth/platform boundary has authenticated subject and can distinguish requested/untrusted tenant from validated membership. |

`CLINICAL AUDIT ONLY` does not apply to these denied actions: no clinical disclosure or mutation succeeded. Successful reads and mutations remain clinical audit events. A denial may warrant security audit even when its outward response is deliberately indistinguishable from not-found.

## Workflow feasibility

| Workflow | Current boundary | Feasibility |
|---|---|---|
| Patient Chart | permission policy; chart-open resource resolution; clinical actor resolution | High for missing permission and unresolved actor; ownership denial requires explicit resolution result |
| Encounter view | `Encounters.View`; resource/detail or compound patient route | High for permission; high for compound cross-patient routes |
| Patient Document view | `Documents.View`; document resolution | High for permission; ownership event only where both requested patient and authoritative owner exist |
| Patient File download | `Documents.View`; compound patient/file lookup before storage | High for permission and cross-patient denial; never include filename/content |
| Report/CSV | `Reports.View`/`Reports.Export`; distinct API actions | High for missing permission; aggregate event has no patient |
| Clinical mutations | permission and actor middleware plus domain ownership | Feasible later; broader surface and duplicate risks |
| User administration | action-specific permissions and platform audit | High, platform security stream |
| Tenant selection/resolution | Auth selection validation and API tenant middleware | High, platform security stream |

## Noise policy

Do not durably audit public endpoint traffic, favicon/static-file failures, ordinary 404s, normal form validation, browser retries, cancellations or service outages as access denials. Rate and anomaly detection should aggregate repeated patterns using operational telemetry rather than flooding the durable event store.
