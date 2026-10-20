# Step 124A - DE09.019 deployment prerequisites and least-privilege runtime design

Date: 2026-10-20 (America/Toronto). **Planning only. PC07.01 PARTIAL; 10 mandatory IDs / three packages. DE09.019 IMPLEMENTED - DEPLOYMENT/VISUAL EVIDENCE PENDING.**

## 1. Checkpoint, decisions and scope

Branch `main`; HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry working tree: only untracked Steps 122 and 123 reports. Both and AGENTS.md preserved. Only this report added. Initial 124A inspection made no database connection. The resumed 124A review performed only the read-only account metadata checks recorded below. No mutation, migration, grant, fixture, configuration edit, restart, build, suite rerun, staging, commit, merge or remote Git operation occurred. All SQL/deployment commands below are **prepared, not executed**.

User-confirmed decisions, not technical completion evidence:

- Tenant `MicroEMR_LocalDev_Fresh` is disposable development data; recreation would not lose important work. This closes the data-value confirmation, not permission to drop/recreate it.
- User will back up `MicroEMR_Platform` before 027. Backup verification remains pending.
- User subsequently confirmed that they already created microemr_dev. Account creation remains user-reported; the resumed review independently verified current login existence and matching users in both approved databases. No further grants, account changes or runtime switch are authorized by that confirmation.
- Prepare/review commands only; every changing operation requires separate explicit approval.

Only approved planning targets: `192.168.50.1,50013`; Platform `MicroEMR_Platform`; tenant `MicroEMR_LocalDev_Fresh`; TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`; TenantKey `local-dev-fresh`.

Reuse [Step 123 actual SELECT evidence](158-step123-pc07-01-private-notes-deployment-approval-review.md): both ONLINE, server `EC2AMAZ-TC26CQK\TELEMED52`, exact endpoint/catalog/identity agreement, 85 preceding migrations hash-valid, only 0085 pending, 027 absent and inspected prerequisites including 026 present. These are prior observations, not fresh pre-execution checks. No hashes repeated. [Step 122](157-step122-pc07-01-private-notes-deployment-visual-acceptance.md) retains detailed schema/postflight SQL and V1-V3 cards.

## 2. Connection configuration and actual runtime identity

API `Program.cs` uses `WebApplication.CreateBuilder(args)` without an inspected custom provider override. Application settings normally resolve, highest first: command-line, environment variables, Development user secrets, environment appsettings, base appsettings; host settings establish environment/content root. Launch profiles set Development but do not prove the launch method of a running process. Never print configuration debug views or process command lines.

- Platform repositories/health use `ConnectionStrings:PlatformDatabase` through `PlatformDatabaseConnection`; health runs SELECT 1, not a login probe.
- Tenant resolver runs `dbo.TenantDatabase_GetByTenantUid` in Platform. `ConfigurationTenantDatabaseSecretProvider` resolves `TenantDatabaseSecrets:<catalog SecretReference>`; confirmed reference `development:MicroEMR_LocalDev_Fresh`.
- `TenantSqlConnectionFactory` requires Active matching assignment, validates InitialCatalog, rejects AttachDbFilename and requires exactly one matching `TenantDatabaseIdentity` row. It does not independently pin this task's IP: verify endpoint separately.
- Reuse Step 123's sanitized local configuration: Platform/tenant login `sa`. No password/full connection string displayed or changed. Configured identity is not evidence of a running connection.
- Actual read-only local process inspection found **no identifiable MicroEMR.Api.exe or dotnet-hosted MicroEMR.Api process** visible to this inspection. Only PID/name/path/time fields were eligible for output; command lines were not emitted. Effective running SQL identity remains **BLOCKED/unestablished**. Do not start/restart/reconfigure to obtain it here.

Prepared operator follow-up, NOT RUN, on the exact server in a Platform/tenant query window:

```sql
SELECT s.session_id,s.login_name,s.original_login_name,s.host_name,s.host_process_id,
 s.program_name,DB_NAME(s.database_id) AS DatabaseContext,
 c.client_net_address,c.local_net_address,c.local_tcp_port
FROM sys.dm_exec_sessions s JOIN sys.dm_exec_connections c ON c.session_id=s.session_id
WHERE s.is_user_process=1 AND s.session_id<>@@SPID
AND s.database_id IN(DB_ID(N'MicroEMR_Platform'),DB_ID(N'MicroEMR_LocalDev_Fresh'));
```

Correlate PID/host/endpoint with a known API process. Generic SqlClient program names alone are insufficient. Pooling/idle context can obscure attribution; no session or insufficient metadata permissions means BLOCKED. Do not collect SQL/request text or grant diagnostics automatically. External/service-hosted runtime remains possible.

## 3. Least-privilege design and concrete blockers

### Resumed 124A account metadata evidence

Checkpoint reconfirmed: `main`, HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`; Steps 122, 123 and this Step 124A report are untracked. Prior reports preserved. The repeated attachment's suggested new name does not supersede the user's explicit existing-account correction. No new login-creation script is appropriate.

Actual SELECT-only results on `192.168.50.1,50013`, server `EC2AMAZ-TC26CQK\TELEMED52`:

| Check | Actual observation | Status |
| --- | --- | --- |
| Login | `microemr_dev`, SQL_LOGIN, enabled | PASS |
| Server membership/direct grants | No assigned server roles; direct CONNECT SQL only | PASS for inspected metadata |
| Platform mapping | `microemr_dev`, SQL_USER, INSTANCE authentication, matching login SID | PASS |
| Tenant mapping | Same name/type/authentication and matching SID | PASS |
| Database roles/direct grants | Neither mapped user has assigned database roles; direct CONNECT only in each DB | PASS for inspected metadata |
| Public application-object/schema grants | None found in either DB; public database metadata grants are VIEW ANY COLUMN ENCRYPTION KEY DEFINITION and VIEW ANY COLUMN MASTER KEY DEFINITION | PASS for inspected metadata |
| Ownership | Login owns neither approved database; mapped users own no schemas | PASS |
| Visible sessions as this login | None at inspection time | Observation only; does not establish running API identity |
| Authentication/runtime functionality | No login attempt as microemr_dev, API request or mutation procedure executed | DEFERRED |

These results establish existing mappings without independently authenticating as that login. No application-object EXECUTE/SELECT readiness is established: the inspected grants do not supply the proposed runtime operations. Keep runtime switch BLOCKED until a complete minimal grant diff and Auth boundary are reviewed, separately approved and verified. Do not recreate the already mapped users, infer least privilege solely from lack of fixed roles, or treat the administrative review connection's rights as runtime rights. No configuration or credentials changed; no password output.

### Intended design

User-created `microemr_dev` replaces the earlier proposed name. Resumed SELECT-only inspection verified its enabled SQL login on the exact approved server and matching instance-authenticated users in both approved databases. Current grants are detailed below; use by the running API and actual authentication as this login remain unverified. Do not recreate/reset the login or either mapped user. The following describes intended additional rights, not grants already present. Mapped users only in the two approved DBs; implicit public membership and explicit object grants, no fixed-role additions. No sysadmin, db_owner, db_ddladmin, db_datareader/db_datawriter, schema/database EXECUTE, ALTER ANY, unrestricted table DML or migration/provisioning rights. One login can technically serve both stores for this single-tenant development API, but compromises both sets of granted capabilities. Separate Platform/tenant logins reduce scope; existing separate settings support that without architecture changes.

**Complete existing-functionality design BLOCKED. Bounded SQL below is not a complete API replacement.**

1. `SqlIdentityUserLookup` directly reads `dbo.AspNetUsers` via AuthDatabase/fallback AuthServerConnection. API Program also registers `AdministrationIdentityDbContext` and `IdentityUserAdministration`: user creation/password reset use Identity/EF writes in a separate Auth store. JWT/OIDC token validation does not itself require this new SQL login, but administration/identity lookup has that dependency. Auth is outside the approved two-store modification scope. Smallest next investigation: separately authorize Auth metadata/administration-permission design, or explicitly retain a separately managed Auth connection with independently verified least privilege. No Auth mapping/repointing/EF migration is authorized.
2. Direct tenant SELECT paths exist for identity batching, scheduling/availability, result chronology, document-recipient audit checks and referral-document completeness checks. A procedure-only account would break those operations. Review exact projections before full grants; moving queries into stored procedures is a separate implementation task.
3. Many chart-category/admin procedures and composed names remain beyond the bounded DE09 allowlist. Finish the remaining caller/deployed-object/type/ownership inventory before C/D. Do not cover missing entries with schema-wide EXECUTE.
4. sa/sysadmin bypasses table/helper denials. Verify effective rights as the eventual non-owner runtime login. Unexpected inherited public/role rights require separate policy review, not silent broadening.

Same-owner static procedure ownership chains permit table/function/audit access without direct table grants. Private writes invoke the helper through draft procedures; public DENY on direct helper execution does not require granting the helper to runtime. Private read/history uses the audited read procedure. Retain 0085 public table DML denials and proposed user-level denials. Never grant protected-table columns (column grants can undermine intended table restrictions).

No TVP/SqlDbType.Structured/TypeName use, CREATE TYPE, runtime dynamic execution or cross-database SQL dependency was found in inspected permission paths/SQL assets. JSON/string/scalar inputs are used. The compatibility dynamic UPDATE in `db/patient_stored_procedures.sql` is deployment-time. Separate connections are not cross-database ownership chains. No TRUSTWORTHY/cross-database chaining changes proposed. Source inspection is not exhaustive deployed dependency certification; complete `sys.parameters`/`sys.types`/`sys.sql_modules`/`sys.sql_expression_dependencies` review for the remaining full allowlist before switch.

## 4. Object-level runtime matrix

P=MicroEMR_Platform; T=MicroEMR_LocalDev_Fresh. E=object EXECUTE; S=justified SELECT. Names are repository-confirmed. V1/V2 below provide read-only permission checks. HOLD rows are omitted from the bounded grant draft.

| Operation | Database and actual objects | Permission/path | Runtime decision/security reason | Verification |
| --- | --- | --- | --- | --- |
| Authentication/admin | OIDC Authority; separate Auth dbo.AspNetUsers; AdministrationIdentityDbContext | OIDC no SQL grant; direct Auth S and separate Identity writes | HOLD, no third-store authorization; preserve Auth boundary | Separate Auth design |
| Tenant selection | P dbo.Tenant_GetByKey, Tenant_GetByUid, TenantDatabase_GetByTenantUid | E through procedures | Include server-side mapping | V1 E=1 |
| Membership/permissions | P dbo.UserTenantMembership_GetActiveByUserId, UserTenantMembership_GetActiveByUserAndTenant, AccessProfile_GetEffective, UserPermissionAccess_Get | E | Include effective selected-tenant access | V1 E=1 |
| Entitlements | P dbo.PlatformAuthorization_GetVersionForUser, PlatformEntitlement_GetActiveForUser | E | Lookup only; omit assign/revoke | V1 E=1 |
| Tenant/clinical identity | T dbo.TenantDatabaseIdentity; ApplicationUser_GetByAuthSubjectId; ApplicationUser | S(TenantUid); E; column S for batching | Include first two; direct batching S HOLD for full-account review | V1/column S |
| Patient chart foundation | T dbo.Patient_Search, Patient_GetByUid, AuditLog_RecordPatientChartOpened, AuditLog_RecordStructuredRead | E | Include chart read plus mandatory audit paths | V1 E=1 |
| Draft create/update | T dbo.PatientPrescription_CreateDraft, PatientPrescription_UpdateDraft | E, chained helper/tables/audit | Include; author/status/version/app permissions remain | V1 E=1 |
| Finalization/output | T dbo.PatientPrescription_Finalize, PatientPrescription_SignAndFinalize, PatientPrescription_Cancel, PatientPrescription_GetArtifact, PatientPrescription_RecordOutputGeneration | E | Include; no direct artifact DML | V1 E=1 |
| Prescription list/detail | T dbo.PatientPrescription_GetByPatientUid, PatientPrescription_GetByUid | E | Include private-free shared responses | V1 E=1 |
| Private current/history | T dbo.PatientPrescriptionPrivateNote_Read (History argument) | E | Include audit-before-return path | V1 E=1; V2 |
| Private writes/revisions | T dbo.PatientPrescriptionPrivateNote_Write; PatientPrescriptionPrivateNote; PatientPrescriptionPrivateNoteRevision | Outer draft E only | Direct helper E and table S/I/U/D DENY; no direct revision path | V1 helper=0; V2 all 0 |
| Tenant denied audit | T dbo.PatientPrescriptionPrivateNote_RecordDenied | E | Include safe metadata audit | V1 E=1 |
| Platform denial audit | P dbo.PlatformSecurityAudit_RecordMissingPermission, PlatformSecurityAudit_RecordCrossPatientOwnership, PlatformSecurityAudit_RecordInvalidTenantMembership, PlatformSecurityAudit_RecordUnresolvedClinicalActor | E | Include trusted server audit entries, no table DML | V1 E=1 |
| Reports | T dbo.Appointment_ReportByStatus, AuditLog_RecordStructuredRead | E | Include representative existing report/export audit | V1 E=1 |
| Documents | T dbo.PatientDocument_GetByPatientUid, PatientDocument_GetByUid, PatientDocumentRecipient_List, PatientDocumentRecipient_ReplaceDraft, PatientDocument_PrepareSignConsultation, PatientDocument_CommitSignConsultation | E | Include representative read/recipient/sign paths; remaining create/edit/template list HOLD | V1 E=1 |
| Document output | T dbo.ClinicalOutputArtifact_Create, ClinicalOutputArtifact_GetFinalBySource, ClinicalOutputArtifact_RecordFailure | E | Include stored artifact operations | V1 E=1 |
| Direct document/referral checks | T dbo.PatientDocument, Patient, AuditLog; PatientReferralDocument, PatientReferral | Bounded direct S | HOLD pending complete projection review; no snapshot-content exposure | Column checks |
| Scheduling/result chronology | T dbo.ScheduleAppointment, ScheduleResource, SchedulingBlockedTime, Patient, PatientEncounter, PatientResult, ApplicationUser | Direct S | HOLD; exact projections/predicates, r.* requirement below | Column/table checks |
| Access management | P dbo.AccessProfile_List/Get/GetUserAssignments/AssignUser/ReplacePermissions; UserPermissionOverride_Set; PlatformMembership_Activate/Deactivate/ReplaceRoles/CreateWithInitialRole | E on each exact named procedure | HOLD pending full API-admin list and separate clinical grants | V1 if approved |
| Provisioning/migrations | T DDL/ledger; P dbo.TenantDatabase_ProvisioningStarted/Completed/Failed | Deployment rights/status E | No runtime grant; separate deployment principal | V3 |

Access management shorthand expands exactly to dbo.AccessProfile_List, dbo.AccessProfile_Get, dbo.AccessProfile_GetUserAssignments, dbo.AccessProfile_AssignUser, dbo.AccessProfile_ReplacePermissions, dbo.PlatformMembership_Activate, dbo.PlatformMembership_Deactivate, dbo.PlatformMembership_ReplaceRoles and dbo.PlatformMembership_CreateWithInitialRole.

Direct-read requirements, not applied grants: ApplicationUser(UserId,UserUid,Username,DisplayName,IsActive,AuthSubjectId); document checks PatientDocument(PatientId,PatientDocumentUid,PatientUid,IsDeleted), Patient(PatientId,PatientUid,IsDeleted), AuditLog(PatientId,EntityName,ActionName,EntityId); completeness check PatientReferralDocument(ReferralUid), PatientReferral(ReferralUid,PatientUid). Do not grant table-wide AuditLog S (OldValue/NewValue unnecessary). SchedulingReadRepository requires appointment/resource/time/status/room/deletion fields, patient demographics and linked-encounter status. PatientResultRepository.ListChronologyAsync uses r.* plus ApplicationUser display-name joins: requires all projected result columns or table S; no protected-note table joins. Complete all required columns before D; no db_datareader shortcut.

### Read-only account verification, NOT RUN

V1, actual runtime credentials in exact T; repeat object values/permission for matrix rows in their respective DB:

```sql
SELECT @@SERVERNAME AS ServerIdentity,DB_NAME() AS DatabaseContext,
 ORIGINAL_LOGIN() AS SqlLogin,USER_NAME() AS DatabaseUser;
SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','EXECUTE') AS CanExecute
FROM (VALUES(N'dbo.PatientPrescription_CreateDraft'),(N'dbo.PatientPrescription_UpdateDraft'),
 (N'dbo.PatientPrescriptionPrivateNote_Read'),(N'dbo.PatientPrescriptionPrivateNote_RecordDenied'),
 (N'dbo.PatientPrescriptionPrivateNote_Write')) v(ObjectName);
SELECT HAS_PERMS_BY_NAME(N'dbo.TenantDatabaseIdentity','OBJECT','SELECT',N'TenantUid','COLUMN') AS CanReadIdentity;
```

V2, T: expect eight zeros after 0085 and separately approved runtime grants:

```sql
SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','SELECT') AS CanSelect,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','INSERT') AS CanInsert,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','UPDATE') AS CanUpdate,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','DELETE') AS CanDelete
FROM (VALUES(N'dbo.PatientPrescriptionPrivateNote'),(N'dbo.PatientPrescriptionPrivateNoteRevision')) v(ObjectName);
```

V3, both mapped databases: expect elevated/data-role checks 0; no broad effective database permissions:

```sql
SELECT IS_SRVROLEMEMBER('sysadmin') AS IsSysadmin,IS_MEMBER('db_owner') AS IsOwner,
 IS_MEMBER('db_ddladmin') AS IsDdlAdmin,IS_MEMBER('db_datareader') AS IsReader,
 IS_MEMBER('db_datawriter') AS IsWriter;
SELECT permission_name FROM sys.fn_my_permissions(NULL,'DATABASE');
SELECT r.name AS DatabaseRole FROM sys.database_role_members rm
JOIN sys.database_principals r ON r.principal_id=rm.role_principal_id
WHERE rm.member_principal_id=USER_ID();
```

Entry procedures E=1, helper=0, identity column S=1. Missing objects/NULL visibility/unexpected grants are BLOCKED. Reviewer additionally inspects server roles/direct server permissions and object grants versus the final allowlist. No mutation procedure execution is necessary for permission-existence checks. Login/mapping metadata PASS is recorded below; authentication and full effective runtime-functionality verification remain DEFERRED.

## 5. Existing account mapping/grant draft - BLOCKED, NOT EXECUTED

Only included matrix rows are covered; no complete-functionality claim. Do not approve C/D from this partial draft. The earlier login-creation batch is superseded by the user's confirmation that microemr_dev already exists and has been removed. Do not create, reset or rotate that account as part of this correction. No password is needed in chat or documentation.

First inspect the existing login and mappings read-only on the exact approved server. The identity/login/role/mapping SELECTs below were executed using the existing authorized administrative connection, without impersonation or mutation. Additional database/public/ownership metadata was inspected as recorded below. Future mismatch or insufficient visibility is BLOCKED, not permission to create a login.

```sql
SELECT @@SERVERNAME AS ServerIdentity,DB_NAME() AS DatabaseContext;
SELECT name,type_desc,is_disabled FROM sys.server_principals WHERE name=N'microemr_dev';
SELECT r.name AS ServerRole FROM sys.server_role_members rm
JOIN sys.server_principals r ON r.principal_id=rm.role_principal_id
JOIN sys.server_principals m ON m.principal_id=rm.member_principal_id
WHERE m.name=N'microemr_dev';
-- Repeat in MicroEMR_Platform and MicroEMR_LocalDev_Fresh separately.
SELECT name,type_desc,authentication_type_desc,
 CASE WHEN sid=SUSER_SID(N'microemr_dev') THEN 1 ELSE 0 END AS MatchesLoginSid
FROM sys.database_principals
WHERE name=N'microemr_dev' OR sid=SUSER_SID(N'microemr_dev');
```

Review direct server permissions, inherited roles/public rights and mapped-user effective rights with section 4 before approving changes. Existing database users may have different names: identify them by SID; do not create duplicate mappings or replace them automatically. Both named mappings are now verified present. Batches P/T below preserve them and require a matching SID; they propose additional bounded grants only, not CREATE USER. The full allowlist remains incomplete, so no grant or runtime switch is authorized.
Batch P, context P only, after C approval/final full-allowlist review:

```sql
SET XACT_ABORT ON;
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_Platform'
 THROW 53401,'Unexpected deployment context.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev' AND sid=SUSER_SID(N'microemr_dev')) THROW 53404,'Expected existing mapping missing; review required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Objects table(Name sysname PRIMARY KEY);
 INSERT @Objects VALUES
 (N'Tenant_GetByKey'),(N'Tenant_GetByUid'),(N'TenantDatabase_GetByTenantUid'),
 (N'UserTenantMembership_GetActiveByUserId'),(N'UserTenantMembership_GetActiveByUserAndTenant'),
 (N'AccessProfile_GetEffective'),(N'UserPermissionAccess_Get'),
 (N'PlatformAuthorization_GetVersionForUser'),(N'PlatformEntitlement_GetActiveForUser'),
 (N'PlatformSecurityAudit_RecordMissingPermission'),(N'PlatformSecurityAudit_RecordCrossPatientOwnership'),
 (N'PlatformSecurityAudit_RecordInvalidTenantMembership'),(N'PlatformSecurityAudit_RecordUnresolvedClinicalActor');
 IF EXISTS(SELECT 1 FROM @Objects WHERE OBJECT_ID(N'dbo.'+Name,N'P') IS NULL)
  THROW 53405,'Required procedure missing.',1;
 DECLARE @GrantSql nvarchar(max);
 SELECT @GrantSql=STRING_AGG(CONVERT(nvarchar(max),N'GRANT EXECUTE ON OBJECT::dbo.'+QUOTENAME(Name)
  +N' TO [microemr_dev];'),NCHAR(10)) FROM @Objects;
 EXEC sys.sp_executesql @GrantSql;
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
```

Batch T, exact tenant context, same endpoint, after 0085:

```sql
SET XACT_ABORT ON;
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_LocalDev_Fresh'
 THROW 53401,'Unexpected deployment context.',1;
IF (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity)<>1 OR NOT EXISTS
 (SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2')
 THROW 53406,'Tenant identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev' AND sid=SUSER_SID(N'microemr_dev')) THROW 53404,'Expected existing mapping missing; review required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 GRANT SELECT ON OBJECT::dbo.TenantDatabaseIdentity (TenantUid) TO [microemr_dev];
 DECLARE @Objects table(Name sysname PRIMARY KEY);
 INSERT @Objects VALUES
 (N'ApplicationUser_GetByAuthSubjectId'),(N'Patient_Search'),(N'Patient_GetByUid'),
 (N'AuditLog_RecordPatientChartOpened'),(N'AuditLog_RecordStructuredRead'),
 (N'PatientPrescription_GetByPatientUid'),(N'PatientPrescription_GetByUid'),
 (N'PatientPrescription_CreateDraft'),(N'PatientPrescription_UpdateDraft'),
 (N'PatientPrescription_Finalize'),(N'PatientPrescription_SignAndFinalize'),
 (N'PatientPrescription_Cancel'),(N'PatientPrescription_GetArtifact'),(N'PatientPrescription_RecordOutputGeneration'),
 (N'PatientPrescriptionPrivateNote_Read'),(N'PatientPrescriptionPrivateNote_RecordDenied'),
 (N'Appointment_ReportByStatus'),(N'PatientDocument_GetByPatientUid'),(N'PatientDocument_GetByUid'),
 (N'PatientDocumentRecipient_List'),(N'PatientDocumentRecipient_ReplaceDraft'),
 (N'PatientDocument_PrepareSignConsultation'),(N'PatientDocument_CommitSignConsultation'),
 (N'ClinicalOutputArtifact_Create'),(N'ClinicalOutputArtifact_GetFinalBySource'),(N'ClinicalOutputArtifact_RecordFailure');
 IF EXISTS(SELECT 1 FROM @Objects WHERE OBJECT_ID(N'dbo.'+Name,N'P') IS NULL)
  THROW 53405,'Required procedure missing.',1;
 DECLARE @GrantSql nvarchar(max);
 SELECT @GrantSql=STRING_AGG(CONVERT(nvarchar(max),N'GRANT EXECUTE ON OBJECT::dbo.'+QUOTENAME(Name)
  +N' TO [microemr_dev];'),NCHAR(10)) FROM @Objects;
 EXEC sys.sp_executesql @GrantSql;
 DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.PatientPrescriptionPrivateNote TO [microemr_dev];
 DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.PatientPrescriptionPrivateNoteRevision TO [microemr_dev];
 DENY EXECUTE ON OBJECT::dbo.PatientPrescriptionPrivateNote_Write TO [microemr_dev];
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
```

No fixed-role memberships proposed. These grant batches are not an atomic two-store change. On failure stop/inspect; no automatic DROP/reset/reuse/retry or configuration change. Existing password/expiration policy is unverified; any rotation or policy change requires separate approval. Current partial grant list deliberately omits HOLD rows; account creation cannot be evidence that all application functionality works. Post-creation V1-V3 metadata checks above remain DEFERRED.

## 6. Exact 0085 runner command - PREPARED, NOT RUN

Entry point `src/MicroEMR.DatabaseTool/Program.cs`: `tenant provision --tenant-key`. Cwd `D:\MasterProgram_Ahmed\MicroEMR`. Project net10.0; UserSecretsId `MicroEMR.Api-local-development`; copies db SQL/JSON to output `database` assets.

**New blocker:** Debug DatabaseTool DLL exists, but packaged `database/tenant-clinical/migrations/0085-private-prescription-notes.sql` is absent. Do not run stale output. Future focused tool build/asset review required, not a full solution build or repeated Step 121 tests.

Exact future commands, after A approval/prerequisites:

```powershell
Set-Location -LiteralPath 'D:\MasterProgram_Ahmed\MicroEMR'
dotnet build .\src\MicroEMR.DatabaseTool\MicroEMR.DatabaseTool.csproj --no-restore -c Debug
# STOP on nonzero exit. Review fresh output/manifest and sole-pending status before continuing.
dotnet .\src\MicroEMR.DatabaseTool\bin\Debug\net10.0\MicroEMR.DatabaseTool.dll tenant provision --tenant-key local-dev-fresh
```

Second command mutates databases. No native only-migration/expected-TenantUid/expected-database flag exists; adding flags does not create enforcement. Verify exact catalog UID/key/endpoint/reference immediately before execution, freeze catalog/provisioning/config changes, and require only 0085 pending. No assign-database/create/activate command.

Tool configuration, highest last in code: optional cwd `src/MicroEMR.Auth/appsettings.json` -> environment variables -> Auth-local-development user secrets -> Program/API-local-development user secrets -> forced `TenantProvisioning:SqlAssetsPath=<AppContext.BaseDirectory>/database`. API appsettings/launchSettings are not automatically loaded; environment cannot override later secrets. Require effective `PlatformAdministration:Enabled=true`, nonblank approved `PlatformAdministration:ActorId`, deployment-principal Platform connection and exact tenant secret. Never print providers/debug values. No configuration edited here; missing values require separate approval, not runtime-credential reuse.

Deployment principal needs tenant DDL/constraints/permission management, metadata/ledger writes, Platform catalog reads and E on `TenantDatabase_ProvisioningStarted`/`Completed`/`Failed` (full names in matrix). Existing sa is technically capable, not the runtime design. Runner verifies database name/identity/hash, obtains `MicroEMR:TenantProvisioning:MicroEMR_LocalDev_Fresh` session lock and transactionally applies each pending migration with its ledger insertion. Fresh pre-execution inspection must establish **only 0085 pending** and no partial schema/hash mismatch; prior Step 123 results cannot replace it.

Expected migration log for 0085; `Provisioning result: Migrated; schema version: 1.0.0; applied migrations: 1.`; exit 0. Ledger one new 0085 row with Step 123 canonical hash, applied machine/time; 86 entries matching manifest. Platform status/time writes before/after/on failure explicitly included in A. Start-status failure occurs before main try. Apply failure rolls back tenant batches/ledger; failure after tenant commit can leave committed schema and Platform failure status. Inspect history/status before retry; never manual ledger repair/direct replay.

Read-only postflight: Step 122 section 5B exact tenant columns/keys/FKs/triggers/definitions/denials/hash; Step 123 Q2 Platform association/status. No account/table-denial or visual PASS inferred from deployment metadata.

## 7. Platform 027 transaction mechanism - STATIC REVIEW ONLY

Inspected `db/platform/027_private_prescription_note_permission.sql` has GO-separated DDL, no outer transaction, USE/:connect/:exit or top-level COMMIT. Transactions inside procedure definitions do not execute on CREATE OR ALTER. Proposed mechanism: **ODBC SQLCMD**, one connection, :On Error exit, -b/-V 11; GO separates batches without ending the outer transaction. [Microsoft SQLCMD commands](https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-commands) document error handling.

After B approval, save the following reviewed text as repository `.tmp\step124a-platform027-reviewed.sql`, re-review the referenced 027 asset and current prerequisites. It was not saved/executed/tested in 124A. Quiesce Platform access administration/provisioning; serializable snapshots take locks/tempdb space.

```sql
:On Error exit
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_NAME()<>N'MicroEMR_Platform' OR @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52'
 THROW 53411,'Unexpected Platform context.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.TenantDatabase WHERE TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2'
 AND DatabaseName=N'MicroEMR_LocalDev_Fresh' AND DatabaseServerKey=N'local-sql'
 AND SecretReference=N'development:MicroEMR_LocalDev_Fresh')
 THROW 53412,'Unexpected tenant assignment.',1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN
 (N'CK_UserPermissionOverride_Key',N'CK_PlatformSecurityAuditEvent_CapabilityPermission')
 AND is_disabled=0 AND is_not_trusted=0)<>2
 THROW 53413,'Platform constraints missing or untrusted.',1;
BEGIN TRANSACTION;
SELECT * INTO #BeforeProfiles FROM dbo.AccessProfile WITH(HOLDLOCK);
SELECT * INTO #BeforePermissions FROM dbo.AccessProfilePermission WITH(HOLDLOCK);
SELECT * INTO #BeforeAssignments FROM dbo.UserTenantAccessProfile WITH(HOLDLOCK);
SELECT * INTO #BeforeOverrides FROM dbo.UserPermissionOverride WITH(HOLDLOCK);
SELECT * INTO #BeforeMemberships FROM dbo.UserTenantMembership WITH(HOLDLOCK);
SELECT * INTO #BeforeRoles FROM dbo.UserTenantRole WITH(HOLDLOCK);
GO
:r "D:\MasterProgram_Ahmed\MicroEMR\db\platform\027_private_prescription_note_permission.sql"
GO
IF XACT_STATE()<>1 OR @@TRANCOUNT<>1 THROW 53414,'Deployment transaction unavailable.',1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN
 (N'CK_UserPermissionOverride_Key',N'CK_PlatformSecurityAuditEvent_CapabilityPermission')
 AND is_disabled=0 AND is_not_trusted=0)<>2
 THROW 53415,'Postflight constraints missing or untrusted.',1;
IF CHARINDEX(N'Prescriptions.ViewPrivateNotes',COALESCE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_UserPermissionOverride_Key')),N''))=0
 OR CHARINDEX(N'PrescriptionPrivateNoteView',COALESCE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_CapabilityPermission')),N''))=0
 OR CHARINDEX(N'PrescriptionPrivateNoteWrite',COALESCE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_CapabilityPermission')),N''))=0
 THROW 53416,'Expected registration missing.',1;
IF EXISTS(SELECT * FROM #BeforeProfiles EXCEPT SELECT * FROM dbo.AccessProfile)
 OR EXISTS(SELECT * FROM dbo.AccessProfile EXCEPT SELECT * FROM #BeforeProfiles)
 OR EXISTS(SELECT * FROM #BeforePermissions EXCEPT SELECT * FROM dbo.AccessProfilePermission)
 OR EXISTS(SELECT * FROM dbo.AccessProfilePermission EXCEPT SELECT * FROM #BeforePermissions)
 OR EXISTS(SELECT * FROM #BeforeAssignments EXCEPT SELECT * FROM dbo.UserTenantAccessProfile)
 OR EXISTS(SELECT * FROM dbo.UserTenantAccessProfile EXCEPT SELECT * FROM #BeforeAssignments)
 OR EXISTS(SELECT * FROM #BeforeOverrides EXCEPT SELECT * FROM dbo.UserPermissionOverride)
 OR EXISTS(SELECT * FROM dbo.UserPermissionOverride EXCEPT SELECT * FROM #BeforeOverrides)
 OR EXISTS(SELECT * FROM #BeforeMemberships EXCEPT SELECT * FROM dbo.UserTenantMembership)
 OR EXISTS(SELECT * FROM dbo.UserTenantMembership EXCEPT SELECT * FROM #BeforeMemberships)
 OR EXISTS(SELECT * FROM #BeforeRoles EXCEPT SELECT * FROM dbo.UserTenantRole)
 OR EXISTS(SELECT * FROM dbo.UserTenantRole EXCEPT SELECT * FROM #BeforeRoles)
 THROW 53417,'Existing assignments/configuration changed.',1;
COMMIT;
SELECT DB_NAME() AS DatabaseContext,N'027 committed; assignments unchanged' AS DeploymentResult;
SELECT name,is_disabled,is_not_trusted,definition FROM sys.check_constraints
WHERE name IN(N'CK_UserPermissionOverride_Key',N'CK_PlatformSecurityAuditEvent_CapabilityPermission');
GO
```

Exact future command, repository cwd, after B and wrapper review:

```powershell
& 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE' -S 'tcp:192.168.50.1,50013' -d 'MicroEMR_Platform' -U 'sa' -N -C -b -V 11 -r 1 -i '.tmp\step124a-platform027-reviewed.sql'
# Require exit code 0 and success row. Never supply password with -P or SQLCMD variables.
```

SQL auth through existing approved credential/secure prompt, no password literal. -N encrypts; -C retains the documented development trust choice, not production certificate validation evidence. No application configuration change. One session, no :connect/parallel client. Re-review if 027 changes.

Failure behavior: fail-fast exits before commit on qualifying errors; XACT_ABORT handles supported runtime failures, connection closure rolls back remaining open transaction, including compile-error paths. No TRY/CATCH across GO assumed. Cancellation/disconnection may leave rollback in progress; verify state. Disconnect during COMMIT is uncertain outcome: inspect definitions/constraints/assignments before retry. Never blindly replay committed 027 or auto-repair partial registration. Verified Platform recovery is still required.

Expected changes: two CHECK constraints and AccessProfile_ReplacePermissions, UserPermissionOverride_Set, PlatformSecurityAudit_RecordMissingPermission definitions. No automatic grants. Full snapshots of six existing profile/access/membership/role tables compared bidirectionally without printing users; no snapshot saved to disk. Postcommit compare complete definitions/026 preserved behavior with Step 122/123 SQL; wrapper keyword guards alone are not semantic certification. No live transaction/deployment PASS claimed.

## 8. Backup preparation and disposable tenant recovery

**Platform recovery BLOCKED pending user backup/verification evidence.** Exact server/Platform DB in SSMS Tasks -> Back Up: full, copy-only deployment recovery point, checksum enabled, new protected unique destination (no overwrite/append ambiguity). Confirm server service-account destination rights, space, backup permissions, version/recovery model, encryption/key/certificate recovery if applicable. File path is on SQL Server host, not this workstation; no path invented. No BACKUP/RESTORE issued.

Operator must record correct identity, times, backup-set identity/position, protected medium, checksum/health status and successful completion; then inspect header/perform RESTORE VERIFYONLY with checksum verification via separately authorized tooling. VERIFYONLY checks readability/completeness, not all contained data structures or a successful actual restore. Obtain controlled restore validation or documented recovery assurance; do not restore over shared Platform. [Microsoft VERIFYONLY documentation](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql) and [checksum guidance](https://learn.microsoft.com/en-nz/SQL/relational-databases/backup-restore/possible-media-errors-during-backup-and-restore-sql-server?view=sql-server-2016) support these limits.

Prepared read-only metadata, NOT RUN, same server (msdb metadata only):

```sql
SELECT TOP(10) bs.backup_set_id,bs.database_name,bs.type,bs.backup_start_date,bs.backup_finish_date,
 bs.is_copy_only,bs.has_backup_checksums,bs.is_damaged,bs.position,bs.first_lsn,bs.last_lsn,
 bmf.physical_device_name
FROM msdb.dbo.backupset bs JOIN msdb.dbo.backupmediafamily bmf ON bmf.media_set_id=bs.media_set_id
WHERE bs.database_name=N'MicroEMR_Platform' AND bs.type='D'
ORDER BY bs.backup_finish_date DESC;
SELECT name,recovery_model_desc,state_desc FROM sys.databases WHERE name=N'MicroEMR_Platform';
```

Require recent correct undamaged full backup and verifiable medium. Metadata/file existence alone is not PASS. Provide header/checksum/verification/recovery evidence and rollback window. Older Platform restore can lose later audit/access/status changes: quiesce/reconcile under separate approval. Platform memberships/profiles are not disposable.

Tenant data-value decision is user-confirmed; **technical recreation source-supported, not exercised**. Runner opens an existing DB; it does NOT CREATE DATABASE. Separately approved DBA must create an empty database at the same exact target with appropriate options/owner/collation and deployment connection; preserve catalog TenantUid/key/server/database/secret reference. Runner can then initialize metadata, matching identity and manifest. Retain/recover secrets and assignments; recreate approved database users/grants and fictional fixtures separately. No assign-database/new-tenant shortcut for mismatched identity; no drop/recreate/restore or historical-artifact deletion now.

## 9. Sequence and separate approvals

1. Establish attributable running identity; finalize full runtime allowlist/Auth boundary and existing-account permission review.
2. Retain disposable-tenant confirmation; establish technical recovery procedure without execution.
3. User completes/verifies Platform backup/recovery assurance.
4. Recheck exact endpoint/catalog/identity, fresh packaged assets, prerequisites and sole-pending 0085; approved deployment actor/configuration, no secret output.
5. Obtain A; focused tool artifact refresh/review, runner 0085, tenant/Platform status postflight.
6. Obtain B; one-connection 027 transaction, definitions/trusted constraints/unchanged assignments postflight.
7. Obtain C for final reviewed account; verify actual rights before any switch. Current partial draft does not close C.
8. Obtain D for named connection settings; G separately for required exact process restart.
9. Obtain E exact fictional recipients/keys and F exact fixture writes, then V1-V3 evidence.

No SQL dependency forces runtime-account changes before 0085/027; D/G after schema/registration prevents premature cutover. Complete security design is still prerequisite in this requested sequence. Provisioning principal stays separate from runtime.

| Approval | Exact scope, all at 192.168.50.1,50013 | Current readiness |
| --- | --- | --- |
| A | Scoped tool build/asset review then only pending 0085 via named runner on MicroEMR_LocalDev_Fresh, exact TenantUid; declared MicroEMR_Platform provisioning-status writes | PREPARED; fresh preflight/config/recovery needed; NOT APPROVED |
| B | Only Platform 027 via reviewed wrapper on MicroEMR_Platform | STATIC REVIEW; verified backup/recovery needed; NOT APPROVED |
| C | Existing microemr_dev: separately reviewed additional grants only in two named DBs; preserve login/users | Existing mappings/current grants inspected; BLOCKED: full allowlist/Auth design and separately approved grant diff; NOT APPROVED |
| D | Named Platform/tenant runtime connection settings to final approved identity | BLOCKED: actual runtime/source unestablished; NOT APPROVED |
| E | Individual clinical grants to explicitly identified fictional users/keys, exact tenant only | BLOCKED: recipients unselected; NOT APPROVED |
| F | Fictional patient/A-owned Draft/Finalized acceptance writes, no real records; Auth/Platform fixture prerequisites separately scoped | DEFERRED; NOT APPROVED |
| G | Necessary restart of exact identified nonproduction API/Web process | DEFERRED; NOT APPROVED |

Separate review requests, not combined authorization. Backup/restore/recreation, deployment-configuration edits or out-of-scope Auth writes are not silently included. No password requested in chat. Close concrete C/D design gaps before asking to execute an incomplete account plan.

## 10. Evidence, remaining blockers and Step 124B

Actual new evidence: source/configuration-path/CLI/SQL permission inspection; no identifiable local API process; stale tool output lacks 0085; documentation/whitespace review. An initial oversized documentation-write command hit Windows process argument-length limit before execution; corrected by applying the report directly. No database operation involved. Prepared scripts are statically reviewed, not live-validated.

Retain Steps 122-123 evidence; Step 121 **185 .NET PASS, 12 frontend PASS, TypeScript PASS, final build 0 warnings/errors**; Step 118 Generic Name/Step 116 Date Precision evidence; all deployment qualifications, remaining DE09 issues, Immunization/allergy qualifications and Family History M7/M8 DEFERRED. V1-V3 remain DEFERRED, exact navigation/fixtures in Step 122. PC07.01 PARTIAL, 10 mandatory IDs / three packages; DE09.019 IMPLEMENTED - DEPLOYMENT/VISUAL EVIDENCE PENDING.

Recommended **Step 124B: finish bounded full runtime/Auth permission inventory and existing-account permission review, identify actual runtime, collect Platform backup verification, refresh/review deployment-tool assets, then request A and B separately.** Obtain C/D/E/F/G only for fully specified reviewed actions. Do not bypass blockers with schema-wide grants, sa runtime reuse or stale output. **STOP: no changing operation executed.**
