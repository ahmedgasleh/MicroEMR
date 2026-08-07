# Database object map

Only repository-visible/source-defined objects are included. Procedures are grouped by prefix where their individual operation names are evident in scripts.

## Auth database

| Object | Role | Used by | Operation |
|---|---|---|---|
| Identity tables represented by `ApplicationDbContext` | users, roles, credentials/session support | Identity managers/Auth controllers | read/write via EF Core |
| OpenIddict EF tables | applications, authorizations, scopes, tokens | OpenIddict server | read/write via OpenIddict EF stores |

## Platform database

| Object | Role / relationship | Repository/service | Operation |
|---|---|---|---|
| `Tenant` | clinic registry | `SqlTenantCatalog`, platform admin | lookup/list/create/status |
| `TenantDatabase` | tenant-to-database assignment | `SqlTenantDatabaseResolver`, provisioning | lookup/provisioning lifecycle |
| `UserTenantMembership` | Identity subject membership | `SqlUserTenantMembershipRepository`, Auth membership service | active lookup/add/status/default |
| `UserTenantRole` | roles within membership | membership/role repositories | list/add/remove/replace |
| `PlatformAuditEvent` | platform administration audit | platform admin SQL services | write/read as scripted |
| `Tenant_GetByUid/GetByKey`, `TenantDatabase_GetByTenantUid` | tenant/database reads | tenant catalog/resolver | read |
| `UserTenantMembership_GetActiveByUserId/GetActiveByUserAndTenant` | auth/API validation | membership services | read |
| `PlatformTenant_*`, `PlatformTenantDatabase_*` | platform administration | platform admin services | read/write |
| `PlatformMembership_*`, `PlatformTenantRole_*` | membership lifecycle/roles | admin repositories | read/write/audit |

## Tenant metadata and security tables

| Object | Role | Used by |
|---|---|---|
| `TenantDatabaseIdentity` | binds physical DB to one TenantUid | `TenantSqlConnectionFactory` identity query |
| `SchemaMigration` | applied migration ledger | provisioning/migration status services |
| `ApplicationUser` | tenant clinical actor, linked by AuthSubjectId | `ClinicalUserRepository`, clinical procedures |
| `UserRole`, `UserPermission` | tenant-local legacy/security data | visible in schema; current primary role authorization comes from platform claims (**REVIEW POINT**) |
| `AuditLog` | clinical mutation audit | multiple stored procedures/services |

## Tenant clinical feature objects

| Feature | Tables | Stored procedures called/defined | Main repository |
|---|---|---|---|
| Patients | `Patient` | `Patient_GetByUid`, `Patient_Search`, `Patient_Create`, `Patient_UpdateDemographics` | `PatientRepository` |
| Allergies | `PatientAllergy`, `AuditLog` | `PatientAllergy_GetByPatientUid/GetByUid/Create/Update/Resolve` | `PatientAllergyRepository` |
| Medications | `PatientMedication`, `AuditLog` | `PatientMedication_GetByPatientUid/GetByUid/Create/Update/Discontinue` | `PatientMedicationRepository` |
| Problems | `PatientProblem` | `PatientProblem_GetByPatientUid/GetByUid/Create/Update/Resolve` | `PatientProblemRepository` |
| Vitals | `PatientVital`, audit | `PatientVital_GetByPatientUid/GetByUid/Create/Update` | `PatientVitalRepository` |
| Results | `PatientResult` | `PatientResult_GetByUid/GetByPatientUid/Create/Update/MarkReviewed/GetUnreviewedCount` | `PatientResultRepository` |
| Tasks/overdue | `PatientTask` | `PatientTask_GetByUid/GetByPatientUid/Create/Update/Complete/Reopen/GetOpenForDashboard/GetOverdueCount/GetOverdue` | `PatientTaskRepository` |
| Chart alerts | `PatientChartAlert` | `PatientChartAlert_GetByUid/GetByPatientUid/Create/Update/Resolve` | `PatientChartAlertRepository` |
| Encounters | `PatientEncounter`, `PatientEncounterHistory`, `PatientEncounterAddendum` | `PatientEncounter_GetByPatientUid/GetByUid/Create/UpdateNote/UpdateSoapNote/Sign/StartFromAppointment`; history/addendum procedures | `PatientEncounterRepository` |
| SOAP templates | `EncounterSoapTemplate` | `EncounterSoapTemplate_GetByUid/GetAll/Create/Update/SetActive` | `EncounterSoapTemplateRepository` |
| Documents | `DocumentTemplate`, `DocumentTemplateVersion`, `PatientDocument`, `PatientDocumentContent`, `DocumentAttachment`, `ClinicalNote` | `DocumentTemplate_*`, `DocumentTemplateVersion_*`, `PatientDocument_*` | document/template repositories |
| Patient files | `PatientFile`, `AuditLog`; bytes external to SQL | `PatientFile_GetByPatientUid/GetByUid/Create/Archive/Restore` | `PatientFileRepository` + `LocalPatientFileStorage` |
| Referrals | `PatientReferral`, `PatientReferralDocument` | referral get/create/status transition and document link/unlink procedures | referral repositories |
| Scheduling | `ScheduleResource`, `ScheduleAppointment`, `SchedulingBlockedTime`, `AppointmentHistory` | `ScheduleResource_GetActive`, `ScheduleAppointment_*`, `SchedulingBlockedTime_*`, `AppointmentHistory_*` | scheduling repositories |
| Clinic configuration | `ClinicProfile` | `ClinicProfile_Get/Save` | `ClinicProfileRepository` |
| Reporting | scheduling/patient tables (**INFERRED from report SQL joins**) | `Appointment_ReportByStatus` | `AppointmentStatusReportRepository` |

## Initial-schema objects with uncertain active paths

`Provider`, `ClinicLocation`, `ClinicResource`, `AppointmentStatus`, `AppointmentType`, legacy `Appointment`/`AppointmentResource`, `ProviderAvailability`, and `ScheduleBlock` are present in `initial.sql`. Separate current scheduling tables also exist. Their coexistence is a **REVIEW POINT**; this document does not infer that every initial object remains active.

