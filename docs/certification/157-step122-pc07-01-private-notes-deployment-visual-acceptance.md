# Step 122 - PC07.01 DE09.019 controlled deployment and visual acceptance

Date: 2026-10-20 (America/Toronto). **PC07.01 PARTIAL. Inventory: 10 mandatory IDs / three packages. DE09.019 implemented; deployment and visual evidence pending.**

## 1. Actual checkpoint and scope

- Branch: `main`.
- HEAD: `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`.
- Entry `git status --short`: empty (clean). This differs from the previously reported feature branch/HEAD; no branch switch or history change was performed.
- Only this report is added. No application/SQL source changes, database connections, database writes, fixture creation, staging, commit, merge or remote operation. No build or completed test suite repeated.
- Inputs: [Step 121](156-step121-pc07-01-private-prescription-notes-implementation.md), the two deployment scripts, manifest, migration runner/hash policy, existing permission/identity schema and relevant Web navigation. Prior policies were reused without reanalysis.

## 2. Script inventory and readiness

| Asset | Database target | Changes and dependencies | Readiness |
| --- | --- | --- | --- |
| `db/tenant-clinical/migrations/0085-private-prescription-notes.sql` | Verified selected tenant clinical database, not Platform/Auth | Parent unique pair; two protected tables; retention triggers; public table DML denials; denied direct helper execution; three private-note procedures; replacement CreateDraft/UpdateDraft definitions | Static inspection PASS; target execution BLOCKED |
| `db/platform/027_private_prescription_note_permission.sql` | Configured Platform database; repository creation default `MicroEMR_Platform` | Replaces two CHECK constraints and three procedures; opts in private-read validation and private view/write denial-audit capability pairs | Static inspection PASS; target execution BLOCKED |

**Actual environment/server/database names are not established.** The default Platform name is not proof of a runtime target. The API appsettings inspected contain no Platform/tenant connection configuration. Runtime overrides may differ. Do not use the README's `MicroEMR_Db` development example as a selected tenant. No credentials or user secrets were read. The smallest next decision is selection of a disposable/nonproduction environment with exact SQL instance, Platform database, tenant database, TenantUid and runtime principal, plus recovery evidence. Stop before any deployment until those are verified and explicitly approved. Production is excluded.

Tenant prerequisites: controlled manifest entries `0000-tenant-metadata` through `0084-prescription-generic-name` must already be present and hash-valid for this bounded deployment (85 preceding entries; 86 including 0085). Specifically preserve 0082 chart-alert safeguards, 0083 date precision and 0084 generic name evidence. Required dependencies include `PatientPrescription` (including GenericName, DateFields, RowVersion and existing prescribing columns), `Patient`, active `ApplicationUser`/`Provider` mapping, structured `AuditLog`, `PatientPrescription_GetByUid`, `PrescriptionDatesValid`, and `PrescriptionDateText`. Missing earlier entries require separate review/approval; do not silently deploy them with 0085.

`TenantDatabaseMigrationRunner` validates expected database name and tenant identity, obtains a session provisioning lock, checks applied hashes, and wraps each pending migration's GO batches plus `SchemaMigration` insertion in one transaction. It also writes Platform provisioning status and can create missing metadata/identity. **It is a mutation workflow, not a preflight/status command, and applies every pending manifest entry.** Require existing identity and only 0085 pending. Do not invoke it for inspection. `SchemaVersion` remains `1.0.0`; version alone cannot establish 0085 deployment. Canonical V2 SHA256 for the inspected 0085 (UTF-8, BOM removed, LF normalized): `4F784ACEBAC3BF086B22DE1F7437B0E78A6E428ED1E4430AA159B80F63DF67D0`.

Platform prerequisites: existing required chain through `026_user_permission_access_scope_repair.sql`, including access profiles/assignments, overrides, membership RowVersion, `AccessManagementAdministrator`, `PlatformAuditEvent`, `PlatformSecurityAuditEvent` and both named CHECK constraints. 027 preserves 026's `UserPermissionAccess_Get` repair. No Platform migration ledger is established by these scripts: verify deployment records AND definitions; a procedure name or keyword alone cannot prove the preceding chain.

## 3. Repeatability, privileges and data effects

- 0085 is **not directly repeatable**: unconditional ADD CONSTRAINT/CREATE TABLE fail on replay. Tracking and hash validation make the existing runner skip a completed migration. Partial untracked objects are a stop condition, not permission to rerun/drop them.
- 027 can converge when rerun against its expected intact constraint names, but is **not failure-atomic as supplied**: GO-separated DROP/ADD and replacements have no outer deployment transaction. XACT_ABORT alone does not roll back prior committed batches. Use a reviewed transaction spanning batches on one connection, with fail-fast handling, or a documented restore recovery; any wrapper must be reviewed before approval. Do not execute ad hoc batches or automatically replay after failure.
- Both need a deployment principal with appropriate ALTER/CREATE TABLE/PROCEDURE/TRIGGER, constraint and permission-management rights (and existing runner metadata/Platform status rights). These exceed routine runtime access; do not grant runtime db_owner/sysadmin to make deployment work.
- 0085 scans/indexes the parent unique pair and can acquire schema locks. It does not backfill notes, rewrite existing prescription rows or alter finalized artifacts. 027 validates existing constraint rows and can block/fail on incompatible data; it does not invoke its replacement mutation procedures or change stored roles, profiles, assignments or overrides.
- Registration alone grants **no access**. No separate permission-registration table is assumed. Permission catalogue/constraint/procedure support and effective assignments are distinct evidence.
- Runtime must execute `PatientPrescription_CreateDraft`, `PatientPrescription_UpdateDraft`, `PatientPrescriptionPrivateNote_Read`, `PatientPrescriptionPrivateNote_RecordDenied` and the existing Platform permission/audit procedures. Direct table DML and helper execution stay denied to public. Verify under the actual non-owner runtime principal; owner/sysadmin bypass cannot establish protection.
- Deploy/restart matching API and Web binaries/assets if old versions are running. SQL changes alone do not require a process restart. `CurrentUserPermissionService` caches per request; existing profile/override changes appear next request and are not tenant permission claims in OIDC. Refresh navigation; fresh A/B logins are useful visual evidence, not an unconditional reauthentication requirement for SQL registration.

## 4. Recovery and exact proposed order (approval required)

Before execution: verify nonproduction ownership/identity; capture current metadata, procedure definitions, constraints and assignments; take protected, restorable backups of both databases and document restore location, access controls, recovery point and restore validation. Include Auth backup/recovery if separately approving account creation. Plan a maintenance window and quiesce writes. Paired recovery must avoid mismatched schema/application/permissions.

1. Verify exact SQL instance/Platform/tenant identity, complete preceding chains, hashes and runtime principal with read-only checks below. Review evidence and recovery plan.
2. Obtain explicit approval naming those targets, these two script paths, sequence, existing runner Platform-status writes, transaction handling and any separate individual grants/fictional fixture actions. Approval of this report is not deployment approval.
3. Apply Platform `027` after confirmed `026`, using the reviewed failure handling. Check registration metadata immediately; stop on error.
4. Apply tenant `0085` through the existing controlled runner with only 0085 pending. If more are pending, stop. Do not insert migration ledger rows manually.
5. Verify both stores, principals and unchanged assignments; activate matching API/Web; then separately approved individual access changes and fictional UI fixtures. Perform V1-V3.

No down script is supplied. Prefer transactional rollback before commit or forward repair after diagnosis; a restore after clinical writes requires an approved reconciliation plan. Never drop protected note/revision tables, delete retained records or replace historical signed output as rollback. New notes, audit history and newly signed fictional artifacts are durable clinical records after acceptance actions. Backups/privileged DBA access remain an operational confidentiality qualification.

## 5. Read-only verification SQL and interpretation

**Prepared and source-reviewed only; not executed.** Use separate SSMS connections explicitly selecting each confirmed database. No `USE` names are fabricated. Run identity/metadata existence checks first; stop on missing objects or insufficient metadata visibility. Later queries assume prerequisites exist. NULL metadata is not PASS. Replace bracketed placeholders only with confirmed nonproduction values. These queries do not select protected NoteText or call audited read procedures (those write audit events).

### A. Both connections: identity and metadata visibility

```sql
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName,
       ORIGINAL_LOGIN() AS OriginalLogin, USER_NAME() AS DatabaseUser,
       HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION') AS CanViewDefinition;
```

PASS requires the approved instance/database and authorized metadata reviewer. Record results separately for Platform and tenant. No result has yet been obtained.

### B. Tenant preflight and postflight

```sql
SELECT name, type_desc FROM sys.objects
WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN
(N'TenantDatabaseIdentity',N'SchemaMigration',N'PatientPrescription',N'Patient',
 N'ApplicationUser',N'Provider',N'AuditLog',N'PatientPrescription_GetByUid',
 N'PrescriptionDatesValid',N'PrescriptionDateText');
-- Continue only if prerequisite objects are present and identity matches selected tenant.
SELECT TenantUid,TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,SchemaVersion,ScriptHash,AppliedAt,AppliedBy
FROM dbo.SchemaMigration ORDER BY MigrationId;
SELECT name,type_desc FROM sys.objects
WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN
(N'PatientPrescriptionPrivateNote',N'PatientPrescriptionPrivateNoteRevision',
 N'PrivateNote_Retain',N'PrivateNoteRevision_Retain',
 N'PatientPrescriptionPrivateNote_Write',N'PatientPrescriptionPrivateNote_Read',
 N'PatientPrescriptionPrivateNote_RecordDenied');

-- Column/type/nullability/identity inventory; no clinical content read.
SELECT OBJECT_NAME(c.object_id) AS ObjectName,c.column_id,c.name,
       TYPE_NAME(c.user_type_id) AS DataType,c.max_length,c.precision,c.scale,
       c.is_nullable,c.is_identity
FROM sys.columns c WHERE c.object_id IN
(OBJECT_ID(N'dbo.PatientPrescriptionPrivateNote'),
 OBJECT_ID(N'dbo.PatientPrescriptionPrivateNoteRevision'))
ORDER BY ObjectName,c.column_id;

SELECT OBJECT_NAME(i.object_id) AS TableName,i.name,i.is_unique,i.is_primary_key,
       ic.key_ordinal,c.name AS ColumnName
FROM sys.indexes i JOIN sys.index_columns ic
 ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id IN (OBJECT_ID(N'dbo.PatientPrescription'),
 OBJECT_ID(N'dbo.PatientPrescriptionPrivateNote'),
 OBJECT_ID(N'dbo.PatientPrescriptionPrivateNoteRevision')) AND i.is_unique=1
ORDER BY TableName,i.name,ic.key_ordinal;

SELECT fk.name,OBJECT_NAME(fk.parent_object_id) AS ChildTable,
       cc.name AS ChildColumn,OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
       pc.name AS ParentColumn,fk.is_disabled,fk.is_not_trusted,
       fk.delete_referential_action_desc
FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc
 ON fc.constraint_object_id=fk.object_id
JOIN sys.columns cc ON cc.object_id=fc.parent_object_id AND cc.column_id=fc.parent_column_id
JOIN sys.columns pc ON pc.object_id=fc.referenced_object_id AND pc.column_id=fc.referenced_column_id
WHERE fk.name IN(N'FK_PrivateNote_Prescription',N'FK_PrivateNoteRevision_Note')
ORDER BY fk.name,fc.constraint_column_id;
SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints
WHERE parent_object_id=OBJECT_ID(N'dbo.PatientPrescriptionPrivateNoteRevision');
SELECT t.name,t.is_disabled,t.is_instead_of_trigger,e.type_desc,m.definition
FROM sys.triggers t JOIN sys.trigger_events e ON e.object_id=t.object_id
LEFT JOIN sys.sql_modules m ON m.object_id=t.object_id
WHERE t.name IN(N'PrivateNote_Retain',N'PrivateNoteRevision_Retain');
SELECT o.name,m.definition FROM sys.objects o
LEFT JOIN sys.sql_modules m ON m.object_id=o.object_id
WHERE o.schema_id=SCHEMA_ID(N'dbo') AND o.name IN
(N'PatientPrescriptionPrivateNote_Write',N'PatientPrescriptionPrivateNote_Read',
 N'PatientPrescriptionPrivateNote_RecordDenied',N'PatientPrescription_CreateDraft',
 N'PatientPrescription_UpdateDraft',N'PatientPrescription_GetByUid',
 N'PrescriptionDatesValid',N'PrescriptionDateText');
SELECT p.name AS PrincipalName,OBJECT_NAME(dp.major_id) AS ObjectName,
       dp.state_desc,dp.permission_name
FROM sys.database_permissions dp JOIN sys.database_principals p
 ON p.principal_id=dp.grantee_principal_id
WHERE dp.class=1 AND dp.major_id IN
(OBJECT_ID(N'dbo.PatientPrescriptionPrivateNote'),
 OBJECT_ID(N'dbo.PatientPrescriptionPrivateNoteRevision'),
 OBJECT_ID(N'dbo.PatientPrescriptionPrivateNote_Write'));

-- Metadata checks on a separate connection as the actual runtime principal.
SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','EXECUTE') AS CanExecute
FROM (VALUES(N'dbo.PatientPrescription_CreateDraft'),(N'dbo.PatientPrescription_UpdateDraft'),
 (N'dbo.PatientPrescriptionPrivateNote_Read'),(N'dbo.PatientPrescriptionPrivateNote_RecordDenied'),
 (N'dbo.PatientPrescriptionPrivateNote_Write')) v(ObjectName);
SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','SELECT') AS CanSelect,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','INSERT') AS CanInsert,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','UPDATE') AS CanUpdate,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','DELETE') AS CanDelete
FROM (VALUES(N'dbo.PatientPrescriptionPrivateNote'),
 (N'dbo.PatientPrescriptionPrivateNoteRevision')) v(ObjectName);
```

Preflight PASS: exactly one identity matching approved Platform assignment; all 85 preceding manifest IDs present with accepted hashes, no unknown/missing migrations; required schema/definitions intact; 0085 absent and no partial protected objects or parent `UQ_PatientPrescription_PatientPair`. Compare ledger to `db/tenant-clinical/manifest.json`, canonical hashing and `TenantMigrationHashPolicy` (only pinned early legacy hashes are accepted). Ledger count alone is insufficient. Existing completed 0085 requires postflight verification, not replay. Any partial mismatch is BLOCKED pending diagnosis.

Postflight PASS requires all seven new named objects, matching 0085 ledger ID/hash above, and all preceding evidence retained. Compare full procedure definitions against 0085, not just existence. Check additive private parameters, required transaction/audit handling and protected-read commit-before-return. The two draft procedures must retain existing public/date/generic behavior.

Required columns (all NOT NULL unless marked `?`; nvarchar metadata max_length is bytes):

- Current: PrivateNoteId bigint identity PK; PrivateNoteUid uniqueidentifier unique; PrescriptionUid uniqueidentifier unique; PatientUid uniqueidentifier; OriginalPrescriberUserId bigint; NoteText nvarchar(1000)? (2000 bytes); CreatedBy bigint; CreatedAtUtc datetime2(7); UpdatedBy bigint?; UpdatedAtUtc datetime2(7)?; RevisionNumber int.
- Revision: RevisionUid uniqueidentifier PK; PrivateNoteUid uniqueidentifier; RevisionNumber int; Operation nvarchar(10) with Create/Update/Clear CHECK; NoteText nvarchar(1000)?; ActorUserId bigint; CreatedAtUtc datetime2(7); RequestCorrelationId nvarchar(100); AuditEventUid uniqueidentifier unique.
- Parent pair unique key `(PrescriptionUid,PatientUid)`; trusted enabled composite FK to parent; trusted enabled revision-to-note FK; unique revision pair `(PrivateNoteUid,RevisionNumber)`; no cascading deletes. Auto-generated unique/CHECK names are not invented.
- Both retention triggers enabled, INSTEAD OF: current DELETE; revision UPDATE and DELETE. Definitions throw 53014.
- Public DENY for SELECT/INSERT/UPDATE/DELETE on both tables and EXECUTE on helper. Runtime CanExecute=1 for four entry procedures, 0 for helper; all eight table access results 0. NULL or privileged bypass is not PASS.

### C. Platform preflight/postflight and selected-user effective access

```sql
SELECT name,type_desc FROM sys.objects WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN
(N'Tenant',N'TenantDatabase',N'UserTenantMembership',N'UserTenantRole',
 N'AccessProfile',N'AccessProfilePermission',N'UserTenantAccessProfile',
 N'UserPermissionOverride',N'PlatformAuditEvent',N'PlatformSecurityAuditEvent',
 N'AccessManagementAdministrator',N'AccessProfile_GetEffective',
 N'UserPermissionAccess_Get',N'AccessProfile_ReplacePermissions',
 N'UserPermissionOverride_Set',N'PlatformSecurityAudit_RecordMissingPermission');
-- Use only after prerequisite objects above are confirmed.
SELECT t.TenantUid,t.TenantKey,t.TenantStatus,d.DatabaseServerKey,d.DatabaseName,
       d.DatabaseStatus,d.CurrentSchemaVersion,d.LastMigrationAt
FROM dbo.Tenant t JOIN dbo.TenantDatabase d ON d.TenantUid=t.TenantUid
WHERE t.TenantUid=CONVERT(uniqueidentifier,N'<confirmed TenantUid>');
SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints
WHERE name IN(N'CK_UserPermissionOverride_Key',N'CK_PlatformSecurityAuditEvent_CapabilityPermission');
SELECT o.name,m.definition FROM sys.objects o LEFT JOIN sys.sql_modules m ON m.object_id=o.object_id
WHERE o.schema_id=SCHEMA_ID(N'dbo') AND o.name IN
(N'AccessProfile_ReplacePermissions',N'UserPermissionOverride_Set',
 N'PlatformSecurityAudit_RecordMissingPermission',N'UserPermissionAccess_Get',
 N'AccessProfile_GetEffective',N'AccessManagementAdministrator');

-- Run separately for A, B and the non-authorized fictional user; no broad user listing.
WITH Target AS
(SELECT CONVERT(uniqueidentifier,N'<confirmed TenantUid>') AS TenantUid,
        CONVERT(nvarchar(450),N'<confirmed fictional Auth subject>') AS UserId),
Effective AS
(
 SELECT pp.PermissionKey FROM Target x
 JOIN dbo.UserTenantAccessProfile a ON a.TenantUid=x.TenantUid AND a.UserId=x.UserId
 JOIN dbo.AccessProfile p ON p.AccessProfileUid=a.AccessProfileUid AND p.TenantUid=x.TenantUid AND p.IsActive=1
 JOIN dbo.AccessProfilePermission pp ON pp.AccessProfileUid=p.AccessProfileUid
 WHERE NOT EXISTS(SELECT 1 FROM dbo.UserPermissionOverride o WHERE o.TenantUid=x.TenantUid
  AND o.UserId=x.UserId AND o.PermissionKey=pp.PermissionKey AND o.OverrideState='Deny')
 UNION
 SELECT o.PermissionKey FROM Target x JOIN dbo.UserPermissionOverride o
 ON o.TenantUid=x.TenantUid AND o.UserId=x.UserId WHERE o.OverrideState='Allow'
)
SELECT x.TenantUid,x.UserId,m.MembershipStatus,e.PermissionKey,
 CAST(CASE WHEN m.MembershipStatus='Active' AND e.PermissionKey IS NOT NULL THEN 1 ELSE 0 END AS bit) AS EffectiveAllowed
FROM Target x LEFT JOIN dbo.UserTenantMembership m ON m.TenantUid=x.TenantUid AND m.UserId=x.UserId
LEFT JOIN Effective e ON 1=1 ORDER BY e.PermissionKey;
SELECT r.RoleName FROM dbo.UserTenantRole r
WHERE r.TenantUid=CONVERT(uniqueidentifier,N'<confirmed TenantUid>')
AND r.UserId=N'<confirmed fictional Auth subject>';
SELECT a.AccessProfileUid,p.Name,p.IsActive,pp.PermissionKey
FROM dbo.UserTenantAccessProfile a JOIN dbo.AccessProfile p
 ON p.AccessProfileUid=a.AccessProfileUid AND p.TenantUid=a.TenantUid
LEFT JOIN dbo.AccessProfilePermission pp ON pp.AccessProfileUid=p.AccessProfileUid
WHERE a.TenantUid=CONVERT(uniqueidentifier,N'<confirmed TenantUid>') AND a.UserId=N'<confirmed fictional Auth subject>';
SELECT PermissionKey,OverrideState FROM dbo.UserPermissionOverride
WHERE TenantUid=CONVERT(uniqueidentifier,N'<confirmed TenantUid>') AND UserId=N'<confirmed fictional Auth subject>';

-- Separate connection as actual Platform runtime principal; all must return 1.
SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','EXECUTE') AS CanExecute
FROM (VALUES(N'dbo.AccessProfile_GetEffective'),(N'dbo.UserPermissionAccess_Get'),
 (N'dbo.PlatformSecurityAudit_RecordMissingPermission')) v(ObjectName);
```

Preflight PASS requires preceding Platform deployment records through 026 and matching definitions, both named constraints present/enabled/trusted, active selected tenant/assignment and confirmed clinical target identity. Postflight: compare exact three replacement procedures/two constraints to 027; new private view/write capability pairs and private-read key present, old shapes preserved, 026 repaired query unchanged. Compare assignment/override snapshots before and after; 027 alone must not change them. Registration may PASS while users correctly have no private access.

The effective-access SELECT mirrors repository `012` logic plus Active membership gating; it does not invoke stored procedures. A requires Patients.View + Prescriptions.Prescribe + Prescriptions.ViewPrivateNotes; B requires Patients.View + Prescriptions.ViewPrivateNotes, with no Prescriptions.Prescribe for this reader fixture. The non-authorized user can retain Patients.View but must lack effective Prescriptions.ViewPrivateNotes. Tenant roles alone, including administrator, do not substitute for those effective keys. No global clinical-role grant is proposed.

### D. Tenant fictional actor prerequisites (after identity confirmation)

```sql
SELECT u.UserId,u.UserUid,u.AuthSubjectId,u.DisplayName,u.IsActive,
       u.ProviderId,p.ProviderUid,p.IsActive AS ProviderIsActive
FROM dbo.ApplicationUser u LEFT JOIN dbo.Provider p ON p.ProviderId=u.ProviderId
WHERE u.AuthSubjectId=N'<confirmed fictional Auth subject>';
```

Run for A/B/non-authorized user. Require one mapped active clinical actor per subject, nonblank display name, correct Auth identity/membership; A additionally requires active linked Provider. B does not need a Provider. Confirm Auth account active through existing User Administration detail (no invented Auth query). Missing prerequisites are BLOCKED, not evidence of an implementation failure. No patient lookup or protected content query is needed for deployment.

## 6. Approval status and fixture preparation

**Approval NOT REQUESTED AS AN EXECUTABLE DEPLOYMENT YET: targets and recovery are unidentified. Deployment DEFERRED; execution BLOCKED by missing target/recovery/explicit approval.** No grants, administration saves, fixtures or signed artifacts were created. Provide exact nonproduction instance/database pair, TenantUid, runtime principal and recovery confirmation first; then request approval for the concrete sequence in section 4. Do not infer it from Step 122 authorization.

After deployment and separate write authorization, use existing UI workflows:

| Fixture | Preparation and prerequisites | Current status |
| --- | --- | --- |
| TEST ONLY Prescriber A | User Administration -> Add/details; active Auth account, Active selected-tenant membership and active provisioned clinical identity. Providers -> Add/Link User if necessary; active Provider linked to A. Manage Access individual grants as above, with authorized administrator. | BLOCKED |
| TEST ONLY Reader B | Same tenant, active account/membership/clinical identity; no Provider required; only approved read permissions above. | BLOCKED |
| TEST ONLY Chart Reader C | Same tenant, active identity; chart access without private-read permission. Do not broaden permissions. | BLOCKED |
| Morgan Private-Note Test | Patients registration workflow; unmistakably fictional, nonproduction only. Capture generated IDs from UI, never invent them. | BLOCKED |
| A-owned Draft | Patient -> Medications -> Prescriptions -> New Prescription; fictional product/display TEST ONLY brand tablet, Oral, Once daily, TEST ONLY directions, quantity 1 tablet, refills 0, valid full written date. Public Notes to pharmacist: TEST ONLY PHARMACIST 122. Private: TEST ONLY INTERNAL NOTE 122. | BLOCKED |
| Finalized prescription | Use the same newly created test prescription after V1; sign/finalize through existing attestation. Never edit existing historical signed output. | BLOCKED |
| Correction Draft | Reuse only if already available; optional, not a new prerequisite. It must start without inherited private note. | DEFERRED |

Permission change navigation: User Administration -> user Details -> Manage Access -> selected permission Override -> Save, only after explicit approval of that user's grant. This uses existing audited/concurrency-controlled procedures. Shared-profile edits affect other users; prefer the separately approved individual override. Administrative Users.ManageAccess and applicable provisioning/provider/patient permissions are operator prerequisites, not extra grants for B. If UI cannot create a prerequisite, document the precise action needed and obtain approval before database modification.

## 7. V1-V3 execution cards and actual results

All visual outcomes **DEFERRED**, prerequisite controlled deployment/fixtures **BLOCKED**. No screenshots, PDF inspection, live browser observations or user confirmation exist for Step 122. Source inspection is not visual PASS. Keep private fixture values out of ordinary logs/evidence attachments; use only fictional sentinel evidence.

| Check | Exact navigation/actions and required observation | Result |
| --- | --- | --- |
| V1 author create/reopen | A -> Patients -> Search -> Morgan Private-Note Test -> Medications -> Prescriptions -> New Prescription. Separate Private Note editor visible; enter sentinel and Save Draft. Row Details displays it. Reopen Edit draft: preserved. | DEFERRED |
| V1 edit | Change private text to TEST ONLY INTERNAL NOTE 122 EDIT; save and reopen Details/editor. Changed value persists; public pharmacist text unchanged. | DEFERRED |
| V1 explicit clear | Clear editor -> save -> Details: Not recorded -> reopen editor blank. History retained. Restore initial sentinel and save for V2/V3. | DEFERRED |
| V2 author/internal reader | A reads separate internal Details. Fresh B session -> same patient -> Medications -> Prescriptions -> Details: same note/history, Read only, no private editor/Edit draft action. | DEFERRED |
| V2 finalized | A -> chart row Sign and finalize prescription -> attestation confirmation -> Details: Finalized, note still readable, read-only, no Edit draft. B reads same finalized note without editing. | DEFERRED |
| V3 internal baseline | Confirm authorized Details still displays private sentinel on the newly finalized fictional prescription. | DEFERRED |
| V3 prescription print | Row View/Print -> PDF -> print preview/printed output: no private sentinel/label; public directions/pharmacist notes retain existing output. Inspect PDF separately from browser display. | DEFERRED |
| V3 CPP | Patient Summary -> Current Prescriptions: no private note. Print CPP -> approved test clinician -> only Current Prescriptions -> print selected categories: no private sentinel/label in preview/output. | DEFERRED |
| V3 protected page printing | Browser print preview of internal Details: private section/history suppressed. | DEFERRED |

V1 PASS only after all create/edit/explicit-clear/reopen observations. V2 PASS only after A/B and Finalized read-only observations. V3 PASS only after internal baseline, actual prescription print/PDF and CPP display/selected printing exclusion with public fields preserved. Use FAIL only for an observed defect, with actor, exact navigation, action, expected/actual result and safe fictional evidence. USER-REPORTED PASS requires explicit user confirmation of completed checks; do not silently convert it to independently observed PASS. C's UI suppression may be recorded as a supplementary observation, not a new security penetration-testing requirement. Reuse Step 121 security/ownership evidence.

## 8. Retained evidence, qualifications and Step 123

Step 121 retained without rerunning: **185 .NET PASS / 0 FAIL / 0 skipped; 12 frontend PASS; TypeScript noEmit and strict emission PASS; final relevant build zero warnings/errors.** Preserve its earlier failed-run/correction history and disposable one-Platform/two-tenant fixture limits. Automated results establish neither deployed OIDC/principal/role configuration nor human visual appearance.

Preserve Step 118 Generic Name V1-V3 USER-REPORTED PASS, 111-case final gate/6 frontend and its qualifications; Step 116 Date Precision V1-V3 USER-REPORTED PASS, 95 distinct relevant .NET outcomes/4 frontend and chronology qualification; Step 114 Chart Alert evidence. Keep all 0082/0083/0084 and earlier migration deployment qualifications, target-engine/tenant assignment/identity/OIDC/backup governance and specification provenance/acceptance qualifications.

Other DE09 unresolved fields, medication-list Discontinue limits, Immunization numeric dose/unit/pairing, Allergy clinical semantics/external recorded-date/ambiguous DIN, Family History M7/M8 DEFERRED, PC07.10 obligations and OntarioMD/CPSO applicability remain unchanged. Overall PC07.01 is **PARTIAL**, not SATISFIED; 10 mandatory IDs / three packages (prescribing 5, CPP 4, scheduling 1). DE09.019 adds no mandatory ID and remains implemented with deployment/visual evidence pending.

Recommended **Step 123: approve and execute the named nonproduction deployment, then close V1-V3 evidence**. First resolve exact targets and verified recovery, review preflight output, and request explicit scoped deployment/grant/fixture approval. Record actual execution/version/principal evidence and observed or user-reported visual results. Investigate only concrete failures; preserve artifacts and all prior qualifications.

## 9. Checks performed and stop

Read-only local Git checkpoint; exact SQL/manifest/runner/hash/permission and UI source inspection; canonical 0085 hash calculation; report-only diff/whitespace review. SQL is prepared from repository-confirmed names, but has no live syntax/execution result. No automated suites/build repeated. No database target claimed verified. Stop at the mandatory approval gate; no database connection or modification performed.
