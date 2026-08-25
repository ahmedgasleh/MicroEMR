# Step 29B — Backup and restore readiness

Date: 2026-08-25

Outcome: **DESIGN COMPLETE; OPERATING CONTROL AND RESTORE EVIDENCE MISSING**

## Purpose and evidence boundary

This document defines a coordinated backup-and-restore operating model for MicroEMR's authentication, platform, per-tenant clinical, patient-file, generated-artifact, and recovery-configuration boundaries. It does not claim that backups are configured or that recovery has been demonstrated.

Repository inspection found no SQL backup script, SQL Agent job, maintenance plan, SSMS backup procedure, protected backup destination, backup inventory, restore transcript, `RESTORE VERIFYONLY`, `DBCC CHECKDB`, recovery-model inventory, recovery exercise, or sign-off. The controlled SQL endpoint remains unavailable over the required encrypted connection as documented in Step 29A. No SQL setting, recovery model, schema, migration, application code, secret, file, or infrastructure configuration was changed.

The local development file root exists at the configured non-web path and contained four files totalling 330,642 bytes at review time. Names and contents were not captured. This is an inventory observation, not backup or recoverability evidence.

## Data and ownership inventory

| Recovery unit | Contents and ownership | Current backup status | Recovery coupling |
|---|---|---|---|
| `MicroEMR_Auth` | ASP.NET Core Identity and OpenIddict users, roles, applications, authorizations, scopes, and tokens. Auth service owns schema changes through EF migrations. | **MISSING** | Platform membership keys reference Auth subject identifiers logically, without a cross-database FK. Full-environment recovery must align Auth and Platform state. |
| `MicroEMR_Platform` | Tenant catalog, tenant database assignments, memberships, roles, access profiles, platform audit, and provisioning state. Platform administration owns changes. | **MISSING** | Authoritative routing catalogue for every tenant DB; must agree with restored tenant identities and secret references. |
| One clinical DB per tenant | Patient/clinical/scheduling data, audit/history, `ApplicationUser`, `SchemaMigration`, and exactly one `TenantDatabaseIdentity`. Each tenant owns its clinical record boundary. | **MISSING** | Independently recoverable for a tenant-only incident if Platform/Auth assignments and identities remain valid. |
| Patient-file store | Uploaded bytes and file-backed final encounter/document PDFs beneath opaque tenant/patient keys. SQL holds metadata, storage key, size, and hashes where applicable. | **MISSING** | Must be restored from the same tenant recovery set as the clinical DB. SQL backup alone is insufficient. |
| Prescription artifacts | Finalized `PatientPrescriptionArtifact.ArtifactJson` is stored inside the tenant clinical DB. | Covered only when that tenant DB is backed up; no current backup exists. | No separate file snapshot for the current prescription representation. |
| Authored documents | Structured/generated document content is primarily SQL-resident. `ClinicalOutputArtifact` final PDFs use the external file provider. | **MISSING** | SQL document/content tables and any file-backed artifact must be restored consistently. |
| Configuration and secrets | Connection strings, tenant secret references, OIDC shared secret, storage root/provider settings, certificate references, and deployment configuration. Values are external to tracked source. | **MISSING recovery evidence** | Recover through the approved secret/configuration system, never by embedding credentials in SQL scripts or backup manifests. |

The current SQL deployment is a remote SQL Server 2019 instance reached over a VPN endpoint. Exact edition, storage layout, SQL Agent availability, database owners, service identity, backup service identity, and recovery models are unavailable because secure SQL connectivity and host administration are blocked. Database ownership in this document means application/data stewardship, not the unverified SQL `owner_sid` property.

## Backup architecture

### Common control plane

The backup operator must maintain a protected inventory keyed by environment, recovery-set ID, UTC checkpoint, database name, tenant UID/key where applicable, backup type, first/last LSN where applicable, recovery model, file-store snapshot identifier, encryption/key identifier, checksum/verification result, size, storage locations, retention class, tool/job version, and operator/run identifier. It must contain references—not credentials, connection strings, tokens, private keys, or PHI samples.

Backup jobs must authenticate through a distinct non-interactive identity with only the SQL backup and required storage-write permissions. Restore authority must be narrower and separately approved. Application runtime identities should not write backup locations, and backup readers should not receive application administration rights.

### Platform and Auth databases

- Take independent full backups of `MicroEMR_Auth` and `MicroEMR_Platform`, but assign them the same environment recovery-set/checkpoint identifier.
- Use checksum and compression when supported and validated. Encrypt backup media with an approved certificate/asymmetric-key or provider mechanism; retain the corresponding recovery key through a separately protected escrow process.
- Add differential backups only when database size, restore-chain complexity, and measured RTO justify them.
- Add transaction-log backups only when the database uses `FULL` or `BULK_LOGGED` recovery, the log chain has been initialized, jobs and alerts are operating, and point-in-time recovery is an approved requirement.
- Under `SIMPLE` recovery, transaction-log backups and point-in-time restore are unavailable; full/differential frequency must meet the approved RPO instead.
- Auth and Platform backups should use a coordinated maintenance/checkpoint window. Cross-database transactions do not provide a consistency guarantee, so recovery must verify Auth subject/member referential consistency after restore.

### Tenant clinical databases

- Enumerate active and retained tenant assignments from the authoritative Platform catalog; do not rely on a filename glob or assume one shared clinical database.
- Back up every assigned clinical database separately and label inventory with tenant UID, tenant key, exact assigned database name, recovery-set ID, and schema version. Do not place the tenant connection secret in the inventory.
- Use full backups as the independently restorable base. Differential and log strategies follow the same recovery-model/RPO conditions as above.
- Record `TenantDatabaseIdentity` and `SchemaMigration` verification as post-backup/restore evidence, not as a replacement for a database backup.
- Alert on an active tenant without a successful current backup, an unexpected database, broken log chain, checksum/verification failure, or missing file-store recovery set.

### Patient files and generated artifacts

The current `LocalPatientFileStorage` root must be captured by a snapshot-capable, encrypted, access-controlled backup mechanism. The scope includes:

- uploaded patient files referenced by `PatientFile.StorageKey`;
- file-backed `ClinicalOutputArtifact` final PDFs and any future provider objects;
- directory/object metadata needed to reproduce the opaque keys;
- checksums, sizes, snapshot identifier, and restore inventory without capturing PHI in logs.

SQL-resident prescription JSON artifacts and authored structured document content remain in the tenant DB backup. File-backed final PDFs do not. The future object-storage provider must preserve the same tenant recovery-set contract through versioned snapshots/object versions and immutable backup copy; an object provider name alone is not evidence of backup.

## Recovery-model and point-in-time status

The required inventory query must capture, for every in-scope database, `name`, `recovery_model_desc`, state, compatibility level, owner classification, and most recent full/differential/log backup metadata. This review could not safely execute it because tenant SQL TLS is open and no alternative authorized encrypted administrative connection exists.

| Database class | Recovery model | Log-chain status | Point-in-time recovery |
|---|---|---|---|
| Auth | Unknown | Unknown | **NOT ESTABLISHED** |
| Platform | Unknown | Unknown | **NOT ESTABLISHED** |
| Each tenant clinical DB | Unknown | Unknown | **NOT ESTABLISHED** |

Do not change a recovery model in this step. Changing from `SIMPLE` to `FULL` does not itself create recoverability: an initial full backup and continuously monitored log-backup chain are required. Recovery models and backup cadence must be selected after the RPO/RTO policy decision.

## Backup security and retention

Required controls are:

1. Encrypt each SQL backup and file-store snapshot at rest with approved algorithms and keys outside the backup payload.
2. Use TLS for transfer and prevent staging on unencrypted local/operator media.
3. Keep at least one access-isolated off-host copy and one geographically appropriate off-site/independent-failure-domain copy. Residency and subprocessor approval must precede provider selection.
4. Make one recovery copy immutable or deletion-protected for the policy-defined window, with separate credentials and multi-party/break-glass recovery.
5. Restrict backup creation, read, restore, retention override, and deletion through separate least-privilege roles; log and review every restore/download/delete.
6. Run SQL backup checksums where supported, `RESTORE VERIFYONLY` as an early media/header check, storage-object checksum verification, job monitoring, and scheduled full restores. `VERIFYONLY` does not replace restore plus `DBCC CHECKDB`.
7. Store no usernames, passwords, connection strings, vault values, private keys, tokens, or PHI in scripts, filenames, job output, tickets, or this evidence pack.
8. Back up or reproducibly escrow the configuration catalogue, vault metadata/policies, DNS/certificate references, infrastructure definitions, versioned application artifacts, and backup-encryption recovery keys. Secret values must remain in the approved secret system and be reissued/rotated when recovery risk requires it.

Backup retention duration, generations, legal holds, tenant termination behavior, immutable period, and final secure disposal are **NEEDS OPERATIONAL POLICY DECISION**. Retention must cover clinical/legal obligations and audit availability without keeping PHI indefinitely. Expiration/deletion must be authorized, logged, include replicas/staging media, and respect legal holds.

## Coordinated tenant SQL/file backup

The application writes file content before SQL metadata and attempts cleanup if metadata creation fails; SQL and filesystem operations are not atomic. A valid tenant recovery set therefore needs a controlled consistency boundary:

1. Announce/enter a tenant-scoped maintenance state that blocks uploads, artifact generation, metadata mutations, and other clinical writes while allowing only explicitly approved reads.
2. Drain in-flight writes and record a UTC recovery-set/checkpoint ID.
3. Capture the tenant database full/differential/log endpoint and the file-store snapshot while writes remain quiesced.
4. Record database backup LSN/time, file snapshot ID, tenant identity, and inventory hashes under the same recovery-set ID.
5. Run media/checksum verification, transfer protected copies, then release maintenance state.
6. Reconcile SQL metadata against storage: each active `PatientFile` and available file-backed `ClinicalOutputArtifact` must exist; verify expected size and stored SHA-256 where available. Missing content fails the recovery set. Unreferenced content must be quarantined for governed review, not automatically deleted.

If the storage platform and SQL Server later support a vendor-guaranteed application-consistent snapshot boundary, that mechanism may replace quiescence only after documented validation. Merely starting backups close together is not sufficient evidence.

## Restore decision model

### Tenant-only incident

Restore only the affected tenant DB and its matching file-store recovery set when Platform/Auth remain authoritative and the incident is isolated to that tenant. Do not restore Platform or Auth unnecessarily.

1. Confirm incident scope, authorization, target recovery point, legal/audit preservation, and selected recovery-set ID.
2. Create an isolated restore location and a new test database name; never overwrite an active database during validation.
3. Restore the tenant full backup, differential if selected, and ordered log chain through the selected time using `NORECOVERY`, then `RECOVERY` only at the approved endpoint.
4. Restore the corresponding tenant file snapshot beneath an isolated root/bucket/prefix.
5. Run `DBCC CHECKDB` on the restored database and retain the clean result or escalate every finding.
6. Verify exactly one expected `TenantDatabaseIdentity`, canonical `SchemaMigration` IDs/hashes, representative non-PHI-safe aggregate counts, audit/history availability, and expected critical objects/procedures.
7. Reconcile all relevant file metadata to isolated content; retrieve representative non-production files and verify size/hash without logging content.
8. Point a separately isolated application configuration at the test database and file root. Verify tenant-positive access and cross-tenant denial.
9. Obtain clinical/operations sign-off before any production cutover. Preserve the damaged environment and audit evidence until incident authority releases it.

### Platform/Auth incident

Restore Auth and Platform to a mutually reviewed checkpoint when identity, membership, authorization, tenant catalogue, or provisioning state is damaged. Verify Auth subjects referenced by Platform memberships; OIDC client registration; tenant assignments; access profiles; audit state; secret references; and every tenant identity. Do not roll tenant clinical DBs back merely because Platform/Auth were restored. Reconcile later tenant changes explicitly.

### Full-environment incident

Restore infrastructure/configuration and protected secret/certificate dependencies first; then Auth and Platform from the coordinated checkpoint; then every tenant database and matching tenant file recovery set. Keep public traffic disabled until identity/membership, assignments, tenant identities, migration ledgers, file reconciliation, audit availability, security configuration, and representative end-to-end reads pass. Document tenants intentionally restored to different point-in-time endpoints and obtain business/privacy approval for any data-loss window.

## Controlled restore-test protocol

The first exercise must use non-production data and an isolated SQL instance/database name, file root, credentials, and application configuration. It must not update Platform assignments that active development uses.

Evidence to retain:

- approved test plan, source classification, operators, UTC start/end, tool/server versions, and recovery-set ID;
- backup command/job identity and redacted completion/checksum output;
- protected transfer and isolated destination evidence;
- restore file list and explicit new database name/path;
- successful restore and `DBCC CHECKDB` output;
- `TenantDatabaseIdentity` and `SchemaMigration` verification;
- bounded table counts and synthetic representative records, with no PHI in evidence;
- restored file count/size/hash reconciliation and representative download/hash result;
- measured backup and restore durations, achieved recovery point, exceptions, sign-off, and secure test-data disposal.

### Exercise result for this step

**NOT PERFORMED.** No existing backup mechanism or backup artifact was found. Recovery models and permissions are unknown, required encrypted SQL connectivity is blocked, and no authorized disposable SQL restore target was established. Creating an ad hoc unencrypted backup/restore path or restoring over an active database would violate the task boundaries.

Consequently:

- backup completion: not available;
- restore completion: not available;
- `DBCC CHECKDB`: not run;
- row-count/sample verification: not run;
- tenant identity/schema verification: not run;
- SQL/file reconciliation and retrieval: not run.

## RPO and RTO

Both targets are **NEEDS OPERATIONAL POLICY DECISION**. Repository evidence contains no approved business impact analysis, acceptable clinical data-loss interval, outage tolerance, service hours, dependency recovery assumptions, or legal/certification target. This document therefore does not invent schedules or compliance claims.

The policy owner must define separate targets for Auth/Platform, a single tenant, and full-environment loss. A subsequent engineering validation must demonstrate that backup cadence/log chains achieve each RPO and that measured infrastructure, key/configuration, database, file, reconciliation, and application recovery fits each RTO.

## Remaining gaps and closure criteria

Implementation/infrastructure gaps:

- approved SQL backup automation with encryption, checksum, inventory, alerts, and protected destinations;
- snapshot/versioned backup provider for patient files and file-backed artifacts;
- tenant-scoped maintenance/quiescence or validated application-consistent snapshot mechanism;
- automated SQL/file reconciliation and missing-content alerting;
- isolated restore environment and recoverable encryption-key/configuration dependencies.

Operational/evidence gaps:

- database recovery-model, edition, ownership, permission, and existing-backup inventory;
- approved RPO/RTO and retention/legal-hold/disposal policy;
- backup/restore identities, segregation of duties, off-site/residency/provider approval, and key escrow;
- documented schedules, monitoring, failure escalation, restore authorization, incident decision tree, and evidence custody;
- completed tenant-only, Platform/Auth, and full-environment restore exercises with measured results.

The Backup/Restore gap closes only after every in-scope database and external file store has monitored encrypted backups; configuration/key recovery is proven; SQL/file recovery sets are coordinated and reconciled; and at least one isolated tenant restore plus the policy-defined broader exercises pass integrity, identity, migration, representative-data, file-retrieval, security, RPO, and RTO validation.

**Current classification: OPEN.**

## One recommended next hosting action

After the Step 29A TLS prerequisite is resolved, **inventory and approve the recovery model, RPO/RTO, and retention policy for Auth, Platform, and every tenant database before implementing backup jobs**. Those decisions determine whether log backups are required and prevent an arbitrary cadence from being mistaken for a recoverability control.
