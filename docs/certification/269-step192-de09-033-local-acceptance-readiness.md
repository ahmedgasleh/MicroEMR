# Step 192 — DE09.033 local development acceptance readiness

2026-10-26 (America/Toronto). **Updated after owner notification: migration 0093 is now VERIFIED APPLIED to the intended development tenant.** Local environment identified: YES. Manual acceptance remains conditional on the outstanding runtime/recovery/clinical-action prerequisites. No migration, clinical save, deployment, restart, fixture creation or test/build rerun was performed by this assessment. PC07.01 remains PARTIAL; **7 mandatory-gap IDs / 3 packages remain unchanged**.

## Follow-up — 0093 application verified

After the owner reported application, a targeted SELECT-only recheck confirmed the same `local-dev-fresh` tenant UID and `MicroEMR_LocalDev_Fresh` database. `dbo.SchemaMigration` now contains `0093-prescription-earliest-pickup`, applied timestamp `2026-10-26T15:53:23.0062913`, with ScriptHash **1D3F745AEF4870CE7C8804B7BEB1B670A08EF7348F94B09319E571271A5EC596**, matching the controlled CanonicalV2 hash. The nullable-payload column, validator function, check constraint, create/update pickup parameters and both finalization review parameters are present. Receipt: `.tmp/step192/0093-applied-confirmation.json`.

**The migration-pending blocker is resolved. Do not reapply 0093.** The pending/absent findings in the original inspection sections below are retained as the earlier baseline and superseded by this follow-up. The remaining actions are current runtime/version verification, recovery-plan confirmation and explicit authorization for the bounded clinical acceptance actions. Application of the migration alone does not attest to those items or authorize saves. No application source or database was changed by this follow-up.

## 1. Evidence reused and read-only scope

Reuse [Step 191A](267-step191a-de09-033-earliest-pickup-contract.md) for the exact CDS-S v2.1 DE09.033 store-only definition and approved contract, and [Step 191B](268-step191b-de09-033-earliest-pickup-implementation.md) for implementation/acceptance instructions. Re-read relevant 0093/schema/runner, launch/tenant-selection and permission definitions only.

Existing `.tmp/step191b/latest-results.json` confirms **162 distinct backend cases with latest PASS outcomes**, including the retained pickup/security/lifecycle coverage. Step 191B retains **18 distinct frontend PASS cases**, TypeScript PASS and affected build PASS. No passing suite or build was rerun. Those results prove isolated behavior, not that the currently running local services contain the feature.

Fresh guarded SELECT-only inspection verified the previously established development Platform/Auth/clinical bindings, target tenant identity, ledger, relevant schema/parameters, account/permissions, existing prescription fixtures and backup history. Connections were checked against the exact approved development endpoint/catalogs before use; no credentials or full connection strings were printed. Sandboxed connectivity failed; authorized outside-sandbox read-only inspection completed. Sanitized receipts: `.tmp/step192/readiness.json`, `manifest-check.json`, `supplement.json`, `roles.json`. Temporary inspection helpers are not application changes.

## 2. Local environment and selection

### Projects and exact launch profiles

All profiles set `ASPNETCORE_ENVIRONMENT=Development`.

| Project | `http` profile | `https` profile |
|---|---|---|
| `src/MicroEMR.Web` | `http://localhost:5100` | `https://localhost:7002;http://localhost:5002` |
| `src/MicroEMR.Api` | `http://localhost:5298` | `https://localhost:7003;http://localhost:5245` |
| `src/MicroEMR.Auth` | `http://localhost:5080` | `https://localhost:7179;http://localhost:5080` |

References: each project's `Properties/launchSettings.json`. Selected development file/user-secret configuration agrees on Web `Api:BaseUrl=https://localhost:7003` and Web/API authority `https://localhost:7179`. Therefore the intended coherent profile set is **https**, with Web at **https://localhost:7002**. The HTTP profiles do not automatically rewrite those API/authority bindings. No service was started/reconfigured, no login performed and no current runtime/version attestation inferred from a profile. Listener inspection was inconclusive in this environment; it is not evidence that services are stopped.

### Verified development databases

| Item | Verified value |
|---|---|
| Configured development SQL endpoint | `192.168.50.1,50013` |
| SQL-reported server identity | `EC2AMAZ-TC26CQK\TELEMED52` |
| Tenant key / UID | `local-dev-fresh` / `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Clinical database | `MicroEMR_LocalDev_Fresh` |
| Supporting databases | `MicroEMR_Platform`, `MicroEMR_Auth` |
| Platform server key | `local-sql` |
| Tenant/database status | Active / Active |
| Clinical timezone | `America/Toronto` |

Platform assignment and actual `dbo.TenantDatabaseIdentity` agree. “Local development” describes this verified development assignment; it does not mean SQL is listening on localhost. The standing owner clarification establishes all development records as fictional/synthetic. No production database was inspected.

Normal selection uses the existing Auth tenant-selection continuation (`AccountController.SelectTenant`) and authenticated tenant claim. API `TenantResolutionMiddleware` revalidates membership and current roles before establishing trusted tenant context/database assignment. Do not use arbitrary query/header tenant overrides. Before later saves, the normal authenticated UI/session must identify the exact tenant above.

### Existing test account

Verified `admin@microemr.local`, Auth subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`: active, email confirmed, no current lockout end; active tenant membership, **ClinicAdministrator** tenant role and active **Clinic Administrator** access profile. Existing metadata allows **Patients.View** and **Prescriptions.Prescribe**. Clinical mapping is active **UserId 2**, active ProviderId **1**, ProviderUid `4432ffba-69f1-4d68-9969-202022051ab0`.

These are eligibility/metadata facts, not a new authenticated permission PASS. No password, token, hash, role change or new account was needed or obtained. The feature requires prescribing permission, not administrative status; this is a suitable existing account, with no claim of non-admin runtime acceptance.

## 3. Migration 0093 readiness and current state

File: [0093-prescription-earliest-pickup.sql](../../db/tenant-clinical/migrations/0093-prescription-earliest-pickup.sql). CanonicalV2 SHA-256:

`1D3F745AEF4870CE7C8804B7BEB1B670A08EF7348F94B09319E571271A5EC596`

The manifest has **94 entries (0000–0093)**; the actual tenant ledger has **93 entries**, latest **0092-encounter-procedures**. The **only pending migration is 0093-prescription-earliest-pickup**. Local hash comparison found no unapproved drift or unexpected applied entry; 18 older hashes match the existing explicitly approved legacy representations. This is a read-only comparison, not a migration runner invocation.

Independent metadata corroborates absence: no `PatientPrescription.EarliestPickup` column, no `PrescriptionPickupValid` function or pickup constraint, and no new pickup/review parameters on the affected procedures. Existing RowVersion remains non-null timestamp/8 bytes. **0093 is not already applied to this tenant.** Earlier disposable test applications do not change that answer.

| Check | Finding |
|---|---|
| Additive storage / legacy compatibility | Adds nullable JSON storage and validator/check constraint; no clinical backfill, deletion or artifact rewrite. NULL legacy records remain valid. |
| Expected procedure changes | Existing GetByPatientUid/GetByUid, CreateDraft/UpdateDraft, Finalize/SignAndFinalize gain pickup mapping/presence, locked correction inheritance and explicit review enforcement. |
| RowVersion / authorization | Existing patient scope, original-prescriber Draft ownership, active-provider and RowVersion predicates retained. No new clinical token or permission relaxation. |
| Audit | Existing transactional before/after record includes new fact; finalization audit/snapshot records review. Audit failure rolls back clinical changes in retained automated evidence. |
| Finalization / correction | Existing linked Draft mechanism; both finalization paths enforce pickup review as applicable; new snapshots retain exact precision/zone/offset. Existing original artifact bytes retained. |
| Idempotency | **Raw script is not independently idempotent**: plain ADD column/constraint will fail on rerun. CREATE OR ALTER for function/procedures is not sufficient to rerun the entire script. |
| Runner guards | `TenantDatabaseMigrationRunner` checks applied IDs/hashes, acquires its migration lock, applies pending scripts in order, and commits each script's GO batches plus `SchemaMigration` row in one transaction. Matching applied migration is skipped. Partial schema without a matching ledger requires investigation, not a rerun by assumption. |
| Rollback | Before commit, runner rollback covers that migration transaction. After acceptance writes, preserve data/audit/artifacts; prefer a forward correction. Do not drop the pickup column or rewrite migration history to undo it. |

Tracking source: `db/tenant-clinical/migrations/0000-tenant-metadata.sql`, `SchemaMigration(MigrationId,SchemaVersion,ScriptHash,AppliedAt,AppliedBy)`; runner/hash policy under `src/MicroEMR.Infrastructure/Provisioning`. `CurrentSchemaVersion` alone does not prove migration currency because manifest entries share versions. The existing provisioning command applies **all pending** migrations and can change platform status; it was not run. Step 193 must recheck the pending set and authorize the exact operation, not blindly execute provisioning.

**Technical readiness CONDITIONAL:** the tested script and controlled runner path are established, but target application, recovery and runtime use remain unauthorized/unverified. No new schema/code fix is identified or performed here.

## 4. Existing fixtures and smallest executable scenario plan

Verified reusable synthetic patient **P**: `9f65d4ca-c347-406a-bb32-c709a8941f80`.

| Fixture | Exact identity / current baseline |
|---|---|
| Finalized source **R0** | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`, prescriber 2, RowVersion `0x0000000000002718` |
| Existing linked Draft **R1** | `ffd80110-e565-46de-b660-3d3f9dc7e463`, prescriber 2, Supersedes=R0, no artifact, RowVersion `0x0000000000002722` |
| Retained R0 artifact | `2a0416d4-546c-4de8-a83e-58c0424beff2` |
| R0 artifact JSON SHA-256 | `B17C55EED1138CD4741F71BF771F42F6E263F1ECE01BEFFE29256FDC7A05D302` |

These are inspection baselines, not tokens to reuse later. Re-read before action. No fixture was created or changed. Before 0093 there is no pickup value; after migration the expected legacy state is NULL, which must be verified.

**Common prerequisite gate for every scenario:** separately approved development migration/deployment/save scope; verified updated runtime/schema; current recovery plan; normal authenticated account/tenant above; current owned patient/order/token; no competing correction supersession. Navigate **https://localhost:7002 → patient P → Medications → existing prescription Edit/Details**. Do not start with new patient creation or arbitrary API calls.

Recommended execution order is **A → B → D → C**, avoiding a redundant known-value save solely to trigger correction review after clear. R1 is already a correction; no preliminary correction creation is needed. C later needs one new linked Draft from the approved D result. The complete future authorization would cover **five clinical actions**: A save, B save, D sign/finalize, C create correction, C clear/save. All are **DEFERRED now**.

| Scenario | Exact future UI actions / expected result | Evidence and disposition |
|---|---|---|
| **A — date-only** | Edit R1; choose Date only; date `2026-11-03`; Save Draft; reopen and follow Details. Display must retain `2026-11-03`, time not specified and recorded `America/Toronto`; no midnight/UTC conversion. | Q2/Q3 below: R1 remains Draft/owner 2, canonical Day JSON, updated token and one intended update audit. PASS only with normal UI/SQL correlation; FAIL on shift/loss/unrelated fact change; otherwise DEFERRED. |
| **B — minute precision** | Same R1: choose Date and time; `2026-11-03`, local `09:30`; visible clinical zone `America/Toronto`, sole offset `-05:00`; Save Draft, reopen/Details. Expected stored value `2026-11-03T09:30-05:00`, Precision Minute. | Q2/Q3: exact minute/zone/offset, Draft state and expected audit. This is an ordinary unambiguous time; no hidden seconds or host-zone shift. Desktop and one narrow viewport verify new labels, wrapping, keyboard/clear behavior across A/B/C; no repeat general mobile certification. |
| **D — existing linked correction review** | After B, R1 already links R0. In the prescription row, inspect current pickup text and explicit review checkbox beside Sign and finalize. Attempting ordinary UI submission without review must be prevented; select review and explicitly confirm signature once. | R1 becomes Finalized with retained pickup/review snapshot; R0 becomes Superseded with the existing replacement link. **R0 clinical facts/artifact stay unchanged; status/link/RowVersion changes are expected supersession behavior.** Q3/Q4 correlate finalization review and unchanged R0 artifact hash. Do not call the original token immutable across a legitimate status change. |
| **C — exact carry-forward then clear** | From the newly Finalized R1, normal Create Correction once produces **R2**, whose UID must be captured from the actual UI/response, never invented. Reopen R2 and verify exact Minute/zone/offset inheritance. Clear pickup, Save Draft, reopen/Details: Not recorded. Leave R2 as retained Draft; no additional sign needed. | Q2/Q3: R2 Supersedes=R1, current pickup NULL, audited original-known → NULL, unchanged R1 artifact/facts. Source-known R1 means review remains required if R2 is later finalized. Creation of R2 is a clinical action requiring explicit future scope; never execute as preliminary readiness setup. |

The source R0 and existing linked R1 can be reused without investigating provenance or adding permissions. If either is no longer eligible at execution, STOP and reselect a suitable existing synthetic fixture read-only; creating a replacement requires separate scope. Do not edit finalized values or clean up accepted records.

**DST manual decision:** do not add a dedicated DST save scenario. Step 191B already tests gaps, repeated-time explicit offset choice and frontend blank selection/error behavior. A/B plus one narrow viewport cover residual display usability. If clinicians identify a concrete ambiguous-offset usability concern, add a separately scoped observation later, not a duplicate safety/concurrency matrix now.

## 5. Verified SELECT-only checks

All existing-object references below were checked against actual development metadata/source. Equivalent pre-0093 identity/ledger/fixture/artifact SELECTs were executed. Pickup/review fields are absent locally; **post-0093 queries are verified against the tested migration definitions, not falsely claimed executed on this unmigrated tenant**. Use parameterized `@PatientUid`, `@PrescriptionUid`, and `@SourcePrescriptionUid` from the approved fixture/session. The new R2 UID must come from the actual future action.

**Q1 — identity and migration gate** (valid now):

```sql
SELECT DB_NAME() AS DatabaseName,
       CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
       TenantUid,TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,ScriptHash,AppliedAt FROM dbo.SchemaMigration
WHERE MigrationId IN(N'0092-encounter-procedures',N'0093-prescription-earliest-pickup');
SELECT COL_LENGTH(N'dbo.PatientPrescription',N'EarliestPickup') AS PickupColumnLength,
       OBJECT_ID(N'dbo.PrescriptionPickupValid',N'FN') AS PickupValidator;
```

**Q2 — current order** (only after verified 0093):

```sql
SELECT PrescriptionUid,PatientUid,Status,PrescriberUserId,EarliestPickup,
       SupersedesPrescriptionUid,SupersededByPrescriptionUid,ArtifactUid,
       CONVERT(varchar(18),CONVERT(varbinary(8),RowVersion),1) AS RowVersionHex
FROM dbo.PatientPrescription
WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid;
```

**Q3 — patient/order audit correlation** (schema valid now; evaluate future boundary):

```sql
SELECT a.AuditLogId,a.UserId,a.ActionName,a.CreatedAt,a.OldValue,a.NewValue
FROM dbo.AuditLog a
JOIN dbo.PatientPrescription p ON a.EntityId=CONVERT(nvarchar(100),p.PrescriptionUid)
JOIN dbo.Patient patient ON patient.PatientUid=p.PatientUid AND patient.PatientId=a.PatientId
WHERE p.PatientUid=@PatientUid AND p.PrescriptionUid=@PrescriptionUid
  AND a.EntityName=N'PatientPrescription'
ORDER BY a.AuditLogId;
```

Record a fresh audit boundary before authorized execution; normal chart-read audit volume is not additional clinical-save count. Inspect pickup and `EarliestPickupReviewed` in intended action payloads, preserving other fields.

**Q4 — retained source artifact fingerprint / prospective review**:

```sql
SELECT ArtifactUid,PrescriptionUid,
       CONVERT(varchar(64),HASHBYTES('SHA2_256',
           CONVERT(varbinary(max),ArtifactJson)),2) AS ArtifactJsonSha256,
       JSON_QUERY(ArtifactJson,'$.EarliestPickup') AS FrozenPickup,
       JSON_VALUE(ArtifactJson,'$.EarliestPickupReviewed') AS PickupReviewed
FROM dbo.PatientPrescriptionArtifact
WHERE PatientUid=@PatientUid AND PrescriptionUid=@SourcePrescriptionUid;
```

For R0, fingerprint must match baseline; legacy pickup/review values can be absent. For newly finalized R1, capture its own new hash/review data before creating/clearing R2. No direct SQL mutation, replay, deliberate stale save, role change or manual API test is needed.

## 6. Authorization checklist and remaining blockers

| Prerequisite before any migration/save | Current evidence / required next decision |
|---|---|
| Exact server/database/tenant | **IDENTIFIED:** endpoint/server/catalog/UID above; recheck identity immediately before action. Only this clinical database is the proposed migration target; Auth/Platform are not migration targets. |
| Development data scope | **ESTABLISHED:** verified development assignment plus standing synthetic-data clarification. This is an existing development tenant, not a disposable test database; prior disposable-test approval does not authorize its migration. |
| Recovery/backup method | SQL history reports latest full **copy-only** backup finished **2026-10-22T22:57:49**, SQL-server wall time (timezone not asserted). History alone does not prove file readability or successful restoration and predates subsequent work. **BLOCKED:** designate/authorize a current backup or proven restoration method and custodian before migration/save; none created here. |
| Exact migration asset/expected changes | **IDENTIFIED:** 0093 path/hash and expected function/column/check/procedure changes above. No pending schema change beyond 0093 was observed. Recheck full pending set/hash immediately before authorized execution. |
| Current migration state | **VERIFIED PENDING:** ledger/schema agree that 0093 is absent. **BLOCKED for live pickup writes** until separately authorized application succeeds and ledger/schema/hash are verified. Never rerun if already applied. |
| Runtime version and launch | Configured https profile set identified. **BLOCKED:** currently loaded Web/API version and actual active bindings have not been attested as Step 191B. Plan any necessary deployment/restart separately; do not treat source/test build output as a deployed runtime. |
| Exact synthetic fixture and actor | **IDENTIFIED:** P/R0/R1, actor 2/provider and permissions above. Revalidate live session/current tokens. **BLOCKED for action:** no clinical-save/signature/R2-creation authorization in Step 192. |
| Audit impact | **DEFINED:** five intended future clinical actions in A/B/D/C; create/update/clear/finalization/supersession events and ordinary read audits as applicable. Frozen review evidence under D. No permission/audit bypass. |
| Rollback/recovery approach | **CONDITIONAL/BLOCKED for execution:** use controlled per-migration transaction before commit; after clinical writes preserve all history and fix forward. Never drop clinical pickup storage, reset the tenant or overwrite artifacts as cleanup. Any full restore requires explicit scope and reconciliation of later retained records. |

Retained automated tenant, patient, permission, actor, RowVersion, audit-failure and finalized-immutability coverage is **sufficient for preparing this bounded manual plan**. It is not deployed acceptance; no manual duplication of hostile/stale cases is proposed. Remaining gaps are actual UI usability, current runtime/schema correlation and the execution/recovery authorizations, not a reason to redo successful isolated security suites.

## 7. Recommended Step 193 and disposition

**Step 193: controlled local development migration/runtime activation and minimal acceptance**, only after the blocked checklist items are explicitly resolved. Recheck identity, hashes/pending set and fixtures; establish current recoverability; authorize the exact migration/runtime action and five-save sequence; then use normal UI A → B → D → C with SELECT-only correlation. If approval/readiness is incomplete, stop at the specific missing item without migration, save or substituted fixture.

Only this report was added outside ignored read-only-helper receipts. No source edit, migration application, clinical write, live fixture creation, deployment/restart, login, passing build/test rerun, feature branch, commit/push/merge/reset/clean/stash, GitHub operation or external correspondence occurred. No previous requirement status changes: **PC07.01 PARTIAL; PC04.01/.14 unresolved/deferred; inventory 7 IDs / 3 packages.** Stop after readiness documentation.
