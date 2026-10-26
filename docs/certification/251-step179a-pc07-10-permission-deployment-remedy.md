# Step 179A — PC07.10 SQL permission and deployment remedy plan

2026-10-24 (America/Toronto). **Assessment/documentation only; no permission or deployment change executed.** Five SQL objects require additional access for the configured runtime **microemr_dev** in **MicroEMR_LocalDev_Fresh**. Both new API endpoints and matching served JavaScript are present. Running module identity is recorded, but exact correspondence to the accepted build/current commit remains **UNVERIFIED**. No clinical acceptance save occurred.

## 1. Reused evidence and safety

Retain [Step 176](248-step176-pc07-10-procedure-foundation-implementation.md) **142 distinct Procedure .NET PASS**, [Step 177](249-step177-pc07-10-encounter-medication-implementation.md) **56 distinct Medication .NET PASS**, frontend/Release and isolated prescription separation/security/audit/concurrency/signing-race evidence. Retain [Step 178](250-step178-pc07-10-deployment-readiness.md) migration **0092 applied with matching hash**, 93/93 ledger/manifest, synthetic fixtures, clinical actor/permissions and zero acceptance saves. None of those suites, migration checks or fixture discoveries was repeated.

Current **main**, HEAD `e25f56cfcb0ba438da7602e0e70d2e48ed2f3c8b`, retained. Existing untracked Step 178 report preserved. Targeted SELECT metadata, public HTTP metadata/assets and local process/module reads only. No login, clinical request, test/build, source/configuration edit, database modification, service restart, branch/commit or remote/GitHub operation.

## 2. Exact principal, objects and calling operations

The **actual API tenant connection** returned `USER_NAME() = microemr_dev`, principal ID **5**, `DB_NAME() = MicroEMR_LocalDev_Fresh`. Operator metadata independently identifies the same principal as **SQL_USER / INSTANCE**. This is a database user identity, not a credential export. Database identity remains tenant **local-dev-fresh**, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`, endpoint **192.168.50.1,50013**.

All rows below concern database **MicroEMR_LocalDev_Fresh**, schema **dbo**, grantee **microemr_dev**. Effective results reuse Step 178 runtime-connection evidence; newly inspected principal/role/schema metadata confirms no conflicting DENY for these targets and no existing object-level grants. Existing Encounter column grants are preserved.

| Object | SQL type | Additional permission | Existing effective access | Calling operation / reason |
|---|---|---|---|---|
| PatientEncounterProcedure_Save | SQL_STORED_PROCEDURE | EXECUTE | **0** | EncounterProcedureService.SaveAsync → EncounterProcedureRepository.SaveAsync; enter atomic Procedure create/update/context/audit/provenance operation |
| PatientEncounterHistory_Create | SQL_STORED_PROCEDURE | EXECUTE | **0** | EncounterMedicationService.SaveAsync → EncounterMedicationRepository.SaveAsync; direct client command records CppMedicationCreate/Update/Discontinue provenance in the same transaction |
| PatientEncounter | USER_TABLE | SELECT | Object **0**; EncounterUid/EncounterStatus allowed, **PatientUid/RowVersion missing**; exact read/lock shape SQL 230 | Medication SaveAsync directly locks owned Open encounter and compares reviewed context token |
| PatientMedication | USER_TABLE | SELECT | Object **0**; required PatientUid/MedicationUid/MedicationStatus/RowVersion **all 0**; SQL 229 | Medication SaveAsync directly locks selected owned Active source and checks source version for update/discontinue |
| PatientEncounterHistory | USER_TABLE | SELECT | Object **0**; required PatientUid/EncounterUid/CreatedBy/ActionType/ActionDescription **all 0**; SQL 229 | Medication SaveAsync directly reads patient/encounter/actor-scoped durable request receipts to prevent duplicate replay |

**Count: five objects / five proposed object-level grants: two EXECUTE + three SELECT.** At the column minimum, 11 required columns lack SELECT (2 + 4 + 5). The owner explicitly requests **object-level permissions only**, so the proposed SELECT statements necessarily cover every column on those **three specific tables**. This is broader than Step 178's column-only candidate. Approve this exact read-access scope deliberately; it is the smallest object set under that constraint, not the absolute smallest column privilege set. No UPDATE/INSERT/DELETE, GRANT OPTION, schema/database-wide SELECT/EXECUTE, role membership, db_owner or db_datareader is proposed.

Existing direct column grants on PatientEncounter are **EncounterUid, AppointmentUid, EncounterStatus**. Newly inspected relevant permissions show those three GRANT SELECT rows, runtime CONNECT and public metadata defaults, with no applicable role/schema DENY. Keep all those existing entries intact. Fresh preflight must stop if that baseline changes; never use REVOKE DENY or grant another principal to make this plan work.

### Direct access versus ownership chaining

Inspected deployed objects/procedures, tables and AuditLog have effective owner **dbo**; module execute-as is NULL. Step 176's wrapper calls current History Create/Update/Get and EncounterHistory_Create using static SQL. The same-owner chain supports those internal operations without separate caller grants on every underlying table or nested procedure. **Granting Procedure wrapper EXECUTE is sufficient for its new entry boundary**, subject to later normal acceptance; do not grant direct History/AuditLog modification rights.

Medication's **C# SqlTransaction does not create an ownership chain** for its separate ad hoc SELECT commands or its separately submitted EncounterHistory_Create call. Those need explicit runtime permissions. Its clinical modifications still call existing PatientMedication_Create/Update/Discontinue; those EXECUTE permissions were already verified. Existing Patient and active ApplicationUser validation SELECT shapes already pass through existing column grants. Do not add table-wide rights there. PatientClinicalHistory_Get EXECUTE=0 does not require a new grant: the current Procedure service lists through PatientClinicalHistory_List, and wrapper-internal Get uses the same-owner chain.

**Procedure-boundary classification: deployment/SQL-permission configuration gap, not a demonstrated functional defect.** Procedure orchestration is already handled by Step 176. Medication intentionally uses direct read validation plus stored-procedure-only changes, consistent with AGENTS.md's requirement for stored procedures for data modifications. No repository rule inspected requires all reads to be stored procedures; existing restricted column grants also show that runtime direct reads are used. No new wrapper, source change or migration is justified solely by these failures. If the owner requires a stricter procedure-only read policy instead of the explicit object-level read plan, stop and request separate implementation authorization; do not silently substitute broader grants or a new orchestration design.

## 3. Proposed grant batch — NOT EXECUTED

Run only after separate Step 179B authorization, on the verified development server, using the existing authorized operator. Record fresh actual-runtime effective checks from section 4 and a complete principal/role/schema/column permission snapshot first. Expected pre-change values for the five target permissions are **all 0**; if inherited access or DENY has changed, stop and revise rather than introduce redundant permissions. Preserve the original change receipt across repeat runs.

This batch has a strict five-target baseline, an already-applied no-op path and transactional failure handling. It changes permissions only, never clinical data. The resulting receipt must be saved in ignored local Step 179B evidence with the pre-change metadata; it is the authority for any later revocation.

```sql
USE [MicroEMR_LocalDev_Fresh];
SET XACT_ABORT ON;
IF DB_NAME() <> N'MicroEMR_LocalDev_Fresh'
 OR (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity) <> 1
 OR NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity
   WHERE TenantKey=N'local-dev-fresh'
     AND TenantUid='f94c6ecd-4417-483e-8a94-0507cf8384c2')
 THROW 53210, 'STOP: development identity mismatch.', 1;
DECLARE @Principal int=DATABASE_PRINCIPAL_ID(N'microemr_dev');
IF @Principal IS NULL OR NOT EXISTS
 (SELECT 1 FROM sys.database_principals WHERE principal_id=@Principal
  AND type_desc=N'SQL_USER' AND authentication_type_desc=N'INSTANCE')
 THROW 53211, 'STOP: runtime database principal mismatch.', 1;

DECLARE @Targets TABLE(ObjectName sysname,PermissionName sysname,ObjectType char(2));
INSERT @Targets VALUES
 (N'dbo.PatientEncounterProcedure_Save',N'EXECUTE','P'),
 (N'dbo.PatientEncounterHistory_Create',N'EXECUTE','P'),
 (N'dbo.PatientEncounter',N'SELECT','U'),
 (N'dbo.PatientMedication',N'SELECT','U'),
 (N'dbo.PatientEncounterHistory',N'SELECT','U');
IF EXISTS(SELECT 1 FROM @Targets WHERE OBJECT_ID(ObjectName,ObjectType) IS NULL)
 THROW 53212, 'STOP: expected SQL object/type missing.', 1;

DECLARE @Principals TABLE(Id int PRIMARY KEY);
;WITH roles AS (
 SELECT @Principal AS Id
 UNION ALL SELECT m.role_principal_id
 FROM sys.database_role_members m JOIN roles r ON m.member_principal_id=r.Id
)
INSERT @Principals SELECT Id FROM roles UNION SELECT DATABASE_PRINCIPAL_ID(N'public');
IF EXISTS(SELECT 1 FROM sys.database_permissions p
 WHERE p.grantee_principal_id IN(SELECT Id FROM @Principals) AND p.state='D'
 AND (p.class=0 OR (p.class=3 AND p.major_id=SCHEMA_ID(N'dbo'))
 OR (p.class=1 AND p.major_id IN(SELECT OBJECT_ID(ObjectName) FROM @Targets))))
 THROW 53213, 'STOP: applicable DENY requires separate review.', 1;

IF (SELECT COUNT(*) FROM @Targets t JOIN sys.database_permissions p
 ON p.class=1 AND p.major_id=OBJECT_ID(t.ObjectName) AND p.minor_id=0
 AND p.permission_name=t.PermissionName AND p.grantee_principal_id=@Principal
 AND p.state='G')=5
BEGIN
 SELECT N'Already present; no changes. Preserve original change receipt.' AS Result;
 RETURN;
END;
IF EXISTS(SELECT 1 FROM @Targets t JOIN sys.database_permissions p
 ON p.class=1 AND p.major_id=OBJECT_ID(t.ObjectName) AND p.minor_id=0
 AND p.permission_name=t.PermissionName AND p.grantee_principal_id=@Principal)
 THROW 53214, 'STOP: partial/changed permission baseline; replan.', 1;

BEGIN TRY
 BEGIN TRANSACTION;
 GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterProcedure_Save] TO [microemr_dev];
 GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterHistory_Create] TO [microemr_dev];
 GRANT SELECT ON OBJECT::[dbo].[PatientEncounter] TO [microemr_dev];
 GRANT SELECT ON OBJECT::[dbo].[PatientMedication] TO [microemr_dev];
 GRANT SELECT ON OBJECT::[dbo].[PatientEncounterHistory] TO [microemr_dev];
 COMMIT TRANSACTION;
 SELECT DB_NAME() AS DatabaseName,N'microemr_dev' AS Grantee,
   ObjectName,PermissionName,CAST(1 AS bit) AS IntroducedByThisBatch,
   SYSUTCDATETIME() AS RecordedUtc FROM @Targets;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
```

The batch intentionally stops on a partially changed baseline; idempotence does not mean overwriting another administrator's changes. Do not continue if fresh runtime checks contradict operator metadata. A pre-commit SQL error rolls back the grant transaction; a later failed runtime verification uses the separately authorized, receipt-bound revocation below. No clinical save is needed to verify SQL capability.

## 4. Exact read-only verification

Run this **using the actual configured API tenant connection**, not the operator connection and not an operator's HAS_PERMS_BY_NAME result. It must resolve the expected database/principal; all five Allowed values must be 1 after the correction. No impersonation, login change or production connection is required.

```sql
SELECT DB_NAME() AS DatabaseName,USER_NAME() AS DatabasePrincipal;
SELECT v.ObjectName,v.PermissionName,
 HAS_PERMS_BY_NAME(v.ObjectName,N'OBJECT',v.PermissionName) AS Allowed
FROM (VALUES
 (N'dbo.PatientEncounterProcedure_Save',N'EXECUTE'),
 (N'dbo.PatientEncounterHistory_Create',N'EXECUTE'),
 (N'dbo.PatientEncounter',N'SELECT'),
 (N'dbo.PatientMedication',N'SELECT'),
 (N'dbo.PatientEncounterHistory',N'SELECT')) v(ObjectName,PermissionName);

-- Zero rows, including the actual validation/lock shapes; no clinical mutation.
SELECT TOP(0) PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid='9f65d4ca-c347-406a-bb32-c709a8941f80' AND IsDeleted=0;
SELECT TOP(0) EncounterStatus,RowVersion FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid='9f65d4ca-c347-406a-bb32-c709a8941f80'
 AND EncounterUid='338e7e85-0972-4345-94f9-01339a5bcd91';
SELECT TOP(0) MedicationStatus,RowVersion FROM dbo.PatientMedication WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid='9f65d4ca-c347-406a-bb32-c709a8941f80'
 AND MedicationUid='2521d27e-172f-47a1-9326-72eea8b0f571';
SELECT TOP(0) ActionType,ActionDescription FROM dbo.PatientEncounterHistory
 WHERE PatientUid='9f65d4ca-c347-406a-bb32-c709a8941f80'
 AND EncounterUid='338e7e85-0972-4345-94f9-01339a5bcd91'
 AND CreatedBy=2 AND ActionType IN(N'CppMedicationCreate',N'CppMedicationUpdate',N'CppMedicationDiscontinue')
 AND ActionDescription LIKE N'00000000000000000000000000000000|%';
SELECT TOP(0) UserId FROM dbo.ApplicationUser WHERE UserId=2 AND IsActive=1;
```

Using the operator, compare full permission snapshots before/after. Only the five minor_id=0 object grants may be new; the three original Encounter column grants, other principals, memberships, all DENYs and existing domain procedure grants remain unchanged. Verify no new direct DML access or GRANT OPTION, no migration/clinical/audit change attributable to this metadata-only step. Query for targeted grants/columns:

```sql
SELECT OBJECT_SCHEMA_NAME(p.major_id) AS ObjectSchema,OBJECT_NAME(p.major_id) AS ObjectName,
 p.permission_name,p.state_desc,p.minor_id,COL_NAME(p.major_id,p.minor_id) AS ColumnName,
 USER_NAME(p.grantor_principal_id) AS Grantor
FROM sys.database_permissions p
WHERE p.grantee_principal_id=DATABASE_PRINCIPAL_ID(N'microemr_dev') AND p.class=1
 AND p.major_id IN(OBJECT_ID(N'dbo.PatientEncounterProcedure_Save'),
 OBJECT_ID(N'dbo.PatientEncounterHistory_Create'),OBJECT_ID(N'dbo.PatientEncounter'),
 OBJECT_ID(N'dbo.PatientMedication'),OBJECT_ID(N'dbo.PatientEncounterHistory'))
ORDER BY ObjectName,p.minor_id,p.permission_name;
```

## 5. Exact rollback/revocation — NOT EXECUTED

Only for a separately authorized rollback of **this successful five-grant batch**, with its saved receipt showing all five IntroducedByThisBatch=1 and pre-change absence. A no-op run is not a new change receipt. If any target permission, grantor, column permission, role or schema policy changed independently since application, **STOP** and reconcile; never revoke a permission merely because it currently exists. Do not use CASCADE or revoke any pre-existing column grant.

After verifying the original identity guard from section 3, execute this batch using the same authorized operator. Compare post-revocation metadata to the original saved snapshot; all five object permissions should return to their original absent state. The three pre-existing Encounter column SELECT grants must survive. The guard rolls back the whole revocation if they do not.

```sql
USE [MicroEMR_LocalDev_Fresh];
SET XACT_ABORT ON;
IF DB_NAME() <> N'MicroEMR_LocalDev_Fresh'
 OR (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity) <> 1
 OR NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity
   WHERE TenantKey=N'local-dev-fresh'
     AND TenantUid='f94c6ecd-4417-483e-8a94-0507cf8384c2')
 THROW 53210, 'STOP: development identity mismatch.', 1;
IF DATABASE_PRINCIPAL_ID(N'microemr_dev') IS NULL
 THROW 53211, 'STOP: runtime database principal missing.', 1;
-- Mandatory preceding gates: original change receipt;
-- fresh metadata equals recorded post-grant state (no intervening permission edits).
BEGIN TRY
 BEGIN TRANSACTION;
 REVOKE EXECUTE ON OBJECT::[dbo].[PatientEncounterProcedure_Save] FROM [microemr_dev];
 REVOKE EXECUTE ON OBJECT::[dbo].[PatientEncounterHistory_Create] FROM [microemr_dev];
 REVOKE SELECT ON OBJECT::[dbo].[PatientEncounter] FROM [microemr_dev];
 REVOKE SELECT ON OBJECT::[dbo].[PatientMedication] FROM [microemr_dev];
 REVOKE SELECT ON OBJECT::[dbo].[PatientEncounterHistory] FROM [microemr_dev];
 IF (SELECT COUNT(*) FROM sys.database_permissions p
   WHERE p.grantee_principal_id=DATABASE_PRINCIPAL_ID(N'microemr_dev')
     AND p.class=1 AND p.major_id=OBJECT_ID(N'dbo.PatientEncounter')
     AND p.permission_name=N'SELECT' AND p.state='G'
     AND COL_NAME(p.major_id,p.minor_id) IN(N'EncounterUid',N'AppointmentUid',N'EncounterStatus'))<>3
   THROW 53215, 'STOP: original column permissions not preserved; rollback revocation.', 1;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
```

No DENY is introduced as rollback. Revocation changes access only: no clinical row, audit, migration ledger or source record is deleted/restored. If any permission check fails after the grant batch, stop without clinical requests; apply this rollback only within the separately approved rollback scope. Preserve the failure evidence and receipt.

## 6. Running API/Web/assets identity

Non-mutating OS listener/module inspection and normal-TLS public HTTP retrieval established:

| Component | Observation |
|---|---|
| API | PID **19628**, listening on loopback **7003**, started **2026-10-24 22:27:49 UTC**, loaded from `src/MicroEMR.Api/bin/Debug/net10.0/MicroEMR.Api.dll` |
| Web | PID **18392**, listening on loopback **7002**, started **2026-10-24 22:27:59 UTC**, loaded from `src/MicroEMR.Web/bin/Debug/net10.0/MicroEMR.Web.dll` |
| Module version | Both main assemblies and their loaded Application/Infrastructure copies report **1.0.0+68392e4d3c6d686257357685c4beb15dd5567431**. Recorded files predate those process starts; API and Web Application/Infrastructure copies match each other |
| Endpoint registration | Live `https://localhost:7003/swagger/v1/swagger.json` **200**, advertises **GET/POST** for both `/api/patients/{patientUid}/encounters/{encounterUid}/procedures` and `/medications` |
| Controller deployment | Both API and Web on-disk main assemblies at loaded module paths contain EncounterProceduresController and EncounterMedicationsController metadata names |
| Static assets | Three public Web GETs **200**; Procedure JS, Medication JS and imported history-clinical-fields JS each byte-identical to repository generated assets |

Recorded SHA-256:

| File | SHA-256 |
|---|---|
| API DLL | AE1EB54765981B3F0B14782558B53F177F5282713397588947336D9FB87DE394 |
| Web DLL | C951F4C87AC2D644806D403C62FB9E71ABF51513F959F705AB8A0790D60BC255 |
| Application DLL, API/Web copies | F39659A53466DE883AB7731B3005AFC91B74036298E9EDCB8677459154A17CB2 |
| Infrastructure DLL, API/Web copies | 6072CDEB5B867E483209A8A3AD848ECB3D8397ED635B054AF4F6702B519485E9 |
| Served procedures.js | 34F0BB82A639446E5FD6A490B825BF5053F42DBBBAC36DECE8DEFCD62829D6E2 |
| Served medications.js | 1F365F7438179CBC8419610317CA830E5A81847C2AE24B144B1DDA691217A1D9 |
| Served history-clinical-fields.js | 48DC6B179A7E7124A1E244510EC6797C11DAAFF90E5C6B1FE66B8620AE83A796 |

**Boundary:** the embedded commit is **68392e4**, while current HEAD is **e25f56c**. Steps 176–177 were developed uncommitted on the earlier tip, so that older embedded commit does **not** prove stale feature code. The current modules also differ in byte hashes from retained Step 177 Debug/Release test output; differing build paths/configurations can produce different binaries. Neither difference alone proves an application defect. Exact source/build-input equivalence to accepted output is therefore **UNVERIFIED**, despite confirmed live endpoints and matching served modules. Loaded-path/file metadata is not a claim of an in-memory assembly digest or authenticated Razor rendering.

Launch profiles configure **Development** with those API/Web HTTPS ports. Actual process environment-name value was not read; avoid exposing whole process environments/secrets. Minimum remaining identity evidence is an existing startup log/console environment line and build/deployment record attesting the observed module hashes to the approved Step 177 source. No diagnostic environment dump, clinical login or product version endpoint is necessary. Readiness already verified the configured API tenant binding/catalog/database identity; authenticated per-request tenant routing remains part of later acceptance, not this public metadata check.

**Restart/redeployment is not yet shown to be necessary.** SQL permission changes alone do not require an API/Web restart. If existing build/launch evidence resolves identity, retain these processes. If it cannot, request a controlled replacement from the approved matching existing artifact/source with recorded hashes/environment and API/Web assets together; authorize that separately in Step 179B. Do not rebuild or restart merely because the embedded commit precedes the owner's subsequent commit. Do not apply 0092 again.

## 7. Separately authorizable Step 179B

1. Refresh only identity, runtime five-permission results and principal/role/column baseline needed for the proposed change. Stop on wrong server/database/tenant/principal, DENY, object/type mismatch or changed/partial permission baseline. No fixture rediscovery or completed test rerun.
2. Obtain owner approval for the **five object-level grants**, including the three full-table SELECT scopes, and receipt-bound rollback if capability checks fail. Use the existing operator; preserve all pre-existing permissions/principals. Apply section 3 only under that authorization.
3. Run section 4 via the runtime binding: five Allowed=1 and all five zero-row query shapes succeed, with exactly the intended metadata delta. No stored-procedure mutation probe or clinical login/save.
4. Resolve remaining environment/source identity from existing build/launch evidence. **If unresolved, STOP before claiming deployment verified**; obtain explicit authority for an identified controlled API/Web replacement/restart and record artifact hashes/startup evidence. Do not treat blanket permission approval as deployment approval.
5. Confirm the two live OpenAPI GET/POST paths and three served JS hashes, ports/module identity and development routing after any approved replacement. Do not submit Web POSTs or claim authenticated Razor/actor/CPP acceptance.
6. Report PASS for permission remedy only when exact effective grants/query shapes/unchanged metadata are verified; PASS for deployment identity only when source/build and environment mapping are supported. FAIL/STOP on unexpected privilege expansion, wrong routing, SQL error, mismatched assets or undeclared restart need. Unresolved build provenance remains **UNVERIFIED**, not PASS. No clinical writes are expected or authorized in Step 179B.

Only after Step 179B succeeds should a later separately authorized step use the **one-login/five-save** plan in Step 178. Retain its existing P/Q, Open SOAP/structured, Signed, medication, preserved Medical history and finalized prescription/artifact resources. Refresh their tokens/audit boundaries then; do not create fixtures now. Positive saves, clinician review and deferred security/release applicability remain outstanding. No additional Surgical fixture is required for the minimum accepted-evidence route.

## 8. Effort, disposition and artifacts

Remaining Step 179B estimate: **0.5–1 h** approved permission application/capability/rollback review, **0.5–1 h** existing build/launch identity reconciliation and report; **1–2 h total** assuming available operator/owner evidence. If matching deployment replacement is actually required, add **0.5–1.5 h** under explicit authorization. No implementation work is currently demonstrated necessary. Later separately authorized clinical acceptance/evidence: **2.5–5 h**; owner/credential/clinical-review availability can extend elapsed time. A newly imposed procedure-only read policy needs separate design/estimate, not automatic implementation.

**PC07.10 PARTIAL — IMPLEMENTED, deployed clinical acceptance pending; PC07.01 PARTIAL; inventory unchanged at 8 mandatory IDs / three packages.** No certification closure from permission planning/public endpoint metadata.

Only this report added outside ignored `.tmp/step179a/`: SELECT-only `permissions.ps1/json`, read-only `binaries.ps1/json`, public `swagger.json` and downloaded JS files. An initial restricted-shell process query was denied; authorized read-only inspection succeeded outside the sandbox. One metadata command had a parse error before execution and was corrected with the small helper; no service/SQL change resulted. No credential/cookie/token output. No tests/builds, clinical login/access through authenticated endpoints, fixture creation, clinical write, GRANT/DENY/REVOKE execution, migration, deployment, service restart, code edit, branch/commit or remote operation. Stop after documentation.
