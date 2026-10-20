# Step 125 - DE09.019 runtime security and visual readiness

Date: 2026-10-20 (America/Toronto). **PC07.01 PARTIAL; inventory 10 mandatory IDs / three packages. DE09.019 IMPLEMENTED - RUNTIME/VISUAL EVIDENCE PENDING.** Database inspection only; only this new report written. No database writes, grants, configuration changes, fixture/artifact generation or application restart.

## 1. Checkpoint and retained deployment evidence

Branch `main`; HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry status: reports 157-160 untracked; no tracked changes. Preserve those reports, AGENTS.md and application/SQL source unchanged. No staging, commit, merge, remote Git, build or test-suite run.

Binding prior PASS evidence, not repeated: [Step 124B sections 12-13](160-step124b-pc07-01-runtime-grants-and-deployment-readiness.md) independently verified Platform 027 definitions/trusted constraints and 0085 ledger/hash/schema/procedures/triggers/constraints/public denials in both tenants. `local-dev-fresh` maps to `MicroEMR_LocalDev_Fresh`, TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`; `local-dev` maps to `MicroEMR_Db`, TenantUid `8531AAB9-E647-4D3F-852C-8FBCF9B4DD7B`. All on `192.168.50.1,50013` / server `EC2AMAZ-TC26CQK\TELEMED52`.

Do not execute 0085/027 or modify migration history. This step connected only Platform and the primary tenant; secondary tenant evidence is reused, with no new testing or implied grant/configuration scope there. Platform user-run transaction/backup/full assignment-row invariance qualifications remain recorded; verified final registration is not invented execution-process evidence.

## 2. Targeted microemr_dev permission results

Reused the exact Step 124B object/column allowlist, without rebuilding the inventory. Fresh administrative metadata confirmed:

- Enabled SQL login `microemr_dev`; matching SQL_USER SID in `MicroEMR_Platform` (P) and `MicroEMR_LocalDev_Fresh` (T).
- No assigned server/database roles. Direct server permission CONNECT SQL; direct database permission CONNECT in each DB. No direct object/column grants found.
- T public has all eight protected-table SELECT/INSERT/UPDATE/DELETE denials and the internal Write helper EXECUTE denial.

Effective checks used **temporary session-only EXECUTE AS LOGIN=N'microemr_dev'**, with REVERT and connection disposal. This changes only the review connection's security context; it does not change a login, role, grant, database record or application configuration. ORIGINAL_LOGIN remained the authenticating reviewer sa; SUSER_SNAME and USER_NAME both became microemr_dev. No clinical stored procedure was invoked. This establishes effective permission metadata under the target security token, not password authentication or API usage.

| Check | Actual result under microemr_dev token | Evidence status |
| --- | --- | --- |
| Sysadmin/db_owner/db_ddladmin/db_datareader/db_datawriter | All five 0 in both DBs | PASS: listed broad roles absent |
| Required P entry procedures | 0 allowed / 27 denied / 0 unavailable | BLOCKED: all 27 runtime EXECUTE grants missing |
| Required T entry procedures | 0 allowed / 201 denied / 0 unavailable | BLOCKED: all 201 runtime EXECUTE grants missing |
| Required T read columns | 0 allowed / 80 denied / 0 unavailable | BLOCKED: all 80 SELECT column grants missing |
| Protected current-note table | SELECT/INSERT/UPDATE/DELETE all 0 | PASS: effective direct permissions denied |
| Protected revision table | SELECT/INSERT/UPDATE/DELETE all 0 | PASS: effective direct permissions denied |
| Internal PatientPrescriptionPrivateNote_Write helper | EXECUTE 0 | PASS: effective direct execution denied |
| Effective database permissions | CONNECT plus public VIEW ANY COLUMN ENCRYPTION KEY DEFINITION / VIEW ANY COLUMN MASTER KEY DEFINITION only | No broad application data/DDL rights found in inspected effective set |
| Login password authentication / actual API request | Not performed | DEFERRED |

No permission result was NULL. The eight table denials and helper denial are not inferred from the reviewer's sa rights. They also do not constitute an attempted table read or security penetration test. Functional runtime use is BLOCKED: this account cannot execute the required API procedures yet. Do not switch to it or grant a broad role to bypass missing rights.

Ownership chaining remains as designed: after approved outer-procedure EXECUTE grants, same-owner static procedure access can perform its existing authorized table/audit/helper work without direct table DML. Current absence of outer grants prevents that path; public restrictions do not need new DENY statements here. A shared SQL login does not replace clinical-user, tenant, ownership or application permission checks.

## 3. Actual API/Web runtime identity

Fresh non-mutating local inspection found no identifiable MicroEMR.Api, MicroEMR.Web or MicroEMR.Auth executable/dotnet-hosted process. No process command line or credential was emitted. Runtime environment, loaded binaries/assets and effective provider precedence cannot be established for an absent/unattributed process.

Sanitized current API user-secrets observations: PlatformDatabase -> exact P endpoint/login sa; development:MicroEMR_LocalDev_Fresh -> exact T endpoint/login sa; AuthDatabase -> MicroEMR_Auth at same endpoint/login sa. These are configuration evidence only. No matching connection override/environment indicator was emitted for the inspection process; this does not establish another process's environment. Existing local API/Web Debug DLLs are present, but timestamps/file existence do not prove deployed binaries match Step 121 or that either is loaded. No build or application startup occurred.

SQL session inspection found no microemr_dev session. A same-host generic .Net SqlClient sa session (SQL session 54, reported client PID 6452) and sa SSMS sessions were visible. PID 6452 was not visible as a local process on follow-up. An idle/pooled/external/stale/generic session cannot be attributed to the API from that information alone. **Actual API runtime identity BLOCKED/unestablished; do not state that the running API uses sa.** The configured sa identity and separate Auth sa configuration remain security qualifications.

Exact non-mutating operator instructions:

1. If an API is already running, identify its PID, executable/content root and environment in its existing launcher/debugger; do not start/restart it for this step. Never share command lines/debug views containing secrets.
2. On the approved server, run the session query in section 8 as an authorized metadata reviewer; correlate host/process/PID/program/endpoint with that known process. Generic program name alone is insufficient. Do not trigger a chart/private-note/API request merely to populate a session; those may write required read audits.
3. In the existing process debugger, inspect only sanitized configuration fields (data source, database name, SQL UserID) through SqlConnectionStringBuilder, not full connection strings. Correlate with the SQL session; configuration alone does not prove effective SQL use.
4. If no attributable existing process/session is available, retain BLOCKED. Identify the exact launcher/process and obtain separate restart/start authorization before subsequent runtime work. Do not fabricate a PID, loaded version or live identity.

Smallest transition plan: approve only section 5 missing SQL grants; verify effective rights without clinical procedures; identify the actual configuration source; separately approve changing only API P/T runtime settings to microemr_dev; independently approve any required API/Web restart and confirm identity/version afterward. Preserve deployment credentials and separate Auth connections. No settings or process controls changed here.

## 4. Auth boundary

Reuse Step 124B section 4: Auth uses separate AuthServerConnection/ApplicationDbContext with Identity/OpenIddict EF operations; API identity lookup/administration separately uses AuthDatabase/fallback AuthServerConnection. Those local connections are configured sa; actual running Auth identity remains unverified. No Auth database connection, data lookup, grant or configuration change in Step 125.

Auth startup SeedData calls Database.MigrateAsync and seed writes; a restart is not a read-only diagnostic. Keep that existing DDL/startup dependency separate from the intended P/T least-privilege switch. No microemr_dev Auth grants proposed. Auth-wide least privilege and its startup-migration separation remain qualified/separately scoped, not silently solved by tenant SQL grants.

## 5. Missing SQL grants only - proposal, NOT EXECUTED

All 228 reviewed procedure rights and 80 reviewed column rights are currently missing. The statements below are generated from the already verified Step 124B literal list, not a wildcard grant inventory or newly broadened scope. They target existing microemr_dev users in P/T only, on the exact approved endpoint. No new account, role, helper EXECUTE, protected-table access, Auth access or secondary-tenant grant.

**Separate approval A required.** Confirm context and matching login SID immediately before any later application. Use a reviewed transaction per DB and retain the prior permission snapshot; two DB batches are not one distributed transaction. A reviewed reverse grant delta, not account/data deletion or automatic REVOKE, is needed for recovery. This report does not authorize running its proposal.

### P - MicroEMR_Platform

```sql
-- NOT EXECUTED. Separate Step 125 approval A required.
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_Platform'
 THROW 53601,'Unexpected server/database context.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev' AND sid=SUSER_SID(N'microemr_dev'))
 THROW 53602,'Expected existing mapping missing; stop.',1;
GRANT EXECUTE ON OBJECT::[dbo].[AccessProfile_AssignUser] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AccessProfile_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AccessProfile_GetEffective] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AccessProfile_GetUserAssignments] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AccessProfile_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AccessProfile_ReplacePermissions] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformAudit_RecordSecurityAuditReview] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformAuthorization_GetVersionForUser] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformEntitlement_GetActiveForUser] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformMembership_Activate] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformMembership_CreateWithInitialRole] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformMembership_Deactivate] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformMembership_ListByTenant] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformMembership_ReplaceRoles] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformSecurityAudit_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformSecurityAudit_RecordCrossPatientOwnership] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformSecurityAudit_RecordInvalidTenantMembership] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformSecurityAudit_RecordMissingPermission] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformSecurityAudit_RecordUnresolvedClinicalActor] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PlatformSecurityAudit_Search] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Tenant_GetByKey] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Tenant_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[TenantDatabase_GetByTenantUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[UserPermissionAccess_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[UserPermissionOverride_Set] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[UserTenantMembership_GetActiveByUserAndTenant] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[UserTenantMembership_GetActiveByUserId] TO [microemr_dev];
```

### T - MicroEMR_LocalDev_Fresh

```sql
-- NOT EXECUTED. Separate Step 125 approval A required.
IF @@SERVERNAME<>N'EC2AMAZ-TC26CQK\TELEMED52' OR DB_NAME()<>N'MicroEMR_LocalDev_Fresh'
 THROW 53601,'Unexpected server/database context.',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'microemr_dev' AND sid=SUSER_SID(N'microemr_dev'))
 THROW 53602,'Expected existing mapping missing; stop.',1;
GRANT EXECUTE ON OBJECT::[dbo].[ApplicationUser_GetByAuthSubjectId] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ApplicationUser_Provision] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Appointment_ReportByStatus] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AppointmentHistory_GetByAppointmentUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AuditLog_RecordPatientChartOpened] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[AuditLog_RecordStructuredRead] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CdsAlert_Acknowledge] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CdsAlert_Dismiss] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CdsAlert_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CdsAlert_PatientExists] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CdsAlert_RecordFinding] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CdsAlert_ResolveRuleFindings] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CdsAlertHistory_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ChronicDiseaseEnrollment_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ChronicDiseaseEnrollment_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ChronicDiseaseEnrollment_Inactivate] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ChronicDiseaseEnrollment_ListByPatient] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_AddIssue] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_BeginValidation] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_CompleteValidation] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_FindPatientMatch] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_GetBatch] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_ImportValidatedBatch] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_ListIssues] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_StagePatient] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalDataMigration_StageProblem] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalOutputArtifact_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalOutputArtifact_GetFinalBySource] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicalOutputArtifact_RecordFailure] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicProfile_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ClinicProfile_Save] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CppDisplayPreference_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[CppDisplayPreference_Save] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplate_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplate_GetActive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplate_GetAll] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplate_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplate_SetActive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplate_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateAdmin_Clone] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateAdmin_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateAdmin_SetActive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateAdmin_UpdateMetadata] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateVersion_CreateDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateVersion_GetByTemplateUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateVersion_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateVersion_Publish] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateVersion_Retire] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[DocumentTemplateVersion_UpdateDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Patient_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Patient_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Patient_Search] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Patient_UpdateDemographics] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientAllergy_AssertNoKnownAllergies] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientAllergy_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientAllergy_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientAllergy_GetSnapshot] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientAllergy_Resolve] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientAllergy_RevokeNoKnownAllergies] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientAllergy_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientChartAlert_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientChartAlert_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientChartAlert_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientChartAlert_Resolve] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientChartAlert_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientClinicalHistory_Archive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientClinicalHistory_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientClinicalHistory_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientClinicalHistory_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocument_CommitSignConsultation] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocument_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocument_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocument_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocument_PrepareSignConsultation] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocument_UpdateDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocumentRecipient_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientDocumentRecipient_ReplaceDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_CreateStructured] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_Sign] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_StartFromAppointment] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_UpdateNote] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_UpdateSoapNote] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounter_UpdateStructuredData] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterAddendum_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterAddendum_GetByEncounterUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterContribution_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterContribution_Save] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterDiagnoses_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterDiagnoses_Save] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterHistory_GetByEncounterUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFamilyMedicalHistory_Archive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFamilyMedicalHistory_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFamilyMedicalHistory_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFamilyMedicalHistory_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFamilyMedicalHistory_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFile_Archive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFile_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFile_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFile_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientFile_Restore] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientImmunization_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientImmunization_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientImmunization_ListByPatient] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientImmunization_MarkEnteredInError] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientImmunization_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientMedication_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientMedication_Discontinue] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientMedication_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientMedication_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientMedication_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_Cancel] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_CreateDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_Finalize] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_GetArtifact] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_RecordOutputGeneration] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_SignAndFinalize] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescription_UpdateDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescriptionPrivateNote_Read] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientPrescriptionPrivateNote_RecordDenied] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProblem_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProblem_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProblem_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProblem_Resolve] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProblem_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProviderRelationship_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProviderRelationship_End] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProviderRelationship_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProviderRelationship_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientProviderRelationshipType_ListActive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_Close] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_GetActiveProviders] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_GetProvider] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_MarkResponseReceived] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_MarkSent] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_Send] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_SetFollowUpDue] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_SetResponseDocument] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferral_UpdateDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferralArtifact_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferralClinicalSelection_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferralClinicalSelection_ReplaceDraft] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferralDocument_GetByReferralUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferralDocument_Link] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientReferralDocument_Unlink] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_Correct] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_GetHistory] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_GetUnreviewed] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_GetUnreviewedCount] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_MarkEnteredInError] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_MarkReviewed] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientResult_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientRiskFactor_Archive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientRiskFactor_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientRiskFactor_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientRiskFactor_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientRiskFactor_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_Complete] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_GetOpenForDashboard] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_GetOverdue] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_GetOverdueCount] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_Reopen] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientTask_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientVital_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientVital_GetByPatientUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientVital_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientVital_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_EligibleApplicationUsers_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_Get] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_LinkApplicationUser] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_List] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_SetActive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_UnlinkApplicationUser] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[Provider_Update] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_Cancel] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_CreateWithCriticalFlag] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_GetByUid] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_GetByUidWithCriticalFlag] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_GetMonthSummary] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_MarkArrived] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_Reschedule] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_UpdateStatus] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleAppointment_UpdateWithCriticalFlag] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[ScheduleResource_GetActive] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[SchedulingBlockedTime_Cancel] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[SchedulingBlockedTime_Create] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[SchedulingBlockedTime_GetForDateRange] TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[TenantDatabaseIdentity] ([TenantUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ApplicationUser] ([UserId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ApplicationUser] ([UserUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ApplicationUser] ([Username]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ApplicationUser] ([DisplayName]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ApplicationUser] ([IsActive]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ApplicationUser] ([AuthSubjectId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[AuditLog] ([PatientId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[AuditLog] ([EntityName]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[AuditLog] ([ActionName]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[AuditLog] ([EntityId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([PatientId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([PatientUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([IsDeleted]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([LastName]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([FirstName]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([HealthCardNumber]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([DateOfBirth]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([GenderIdentity]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[Patient] ([ChartNumber]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientDocument] ([PatientId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientDocument] ([PatientDocumentUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientDocument] ([PatientUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientDocument] ([IsDeleted]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientEncounter] ([AppointmentUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientEncounter] ([EncounterUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientEncounter] ([EncounterStatus]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientReferral] ([ReferralUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientReferral] ([PatientUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientReferralDocument] ([ReferralUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([StartDateTimeUtc]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([EndDateTimeUtc]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([PrimaryResourceId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([RoomResourceId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([IsDeleted]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([AppointmentStatus]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([AppointmentUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([PatientUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([AppointmentType]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([Reason]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([ScheduleAppointmentId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([IsCritical]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleAppointment] ([IsAdHoc]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleResource] ([ResourceId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleResource] ([ResourceUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[ScheduleResource] ([DisplayName]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[SchedulingBlockedTime] ([StartDateTimeUtc]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[SchedulingBlockedTime] ([EndDateTimeUtc]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[SchedulingBlockedTime] ([IsActive]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[SchedulingBlockedTime] ([ResourceUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([PatientResultId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([PatientResultUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([PatientUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ResultType]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ResultName]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ResultDate]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ResultSummary]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ResultValue]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ResultUnit]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ReferenceRange]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ResultStatus]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ReviewedAt]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ReviewedBy]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ReviewNote]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([CreatedAt]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([CreatedBy]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([UpdatedAt]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([UpdatedBy]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([RowVersion]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([LifecycleStatus]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([SourceType]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([SourceOrganization]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([SourceSystem]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ExternalResultId]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([ReceivedAtUtc]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([Abnormality]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([PreviousResultUid]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([EnteredInErrorAtUtc]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([EnteredInErrorBy]) TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientResult] ([EnteredInErrorReason]) TO [microemr_dev];
```

Only missing GRANT statements are proposed; retain the existing public protected denials. Application access-management EXECUTE rights allow the trusted API to carry out its existing authorized workflows; they do not grant private-note access to any clinical account. Clinical assignments have their own approval C. Step 124B contains the source justification and full allowlist; no need for schema-wide EXECUTE, fixed data roles or migration rights.

## 6. Fictional fixture readiness

The only candidate names inspected were the explicitly fictional Step 122 fixtures. No general patient/user enumeration or private-note text lookup. Queries read candidate identifiers/actor state and prescription identifiers/status only, no clinical content or mutation/audited-read procedures.

| Prerequisite | Targeted actual result | Status / required decision |
| --- | --- | --- |
| Morgan Private-Note Test patient | No live Patient row with FirstName Morgan and LastName Private-Note Test in T | BLOCKED: approve creation or identify an existing explicitly fictional candidate |
| TEST ONLY Prescriber A | No matching ApplicationUser display name | BLOCKED: actual test subject/active actor/provider not identified |
| TEST ONLY Reader B | No matching ApplicationUser display name | BLOCKED: actual test subject/active actor not identified |
| TEST ONLY Chart Reader C | No matching ApplicationUser display name | BLOCKED: actual test subject/active actor not identified |
| Draft / Finalized prescriptions for that named patient | No matching identifiers/status rows | BLOCKED: approve fictional Draft setup and subsequent finalization, or nominate existing fictional records |
| Selected-tenant private profile grants | 0 Prescriptions.ViewPrivateNotes profile-permission rows | BLOCKED for A/B entitlement |
| Selected-tenant private overrides | No rows for that permission | BLOCKED for A/B entitlement |
| Candidate-specific membership/provider/effective permissions | No actual candidate subjects found; no invented IDs | DEFERRED/BLOCKED pending named candidates |
| Running matching API/Web | No identifiable local process | BLOCKED |

Absence under these names does not prove that no suitable fictional records exist elsewhere. None has been selected/verified for this scenario, and no real record will be substituted. No Auth subject, Provider UID, patient UID or prescription UID is invented in this report.

Exact separately approved setup through existing workflows:

- Identify existing fictional Auth accounts or separately scope account creation; User Administration -> user Details/Manage Access for Active tenant membership/clinical provisioning. A requires active mapped Provider via Providers -> Add/Link User if absent. This may write Auth/P/T; Auth writes require explicit named scope, not implied approval from this report.
- A: effective Patients.View + Prescriptions.Prescribe + Prescriptions.ViewPrivateNotes; B: Patients.View + Prescriptions.ViewPrivateNotes without prescribing for this reader fixture; C: Patients.View without private read. Use approved individual overrides, not blanket role/private access. Membership/actor must be active and test-user identities verified.
- Patients registration for Morgan Private-Note Test; then A-owned fictional Draft through Medications/Prescriptions. Capture real generated IDs only after approved setup. A safe path to finalize exists in the existing chart UI; finalization is separately approved clinical test work, not performed here.

## 7. V1-V3 executable manual checklist - all DEFERRED

Use the exact Step 122 navigation and fictional sentinel labels, after grants/runtime/actor/patient readiness and separate approval F for acceptance writes/read-audit/output operations. Save/clear/finalize and even read/denial/output workflows can write clinical/audit records; no scenario was executed in Step 125. Do not create notes, read protected content, print/export or generate artifacts under a read-only approval.

| Check | Actor/navigation and actions | Expected observation | Actual |
| --- | --- | --- | --- |
| V1 create/reopen | A -> Patients -> Search -> Morgan Private-Note Test -> Medications -> Prescriptions -> New Prescription. Use Step 122 test medication/directions/quantity/date; Notes to pharmacist TEST ONLY PHARMACIST 122; Private Note TEST ONLY INTERNAL NOTE 122. Save Draft -> row Details -> Edit draft | Separate Private Note editor visible to A; saved text persists; ordinary fields unchanged | DEFERRED |
| V1 edit | Change private text to TEST ONLY INTERNAL NOTE 122 EDIT -> save -> reopen Details/editor | Edited text persists, understandable feedback | DEFERRED |
| V1 clear | Clear editor -> save -> reopen Details/editor | Not recorded/blank current editor; retained history; no stale text | DEFERRED |
| V1 preparation for V2/V3 | Restore initial private sentinel on approved Draft and save | Known fictional note available for later comparisons | DEFERRED |
| V2 A/B internal | A reads Details; fresh B session -> same patient -> Medications/Prescriptions -> row Details | Same note/history internally for A/B; B read-only, no private editor/Edit draft | DEFERRED |
| V2 C | C -> same fictional patient's row Details | No private-note content/control visible; use automation for enforcement beyond visible UI | DEFERRED |
| V2 finalized | A -> chart row Sign and finalize prescription -> existing attestation -> Details; B opens Details | Finalized note read-only; no edit control for either actor | DEFERRED |
| V3 internal baseline | Authorized A/B Details on that newly finalized fictional prescription | Note visible internally before checking absence elsewhere | DEFERRED |
| V3 prescription | Row View/Print -> PDF -> print preview/approved test printing | Private sentinel/label absent in PDF and print; pharmacist notes/directions/indication retain established presentation | DEFERRED |
| V3 CPP | Summary -> Current Prescriptions; Print CPP -> approved test clinician -> only Current Prescriptions -> print selected categories | No private note on CPP screen/selected-print preview/output; public fields unchanged | DEFERRED |
| V3 browser print | Browser print preview of protected Details | Private section/history suppressed | DEFERRED |

No historical signed output is modified. Finalize only the newly approved fictional record. Optional already-existing correction Draft remains optional; do not create it as an extra prerequisite. Record exact failing actor/navigation/action and safe fictional evidence for observed FAIL. USER-REPORTED PASS requires explicit completed observations; never infer visual PASS from automated or metadata evidence. No visual scenario currently PASS or FAIL.

### Reused automated assertions, no repeat testing

| Assertion | Existing Step 121 automated evidence | Remaining human observation |
| --- | --- | --- |
| Create/edit/clear/omission, revision/audit atomicity | CreateIsAtomicAndNullRemainsHonest; EditOmissionClearAndRetainedHistoryUseOneParentVersion; audit/revision rollback cases | V1 editor/feedback/save/reopen/clear |
| Original-author-only edit; authorized B; C/inactive/scope rejection | AuthorizedNonPrescribingStaffReadsButCannotWriteAndOtherPrescriberCannotEdit; ForbiddenReadsAndWritesHaveNoPrivateSuccessAudit; CrossPatientAndBothTenantDirectionsRejectWithoutForeignReadAudit | V2 visible section and controls only |
| Finalized immutability/correction/retention | FinalizationAndEditRaceSerializeAndProtectAllOutwardArtifacts; CorrectionsHaveIndependentAuthorsNoCopyAndRetainSupersededOriginal; cancellation/retained artifact tests | V2 finalized read-only appearance |
| Read/denial audit and content confidentiality | ClinicalAuditCorrelationAndRepresentativeReportCsvExcludeProtectedContent; ReadAuditFailureReleasesNothingAndOrdinaryLogsAndAuditsContainNoText; existing MissingPermission cases | No manual audit/security penetration duplicate |
| Shared response/CPP/print/PDF/frozen artifact exclusion | SharedResponsesCppSelectedPrintAndLegacyArtifactsNeverAcquirePrivateContent; both-finalizer artifact/output tests; Web/frontend print-container checks | V3 actual readable PDF/print/CPP appearance and exclusion |

No concrete missing automated assertion was found in this bounded reuse review; no new test or broad regression proposed. Automated fixture coverage does not prove live SQL grants, OIDC/session mapping, runtime version or human appearance.

## 8. Read-only verification SQL and execution record

All queries target P/T on the exact server. No migration/schema recheck, clinical stored procedure or protected-content SELECT. Missing GRANT proposals are confined to section 5, not this read-only section.

### Q1 - existing principal/mappings/explicit rights

Principal: authorized metadata reviewer, not a claim of microemr_dev authentication. Run separately in P and T:

```sql
SELECT @@SERVERNAME AS ServerIdentity,DB_NAME() AS DatabaseContext;
SELECT name,type_desc,is_disabled FROM sys.server_principals WHERE name=N'microemr_dev';
SELECT r.name AS ServerRole FROM sys.server_role_members m
JOIN sys.server_principals r ON r.principal_id=m.role_principal_id
JOIN sys.server_principals p ON p.principal_id=m.member_principal_id WHERE p.name=N'microemr_dev';
SELECT p.permission_name,p.state_desc FROM sys.server_permissions p
JOIN sys.server_principals u ON u.principal_id=p.grantee_principal_id WHERE u.name=N'microemr_dev';
SELECT name,type_desc,CASE WHEN sid=SUSER_SID(N'microemr_dev') THEN 1 ELSE 0 END AS MatchesLoginSid
FROM sys.database_principals WHERE name=N'microemr_dev' OR sid=SUSER_SID(N'microemr_dev');
SELECT r.name AS DatabaseRole FROM sys.database_role_members m
JOIN sys.database_principals r ON r.principal_id=m.role_principal_id
JOIN sys.database_principals p ON p.principal_id=m.member_principal_id WHERE p.sid=SUSER_SID(N'microemr_dev');
SELECT p.class_desc,p.state_desc,p.permission_name,OBJECT_NAME(p.major_id) AS ObjectName,p.minor_id
FROM sys.database_permissions p JOIN sys.database_principals u ON u.principal_id=p.grantee_principal_id
WHERE u.sid=SUSER_SID(N'microemr_dev');
```

Executed YES; mapping/enabled/no-role checks PASS, CONNECT-only baseline confirmed. Expected after approved grants: only section 5 reviewed object/column additions; no broad roles/rights. Unexpected excess requires separate investigation, no automatic revoke/escalation.

### Q2 - effective login token, no clinical procedure calls

Actual execution used a reviewer authorized to impersonate login, in an isolated connection per DB, with TRY/CATCH/REVERT and disposal. It checked every literal Step 124B procedure and column, not just the representative samples below. If reviewer impersonation is unavailable, connect directly as microemr_dev through the local credential UI; do not request passwords in chat or grant impersonation to perform this check.

```sql
BEGIN TRY
 EXECUTE AS LOGIN=N'microemr_dev';
 SELECT ORIGINAL_LOGIN() AS SessionAuthenticator,SUSER_SNAME() AS EffectiveLogin,
 USER_NAME() AS EffectiveDatabaseUser,DB_NAME() AS DatabaseContext,
 IS_SRVROLEMEMBER('sysadmin') AS IsSysadmin,IS_MEMBER('db_owner') AS IsOwner,
 IS_MEMBER('db_ddladmin') AS IsDdlAdmin,IS_MEMBER('db_datareader') AS IsReader,
 IS_MEMBER('db_datawriter') AS IsWriter;
 -- T samples; P uses its existing literal procedure list from Step 124B.
 SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','EXECUTE') AS Allowed
 FROM(VALUES(N'dbo.PatientPrescription_CreateDraft'),(N'dbo.PatientPrescriptionPrivateNote_Read'),
 (N'dbo.PatientPrescriptionPrivateNote_RecordDenied')) v(ObjectName);
 SELECT HAS_PERMS_BY_NAME(N'dbo.TenantDatabaseIdentity','OBJECT','SELECT',N'TenantUid','COLUMN') AS AllowedIdentity;
 SELECT v.ObjectName,HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','SELECT') AS CanSelect,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','INSERT') AS CanInsert,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','UPDATE') AS CanUpdate,
 HAS_PERMS_BY_NAME(v.ObjectName,'OBJECT','DELETE') AS CanDelete
 FROM(VALUES(N'dbo.PatientPrescriptionPrivateNote'),(N'dbo.PatientPrescriptionPrivateNoteRevision')) v(ObjectName);
 SELECT HAS_PERMS_BY_NAME(N'dbo.PatientPrescriptionPrivateNote_Write','OBJECT','EXECUTE') AS CanExecuteHelper;
 SELECT permission_name FROM sys.fn_my_permissions(NULL,'DATABASE');
 REVERT;
END TRY
BEGIN CATCH
 IF SUSER_SNAME()<>ORIGINAL_LOGIN() REVERT;
 THROW;
END CATCH;
```

Executed YES with full allowlists, section 2 results. Protected 0/0 checks PASS; functional permissions BLOCKED (all 308 required checks 0). No NULL; later NULL/missing-object result is BLOCKED rather than PASS. Expected after approved A: allowlisted E/S=1, protected eight DML checks=0, helper=0, broad role flags=0. Effective metadata inspection is not password/login authentication, clinical authorization or audited clinical read testing.

### Q3 - runtime attribution

Principal: authorized reviewer; P connection; no SQL text or private data collected:

```sql
SELECT s.session_id,s.login_name,s.host_name,s.host_process_id,s.program_name,
 DB_NAME(s.database_id) AS DatabaseContext
FROM sys.dm_exec_sessions s
WHERE s.is_user_process=1 AND s.session_id<>@@SPID
AND (s.login_name=N'microemr_dev' OR
 (s.host_name=HOST_NAME() AND s.database_id IN(DB_ID(N'MicroEMR_Platform'),DB_ID(N'MicroEMR_LocalDev_Fresh'))));
```

Executed YES; no microemr_dev session, generic sa/SSMS sessions only, .Net PID not locally attributable. Runtime identity remains BLOCKED. No process launch or diagnostic API/chart request attempted.

### Q4 - named fictional fixture readiness

Principal: authorized reviewer; T:

```sql
SELECT PatientUid FROM dbo.Patient WHERE IsDeleted=0 AND FirstName=N'Morgan' AND LastName=N'Private-Note Test';
SELECT u.UserId,u.UserUid,u.DisplayName,u.AuthSubjectId,u.IsActive,u.ProviderId,p.ProviderUid,p.IsActive AS ProviderIsActive
FROM dbo.ApplicationUser u LEFT JOIN dbo.Provider p ON p.ProviderId=u.ProviderId
WHERE u.DisplayName IN(N'TEST ONLY Prescriber A',N'TEST ONLY Reader B',N'TEST ONLY Chart Reader C');
SELECT x.PrescriptionUid,x.PatientUid,x.PrescriberUserId,x.Status
FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid
WHERE p.IsDeleted=0 AND p.FirstName=N'Morgan' AND p.LastName=N'Private-Note Test'
AND x.Status IN(N'Draft',N'Finalized');
```

Principal: authorized reviewer; P:

```sql
SELECT COUNT(*) AS PrivateProfilePermissionRows FROM dbo.AccessProfile p
JOIN dbo.AccessProfilePermission pp ON pp.AccessProfileUid=p.AccessProfileUid
WHERE p.TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2'
AND pp.PermissionKey=N'Prescriptions.ViewPrivateNotes';
SELECT OverrideState,COUNT(*) AS OverrideRows FROM dbo.UserPermissionOverride
WHERE TenantUid='F94C6ECD-4417-483E-8A94-0507CF8384C2'
AND PermissionKey=N'Prescriptions.ViewPrivateNotes' GROUP BY OverrideState;
```

Executed YES: three T queries returned zero rows; P returned count 0/no overrides. Expected ready state requires actual verified fictional IDs, A provider/active actor, suitable A-owned Draft and appropriate grants; none established. Readiness BLOCKED, not an observed UI defect. No Auth query, protected text or unrelated patient/user lookup.

## 9. Separate approvals and remaining blockers

These **Step 125 A-F labels supersede earlier label letters only for this next work**, not retroactive authorization. 0085/027 are excluded; already verified and must not be replayed.

| Approval | Exact scope requiring explicit decision | Current status / verification and recovery |
| --- | --- | --- |
| A: missing SQL grants | Section 5 exact 27 P EXECUTE + 201 T EXECUTE + 80 T column SELECT for existing microemr_dev on exact server | BLOCKED for runtime until approved/applied; effective checks before/after; per-DB permission transaction/prior snapshot and reviewed reverse delta; no login/user/Auth/secondary-tenant change |
| B: runtime configuration | Only named API PlatformDatabase and development:MicroEMR_LocalDev_Fresh secret setting to verified identity; keep Auth/deployment credentials separate | Actual process/provider unestablished; identify first, preserve secure prior settings for separately approved rollback; no current switch |
| C: clinical permissions | Explicitly chosen fictional Auth subjects, Active primary-tenant membership and only necessary A/B permissions; C stays without private read | Candidates absent/unselected; audited existing individual overrides, no role-wide grants; retain audit on any approved reversal |
| D: fixtures | Named fictional accounts if needed (Auth/P/T scope separately specified), provider link, Morgan patient and A-owned Draft | Names currently absent; actual IDs only after approved UI creation; no real data, preserve records/revisions; no physical delete |
| E: restart | Exact identified nonproduction API/Web process/launcher if needed; Auth startup excluded | Not approved; preserve settings/binaries and separately approve rollback. No automatic launch/restart to inspect |
| F: V1-V3 | Only approved fictional chart; note create/edit/clear/finalize plus associated read/denial audit and output-generation/printing operations | DEFERRED; prereqs A-E as applicable, exact section 7 cards. Preserve finalized artifacts/history; no historical modification |

No combined blanket approval requested. Backup/recovery qualifications from Step 124B remain unverified; read-only security results do not certify recovery or a whole-application least-privilege state. To avoid unnecessary accounts/data, user may nominate existing explicitly fictional actors/patient/records for targeted verification before D. Do not invent IDs or substitute a real chart.

## 10. Step 126 and certification

Recommended **Step 126: obtain scoped A approval and apply only the missing SQL grants; verify microemr_dev, identify the real API/Web launcher/provider, then obtain B/E separately for an intentional runtime transition.** Identify or approve C/D fictional fixtures, and F acceptance operations only after runtime/permissions are ready. Auth least privilege/startup migration separation remains a separate decision. No broad security/manual/regression campaign needed absent a concrete failure.

Retain Step 121 **185 .NET PASS, 12 frontend PASS, TypeScript PASS, final build 0 warnings/errors** and all failure/correction/fixture limits; prior 027 and both-tenant 0085 metadata PASS; Generic Name/Date Precision and earlier deployment qualifications; unresolved DE09, Immunization/allergy and Family History M7/M8 deferred evidence. Current PASS covers narrowly stated effective SQL restrictions/metadata, not V1-V3 or actual runtime authentication. **PC07.01 PARTIAL; 10 mandatory IDs / three packages; DE09.019 IMPLEMENTED - RUNTIME/VISUAL EVIDENCE PENDING.**

Checks performed: targeted read-only metadata/effective-token/fixture-name queries, sanitized configuration and non-mutating process/session inspection, prior inventory/evidence reuse, new-report review/whitespace check. No deployment repetition, build or test run. Only this report added; earlier reports unchanged. **STOP after Step 125. No write, grant, configuration, fixture, restart or acceptance execution is authorized by this report.**
