# Step 126A - least-privilege runtime SQL grant review

Date: 2026-10-20 (America/Toronto). **REVIEW ONLY. No SQL executed or database connected in this step. Full-runtime approval BLOCKED.** PC07.01 PARTIAL; 10 mandatory IDs / three packages; DE09.019 IMPLEMENTED - RUNTIME/VISUAL EVIDENCE PENDING.

## 1. Git checkpoint, scope and reused evidence

Branch `main`; HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry status: reports 157-161 untracked, no tracked modifications. Earlier reports, AGENTS.md and SQL/application source preserved. Only this report added. No grants/denials/revokes, login/user/password/mapping changes, configuration changes, restart, fixture/clinical operations, migrations, build, tests, commit/merge or remote Git.

Reused [Step 125](161-step125-pc07-01-private-notes-runtime-and-visual-readiness.md) literal 228-procedure/80-column inventory and effective-token results; [Step 124B sections 12-13](160-step124b-pc07-01-runtime-grants-and-deployment-readiness.md) deployed 027/0085 postflight and retained module/dependency metadata. No inventory rescan, fresh permissions query or repeated migration/hash/test evidence. Source inspection focused on administration/import/template policy ambiguities and direct-read justification.

Exact review targets: `192.168.50.1,50013` / `EC2AMAZ-TC26CQK\TELEMED52`; `MicroEMR_Platform` (P) and `MicroEMR_LocalDev_Fresh` (T), TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`, existing login/users `microemr_dev`. No Auth or MicroEMR_Db permission scope. Existing enabled/matching users and CONNECT-only baseline are prior evidence; recheck before any later approved operation.

## 2. Decision and counts

| Original Step 125 proposal | REQUIRED | EXCLUDED | UNRESOLVED | Total |
| --- | ---: | ---: | ---: | ---: |
| P procedure EXECUTE | 12 | 15 | 0 | 27 |
| T procedure EXECUTE | 173 | 20 | 8 | 201 |
| T column SELECT | 80 | 0 | 0 | 80 |
| Total | **265** | **35** | **8** | **308** |

REQUIRED means demonstrated dependency of the restricted clinical runtime profile below, not a statement that existence implies permission. EXCLUDED means no grant in that restricted profile: these are administrative/bulk-import capabilities, even where existing administrative API callers are real. UNRESOLVED means mixed personal/clinic authoring scope requires an explicit security decision; no grant included. Every original permission appears exactly once in section 6. Additional helper/storage/deployment exclusions not present in the original 308 are listed separately and not added to these counts.

**Approval decision: BLOCKED for a full existing-API runtime replacement.** The required-only candidate is executable SQL for a restricted clinical profile, but existing administrative and mixed-authoring paths share the API's current P/T connections. This script alone cannot preserve those capabilities during a global connection switch. No SQL permission can distinguish a clinical versus administrative caller using that same database user. Do not silently disable existing screens or put extra admin grants back to obtain a green result.

Smallest next decision: specify whether a restricted clinical profile is acceptable for a separately approved constrained deployment, or separately scope a clinical/admin connection boundary while preserving authorized existing administration. Resolve the eight mixed-authoring capabilities before full-runtime approval. No source refactor or separate privileged connection was implemented. This is a scope/design gate, not evidence that all administrative features are unnecessary or insecure.

## 3. Focused security findings

### Required entry procedures and protected ownership chains

Patient chart/prescription/lifecycle, clinical categories, routine document creation/signing, scheduling, reports and mandatory audit paths are retained only where the existing repository calls a named entry procedure. Step 124B's cataloged dependencies have dbo effective ownership and no inspected dynamic SQL/cross-database/EXECUTE AS dependencies. Step 124B section 13 separately verified the new private-note helper/read/write module bodies in both tenants. No procedure was invoked here; signed/execution-context mechanisms are not assumed or introduced.

Direct private-note entry grants are only dbo.PatientPrescriptionPrivateNote_Read (current/history modes) and dbo.PatientPrescriptionPrivateNote_RecordDenied. Draft writes use dbo.PatientPrescription_CreateDraft/UpdateDraft -> same-owner internal Write helper -> protected state/revisions/audit. Public direct helper/table denial remains in place. No direct helper EXECUTE or private table SELECT/INSERT/UPDATE/DELETE, no protected-column grant. Retention triggers and finalized immutability remain unchanged; no signed artifact is read/rewritten as part of this review.

SQL EXECUTE is service-level capability, not a clinical grant: Patients.View + Prescriptions.ViewPrivateNotes, Active tenant membership/clinical actor, and Prescriptions.Prescribe + original-prescriber Draft/version checks remain enforced by the existing application/SQL contract. The protected Read procedure trusts the API's resolved actor/capability inputs; possession of the runtime credential is not a substitute for end-user authentication. Do not distribute it to clinicians as an interactive database account or claim SQL alone enforces Platform clinical permission keys.

### Excluded capabilities (35)

- **X1, 12 P access/user administration:** seven access-profile/override administration/read procedures plus membership activate/deactivate/create/list/role replacement. AccessProfilesController and TenantUserAdministrationService use these for Users.ManageAccess workflows. Keep AccessProfile_GetEffective and selected-user membership lookups for ordinary authorization; omit the administrative surface from this restricted profile.
- **X2, three P security-review procedures:** PlatformSecurityAuditController uses SecurityAuditView entitlement to search/detail privileged security evidence and record review. Required denial-audit writers remain; security-review administration is separate.
- **X3, nine T ClinicalDataMigration procedures:** ClinicalDataMigrationController requires Users.ManageAccess and stages/validates/imports external clinical batches. This is an existing bulk-import API feature, not a reason to grant schema migration rights or bulk ingestion to every restricted runtime.
- **X4, one T ApplicationUser_Provision:** used by TenantUserAdministrationService for explicit user onboarding; AuthenticatedClinicalUserAccessor only resolves an existing active actor and does not need this write to read a chart.
- **X5, one T ClinicProfile_Save:** clinic-settings management. ClinicProfile_Get remains REQUIRED because ClinicalPdfPreviewService, PatientReferralService and CppPrintService consume clinic data for clinical output.
- **X6, six T Provider administration procedures:** create/update/activate/link/unlink and eligible-user enumeration require Providers.Manage in ProvidersController. Provider_List/Get remain for existing provider read/selection dependencies.
- **X7, three legacy template mutations:** DocumentTemplatesController Create/Update/SetActive carry explicit ClinicAdministrator authorization. Clinical template retrieval remains.

Every exact excluded object/caller is retained in the classification table. Omitting them is deliberate privilege separation, not proof that current admin endpoints would work under microemr_dev. They would not; do not switch the whole API without resolving that architecture/scope decision.

### Unresolved mixed-authoring scope (8)

**U1:** DocumentTemplateAdmin_Create/Clone/UpdateMetadata/SetActive and DocumentTemplateVersion_CreateDraft/UpdateDraft/Publish/Retire. TemplateAdministrationController/DocumentTemplateVersionsController call services whose TemplateAuthorizationService permits clinic changes for administrators and personal changes for owners (with existing permission gates). Cached DocumentTemplateAdmin_Create SQL accepts Clinic/Personal scopes. An object EXECUTE grant covers the complete entry procedure, not just its personal branch.

Thus neither its “Admin” name nor an ordinary personal-template caller justifies an automatic blanket inclusion/exclusion. Required evidence/decision: whether personal template authoring must remain part of this restricted profile and how clinic administration is separated, or explicit approval of that mixed service capability under existing application authorization. No new wrapper/signature/EXECUTE AS policy or migration is proposed. These eight entries are omitted until resolved; authoring/version operations may be unavailable. Reading existing templates/versions and composing/signing clinical documents are retained.

**DocumentTemplate_GetAll is REQUIRED:** DocumentTemplatesController.GetTemplates and GetEncounterTemplates call PatientDocumentService.GetTemplatesAsync -> that procedure for ordinary selection, then apply existing scope/owner filters. Its name is not evidence it is admin-only. DocumentTemplateVersion_GetByUid/GetByTemplateUid serve existing rendering/selection and are also retained. No ownership/access rule is weakened.

## 4. Column review and Auth boundary

All 80 original columns have direct repository-query dependencies; none is a protected-note storage column. Table SELECT is not proposed. The exact column/caller/reason/security qualification is in section 6. Scoped direct queries execute in the single selected tenant DB; column grants do not supply per-patient row-level security. Preserve ITenantSqlConnectionFactory assignment/identity checks and existing API role/clinical access checks.

| Reason | Column group / calling query | Narrower existing path assessment and qualification |
| --- | --- | --- |
| C1 | TenantDatabaseIdentity.TenantUid; TenantSqlConnectionFactory.VerifyDatabaseIdentityAsync | Direct identity SELECT required before clinical operations; no ledger/init write |
| C2 | ApplicationUser six columns; ClinicalUserRepository.GetByAuthSubjectIdsAsync and PatientResultRepository chronology attribution | Single-subject SP exists but does not replace batched query as implemented; identifiers/display names are sensitive, no extra user columns |
| C3 | Patient, PatientDocument, AuditLog four-column subset; HasRecipientReplacementAsync plus scheduling patient fields | Existing query joins IDs/deletion/audit action; no stored equivalent used. AuditLog.OldValue/NewValue excluded |
| C4 | ScheduleAppointment, ScheduleResource, SchedulingBlockedTime, PatientEncounter; SchedulingReadRepository direct availability/history/events | Existing single-entry/history SPs do not supply the implemented full event/availability projections. ScheduleResource_GetActive exists and is retained; legacy fallback-only columns not added |
| C5 | PatientReferral/PatientReferralDocument identifiers; ReferralDocumentRepository completeness count | Separate linked-document SP used, but direct count is the existing completeness check; no clinical document content granted |
| C6 | PatientResult 30 current columns; ListChronologyAsync uses r.* | Existing PatientResult_GetByPatientUid filters LifecycleStatus=Current and cannot replace chronology containing corrected/entered-in-error history. Freeze explicit columns; future additions require review. Includes sensitive result/review text, but no prescription private-note join |

No refactor to views/procedures or semantic narrowing of historical records is authorized. These direct clinical reads are necessary for current callers; do not grant db_datareader or future table-wide access to avoid the explicit list.

Auth: reuse Steps 124B/125. API AuthDatabase and Auth AuthServerConnection are separate settings currently configured sa; Identity/OpenIddict direct EF operations and Auth startup migrations remain outside this two-DB grant plan. No Auth object appears in the 308 proposed grants. No Auth connection/query/mapping/grant. Separate Auth privilege/startup-migration design and actual runtime identity remain qualifications. No global least-privilege or whole-application readiness claim.

## 5. Required-only executable candidate - NOT EXECUTED, approval BLOCKED

The following is a **SQLCMD-mode script**, suitable only after separate explicit user approval of its restricted scope and closure of section 2 gates. It has explicit Platform/primary-tenant sections, no automatically executed migration/startup registration, no login/user/password/mapping operation, no role escalation, no blanket schema/table grant, no migration/provisioning permission and no unresolved/excluded grant.

For later review/execution, save this block as a manually invoked local artifact; no external script file is necessary in this review. Use one SQLCMD-mode connection to `192.168.50.1,50013` with the reviewed deployment principal; :On Error exit plus client fail-fast (-b/-V 11 with ODBC SQLCMD) prevents continuing to the next DB after error. Ordinary SSMS mode does not recognize its directives. No script run here, even with ROLLBACK.

Transactions are per DB: a later tenant failure does not undo an earlier committed Platform batch. Preflight rejects unknown positive permissions/roles/ownership rather than silently revoking them. Grants from approved literal manifests are declarative/repeatable; public denials remain unchanged. Current baseline must be rechecked at actual execution, not assumed from Step 125.

```sql
:On Error exit
-- REVIEW ONLY: full-runtime approval BLOCKED; do not execute without explicit scoped approval.
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
 (N'dbo.DocumentTemplateAdmin_Clone'),
 (N'dbo.DocumentTemplateAdmin_Create'),
 (N'dbo.DocumentTemplateAdmin_SetActive'),
 (N'dbo.DocumentTemplateAdmin_UpdateMetadata'),
 (N'dbo.DocumentTemplateVersion_CreateDraft'),
 (N'dbo.DocumentTemplateVersion_Publish'),
 (N'dbo.DocumentTemplateVersion_Retire'),
 (N'dbo.DocumentTemplateVersion_UpdateDraft'),
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

Static review only: exact required literal list, identifier quoting, role/context/mapping safeguards, transaction/catch/revert behavior and forbidden-list checks. This is not live SQL compilation, transaction testing or runtime regression evidence. A failure before commit rolls back that DB's grant batch where supported; connection loss/uncertain COMMIT requires metadata inspection before retry. Preserve pre-change permission metadata; recovery is a separately approved reverse delta, not automatic REVOKE/login drop/clinical deletion. Platform recovery/backup qualifications remain.

## 6. Complete original-inventory classification and traceability

Abbreviations: P/T resolve to the exact DBs in section 1. All objects schema dbo. E=object EXECUTE; S=specified column SELECT. Source paths are relative to src/MicroEMR.Infrastructure unless explicitly noted. Tables list every original permission once; the 80 column rows are individually enumerated, not replaced by table SELECT.

R1=existing clinical workflow direct entry; R2=tenant/membership/effective-permission/entitlement lookup; R3=mandatory read/denial/output audit. All REQUIRED entry rows retain existing application authorization, clinical actor/tenant/version/audit controls and the trusted-service qualification in section 3. X1-X7/U1/C1-C6 meanings above are the reason/security references for each row. Existence alone was not used to classify REQUIRED.

| DB | Exact object / column | Permission | Decision | Caller | Reason / qualification |
| --- | --- | --- | --- | --- | --- |
| P | `dbo.AccessProfile_AssignUser` | EXECUTE | EXCLUDED | Tenancy/SqlAccessProfileRepository.cs | X1 |
| P | `dbo.AccessProfile_Get` | EXECUTE | EXCLUDED | Tenancy/SqlAccessProfileRepository.cs | X1 |
| P | `dbo.AccessProfile_GetEffective` | EXECUTE | REQUIRED | Tenancy/SqlAccessProfileRepository.cs | R2 |
| P | `dbo.AccessProfile_GetUserAssignments` | EXECUTE | EXCLUDED | Tenancy/SqlAccessProfileRepository.cs | X1 |
| P | `dbo.AccessProfile_List` | EXECUTE | EXCLUDED | Tenancy/SqlAccessProfileRepository.cs | X1 |
| P | `dbo.AccessProfile_ReplacePermissions` | EXECUTE | EXCLUDED | Tenancy/SqlAccessProfileRepository.cs | X1 |
| P | `dbo.PlatformAudit_RecordSecurityAuditReview` | EXECUTE | EXCLUDED | SecurityAudit/SqlPlatformSecurityAuditReviewRepository.cs | X2 |
| P | `dbo.PlatformAuthorization_GetVersionForUser` | EXECUTE | REQUIRED | PlatformEntitlements/SqlPlatformEntitlementRepository.cs | R2 |
| P | `dbo.PlatformEntitlement_GetActiveForUser` | EXECUTE | REQUIRED | PlatformEntitlements/SqlPlatformEntitlementRepository.cs | R2 |
| P | `dbo.PlatformMembership_Activate` | EXECUTE | EXCLUDED | Tenancy/SqlTenantMembershipLifecycleRepository.cs | X1 |
| P | `dbo.PlatformMembership_CreateWithInitialRole` | EXECUTE | EXCLUDED | Tenancy/SqlTenantUserCreationRepository.cs | X1 |
| P | `dbo.PlatformMembership_Deactivate` | EXECUTE | EXCLUDED | Tenancy/SqlTenantMembershipLifecycleRepository.cs | X1 |
| P | `dbo.PlatformMembership_ListByTenant` | EXECUTE | EXCLUDED | Tenancy/SqlPlatformAdministrationServices.cs | X1 |
| P | `dbo.PlatformMembership_ReplaceRoles` | EXECUTE | EXCLUDED | Tenancy/SqlTenantRoleManagementRepository.cs | X1 |
| P | `dbo.PlatformSecurityAudit_GetByUid` | EXECUTE | EXCLUDED | SecurityAudit/SqlPlatformSecurityAuditReviewRepository.cs | X2 |
| P | `dbo.PlatformSecurityAudit_RecordCrossPatientOwnership` | EXECUTE | REQUIRED | SecurityAudit/SqlPlatformSecurityAuditRepository.cs | R2 |
| P | `dbo.PlatformSecurityAudit_RecordInvalidTenantMembership` | EXECUTE | REQUIRED | SecurityAudit/SqlPlatformSecurityAuditRepository.cs | R2 |
| P | `dbo.PlatformSecurityAudit_RecordMissingPermission` | EXECUTE | REQUIRED | SecurityAudit/SqlPlatformSecurityAuditRepository.cs | R2 |
| P | `dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor` | EXECUTE | REQUIRED | SecurityAudit/SqlPlatformSecurityAuditRepository.cs | R2 |
| P | `dbo.PlatformSecurityAudit_Search` | EXECUTE | EXCLUDED | SecurityAudit/SqlPlatformSecurityAuditReviewRepository.cs | X2 |
| P | `dbo.Tenant_GetByKey` | EXECUTE | REQUIRED | Tenancy/SqlTenantCatalog.cs | R2 |
| P | `dbo.Tenant_GetByUid` | EXECUTE | REQUIRED | Tenancy/SqlTenantCatalog.cs | R2 |
| P | `dbo.TenantDatabase_GetByTenantUid` | EXECUTE | REQUIRED | Tenancy/SqlTenantDatabaseResolver.cs | R2 |
| P | `dbo.UserPermissionAccess_Get` | EXECUTE | EXCLUDED | Tenancy/SqlAccessProfileRepository.cs | X1 |
| P | `dbo.UserPermissionOverride_Set` | EXECUTE | EXCLUDED | Tenancy/SqlAccessProfileRepository.cs | X1 |
| P | `dbo.UserTenantMembership_GetActiveByUserAndTenant` | EXECUTE | REQUIRED | Tenancy/SqlUserTenantMembershipRepository.cs | R2 |
| P | `dbo.UserTenantMembership_GetActiveByUserId` | EXECUTE | REQUIRED | Tenancy/SqlUserTenantMembershipRepository.cs | R2 |
| T | `dbo.ApplicationUser_GetByAuthSubjectId` | EXECUTE | REQUIRED | ClinicalUsers/ClinicalUserRepository.cs | R1 |
| T | `dbo.ApplicationUser_Provision` | EXECUTE | EXCLUDED | ClinicalUsers/ClinicalUserRepository.cs | X4 |
| T | `dbo.Appointment_ReportByStatus` | EXECUTE | REQUIRED | Reporting/AppointmentStatusReportRepository.cs | R1 |
| T | `dbo.AppointmentHistory_GetByAppointmentUid` | EXECUTE | REQUIRED | Scheduling/SchedulingReadRepository.cs | R1 |
| T | `dbo.AuditLog_RecordPatientChartOpened` | EXECUTE | REQUIRED | ReadAudit/ReadAuditRepository.cs | R3 |
| T | `dbo.AuditLog_RecordStructuredRead` | EXECUTE | REQUIRED | ReadAudit/ReadAuditRepository.cs | R3 |
| T | `dbo.CdsAlert_Acknowledge` | EXECUTE | REQUIRED | Cds/CdsRepository.cs | R1 |
| T | `dbo.CdsAlert_Dismiss` | EXECUTE | REQUIRED | Cds/CdsRepository.cs | R1 |
| T | `dbo.CdsAlert_List` | EXECUTE | REQUIRED | Cds/CdsRepository.cs | R1 |
| T | `dbo.CdsAlert_PatientExists` | EXECUTE | REQUIRED | Cds/CdsRepository.cs | R1 |
| T | `dbo.CdsAlert_RecordFinding` | EXECUTE | REQUIRED | Cds/CdsRepository.cs | R1 |
| T | `dbo.CdsAlert_ResolveRuleFindings` | EXECUTE | REQUIRED | Cds/CdsRepository.cs | R1 |
| T | `dbo.CdsAlertHistory_List` | EXECUTE | REQUIRED | Cds/CdsRepository.cs | R1 |
| T | `dbo.ChronicDiseaseEnrollment_Create` | EXECUTE | REQUIRED | Cdm/CdmEnrollmentRepository.cs | R1 |
| T | `dbo.ChronicDiseaseEnrollment_GetByUid` | EXECUTE | REQUIRED | Cdm/CdmEnrollmentRepository.cs | R1 |
| T | `dbo.ChronicDiseaseEnrollment_Inactivate` | EXECUTE | REQUIRED | Cdm/CdmEnrollmentRepository.cs | R1 |
| T | `dbo.ChronicDiseaseEnrollment_ListByPatient` | EXECUTE | REQUIRED | Cdm/CdmEnrollmentRepository.cs | R1 |
| T | `dbo.ClinicalDataMigration_AddIssue` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_BeginValidation` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_CompleteValidation` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_FindPatientMatch` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_GetBatch` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_ImportValidatedBatch` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_ListIssues` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_StagePatient` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalDataMigration_StageProblem` | EXECUTE | EXCLUDED | ClinicalDataMigration/ClinicalDataMigrationRepository.cs | X3 |
| T | `dbo.ClinicalOutputArtifact_Create` | EXECUTE | REQUIRED | ClinicalOutput/ClinicalOutputArtifactRepository.cs | R1 |
| T | `dbo.ClinicalOutputArtifact_GetFinalBySource` | EXECUTE | REQUIRED | ClinicalOutput/ClinicalOutputArtifactRepository.cs | R1 |
| T | `dbo.ClinicalOutputArtifact_RecordFailure` | EXECUTE | REQUIRED | ClinicalOutput/ClinicalOutputArtifactRepository.cs | R3 |
| T | `dbo.ClinicProfile_Get` | EXECUTE | REQUIRED | ClinicConfiguration/ClinicProfileRepository.cs | R1 |
| T | `dbo.ClinicProfile_Save` | EXECUTE | EXCLUDED | ClinicConfiguration/ClinicProfileRepository.cs | X5 |
| T | `dbo.CppDisplayPreference_Get` | EXECUTE | REQUIRED | PatientCpp/CppDisplayPreferencesRepository.cs | R1 |
| T | `dbo.CppDisplayPreference_Save` | EXECUTE | REQUIRED | PatientCpp/CppDisplayPreferencesRepository.cs | R1 |
| T | `dbo.DocumentTemplate_Create` | EXECUTE | EXCLUDED | PatientDocuments/PatientDocumentRepository.cs | X7 |
| T | `dbo.DocumentTemplate_GetActive` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.DocumentTemplate_GetAll` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.DocumentTemplate_GetByUid` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.DocumentTemplate_SetActive` | EXECUTE | EXCLUDED | PatientDocuments/PatientDocumentRepository.cs | X7 |
| T | `dbo.DocumentTemplate_Update` | EXECUTE | EXCLUDED | PatientDocuments/PatientDocumentRepository.cs | X7 |
| T | `dbo.DocumentTemplateAdmin_Clone` | EXECUTE | UNRESOLVED | PatientDocuments/TemplateAdministrationRepository.cs | U1 |
| T | `dbo.DocumentTemplateAdmin_Create` | EXECUTE | UNRESOLVED | PatientDocuments/TemplateAdministrationRepository.cs | U1 |
| T | `dbo.DocumentTemplateAdmin_SetActive` | EXECUTE | UNRESOLVED | PatientDocuments/TemplateAdministrationRepository.cs | U1 |
| T | `dbo.DocumentTemplateAdmin_UpdateMetadata` | EXECUTE | UNRESOLVED | PatientDocuments/TemplateAdministrationRepository.cs | U1 |
| T | `dbo.DocumentTemplateVersion_CreateDraft` | EXECUTE | UNRESOLVED | PatientDocuments/DocumentTemplateVersionRepository.cs | U1 |
| T | `dbo.DocumentTemplateVersion_GetByTemplateUid` | EXECUTE | REQUIRED | PatientDocuments/DocumentTemplateVersionRepository.cs | R1 |
| T | `dbo.DocumentTemplateVersion_GetByUid` | EXECUTE | REQUIRED | PatientDocuments/DocumentTemplateVersionRepository.cs | R1 |
| T | `dbo.DocumentTemplateVersion_Publish` | EXECUTE | UNRESOLVED | PatientDocuments/DocumentTemplateVersionRepository.cs | U1 |
| T | `dbo.DocumentTemplateVersion_Retire` | EXECUTE | UNRESOLVED | PatientDocuments/DocumentTemplateVersionRepository.cs | U1 |
| T | `dbo.DocumentTemplateVersion_UpdateDraft` | EXECUTE | UNRESOLVED | PatientDocuments/DocumentTemplateVersionRepository.cs | U1 |
| T | `dbo.Patient_Create` | EXECUTE | REQUIRED | Patients/PatientRepository.cs | R1 |
| T | `dbo.Patient_GetByUid` | EXECUTE | REQUIRED | Patients/PatientRepository.cs | R1 |
| T | `dbo.Patient_Search` | EXECUTE | REQUIRED | Patients/PatientRepository.cs | R1 |
| T | `dbo.Patient_UpdateDemographics` | EXECUTE | REQUIRED | Patients/PatientRepository.cs | R1 |
| T | `dbo.PatientAllergy_AssertNoKnownAllergies` | EXECUTE | REQUIRED | PatientAllergies/PatientAllergyRepository.cs | R1 |
| T | `dbo.PatientAllergy_Create` | EXECUTE | REQUIRED | PatientAllergies/PatientAllergyRepository.cs | R1 |
| T | `dbo.PatientAllergy_GetByUid` | EXECUTE | REQUIRED | PatientAllergies/PatientAllergyRepository.cs | R1 |
| T | `dbo.PatientAllergy_GetSnapshot` | EXECUTE | REQUIRED | PatientAllergies/PatientAllergyRepository.cs | R1 |
| T | `dbo.PatientAllergy_Resolve` | EXECUTE | REQUIRED | PatientAllergies/PatientAllergyRepository.cs | R1 |
| T | `dbo.PatientAllergy_RevokeNoKnownAllergies` | EXECUTE | REQUIRED | PatientAllergies/PatientAllergyRepository.cs | R1 |
| T | `dbo.PatientAllergy_Update` | EXECUTE | REQUIRED | PatientAllergies/PatientAllergyRepository.cs | R1 |
| T | `dbo.PatientChartAlert_Create` | EXECUTE | REQUIRED | PatientChartAlerts/PatientChartAlertRepository.cs | R1 |
| T | `dbo.PatientChartAlert_GetByPatientUid` | EXECUTE | REQUIRED | PatientChartAlerts/PatientChartAlertRepository.cs | R1 |
| T | `dbo.PatientChartAlert_GetByUid` | EXECUTE | REQUIRED | PatientChartAlerts/PatientChartAlertRepository.cs | R1 |
| T | `dbo.PatientChartAlert_Resolve` | EXECUTE | REQUIRED | PatientChartAlerts/PatientChartAlertRepository.cs | R1 |
| T | `dbo.PatientChartAlert_Update` | EXECUTE | REQUIRED | PatientChartAlerts/PatientChartAlertRepository.cs | R1 |
| T | `dbo.PatientClinicalHistory_Archive` | EXECUTE | REQUIRED | PatientClinicalHistory/PatientClinicalHistoryRepository.cs | R1 |
| T | `dbo.PatientClinicalHistory_Create` | EXECUTE | REQUIRED | PatientClinicalHistory/PatientClinicalHistoryRepository.cs | R1 |
| T | `dbo.PatientClinicalHistory_List` | EXECUTE | REQUIRED | PatientClinicalHistory/PatientClinicalHistoryRepository.cs | R1 |
| T | `dbo.PatientClinicalHistory_Update` | EXECUTE | REQUIRED | PatientClinicalHistory/PatientClinicalHistoryRepository.cs | R1 |
| T | `dbo.PatientDocument_CommitSignConsultation` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentSigningRepository.cs | R1 |
| T | `dbo.PatientDocument_Create` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.PatientDocument_GetByPatientUid` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.PatientDocument_GetByUid` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.PatientDocument_PrepareSignConsultation` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentSigningRepository.cs | R1 |
| T | `dbo.PatientDocument_UpdateDraft` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.PatientDocumentRecipient_List` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.PatientDocumentRecipient_ReplaceDraft` | EXECUTE | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | R1 |
| T | `dbo.PatientEncounter_Create` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_CreateStructured` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_GetByPatientUid` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_GetByUid` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_Sign` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_StartFromAppointment` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_UpdateNote` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_UpdateSoapNote` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounter_UpdateStructuredData` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounterAddendum_Create` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounterAddendum_GetByEncounterUid` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientEncounterContribution_Get` | EXECUTE | REQUIRED | PatientEncounters/EncounterContributionRepository.cs | R1 |
| T | `dbo.PatientEncounterContribution_Save` | EXECUTE | REQUIRED | PatientEncounters/EncounterContributionRepository.cs | R1 |
| T | `dbo.PatientEncounterDiagnoses_Get` | EXECUTE | REQUIRED | PatientEncounters/EncounterDiagnosisRepository.cs | R1 |
| T | `dbo.PatientEncounterDiagnoses_Save` | EXECUTE | REQUIRED | PatientEncounters/EncounterDiagnosisRepository.cs | R1 |
| T | `dbo.PatientEncounterHistory_GetByEncounterUid` | EXECUTE | REQUIRED | PatientEncounters/PatientEncounterRepository.cs | R1 |
| T | `dbo.PatientFamilyMedicalHistory_Archive` | EXECUTE | REQUIRED | PatientFamilyHistory/PatientFamilyHistoryRepository.cs | R1 |
| T | `dbo.PatientFamilyMedicalHistory_Create` | EXECUTE | REQUIRED | PatientFamilyHistory/PatientFamilyHistoryRepository.cs | R1 |
| T | `dbo.PatientFamilyMedicalHistory_Get` | EXECUTE | REQUIRED | PatientFamilyHistory/PatientFamilyHistoryRepository.cs | R1 |
| T | `dbo.PatientFamilyMedicalHistory_List` | EXECUTE | REQUIRED | PatientFamilyHistory/PatientFamilyHistoryRepository.cs | R1 |
| T | `dbo.PatientFamilyMedicalHistory_Update` | EXECUTE | REQUIRED | PatientFamilyHistory/PatientFamilyHistoryRepository.cs | R1 |
| T | `dbo.PatientFile_Archive` | EXECUTE | REQUIRED | PatientFiles/PatientFileRepository.cs | R1 |
| T | `dbo.PatientFile_Create` | EXECUTE | REQUIRED | PatientFiles/PatientFileRepository.cs | R1 |
| T | `dbo.PatientFile_GetByPatientUid` | EXECUTE | REQUIRED | PatientFiles/PatientFileRepository.cs | R1 |
| T | `dbo.PatientFile_GetByUid` | EXECUTE | REQUIRED | PatientFiles/PatientFileRepository.cs | R1 |
| T | `dbo.PatientFile_Restore` | EXECUTE | REQUIRED | PatientFiles/PatientFileRepository.cs | R1 |
| T | `dbo.PatientImmunization_Create` | EXECUTE | REQUIRED | PatientImmunizations/PatientImmunizationRepository.cs | R1 |
| T | `dbo.PatientImmunization_GetByUid` | EXECUTE | REQUIRED | PatientImmunizations/PatientImmunizationRepository.cs | R1 |
| T | `dbo.PatientImmunization_ListByPatient` | EXECUTE | REQUIRED | PatientImmunizations/PatientImmunizationRepository.cs | R1 |
| T | `dbo.PatientImmunization_MarkEnteredInError` | EXECUTE | REQUIRED | PatientImmunizations/PatientImmunizationRepository.cs | R1 |
| T | `dbo.PatientImmunization_Update` | EXECUTE | REQUIRED | PatientImmunizations/PatientImmunizationRepository.cs | R1 |
| T | `dbo.PatientMedication_Create` | EXECUTE | REQUIRED | PatientMedications/PatientMedicationRepository.cs | R1 |
| T | `dbo.PatientMedication_Discontinue` | EXECUTE | REQUIRED | PatientMedications/PatientMedicationRepository.cs | R1 |
| T | `dbo.PatientMedication_GetByPatientUid` | EXECUTE | REQUIRED | PatientMedications/PatientMedicationRepository.cs | R1 |
| T | `dbo.PatientMedication_GetByUid` | EXECUTE | REQUIRED | PatientMedications/PatientMedicationRepository.cs | R1 |
| T | `dbo.PatientMedication_Update` | EXECUTE | REQUIRED | PatientMedications/PatientMedicationRepository.cs | R1 |
| T | `dbo.PatientPrescription_Cancel` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescription_CreateDraft` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescription_Finalize` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescription_GetArtifact` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescription_GetByPatientUid` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescription_GetByUid` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescription_RecordOutputGeneration` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionOutputRepository.cs | R3 |
| T | `dbo.PatientPrescription_SignAndFinalize` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionOutputRepository.cs; PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescription_UpdateDraft` | EXECUTE | REQUIRED | PatientPrescriptions/PatientPrescriptionRepository.cs | R1 |
| T | `dbo.PatientPrescriptionPrivateNote_Read` | EXECUTE | REQUIRED | PatientPrescriptions/PrescriptionPrivateNoteRepository.cs | R1 |
| T | `dbo.PatientPrescriptionPrivateNote_RecordDenied` | EXECUTE | REQUIRED | PatientPrescriptions/PrescriptionPrivateNoteRepository.cs | R3 |
| T | `dbo.PatientProblem_Create` | EXECUTE | REQUIRED | PatientProblems/PatientProblemRepository.cs | R1 |
| T | `dbo.PatientProblem_GetByPatientUid` | EXECUTE | REQUIRED | PatientProblems/PatientProblemRepository.cs | R1 |
| T | `dbo.PatientProblem_GetByUid` | EXECUTE | REQUIRED | PatientProblems/PatientProblemRepository.cs | R1 |
| T | `dbo.PatientProblem_Resolve` | EXECUTE | REQUIRED | PatientProblems/PatientProblemRepository.cs | R1 |
| T | `dbo.PatientProblem_Update` | EXECUTE | REQUIRED | PatientProblems/PatientProblemRepository.cs | R1 |
| T | `dbo.PatientProviderRelationship_Create` | EXECUTE | REQUIRED | PatientCareTeam/PatientCareTeamRepository.cs | R1 |
| T | `dbo.PatientProviderRelationship_End` | EXECUTE | REQUIRED | PatientCareTeam/PatientCareTeamRepository.cs | R1 |
| T | `dbo.PatientProviderRelationship_List` | EXECUTE | REQUIRED | PatientCareTeam/PatientCareTeamRepository.cs | R1 |
| T | `dbo.PatientProviderRelationship_Update` | EXECUTE | REQUIRED | PatientCareTeam/PatientCareTeamRepository.cs | R1 |
| T | `dbo.PatientProviderRelationshipType_ListActive` | EXECUTE | REQUIRED | PatientCareTeam/PatientCareTeamRepository.cs | R1 |
| T | `dbo.PatientReferral_Close` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_Create` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_GetActiveProviders` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_GetByPatientUid` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_GetByUid` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_GetProvider` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_MarkResponseReceived` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_MarkSent` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_Send` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_SetFollowUpDue` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_SetResponseDocument` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferral_UpdateDraft` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferralArtifact_Get` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.cs | R1 |
| T | `dbo.PatientReferralClinicalSelection_Get` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.ClinicalSelections.cs | R1 |
| T | `dbo.PatientReferralClinicalSelection_ReplaceDraft` | EXECUTE | REQUIRED | PatientReferrals/PatientReferralRepository.ClinicalSelections.cs | R1 |
| T | `dbo.PatientReferralDocument_GetByReferralUid` | EXECUTE | REQUIRED | PatientReferrals/ReferralDocumentRepository.cs | R1 |
| T | `dbo.PatientReferralDocument_Link` | EXECUTE | REQUIRED | PatientReferrals/ReferralDocumentRepository.cs | R1 |
| T | `dbo.PatientReferralDocument_Unlink` | EXECUTE | REQUIRED | PatientReferrals/ReferralDocumentRepository.cs | R1 |
| T | `dbo.PatientResult_Correct` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_Create` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_GetByPatientUid` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_GetByUid` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_GetHistory` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_GetUnreviewed` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_GetUnreviewedCount` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_MarkEnteredInError` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_MarkReviewed` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientResult_Update` | EXECUTE | REQUIRED | PatientResults/PatientResultRepository.cs | R1 |
| T | `dbo.PatientRiskFactor_Archive` | EXECUTE | REQUIRED | PatientRiskFactors/PatientRiskFactorRepository.cs | R1 |
| T | `dbo.PatientRiskFactor_Create` | EXECUTE | REQUIRED | PatientRiskFactors/PatientRiskFactorRepository.cs | R1 |
| T | `dbo.PatientRiskFactor_Get` | EXECUTE | REQUIRED | PatientRiskFactors/PatientRiskFactorRepository.cs | R1 |
| T | `dbo.PatientRiskFactor_List` | EXECUTE | REQUIRED | PatientRiskFactors/PatientRiskFactorRepository.cs | R1 |
| T | `dbo.PatientRiskFactor_Update` | EXECUTE | REQUIRED | PatientRiskFactors/PatientRiskFactorRepository.cs | R1 |
| T | `dbo.PatientTask_Complete` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_Create` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_GetByPatientUid` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_GetByUid` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_GetOpenForDashboard` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_GetOverdue` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_GetOverdueCount` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_Reopen` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientTask_Update` | EXECUTE | REQUIRED | PatientTasks/PatientTaskRepository.cs | R1 |
| T | `dbo.PatientVital_Create` | EXECUTE | REQUIRED | PatientVitals/PatientVitalRepository.cs | R1 |
| T | `dbo.PatientVital_GetByPatientUid` | EXECUTE | REQUIRED | PatientVitals/PatientVitalRepository.cs | R1 |
| T | `dbo.PatientVital_GetByUid` | EXECUTE | REQUIRED | PatientVitals/PatientVitalRepository.cs | R1 |
| T | `dbo.PatientVital_Update` | EXECUTE | REQUIRED | PatientVitals/PatientVitalRepository.cs | R1 |
| T | `dbo.Provider_Create` | EXECUTE | EXCLUDED | Providers/ProviderAdministrationRepository.cs | X6 |
| T | `dbo.Provider_EligibleApplicationUsers_List` | EXECUTE | EXCLUDED | Providers/ProviderAdministrationRepository.cs | X6 |
| T | `dbo.Provider_Get` | EXECUTE | REQUIRED | Providers/ProviderAdministrationRepository.cs | R1 |
| T | `dbo.Provider_LinkApplicationUser` | EXECUTE | EXCLUDED | Providers/ProviderAdministrationRepository.cs | X6 |
| T | `dbo.Provider_List` | EXECUTE | REQUIRED | Providers/ProviderAdministrationRepository.cs | R1 |
| T | `dbo.Provider_SetActive` | EXECUTE | EXCLUDED | Providers/ProviderAdministrationRepository.cs | X6 |
| T | `dbo.Provider_UnlinkApplicationUser` | EXECUTE | EXCLUDED | Providers/ProviderAdministrationRepository.cs | X6 |
| T | `dbo.Provider_Update` | EXECUTE | EXCLUDED | Providers/ProviderAdministrationRepository.cs | X6 |
| T | `dbo.ScheduleAppointment_Cancel` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_CreateWithCriticalFlag` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_GetByUid` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_GetByUidWithCriticalFlag` | EXECUTE | REQUIRED | Scheduling/SchedulingReadRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_GetMonthSummary` | EXECUTE | REQUIRED | Scheduling/SchedulingReadRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_MarkArrived` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_Reschedule` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_UpdateStatus` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.ScheduleAppointment_UpdateWithCriticalFlag` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.ScheduleResource_GetActive` | EXECUTE | REQUIRED | Scheduling/SchedulingReadRepository.cs | R1 |
| T | `dbo.SchedulingBlockedTime_Cancel` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.SchedulingBlockedTime_Create` | EXECUTE | REQUIRED | Scheduling/SchedulingAppointmentRepository.cs | R1 |
| T | `dbo.SchedulingBlockedTime_GetForDateRange` | EXECUTE | REQUIRED | Scheduling/SchedulingReadRepository.cs | R1 |
| T | `dbo.TenantDatabaseIdentity.TenantUid` | SELECT | REQUIRED | Tenancy/TenantSqlConnectionFactory.cs | C1 |
| T | `dbo.ApplicationUser.UserId` | SELECT | REQUIRED | ClinicalUsers/ClinicalUserRepository.cs; PatientResults/PatientResultRepository.cs | C2 |
| T | `dbo.ApplicationUser.UserUid` | SELECT | REQUIRED | ClinicalUsers/ClinicalUserRepository.cs; PatientResults/PatientResultRepository.cs | C2 |
| T | `dbo.ApplicationUser.Username` | SELECT | REQUIRED | ClinicalUsers/ClinicalUserRepository.cs; PatientResults/PatientResultRepository.cs | C2 |
| T | `dbo.ApplicationUser.DisplayName` | SELECT | REQUIRED | ClinicalUsers/ClinicalUserRepository.cs; PatientResults/PatientResultRepository.cs | C2 |
| T | `dbo.ApplicationUser.IsActive` | SELECT | REQUIRED | ClinicalUsers/ClinicalUserRepository.cs; PatientResults/PatientResultRepository.cs | C2 |
| T | `dbo.ApplicationUser.AuthSubjectId` | SELECT | REQUIRED | ClinicalUsers/ClinicalUserRepository.cs; PatientResults/PatientResultRepository.cs | C2 |
| T | `dbo.AuditLog.PatientId` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.AuditLog.EntityName` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.AuditLog.ActionName` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.AuditLog.EntityId` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.Patient.PatientId` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.PatientUid` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.IsDeleted` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.LastName` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.FirstName` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.HealthCardNumber` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.DateOfBirth` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.GenderIdentity` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.Patient.ChartNumber` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs; Scheduling/SchedulingReadRepository.cs | C3 |
| T | `dbo.PatientDocument.PatientId` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.PatientDocument.PatientDocumentUid` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.PatientDocument.PatientUid` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.PatientDocument.IsDeleted` | SELECT | REQUIRED | PatientDocuments/PatientDocumentRepository.cs | C3 |
| T | `dbo.PatientEncounter.AppointmentUid` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.PatientEncounter.EncounterUid` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.PatientEncounter.EncounterStatus` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.PatientReferral.ReferralUid` | SELECT | REQUIRED | PatientReferrals/ReferralDocumentRepository.cs | C5 |
| T | `dbo.PatientReferral.PatientUid` | SELECT | REQUIRED | PatientReferrals/ReferralDocumentRepository.cs | C5 |
| T | `dbo.PatientReferralDocument.ReferralUid` | SELECT | REQUIRED | PatientReferrals/ReferralDocumentRepository.cs | C5 |
| T | `dbo.ScheduleAppointment.StartDateTimeUtc` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.EndDateTimeUtc` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.PrimaryResourceId` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.RoomResourceId` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.IsDeleted` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.AppointmentStatus` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.AppointmentUid` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.PatientUid` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.AppointmentType` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.Reason` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.ScheduleAppointmentId` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.IsCritical` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleAppointment.IsAdHoc` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleResource.ResourceId` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleResource.ResourceUid` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.ScheduleResource.DisplayName` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.SchedulingBlockedTime.StartDateTimeUtc` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.SchedulingBlockedTime.EndDateTimeUtc` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.SchedulingBlockedTime.IsActive` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.SchedulingBlockedTime.ResourceUid` | SELECT | REQUIRED | Scheduling/SchedulingReadRepository.cs | C4 |
| T | `dbo.PatientResult.PatientResultId` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.PatientResultUid` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.PatientUid` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ResultType` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ResultName` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ResultDate` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ResultSummary` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ResultValue` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ResultUnit` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ReferenceRange` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ResultStatus` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ReviewedAt` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ReviewedBy` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ReviewNote` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.CreatedAt` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.CreatedBy` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.UpdatedAt` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.UpdatedBy` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.RowVersion` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.LifecycleStatus` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.SourceType` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.SourceOrganization` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.SourceSystem` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ExternalResultId` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.ReceivedAtUtc` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.Abnormality` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.PreviousResultUid` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.EnteredInErrorAtUtc` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.EnteredInErrorBy` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |
| T | `dbo.PatientResult.EnteredInErrorReason` | SELECT | REQUIRED | PatientResults/PatientResultRepository.cs | C6 |

Additional excluded rights outside the 308-count denominator: direct PatientPrescriptionPrivateNote_Write EXECUTE; all protected current/revision table/column DML reads/writes; SchemaMigration/identity-initialization writes; TenantDatabase_ProvisioningStarted/Completed/Failed; DDL, CREATE/ALTER roles/logins/users, schema/database EXECUTE, fixed privileged/data roles, CLI catalog/entitlement mutations, Auth objects and secondary-tenant access. These are absent from the candidate. Ownership chaining supplies only the reviewed indirect private helper/table access, not direct permission.

## 7. Verification plan and interpretation

No live verification executed in 126A; reuse Step 125 effective-token findings as prior baseline (all proposed runtime rights missing, protected access denied, no privileged roles). The candidate contains post-grant **metadata-only checks under microemr_dev** before each commit: every REQUIRED E/S=1; all excluded/unresolved entry EXECUTE=0; both protected-table S/I/U/D=0; helper E=0; broad-role flags=0; no unexpected database-level rights. It uses temporary EXECUTE AS LOGIN/REVERT and never invokes clinical procedures. ORIGINAL_LOGIN remains reviewer; effective login/user must be microemr_dev. This is effective-token evidence, not password authentication or loaded API identity.

Independent read-only postflight, authorized reviewer for context/mappings/explicit metadata; direct microemr_dev login or safely impersonated token for effective checks:

```sql
SELECT @@SERVERNAME AS ServerIdentity,DB_NAME() AS DatabaseContext,
 ORIGINAL_LOGIN() AS Authenticator,SUSER_SNAME() AS EffectiveLogin,USER_NAME() AS DatabaseUser;
SELECT IS_SRVROLEMEMBER('sysadmin') AS IsSysadmin,IS_MEMBER('db_owner') AS IsOwner,
 IS_MEMBER('db_ddladmin') AS IsDdlAdmin,IS_MEMBER('db_datareader') AS IsReader,
 IS_MEMBER('db_datawriter') AS IsWriter;
SELECT permission_name FROM sys.fn_my_permissions(NULL,'DATABASE');
-- Primary tenant; repeat REQUIRED literal @Exec/@Read values from candidate for complete E/S comparison.
SELECT HAS_PERMS_BY_NAME(N'dbo.PatientPrescriptionPrivateNote_Read','OBJECT','EXECUTE') AS CanReadThroughEntry,
 HAS_PERMS_BY_NAME(N'dbo.PatientPrescriptionPrivateNote_Write','OBJECT','EXECUTE') AS CanExecuteHelper;
SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','SELECT') AS CanSelect,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','INSERT') AS CanInsert,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','UPDATE') AS CanUpdate,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','DELETE') AS CanDelete
FROM(VALUES(N'dbo.PatientPrescriptionPrivateNote'),(N'dbo.PatientPrescriptionPrivateNoteRevision')) v(ObjectName);
```

Expected post-approved subset: 12 P + 173 T entry E=1, 80 column S=1; 35 excluded + eight unresolved entries remain direct E=0; direct helper/table restrictions as above; no new fixed roles or broad rights. Missing metadata/NULL is BLOCKED, not PASS. Check exact object/column permission rows against the manifest, including absence of GRANT WITH GRANT OPTION and unexpected schema/table grants. Reviewer sa results are not runtime-denial evidence.

No procedure call, chart fetch, read-history endpoint, report generation, fixture or print action during permission-only checks (they can audit/write). Successful metadata checks do not establish full application functionality, clinical policies, OIDC identity, signed-artifact invariance or V1-V3. Later separately approved clinical acceptance remains necessary.

## 8. Approval readiness, risks and Step 126B

**BLOCKED for full runtime approval.** Candidate includes only 265 REQUIRED permissions, excludes 35 administrative/import capabilities and omits eight UNRESOLVED mixed-authoring permissions. No executable permission change is approved. Known omitted admin paths and unresolved personal/clinic authoring cannot be silently broken by a global P/T switch; separate Auth dependency and actual API identity remain unverified.

Recommended **Step 126B: decide restricted clinical/admin scope and resolve the eight mixed-authoring entries, then approve a concrete final grant profile separately from any configuration switch/restart.** If preserving all current API features is mandatory, separately approve a bounded administrative connection/capability design or explicitly revisit individual excluded capabilities; do not default back to all 308, a schema grant or sa. No implementation scope is expanded by this report.

After that decision, collect fresh context/principal/permission baseline and recovery evidence, obtain explicit grant approval, and perform metadata postflight as microemr_dev before any separately approved runtime transition. Clinical grants, fictional fixtures and V1-V3 writes remain separate approvals. No migration or 027 replay in the plan.

Preserve 0085 both-tenant and 027 metadata PASS, Step 121 185 .NET PASS / 12 frontend PASS / TypeScript PASS / final build zero warnings/errors, all prior deployment/Auth/backup qualifications, Generic Name/Date Precision evidence, unresolved DE09/Immunization/allergy and Family History M7/M8 deferred evidence. V1-V3 DEFERRED; PC07.01 PARTIAL, inventory 10 IDs / three packages.

Documentation checks: complete 308-row partition, candidate literal-manifest equality/no excluded/unresolved entries, no duplicate rights, no forbidden broad/account/helper grant and whitespace review. No database syntax/run/test claim. Earlier reports are retained evidence, not overwritten by this refinement. **STOP after 126A.**
