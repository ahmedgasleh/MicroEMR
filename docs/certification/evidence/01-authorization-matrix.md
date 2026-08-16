# Step 12 authorization evidence

Classification is limited to repository evidence and does not assert certification or compliance. API policies are authoritative; UI visibility is presentation only.

| Capability | Actual permission | UI evidence | Web/server evidence | API evidence | Result / remaining evidence |
|---|---|---|---|---|---|
| View patients | `Patients.View` | Sidebar/chart links are permission-aware | Patient routes use `RequireWebPermission` | `PatientsController` and patient-scoped controllers require the permission | VERIFIED BY AUTOMATED TEST; live deny test remains |
| Manage demographics | `Patients.Edit` | Edit actions are conditional | create/edit actions require permission | create/update require permission | VERIFIED BY AUTOMATED TEST |
| Manage allergies, medications, problems, vitals and alerts | `ClinicalData.Manage` | actions are conditionally presented | proxy writes require authentication; API is boundary | mutation actions require permission | VERIFIED BY AUTOMATED TEST |
| View encounters | `Encounters.View` | chart navigation is conditional | encounter controller requires permission | controller requires permission | VERIFIED BY AUTOMATED TEST |
| Edit encounters | `Encounters.Edit` | create/edit controls are conditional | create/edit routes require permission | mutation actions require permission | VERIFIED BY AUTOMATED TEST |
| Sign encounters | `Encounters.Sign` | sign action is conditional | sign route requires permission | `SignEncounter` has the specific policy | VERIFIED BY AUTOMATED TEST |
| View scheduling | `Scheduling.View` | scheduler navigation is conditional | scheduling controller requires permission | scheduling controller requires permission | VERIFIED BY AUTOMATED TEST |
| Manage scheduling | `Scheduling.Manage` | selection/movement is disabled without permission | write routes require permission | scheduling mutations require permission | VERIFIED BY AUTOMATED TEST; retain runtime screenshot |
| View/manage referrals | `Referrals.View`, `Referrals.Manage` | chart actions are permission-aware | authenticated proxy | API controller and document-link mutations use separate policies | VERIFIED BY AUTOMATED TEST |
| View/manage documents and files | `Documents.View`, `Documents.Manage` | chart actions are permission-aware | document/file routes protected | read and mutation policies are separated | VERIFIED BY AUTOMATED TEST |
| View/review results | `Results.View`, `Results.Review` | result actions are permission-aware | authenticated proxy | separate view/review policies | VERIFIED BY CODE INSPECTION; runtime deny test needed |
| View/manage tasks | `Tasks.View`, `Tasks.Manage` | task indicators/actions are conditional | authenticated proxy | separate view/manage policies | VERIFIED BY CODE INSPECTION; runtime deny test needed |
| View/export reports | `Reports.View`, `Reports.Export` | export action conditional | reports controller protected | CSV action has export permission | VERIFIED BY AUTOMATED TEST |
| Use/manage templates | `Templates.Use`, `Templates.Manage` | template actions conditional | administration controller protected | template operations use the applicable policy | VERIFIED BY CODE INSPECTION |
| View/manage users | `Users.View`, `Users.Manage` | admin navigation/actions conditional | admin controllers protected | operations use permission policies and tenant-admin constraints | VERIFIED BY AUTOMATED TEST |
| Manage access | `Users.ManageAccess` | access-profile actions conditional | controller protected | role/profile/override mutations protected | VERIFIED BY AUTOMATED TEST |
| Manage clinic settings | `ClinicSettings.Manage` | navigation/action conditional | controller protected | configuration mutation protected | VERIFIED BY AUTOMATED TEST |

## Six baseline authorization failures

All six were obsolete role-policy/cardinality expectations after the intentional migration to granular effective permissions. Production still requires authentication and adds the narrower business permission. Updating assertions to locate the exact permission policy is stronger than counting attributes or expecting the former `TenantClinicAdministrator` policy.

| Test | Old expectation | Actual secure architecture | Correction / disposition |
|---|---|---|---|
| `UserAdministrationStabilizationTests.AllApiOperationsShareTenantAdministratorAuthorization` | one class role policy | class authentication; actions use `Users.View`, `Users.Manage`, or `Users.ManageAccess` | test now checks representative action-specific policies; A |
| `TemplateAdministrationWebTests.AdministrationControllerRequiresAuthentication` | exactly one authorization attribute | authentication plus `Templates.Manage` | test now requires both; A |
| `ClinicConfigurationWebTests.ControllerAndNavigationUseSameNarrowTenantAdminClaimAsApi` | one tenant-admin policy | authentication plus `ClinicSettings.Manage` | test now requires effective permission; A |
| `ClinicConfigurationFoundationTests.ApiIsAdminOnlyAndRequestCannotSelectTenantOrChangePlatformOwnedFields` | one tenant-admin policy | authentication plus `ClinicSettings.Manage` | test renamed/corrected; A |
| `AppointmentStatusReportTests.ApiAndWebAreTenantAdministratorProtectedAndContractsAreNarrow` | one tenant-admin policy at API/Web | both layers require `Reports.View`; export additionally requires `Reports.Export` | test now locates `Reports.View`; A |
| `TenantUserAdministrationTests.ApiAndWebControllersRequireTenantClinicAdministratorPolicy` | tenant-admin policy on both controllers | API has action-specific user permissions; Web entry requires `Users.View` | test now verifies both layers and representative API actions; A |

In every case application behaviour remained protected, test correction was justified, no production correction was needed, and no interpretation dependency exists. All six corrected tests pass. `ClinicalControllerAuthorizationTests` also passes all six theory inputs unchanged.

## Runtime boundary

Attribute and handler tests do not prove deployed issuer/audience configuration or every UI state. Execute `CERT-SEC-R001`, `R002`, and `R006` from the Step 12 summary and retain 401/403 responses and screenshots.
