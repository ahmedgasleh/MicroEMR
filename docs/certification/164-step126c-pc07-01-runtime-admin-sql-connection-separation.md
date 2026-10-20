# Step 126C - runtime/admin SQL connection separation implementation

Date: 2026-10-20 (America/Toronto). **Local implementation; configuration/deployment NOT performed.** PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED.

## 1. Checkpoint and scope

Branch `main`, HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry: no tracked modifications; reports 157-163 already untracked and preserved. Primary reference: [Step 126B](163-step126b-pc07-01-sql-runtime-identity-and-permission-scope.md). No regenerated inventory or change to its 273 REQUIRED / 35 EXCLUDED / 0 UNRESOLVED normal-account classification.

Implemented explicit Infrastructure capabilities for administrative Platform and tenant connections, method-level routing in mixed repositories, and deployment-only DI registrations. No controller, Application contract, authorization policy, clinical stored procedure, SQL grant, migration or Auth source changed. No SQL connections, clinical writes, fixtures, active secret changes, service restarts, commits or remote Git operations.

## 2. Before/after connection routing

| Operation | Before | After |
| --- | --- | --- |
| Ordinary Platform lookup, selected-user membership/effective access, entitlement, denial audit and health | PlatformDatabase | Same unchanged normal setting; no administrative retry |
| Clinical requests, private prescription-note approved entry points, routine clinical audit/output | TenantDatabaseSecrets via ITenantSqlConnectionFactory | Same normal factory and selected assignment |
| All eight modern template-authoring/version entry points | Normal tenant factory | Unchanged normal factory; no elevated template account |
| Access profile administration/overrides; membership activation/deactivation/creation/role replacement; security review | PlatformDatabase | Explicit IAdministrativePlatformConnectionStringProvider |
| Membership list by selected user / list by tenant | Shared PlatformDatabase | ListByUser stays normal; ListByTenant uses administrative provider. Procedure selection is private code, not a request-selected connection |
| Clinical actor lookup / provisioning and operator mapping | Shared tenant factory | Lookup stays normal; Provision/SetAuthSubjectId use administrative capability |
| Clinic profile Get / Save | Shared tenant factory | Get stays normal; Save administrative |
| Provider List/Get / mutations and eligible-user enumeration | Shared tenant factory | List/Get normal; six administrative entry points use administrative capability |
| Legacy template create/update/set-active / ordinary document/template retrieval | Shared tenant factory | Only three legacy mutations administrative; retrieval and clinical document operations stay normal |
| Nine clinical import/validation entry points | Shared tenant factory | Administrative tenant capability; existing Users.ManageAccess API boundary retained |
| Tenant schema deployment/ledger/identity/provisioning status | Provisioning services registered in API and DatabaseTool | Removed from normal API registration; retained by explicit DatabaseTool composition |
| Auth database and startup migrations/seeding | Separate Auth context/configuration | Unchanged; not in this account cutover |

Administrative here means the bounded application capability selected in Step 126B, not a requirement for db_owner/sysadmin/DDL. None of the 35 excluded application procedures justifies giving a deployment credential to normal web requests. Creating/reviewing a suitable account remains outside this task. No new SQL identity was created or assumed available.

## 3. Exact configuration contract and compatibility

Existing active connection values are unchanged. Existing names continue to supply normal API connections:

- `ConnectionStrings:PlatformDatabase`.
- `TenantDatabaseSecrets:{assignedSecretReference}`; no hardcoded tenant name in services.
- Existing AuthDatabase/AuthServerConnection settings remain untouched.

New, deliberately **unpopulated** administrative settings:

- `ConnectionStrings:AdministrativePlatformDatabase`.
- `AdministrativeTenantDatabaseSecrets:{assignedSecretReference}`.

No appsettings or user-secret values were edited, copied or printed. Administrative settings are resolved on use, so constructing ordinary mixed repositories does not require administrative credentials. Missing settings throw a named configuration error; there is **no compatibility fallback** to PlatformDatabase/normal tenant secrets for an administrative API operation, and no normal-to-admin retry after connection failure or permission denial.

Before using the rebuilt API's administrative endpoints after a later restart, an operator must supply these settings under separate approval. Until then those operations fail closed; current running processes were not restarted. Their existing business behavior is preserved in code, but live administrative continuity is **not verified** and must not be claimed without that configuration and operational testing. This prerequisite applies even before a microemr_dev runtime switch.

Administrative configuration must specify the same server and database as the corresponding normal connection. Malformed, blank, attach-file and different-target settings are rejected with sanitized errors; parser exceptions/values are not attached. The tenant wrapper then reuses TenantSqlConnectionFactory's tenant context, active assignment, assigned database and TenantDatabaseIdentity verification. No untrusted request value can choose a credential scope. Equivalent server aliases with different text require configuration alignment; the check intentionally does not guess endpoint equivalence.

DatabaseTool keeps its existing operator paths and uses dedicated deployment adapters, rather than requiring API administrative settings. Its existing user-secrets ID is shared with the API; this dependency was verified, not assumed isolated. Program now adds the optional operator-only `MicroEMR.DatabaseTool-local-development` user-secret profile after the existing sources, then process environment overrides last. With neither override present, existing tool values remain compatible. This compatibility is limited to the explicitly gated operator tool; it is not an API privilege fallback.

Before changing the shared API secrets, populate/verify isolated operator configuration for PlatformDatabase and assigned tenant secrets. Otherwise the tool would inherit the future restricted API values and migrations would fail. Never place deployment overrides in the API secret store or a globally shared environment. Existing PlatformAdministration:Enabled/ActorId gates and provisioning transactions remain. This step does not run DatabaseTool or populate its new profile.

## 4. Changed files and registrations

New Infrastructure files under `src/MicroEMR.Infrastructure/`:

- `Tenancy/AdministrativeConnectionConfiguration.cs`: sanitized validation and matching-target check.
- `Tenancy/AdministrativePlatformDatabaseConnection.cs`: explicit new Platform setting.
- `Tenancy/IAdministrativePlatformConnectionStringProvider.cs`: bounded Infrastructure provider and API implementation.
- `Tenancy/IAdministrativeTenantSqlConnectionFactory.cs`: distinct capability, not an extension of the ordinary factory interface.
- `Tenancy/AdministrativeTenantSqlConnectionFactory.cs`: administrative secret resolver and wrapper over existing tenant verification.
- `Provisioning/DeploymentPlatformConnectionStringProvider.cs` and `DeploymentTenantSqlConnectionFactory.cs`: explicit operator composition adapters using the tool's configuration.

Modified Infrastructure files:

- `DependencyInjection.cs`: normal registration adds both administrative API capabilities and directly retains ConfigurationTenantDatabaseSecretProvider. It no longer calls AddMicroEmrTenantProvisioning. The explicit provisioning registration retains the migration/status/source services and supplies the deployment adapters. AddMicroEmrPlatformInfrastructure supplies the API administrative Platform provider; the tool's subsequent explicit provisioning registration replaces it with the deployment adapter.
- `Tenancy/SqlAccessProfileRepository.cs`: GetEffective stays normal; only administrative methods switch.
- `Tenancy/SqlTenantMembershipLifecycleRepository.cs`, `SqlTenantRoleManagementRepository.cs`, `SqlTenantUserCreationRepository.cs`: explicit administrative provider, lazily resolved.
- `Tenancy/SqlPlatformAdministrationServices.cs`: ListByUser stays normal; ListByTenant and operator membership mutations use the administrative provider, supplied by the correct composition root. Existing tenant operator service remains on its existing tool configuration.
- `SecurityAudit/SqlPlatformSecurityAuditReviewRepository.cs`: administrative review provider; denial writers unchanged.
- `ClinicalUsers/ClinicalUserRepository.cs`, `ClinicConfiguration/ClinicProfileRepository.cs`, `Providers/ProviderAdministrationRepository.cs`, `PatientDocuments/PatientDocumentRepository.cs`: method-level splits in section 2.
- `ClinicalDataMigration/ClinicalDataMigrationRepository.cs`: administrative tenant capability only.

Other changed files: `src/MicroEMR.DatabaseTool/Program.cs` (operator configuration precedence); `tests/MicroEMR.Api.Tests/RuntimeAdministrativeConnectionTests.cs` (focused unit coverage); this report. Total: 13 modified source files, seven new Infrastructure files, one new test file and this report. Earlier certification reports preserved.

## 5. Security and functionality review

No new SQL grants, direct table access, stored-procedure names, broad database roles, clinical helper access or execution-context mechanism. Existing SQL command text, parameters, actor attribution, concurrency, transaction/error translation and authorization checks remain. Modern templates remain ordinary procedure calls with existing owner/scope/role validation; publishing/retirement does not gain direct signed-document access.

Private-note repositories and Draft prescription paths remain on the ordinary factory. Original-prescriber Draft editing, read permission, protected-table/helper denials, audit and finalized immutability are unchanged; no outward-facing print/PDF/CPP path changed. Prior Step 121 and migration postflight evidence is retained, not rerun or relabeled as new proof.

The API's administrative capabilities are Infrastructure-only and not exposed through a clinical factory interface or request flag. They rely on existing authorized controller/Application callers; DI separation does not replace those checks or isolate secrets from compromise of the API process. The normal/API configuration has no deployment adapter registration. No automatic fallback to an operator credential exists.

Remaining prerequisites: administrative identity/grant manifest and required identity/lookup/audit dependencies; approval to populate new settings; isolated DatabaseTool overrides before shared-secret changes; exact grant approval and effective-token checks; actual password login and process-identity evidence; separate Auth security/startup review. No change to the normal-account grant counts or a recommendation to broaden grants to get a green test.

## 6. Automated verification

New unit file defines **23 cases** (15 Facts and eight InlineData cases across three Theories). It covers T1-T8 routing/configuration concerns with fake connection factories and in-memory settings: ordinary reads versus administrative methods, all eight modern template routes, no retry after normal failure, tenant/assignment mismatch rejection before opening SQL, missing/invalid settings and sanitized diagnostics, Auth setting independence, explicit deployment registration, and construction without administrative secrets. These are unit checks, not live SQL permission evidence.

Relevant build command:

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -v minimal
```

**PASS: 0 warnings, 0 errors, 4.12 seconds.** Builds Core, Application, Infrastructure, DatabaseTool, API, Web and tests. An earlier final-build attempt failed on an unsupported AddUserSecrets optional argument; corrected and reran because of that failure. Initial test compilation also found two incorrect test constructor arguments; corrected. No dependency/framework changes made.

Focused test command:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~RuntimeAdministrativeConnectionTests|FullyQualifiedName~TenantSqlConnectionFactoryTests|FullyQualifiedName~ClinicalRepositoryConnectionTests" -v minimal
```

**BLOCKED: no executed test PASS.** After compilation succeeded, xUnit 2.4.5 reported `could not find dependent assembly 'MicroEMR.Api.Tests'` and no matching tests. A diagnostic retry stalled in assembly loading and was canceled. An approved outside-sandbox `--no-build --no-restore` retry also reported missing dependent `MicroEMR.Infrastructure` and discovered no matching tests. Zero discovered tests is not a successful test result, regardless of process exit code. Diagnostics are local ignored `.tmp/step126c-tests*.log` artifacts. No live SQL integration suite was run.

Final direct `dotnet vstest MicroEMR.Api.Tests.dll` attempt from the output directory, with the same three class filters plus TenantUserAdministrationTests, AddTenantUserWorkflowTests, DocumentTemplateVersioningTests and DeferredTenantContextTests, again failed discovery with missing MicroEMR.Infrastructure (0 cases executed). Its console logger argument was mistakenly left unquoted at a semicolon, causing an additional PowerShell command error after the failed discovery; that does not change the lack of executed test evidence. No further repetition or test-framework change made. The built Infrastructure DLL in test output matches the Infrastructure build artifact by SHA-256; stale mismatched copies are not established as the cause.

Final diff/whitespace and configuration-scope checks: tracked changes confined to the listed code; no SQL/Auth/startup/active appsettings changes, no real credentials introduced. User-secret stores were not written by any task operation. No whole-solution/test-suite repetition or database deployment performed.

## 7. Deployment, rollback and Step 126D

Implementation is explicit, but **not deployment-ready** while automated test discovery and administrative configuration remain unresolved. Do not restart the API or switch normal connections based on build success alone.

Recommended Step 126D: resolve the local test assembly-loading problem without changing the test framework or running SQL fixtures; execute these focused unit cases and selected existing tenant-user/template regressions. Review a bounded administrative grant manifest and identity availability. Obtain separate approval for exact settings/targets, isolated operator configuration and the Step 126B normal grant script. Only then execute approved grants, verify required/excluded/protected denials, approve the connection switch and controlled restart, and gather actual runtime identity/functional evidence. Fictional fixtures and V1-V3 remain a separately approved operational scope.

Rollback must preserve unrelated user changes: review/revert only this task's code if needed; no Git reset/clean, credential rewrite, auto-revoke/drop or clinical-history change. For a later approved deployment, preserve reviewed prior secret values securely and restore only that approved configuration if runtime checks fail. Current active services/configuration were not changed, so no operational rollback was performed here.

PC07.01 **PARTIAL**; DE09.019 **IMPLEMENTED - RUNTIME/VISUAL EVIDENCE PENDING**; inventory **10 IDs / three packages**; V1-V3 **DEFERRED**. No SQL least-privilege verification, administrative live continuity or visual acceptance PASS claimed.
