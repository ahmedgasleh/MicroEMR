# Storage, retention, integrity and failure design

## Storage recommendation: hybrid

Successful clinical disclosures should extend the existing per-tenant clinical `AuditLog`. This preserves tenant isolation, clinical actor/patient foreign-key relationships, local clinic review, and the existing mutation audit stream. Future structured columns and an insert-only procedure should be additive and compatible with historical rows. Trusted `TenantDatabaseIdentity`/`ITenantContext` supplies tenant identity; any exported or replicated event must carry explicit `TenantUid`.

Platform administrative reads, tenant-selection/security denials, cross-tenant attempts, and audit-system operations should use the platform audit/security stream. They can occur before a clinical database is safely selected. Do not duplicate successful clinical events centrally in the product transaction path initially. A later operational immutable copy/SIEM can consolidate both streams using event UIDs.

Tradeoffs:

- Tenant-local storage provides strong isolation and patient linkage but makes cross-tenant investigation and uniform retention harder.
- Platform-central storage supports investigation but increases breach radius and creates a sensitive activity index.
- Hybrid routing matches trusted context and avoids forcing rejected requests into a tenant database. Operational replication can provide global oversight without making the platform DB the primary clinical audit store.

## Write and failure semantics

Initial read-audit writes should be synchronous through a stored procedure so the application knows whether evidence was persisted. Do not use in-memory fire-and-forget, browser-generated events, or a buffer that can be silently lost.

| Action | Recommended audit failure behaviour | Rationale / approval needed |
|---|---|---|
| Download, print, export, audit-log view/export | Fail closed with 503 and emit high-severity operational event | Portable disclosure must not occur without its audit record. Confirm business/privacy owner accepts availability tradeoff. |
| Encounter/document detail view | Prefer fail closed for first slice | High-sensitivity deliberate disclosure; simplest reliable evidence. Clinical-safety exception policy requires governance before any fail-open path. |
| Patient chart open needed for care | Policy decision: fail closed by default; a formally approved emergency fail-open mode must synchronously raise a critical security event and preserve retry evidence | Denying access may affect care; silently allowing unaudited access undermines the control. OntarioMD/privacy interpretation required. |
| Security denial logging | Original denial always remains denied; logging failure must not change it | Emit fallback infrastructure alert without exposing identifiers. |

Database deadlock/transient failure may receive a small bounded server retry. No client retry may cause duplicate disclosure events. If reliable queuing is later approved, it requires durable encrypted storage, idempotency, monitoring, replay, poison handling and evidence; it is not part of the first slice.

## Product integrity controls

- Insert-only application stored procedure; no update/delete API or ordinary application permission.
- Least-privilege SQL principal granted execute on insert/search procedures, not direct table mutation.
- Server-generated event UID/time and validation against trusted actor, patient/resource and tenant identity.
- Controlled event/action/resource values, bounded safe details, indexes for time/user/patient/resource.
- Audit-review access and export are themselves audited.
- Soft deletion is not appropriate for audit evidence; correction should be an appended event, never an overwrite.

## Operational/cloud controls and evidence

- Restricted DBA/support access with privileged-access logging and periodic review.
- Encrypted backup, restore tests, clock synchronization, monitoring of write failures and capacity.
- Append-only/immutable centralized copy or SIEM where justified, protected from application credentials.
- Documented incident correlation, export custody and integrity verification. Checksums/signatures are optional until threat/risk analysis shows a need; database grants plus immutable replication are the pragmatic first controls.
- Evidence: deployed grants, configuration exports, access-review records, sample alerts, immutable-retention settings, restore results and penetration-test findings.

## Retention

No retention period is specified because the exact OntarioMD requirement and approved business/privacy policy are unavailable. Obtain authoritative requirements covering minimum/maximum retention, legal holds, patient-record relationship, tenant termination, backups, correction, archive retrieval and destruction authorization. Capacity planning must account for chart opens plus every download/export, indexes and immutable copies. Audit search/export must remain available throughout the approved period. Normal clinical archive/deletion must not cascade-delete audit rows.
