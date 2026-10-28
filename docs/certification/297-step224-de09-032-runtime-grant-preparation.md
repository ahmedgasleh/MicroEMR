# Step 224 — Prepare the three narrow runtime EXECUTE grants

2026-10-28 (America/Toronto). **PREPARATION COMPLETE — READY FOR OWNER-AUTHORIZED STEP 225, subject to an unchanged-state preflight. NO GRANTS EXECUTED.** The verified target and effective permission state match Step 222. Recovery classification: **OWNER-APPROVED LOCAL RECOVERY POINT — BACKUP VERIFIED; RESTORE UNTESTED.** Directory activation/clinical acceptance is not declared complete.

## 1. Branch and worktree preservation

Read AGENTS.md and the supplied Step 224 instruction. Actual branch is **`main`**. The three pre-existing untracked reports 294–296 were identified and preserved byte-for-byte using SHA-256 comparison. Remained on main. This report did not exist before creation.

Created only this report. No helper/receipt file, application/SQL/migration/configuration edit, Git mutation, build, test, backup operation, verification/restore, service operation, grant/revoke, clinical mutation or mutating API call occurred. SQL inspection used SELECT-only metadata and effective-permission checks on the verified development target. Credentials/connection strings were neither printed nor included here. Production databases were not accessed.

## 2. Step 223 owner recovery approval

The owner decision supplied with Step 224 explicitly accepts the October 28 Step 222 full backup as the local-dev-fresh activation recovery point, approves its documented proposed restoration runbook, and acknowledges that no restore rehearsal has been performed. This is **owner-provided approval**, not an independently performed restore or a new verification operation.

The approved backup identity, reused from [report 296](296-step222-de09-032-current-backup-verification.md), is:

- Backup set **45**, position **1**, UUID **`028F4272-0FAA-4ECB-A2C9-F7F6352683D4`**.
- Full, non-copy-only, checksum present; damage flag false; CHECKSUM/VERIFYONLY **PASS**, metadata **MATCH**.
- Client-observed completion **2026-10-28 12:10:25.9203909 EDT** (`16:10:25.9203909 UTC`); SQL-recorded finish `2026-10-28T16:10:26`.
- Exact SQL-server-side file:

```text
C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_step222_20261028T160740Z_c4d43cffea5c499cb4778f83dd0898c6.bak
```

The approval resolves the earlier point/runbook decision gate for this bounded local plan. Retain **restore untested**, same-host/volume durability and SQL/external-file limitations. It does not authorize an actual restore, backup repetition, grant execution, broad security change or clinical acceptance writes. No backup or VERIFYONLY was repeated.

## 3. Fresh target and migration verification

SELECT-only metadata independently reverified:

| Check | Observed result |
|---|---|
| Server | **`EC2AMAZ-TC26CQK\TELEMED52`** |
| Database | **`MicroEMR_LocalDev_Fresh`** |
| Tenant key | **`local-dev-fresh`** |
| Tenant UID / identity cardinality | **`f94c6ecd-4417-483e-8a94-0507cf8384c2`**, exactly one row |
| State | **ONLINE** |
| 0098 | Applied, stored AppliedAt `2026-10-28T14:22:08.2936851` |
| 0099 | Applied, stored AppliedAt `2026-10-28T15:29:10.6446800` |

The recorded hashes are unchanged from approved evidence:

```text
0098: 44B5AA6E2E9A2FC9732F5AEBE03869211911D843EEE32481DCD33ADAFFE42D85
0099: F99B770588FFDA3BF31321C5CF788D6E41DB61B37CB82BA01BA3DC9435543574
```

**Do not rerun either migration.** The requested target/permission stop condition was not triggered: the fresh observations match Step 222's applied-migration/missing-grant state.

## 4. Exact procedure and principal identities

| Procedure | Current object_id | Type / effective owner | Purpose / direct runtime caller |
|---|---:|---|---|
| `[dbo].[DispensingLocation_Get]` | **333244242** | SQL stored procedure / dbo, principal ID 1 | UID/exact identity details; Infrastructure directory repository via API |
| `[dbo].[DispensingLocation_Search]` | **349244299** | SQL stored procedure / dbo, principal ID 1 | Bounded directory search; repository via API |
| `[dbo].[DispensingLocation_Save]` | **365244356** | SQL stored procedure / dbo, principal ID 1 | Audited maintenance; repository via API |

The database principal **`microemr_dev` exists**, principal ID **5**, type **SQL_USER**, authentication **INSTANCE**. A separate connection using the actual configured API runtime credential confirmed `USER_NAME() = microemr_dev`, `USER_ID() = 5`, and the intended database. It is not dbo and is not a sysadmin. Web calls the API and requires no separate direct SQL grant.

## 5. Effective permission, roles and DENY precheck

| Runtime check | Current result |
|---|---:|
| Database-wide EXECUTE | **0** |
| dbo schema-wide EXECUTE | **0** |
| Get effective EXECUTE | **0** |
| Search effective EXECUTE | **0** |
| Save effective EXECUTE | **0** |
| Effective database roles | **public only** |
| Runtime sysadmin membership | **0** |

Operator metadata checked the runtime user, recursively applicable roles and public at database, dbo-schema and the three object scopes for EXECUTE/CONTROL/ALTER/TAKE OWNERSHIP. **No applicable rows were returned**, including no applicable explicit DENY or pre-existing broad grant affecting this operation. Effective permissions were checked independently; absence of a direct GRANT was not used as a substitute for that check.

The existing direct object-grant pattern and Stage 2 call graph from [report 294](294-step220-de09-032-activation-permission-plan.md) are retained. Only these three new direct procedure permissions are needed. Existing prescription calls use already-granted procedures; static same-owner procedure/table/function calls retain ownership-chain behavior. No additional table DML/SELECT, function, schema, database, role or ownership permission is proposed.

## 6. Exact proposed grants — NOT EXECUTED

The only proposed security changes are:

```sql
GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Get]
TO [microemr_dev];

GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Search]
TO [microemr_dev];

GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Save]
TO [microemr_dev];
```

No WITH GRANT OPTION, schema/database EXECUTE, ALTER, CONTROL, ownership change, role membership change or other-principal modification is authorized. This document is preparation, not authorization to run the statements.

## 7. Step 225 execution safeguards and transaction core

The separately authorized operator must first recheck the target, owner approval and backup identity, migration hashes, the principal/object IDs above, and the effective/explicit permission state. Reconfirm the approved backup's msdb UUID/set/position/path/type/checksum metadata and current file existence using reads; **do not run another backup, VERIFYONLY, HEADERONLY or restore under a grant-only authorization**.

Require the current state to remain exactly the reviewed missing-grant state. If any effective permission is already granted, an applicable DENY/role/broad permission appears, a procedure/principal has been recreated, or a target/hash differs, **STOP and report before any grant**. Do not treat a changed state as permission to broaden/revoke/recreate anything. Use a controlled operator window without concurrent security/schema changes; application services need not be restarted.

Capture read-only pre-execution evidence: server/database/tenant, migrations, approved backup identity, full database permission rows, role membership, principals/owners, procedure definitions, object/schema/column metadata and current effective runtime permissions. Reuse existing protected connection configuration; record no credentials. The operator must already have sufficient metadata/GRANT/impersonation authority; do not grant extra operator privileges to make the plan run.

The following is an **unexecuted transaction core**, following those independently recorded preflight checks. It intentionally rejects stale principal/object identities and existing applicable permissions, uses one transaction for the three grants, checks both explicit and effective rights before commit, and restores impersonation on failure. It is not a replacement for the broader pre/post evidence comparison.

```sql
-- DOCUMENTATION ONLY: Step 225 requires separate explicit owner authorization.
-- Connect with the approved operator to the already verified development database.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@TRANCOUNT <> 0
    THROW 54400, 'Stop: use a session with no existing transaction.', 1;

DECLARE @AsRuntime bit = 0,
        @Get int, @Search int, @Save int, @DatabaseExecute int, @SchemaExecute int;

BEGIN TRY
    BEGIN TRANSACTION;

    IF ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), N'')
           <> N'EC2AMAZ-TC26CQK\TELEMED52'
       OR DB_NAME() <> N'MicroEMR_LocalDev_Fresh'
        THROW 54401, 'Stop: unexpected server or database.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.databases
                   WHERE database_id = DB_ID() AND state_desc = N'ONLINE')
       OR (SELECT COUNT_BIG(*) FROM dbo.TenantDatabaseIdentity) <> 1
       OR NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity
                      WHERE TenantUid = 'f94c6ecd-4417-483e-8a94-0507cf8384c2'
                        AND TenantKey = N'local-dev-fresh')
        THROW 54402, 'Stop: database state or tenant mismatch.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration
                   WHERE MigrationId = N'0098-prescription-dispensing-location-reference'
                     AND ScriptHash = '44B5AA6E2E9A2FC9732F5AEBE03869211911D843EEE32481DCD33ADAFFE42D85')
       OR NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration
                      WHERE MigrationId = N'0099-dispensing-location-directory'
                        AND ScriptHash = 'F99B770588FFDA3BF31321C5CF788D6E41DB61B37CB82BA01BA3DC9435543574')
        THROW 54403, 'Stop: migration prerequisites differ.', 1;

    IF ISNULL(DATABASE_PRINCIPAL_ID(N'microemr_dev'), -1) <> 5
       OR NOT EXISTS (SELECT 1 FROM sys.database_principals
                      WHERE principal_id = 5 AND type = 'S'
                        AND authentication_type_desc = N'INSTANCE')
       OR ISNULL(OBJECT_ID(N'dbo.DispensingLocation_Get', N'P'), -1) <> 333244242
       OR ISNULL(OBJECT_ID(N'dbo.DispensingLocation_Search', N'P'), -1) <> 349244299
       OR ISNULL(OBJECT_ID(N'dbo.DispensingLocation_Save', N'P'), -1) <> 365244356
        THROW 54404, 'Stop: principal or procedure identity changed.', 1;

    IF EXISTS (SELECT 1 FROM sys.database_role_members WHERE member_principal_id = 5)
       OR EXISTS (SELECT 1 FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
                  WHERE o.object_id IN (333244242,349244299,365244356)
                    AND COALESCE(o.principal_id,s.principal_id) <> 1)
        THROW 54405, 'Stop: role membership or ownership changed.', 1;

    IF EXISTS (
        SELECT 1 FROM sys.database_permissions p
        WHERE p.grantee_principal_id IN (5, DATABASE_PRINCIPAL_ID(N'public'))
          AND p.permission_name IN (N'EXECUTE',N'CONTROL',N'ALTER',N'TAKE OWNERSHIP')
          AND (p.class = 0 OR (p.class = 3 AND p.major_id = SCHEMA_ID(N'dbo'))
               OR (p.class = 1 AND p.major_id IN (333244242,349244299,365244356))))
        THROW 54406, 'Stop: unexpected applicable grant or deny.', 1;

    EXECUTE AS USER = N'microemr_dev';
    SET @AsRuntime = 1;
    SELECT @Get = HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Get',N'OBJECT',N'EXECUTE'),
           @Search = HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Search',N'OBJECT',N'EXECUTE'),
           @Save = HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Save',N'OBJECT',N'EXECUTE'),
           @DatabaseExecute = HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'EXECUTE'),
           @SchemaExecute = HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'EXECUTE');
    REVERT;
    SET @AsRuntime = 0;
    IF ISNULL(@Get,-1) <> 0 OR ISNULL(@Search,-1) <> 0 OR ISNULL(@Save,-1) <> 0
       OR ISNULL(@DatabaseExecute,-1) <> 0 OR ISNULL(@SchemaExecute,-1) <> 0
        THROW 54407, 'Stop: effective permission baseline changed.', 1;

    GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Get] TO [microemr_dev];
    GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Search] TO [microemr_dev];
    GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Save] TO [microemr_dev];

    IF (SELECT COUNT(*) FROM sys.database_permissions
        WHERE grantee_principal_id = 5 AND class = 1 AND minor_id = 0
          AND major_id IN (333244242,349244299,365244356)
          AND permission_name = N'EXECUTE' AND state = 'G') <> 3
        THROW 54408, 'Stop: expected object grants not established.', 1;

    EXECUTE AS USER = N'microemr_dev';
    SET @AsRuntime = 1;
    SELECT @Get = HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Get',N'OBJECT',N'EXECUTE'),
           @Search = HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Search',N'OBJECT',N'EXECUTE'),
           @Save = HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Save',N'OBJECT',N'EXECUTE'),
           @DatabaseExecute = HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'EXECUTE'),
           @SchemaExecute = HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'EXECUTE');
    REVERT;
    SET @AsRuntime = 0;
    IF ISNULL(@Get,0) <> 1 OR ISNULL(@Search,0) <> 1 OR ISNULL(@Save,0) <> 1
       OR ISNULL(@DatabaseExecute,-1) <> 0 OR ISNULL(@SchemaExecute,-1) <> 0
        THROW 54409, 'Stop: effective rights differ from the narrow plan.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @AsRuntime = 1
    BEGIN
        REVERT;
    END;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

No SQL above was submitted to the database. No stored procedure, especially Save, is executed by the permission checks. EXECUTE AS/REVERT in this future script changes only the session security context and must always return to the operator before commit/reporting; it grants no impersonation or other right. A failed precondition/verification rolls back this transaction's grants; it does not authorize an automatic retry or compensating REVOKE of unrelated permissions.

## 8. Exact Step 225 post-grant verification plan

1. Confirm successful commit/result; if the connection/result becomes ambiguous, inspect read-only state before any new action. Do not repeat grants automatically.
2. As the operator, inspect sys.database_permissions: expect **exactly three new rows** relative to the preserved baseline, all class OBJECT_OR_COLUMN, minor_id 0, permission EXECUTE, state **GRANT (not grant-with-grant-option)**, grantee microemr_dev, and the three verified object IDs. The expected grantor is the authorized operator's SQL grantor identity; record it without exposing credentials. No prior row should disappear/change.
3. On the real configured runtime connection, reverify exact database/tenant/USER_NAME/USER_ID, then SELECT HAS_PERMS_BY_NAME for the three names: expect **1/1/1**. Database/schema-wide EXECUTE must remain **0/0**, sysadmin **0**, and effective roles **public only**. This real-connection check complements the transaction's database-user impersonation checks.
4. Compare complete before/after database permission, role membership, principal/owner and object/schema/column-definition snapshots. No change is permitted except the three expected grants. Migration ledger/hashes, procedure definitions/object identities and unrelated grants must remain unchanged. Any discrepancy is a stop/report condition, not permission to broaden or repair automatically.
5. Confirm the approved recovery metadata is still the referenced point; do not perform a backup/restore operation. Preserve evidence of the transaction, checks and owner authorization.
6. Stop. Do not create/edit/deactivate/reactivate directory records, invoke Save, edit/finalize prescriptions, run mutating API calls, apply migrations or restart Web/API/Auth. No authenticated workflow acceptance is included in this grant-only step.

For a precise real-runtime check, read-only verification may use:

```sql
SELECT DB_NAME() AS DatabaseName, USER_NAME() AS RuntimePrincipal, USER_ID() AS PrincipalId,
       HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Get',N'OBJECT',N'EXECUTE') AS GetExecute,
       HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Search',N'OBJECT',N'EXECUTE') AS SearchExecute,
       HAS_PERMS_BY_NAME(N'dbo.DispensingLocation_Save',N'OBJECT',N'EXECUTE') AS SaveExecute,
       HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'EXECUTE') AS DatabaseExecute,
       HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'EXECUTE') AS SchemaExecute;
```

Run it through the actual configured runtime connection, not through the operator connection while assuming operator access represents runtime access. No directory procedure call is necessary to verify the Save permission, and no live clinical save is justified by this check.

## 9. Execution readiness and exact authorization still required

**Ready for a separately authorized bounded Step 225**, contingent on the same-state execution preflight. Recovery point/runbook approval is already supplied; restore remains untested by explicit owner acknowledgment. The remaining immediate gate is **explicit authorization to execute the three grants and read-only verification**. It is not an unresolved request to approve recovery again, and it is not automatic directory activation acceptance.

Suggested exact owner authorization:

> I authorize Step 225 only on EC2AMAZ-TC26CQK\TELEMED52, database MicroEMR_LocalDev_Fresh, tenant local-dev-fresh / f94c6ecd-4417-483e-8a94-0507cf8384c2. Reconfirm the owner-approved Step 222 backup set 45, position 1, the migrations and the reviewed security state; then execute only GRANT EXECUTE on dbo.DispensingLocation_Get, dbo.DispensingLocation_Search and dbo.DispensingLocation_Save to database principal microemr_dev, using the guarded transaction and read-only verification plan in report 297. Stop on any drift, DENY or unexpected permission. No schema/database-wide grants, role/ownership/other-principal changes, migrations, backup/restore operations, service restarts, clinical/directory mutations or mutating API calls are authorized. Authenticated workflow acceptance remains separate.

That authorization has **not** been supplied by this preparation request. No GRANT was executed. No request for a new backup, verification or restore is part of the plan.

## 10. Certification boundary and recommended Step 225

Retain **OWNER-APPROVED LOCAL RECOVERY POINT — BACKUP VERIFIED; RESTORE UNTESTED**. Historical reports keep their original chronology/qualifications; they were not rewritten to imply earlier owner approval or a restore rehearsal.

Preserve **DE09.032 PARTIAL; PC07.01 PARTIAL; mandatory inventory 7 IDs / 3 packages**, prior DE09.034/DE09.035 qualified closures, Stage 1/2 automated evidence, authenticated UI qualification, earlier NU1900 qualification and Auth restart restrictions. No certification analysis or test suite was repeated and no directory activation-complete claim is made.

**Recommended Step 225:** obtain the exact bounded execution authorization, revalidate the target/recovery/security preconditions, execute only the three object grants in one transaction, verify explicit and effective permission deltas through the intended runtime principal, and stop without clinical/workflow writes or service changes. Authenticated workflow acceptance must be a later separately authorized step.
