# Security denial storage and identity model

## Recommended hybrid storage

Successful patient disclosures remain in tenant-local `AuditLog`. Denials are security events and should use a distinct semantic stream so reviewers do not mistake attempted access for disclosure.

The recommended authoritative denial store is a central platform security table/service, separate from `PlatformAuditEvent` unless that existing contract is deliberately evolved through a reviewed migration. It can record pre-tenant failures and correlate cross-tenant patterns. Tenant-resolved high-confidence ownership denials may additionally produce a tenant-local security record only when a governed dual-write/outbox design prevents misleading partial evidence. The first slice should use central storage only; dual recording is deferred.

Central storage advantages are availability before tenant resolution, cross-tenant investigation, consistent opaque-subject representation and operational monitoring. Risks are broader sensitivity, separation-of-duty requirements and a larger compromise domain. Tenant-local storage improves clinic review and residency boundaries but cannot safely record rejected tenant claims and fragments cross-tenant investigations.

## Identity by denial stage

| Stage | Actor | Tenant | Patient/resource |
|---|---|---|---|
| Unauthenticated | no actor; optional privacy-reviewed request metadata | none | capability only; do not resolve clinical objects |
| Authenticated, pre-tenant | opaque `sub`; clinical UserId null | requested claim may be retained only as `RequestedTenantUid` and marked untrusted | do not query tenant clinical resources |
| Tenant claim validated, membership denied | opaque `sub` | catalog tenant may be known, but not an authorized tenant context; store separately from `TrustedTenantUid` | none |
| Trusted tenant, clinical actor unresolved | opaque `sub`; ClinicalUserId null | trusted resolved tenant | capability and safely requested resource UID only where justified |
| Trusted tenant and clinical actor | opaque `sub` plus resolved ClinicalUserId | trusted resolved tenant | requested identifiers plus authoritative owner only if safely resolved before denial |
| Cross-tenant UID attempt | opaque subject/clinical actor in trusted Tenant A | Tenant A only as trusted; do not probe Tenant B | requested resource UID may be retained; owner tenant usually unknown and must not be guessed |

Never backfill a numeric clinical user from `sub`. Never promote a failed browser/token tenant value to trusted identity. Hashing an opaque subject is an operational privacy choice, not a substitute for an approved identity contract.

## Patient and resource identity

Use separate conceptual fields for `RequestedPatientUid` and `AuthoritativePatientUid`. The latter is populated only after a trusted tenant-local lookup establishes ownership. `ResourceUid` may be recorded for authenticated, scoped attempts where investigation value outweighs identifier sensitivity. It must not be echoed to the caller or placed in free-text messages.

For a concealed ownership denial, the API keeps its existing 404. The internal event may record `CrossPatientOwnership`, requested patient, authoritative patient and resource UID under restricted security-reader access. For a resource absent in the trusted tenant, record no authoritative owner and normally create no event.

## Operational controls

Security audit writers require insert-only least privilege. Review/export requires separate authorization and must itself be audited. Production needs encryption, clock synchronization, restricted privileged access, backup/restore evidence, monitoring, legal hold, retention/destruction policy and approved immutable replication. No retention duration is asserted without OntarioMD, legal, privacy and business approval.
