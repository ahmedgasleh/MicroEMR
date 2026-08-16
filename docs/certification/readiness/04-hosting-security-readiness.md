# Hosting and Privacy/Security Readiness

## Architecture evidence

### Current implementation

- `MicroEMR.Auth`: OpenIddict authentication and shared identity/platform administration.
- `MicroEMR.Web`: browser-facing MVC/UI and token-authenticated API client boundary.
- `MicroEMR.Api`: authoritative API authorization, tenant and clinical-actor middleware.
- `MicroEMR.Core`: shared domain abstractions.
- `MicroEMR.Application`: contracts, services, permissions and business rules.
- `MicroEMR.Infrastructure`: SQL Server repositories, tenant database resolution and migrations.
- Shared platform/authentication data is separated from per-tenant clinical databases.
- Effective permissions supplement authentication; clinical mutations resolve tenant-local clinical actors.

### Planned production architecture

No production cloud/provider topology is established by repository evidence. Local paths and development configuration must not be described as deployed hosting controls.

## Hosting inventory

| Area | Status | Needed evidence/action |
|---|---|---|
| Hosting provider, geography, residency, subprocessors | NOT ESTABLISHED | Business/provider decision and contracts. |
| Tenant database isolation | IMPLEMENTED | Runtime isolation and operational-access evidence. |
| Backups, retention and restore testing | EVIDENCE NEEDED | Policy, configuration, restore results. |
| DR, RPO and RTO | NOT ESTABLISHED | Approved architecture, runbook and exercise. |
| Availability/log/security monitoring and alerting | PLANNED | Production tools, ownership and response evidence. |
| Incident response | EVIDENCE NEEDED | Policy, contacts, exercise and notification workflow. |
| Vulnerability/patch/dependency management | PARTIAL | Development practices exist indirectly; policy, cadence and reports needed. |
| Administrative access and secrets/certificates | PARTIAL | Product configuration exists; production PAM, rotation and custody evidence needed. |
| Malware/endpoint/provider assurance | NOT ESTABLISHED | Provider and operational controls. |
| Change/capacity/support management | EVIDENCE NEEDED | Approved processes, metrics and records. |

## Privacy and security inventory

| Control | Product / Operational / Evidence status |
|---|---|
| Authentication/session/token handling | PRODUCT CONTROL: OpenIddict/OIDC implemented; runtime/configuration evidence needed. |
| User administration/access profiles | PRODUCT CONTROL: tenant memberships, roles and effective permissions implemented; access-review process needed. |
| API authorization | PRODUCT CONTROL: permission policies widely present; complete negative-test evidence required. |
| Tenant isolation | PRODUCT CONTROL: trusted resolution and per-tenant connections; penetration/runtime proof required. |
| Patient/data-level isolation | PARTIAL PRODUCT CONTROL: patient-scoped routes/procedures vary by domain; systematic test matrix required. |
| Audit logging | PARTIAL PRODUCT CONTROL: many mutations audited; completeness, retention, review and tamper controls need evidence. |
| PIA and TRA | DOCUMENTARY EVIDENCE: NOT STARTED/NOT FOUND. |
| Incident response and breach handling | OPERATIONAL CONTROL: NOT ESTABLISHED. |
| Secure SDLC, vulnerability management and penetration testing | DOCUMENTARY/OPERATIONAL EVIDENCE NEEDED. |
| Production keys, certificates and secrets | OPERATIONAL CONTROL: NOT ESTABLISHED by repository evidence. |

