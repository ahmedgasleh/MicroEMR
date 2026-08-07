# Important object inventory

This inventory selects architecture-bearing objects and groups closely related DTO families rather than listing every request/response class.

| Project | Namespace | Object | Type | Responsibility | Depends On | Used By |
|---|---|---|---|---|---|---|
| Auth | `MicroEMR.Auth` | `Program` | composition root | Identity, EF/OpenIddict, tenant services | Auth DB, Infrastructure | Auth host |
| Auth | `.Data` | `ApplicationDbContext`, `ApplicationUser` | EF context/entity | Identity and OpenIddict persistence/user status | EF Core/Identity | Auth services/controllers |
| Auth | `.Controllers` | `AuthorizationController` | controller | OIDC authorization, claims, tenant continuation, logout | Identity, OpenIddict, tenant services | Web OIDC client |
| Auth | `.Controllers` | `AccountController` | controller | login and tenant-selection UI | Identity, membership/selection services | Browser |
| Auth | `.Services.Tenancy` | `UserTenantResolver` | service | decide resolved/selection-required membership state | membership service | AuthorizationController |
| Auth | `.Services.Tenancy` | `TenantClaimEnricher` | service | add validated tenant and role claims | membership data | AuthorizationController |
| Auth | `.Services.Tenancy` | `DistributedPendingTenantSelectionStore` | service | short-lived selection continuation | distributed cache | Auth controllers |
| Web | `MicroEMR.Web` | `Program` | composition root | MVC, OIDC cookie client, typed API clients/policy | Application contracts, Auth, API | Web host |
| Web | `.Controllers` | `HomeController` | MVC controller | Dashboard schedule, tasks, results | scheduling/task/result clients | Home view |
| Web | `.Controllers` | `PatientsController` | MVC controller | search/create/demographics/chart shell | patient and chart API clients | patient views/TS |
| Web | `.Controllers` | `SchedulingController` | MVC controller | scheduling page and mutations | scheduling client | Scheduling view/TS |
| Web | `.Controllers` | `Patient*Controller` family | MVC controllers | bridge chart AJAX/forms to typed clients | feature API clients | patient TypeScript |
| Web | `.Controllers` | `DocumentTemplatesController`, `EncounterSoapTemplatesController` | MVC controllers | template management | template clients | management views/TS |
| Web | `.Controllers` | `ReportsController` | MVC controller | appointment status report/CSV | report client | report view |
| Web | `.Controllers` | `TenantUserAdministrationController`, `ClinicConfigurationController` | MVC controllers | tenant administration | admin/config clients, policy | admin views/TS |
| Web | `.Services.*` | `PatientApiClient` | typed HTTP client | patient endpoints + bearer forwarding | HttpClient, HttpContext | PatientsController |
| Web | `.Services.*` | `SchedulingApiClient` | typed HTTP client | scheduling endpoints + bearer forwarding | HttpClient, HttpContext | Home/Scheduling |
| Web | `.Services.*` | `Patient*ApiClient` family | typed HTTP clients | chart feature API calls | HttpClient, saved access token | Web controllers |
| Web | `.Services.*` | `PatientDocumentApiClient`, `PatientFileApiClient` | typed HTTP clients | documents/templates/files/content | HttpClient | document/file controllers |
| Web | `.Services.*` | `TenantUserAdministrationApiClient`, `ClinicConfigurationApiClient`, `AppointmentStatusReportApiClient` | typed HTTP clients | admin/config/report API | HttpClient | corresponding controllers |
| Web | `ClientApp` | `patient-*.ts` modules | TypeScript | chart list/forms/modals/AJAX | MVC endpoints/DOM | patient views |
| Web | `ClientApp` | `appointment-encounter-linking.ts` | TypeScript | start encounter from schedule | Scheduling MVC action | scheduling view |
| Web | `ClientApp` | `overdue-task-indicator.ts` | TypeScript | one count fetch, accessible badge | PatientTasks MVC action | app layout |
| Api | `MicroEMR.Api` | `Program` | composition root | JWT, authorization, tenant/actor middleware, DI | Application/Infrastructure | API host |
| Api | `.Middleware` | `TenantResolutionMiddleware` | middleware | validate tenant claim/catalog/membership; set context/roles | platform repositories | all authenticated API requests |
| Api | `.Middleware` | `ClinicalUserActorResolutionMiddleware` | middleware | resolve tenant clinical user actor | authenticated clinical accessor | clinical controllers |
| Api | `.Middleware` | `TenantDatabaseExceptionMiddleware` | middleware | safe tenant DB error response | logger | API pipeline |
| Api | `.ClinicalUsers` | `AuthenticatedClinicalUserAccessor` | service | map token subject to active tenant `ApplicationUser` | tenant context, clinical repository | application services/middleware |
| Api | `.Authorization` | `TenantRoleAuthorizationHandler` | handler | tenant-role requirement evaluation | current principal | policies/controllers |
| Api | `.Controllers` | `PatientsController` | API controller | patient search/get/create/update | `IPatientService` | PatientApiClient |
| Api | `.Controllers` | `PatientEncountersController` | API controller | encounter lifecycle/history/addenda | encounter service | encounter client |
| Api | `.Controllers` | `SchedulingController` | API controller | current scheduling workflow | scheduling services | SchedulingApiClient |
| Api | `.Controllers` | `PatientFilesController` | API controller | file metadata/content/lifecycle | patient file service | PatientFileApiClient |
| Api | `.Controllers` | `PatientReferralsController`, `PatientReferralDocumentsController` | API controllers | referral workflow/document links | referral services | referral clients |
| Api | `.Controllers` | `PatientTasksController`, `PatientTaskDashboardController` | API controllers | patient tasks/open/overdue | task repository/overdue service | task client |
| Api | `.Controllers` | `PatientAllergies/Medications/Problems/VitalsController` | API controllers | clinical chart submodules | respective services | feature clients |
| Api | `.Controllers` | `PatientResultsController`, `PatientChartAlertsController` | API controllers | results/review and chart alerts | repositories | feature clients/Dashboard |
| Api | `.Controllers` | `DocumentTemplatesController`, `DocumentTemplateVersionsController` | API controllers | templates/version lifecycle | document services/repos | document client |
| Api | `.Controllers` | `TenantUserAdministrationController`, `ClinicConfigurationController`, `AppointmentReportsController` | API controllers | admin/config/reporting | application services | Web clients |
| Application | `MicroEMR.Application` | `DependencyInjection` | registration module | registers application services | service implementations | API/Auth composition |
| Application | `.Patients` | `IPatientService`, `PatientService`, patient DTOs | service/contracts | patient validation/orchestration | patient repository, actor | PatientsController |
| Application | `.PatientEncounters` | `IPatientEncounterService`, `PatientEncounterService`, DTOs/exceptions | service/contracts | encounter lifecycle | encounter repository, actor | encounter API |
| Application | `.Scheduling` | scheduling service/repository interfaces and models | services/contracts | appointment reads/mutations/status transitions | scheduling repositories, actor | SchedulingController |
| Application | `.PatientDocuments` | document/template services, interfaces, DTOs | services/contracts | document/template workflows | repositories, actor | document APIs |
| Application | `.PatientFiles` | `IPatientFileService`, `PatientFileService`, models | service/contracts | file metadata/storage/audit-safe lifecycle | repo/storage/patient/actor/tenant | files API |
| Application | `.PatientReferrals` | referral/status/document services and DTOs | services/contracts | referral workflow | repositories, actor | referral APIs |
| Application | `.PatientTasks` | `IPatientTaskRepository`, `PatientTaskOverdueService`, task models | contracts/service | task CRUD contracts and overdue scope | clinical actor/repository | task APIs |
| Application | chart feature namespaces | allergy/medication/problem/vital services and DTOs | services/contracts | chart submodule workflows | feature repositories | API controllers |
| Application | `.ClinicalUsers` | `IAuthenticatedClinicalUserAccessor`, `IClinicalUserRepository` | interfaces | clinical actor abstraction | implementation-specific | services/API |
| Application | `.Tenancy` | tenant context/catalog/database interfaces/models | interfaces/models | tenant boundary contracts | Core tenant | middleware/infrastructure |
| Application | `.TenantUserAdministration`, `.ClinicConfiguration`, `.Reporting` | services/contracts | services | tenant admin/config/report orchestration | repositories/actor | API controllers |
| Infrastructure | `MicroEMR.Infrastructure` | `DependencyInjection` | registration module | binds repositories/tenant/platform services | configuration | API/Auth/Tool |
| Infrastructure | `.Tenancy` | `SqlTenantCatalog`, `SqlUserTenantMembershipRepository` | repositories | platform tenant/membership lookup | platform SQL | tenant middleware/Auth |
| Infrastructure | `.Tenancy` | `SqlTenantDatabaseResolver` | repository | resolve tenant database assignment | platform SQL | connection factory |
| Infrastructure | `.Tenancy` | `TenantSqlConnectionFactory` | factory | validate assignment/secret/catalog/database identity | tenant context/resolver/secrets | all clinical repositories |
| Infrastructure | `.Tenancy` | `ConfigurationTenantDatabaseSecretProvider` | service | resolve configured secret reference | configuration | connection factory |
| Infrastructure | `.ClinicalUsers` | `ClinicalUserRepository` | repository | subject-to-clinical-user lookup/provision | tenant SQL | actor/admin services |
| Infrastructure | feature namespaces | `PatientRepository`, `Patient*Repository` family | repositories | execute feature stored procedures/map DTOs | tenant connection factory | services/controllers |
| Infrastructure | `.Scheduling` | `SchedulingReadRepository`, `SchedulingAppointmentRepository` | repositories | scheduling stored procedures | tenant connection factory | scheduling services |
| Infrastructure | `.PatientFiles` | `PatientFileRepository`, `LocalPatientFileStorage` | repository/storage | file metadata SPs and local bytes | tenant SQL/filesystem | PatientFileService |
| Infrastructure | `.Reporting` | `AppointmentStatusReportRepository` | repository | status report SP | tenant connection factory | report service |
| Infrastructure | `.Provisioning` | migration source/runner/status services | services/repos | tenant schema provisioning and status | platform/tenant SQL, scripts | DatabaseTool/platform admin |
| Core | `MicroEMR.Core.Tenancy` | `Tenant`, `TenantStatus` | model/enum | platform tenant primitive | none | application/infrastructure |
| Core | `MicroEMR.Core.Domain` | entity/scheduling interfaces | interfaces | legacy/shared domain shapes | none | limited current consumers |

