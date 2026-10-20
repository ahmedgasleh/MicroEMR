# Step 126B - SQL runtime identity and permission scope

Date: 2026-10-20 (America/Toronto). **ANALYSIS AND DESIGN ONLY. No SQL connection or execution. Normal-runtime candidate ready for scoped review; BLOCKED FOR FULL RUNTIME until administrative routing is implemented and verified.** PC07.01 PARTIAL; 10 mandatory IDs / three packages; V1-V3 DEFERRED.

## 1. Git checkpoint and evidence boundary

Branch `main`, HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Reports 157-162 were already untracked; no tracked modifications. Only this report added. No application/configuration/SQL source change, build, test-suite run, migration, connection switch, restart, fixture, clinical write, commit or remote operation.

Read [Step 126A](162-step126a-pc07-01-runtime-grant-security-review.md) first. Reused its full inventory and caller findings and Step 124B's retained object/ownership/dependency metadata. Focused inspection covered the eight template entry points, their controllers/services/repositories, tenant/Platform connection selection, DI, DatabaseTool and Auth startup. No new 308-item inventory or SQL query. [Step 125](161-step125-pc07-01-private-notes-runtime-and-visual-readiness.md) remains the source for the CONNECT-only prior token baseline and pending runtime/fixture evidence; this step does not refresh it.

Targets remain existing `microemr_dev` on `192.168.50.1,50013`, SQL instance `EC2AMAZ-TC26CQK\TELEMED52`, databases `MicroEMR_Platform` (P) and `MicroEMR_LocalDev_Fresh` (T). Primary tenant `F94C6ECD-4417-483E-8A94-0507CF8384C2`. No Auth or secondary `MicroEMR_Db` grants.

## 2. Decision: separate database authority from application permissions

All eight unresolved procedures are **REQUIRED for normal runtime**, including existing personal authoring. Clinic authoring through these same entry points is application administration, not SQL database administration. Their complete existing business capability is included intentionally; SQL EXECUTE cannot restrict them to the Personal branch. Existing scope/owner/role checks remain the boundary. No broad SQL role, direct template-table write, DDL, impersonation or additional template account is needed.

The user's separate-administration approach is retained for the 35 previously EXCLUDED permissions: they remain excluded **from microemr_dev**, not removed from the product. Their current administrative/security-review/onboarding/import callers must continue through a separately scoped application-administration path. This is a design recommendation for a bounded service identity, not approval to create an account, choose credentials or grant it privileges. Database deployment remains a different, operator-controlled capability outside ordinary requests. Do not use a deployment/sysadmin credential for those application-administration endpoints.

Thus the preferred architecture can support the current application after targeted connection routing. **It cannot preserve all functionality by changing the two shared connection strings alone.** Zero unresolved normal-runtime permissions does not mean administrative implementation, Auth security or actual runtime verification is complete. Moving all 35 into microemr_dev would avoid routing changes but contradict the approved separation approach; it is not this recommendation.

## 3. Eight-permission resolution matrix

Every row is database `MicroEMR_LocalDev_Fresh`, schema `dbo`, permission `EXECUTE`. All use the existing `ITenantSqlConnectionFactory`; none requires provisioning, schema migration or database-administrator authority. Cached definitions have dbo effective ownership, audit writes and no CREATE/ALTER schema or login operations. Step 124B's dependency review applies.

| Exact object | Verified calling path | Ordinary runtime dependency and administrative scope | Boundary / recommendation |
| --- | --- | --- | --- |
| `dbo.DocumentTemplateAdmin_Create` | `TemplateAdministrationController.Create` -> `TemplateAdministrationService.CreateAsync` -> `TemplateAdministrationRepository.CreateAsync` | Creates Personal or Clinic template and initial draft; no database provisioning | REQUIRED. Controller Templates.Manage; EnsureCanCreate rejects System, requires clinic administrator for Clinic and authorized owner for Personal; validates definition |
| `dbo.DocumentTemplateAdmin_Clone` | Same controller/service/repository, `Clone` / `CloneAsync` | Clones visible source into authorized Personal/Clinic destination | REQUIRED. CanView source and EnsureCanCreate destination; no blanket ownership override |
| `dbo.DocumentTemplateAdmin_UpdateMetadata` | Same controller/service/repository, `UpdateMetadata` / `UpdateMetadataAsync` | Edits authorized Personal/Clinic metadata | REQUIRED. CanMutate current and EnsureCanCreate destination; optimistic row version; System mutation refused by application |
| `dbo.DocumentTemplateAdmin_SetActive` | Same controller/service/repository, `SetActive` / `SetActiveAsync` | Activates/deactivates an authorized template without deleting history | REQUIRED. CanMutate and row version; no physical template deletion |
| `dbo.DocumentTemplateVersion_CreateDraft` | `DocumentTemplateVersionsController.CreateDraft` -> `DocumentTemplateVersionService.CreateDraftVersionAsync` -> `DocumentTemplateVersionRepository.CreateDraftAsync` | Authoring draft from published version; service returns existing draft when present | REQUIRED. Controller CanAccess(mutate=true) checks CanMutate and resolved clinical actor; SQL requires current published source |
| `dbo.DocumentTemplateVersion_UpdateDraft` | Versions controller `UpdateDraft` -> service `UpdateDraftVersionAsync` -> repository `UpdateDraftAsync` | Edits draft content/definition | REQUIRED. Same scope/owner/role boundary; serializer and row version; SQL rejects editing published/retired version |
| `dbo.DocumentTemplateVersion_Publish` | Versions controller `Publish` -> service `PublishVersionAsync` -> repository `PublishAsync` | Publishes authorized authoring draft for subsequent selection | REQUIRED. Same CanMutate; definition validation and expected row version; SQL checks Draft state |
| `dbo.DocumentTemplateVersion_Retire` | Versions controller `Retire` -> service `RetireVersionAsync` -> repository `RetireAsync` | Retires authorized version while retaining historical content | REQUIRED. Same CanMutate; expected row version/state transition; no database DDL or clinical-record deletion |

`TemplateAdministrationController` has class-level Templates.Manage. The versions controller's actual boundary is authenticated access plus its CanAccess/TemplateAuthorizationService check and clinical actor resolution for mutations; it does **not** declare the same Templates.Manage attribute. This review preserves that existing behavior and does not claim a stronger permission gate than the source implements. Any desired policy alignment is a separately scoped security change, not silently included here.

Giving the service SQL EXECUTE permits the entire procedure to a holder of its credential. It does not authenticate an end user or independently enforce Platform permission keys. Direct credential use could bypass the HTTP authorization layer; protect credentials as service secrets. The application already supplies trusted actor/scope inputs under the prior sa configuration. Replacing sa with exact EXECUTE rights reduces database authority without changing that trust model. No direct-read path or helper execution is added.

## 4. Shared-connection dependency analysis

Existing names below are source names, not proposed configuration aliases:

- `PlatformDatabaseConnection.GetConnectionString` reads `ConnectionStrings:PlatformDatabase`. Tenant catalog/resolver, membership, effective access, entitlement, security-denial writers, access administration, security review and provisioning status repositories share it. `PlatformDatabaseHealthCheck` opens the same setting.
- `TenantSqlConnectionFactory.OpenConnectionAsync` requires tenant context, resolves its Platform assignment, uses `ConfigurationTenantDatabaseSecretProvider` (`TenantDatabaseSecrets:{secretReference}`), validates assigned database name and verifies TenantDatabaseIdentity. Primary existing key is `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh`. Clinical, template and administrative tenant repositories share the factory. It is scoped in DI, not selected by the clinician's SQL login.
- `AddMicroEmrInfrastructure` registers both ordinary repositories and `AddMicroEmrPlatformInfrastructure` / `AddMicroEmrTenantProvisioning`. Registration alone does not run migration. Inspected API Program has no hosted tenant migration/startup invocation. No API tenant migration worker was identified in these relevant registrations; this is not a claim about externally scheduled jobs.
- DatabaseTool independently loads configuration/environment/user secrets, checks `PlatformAdministration:Enabled` and ActorId, registers the same Platform/provisioning services and tenant factory. Its migration runner resolves the **same logical tenant secret reference** and updates Platform provisioning status, tenant identity/ledger and pending schema batches. Separate process configuration can supply deployment credentials under these existing keys without sharing an API secret store. Switching a common environment/secret source indiscriminately would break deployment or leak its privileges into runtime.
- Auth registers Platform services and an Auth `SeedData` hosted service. That service calls `Database.MigrateAsync` and performs Identity/OpenIddict seed writes in its own Auth database context. API administrative Identity operations also use AuthDatabase / AuthServerConnection fallback. These remain unchanged pending separate review; no claim of whole-system removal of sa.

| Operation / category | Database | Existing connection | Required privilege | Recommended execution identity |
| --- | --- | --- | --- | --- |
| A: charts, prescriptions/private-note entry points, encounters, scheduling, results, reports, clinical output/audits | T | ITenantSqlConnectionFactory / assigned tenant secret | Exact entry EXECUTE and retained 80 column SELECT rights | microemr_dev; clinical authorization and tenant checks remain mandatory |
| A: Personal and Clinic template authoring through eight mixed entry points; template selection, patient-document generation and finalized retrieval | T | Same tenant factory; template/document repositories | Existing entry EXECUTE only, including eight resolved here | microemr_dev; scope/owner/role distinction stays in existing application policy |
| B: tenant catalog/assignment, caller membership/effective access, entitlement, security-denial audit, Platform health | P | PlatformDatabase; health opens connection | 12 retained entry EXECUTE / CONNECT | microemr_dev |
| C: access profiles/overrides, tenant membership/user administration, privileged security review | P | Same PlatformDatabase today | 15 excluded entry EXECUTE; authorized application operations, no db_owner/DDL | Separately scoped application-administration identity; preserve role/permission/entitlement gates |
| C: onboarding clinical actor, clinic settings, provider administration, legacy template mutation | T | Same tenant factory today | 11 excluded entry EXECUTE, no DDL or arbitrary data writes | Same bounded application-administration path, preserving selected tenant verification |
| C: clinical package validation/staging/import | T | ClinicalDataMigrationRepository / same tenant factory | Nine excluded ClinicalDataMigration entry EXECUTE; Users.ManageAccess; audited clinical import | Bounded application-administration path, not deployment login; keep existing API feature functional |
| D: tenant catalog lifecycle/assignment, operator membership commands, SQL user/role/security administration | P / selected deployment target | DatabaseTool PlatformDatabase and operator SQL tooling | Separately reviewed command-specific rights; not part of 308 runtime proposal | Operator-controlled administrative/deployment session, outside ordinary requests |
| E: tenant schema batches, identity/ledger and Platform provisioning status; Platform scripts | T / P | DatabaseTool tenant secret + PlatformDatabase; explicit operator script connection | Scoped deployment DDL/metadata rights, not runtime grants | Deployment identity in isolated tool configuration; no API fallback |
| Auth: login/token/Identity operations and startup seed/migration | Auth; Platform lookup separately | AuthServerConnection / API AuthDatabase; own PlatformDatabase setting | Auth EF/runtime and startup requirements need separate review | Existing Auth configuration unchanged; not covered by this candidate |

Patient-document generation and finalized retrieval already use retained document/version/clinical-output entry points and existing audit/signing services. They do not require the authoring eight just to read an existing finalized document. Keep version-pinned content, document signing/immutability checks and clinical audit unchanged. Publishing/retiring a template version must not rewrite an already signed document; this is preservation of the existing contract, not fresh runtime proof.

## 5. Smallest feasible implementation and identity architecture

No additional account is needed for templates. Separate deployment process configuration is sufficient for DatabaseTool versus normal runtime; no new deployment connection key is intrinsically required. Do not raise microemr_dev privileges to make the migration runner work.

The actual 35 excluded administrative dependencies justify a **bounded application-administration connection boundary** if their SQL capabilities must remain separate as approved. This identity is not sysadmin, db_owner, db_ddladmin or the deployment identity. Reuse an appropriately scoped existing identity if available; none has been verified here. Account creation and its exact grant manifest require separate approval. Its eventual manifest must include necessary tenant identity/lookup/audit dependencies, reviewed explicitly, not an assumed grant of only 35 procedures or a copy of all normal privileges.

Smallest proposed code change (design only):

1. Add an explicit Infrastructure connection abstraction for application-administration repositories and a separate secret binding, with DI wiring. Names/keys must be chosen during implementation and are **not existing settings**. Keep controllers thin and authorization in the existing API/Application paths. No caller-supplied privilege flag, query-string selector, fallback to sa or automatic retry under an administrative credential.
2. Route the P administration-only membership/user/role and security-review methods through that boundary. `SqlAccessProfileRepository` mixes GetEffective with administrative operations: keep GetEffective on normal PlatformDatabase and route only its administration methods. Do not replace its entire connection indiscriminately. Required denial-audit writers stay normal.
3. Route the T administration-only calls through the bounded tenant administration connection. Mixed repositories require method-level separation: `ClinicalUserRepository` lookup stays normal while Provision is administrative (CLI SetAuthSubjectId remains operator scope); ClinicProfile Get stays normal while Save is administrative; legacy document-template mutation stays separate from ordinary retrieval. Provider administration and ClinicalDataMigration repositories use the bounded path. The eight modern mixed authoring procedures stay normal by the explicit whole-entry capability decision in section 2.
4. Preserve assignment resolution, expected database validation and TenantDatabaseIdentity verification for **both** tenant paths. Privilege selection must not select another tenant/database. Preserve cancellation, ILogger behavior, actor attribution, stored-procedure parameters, audit, concurrency and last-administrator safeguards.
5. Separate API registration of runtime tenant-secret resolution from migration runner/status services. Keep shared resolver functionality available; do not remove it by deleting the whole provisioning registration. Register migration capabilities in DatabaseTool only, retain its explicit operator gate, and keep deployment credentials out of API configuration. Before moving any registration, verify actual consumers in the focused implementation task.

These routing/configuration changes are required **before a full API switch** under this chosen separation design. Granting the normal subset can happen first without switching connections. No automatic accounts, application code or configuration changes are made here. Keeping every application-administration operation outside the web process would instead require moving existing API features to a separate administration host/tool; that is larger and is not recommended merely to close this review.

Credentials: isolate deployment configuration by process/store, retain TLS settings, use trusted local development secret storage rather than tracked appsettings/source, redact logs, and never hand SQL credentials to clinical users. A second identity in the same API process separates SQL capability routing but does not isolate credentials from compromise of that process; stronger process isolation would be a separately justified design. Do not claim connection separation alone is an end-user authorization barrier.

## 6. Final classification and security conditions

| Scope | REQUIRED | EXCLUDED from microemr_dev | UNRESOLVED | Total |
| --- | ---: | ---: | ---: | ---: |
| P procedure EXECUTE | 12 | 15 | 0 | 27 |
| T procedure EXECUTE | 181 | 20 | 0 | 201 |
| T explicit column SELECT | 80 | 0 | 0 | 80 |
| Total | **273** | **35** | **0** | **308** |

Exact delta: section 3's eight T EXECUTE permissions move UNRESOLVED -> REQUIRED. No other classification changes. Required procedure total is 193; column count remains 80. The 35 exclusions are exactly Step 126A section 6's EXCLUDED rows; their functionality remains required on the bounded administration path. Zero UNRESOLVED describes this normal-account classification, not a completed grant design for the separate administration identity or Auth.

Retain Step 126A's security conditions: no broad schema EXECUTE, table-wide SELECT, direct template-table mutations, clinical-table DML, role memberships, ownership or grant option. The explicit 80-column list is unchanged. Protected private-note current/revision table denials and direct Write helper denial remain. Only approved Read/RecordDenied and Draft prescription entry points are callable; original-prescriber, Draft/version and finalized rules remain. Same-owner static chains permit intended internal work without granting protected storage/helper rights. Do not remove DENY when an entry fails: inspect ownership/context/dependency and fail closed. Avoid schema grants and column grants on protected tables; column-grant versus table-DENY subtleties cannot be used as a workaround. No new signed module, EXECUTE AS owner module or cross-database permission mechanism is proposed.

Risks remain: prior metadata may drift; service identity has whole-procedure capability; direct column reads rely on application/tenant checks; frozen columns require review when queries change; two-database grants are not atomic across databases; unknown existing effective permissions or missing metadata block execution. SQL impersonation verifies token permissions, not the account's password authentication or actual process configuration. Auth startup/Identity rights, fixtures and runtime identity are unverified in this step.

## 7. Revised normal-runtime grant candidate - NOT EXECUTED

The complete SQLCMD block below derives from Step 126A section 5 with only: (a) eight exact object literals removed from T @Omitted and added to T @Exec, and (b) review header wording updated. P 12, T 181, column 80 and omitted P 15 / T 20 are statically checked. All server/database/user, tenant identity, role/ownership, positive-permission baseline, transaction, effective-token and protected-denial checks are retained. No administrative identity script is included.

**Ready for review of exactly this partial normal grant; not authorized for execution and BLOCKED FOR FULL RUNTIME connection deployment.** Approval must name the full block and both target databases. Do not execute the old 265-right candidate in addition or append eight grants after its postcheck: that older candidate intentionally treats those eight as forbidden. This block replaces that normal candidate for future scoped approval. SQLCMD mode, fail-fast and per-database transaction limitations from Step 126A apply. Static comparison is not SQL compilation or runtime PASS.

```sql
:On Error exit
-- Step 126B REVIEW ONLY: normal subset; BLOCKED FOR FULL RUNTIME until administrative routing.
-- No execution authorized. Requires separate approval of this exact block and both targets.
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' THROW 53701,'Unexpected server.',1;
GO
USE [MicroEMR_Platform];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_Platform'
 THROW 53701,'Unexpected server/database.',1;
IF SUSER_SNAME()<>ORIGINAL_LOGIN() THROW 53702,'Use a non-impersonated reviewer session.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev'
 AND type='S' AND sid=SUSER_SID(N'microemr_dev'))
 THROW 53702,'Expected existing mapped SQL user required.',1;
IF EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=USER_ID(N'microemr_dev'))
 OR EXISTS(SELECT 1 FROM sys.server_role_members m JOIN sys.server_principals p ON p.principal_id=m.member_principal_id WHERE p.name=N'microemr_dev')
 OR EXISTS(SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND owner_sid=SUSER_SID(N'microemr_dev'))
 OR EXISTS(SELECT 1 FROM sys.schemas WHERE principal_id=USER_ID(N'microemr_dev'))
 THROW 53704,'Unexpected runtime role/ownership.',1;
IF COALESCE(HAS_PERMS_BY_NAME(N'microemr_dev','LOGIN','IMPERSONATE'),0)<>1
 AND COALESCE(IS_SRVROLEMEMBER('sysadmin'),0)<>1
 THROW 53705,'Reviewer needs existing safe permission-verification capability.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.TenantDatabase WHERE TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2'
 AND DatabaseName=N'MicroEMR_LocalDev_Fresh' AND DatabaseServerKey=N'local-sql'
 AND SecretReference=N'development:MicroEMR_LocalDev_Fresh')
 THROW 53703,'Unexpected tenant assignment.',1;
DECLARE @Exec table(ObjectName sysname PRIMARY KEY);
INSERT @Exec VALUES
 (N'dbo.AccessProfile_GetEffective'),
 (N'dbo.PlatformAuthorization_GetVersionForUser'),
 (N'dbo.PlatformEntitlement_GetActiveForUser'),
 (N'dbo.PlatformSecurityAudit_RecordCrossPatientOwnership'),
 (N'dbo.PlatformSecurityAudit_RecordInvalidTenantMembership'),
 (N'dbo.PlatformSecurityAudit_RecordMissingPermission'),
 (N'dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor'),
 (N'dbo.Tenant_GetByKey'),
 (N'dbo.Tenant_GetByUid'),
 (N'dbo.TenantDatabase_GetByTenantUid'),
 (N'dbo.UserTenantMembership_GetActiveByUserAndTenant'),
 (N'dbo.UserTenantMembership_GetActiveByUserId');
DECLARE @Read table(ObjectName sysname,ColumnName sysname,PRIMARY KEY(ObjectName,ColumnName));
-- No Platform direct table/column SELECT grants.
DECLARE @Omitted table(ObjectName sysname PRIMARY KEY);
INSERT @Omitted VALUES
 (N'dbo.AccessProfile_AssignUser'),
 (N'dbo.AccessProfile_Get'),
 (N'dbo.AccessProfile_GetUserAssignments'),
 (N'dbo.AccessProfile_List'),
 (N'dbo.AccessProfile_ReplacePermissions'),
 (N'dbo.PlatformAudit_RecordSecurityAuditReview'),
 (N'dbo.PlatformMembership_Activate'),
 (N'dbo.PlatformMembership_CreateWithInitialRole'),
 (N'dbo.PlatformMembership_Deactivate'),
 (N'dbo.PlatformMembership_ListByTenant'),
 (N'dbo.PlatformMembership_ReplaceRoles'),
 (N'dbo.PlatformSecurityAudit_GetByUid'),
 (N'dbo.PlatformSecurityAudit_Search'),
 (N'dbo.UserPermissionAccess_Get'),
 (N'dbo.UserPermissionOverride_Set');
IF EXISTS(SELECT 1 FROM @Exec WHERE OBJECT_ID(ObjectName,N'P') IS NULL)
 OR EXISTS(SELECT 1 FROM @Omitted WHERE OBJECT_ID(ObjectName,N'P') IS NULL)
 OR EXISTS(SELECT 1 FROM @Read WHERE COL_LENGTH(ObjectName,ColumnName) IS NULL)
 THROW 53706,'Expected object/column missing; review rather than partially grant.',1;
-- Reject unexpected pre-existing positive user permissions; never silently revoke them.
IF EXISTS(SELECT 1 FROM sys.database_permissions p
 WHERE p.grantee_principal_id IN(USER_ID(N'microemr_dev'),DATABASE_PRINCIPAL_ID(N'public'))
 AND (p.class<>1 OR OBJECTPROPERTY(p.major_id,'IsMSShipped')=0)
 AND p.state IN('G','W') AND
 (p.state='W' OR NOT
 ((p.class=0 AND p.permission_name IN
 (N'CONNECT',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION'))
 OR (p.class=1 AND p.permission_name=N'EXECUTE' AND p.minor_id=0
 AND EXISTS(SELECT 1 FROM @Exec e WHERE OBJECT_ID(e.ObjectName)=p.major_id))
 OR (p.class=1 AND p.permission_name=N'SELECT'
 AND EXISTS(SELECT 1 FROM @Read r WHERE OBJECT_ID(r.ObjectName)=p.major_id
 AND COLUMNPROPERTY(p.major_id,r.ColumnName,'ColumnId')=p.minor_id)))))
 THROW 53707,'Unexpected positive permissions; approve a separate delta.',1;
IF EXISTS(SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals u ON u.principal_id=p.grantee_principal_id
 WHERE u.name=N'microemr_dev' AND p.state IN('G','W')
 AND (p.state='W' OR p.permission_name<>N'CONNECT SQL'))
 THROW 53707,'Unexpected positive server permissions.',1;
DECLARE @AsRuntime bit=0;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @GrantExec nvarchar(max),@GrantRead nvarchar(max);
 SELECT @GrantExec=STRING_AGG(CONVERT(nvarchar(max),N'GRANT EXECUTE ON OBJECT::'
 +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))+N' TO [microemr_dev];'),NCHAR(10)) FROM @Exec;
 EXEC sys.sp_executesql @GrantExec;
 SELECT @GrantRead=STRING_AGG(CONVERT(nvarchar(max),N'GRANT SELECT ON OBJECT::'
 +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))
 +N' ('+QUOTENAME(ColumnName)+N') TO [microemr_dev];'),NCHAR(10)) FROM @Read;
 IF @GrantRead IS NOT NULL EXEC sys.sp_executesql @GrantRead;
 EXECUTE AS LOGIN=N'microemr_dev';
 SET @AsRuntime=1;
 IF SUSER_SNAME()<>N'microemr_dev' OR USER_NAME()<>N'microemr_dev'
 THROW 53708,'Wrong effective verification principal.',1;
 IF EXISTS(SELECT 1 FROM @Exec WHERE COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','EXECUTE'),-1)<>1)
 OR EXISTS(SELECT 1 FROM @Read WHERE COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','SELECT',ColumnName,'COLUMN'),-1)<>1)
 THROW 53709,'Required effective permissions missing.',1;
 IF EXISTS(SELECT 1 FROM @Omitted WHERE COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','EXECUTE'),-1)<>0)
 THROW 53710,'Excluded/unresolved direct execution is available.',1;
 -- Private storage exists only in tenant; no cross-database lookup here.
 IF COALESCE(IS_SRVROLEMEMBER('sysadmin'),-1)<>0 OR COALESCE(IS_MEMBER('db_owner'),-1)<>0
 OR COALESCE(IS_MEMBER('db_ddladmin'),-1)<>0 OR COALESCE(IS_MEMBER('db_datareader'),-1)<>0
 OR COALESCE(IS_MEMBER('db_datawriter'),-1)<>0
 OR EXISTS(SELECT 1 FROM sys.fn_my_permissions(NULL,'DATABASE') WHERE permission_name NOT IN
 (N'CONNECT',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION'))
 THROW 53712,'Unexpected effective database privileges.',1;
 REVERT;
 SET @AsRuntime=0;
 COMMIT;
 SELECT DB_NAME() AS DatabaseContext,(SELECT COUNT(*) FROM @Exec) AS RequiredExecuteRights,
 (SELECT COUNT(*) FROM @Read) AS RequiredColumnRights,N'Required-only permission batch committed' AS Result;
END TRY
BEGIN CATCH
 IF @AsRuntime=1 REVERT;
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
GO
USE [MicroEMR_LocalDev_Fresh];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_LocalDev_Fresh'
 THROW 53701,'Unexpected server/database.',1;
IF SUSER_SNAME()<>ORIGINAL_LOGIN() THROW 53702,'Use a non-impersonated reviewer session.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev'
 AND type='S' AND sid=SUSER_SID(N'microemr_dev'))
 THROW 53702,'Expected existing mapped SQL user required.',1;
IF EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=USER_ID(N'microemr_dev'))
 OR EXISTS(SELECT 1 FROM sys.server_role_members m JOIN sys.server_principals p ON p.principal_id=m.member_principal_id WHERE p.name=N'microemr_dev')
 OR EXISTS(SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND owner_sid=SUSER_SID(N'microemr_dev'))
 OR EXISTS(SELECT 1 FROM sys.schemas WHERE principal_id=USER_ID(N'microemr_dev'))
 THROW 53704,'Unexpected runtime role/ownership.',1;
IF COALESCE(HAS_PERMS_BY_NAME(N'microemr_dev','LOGIN','IMPERSONATE'),0)<>1
 AND COALESCE(IS_SRVROLEMEMBER('sysadmin'),0)<>1
 THROW 53705,'Reviewer needs existing safe permission-verification capability.',1;
IF (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity)<>1 OR NOT EXISTS
 (SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2')
 THROW 53703,'Unexpected tenant identity.',1;
DECLARE @Exec table(ObjectName sysname PRIMARY KEY);
INSERT @Exec VALUES
 (N'dbo.DocumentTemplateAdmin_Clone'),
 (N'dbo.DocumentTemplateAdmin_Create'),
 (N'dbo.DocumentTemplateAdmin_SetActive'),
 (N'dbo.DocumentTemplateAdmin_UpdateMetadata'),
 (N'dbo.DocumentTemplateVersion_CreateDraft'),
 (N'dbo.DocumentTemplateVersion_Publish'),
 (N'dbo.DocumentTemplateVersion_Retire'),
 (N'dbo.DocumentTemplateVersion_UpdateDraft'),
 (N'dbo.ApplicationUser_GetByAuthSubjectId'),
 (N'dbo.Appointment_ReportByStatus'),
 (N'dbo.AppointmentHistory_GetByAppointmentUid'),
 (N'dbo.AuditLog_RecordPatientChartOpened'),
 (N'dbo.AuditLog_RecordStructuredRead'),
 (N'dbo.CdsAlert_Acknowledge'),
 (N'dbo.CdsAlert_Dismiss'),
 (N'dbo.CdsAlert_List'),
 (N'dbo.CdsAlert_PatientExists'),
 (N'dbo.CdsAlert_RecordFinding'),
 (N'dbo.CdsAlert_ResolveRuleFindings'),
 (N'dbo.CdsAlertHistory_List'),
 (N'dbo.ChronicDiseaseEnrollment_Create'),
 (N'dbo.ChronicDiseaseEnrollment_GetByUid'),
 (N'dbo.ChronicDiseaseEnrollment_Inactivate'),
 (N'dbo.ChronicDiseaseEnrollment_ListByPatient'),
 (N'dbo.ClinicalOutputArtifact_Create'),
 (N'dbo.ClinicalOutputArtifact_GetFinalBySource'),
 (N'dbo.ClinicalOutputArtifact_RecordFailure'),
 (N'dbo.ClinicProfile_Get'),
 (N'dbo.CppDisplayPreference_Get'),
 (N'dbo.CppDisplayPreference_Save'),
 (N'dbo.DocumentTemplate_GetActive'),
 (N'dbo.DocumentTemplate_GetAll'),
 (N'dbo.DocumentTemplate_GetByUid'),
 (N'dbo.DocumentTemplateVersion_GetByTemplateUid'),
 (N'dbo.DocumentTemplateVersion_GetByUid'),
 (N'dbo.Patient_Create'),
 (N'dbo.Patient_GetByUid'),
 (N'dbo.Patient_Search'),
 (N'dbo.Patient_UpdateDemographics'),
 (N'dbo.PatientAllergy_AssertNoKnownAllergies'),
 (N'dbo.PatientAllergy_Create'),
 (N'dbo.PatientAllergy_GetByUid'),
 (N'dbo.PatientAllergy_GetSnapshot'),
 (N'dbo.PatientAllergy_Resolve'),
 (N'dbo.PatientAllergy_RevokeNoKnownAllergies'),
 (N'dbo.PatientAllergy_Update'),
 (N'dbo.PatientChartAlert_Create'),
 (N'dbo.PatientChartAlert_GetByPatientUid'),
 (N'dbo.PatientChartAlert_GetByUid'),
 (N'dbo.PatientChartAlert_Resolve'),
 (N'dbo.PatientChartAlert_Update'),
 (N'dbo.PatientClinicalHistory_Archive'),
 (N'dbo.PatientClinicalHistory_Create'),
 (N'dbo.PatientClinicalHistory_List'),
 (N'dbo.PatientClinicalHistory_Update'),
 (N'dbo.PatientDocument_CommitSignConsultation'),
 (N'dbo.PatientDocument_Create'),
 (N'dbo.PatientDocument_GetByPatientUid'),
 (N'dbo.PatientDocument_GetByUid'),
 (N'dbo.PatientDocument_PrepareSignConsultation'),
 (N'dbo.PatientDocument_UpdateDraft'),
 (N'dbo.PatientDocumentRecipient_List'),
 (N'dbo.PatientDocumentRecipient_ReplaceDraft'),
 (N'dbo.PatientEncounter_Create'),
 (N'dbo.PatientEncounter_CreateStructured'),
 (N'dbo.PatientEncounter_GetByPatientUid'),
 (N'dbo.PatientEncounter_GetByUid'),
 (N'dbo.PatientEncounter_Sign'),
 (N'dbo.PatientEncounter_StartFromAppointment'),
 (N'dbo.PatientEncounter_UpdateNote'),
 (N'dbo.PatientEncounter_UpdateSoapNote'),
 (N'dbo.PatientEncounter_UpdateStructuredData'),
 (N'dbo.PatientEncounterAddendum_Create'),
 (N'dbo.PatientEncounterAddendum_GetByEncounterUid'),
 (N'dbo.PatientEncounterContribution_Get'),
 (N'dbo.PatientEncounterContribution_Save'),
 (N'dbo.PatientEncounterDiagnoses_Get'),
 (N'dbo.PatientEncounterDiagnoses_Save'),
 (N'dbo.PatientEncounterHistory_GetByEncounterUid'),
 (N'dbo.PatientFamilyMedicalHistory_Archive'),
 (N'dbo.PatientFamilyMedicalHistory_Create'),
 (N'dbo.PatientFamilyMedicalHistory_Get'),
 (N'dbo.PatientFamilyMedicalHistory_List'),
 (N'dbo.PatientFamilyMedicalHistory_Update'),
 (N'dbo.PatientFile_Archive'),
 (N'dbo.PatientFile_Create'),
 (N'dbo.PatientFile_GetByPatientUid'),
 (N'dbo.PatientFile_GetByUid'),
 (N'dbo.PatientFile_Restore'),
 (N'dbo.PatientImmunization_Create'),
 (N'dbo.PatientImmunization_GetByUid'),
 (N'dbo.PatientImmunization_ListByPatient'),
 (N'dbo.PatientImmunization_MarkEnteredInError'),
 (N'dbo.PatientImmunization_Update'),
 (N'dbo.PatientMedication_Create'),
 (N'dbo.PatientMedication_Discontinue'),
 (N'dbo.PatientMedication_GetByPatientUid'),
 (N'dbo.PatientMedication_GetByUid'),
 (N'dbo.PatientMedication_Update'),
 (N'dbo.PatientPrescription_Cancel'),
 (N'dbo.PatientPrescription_CreateDraft'),
 (N'dbo.PatientPrescription_Finalize'),
 (N'dbo.PatientPrescription_GetArtifact'),
 (N'dbo.PatientPrescription_GetByPatientUid'),
 (N'dbo.PatientPrescription_GetByUid'),
 (N'dbo.PatientPrescription_RecordOutputGeneration'),
 (N'dbo.PatientPrescription_SignAndFinalize'),
 (N'dbo.PatientPrescription_UpdateDraft'),
 (N'dbo.PatientPrescriptionPrivateNote_Read'),
 (N'dbo.PatientPrescriptionPrivateNote_RecordDenied'),
 (N'dbo.PatientProblem_Create'),
 (N'dbo.PatientProblem_GetByPatientUid'),
 (N'dbo.PatientProblem_GetByUid'),
 (N'dbo.PatientProblem_Resolve'),
 (N'dbo.PatientProblem_Update'),
 (N'dbo.PatientProviderRelationship_Create'),
 (N'dbo.PatientProviderRelationship_End'),
 (N'dbo.PatientProviderRelationship_List'),
 (N'dbo.PatientProviderRelationship_Update'),
 (N'dbo.PatientProviderRelationshipType_ListActive'),
 (N'dbo.PatientReferral_Close'),
 (N'dbo.PatientReferral_Create'),
 (N'dbo.PatientReferral_GetActiveProviders'),
 (N'dbo.PatientReferral_GetByPatientUid'),
 (N'dbo.PatientReferral_GetByUid'),
 (N'dbo.PatientReferral_GetProvider'),
 (N'dbo.PatientReferral_MarkResponseReceived'),
 (N'dbo.PatientReferral_MarkSent'),
 (N'dbo.PatientReferral_Send'),
 (N'dbo.PatientReferral_SetFollowUpDue'),
 (N'dbo.PatientReferral_SetResponseDocument'),
 (N'dbo.PatientReferral_UpdateDraft'),
 (N'dbo.PatientReferralArtifact_Get'),
 (N'dbo.PatientReferralClinicalSelection_Get'),
 (N'dbo.PatientReferralClinicalSelection_ReplaceDraft'),
 (N'dbo.PatientReferralDocument_GetByReferralUid'),
 (N'dbo.PatientReferralDocument_Link'),
 (N'dbo.PatientReferralDocument_Unlink'),
 (N'dbo.PatientResult_Correct'),
 (N'dbo.PatientResult_Create'),
 (N'dbo.PatientResult_GetByPatientUid'),
 (N'dbo.PatientResult_GetByUid'),
 (N'dbo.PatientResult_GetHistory'),
 (N'dbo.PatientResult_GetUnreviewed'),
 (N'dbo.PatientResult_GetUnreviewedCount'),
 (N'dbo.PatientResult_MarkEnteredInError'),
 (N'dbo.PatientResult_MarkReviewed'),
 (N'dbo.PatientResult_Update'),
 (N'dbo.PatientRiskFactor_Archive'),
 (N'dbo.PatientRiskFactor_Create'),
 (N'dbo.PatientRiskFactor_Get'),
 (N'dbo.PatientRiskFactor_List'),
 (N'dbo.PatientRiskFactor_Update'),
 (N'dbo.PatientTask_Complete'),
 (N'dbo.PatientTask_Create'),
 (N'dbo.PatientTask_GetByPatientUid'),
 (N'dbo.PatientTask_GetByUid'),
 (N'dbo.PatientTask_GetOpenForDashboard'),
 (N'dbo.PatientTask_GetOverdue'),
 (N'dbo.PatientTask_GetOverdueCount'),
 (N'dbo.PatientTask_Reopen'),
 (N'dbo.PatientTask_Update'),
 (N'dbo.PatientVital_Create'),
 (N'dbo.PatientVital_GetByPatientUid'),
 (N'dbo.PatientVital_GetByUid'),
 (N'dbo.PatientVital_Update'),
 (N'dbo.Provider_Get'),
 (N'dbo.Provider_List'),
 (N'dbo.ScheduleAppointment_Cancel'),
 (N'dbo.ScheduleAppointment_CreateWithCriticalFlag'),
 (N'dbo.ScheduleAppointment_GetByUid'),
 (N'dbo.ScheduleAppointment_GetByUidWithCriticalFlag'),
 (N'dbo.ScheduleAppointment_GetMonthSummary'),
 (N'dbo.ScheduleAppointment_MarkArrived'),
 (N'dbo.ScheduleAppointment_Reschedule'),
 (N'dbo.ScheduleAppointment_UpdateStatus'),
 (N'dbo.ScheduleAppointment_UpdateWithCriticalFlag'),
 (N'dbo.ScheduleResource_GetActive'),
 (N'dbo.SchedulingBlockedTime_Cancel'),
 (N'dbo.SchedulingBlockedTime_Create'),
 (N'dbo.SchedulingBlockedTime_GetForDateRange');
DECLARE @Read table(ObjectName sysname,ColumnName sysname,PRIMARY KEY(ObjectName,ColumnName));
INSERT @Read VALUES
 (N'dbo.TenantDatabaseIdentity',N'TenantUid'),
 (N'dbo.ApplicationUser',N'UserId'),
 (N'dbo.ApplicationUser',N'UserUid'),
 (N'dbo.ApplicationUser',N'Username'),
 (N'dbo.ApplicationUser',N'DisplayName'),
 (N'dbo.ApplicationUser',N'IsActive'),
 (N'dbo.ApplicationUser',N'AuthSubjectId'),
 (N'dbo.AuditLog',N'PatientId'),
 (N'dbo.AuditLog',N'EntityName'),
 (N'dbo.AuditLog',N'ActionName'),
 (N'dbo.AuditLog',N'EntityId'),
 (N'dbo.Patient',N'PatientId'),
 (N'dbo.Patient',N'PatientUid'),
 (N'dbo.Patient',N'IsDeleted'),
 (N'dbo.Patient',N'LastName'),
 (N'dbo.Patient',N'FirstName'),
 (N'dbo.Patient',N'HealthCardNumber'),
 (N'dbo.Patient',N'DateOfBirth'),
 (N'dbo.Patient',N'GenderIdentity'),
 (N'dbo.Patient',N'ChartNumber'),
 (N'dbo.PatientDocument',N'PatientId'),
 (N'dbo.PatientDocument',N'PatientDocumentUid'),
 (N'dbo.PatientDocument',N'PatientUid'),
 (N'dbo.PatientDocument',N'IsDeleted'),
 (N'dbo.PatientEncounter',N'AppointmentUid'),
 (N'dbo.PatientEncounter',N'EncounterUid'),
 (N'dbo.PatientEncounter',N'EncounterStatus'),
 (N'dbo.PatientReferral',N'ReferralUid'),
 (N'dbo.PatientReferral',N'PatientUid'),
 (N'dbo.PatientReferralDocument',N'ReferralUid'),
 (N'dbo.ScheduleAppointment',N'StartDateTimeUtc'),
 (N'dbo.ScheduleAppointment',N'EndDateTimeUtc'),
 (N'dbo.ScheduleAppointment',N'PrimaryResourceId'),
 (N'dbo.ScheduleAppointment',N'RoomResourceId'),
 (N'dbo.ScheduleAppointment',N'IsDeleted'),
 (N'dbo.ScheduleAppointment',N'AppointmentStatus'),
 (N'dbo.ScheduleAppointment',N'AppointmentUid'),
 (N'dbo.ScheduleAppointment',N'PatientUid'),
 (N'dbo.ScheduleAppointment',N'AppointmentType'),
 (N'dbo.ScheduleAppointment',N'Reason'),
 (N'dbo.ScheduleAppointment',N'ScheduleAppointmentId'),
 (N'dbo.ScheduleAppointment',N'IsCritical'),
 (N'dbo.ScheduleAppointment',N'IsAdHoc'),
 (N'dbo.ScheduleResource',N'ResourceId'),
 (N'dbo.ScheduleResource',N'ResourceUid'),
 (N'dbo.ScheduleResource',N'DisplayName'),
 (N'dbo.SchedulingBlockedTime',N'StartDateTimeUtc'),
 (N'dbo.SchedulingBlockedTime',N'EndDateTimeUtc'),
 (N'dbo.SchedulingBlockedTime',N'IsActive'),
 (N'dbo.SchedulingBlockedTime',N'ResourceUid'),
 (N'dbo.PatientResult',N'PatientResultId'),
 (N'dbo.PatientResult',N'PatientResultUid'),
 (N'dbo.PatientResult',N'PatientUid'),
 (N'dbo.PatientResult',N'ResultType'),
 (N'dbo.PatientResult',N'ResultName'),
 (N'dbo.PatientResult',N'ResultDate'),
 (N'dbo.PatientResult',N'ResultSummary'),
 (N'dbo.PatientResult',N'ResultValue'),
 (N'dbo.PatientResult',N'ResultUnit'),
 (N'dbo.PatientResult',N'ReferenceRange'),
 (N'dbo.PatientResult',N'ResultStatus'),
 (N'dbo.PatientResult',N'ReviewedAt'),
 (N'dbo.PatientResult',N'ReviewedBy'),
 (N'dbo.PatientResult',N'ReviewNote'),
 (N'dbo.PatientResult',N'CreatedAt'),
 (N'dbo.PatientResult',N'CreatedBy'),
 (N'dbo.PatientResult',N'UpdatedAt'),
 (N'dbo.PatientResult',N'UpdatedBy'),
 (N'dbo.PatientResult',N'RowVersion'),
 (N'dbo.PatientResult',N'LifecycleStatus'),
 (N'dbo.PatientResult',N'SourceType'),
 (N'dbo.PatientResult',N'SourceOrganization'),
 (N'dbo.PatientResult',N'SourceSystem'),
 (N'dbo.PatientResult',N'ExternalResultId'),
 (N'dbo.PatientResult',N'ReceivedAtUtc'),
 (N'dbo.PatientResult',N'Abnormality'),
 (N'dbo.PatientResult',N'PreviousResultUid'),
 (N'dbo.PatientResult',N'EnteredInErrorAtUtc'),
 (N'dbo.PatientResult',N'EnteredInErrorBy'),
 (N'dbo.PatientResult',N'EnteredInErrorReason');
DECLARE @Omitted table(ObjectName sysname PRIMARY KEY);
INSERT @Omitted VALUES
 (N'dbo.ApplicationUser_Provision'),
 (N'dbo.ClinicalDataMigration_AddIssue'),
 (N'dbo.ClinicalDataMigration_BeginValidation'),
 (N'dbo.ClinicalDataMigration_CompleteValidation'),
 (N'dbo.ClinicalDataMigration_FindPatientMatch'),
 (N'dbo.ClinicalDataMigration_GetBatch'),
 (N'dbo.ClinicalDataMigration_ImportValidatedBatch'),
 (N'dbo.ClinicalDataMigration_ListIssues'),
 (N'dbo.ClinicalDataMigration_StagePatient'),
 (N'dbo.ClinicalDataMigration_StageProblem'),
 (N'dbo.ClinicProfile_Save'),
 (N'dbo.DocumentTemplate_Create'),
 (N'dbo.DocumentTemplate_SetActive'),
 (N'dbo.DocumentTemplate_Update'),
 (N'dbo.Provider_Create'),
 (N'dbo.Provider_EligibleApplicationUsers_List'),
 (N'dbo.Provider_LinkApplicationUser'),
 (N'dbo.Provider_SetActive'),
 (N'dbo.Provider_UnlinkApplicationUser'),
 (N'dbo.Provider_Update');
IF EXISTS(SELECT 1 FROM @Exec WHERE OBJECT_ID(ObjectName,N'P') IS NULL)
 OR EXISTS(SELECT 1 FROM @Omitted WHERE OBJECT_ID(ObjectName,N'P') IS NULL)
 OR EXISTS(SELECT 1 FROM @Read WHERE COL_LENGTH(ObjectName,ColumnName) IS NULL)
 THROW 53706,'Expected object/column missing; review rather than partially grant.',1;
-- Reject unexpected pre-existing positive user permissions; never silently revoke them.
IF EXISTS(SELECT 1 FROM sys.database_permissions p
 WHERE p.grantee_principal_id IN(USER_ID(N'microemr_dev'),DATABASE_PRINCIPAL_ID(N'public'))
 AND (p.class<>1 OR OBJECTPROPERTY(p.major_id,'IsMSShipped')=0)
 AND p.state IN('G','W') AND
 (p.state='W' OR NOT
 ((p.class=0 AND p.permission_name IN
 (N'CONNECT',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION'))
 OR (p.class=1 AND p.permission_name=N'EXECUTE' AND p.minor_id=0
 AND EXISTS(SELECT 1 FROM @Exec e WHERE OBJECT_ID(e.ObjectName)=p.major_id))
 OR (p.class=1 AND p.permission_name=N'SELECT'
 AND EXISTS(SELECT 1 FROM @Read r WHERE OBJECT_ID(r.ObjectName)=p.major_id
 AND COLUMNPROPERTY(p.major_id,r.ColumnName,'ColumnId')=p.minor_id)))))
 THROW 53707,'Unexpected positive permissions; approve a separate delta.',1;
IF EXISTS(SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals u ON u.principal_id=p.grantee_principal_id
 WHERE u.name=N'microemr_dev' AND p.state IN('G','W')
 AND (p.state='W' OR p.permission_name<>N'CONNECT SQL'))
 THROW 53707,'Unexpected positive server permissions.',1;
DECLARE @AsRuntime bit=0;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @GrantExec nvarchar(max),@GrantRead nvarchar(max);
 SELECT @GrantExec=STRING_AGG(CONVERT(nvarchar(max),N'GRANT EXECUTE ON OBJECT::'
 +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))+N' TO [microemr_dev];'),NCHAR(10)) FROM @Exec;
 EXEC sys.sp_executesql @GrantExec;
 SELECT @GrantRead=STRING_AGG(CONVERT(nvarchar(max),N'GRANT SELECT ON OBJECT::'
 +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))
 +N' ('+QUOTENAME(ColumnName)+N') TO [microemr_dev];'),NCHAR(10)) FROM @Read;
 IF @GrantRead IS NOT NULL EXEC sys.sp_executesql @GrantRead;
 EXECUTE AS LOGIN=N'microemr_dev';
 SET @AsRuntime=1;
 IF SUSER_SNAME()<>N'microemr_dev' OR USER_NAME()<>N'microemr_dev'
 THROW 53708,'Wrong effective verification principal.',1;
 IF EXISTS(SELECT 1 FROM @Exec WHERE COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','EXECUTE'),-1)<>1)
 OR EXISTS(SELECT 1 FROM @Read WHERE COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','SELECT',ColumnName,'COLUMN'),-1)<>1)
 THROW 53709,'Required effective permissions missing.',1;
 IF EXISTS(SELECT 1 FROM @Omitted WHERE COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','EXECUTE'),-1)<>0)
 THROW 53710,'Excluded/unresolved direct execution is available.',1;
 IF EXISTS(SELECT 1 FROM(VALUES(N'dbo.PatientPrescriptionPrivateNote'),(N'dbo.PatientPrescriptionPrivateNoteRevision')) v(ObjectName)
 WHERE COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','SELECT'),-1)<>0
 OR COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','INSERT'),-1)<>0
 OR COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','UPDATE'),-1)<>0
 OR COALESCE(HAS_PERMS_BY_NAME(ObjectName,'OBJECT','DELETE'),-1)<>0)
 OR COALESCE(HAS_PERMS_BY_NAME(N'dbo.PatientPrescriptionPrivateNote_Write','OBJECT','EXECUTE'),-1)<>0
 THROW 53711,'Protected direct permissions are not denied.',1;
 IF COALESCE(IS_SRVROLEMEMBER('sysadmin'),-1)<>0 OR COALESCE(IS_MEMBER('db_owner'),-1)<>0
 OR COALESCE(IS_MEMBER('db_ddladmin'),-1)<>0 OR COALESCE(IS_MEMBER('db_datareader'),-1)<>0
 OR COALESCE(IS_MEMBER('db_datawriter'),-1)<>0
 OR EXISTS(SELECT 1 FROM sys.fn_my_permissions(NULL,'DATABASE') WHERE permission_name NOT IN
 (N'CONNECT',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION'))
 THROW 53712,'Unexpected effective database privileges.',1;
 REVERT;
 SET @AsRuntime=0;
 COMMIT;
 SELECT DB_NAME() AS DatabaseContext,(SELECT COUNT(*) FROM @Exec) AS RequiredExecuteRights,
 (SELECT COUNT(*) FROM @Read) AS RequiredColumnRights,N'Required-only permission batch committed' AS Result;
END TRY
BEGIN CATCH
 IF @AsRuntime=1 REVERT;
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
GO
```

## 8. Approval readiness and proposed Step 126C

Step 126C should first implement and review the bounded application-administration routing and separated deployment registration/configuration contract described above, under a new explicit implementation scope. Define/review the administration identity manifest and secret binding there; do not silently create accounts. Run focused tests then for normal versus administrative routing, tenant mismatch rejection, clinical/template authorization and denial audit. No build/test run is appropriate for this documentation-only step.

Controlled later execution sequence:

1. Review this exact normal candidate and target scope. **Separate explicit approval required** to execute its grants on P/T; none given by approval of the security approach.
2. Execute only the approved block in fail-fast SQLCMD mode. Verify effective required/excluded rights and protected table/helper denials as microemr_dev. Separately approve any additional read-only verification session, password-based login validation or evidence collection scope. Do not interpret reviewer sa access as runtime evidence.
3. Finish/review administration routing and its separately approved identity/grants before switching the shared API settings. Preserve all 35 capabilities and Auth integration. No schema migration or rerun of 0085/027 is part of this work.
4. **Separate explicit approval required** for exact development secret changes: API ConnectionStrings:PlatformDatabase and primary assigned tenant secret only, after readiness checks. Leave Auth and secondary tenant configuration unchanged. Isolate DatabaseTool deployment configuration and verify that the API has no deployment-credential fallback. Do not disclose secret values in approval/report.
5. **Separate explicit approval required** for affected development service restart and operational test/fixture scope. Restart only affected processes; verify actual identities and both normal/admin routes, then focused authoring/document/clinical operations and expected denials. An approved rollback should restore reviewed connection configuration if needed, not auto-revoke grants/drop users or rewrite clinical history.
6. Execute PC07.01 V1-V3 only under separately approved fictional accounts/fixtures and clinical/audit/output operations. Preserve existing Step 121/124B test/build and migration evidence without rerunning it for this review.

The plan above is not authorization for any operational action. No permission-classification question remains for the user. The remaining gate is implementation and exact operational approval, not guessing missing SQL grants. A whole-application readiness claim also requires the separate Auth dependency/security review.

## 9. Checks and certification status

Focused source/cached-definition review and documentation-only manifest checks performed; final Git/whitespace review passed. Earlier reports/source/configuration preserved. No SQL connected/executed; no builds or automated application tests run.

PC07.01 remains **PARTIAL**, mandatory gap inventory **10 IDs / three packages**, DE09.019 **IMPLEMENTED - RUNTIME/VISUAL EVIDENCE PENDING**, V1-V3 **DEFERRED**. No permission verification, functional acceptance or visual PASS is claimed here.
