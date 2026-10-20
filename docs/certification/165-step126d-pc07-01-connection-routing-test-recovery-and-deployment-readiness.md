# Step 126D - connection routing test recovery and deployment readiness

Date: 2026-10-20 (America/Toronto). **Deployment BLOCKED. Assembly-loading root cause established; unit execution remains blocked by Windows Application Control.** No SQL connection, permission change, active configuration change, restart, migration, fixture, clinical write, commit or remote operation.

## 1. Checkpoint and changes

Branch `main`; HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry: Step 126C's 13 modified tracked files and nine added task files, plus earlier reports 157-163, preserved. Read [Step 126C](164-step126c-pc07-01-runtime-admin-sql-connection-separation.md) first; reused [Step 126B](163-step126b-pc07-01-sql-runtime-identity-and-permission-scope.md) permission scope without regenerating the inventory.

Only three deliverable files changed in this step:

- `src/MicroEMR.Application/TenantUserAdministration/TenantUserAdministrationService.cs`: move the existing selected-tenant membership read before potential Auth identity creation.
- `tests/MicroEMR.Api.Tests/AddTenantUserWorkflowTests.cs`: add `AdministrativePlatformFailureIsRejectedBeforeCreatingIdentityOrMembership` and a failure option on its existing fake membership service.
- This report. No Step 126C routing/configuration source or earlier report overwritten.

The narrow correction prevents missing/unavailable administrative Platform configuration from first leaving a newly created Auth identity. Role validation still precedes both operations; duplicate checks, authorization, audit and stored-procedure writes are unchanged. This does not make a cross-database workflow atomic. Optional clinical provisioning can still return the existing recoverable partial result after membership creation; failures after a successful preflight can still require existing operational recovery. No Auth database configuration/startup change.

## 2. Exact discovery root cause

Test project: `net10.0`; Microsoft.NET.Test.Sdk **17.12.0**, xunit **2.9.2**, Visual Studio adapter **2.4.5**. SDK **10.0.401**; generated runtimeconfig requests .NETCore.App/AspNetCore.App 10.0.0 with installed compatible 10.0.12 runtime. Existing output is `tests/MicroEMR.Api.Tests/bin/Debug/net10.0/`. The case filter names the actual RuntimeAdministrativeConnectionTests class.

A framework-only, ignored local loader probe at `.tmp/step126d-probe` used AssemblyDependencyResolver and AssemblyLoadContext to load the existing test DLL and inspect its types, without constructing fixtures or opening SQL. This exposed the exception hidden by xUnit's generic missing-dependent-assembly warning:

`System.IO.FileLoadException: An Application Control policy has blocked this file. (0x800711C7)`

Affected compiled dependencies include **MicroEMR.Infrastructure.dll, MicroEMR.Api.dll and MicroEMR.Web.dll**. Read-only Code Integrity events **3077 / 3033**, including 2026-10-20 13:48:35 local for the probe and 13:51:41-42 for **testhost.exe**, confirm enterprise signing/policy rejection. Policy ID: `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`. Files exist; Step 126C verified matching Infrastructure artifact hashes. The probe used the deps manifest to resolve their real paths before the operating system rejected loading them.

Classification: **environmental code-integrity enforcement causing dependency-load/discovery failure**. Compilation succeeds; this is not an incorrect filter or an observed test assertion failure. No evidence justifies changing SDK/test packages. No package update, cache purge, framework replacement, artifact relocation to evade policy, allowlist change, signing change or security-control disablement attempted. The probe is a diagnostic, not a second test framework or a substitute test PASS.

Recovery requires the machine/policy owner to authorize these development build artifacts through the organization's approved trust/signing process, or provide an approved test environment. That is outside the local code/test-correction authorization. Do not disable Application Control. The same block may affect loading rebuilt application binaries, so it is also a runtime-activation prerequisite, not solely a test inconvenience.

## 3. Automated evidence

Diagnostic command (no clinical execution):

```powershell
dotnet run --project .tmp/step126d-probe/Probe.csproj --no-launch-profile -- tests/MicroEMR.Api.Tests/bin/Debug/net10.0/MicroEMR.Api.Tests.dll
```

Result: exit 1; loader exceptions above. Temporary SDK-only probe assets/artifacts are ignored, not delivered application code.

One focused retry after the correction:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~RuntimeAdministrativeConnectionTests" --logger "trx;LogFileName=step126d-routing.trx" --results-directory .tmp/step126d -v minimal
```

Build phase succeeded. Discovery again reported `could not find dependent assembly 'MicroEMR.Infrastructure'` and no matching tests. `.tmp/step126d/step126d-routing.trx` contains **total/discovered 0, executed 0, passed 0, failed 0, notExecuted/skipped 0**. All **23 source cases remain BLOCKED**, not 23 framework skips. The process returned exit 0 / TRX Completed despite zero tests; this is **not PASS**. Code Integrity events confirm testhost rejection during this attempt.

The new onboarding regression and existing TenantSqlConnectionFactoryTests, ClinicalRepositoryConnectionTests, DeferredTenantContextTests, TenantUserAdministrationTests, AddTenantUserWorkflowTests and DocumentTemplateVersioningTests remain unexecuted under the same assembly-level blocker. No repeated broad run or SQL-fixture suite attempted. After approved policy recovery, run the 23-case command above and this focused regression command, requiring actual nonzero discovery:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~TenantSqlConnectionFactoryTests|FullyQualifiedName~ClinicalRepositoryConnectionTests|FullyQualifiedName~DeferredTenantContextTests|FullyQualifiedName~TenantUserAdministrationTests|FullyQualifiedName~AddTenantUserWorkflowTests|FullyQualifiedName~DocumentTemplateVersioningTests" -v minimal
```

Final relevant build, run once after corrections and the focused attempt:

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -v minimal
```

**PASS: 0 warnings, 0 errors, 5.80 seconds.** Includes Core, Application, Infrastructure, API, Web, DatabaseTool and test compilation. Build success does not override the execution block. No full API/Auth suites, migrations or SQL tests run.

## 4. Routing/security review

Static checks retain Step 126C's exact operation split. Normal Platform lookups, effective access, entitlement and denial writers still use PlatformDatabase. Normal clinical repositories, provider reads, clinic-profile read, patient-document retrieval and all eight modern template/version authoring entries retain ITenantSqlConnectionFactory. Administrative methods use their separate Infrastructure capability; neither a request parameter nor a SQL exception selects it. There is no normal-to-administrative retry. Mixed repositories switch individual methods, not all reads.

Administrative tenant connections reuse TenantSqlConnectionFactory: selected tenant context -> active Platform assignment -> assigned secret reference -> assigned database validation -> TenantDatabaseIdentity verification. The administrative setting must also match the normal connection's server/catalog. No hardcoded tenant routing or new direct clinical-data path. API registration does not call AddMicroEmrTenantProvisioning; deployment adapters and migration/status services belong to DatabaseTool's explicit composition.

These are code-level boundaries, not a sandbox against arbitrary code executing in the API process: mixed repositories hold both capabilities, and DI alone does not enforce end-user permissions or protect both credentials after process compromise. Controllers/Application checks remain mandatory. No new broad service-locator interface, HTTP privilege selector or clinical interface exposing an administrative factory was introduced.

Private-note SQL entries, protected table/revision and helper exclusions, original-prescriber Draft rules, finalized immutability, denial audit and outward print/PDF/CPP exclusion are unchanged. Existing Step 121 and migration evidence is retained, not reissued as runtime verification. T1-T8 test intentions remain in the original 23 cases; none has execution evidence in this step.

## 5. Administrative dependency/readiness matrix

P = MicroEMR_Platform; T = selected assigned tenant database (approved pilot: MicroEMR_LocalDev_Fresh). **AP** = ConnectionStrings:AdministrativePlatformDatabase; **AT** = AdministrativeTenantDatabaseSecrets:{assignedSecretReference}. Both are lazy on operation, not constructor/startup requirements.

| Operation / actual caller | Configuration / database | Required SQL capability | Existing application authorization | Missing configuration / startup impact |
| --- | --- | --- | --- | --- |
| Access profile/override administration; SqlAccessProfileRepository admin methods | AP / P | Seven excluded procedure EXECUTEs | Users.ManageAccess | Operation throws configuration error; GetEffective stays normal; no admin setting needed merely to construct repository |
| Tenant user list; SqlPlatformMembershipAdministrationService.GetTenantMembershipsAsync | AP / P | PlatformMembership_ListByTenant EXECUTE | Users.View for list/detail | Specific endpoint fails; ListByUser remains normal |
| User creation, membership lifecycle and role management repositories | AP / P | CreateWithInitialRole, Activate, Deactivate, ReplaceRoles EXECUTE | Users.Manage; role changes Users.ManageAccess | Relevant operations fail; new preflight prevents Add User Auth creation when initial admin membership read fails |
| Security review; SqlPlatformSecurityAuditReviewRepository | AP / P | Three excluded search/detail/review-audit EXECUTEs | Platform SecurityAuditView entitlement | Review endpoints fail; required normal denial-audit writers unaffected |
| Clinical-user provisioning; ClinicalUserRepository.ProvisionAsync | AT / T | ApplicationUser_Provision EXECUTE + identity column SELECT | Users.Manage and selected-tenant membership/clinical actor checks | Provision endpoint fails; optional Add User provisioning retains existing partial-result behavior |
| ClinicProfileRepository.SaveAsync | AT / T | ClinicProfile_Save EXECUTE + identity column SELECT | ClinicSettings.Manage | Save fails; ordinary Get remains normal |
| Provider mutations/eligible users; ProviderAdministrationRepository | AT / T | Six excluded EXECUTEs + identity column SELECT | Providers.View plus Providers.Manage | These operations fail; Provider List/Get remain normal |
| Legacy template mutations; PatientDocumentRepository.ExecuteTemplateMutationAsync | AT / T | Three legacy mutation EXECUTEs + identity column SELECT | Templates.Use and ClinicAdministrator | Those mutations fail; modern authoring and patient-document retrieval remain normal |
| Clinical package staging/validation/import; ClinicalDataMigrationRepository | AT / T | Nine excluded EXECUTEs + identity column SELECT | Users.ManageAccess, tenant/clinical actor | Import/validation endpoints fail; this is application data import, not schema deployment |
| Tenant catalog/operator membership, provisioning and schema deployment | Isolated DatabaseTool PlatformDatabase and assigned TenantDatabaseSecrets / P,T | Separately reviewed operator procedures, schema/ledger/status rights as needed | Trusted operator tool, PlatformAdministration:Enabled and ActorId; no public runtime grant | New AP/AT are not required by tool adapters; incomplete isolated operator values can break the tool after API cutover |

Normal application administration uses bounded stored-procedure rights, not SQL db_owner/sysadmin. SQL login/user/role administration and schema deployment remain separate operator activities. CLI SetAuthSubjectId uses the tool's deployment adapter; it is not an extra API administrative grant in this plan.

Static startup/health assessment: API Program's ValidateOnStart options do not resolve AP/AT; DeferredTenantContext is not dereferenced during administrative factory construction. Missing AP/AT therefore do not inherently prevent API startup. The Platform health check uses normal PlatformDatabase and SELECT 1 only: it can be healthy while admin endpoints are unavailable, and it does not verify grants or tenant identity. No administrative background worker was found in the inspected API DI/startup paths. Auth startup/Identity use their separate configuration and remain unchanged. Actual startup/restart has not been tested and Windows policy can independently block binary loading.

Read-only presence checks of the API local user-secret file show normal PlatformDatabase, primary tenant secret and AuthDatabase configured; AP and primary AT absent. The isolated DatabaseTool profile file is absent. Values were neither printed nor edited. These are file-level observations, not proof of running-service environment overrides or effective credentials.

## 6. Permission candidate impact

**No delta:** normal-account candidate remains **273 REQUIRED / 35 EXCLUDED / 0 UNRESOLVED**, exactly [Step 126B section 7](163-step126b-pc07-01-sql-runtime-identity-and-permission-scope.md#7-revised-normal-runtime-grant-candidate---not-executed). No previously included normal grant moves; modern authoring remains normal. No grant script executed or duplicated here.

Administrative API requirements are the existing **15 P + 20 T excluded EXECUTEs** identified in Step 126A's classification, plus CONNECT and **SELECT on dbo.TenantDatabaseIdentity(TenantUid)** in each selected tenant for the administrative factory's mandatory identity query. That column overlaps the normal set; it is not a reason to copy all 80 normal column rights or all 273 permissions. Other administrative entry work uses the existing same-owner procedures, including their audit writes. No extra direct protected-note table/helper rights, blanket SELECT/schema EXECUTE, role membership or grant option.

The administrative identity has not been selected/verified under AP/AT. Prior shared sa configuration is not approval to reuse sa for API administration. No live SQL query establishes current rights; **whether a suitable existing bounded identity already has them is UNKNOWN**. Mapping, current effective permissions, public denials, ownership/dependency drift and any additional deployment-only rights require a separately approved preflight. Do not infer that only the 35 EXECUTEs suffice without CONNECT/tenant identity verification. Operator CLI mutations/schema deployment have their own permissions outside this API manifest; they do not move into microemr_dev.

## 7. Exact controlled configuration preparation

Only future normal pilot targets: existing microemr_dev in P and MicroEMR_LocalDev_Fresh. Retain the assigned primary reference `development:MicroEMR_LocalDev_Fresh`, tenant UID `F94C6ECD-4417-483E-8A94-0507CF8384C2` and reviewed endpoint `192.168.50.1,50013` / SQL instance `EC2AMAZ-TC26CQK\TELEMED52`. Preserve TLS options and all Auth settings.

API development storage: existing Microsoft user-secret store `MicroEMR.Api-local-development` under `%APPDATA%/Microsoft/UserSecrets/<id>/secrets.json`, or separately scoped approved process environment. It is a local development store, not an encrypted production vault. No secret goes in tracked appsettings, command history, logs or this report. Placeholder-only key examples below are **not active configuration**:

```json
{
  "ConnectionStrings:PlatformDatabase": "<reviewed Platform connection using microemr_dev>",
  "TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh": "<reviewed primary tenant connection using microemr_dev>",
  "ConnectionStrings:AdministrativePlatformDatabase": "<bounded administrative connection to the SAME Platform server/catalog>",
  "AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh": "<bounded administrative connection to the SAME primary tenant server/catalog>"
}
```

These are full connection-string settings, not instructions to interpolate a password in source or synthesize a database name from a request. TenantSqlConnectionFactory dynamically resolves a trusted assignment/secret reference and parses its complete connection string; it does not automatically clone the Platform connection. Every additional selected tenant needs its own approved normal/admin secret mapping. A single admin connection string cannot be copied across tenant databases. Secondary local-dev/MicroEMR_Db is outside this pilot permission/configuration change; do not silently switch it or claim whole-system least privilege.

DatabaseTool must have its own operator profile `MicroEMR.DatabaseTool-local-development` or scoped process environment overriding **the same existing keys** PlatformDatabase and TenantDatabaseSecrets:{reference}. It still loads legacy Auth/API sources before those overrides; absent overrides currently preserve compatibility but do not establish isolation. Populate/review both operator target connections before changing shared API secrets; no new production fallback or new deployment account is assumed. Never put deployment credentials in API storage or shared machine-wide environment.

Before approval, validate only sanitized facts: required-key presence, intended login name, server/catalog equality, expected assigned tenant, encryption options, profile/source precedence, absence of deployment fallback in API and no secrets in tracked diff. Then obtain separately authorized effective-token/password-login checks. Report checks as booleans/target identifiers; never emit full strings/passwords. Such future SQL checks and secret writes were not performed here.

## 8. Deployment gates, rollback and Step 126E

**Prerequisite R:** policy owner authorizes test/application build artifacts through approved trust/signing, then execute the 23 routing cases and the targeted onboarding/routing/template regressions with nonzero discovery. No framework replacement or security-control disablement to obtain a green result. No deployment gate opens on build success alone.

| Separate approval gate | Concrete scope and required evidence |
| --- | --- |
| A - SQL grants | Approve exact Step 126B normal SQLCMD block, existing microemr_dev users and only P/primary T. Execute fail-fast only after approval; verify required rights, 35 exclusions and protected storage/helper denials under the runtime token. Respect per-database transaction limits; tenant failure does not undo a committed Platform batch. Do not rerun 0085/027. |
| B - administrative configuration | Approve bounded identity availability/manifest and exact AP/AT target mapping, secure API storage and isolated operator overrides. Any needed account/permission operation requires its own explicit scope; do not default to sa. Verify same-target validation, lazy-failure behavior and no deployment credentials in API configuration. |
| C - runtime activation | After R/A/B pass, explicitly approve changing only the two normal primary development settings to microemr_dev and restarting only affected development services. Keep Auth and secondary tenant unchanged; verify actual process identity and operator configuration isolation. |
| D - functional verification | Separately approve scoped authenticated smoke/denial operations and any fictional records/audit writes. Cover authentication/tenant selection, charts, scheduling, prescriptions, template authoring, user/provider/clinic/security administration, tenant mismatch rejection and expected denied behavior. Green health alone is insufficient. |
| E - PC07.01 acceptance | Separately approve fictional fixtures/accounts and clinical/audit/output operations for V1-V3. Preserve signed/finalized integrity and private-note output exclusions; retain execution evidence before any acceptance claim. |

No gate executed. Rollback for later activation restores the reviewed known-good API/operator configuration and prior application artifact, with a controlled approved restart. Keep prior values in authorized secure storage, retain tenant mappings, clinical authorization, audit, protected denials and clinical history. Do not auto-revoke/drop users or reset schema/migration ledgers; resolve grant partial-commit state by separately reviewed metadata checks. No clinical data discard, security bypass, Git reset/clean or unrelated-change rollback.

Recommended **Step 126E: approved Application Control trust recovery and focused execution**, followed by bounded administrative identity/configuration readiness review. Until policy authorization and tests succeed, operational grant execution, restart and runtime activation remain BLOCKED. SQL account rights, absent admin/tool settings, actual runtime identity and separate Auth privilege review are additional outstanding prerequisites.

Final checks: targeted diff/whitespace review; prior Step 126C work and reports preserved; no SQL/Auth/active settings changed. Certification remains **PC07.01 PARTIAL**, **10 mandatory IDs / three packages**, **V1-V3 DEFERRED**, DE09.019 **IMPLEMENTED - RUNTIME/VISUAL EVIDENCE PENDING**. No test execution, SQL least-privilege or visual acceptance PASS is claimed.
