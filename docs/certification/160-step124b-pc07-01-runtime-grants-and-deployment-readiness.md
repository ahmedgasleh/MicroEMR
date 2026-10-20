# Step 124B - runtime grants and deployment readiness

Date: 2026-10-20 (America/Toronto). **Read-only review plus authorized local DatabaseTool asset refresh. No database modifications. PC07.01 PARTIAL; 10 mandatory IDs / three packages. DE09.019 IMPLEMENTED - DEPLOYMENT/VISUAL EVIDENCE PENDING.**

**Current follow-up: Platform 027 registration verified (section 12); tenant 0085 ledger/schema postflight verified in both user-named test tenants (section 13).** Earlier planning/pending observations are retained as historical evidence, superseded by these follow-ups for current deployment state. Codex performed no database modifications. Runtime grants/switch and V1-V3 remain pending.

## 1. Checkpoint and scope

Branch `main`; HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry working tree: untracked reports 157, 158 and 159 only. All preserved unchanged, including the corrected existing-account findings in [Step 124A](159-step124a-pc07-01-private-notes-deployment-prerequisite-closure.md). This step adds only this report as a reviewable repository file; ignored .tmp inventory/metadata and bin/obj build outputs are local supporting artifacts.

Exact targets: `192.168.50.1,50013`; `MicroEMR_Platform` (P), `MicroEMR_LocalDev_Fresh` (T); TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`; key `local-dev-fresh`. Only these two databases were connected. No Auth connection, clinical-content SELECT, mutation procedure, migration runner, GRANT/DENY/REVOKE, account/password/configuration change, backup/restore, fixture, restart, commit/merge or remote Git operation.

Reused [Step 123](158-step123-pc07-01-private-notes-deployment-approval-review.md) preceding migration hashes and [Step 124A](159-step124a-pc07-01-private-notes-deployment-prerequisite-closure.md) login/mapping evidence. No repeated hash analysis, specification analysis or Step 121 suites.

## 2. Existing account and effective-runtime qualification

Existing `microemr_dev`: Step 124A verified enabled SQL login, matching instance-authenticated users in P/T, CONNECT SQL/CONNECT, no assigned server/database roles, neither DB nor any schema owned, and no inspected direct/role/public application-object grants. Public encryption-key-definition metadata rights were recorded separately. Do not recreate login/users or change password.

Those account observations are retained evidence, not fresh authentication as that login. Actual running API login remains unestablished; no runtime session using microemr_dev was observed in Step 124A. Source/local configuration still resolves separate API P/T settings to sa; effective process overrides were not established. **Do not switch to microemr_dev before C grants and verification.** This step's metadata queries used the existing authorized administrative connection; its rights cannot prove runtime restrictions.

## 3. Completed bounded SQL inventory

Inventory boundary: existing API SQL operations in Infrastructure repositories/services, the API health/Identity paths and Auth's separate connection/code boundary. Source extraction was checked against deployed object/type/column/module/dependency metadata. Composed Allergy Create/Update and FamilyHistory/RiskFactor List/Get/Create/Update/Archive names were expanded from actual callers. CLI-only catalog/provisioning, entitlement assignment/revocation and ApplicationUser_SetAuthSubjectId were explicitly excluded. TenantUserAdministration consumes PlatformMembership_ListByTenant, not the entire CLI membership administration surface.

| Scope | Verified entry objects | Proposed permission | Classification |
| --- | --- | --- | --- |
| Platform tenant/catalog/membership/access/entitlement lookups, authorized access management and security-audit paths | 27 existing procedures, individually listed in P script below | Object EXECUTE only | A: existing runtime functionality |
| Tenant charts, clinical categories, prescribing/lifecycle, reports/documents/templates, clinic/provider management, existing clinical-data import endpoints and audits | 199 existing procedures, individually listed in T script | Object EXECUTE only | A |
| New private-note direct API entries | dbo.PatientPrescriptionPrivateNote_Read; dbo.PatientPrescriptionPrivateNote_RecordDenied | Object EXECUTE after 0085 | B: introduced by 0085 |
| Existing draft entry definitions extended by 0085 | dbo.PatientPrescription_CreateDraft; dbo.PatientPrescription_UpdateDraft | Existing EXECUTE retained; no extra helper grant | A permissions, B implementation |
| Direct-read paths | 80 explicit columns across 12 T tables, listed below | Column SELECT only | A |
| Internal private-note helper/revisions/current table | dbo.PatientPrescriptionPrivateNote_Write; dbo.PatientPrescriptionPrivateNoteRevision; dbo.PatientPrescriptionPrivateNote | No direct runtime content/helper access; explicit restrictions | B |
| DatabaseTool/migration operations | dbo.SchemaMigration, dbo.TenantDatabaseIdentity initialization, DDL; P dbo.TenantDatabase_ProvisioningStarted/Completed/Failed | Deployment principal only; no runtime grants | C: deployment only |
| CLI-only catalog/status/tenant-role mutations and entitlement assign/revoke; unrelated tenant databases; Auth mapping for this login | No runtime grant proposed | None | D: not required by the reviewed two-store API runtime |

The exact literal lists in section 5 are the object-level permission matrix: each @Exec row identifies database context, object, A/B class and required E=EXECUTE; each @Read row identifies exact table+column and required S=SELECT. Expected post-grant E/S=1 for these rows only, subject to application authorization. No broad-role exception is needed.

Deployed metadata checks: P 27/27 entry procedures exist. T 199/199 existing entries and all twelve read tables exist; only the two new private-note entries are absent, expected before 0085. The reachable cataloged dependency graphs (including internal tables/functions) have dbo effective ownership, no cross-server/cross-database references and no inspected runtime dynamic SQL/EXECUTE AS. Source checks also cover the new 0085 procedures, which are not yet deployed. No user-defined/table-type parameters were found in either approved database. System scalar/JSON inputs require no type grant.

Same-owner static ownership chains cover internal table/function/audit access. Do not grant their underlying write rights or internal helper EXECUTE. Named SQL-return-variable calls inspected in access management/scheduling/import are calls to sys.sp_getapplock, not dynamic SQL; their normal public-principal application-lock usage does not justify server/DDL rights. No TRUSTWORTHY/cross-database chaining change. Metadata dependency discovery is limited for dynamic/unresolved SQL; none was found in the reviewed runtime paths. No live invocation/behavior or adversarial permission test was performed.

### Direct SELECT justification and limits

| Existing source path | Exact read scope | Why needed |
| --- | --- | --- |
| TenantSqlConnectionFactory | TenantDatabaseIdentity.TenantUid | Match the selected tenant before repository operations |
| ClinicalUserRepository batching and result chronology | ApplicationUser UserId/UserUid/Username/DisplayName/IsActive/AuthSubjectId | Map active clinical identities and existing attribution |
| PatientDocumentRepository.HasRecipientReplacementAsync | PatientDocument identifiers/deletion; Patient identifiers/deletion; AuditLog PatientId/EntityName/ActionName/EntityId | Existing scoped recipient-change detection; no OldValue/NewValue SELECT |
| ReferralDocumentRepository completeness count | PatientReferralDocument.ReferralUid; PatientReferral.ReferralUid/PatientUid | Compare scoped linked-document count |
| SchedulingReadRepository | Explicit appointment/resource/time/status/patient-demographic/encounter columns in @Read | Existing history, events and availability queries |
| PatientResultRepository.ListChronologyAsync | All 30 current PatientResult columns, explicitly enumerated | Existing r.* projection requires these current columns; future schema additions require review, no future blanket table SELECT |

ScheduleResource_GetActive exists and gets EXECUTE. Its legacy direct-table fallback is not needed on this verified target; @Read grants only columns needed by the current direct joins. Do not silently broaden fallback rights. All direct-read columns were cross-checked to source projections/predicates and inspected schema; TenantDatabaseIdentity.TenantUid also has the prior exact identity-query evidence. No SELECT on either protected table. App authorization, selected-tenant connection checks, actor scoping and required read audits remain necessary: SQL column grants do not implement per-user clinical policy.

## 4. Auth dependency conclusion - separate boundary

Local configuration inspected without displaying password/full connection strings:

| Consumer/configuration source | Key | Configured DB/login |
| --- | --- | --- |
| API local secrets | ConnectionStrings:PlatformDatabase; TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh | Exact P/T endpoint, sa |
| API local secrets | ConnectionStrings:AuthDatabase | MicroEMR_Auth at same endpoint, sa |
| Auth local secrets | ConnectionStrings:AuthServerConnection | MicroEMR_Auth at same endpoint, sa |
| Auth local secrets | ConnectionStrings:PlatformDatabase | MicroEMR_Platform at same endpoint, sa |

These are separately configured connections using the same configured SQL login today, not proof of running identities. **No connection to MicroEMR_Auth was made.**

MicroEMR.Auth uses its own ApplicationDbContext/SQL connection with Identity and OpenIddict EF stores: direct table reads/writes for users/roles/claims/tokens and authorization/application/scope/token persistence. Its model also includes AppModules/AppPermissions/RolePermissions/AppMenuButtons. API identity lookup directly reads dbo.AspNetUsers; API create-user/password-reset uses AdministrationIdentityDbContext/UserManager writes through its separate AuthDatabase connection. These are not the tenant stored-procedure model.

Important bounded finding: Auth registers SeedData as a hosted service; SeedData calls Database.MigrateAsync and performs seed writes at startup. Starting/restarting Auth could therefore perform DDL/data writes. Do not restart it or grant DDL to microemr_dev. Separating that existing startup migration from an eventual least-privilege Auth identity requires a separately scoped implementation/deployment decision. No migrations or authentication behavior changed here.

Boundary recommendation for D review: use microemr_dev only for the approved API P/T settings after C; retain the separately configured API AuthDatabase/Auth AuthServerConnection outside that switch, and leave Auth's own Platform setting unchanged unless explicitly named. This resolves what the P/T grant script must cover; it does not certify the existing sa Auth configuration as least-privileged. A dedicated Auth identity and startup-migration separation remain a separate security task. No Auth grants, user mappings, secret changes or permission assumptions included in C.

## 5. Minimal proposed grants - NOT EXECUTED

Two independent batches, one explicit SSMS/SqlClient connection to each exact DB, using a separately approved deployment principal on `192.168.50.1,50013`. No login/user creation, password, fixed role, schema-wide EXECUTE, table DML or deployment rights. T batch deliberately requires 0085 first. P and T changes are not one atomic distributed deployment.

Existing full-account permission scope: 27 P EXECUTE, 201 T EXECUTE, 80 T column SELECT and explicit protected restrictions. This closes Step 124A's omitted category/direct-read list for the reviewed API; it is not permission to execute these changes. Clinical grants E are separate from runtime SQL grants C, including when an entry procedure manages user access. Existing server-side authorization/auditing must remain in effect.

All listed GRANT/DENY operations are declarative/repeatable, but do not remove unexpected prior excess rights. Before C, recheck login/SID/mappings/roles/ownership and compare grants; STOP if changed. No automatic REVOKE/reset/recreation. Restore an approved prior permission state only through a separately reviewed reverse delta if recovery is needed; do not drop protected data.

### P batch - MicroEMR_Platform

```sql
-- PREPARED ONLY. Separate approval C required. Existing mappings must be preserved.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_Platform'
 THROW 53501,'Unexpected server/database context.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev'
 AND type='S' AND sid=SUSER_SID(N'microemr_dev'))
 THROW 53502,'Expected existing mapping missing; stop for review.',1;
IF EXISTS(SELECT 1 FROM sys.server_role_members rm JOIN sys.server_principals p
 ON p.principal_id=rm.member_principal_id WHERE p.name=N'microemr_dev')
 OR EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=USER_ID(N'microemr_dev'))
 OR EXISTS(SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND owner_sid=SUSER_SID(N'microemr_dev'))
 THROW 53505,'Unexpected runtime role/ownership; review baseline.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.TenantDatabase WHERE TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2'
 AND DatabaseName=N'MicroEMR_LocalDev_Fresh' AND DatabaseServerKey=N'local-sql'
 AND SecretReference=N'development:MicroEMR_LocalDev_Fresh')
 THROW 53503,'Unexpected tenant assignment.',1;
DECLARE @Exec table(ObjectName sysname PRIMARY KEY,Origin char(1) NOT NULL);
-- A=existing API runtime; B=new 0085 direct entry. Each row requires only object EXECUTE.
INSERT @Exec(ObjectName,Origin) VALUES
 (N'dbo.AccessProfile_AssignUser','A'),
 (N'dbo.AccessProfile_Get','A'),
 (N'dbo.AccessProfile_GetEffective','A'),
 (N'dbo.AccessProfile_GetUserAssignments','A'),
 (N'dbo.AccessProfile_List','A'),
 (N'dbo.AccessProfile_ReplacePermissions','A'),
 (N'dbo.PlatformAudit_RecordSecurityAuditReview','A'),
 (N'dbo.PlatformAuthorization_GetVersionForUser','A'),
 (N'dbo.PlatformEntitlement_GetActiveForUser','A'),
 (N'dbo.PlatformMembership_Activate','A'),
 (N'dbo.PlatformMembership_CreateWithInitialRole','A'),
 (N'dbo.PlatformMembership_Deactivate','A'),
 (N'dbo.PlatformMembership_ListByTenant','A'),
 (N'dbo.PlatformMembership_ReplaceRoles','A'),
 (N'dbo.PlatformSecurityAudit_GetByUid','A'),
 (N'dbo.PlatformSecurityAudit_RecordCrossPatientOwnership','A'),
 (N'dbo.PlatformSecurityAudit_RecordInvalidTenantMembership','A'),
 (N'dbo.PlatformSecurityAudit_RecordMissingPermission','A'),
 (N'dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor','A'),
 (N'dbo.PlatformSecurityAudit_Search','A'),
 (N'dbo.Tenant_GetByKey','A'),
 (N'dbo.Tenant_GetByUid','A'),
 (N'dbo.TenantDatabase_GetByTenantUid','A'),
 (N'dbo.UserPermissionAccess_Get','A'),
 (N'dbo.UserPermissionOverride_Set','A'),
 (N'dbo.UserTenantMembership_GetActiveByUserAndTenant','A'),
 (N'dbo.UserTenantMembership_GetActiveByUserId','A');
DECLARE @Read table(ObjectName sysname,ColumnName sysname,PRIMARY KEY(ObjectName,ColumnName));
-- No direct Platform table SELECT is needed.
IF EXISTS(SELECT 1 FROM @Exec WHERE OBJECT_ID(ObjectName,N'P') IS NULL)
 THROW 53506,'Missing expected procedure; do not grant an incomplete list.',1;
IF EXISTS(SELECT 1 FROM @Read WHERE COL_LENGTH(ObjectName,ColumnName) IS NULL)
 THROW 53507,'Missing expected read column; review schema.',1;
-- These are exact literal lists, not wildcard enumeration of deployed objects.
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @GrantExec nvarchar(max),@GrantRead nvarchar(max);
 SELECT @GrantExec=STRING_AGG(CONVERT(nvarchar(max),N'GRANT EXECUTE ON OBJECT::'
  +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))
  +N' TO [microemr_dev];'),NCHAR(10)) FROM @Exec;
 EXEC sys.sp_executesql @GrantExec;
 SELECT @GrantRead=STRING_AGG(CONVERT(nvarchar(max),N'GRANT SELECT ON OBJECT::'
  +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))
  +N' ('+QUOTENAME(ColumnName)+N') TO [microemr_dev];'),NCHAR(10)) FROM @Read;
 IF @GrantRead IS NOT NULL EXEC sys.sp_executesql @GrantRead;
 -- No clinical grants or assignment procedures are invoked.
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
-- Administrative metadata only. This is NOT proof of effective rights as runtime login.
SELECT DB_NAME() AS DatabaseContext,(SELECT COUNT(*) FROM @Exec) AS ExpectedProcedureGrants,
 (SELECT COUNT(*) FROM @Read) AS ExpectedColumnGrants;
```

### T batch - MicroEMR_LocalDev_Fresh, after approved 0085

```sql
-- PREPARED ONLY. Separate approval C required. Existing mappings must be preserved.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_LocalDev_Fresh'
 THROW 53501,'Unexpected server/database context.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev'
 AND type='S' AND sid=SUSER_SID(N'microemr_dev'))
 THROW 53502,'Expected existing mapping missing; stop for review.',1;
IF EXISTS(SELECT 1 FROM sys.server_role_members rm JOIN sys.server_principals p
 ON p.principal_id=rm.member_principal_id WHERE p.name=N'microemr_dev')
 OR EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=USER_ID(N'microemr_dev'))
 OR EXISTS(SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND owner_sid=SUSER_SID(N'microemr_dev'))
 THROW 53505,'Unexpected runtime role/ownership; review baseline.',1;
IF (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity)<>1 OR NOT EXISTS
 (SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2')
 THROW 53503,'Unexpected tenant identity.',1;
IF OBJECT_ID(N'dbo.PatientPrescriptionPrivateNote',N'U') IS NULL
 OR OBJECT_ID(N'dbo.PatientPrescriptionPrivateNoteRevision',N'U') IS NULL
 OR OBJECT_ID(N'dbo.PatientPrescriptionPrivateNote_Write',N'P') IS NULL
 THROW 53504,'0085 protected objects required before runtime grants.',1;
DECLARE @Exec table(ObjectName sysname PRIMARY KEY,Origin char(1) NOT NULL);
-- A=existing API runtime; B=new 0085 direct entry. Each row requires only object EXECUTE.
INSERT @Exec(ObjectName,Origin) VALUES
 (N'dbo.ApplicationUser_GetByAuthSubjectId','A'),
 (N'dbo.ApplicationUser_Provision','A'),
 (N'dbo.Appointment_ReportByStatus','A'),
 (N'dbo.AppointmentHistory_GetByAppointmentUid','A'),
 (N'dbo.AuditLog_RecordPatientChartOpened','A'),
 (N'dbo.AuditLog_RecordStructuredRead','A'),
 (N'dbo.CdsAlert_Acknowledge','A'),
 (N'dbo.CdsAlert_Dismiss','A'),
 (N'dbo.CdsAlert_List','A'),
 (N'dbo.CdsAlert_PatientExists','A'),
 (N'dbo.CdsAlert_RecordFinding','A'),
 (N'dbo.CdsAlert_ResolveRuleFindings','A'),
 (N'dbo.CdsAlertHistory_List','A'),
 (N'dbo.ChronicDiseaseEnrollment_Create','A'),
 (N'dbo.ChronicDiseaseEnrollment_GetByUid','A'),
 (N'dbo.ChronicDiseaseEnrollment_Inactivate','A'),
 (N'dbo.ChronicDiseaseEnrollment_ListByPatient','A'),
 (N'dbo.ClinicalDataMigration_AddIssue','A'),
 (N'dbo.ClinicalDataMigration_BeginValidation','A'),
 (N'dbo.ClinicalDataMigration_CompleteValidation','A'),
 (N'dbo.ClinicalDataMigration_FindPatientMatch','A'),
 (N'dbo.ClinicalDataMigration_GetBatch','A'),
 (N'dbo.ClinicalDataMigration_ImportValidatedBatch','A'),
 (N'dbo.ClinicalDataMigration_ListIssues','A'),
 (N'dbo.ClinicalDataMigration_StagePatient','A'),
 (N'dbo.ClinicalDataMigration_StageProblem','A'),
 (N'dbo.ClinicalOutputArtifact_Create','A'),
 (N'dbo.ClinicalOutputArtifact_GetFinalBySource','A'),
 (N'dbo.ClinicalOutputArtifact_RecordFailure','A'),
 (N'dbo.ClinicProfile_Get','A'),
 (N'dbo.ClinicProfile_Save','A'),
 (N'dbo.CppDisplayPreference_Get','A'),
 (N'dbo.CppDisplayPreference_Save','A'),
 (N'dbo.DocumentTemplate_Create','A'),
 (N'dbo.DocumentTemplate_GetActive','A'),
 (N'dbo.DocumentTemplate_GetAll','A'),
 (N'dbo.DocumentTemplate_GetByUid','A'),
 (N'dbo.DocumentTemplate_SetActive','A'),
 (N'dbo.DocumentTemplate_Update','A'),
 (N'dbo.DocumentTemplateAdmin_Clone','A'),
 (N'dbo.DocumentTemplateAdmin_Create','A'),
 (N'dbo.DocumentTemplateAdmin_SetActive','A'),
 (N'dbo.DocumentTemplateAdmin_UpdateMetadata','A'),
 (N'dbo.DocumentTemplateVersion_CreateDraft','A'),
 (N'dbo.DocumentTemplateVersion_GetByTemplateUid','A'),
 (N'dbo.DocumentTemplateVersion_GetByUid','A'),
 (N'dbo.DocumentTemplateVersion_Publish','A'),
 (N'dbo.DocumentTemplateVersion_Retire','A'),
 (N'dbo.DocumentTemplateVersion_UpdateDraft','A'),
 (N'dbo.Patient_Create','A'),
 (N'dbo.Patient_GetByUid','A'),
 (N'dbo.Patient_Search','A'),
 (N'dbo.Patient_UpdateDemographics','A'),
 (N'dbo.PatientAllergy_AssertNoKnownAllergies','A'),
 (N'dbo.PatientAllergy_Create','A'),
 (N'dbo.PatientAllergy_GetByUid','A'),
 (N'dbo.PatientAllergy_GetSnapshot','A'),
 (N'dbo.PatientAllergy_Resolve','A'),
 (N'dbo.PatientAllergy_RevokeNoKnownAllergies','A'),
 (N'dbo.PatientAllergy_Update','A'),
 (N'dbo.PatientChartAlert_Create','A'),
 (N'dbo.PatientChartAlert_GetByPatientUid','A'),
 (N'dbo.PatientChartAlert_GetByUid','A'),
 (N'dbo.PatientChartAlert_Resolve','A'),
 (N'dbo.PatientChartAlert_Update','A'),
 (N'dbo.PatientClinicalHistory_Archive','A'),
 (N'dbo.PatientClinicalHistory_Create','A'),
 (N'dbo.PatientClinicalHistory_List','A'),
 (N'dbo.PatientClinicalHistory_Update','A'),
 (N'dbo.PatientDocument_CommitSignConsultation','A'),
 (N'dbo.PatientDocument_Create','A'),
 (N'dbo.PatientDocument_GetByPatientUid','A'),
 (N'dbo.PatientDocument_GetByUid','A'),
 (N'dbo.PatientDocument_PrepareSignConsultation','A'),
 (N'dbo.PatientDocument_UpdateDraft','A'),
 (N'dbo.PatientDocumentRecipient_List','A'),
 (N'dbo.PatientDocumentRecipient_ReplaceDraft','A'),
 (N'dbo.PatientEncounter_Create','A'),
 (N'dbo.PatientEncounter_CreateStructured','A'),
 (N'dbo.PatientEncounter_GetByPatientUid','A'),
 (N'dbo.PatientEncounter_GetByUid','A'),
 (N'dbo.PatientEncounter_Sign','A'),
 (N'dbo.PatientEncounter_StartFromAppointment','A'),
 (N'dbo.PatientEncounter_UpdateNote','A'),
 (N'dbo.PatientEncounter_UpdateSoapNote','A'),
 (N'dbo.PatientEncounter_UpdateStructuredData','A'),
 (N'dbo.PatientEncounterAddendum_Create','A'),
 (N'dbo.PatientEncounterAddendum_GetByEncounterUid','A'),
 (N'dbo.PatientEncounterContribution_Get','A'),
 (N'dbo.PatientEncounterContribution_Save','A'),
 (N'dbo.PatientEncounterDiagnoses_Get','A'),
 (N'dbo.PatientEncounterDiagnoses_Save','A'),
 (N'dbo.PatientEncounterHistory_GetByEncounterUid','A'),
 (N'dbo.PatientFamilyMedicalHistory_Archive','A'),
 (N'dbo.PatientFamilyMedicalHistory_Create','A'),
 (N'dbo.PatientFamilyMedicalHistory_Get','A'),
 (N'dbo.PatientFamilyMedicalHistory_List','A'),
 (N'dbo.PatientFamilyMedicalHistory_Update','A'),
 (N'dbo.PatientFile_Archive','A'),
 (N'dbo.PatientFile_Create','A'),
 (N'dbo.PatientFile_GetByPatientUid','A'),
 (N'dbo.PatientFile_GetByUid','A'),
 (N'dbo.PatientFile_Restore','A'),
 (N'dbo.PatientImmunization_Create','A'),
 (N'dbo.PatientImmunization_GetByUid','A'),
 (N'dbo.PatientImmunization_ListByPatient','A'),
 (N'dbo.PatientImmunization_MarkEnteredInError','A'),
 (N'dbo.PatientImmunization_Update','A'),
 (N'dbo.PatientMedication_Create','A'),
 (N'dbo.PatientMedication_Discontinue','A'),
 (N'dbo.PatientMedication_GetByPatientUid','A'),
 (N'dbo.PatientMedication_GetByUid','A'),
 (N'dbo.PatientMedication_Update','A'),
 (N'dbo.PatientPrescription_Cancel','A'),
 (N'dbo.PatientPrescription_CreateDraft','A'),
 (N'dbo.PatientPrescription_Finalize','A'),
 (N'dbo.PatientPrescription_GetArtifact','A'),
 (N'dbo.PatientPrescription_GetByPatientUid','A'),
 (N'dbo.PatientPrescription_GetByUid','A'),
 (N'dbo.PatientPrescription_RecordOutputGeneration','A'),
 (N'dbo.PatientPrescription_SignAndFinalize','A'),
 (N'dbo.PatientPrescription_UpdateDraft','A'),
 (N'dbo.PatientPrescriptionPrivateNote_Read','B'),
 (N'dbo.PatientPrescriptionPrivateNote_RecordDenied','B'),
 (N'dbo.PatientProblem_Create','A'),
 (N'dbo.PatientProblem_GetByPatientUid','A'),
 (N'dbo.PatientProblem_GetByUid','A'),
 (N'dbo.PatientProblem_Resolve','A'),
 (N'dbo.PatientProblem_Update','A'),
 (N'dbo.PatientProviderRelationship_Create','A'),
 (N'dbo.PatientProviderRelationship_End','A'),
 (N'dbo.PatientProviderRelationship_List','A'),
 (N'dbo.PatientProviderRelationship_Update','A'),
 (N'dbo.PatientProviderRelationshipType_ListActive','A'),
 (N'dbo.PatientReferral_Close','A'),
 (N'dbo.PatientReferral_Create','A'),
 (N'dbo.PatientReferral_GetActiveProviders','A'),
 (N'dbo.PatientReferral_GetByPatientUid','A'),
 (N'dbo.PatientReferral_GetByUid','A'),
 (N'dbo.PatientReferral_GetProvider','A'),
 (N'dbo.PatientReferral_MarkResponseReceived','A'),
 (N'dbo.PatientReferral_MarkSent','A'),
 (N'dbo.PatientReferral_Send','A'),
 (N'dbo.PatientReferral_SetFollowUpDue','A'),
 (N'dbo.PatientReferral_SetResponseDocument','A'),
 (N'dbo.PatientReferral_UpdateDraft','A'),
 (N'dbo.PatientReferralArtifact_Get','A'),
 (N'dbo.PatientReferralClinicalSelection_Get','A'),
 (N'dbo.PatientReferralClinicalSelection_ReplaceDraft','A'),
 (N'dbo.PatientReferralDocument_GetByReferralUid','A'),
 (N'dbo.PatientReferralDocument_Link','A'),
 (N'dbo.PatientReferralDocument_Unlink','A'),
 (N'dbo.PatientResult_Correct','A'),
 (N'dbo.PatientResult_Create','A'),
 (N'dbo.PatientResult_GetByPatientUid','A'),
 (N'dbo.PatientResult_GetByUid','A'),
 (N'dbo.PatientResult_GetHistory','A'),
 (N'dbo.PatientResult_GetUnreviewed','A'),
 (N'dbo.PatientResult_GetUnreviewedCount','A'),
 (N'dbo.PatientResult_MarkEnteredInError','A'),
 (N'dbo.PatientResult_MarkReviewed','A'),
 (N'dbo.PatientResult_Update','A'),
 (N'dbo.PatientRiskFactor_Archive','A'),
 (N'dbo.PatientRiskFactor_Create','A'),
 (N'dbo.PatientRiskFactor_Get','A'),
 (N'dbo.PatientRiskFactor_List','A'),
 (N'dbo.PatientRiskFactor_Update','A'),
 (N'dbo.PatientTask_Complete','A'),
 (N'dbo.PatientTask_Create','A'),
 (N'dbo.PatientTask_GetByPatientUid','A'),
 (N'dbo.PatientTask_GetByUid','A'),
 (N'dbo.PatientTask_GetOpenForDashboard','A'),
 (N'dbo.PatientTask_GetOverdue','A'),
 (N'dbo.PatientTask_GetOverdueCount','A'),
 (N'dbo.PatientTask_Reopen','A'),
 (N'dbo.PatientTask_Update','A'),
 (N'dbo.PatientVital_Create','A'),
 (N'dbo.PatientVital_GetByPatientUid','A'),
 (N'dbo.PatientVital_GetByUid','A'),
 (N'dbo.PatientVital_Update','A'),
 (N'dbo.Provider_Create','A'),
 (N'dbo.Provider_EligibleApplicationUsers_List','A'),
 (N'dbo.Provider_Get','A'),
 (N'dbo.Provider_LinkApplicationUser','A'),
 (N'dbo.Provider_List','A'),
 (N'dbo.Provider_SetActive','A'),
 (N'dbo.Provider_UnlinkApplicationUser','A'),
 (N'dbo.Provider_Update','A'),
 (N'dbo.ScheduleAppointment_Cancel','A'),
 (N'dbo.ScheduleAppointment_CreateWithCriticalFlag','A'),
 (N'dbo.ScheduleAppointment_GetByUid','A'),
 (N'dbo.ScheduleAppointment_GetByUidWithCriticalFlag','A'),
 (N'dbo.ScheduleAppointment_GetMonthSummary','A'),
 (N'dbo.ScheduleAppointment_MarkArrived','A'),
 (N'dbo.ScheduleAppointment_Reschedule','A'),
 (N'dbo.ScheduleAppointment_UpdateStatus','A'),
 (N'dbo.ScheduleAppointment_UpdateWithCriticalFlag','A'),
 (N'dbo.ScheduleResource_GetActive','A'),
 (N'dbo.SchedulingBlockedTime_Cancel','A'),
 (N'dbo.SchedulingBlockedTime_Create','A'),
 (N'dbo.SchedulingBlockedTime_GetForDateRange','A');
DECLARE @Read table(ObjectName sysname,ColumnName sysname,PRIMARY KEY(ObjectName,ColumnName));
INSERT @Read(ObjectName,ColumnName) VALUES
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
IF EXISTS(SELECT 1 FROM @Exec WHERE OBJECT_ID(ObjectName,N'P') IS NULL)
 THROW 53506,'Missing expected procedure; do not grant an incomplete list.',1;
IF EXISTS(SELECT 1 FROM @Read WHERE COL_LENGTH(ObjectName,ColumnName) IS NULL)
 THROW 53507,'Missing expected read column; review schema.',1;
-- These are exact literal lists, not wildcard enumeration of deployed objects.
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @GrantExec nvarchar(max),@GrantRead nvarchar(max);
 SELECT @GrantExec=STRING_AGG(CONVERT(nvarchar(max),N'GRANT EXECUTE ON OBJECT::'
  +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))
  +N' TO [microemr_dev];'),NCHAR(10)) FROM @Exec;
 EXEC sys.sp_executesql @GrantExec;
 SELECT @GrantRead=STRING_AGG(CONVERT(nvarchar(max),N'GRANT SELECT ON OBJECT::'
  +QUOTENAME(PARSENAME(ObjectName,2))+N'.'+QUOTENAME(PARSENAME(ObjectName,1))
  +N' ('+QUOTENAME(ColumnName)+N') TO [microemr_dev];'),NCHAR(10)) FROM @Read;
 IF @GrantRead IS NOT NULL EXEC sys.sp_executesql @GrantRead;
 DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.PatientPrescriptionPrivateNote TO [microemr_dev];
 DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.PatientPrescriptionPrivateNoteRevision TO [microemr_dev];
 DENY EXECUTE ON OBJECT::dbo.PatientPrescriptionPrivateNote_Write TO [microemr_dev];
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
-- Administrative metadata only. This is NOT proof of effective rights as runtime login.
SELECT DB_NAME() AS DatabaseContext,(SELECT COUNT(*) FROM @Exec) AS ExpectedProcedureGrants,
 (SELECT COUNT(*) FROM @Read) AS ExpectedColumnGrants;
```

## 6. Read-only verification plan

Before C: retain Step 124A baseline and recheck exact context, existing matching users, server roles/direct permissions, database roles/grants and inherited public rights. These are inspection queries, not clinical procedure calls. P/T baseline expected CONNECT only; no new runtime grant was applied in 124B.

```sql
SELECT @@SERVERNAME AS ServerIdentity,DB_NAME() AS DatabaseContext,
 ORIGINAL_LOGIN() AS SqlLogin,USER_NAME() AS DatabaseUser;
SELECT name,type_desc,CASE WHEN sid=SUSER_SID(N'microemr_dev') THEN 1 ELSE 0 END AS MatchesLoginSid
FROM sys.database_principals WHERE name=N'microemr_dev' OR sid=SUSER_SID(N'microemr_dev');
SELECT u.name AS DatabaseUser,r.name AS RoleName
FROM sys.database_role_members rm JOIN sys.database_principals u ON u.principal_id=rm.member_principal_id
JOIN sys.database_principals r ON r.principal_id=rm.role_principal_id WHERE u.sid=SUSER_SID(N'microemr_dev');
SELECT p.class_desc,p.state_desc,p.permission_name,OBJECT_SCHEMA_NAME(p.major_id) AS ObjectSchema,
 OBJECT_NAME(p.major_id) AS ObjectName,COL_NAME(p.major_id,p.minor_id) AS ColumnName
FROM sys.database_permissions p JOIN sys.database_principals u ON u.principal_id=p.grantee_principal_id
WHERE u.sid=SUSER_SID(N'microemr_dev');
```

After approved C, connect **as microemr_dev** separately to P/T using an authorized local credential UI, never password in chat/report. No impersonation/grant change needed during verification. Read-only current-user SQL:

```sql
SELECT ORIGINAL_LOGIN() AS SqlLogin,DB_NAME() AS DatabaseContext,
 IS_SRVROLEMEMBER('sysadmin') AS IsSysadmin,IS_MEMBER('db_owner') AS IsOwner,
 IS_MEMBER('db_ddladmin') AS IsDdlAdmin,IS_MEMBER('db_datareader') AS IsReader,
 IS_MEMBER('db_datawriter') AS IsWriter;
SELECT permission_name FROM sys.fn_my_permissions(NULL,'DATABASE');
SELECT SCHEMA_NAME(schema_id)+N'.'+name AS ProcedureName,
 HAS_PERMS_BY_NAME(SCHEMA_NAME(schema_id)+N'.'+name,'OBJECT','EXECUTE') AS CanExecute
FROM sys.procedures WHERE is_ms_shipped=0 ORDER BY ProcedureName;
-- T only: repeat for every literal @Read object/column, not just this identity example.
SELECT HAS_PERMS_BY_NAME(N'dbo.TenantDatabaseIdentity','OBJECT','SELECT',N'TenantUid','COLUMN') AS CanReadIdentity;
SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','SELECT') AS CanSelect,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','INSERT') AS CanInsert,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','UPDATE') AS CanUpdate,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','DELETE') AS CanDelete
FROM (VALUES(N'dbo.PatientPrescriptionPrivateNote'),(N'dbo.PatientPrescriptionPrivateNoteRevision')) v(ObjectName);
SELECT HAS_PERMS_BY_NAME(N'dbo.PatientPrescriptionPrivateNote_Write','OBJECT','EXECUTE') AS CanExecuteHelper;
```

Expected: original login microemr_dev; all five elevated/fixed-role flags 0; exact 27 P/201 T allowlist procedures execute=1, all 80 read-column checks=1; both protected-table DML matrices all 0 and helper=0. Check visible-procedure results against the literal lists, not merely counts. Missing metadata rows/NULL is BLOCKED, not PASS; reviewer can independently establish object existence without treating sa permission results as runtime evidence. No broad database permissions or unexpected application-object grants. Additional explicitly granted columns remain in-scope only if independently approved; do not infer privileges from role absence alone. Every post-C check is **DEFERRED** here.

Do not call chart/private read/history/output/report procedures during SELECT-only verification: many commit audit/output events. Functionality checks occur only in separately approved fixture/visual work. No manual security penetration test requested.

## 7. DatabaseTool packaging and 0085 readiness

**Focused packaging check PASS; migration not executed.** Project Content entries include db/**/*.sql and JSON with CopyToOutputDirectory=PreserveNewest, linked under output database/. Earlier output predated the new asset (missing 0085 despite current project inputs); no project defect/change was needed. The authorized non-database asset-refresh exception was used:

```powershell
Set-Location -LiteralPath 'D:\MasterProgram_Ahmed\MicroEMR'
dotnet build .\src\MicroEMR.DatabaseTool\MicroEMR.DatabaseTool.csproj --no-restore -c Debug -v:q
```

Actual result: exit 0, **0 warnings / 0 errors, 14.69 seconds**. This was a DatabaseTool project build and required referenced projects, not a full solution build, app startup or test rerun. Packaged 0085 now exists and matches repository bytes exactly; packaged manifest contains its ID once. No hash-analysis rerun.

Fresh SELECT-only migration-ID/identity check: 85 applied entries; comparing to packaged manifest gives **only 0085-private-prescription-notes pending**, no unknown IDs; tenant identity agrees. Platform assignment/reference also agrees. Prior accepted hash evidence reused, not freshly recertified. Repeat required preflight immediately before A; tool has no native only-migration/expected-UID flag and applies all pending entries.

Prepared mutation command, **NOT RUN**, same cwd:

```powershell
dotnet .\src\MicroEMR.DatabaseTool\bin\Debug\net10.0\MicroEMR.DatabaseTool.dll tenant provision --tenant-key local-dev-fresh
```

Use Step 124A section 6's verified effective configuration precedence, approved actor/administration flag, deployment credential, exact catalog/identity guards and Platform provisioning-status side effects. No runtime microemr_dev migration rights. Freeze catalog/config/provisioning during window. Expected applied count 1/Migrated and matching ledger/postflight; per-migration tenant transaction does not make Platform status writes atomic with tenant commit. Failure after tenant commit requires inspection, never direct replay/manual ledger edit.

## 8. Platform 027 readiness

Reuse Step 124A section 7's reviewed **single-connection ODBC SQLCMD transaction** and exact command, target P on the exact endpoint, including :On Error exit/-b/-V 11, GO/:r handling, XACT_ABORT, rollback-on-open-connection-loss qualification, correct context guard, trusted/enabled constraints and bidirectional assignment snapshots. No execution in this step.

Source `db/platform/027_private_prescription_note_permission.sql` remains the only 027 input; it changes two constraints and three procedure definitions, grants no clinical access. Validate exact definitions and preserved 026 query after commit, not keywords alone. A disconnected COMMIT can have uncertain outcome; inspect before retry. Rollback may still be in progress after cancellation. Recovery for prior partial/committed changes requires approved state repair or verified Platform backup, never automatic replay/restore.

Initial review readiness was execution mechanism prepared, with backup/recovery evidence and B approval outstanding. **Superseded by section 12 for current deployment state: user execution reported and current 027 registration independently verified.** Wrapper/backup/approval-process evidence and complete unchanged-assignment proof are not established retrospectively.

## 9. Platform backup findings

Actual SELECT-only backup history in msdb via approved P connection (no separate Auth/other application database connection):

| Item | Observed |
| --- | --- |
| Full backup set | 43, MicroEMR_Platform, position 1 |
| Start/finish | 2026-10-20 15:36:21, SQL Server-local fields; server timezone not independently established |
| copy-only / backup checksums / damaged | False / False / False |
| Disk medium in recorded history | C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_Platform.bak |
| Engine / P and T compatibility | 15.0.2000.5 / both 150 |

**Backup existence/completion metadata observed; deployment recovery remains BLOCKED.** No checksum/readability/media-access/header/VERIFYONLY/actual restore evidence obtained, no confirmation this is the intended protected recovery point, and path may be reused/overwritten. is_damaged=0 is not proof the file remains readable or restorable. No BACKUP/RESTORE/VERIFYONLY executed. Do not assume this historical set is sufficient or automatically create/overwrite another backup.

Prepared repeatable SELECT-only check, same endpoint/context:

```sql
SELECT TOP(5) bs.backup_set_id,bs.database_name,
 CONVERT(varchar(19),bs.backup_start_date,126) AS ServerLocalStart,
 CONVERT(varchar(19),bs.backup_finish_date,126) AS ServerLocalFinish,
 bs.is_copy_only,bs.has_backup_checksums,bs.is_damaged,bs.position,bmf.physical_device_name
FROM msdb.dbo.backupset bs JOIN msdb.dbo.backupmediafamily bmf ON bmf.media_set_id=bs.media_set_id
WHERE bs.database_name=N'MicroEMR_Platform' AND bs.type='D'
ORDER BY bs.backup_finish_date DESC;
```

Required user/operator evidence before B: chosen correct recovery point/set/position; protected medium still present with service access; header/database identity; independently authorized readability/integrity verification and documented restore assurance/recovery window. Missing backup checksums must be reflected in that assurance, not hidden. Keep Platform memberships/access/audit data protected; disposable-tenant confirmation does not make Platform disposable. No restored test environment or production target substituted.

## 10. Separate approval readiness

All rows target the exact endpoint. None authorizes another row or the operations described in recovery.

| Item | Exact target and expected writes | Verification | Recovery / current gate |
| --- | --- | --- | --- |
| A: 0085 | User reports execution in local-dev-fresh and local-dev; section 13 resolves exact targets | Both ledger hashes and schema/module/constraint/retention metadata independently verified | Current postflight PASS; execution transaction/output not directly observed. Do not replay or edit ledger |
| B: 027 | P only; two constraints/three procedures; user reports execution | Section 12: exact procedures, trusted/enabled constraints, permission sets and unchanged 026 verified | Current registration PASS; execution method/backup/process evidence and full assignment-row invariance unverified. Do not rerun |
| C: SQL grants | Existing microemr_dev P/T users only; section 5 exact 27+201 EXECUTE, 80 column SELECT, protected restrictions | Fresh baseline and actual microemr_dev effective metadata checks; no procedure calls | Each DB batch transaction; separate reviewed reverse permission delta, no account/data deletion. C required after schema deployment; no Auth grant |
| D: runtime switch | Only explicitly approved API PlatformDatabase and exact tenant secret setting; separately retain Auth connections | Identify actual process/config provider/login; verify C before switch | Preserve protected prior settings for separately approved rollback. No setting changed; explicit D required |
| E: clinical grants | P selected TenantUid; exact fictional subjects/profile/override keys still to identify | Active membership/effective keys/provider/clinical actor checks | Audited bounded revoke/override reversal separately approved; preserve audits. Explicit recipients/E required |
| F: fictional fixtures | T fictional patient/A-owned Draft/finalized acceptance records; any Auth/P prerequisites separately scoped | Step 122 V1-V3 cards, distinguish observed vs user-reported | Retain audit/revisions/signed artifacts, no physical clinical deletion. Explicit F required |
| G: restart if necessary | Exact identified nonproduction API/Web process only; Auth startup excluded | Version/asset/login check, approved identity; no automatic health action that writes data | Separately approved process/config rollback. Runtime unestablished, G not approved |

Recommended order: close Platform recovery/effective deployment configuration and actual runtime attribution; fresh preflight; A then tenant postflight; B then Platform postflight; C then effective account checks; D/G separately; E/F separately; V1-V3. Account name existence is not authorization for any grant or switch. No combined blanket approval requested. Auth security/startup migration work remains a separate task rather than a reason to broaden microemr_dev.

Follow-up sequence adjustment: B is now already observed deployed. Preserve it and complete the remaining A/C/D/E/F/G gates without replaying B. The prior tenant-first sequence was proposed coordination, not a cross-database SQL dependency; this verification records the actual Platform-first state without inventing prior approvals or tenant deployment evidence.

## 11. Evidence, remaining blockers and next step

Completed: source/deployed-object/column/ownership/type dependency review for the named API runtime; separate Auth configuration/code boundary; focused non-database DatabaseTool build and exact packaged-asset check; fresh migration-ID/identity/assignment metadata check; Platform backup metadata check; proposed grant-script structural/whitespace review. All live SQL was SELECT/metadata only. The grant batches/wrapper/postgrant checks were not executed or authenticated as microemr_dev.

Remaining gates: verified Platform recovery; approved effective tool configuration/actor/deployment principal; actual running API identity/provider attribution; separate A/B/C/D/E/F/G approvals and chosen fictional clinical subjects. Auth-wide least privilege remains qualified because existing separate connections are configured sa and Auth startup runs migrations; no global least-privilege claim.

Recommended **Step 124C: close Platform recovery and runtime/deployment-configuration evidence, approve A/B separately, perform controlled migrations and postflight, then separately approve C and validate microemr_dev before any runtime switch.** Keep E/F/G separate. No further broad source scan or passing-suite rerun needed absent a concrete failure.

Retain Step 121 **185 .NET PASS, 12 frontend PASS, TypeScript PASS, final build 0 warnings/errors**, Steps 123/124A evidence and all earlier qualifications, Step 118 Generic Name/Step 116 Date Precision, existing deployment qualifications, remaining DE09 issues, Immunization/allergy and Family History M7/M8 deferred evidence. **PC07.01 PARTIAL, 10 IDs / three packages. DE09.019 IMPLEMENTED - DEPLOYMENT/VISUAL EVIDENCE PENDING.** V1-V3 remain DEFERRED. Stop after Step 124B; no changing database operation performed.

## 12. User-executed 027: independent read-only postflight

Follow-up date: 2026-10-20. User explicitly reports executing `db/platform/027_private_prescription_note_permission.sql` on `MicroEMR_Platform`. Execution itself is **USER-REPORTED**; the current deployed state below is independently **PASS** where stated. Codex neither executed nor replayed 027 and made no database changes. Only this report is updated; reports 157-159 and all SQL/application source are preserved.

Git follow-up checkpoint: `main`, HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`; reports 157-160 untracked on entry. SELECT-only connections were restricted to the exact approved endpoint `192.168.50.1,50013` and P/T. Server identity `EC2AMAZ-TC26CQK\TELEMED52` and database contexts were verified. Existing administrative credentials were used only in memory; no password/full connection string was displayed. No mutation procedure, grant, backup/restore, build or test suite was executed.

| Postflight check | Actual evidence | Status |
| --- | --- | --- |
| Platform target | Exact approved endpoint; server identity above; DB_NAME()=MicroEMR_Platform | PASS |
| AccessProfile_ReplacePermissions | Complete definition matches repository 027 after CREATE/ALTER header and line-ending normalization; ANSI_NULLS/QUOTED_IDENTIFIER enabled | PASS |
| UserPermissionOverride_Set | Same full-definition comparison and settings | PASS |
| PlatformSecurityAudit_RecordMissingPermission | Same full-definition comparison and settings | PASS |
| CK_UserPermissionOverride_Key | Present, enabled, trusted; all 29 allowed keys match 027, including Prescriptions.ViewPrivateNotes; full OR expression inspected | PASS |
| CK_PlatformSecurityAuditEvent_CapabilityPermission | Present, enabled, trusted; all nine capability/permission pairs match 027; RequiredPermission non-NULL grouping and TenantSelection/NULL alternative retained | PASS |
| Existing 026 query | UserPermissionAccess_Get definition exactly unchanged from the retained pre-027 Step 124B metadata | PASS |
| Selected-tenant private profile grants | Zero rows granting Prescriptions.ViewPrivateNotes through profiles for the confirmed TenantUid | PASS: no grant found |
| Selected-tenant private overrides | No Allow/Deny private-key override rows for that TenantUid | PASS: no override found |
| Runtime SQL user | microemr_dev direct Platform permissions still CONNECT only | Observation; no runtime grant readiness claimed |
| Selected-tenant role/membership aggregates | ClinicAdministrator 1, MedicalAssistant 1, Physician 2, Scheduler 1; five memberships, matching earlier counts | Aggregate comparison only |
| Tenant 0085 | Matching tenant identity; no 0085 ledger row; protected current/revision tables and read/denial procedures absent | Deployment remains DEFERRED |

These are metadata checks, not procedure invocation or application/clinical authorization tests. Full procedure comparison covers more than keyword presence. Constraint comparison covered expected literal sets/structural predicates, not merely object existence. Registration does not grant clinical access.

Evidence limits: no pre/post full assignment-row snapshots from the user's execution are available. Matching aggregate counts and no private grants do **not** establish that every profile, membership, role, assignment or override row was unchanged. SQLCMD/SSMS wrapper use, fail-fast/transaction history, prior scoped approval, backup verification and exact execution time were not observed and are not inferred from final schema. Platform backup/recovery assurance in section 9 remains unverified; this does not erase the independently verified current registration. No claim that 0085 ran first or that complete DE09.019 deployment/visual acceptance has occurred.

Read-only inspection used `sys.objects`/`sys.sql_modules` for the four named procedure definitions and stored SET flags, `sys.check_constraints` for both definitions/trust/enable flags, selected-tenant profile/override/role/membership queries, and `sys.database_permissions` for microemr_dev. Tenant checks read only `TenantDatabaseIdentity`, the 0085 `SchemaMigration` row predicate and four named `sys.objects` entries. Source comparison was local; no SQL DDL/DML was sent. Documentation whitespace checks completed; no prior automated evidence rerun.

**Next:** preserve the verified 027 state; do not rerun it. Close recovery/deployment-configuration prerequisites, obtain separate A approval for tenant 0085, perform tenant postflight, then separately approve C grants and verify microemr_dev before D/G. E/F and actual V1-V3 follow separately. No authorization for those operations is inferred from the user's completed 027 execution. Certification and all earlier qualifications remain unchanged.

## 13. User-applied 0085: both test-tenant postflights

Follow-up date: 2026-10-20. User reports applying 0085 to the two test tenant keys `local-dev-fresh` and `local-dev`. This authorizes read-only verification of those named tenants, not further deployment, grants, runtime configuration, fixture work or migration-history changes. Catalog resolution occurred before connecting to the second tenant; no guessed database or alternate endpoint was used. All connections used `192.168.50.1,50013`, server `EC2AMAZ-TC26CQK\TELEMED52`.

| Tenant key | Catalog-resolved database | Confirmed TenantUid | Stored 0085 AppliedAt |
| --- | --- | --- | --- |
| local-dev-fresh | MicroEMR_LocalDev_Fresh | F94C6ECD-4417-483E-8A94-0507CF8384C2 | 2026-10-20T16:32:55.9458784 |
| local-dev | MicroEMR_Db | 8531AAB9-E647-4D3F-852C-8FBCF9B4DD7B | 2026-10-20T16:33:12.1163159 |

Both catalog assignments/tenants are Active on DatabaseServerKey `local-sql`; references are respectively `development:MicroEMR_LocalDev_Fresh` and `development:MicroEMR_Db`. Each resolved credential was checked in memory for exact server/database and SQL authentication. Each clinical database returned exactly one matching TenantDatabaseIdentity UID/key before further queries. In particular, **local-dev is MicroEMR_Db, not MicroEMR_Tenant_LocalDev**. No credential/full connection string was displayed.

**Independent metadata postflight PASS in both databases:**

- Exactly one `0085-private-prescription-notes` ledger row, SchemaVersion 1.0.0, hash `4F784ACEBAC3BF086B22DE1F7437B0E78A6E428ED1E4430AA159B80F63DF67D0`, AppliedBy `ALIENWARE_AHMED`. All 86 manifest IDs recorded, no missing or unknown IDs. This checks the 0085 hash against previously verified source identity; it does not repeat all preceding hash analysis for the newly checked tenant.
- All nine inspected task objects present: both protected tables, both retention triggers, three private-note procedures and both replaced draft procedures.
- Complete definitions of CreateDraft, UpdateDraft, private Write/Read/RecordDenied and both retention triggers match repository 0085 after excluding leading comments and normalizing CREATE/ALTER header and line endings. No clinical procedure was invoked. Leading batch comments explain initial raw-text differences; no body mismatch remained.
- All 11 current-note and nine revision columns match expected names/types/lengths/nullability/identity/precision metadata, including nullable nvarchar(1000) note text, datetime2(7), original author and correlation/audit identifiers.
- Expected primary/unique keys present: parent PrescriptionUid/PatientUid pair; current-note identity PK and unique note/prescription UIDs; revision UID PK, unique audit-event UID and note-UID/revision-number pair.
- Both foreign keys enabled/trusted with the expected column associations and NO_ACTION delete behavior. Revision Operation CHECK enabled/trusted, admitting only Create/Update/Clear.
- Current-note INSTEAD OF DELETE retention trigger enabled; revision INSTEAD OF UPDATE/DELETE trigger enabled; definitions match the immutable-retention implementation.
- Public DENY SELECT/INSERT/UPDATE/DELETE exists on both protected tables; public DENY EXECUTE exists on the internal Write helper. These are permission-metadata observations, not evidence of enforcement under an ordinary runtime principal.
- Platform catalog provisioning metadata remains Active/schema 1.0.0 and records LastMigrationAt values 2026-10-20T16:32:56.0247633 (fresh) and 2026-10-20T16:33:12.1949442 (local-dev), consistent with completed provisioning-status updates. Stored values are quoted without inferring execution method or independently observing transaction history.

Execution itself remains USER-REPORTED; current ledger/schema results are independently verified. Runner console output, exact command and per-run transaction/backup process were not observed. No assertion that every historical signed artifact/content row remained unchanged follows from metadata inspection. No protected content or real patient record was read. No clinical read/history procedure was called, since those write audit events.

The earlier A-pending/0085-absent observations and section 12's then-next instruction are superseded by this postflight; **do not rerun 0085 in either tenant, rerun 027, or manually insert/edit migration history**. No automatic permission grant follows from either deployment. The prepared C grant batch is still scoped to Platform plus MicroEMR_LocalDev_Fresh only; verification of MicroEMR_Db does not silently extend its grant/configuration/fixture approval or establish microemr_dev mappings there.

Only this report was updated. Git remains `main` at `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`; previous reports, SQL and application files preserved. Checks were SELECT-only catalog/identity/ledger/schema/module/index/constraint/trigger/permission metadata plus local source comparison and documentation whitespace review. No migration, history repair, grant, build, test suite, restart, configuration change, backup/restore, fixture, commit or merge performed by Codex.

**Next:** retain the verified Platform and two-tenant migration evidence; separately close recovery/runtime qualifications and approve C for explicitly named runtime targets, verify actual microemr_dev rights, then D/G and E/F as needed before V1-V3. Runtime/clinical permissions, Auth security boundary, backup assurance and visual results remain qualified/pending. PC07.01 stays PARTIAL, inventory 10 mandatory IDs / three packages; DE09.019 now has verified Platform/tenant deployment metadata, while runtime and visual evidence remain pending.
