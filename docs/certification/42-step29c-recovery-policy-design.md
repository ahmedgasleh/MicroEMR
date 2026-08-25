# Step 29C — Recovery policy design

Date: 2026-08-25

Status: **PROPOSED — REQUIRES OPERATIONAL APPROVAL**

## Purpose and authority boundary

This document proposes the operational policy decisions required before MicroEMR backup automation can be designed or enabled. It does not implement jobs, change recovery models, establish monitoring, select a hosting vendor, or prove recovery.

The repository identifies OntarioMD Hosting 1.3 and Privacy & Security 2.1 as applicable, but contains neither the detailed Hosting 1.3 clauses nor the substantiation/evidence rubric. No target below is represented as OntarioMD-mandated. Business, clinical, privacy, security, infrastructure, and service owners must approve the targets after a business-impact analysis and exact-requirement review. Exercises must demonstrate achievability before any RTO or RPO becomes a service commitment.

Step 29B remains the technical backup/restore design baseline. Step 29A's tenant SQL TLS prerequisite remains open. This step changes neither condition.

## Recovery units and coupling

| Recovery unit | Contents | Coupling policy | Priority |
|---|---|---|---|
| Auth database | Identity users/roles and OpenIddict applications, grants, authorizations, scopes, and tokens | Recover with Platform to a reviewed compatible checkpoint for a shared-service incident; validate subject-to-membership relationships | Shared-service tier |
| Platform database | Tenant catalogue/assignments, memberships, tenant roles/access, audit, and provisioning state | Recover with Auth when shared identity/routing state is affected; do not restore for an isolated tenant-data incident | Shared-service tier |
| Individual tenant clinical database | One tenant's clinical, scheduling, audit/history, actor mapping, migration ledger, and tenant identity | Recover independently with the matching tenant file/artifact recovery set while Auth/Platform remain authoritative | Clinical tier |
| Patient-file storage | Uploaded patient content addressed by tenant/patient-qualified opaque keys | Recover with the matching tenant clinical DB recovery set; SQL metadata must not point to missing content | Clinical file tier |
| Generated clinical artifacts | SQL-resident prescription artifacts; file-backed final encounter/document PDFs with SQL metadata | SQL-resident artifacts follow the tenant DB; file-backed artifacts follow the tenant file snapshot | Same tier as source tenant |
| Service configuration | Versioned application artifacts, protected deployment baseline, DNS/proxy/storage settings, tenant secret references, and dependency inventory | Recover before starting services; values come from approved protected systems, not the repository or backup scripts | Foundation tier |
| Key material | OIDC signing/encryption keys, ASP.NET Core data-protection key ring, TLS certificates, backup-encryption recovery keys, secret-store recovery/identity dependencies | Recover/rotate according to purpose before dependent services or backup media are used | Foundation tier |

The current repository does not configure durable ASP.NET Core data-protection key persistence and uses development OpenIddict certificates. Production key models are implementation gaps; this policy defines their recovery obligations without treating development keys as recoverable production controls.

## Target model

All numerical values are planning candidates, not approved policy or certification claims.

### Data-loss targets

| Recovery scope | Candidate RPO | Rationale and constraint | Status |
|---|---:|---|---|
| Tenant clinical SQL | 15 minutes | Candidate `FULL` recovery/log-backup interval for active clinical writes. Requires recovery-model approval, initialized and continuously monitored log chains, and successful point-in-time exercises. | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Patient-file store and file-backed artifacts | 60 minutes maximum, with an objective to align more frequently to SQL recovery markers | Current provider has no versioning/snapshot mechanism. The effective coordinated tenant RPO is the worse of SQL and file recovery points; a 15-minute tenant-wide RPO cannot be claimed until file protection matches it. | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Auth | 30 minutes | Identity/token state changes less frequently than clinical writes but remains required for access and recovery. Requires `FULL` recovery/log protection if approved. | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Platform | 30 minutes | Tenant assignment, membership, permission, and audit changes are security-sensitive shared state. Coordinate checkpoint identifiers with Auth. | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Full environment | No independent number until dependency inventory and coordinated exercises exist | The achievable point is constrained by the oldest consistent Auth/Platform/tenant/file/key/config recovery set. | **NEEDS OPERATIONAL POLICY DECISION** |

The candidate composite single-tenant RPO is currently **60 minutes**, not 15 minutes, because file snapshots are the limiting unit. A future storage provider with continuous versioning or coordinated snapshots could support a lower approved composite target.

### Service-restoration targets

| Recovery scenario | Candidate target category | Planning value | Status |
|---|---|---:|---|
| One tenant; Auth/Platform and hosting remain available | Urgent tenant recovery | Restore validated access within 4 hours of authorization | **PROPOSED — REQUIRES OPERATIONAL APPROVAL AND EXERCISE** |
| Auth/Platform shared-service recovery before tenant access | Critical shared-service recovery | Restore shared authentication/routing within 4 hours, then validate tenant access | **PROPOSED — REQUIRES OPERATIONAL APPROVAL AND EXERCISE** |
| Full environment including infrastructure, keys/config, all DBs and files | Disaster recovery | Restore validated priority service within 24 hours; complete tenant restoration sequence must be separately measured | **PROPOSED — REQUIRES OPERATIONAL APPROVAL AND FULL EXERCISE** |

These are engineering planning targets only. They exclude any unapproved assumption about staffing, 24x7 response, provider recovery, data volume, number of tenants, or damaged key material. Measured exercises must either validate or revise them.

## Policy decision matrix

| Policy area | Proposed decision | Evidence/rationale | Approval required | Status |
|---|---|---|---|---|
| RPO | Use differentiated candidates: tenant SQL 15 minutes; Auth/Platform 30 minutes; file storage 60 minutes; composite tenant 60 minutes until file protection improves. | Clinical writes are more frequent; file/SQL atomicity is absent; shared state is security-critical. | Business owner, clinical safety, privacy, infrastructure | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| RTO | Candidate 4 hours for isolated tenant or shared-service recovery and 24 hours for priority full-environment service. Never publish until exercised. | Separate incident scopes have materially different dependencies. | Business/service owner, clinical operations, incident/DR owner | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Retention | Candidate daily 35 days, weekly 13 weeks, monthly 13 months; log chain retained at least through all dependent retained recovery points; file snapshots mirror the paired SQL set; restore evidence 7 years if PHI-free. | Provides multiple generations and auditability, but no legal/contract evidence establishes durations. | Privacy/legal, records management, security, business owner | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Backup schedule | Candidate daily SQL full; differential every 6 hours if measured restore benefit justifies it; logs every 15 minutes for tenant DBs and 30 minutes for Auth/Platform if `FULL` is approved; hourly file snapshots with daily coordinated recovery set. | Supports candidate targets while preserving conditional recovery-model decisions. | Infrastructure/DB owner, business owner | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Off-site copy | Maintain a protected copy outside the primary host and a second copy outside the primary failure domain. | Primary-host or site loss must not destroy recovery material. | Security, privacy/residency, infrastructure, vendor management | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Immutability | One access-isolated copy is deletion-protected/immutable for its approved retention tier; production backup administrators cannot shorten it alone. | Protects against ransomware, credential compromise, and accidental deletion. | Security, privacy/legal, infrastructure | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Encryption | Encrypt SQL backups, file snapshots, manifests, and transfers; escrow recovery keys separately with tested recovery and dual control. | Backup media contains PHI and identity/security state. | Security/privacy and key custodian | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Restore authority | Routine test restores require operations approval; tenant production recovery requires incident owner plus clinical/business owner; overwrite/cutover requires dual approval; full DR requires executive incident authority plus security/privacy and operations. | Restore can overwrite current data, expose PHI, or alter access state. | Named roles must be assigned | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Service identities | Separate non-interactive backup executor, restore executor, and backup administrator; no shared DBA credentials in jobs/scripts. | Limits read, overwrite, deletion, and policy-administration blast radius. | Security/IAM, DBA, infrastructure | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Legal hold | Hold suspends expiry/destruction for explicitly scoped sets without silently changing normal policy; request, scope, custodian, review, and release are recorded. | Prevents required evidence/records from expiring while avoiding indefinite global retention. | Privacy/legal/records authority | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Secure disposal | After expiry and hold checks, authorized automation deletes all replicas/staging copies; failed deletion alerts; media uses approved sanitization or cryptographic erasure; destruction evidence is retained without PHI. | Retention must have a governed endpoint across all copies. | Privacy/legal, security, infrastructure | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |
| Test cadence | Monthly rotating tenant restore; quarterly Auth/Platform coordinated restore; annual full-environment DR exercise; repeat after material architecture/key/provider change. | Restore proof, not backup-job success, establishes recoverability. | Business, clinical, security, infrastructure/DR | **PROPOSED — REQUIRES OPERATIONAL APPROVAL** |

## Schedule concept

No job is implemented by this design.

| Protection | Candidate schedule | Conditions |
|---|---|---|
| Tenant DB full | Daily, checksum/encryption/compression where supported | Every assigned active or retained tenant appears in inventory. |
| Tenant DB differential | Every 6 hours | Use only if restore testing proves better RTO without unacceptable chain complexity. |
| Tenant DB transaction log | Every 15 minutes | Only after `FULL` recovery approval, initial full backup, verified log chain, capacity planning, and alerting. |
| Auth/Platform full | Daily under one shared-service checkpoint identifier | Independent backups, coordinated checkpoint, subject/membership reconciliation. |
| Auth/Platform differential | Every 6 hours | Conditional on measured benefit. |
| Auth/Platform transaction log | Every 30 minutes | Conditional on `FULL` recovery and monitored chains. |
| Tenant file snapshot | Hourly; one daily snapshot explicitly paired to the daily SQL recovery set | Must include uploads and file-backed artifacts and expose immutable snapshot/version IDs. |
| SQL/file consistency set | Daily and before high-risk tenant change/import; more frequently if provider capabilities permit | Quiesce/drain or use a validated application-consistent snapshot mechanism. |
| Integrity/media verification | Every run uses checksums; daily media/header verification; monthly restore performs `DBCC CHECKDB` and file reconciliation | `VERIFYONLY` never substitutes for restoration. |
| Off-site replication | Start immediately after local backup/snapshot; alert if candidate RPO window is exceeded | Destination must be independently authenticated and residency-approved. |

If a database remains in `SIMPLE`, log scheduling is inapplicable and the achievable RPO is limited by full/differential cadence. Recovery models must not be changed until approved and inventoried.

## Shared recovery-set model

Each coordinated set receives an opaque identifier such as `environment/tenant-uid/UTC-run-uid`; the identifier contains no patient data or credential. The protected recovery manifest records:

- environment and recovery-set ID;
- tenant UID/key and exact assigned database name;
- UTC quiescence start, drain completion, SQL endpoint, file snapshot completion, and release time;
- SQL backup type, backup-set identifier, first/last/checkpoint LSN, recovery model, checksum, encrypted-media/key reference, size, and location references;
- file provider/root or bucket reference, immutable snapshot/version ID, object count/bytes, manifest hash, and storage-encryption/key reference;
- backup job/run and non-interactive execution identity;
- schema/migration version and expected `TenantDatabaseIdentity` without secret data;
- media/integrity, replication, reconciliation, alert, exception, and approval evidence.

The manifest is signed or stored in tamper-evident protected storage. It contains references to secrets/keys, never values. A set is eligible for restore only when both SQL and file members, integrity results, off-site status, and required approvals are complete. Partial sets are failed/quarantined, not advertised as recovery points.

## Consistency-window policy

Until a vendor-validated application-consistent snapshot exists:

1. Enter a tenant-scoped maintenance state that blocks clinical mutations, file uploads, and artifact generation.
2. Drain in-flight writes and record the recovery-set start.
3. Take the SQL backup/log endpoint and file snapshot while writes remain quiesced.
4. Record both under one recovery-set identifier and verify transfer/integrity.
5. Reconcile each active `PatientFile` and available file-backed `ClinicalOutputArtifact` to content existence, size, and SHA-256 where stored.
6. Quarantine unexpected orphan objects; never delete them automatically.
7. Release maintenance only after a complete set or record the run as failed.

The policy owner must approve the maximum consistency-window duration and permitted service impact. “Backups started near the same time” is not an application-consistency guarantee.

## Identity and authorization model

| Identity/role | Permitted | Prohibited |
|---|---|---|
| Backup executor | Read/database-backup operations required for assigned DBs; create encrypted objects/manifests in the primary backup landing area | Restore/overwrite DBs, change retention, delete immutable copies, administer users, read application secrets |
| Restore executor | Read approved recovery sets; restore only to approved target instances/paths; run validation queries/checks | Change backup policy/retention, delete source backups, self-authorize production cutover |
| Backup administrator | Configure schedules/destinations/retention and review job evidence | Read clinical content by default, perform unapproved production restore, serve as sole immutable-copy deletion authority |
| Recovery approver | Select incident scope/recovery point and authorize the relevant restore/cutover | Execute using privileged service credentials or approve own exceptional deletion |

Routine non-production test restore: operations/DB owner approval with a documented synthetic/non-production target. Tenant recovery: incident commander plus tenant clinical/business authority. Production overwrite/cutover: dual approval including an independent clinical/business data owner; privacy/security joins when confidentiality, integrity, audit, or breach scope exists. Full-environment DR: executive incident authority, operations/DR owner, and security/privacy approval. Emergency action must use governed break-glass identity, time-bound access, complete logging, and retrospective review.

## Off-host, off-site, immutability, and residency

- The first protected copy must leave the primary SQL/file host promptly; a host-local `.bak` or filesystem copy is not a backup policy outcome.
- A second copy must be in an independently failing location/account with separate credentials and administrative boundary.
- At least one copy must be immutable/deletion-protected for the approved tier. Backup administrators cannot alone disable protection, shorten retention, or destroy encryption keys.
- All copies and manifests are encrypted in transit and at rest. Key escrow is separate from encrypted media and recoverable without the failed primary environment.
- Access is deny-by-default, non-public, network-restricted, strongly authenticated, logged, alerted, and periodically reviewed.
- Canadian residency, cross-border access, provider/subprocessor terms, breach obligations, durability, and secure deletion remain **NEEDS OPERATIONAL POLICY DECISION**. No vendor is selected.

## Key and configuration recovery

| Dependency | Required recovery copy/escrow | Recovery rule |
|---|---|---|
| OIDC signing/encryption material | Approved HSM/vault or encrypted offline escrow in an independent failure domain; certificate chain and key identifiers in manifest | Restore before Auth issues/validates dependent tokens; rotate/revoke if compromise is suspected. Development certificates are not a production plan. |
| ASP.NET Core data-protection key ring | Durable protected shared key repository with encryption at rest and independent recovery copy | Preserve required cookie/antiforgery/data lifetimes or deliberately invalidate sessions under an approved incident decision. Current production persistence is not established. |
| TLS certificates/private keys | Managed certificate source or encrypted escrow, chain, SAN inventory, renewal/revocation procedure | Restore only to approved identities/endpoints with private-key ACLs; never place keys in evidence. |
| Backup encryption keys | Separate dual-controlled escrow/HSM/vault plus tested recovery path | Must survive loss of the primary environment and remain available for every retained encrypted set. Loss means the backup is unrecoverable. |
| Secret-store configuration | Independently protected tenant/database secret-reference catalogue, access policy/IAM recovery, endpoint/config inventory | Recover protected system and reissue secrets where appropriate; never export plaintext secrets into scripts/manifests. |
| Deployment configuration | Versioned, approved, redacted baseline and infrastructure/dependency inventory | Restore exact version, validate drift and environment binding, then inject secrets from the approved system. |

Key recovery is tested as part of restore exercises. Escrow access requires dual control, is logged, and cannot depend solely on the infrastructure being recovered.

## Recovery authorization and sequences

### Tenant-only recovery

1. Incident authority confirms an isolated tenant event, preserves evidence, approves outage/data-loss scope, and records dual approval.
2. Select the latest complete policy-compliant recovery set at or before the approved point.
3. Isolate the tenant and block writes; keep unaffected Auth/Platform and tenants available.
4. Restore the tenant DB under an isolated name and apply the ordered differential/log chain.
5. Restore the matching file snapshot under an isolated root/prefix.
6. Run `DBCC CHECKDB`; verify `TenantDatabaseIdentity`, `SchemaMigration`, bounded representative data, audit/history, and SQL/file existence-size-hash consistency.
7. Validate an isolated application startup, login, tenant selection, representative clinical read/file retrieval, positive tenant access, and cross-tenant denial.
8. Independent approvers review measured loss, exceptions, evidence, and cutover plan; then re-enable access or reject the restore.

Auth/Platform must not be restored merely because one tenant is affected.

### Full-environment recovery

1. Executive incident authority declares DR, isolates damaged systems, preserves evidence, and approves the recovery point/order.
2. Rebuild trusted infrastructure/network/DNS and restore secure configuration, secret-store access, backup keys, data-protection keys, and required certificates.
3. Restore and validate Auth.
4. Restore Platform to the coordinated checkpoint and reconcile Auth subjects, memberships, permissions, audit, and secret references.
5. Verify the full tenant catalogue, assignments, expected recovery sets, and restoration priority.
6. Restore tenant DBs in approved clinical priority order; verify identity and migration state.
7. Restore each matching tenant file recovery set.
8. Run integrity checks and SQL/file reconciliation; document any tenant with a different approved recovery point.
9. Validate service startup, login, tenant selection, representative clinical reads/files, authorization, isolation, audit, and security configuration.
10. Independent operational, security/privacy, and clinical/business approvers enable traffic in controlled stages.

## Exercise and objective evidence policy

Candidate cadence:

- monthly: rotating isolated tenant DB plus matching file-store restore;
- quarterly: coordinated Auth/Platform restore and subject/membership/tenant-catalogue validation;
- annually: full-environment DR exercise, including configuration/key recovery and prioritized tenant restoration;
- event-driven: repeat affected exercises after material SQL/storage/identity/key/provider/topology changes or a failed control.

Each exercise must retain authorization, selected recovery set, UTC timing, achieved RPO/RTO, backup and restore completion, checksum/media results, `DBCC CHECKDB`, tenant identity, migration state, bounded representative synthetic/non-production clinical data, file reconciliation/retrieval/hash, application startup, login, tenant selection, authorization/isolation, exceptions, remediation owner/date, approval, and secure destruction of test copies. No success claim is permitted before objective evidence exists.

## Future alert requirements

Automation must eventually alert on failed or missed jobs; stale backups/snapshots; active tenants absent from inventory; checksum, media, reconciliation, or integrity failure; insufficient landing/off-site capacity; broken/missing log chains; replication delay/failure; incomplete recovery sets; unexpected recovery-model changes; encryption/key-access failure; immutable-policy change; unauthorized restore/download/delete; and approaching retention/key/certificate expiry. Alerts require severity, owner, route, acknowledgement/escalation time, runbook, ticket correlation, and periodic test. Monitoring is not implemented in this step.

## Approval register

The following remain unresolved approvals:

- exact Hosting 1.3 and Privacy & Security requirement mapping;
- business-impact analysis, clinical service priority, support hours, tenant population/data-volume assumptions;
- candidate RPO/RTO values and whether 24x7 response staffing exists;
- recovery models and whether point-in-time recovery is required per database class;
- daily/weekly/monthly/log/file/evidence retention durations and legal-hold authority;
- maximum maintenance/consistency window;
- restore/cutover/overwrite/break-glass role assignments and dual-approval workflow;
- backup, restore, administration, immutable-copy deletion, and key-custodian identities;
- storage provider, Canadian residency, off-site region/failure domain, subprocessors, and contract terms;
- encryption standards, key ownership, escrow sites, rotation/revocation, and recovery-test cadence;
- secure disposal method and destruction-evidence retention;
- exercise cadence, evidence custodian, exception acceptance, and remediation deadlines.

## Remaining implementation gaps

No policy becomes effective until later authorized work supplies SQL recovery-model inventory; encrypted backup/log/differential automation; protected manifests; file snapshot/versioning and reconciliation; tenant maintenance/quiescence; distinct identities/RBAC; immutable off-site storage; durable production OIDC/data-protection/TLS/backup key handling; alerting; an isolated recovery environment; recovery runbooks/tooling; and successful measured exercises.

## One recommended next hosting action

**Convene the named business, clinical, privacy/security, and infrastructure owners to approve or revise the candidate RPO/RTO, retention, recovery-model, and authority matrix in this document.** Do not implement backup jobs until that decision record exists.
