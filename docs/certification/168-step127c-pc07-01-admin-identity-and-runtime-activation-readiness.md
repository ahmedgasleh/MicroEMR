# Step 127C - administrative identity and runtime activation readiness

Date: 2026-10-20 (America/Toronto). **Administrative readiness BLOCKED. Read-only review and preparation completed; activation NOT performed.**

## 1. Git checkpoint and retained evidence

Actual branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a` (`approved SQL permission deployment is complete step127B`). Entry working tree was clean, unlike the earlier checkpoint at `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. This review adds only this report; no existing files were edited or staged. No commit or remote operation occurred.

Read [Step 127B](167-step127b-pc07-01-controlled-sql-grant-application.md) first; reused [126B](163-step126b-pc07-01-sql-runtime-identity-and-permission-scope.md), [126C](164-step126c-pc07-01-runtime-admin-sql-connection-separation.md), [126D](165-step126d-pc07-01-connection-routing-test-recovery-and-deployment-readiness.md) and [127A](166-step127a-pc07-01-final-sql-grant-preflight.md) for bounded routing, permission and test evidence. No regenerated inventory, repeated runtime-grant verification, build or test execution.

Established Step 127B results remain **273/273 required permissions PASS; 35/35 exclusions preserved; 9/9 protected-object checks PASS**. Step 126E remains **24 unique tests PASS**, as recorded in 127A. These are retained evidence, not new application-runtime or password-authentication results.

## 2. Operation and connection matrix

Abbreviations below name exact existing settings and implementations:

| Route | Configuration key | Target / resolver |
| --- | --- | --- |
| P | `ConnectionStrings:PlatformDatabase` | `MicroEMR_Platform`; `PlatformDatabaseConnection` |
| T | `TenantDatabaseSecrets:{assignedSecretReference}` | Assigned tenant database; `ITenantSqlConnectionFactory` / `TenantSqlConnectionFactory` |
| AP | `ConnectionStrings:AdministrativePlatformDatabase` | Same server/catalog as P; `IAdministrativePlatformConnectionStringProvider` / `AdministrativePlatformConnectionStringProvider` |
| AT | `AdministrativeTenantDatabaseSecrets:{assignedSecretReference}` | Same target as T; `IAdministrativeTenantSqlConnectionFactory` / `AdministrativeTenantSqlConnectionFactory` |
| OP | P and T keys in DatabaseTool's effective configuration | Operator-only deployment adapters; separate override profile, not AP/AT keys |
| Auth | `ConnectionStrings:AuthDatabase`, falling back to `ConnectionStrings:AuthServerConnection` for API identity/lookup | Separate Auth database; Auth host itself requires `ConnectionStrings:AuthServerConnection` |

For this development assignment, `{assignedSecretReference}` is exactly `development:MicroEMR_LocalDev_Fresh`; T/AT target `MicroEMR_LocalDev_Fresh`. P/AP and T/AT endpoint is `192.168.50.1,50013`. Expected TenantUid is `F94C6ECD-4417-483E-8A94-0507CF8384C2`. Routes are selected in code, not by a caller-supplied privilege flag.

All C rows below run on authorized requests, resolve AP/AT lazily and fail the affected operation if absent. Administrative credentials are not a startup prerequisite merely because the repository is constructed. CONNECT is required in each applicable database; every AT operation also requires SELECT on **only** `dbo.TenantDatabaseIdentity(TenantUid)` for the factory's identity check. Procedure EXECUTE includes same-owner internal work; it does not imply direct table DML, database ownership or DDL.

| Category / operation | Calling service -> Infrastructure implementation | Route / required SQL operations | Application prerequisite / timing |
| --- | --- | --- | --- |
| A. Ordinary Platform | Tenant catalog/database resolver, membership/effective-access/entitlement services, denial audit, health | P; existing 12 runtime EXECUTEs; health `SELECT 1` | Existing authenticated tenant/permission processing; health on request. Missing P fails dependent resolution/construction or request; health alone cannot prove administration works |
| B. Ordinary clinical tenant | Clinical services including `PatientDocumentService`, actor lookup, private-note service, modern template authoring/version services | T; existing 181 EXECUTEs and 80 column SELECTs, unchanged | Existing tenant membership, clinical actor, permissions and ownership checks; requests. Missing T fails selected tenant connection, not general API construction |
| C. Access profiles and overrides | `AccessProfileService` -> `SqlAccessProfileRepository` | AP: `AccessProfile_List`, `AccessProfile_Get`, `AccessProfile_GetUserAssignments`, `AccessProfile_ReplacePermissions`, `AccessProfile_AssignUser`, `UserPermissionAccess_Get`, `UserPermissionOverride_Set` | `AccessProfilesController`: authenticated + Users.ManageAccess. User-details access methods retain their existing Users.View/Users.ManageAccess endpoint gates. `GetEffectiveAsync` remains P |
| C. User listing and membership lifecycle | `TenantUserAdministrationService` -> `SqlPlatformMembershipAdministrationService`, `SqlTenantMembershipLifecycleRepository` | AP: `PlatformMembership_ListByTenant`, `PlatformMembership_Activate`, `PlatformMembership_Deactivate` | Users.View for list/details; Users.Manage for activate/deactivate; selected tenant, actor, concurrency and last-administrator protections retained |
| C. User creation and role replacement | `TenantUserAdministrationService` -> `SqlTenantUserCreationRepository`, `SqlTenantRoleManagementRepository` | AP: `PlatformMembership_CreateWithInitialRole`, `PlatformMembership_ReplaceRoles` | Users.Manage for Add User; Users.ManageAccess for roles; Auth operations separately configured. Add User checks administrative membership access before creating an Auth identity |
| C. Security review | `PlatformSecurityAuditReviewService` -> `SqlPlatformSecurityAuditReviewRepository` | AP: `PlatformSecurityAudit_Search`, `PlatformSecurityAudit_GetByUid`, `PlatformAudit_RecordSecurityAuditReview` | Authenticated platform SecurityAudit.View entitlement, not a tenant-wide administrator bypass; disclosure/review audit retained |
| C. Clinical actor provisioning | `TenantUserAdministrationService` -> `ClinicalUserRepository.ProvisionAsync` | AT: `ApplicationUser_Provision` | Users.Manage; selected tenant/eligible target identity. Optional Add User provisioning can return existing recoverable partial result after membership creation |
| C. Clinic profile save | `ClinicConfigurationService` -> `ClinicProfileRepository.SaveAsync` | AT: `ClinicProfile_Save` | Authenticated + ClinicSettings.Manage; resolved clinical actor. Get remains T |
| C. Provider administration | `ProviderAdministrationService` -> `ProviderAdministrationRepository` | AT: `Provider_Create`, `Provider_Update`, `Provider_SetActive`, `Provider_EligibleApplicationUsers_List`, `Provider_LinkApplicationUser`, `Provider_UnlinkApplicationUser` | Providers.View plus Providers.Manage for administrative methods; actor for mutations, existing concurrency checks. List/Get remain T |
| C. Legacy template mutations | `PatientDocumentService` -> `PatientDocumentRepository` | AT: `DocumentTemplate_Create`, `DocumentTemplate_Update`, `DocumentTemplate_SetActive` | Templates.Use plus ClinicAdministrator policy; existing template authorization. Retrieval and all eight modern authoring/version methods remain T |
| C. Clinical package validation/staging/import | `ClinicalDataMigrationValidationService`, `ClinicalDataMigrationImportService` -> `ClinicalDataMigrationRepository` | AT: `ClinicalDataMigration_BeginValidation`, `ClinicalDataMigration_FindPatientMatch`, `ClinicalDataMigration_StagePatient`, `ClinicalDataMigration_StageProblem`, `ClinicalDataMigration_AddIssue`, `ClinicalDataMigration_CompleteValidation`, `ClinicalDataMigration_GetBatch`, `ClinicalDataMigration_ListIssues`, `ClinicalDataMigration_ImportValidatedBatch` | Authenticated + Users.ManageAccess; existing actor, validation and clinical audit behavior. This is application data import, not schema deployment |
| D. Tenant/operator administration | DatabaseTool -> `SqlPlatformTenantAdministrationService` | OP/P: `PlatformTenant_List`, `PlatformTenant_GetByUid`, `PlatformTenant_GetByKey`, `PlatformTenant_Create`, `PlatformTenantDatabase_UpsertProvisioning`, `PlatformTenant_SetStatus` | Trusted CLI: PlatformAdministration.Enabled=true and ActorId; confirmations where implemented. Provisioning/operator invocation, not startup. Service itself reads P, not AP |
| D. Operator membership/roles, entitlement changes and actor mapping | DatabaseTool -> `SqlPlatformMembershipAdministrationService`, `PlatformEntitlementService`, `ClinicalUserRepository.SetAuthSubjectIdAsync` / ProvisionAsync | OP: `PlatformMembership_ListByUser`, `PlatformMembership_ListByTenant`, `PlatformMembership_Add`, `PlatformMembership_SetStatus`, `PlatformMembership_SetDefault`, `PlatformTenantRole_Add`, `PlatformTenantRole_Remove`; `PlatformEntitlement_AssignToUser` / `PlatformEntitlement_RevokeFromUser`; tenant `ApplicationUser_SetAuthSubjectId` / `ApplicationUser_Provision` | Same CLI gates, target selection and confirmations; Auth lookup where needed. ListByUser and entitlement repository use P; other membership methods use deployment AP adapter. SetAuthSubjectId is CLI-only and is not one of the 35 API administration grants |
| E. Migration/deployment | DatabaseTool -> `TenantDatabaseMigrationRunner`, `SqlTenantProvisioningStatusRepository`, `TenantMigrationStatusService` / `SqlTenantMigrationStatusReader` | OP: P `TenantDatabase_ProvisioningStarted`, `TenantDatabase_ProvisioningCompleted`, `TenantDatabase_ProvisioningFailed`; T migration batches, schema metadata, `SchemaMigration` reads/inserts, identity read/initial insert, application locks | Explicit operator invocation only. Needs script-specific DDL/DML and metadata rights beyond C; no generic minimum deployment manifest proven here. No CREATE DATABASE call in the inspected runner; it opens an assigned existing database |
| F. Auth identity operations and startup | API `IdentityUserAdministration`, Infrastructure `SqlIdentityUserLookup`; Auth `SeedData` | Auth: Identity/UserManager reads/writes; Auth startup `MigrateAsync` and seeding | Existing authentication, user-administration permissions and Auth configuration. Auth host missing AuthServerConnection prevents startup. No new Auth rights/configuration proposed |

All SQL procedure names in the matrix have schema `dbo`. C totals exactly **15 P + 20 T entry EXECUTEs**, reusing report 163's exclusions. D/E permissions must not be added to that API manifest. D/E services can be registered in shared composition without becoming an authorized API endpoint; DatabaseTool's migration runner/status registrations occur only through explicit `AddMicroEmrTenantProvisioning`.

## 3. Existing identities and narrow SQL evidence

Read-only metadata sessions on the verified endpoint returned server `EC2AMAZ-TC26CQK\TELEMED52`; database contexts only P and T. Existing API P credentials authenticated the reviewer as `sa`; secret values stayed in memory. A sandbox connection first failed with SQL error 5; the authorized read-only check succeeded with expanded network access. Two local PowerShell builder-property errors occurred before their affected connections/queries; corrected index-based assignments succeeded. No security setting changed.

Server query scope: non-system SQL/Windows login names, enabled state, explicit server roles/permissions, and the reviewer's effective server permissions. Database query scope: SID mappings for the eight non-system SQL logins below, named database roles, the retained 35 procedure objects/owners, direct cross-database/server/caller-dependent dependency rows and reviewer effective access. Table-variable INSERTs held literal allowlists only; no persistent SQL write, procedure execution, impersonation or clinical-content read occurred.

| Existing principal(s) | Enabled / role evidence | P/T mapping and suitability |
| --- | --- | --- |
| `sa` | Enabled; sysadmin; effective CONNECT SQL, ALTER ANY LOGIN, ALTER ANY DATABASE and CONTROL SERVER returned | Maps to dbo in P/T; db_owner flag=1 and effective database CONTROL=1. Sufficient but excessive; reject for routine application administration |
| `admin`, `CcDBA`, `ccguest`, `telemedapi`, `vitelflo`, `waexporter` | All enabled; every login has sysadmin membership | No explicit SID-matched database user in P/T. This does not restrict a sysadmin: it accesses as dbo. All excessive; no credential authentication or reuse authorization established |
| `microemr_dev` | Enabled; not sysadmin; no assigned server role returned | Matching SQL_USER in P/T; no assigned database role returned. Retain Step 127B scoped runtime rights/exclusions; never elevate it for C/D/E |
| Existing Windows/SQL-service identities | Operator Windows login is sysadmin; service accounts have system duties and some broad server rights/roles | Not candidate application identities; no service-account credentials or delegation proposed. No bounded integrated-authentication identity verified |

| Narrow check under actual reviewer token | P | T | Interpretation |
| --- | ---: | ---: | --- |
| Administrative manifest procedures present | 15/15 | 20/20 | Existing allowlists, not regenerated inventory |
| Administrative procedures effectively dbo-owned | 15/15 | 20/20 | Consistent with retained same-owner dependency analysis |
| `HAS_PERMS_BY_NAME(..., OBJECT, EXECUTE)=1` | 15/15 | 20/20 | **sa token only**; not bounded-identity PASS |
| Direct cross-database/server or caller-dependent dependencies returned | 0 | 0 | Direct catalog check only; retained dependency/definition evidence in 127A/127B supplies earlier context |
| Tenant identity-column SELECT | N/A | 1 | **sa token only** |

No existing suitable bounded application identity was found. Only sa was authenticated here; the other SQL login role findings are metadata, not password-based effective access tests. Existing sysadmin identities do not justify routine use simply because their privileges suffice. Current local normal API P/T and Auth connections also identify `sa` in sanitized configuration inspection: this remains a development limitation, not a least-privilege solution.

**Stop boundary:** a bounded candidate's authentication, effective allowlist, inherited restrictions and negative checks cannot be verified without an approved candidate. No bounded-access PASS is claimed, and no attempt was made to create or alter one.

## 4. Recommended bounded administrative scope

Gate A should approve a named dedicated application-administration login (name/credential still undecided) mapped only to P and the explicitly approved development tenant T. Existing SQL candidates are unsuitable; a new bounded identity is therefore the current recommendation unless an operator supplies another suitable existing identity for review.

Proposed scope: login/database CONNECT; the **15 P and 20 T exact procedure EXECUTEs in section 2**; T column SELECT on `TenantDatabaseIdentity(TenantUid)`. The normal P identity continues to resolve the tenant assignment, and ordinary actor/lookup calls continue through T, so those dependencies do not justify copying the entire runtime manifest to the administrative login. Internal procedure/audit work should use the established ownership chain, with a targeted definition/dependency review before final grant approval.

Require effective checks for all proposed positives, public/role inheritance, and negatives including private-note storage/helper, DDL, ownership, broad data access, Auth and other tenant access. Retain existing public protected-note DENYs; do not grant around them. No sysadmin, db_owner, db_ddladmin, broad reader/writer roles, schema-wide EXECUTE, impersonation or arbitrary table DML is technically established as necessary for C. The proposal is **not an executed or fully proven grant manifest**. Separate D/E operator authority should remain outside the API process and be reviewed against the actual intended deployment scripts; do not infer a need for blanket sysadmin/db_owner from the runner.

## 5. Exact configuration inventory and missing settings

Inspected appsettings plus Development appsettings and local user secrets for API/Web/Auth, and presence of the isolated DatabaseTool profile. Checked corresponding connection/operator environment overrides in this review process: none present. This is a reconstructed Development file/secret configuration, not a snapshot of another running process, command-line arguments, IDE overrides or an undisclosed launcher.

| Setting / owner | Current observation | Eventual value requirements / missing behavior |
| --- | --- | --- |
| API `ConnectionStrings:PlatformDatabase` | Present; SQL login sa; verified P endpoint/catalog | Later C approval changes only credentials to microemr_dev, preserving endpoint/catalog and reviewed connection options; normal Platform/tenant-resolution prerequisite |
| API `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | Present; SQL login sa; T endpoint/catalog | Later C approval changes credentials to microemr_dev; needed when this assignment is opened |
| API `ConnectionStrings:AdministrativePlatformDatabase` | **Absent** in inspected sources | Same server text/catalog as API P; approved bounded admin credentials; required on AP use |
| API `AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | **Absent** in inspected sources | Same server text/catalog as assigned T; approved bounded admin credentials; required on AT use |
| DatabaseTool isolated `MicroEMR.DatabaseTool-local-development` profile | **Absent** | Optional for current compatibility, operational prerequisite before changing shared API values: isolated operator P and assigned T keys, with separately verified operator rights |
| `PlatformAdministration:Enabled`, `PlatformAdministration:ActorId` | Present in API user-secret store inherited by DatabaseTool; values not disclosed | Tool requires Enabled=true and nonblank ActorId before running; source presence alone does not certify these gates or operator authority |
| API `ConnectionStrings:AuthDatabase` | Present, targets MicroEMR_Auth; AuthServerConnection fallback absent | Preserve exactly; needed by identity lookup/create/reset, independent of P/T/AP/AT |
| Auth host `ConnectionStrings:AuthServerConnection` | Present, targets MicroEMR_Auth | Preserve exactly; required explicitly at host startup. Existing migration/seeding behavior unchanged |
| Auth host `ConnectionStrings:PlatformDatabase` | Present | Separate Auth-host Platform configuration remains unchanged in this bounded API cutover; ordinary Platform access must not silently inherit API changes |
| Web SQL keys | All six inspected SQL settings absent in file/user-secret reconstruction | Web has its own user-secret profile and OIDC/API clients. Web entitlement accessor reads the cookie-protected token. DI's shared Platform registration alone does not establish that Web needs AP/AT. Do not add SQL credentials speculatively; identify any actual SQL consumer/launcher override before activation |

`ConfigurationTenantDatabaseSecretProvider` does not substitute databases in a common administrative string. Each trusted assignment reference resolves a separate AT entry. `AdministrativeConnectionConfiguration` requires a nonblank parseable setting, server/catalog equality with the normal connection (case-insensitive textual comparison), and no AttachDbFilename. Equivalent aliases with different text can fail. The tenant wrapper then checks tenant context, Active matching assignment, assigned database and exactly one matching `TenantDatabaseIdentity` row. A credential reusable at SQL level is not automatically configured for every tenant.

Use existing .NET configuration: Development API secrets ID `MicroEMR.Api-local-development`, or protected environment variables scoped to the actual API launch process:

- `ConnectionStrings__AdministrativePlatformDatabase`
- `AdministrativeTenantDatabaseSecrets__development__MicroEMR_LocalDev_Fresh`
- Later runtime switch: `ConnectionStrings__PlatformDatabase` and `TenantDatabaseSecrets__development__MicroEMR_LocalDev_Fresh`.

Preserve reviewed encryption/trust options; do not weaken TLS to connect. Local user secrets are outside Git but are not an encrypted production vault. Do not put credentials in tracked appsettings/launch profiles, command arguments, report text or shell history. Secret entry requires a separately approved protected local mechanism.

DatabaseTool deliberately shares the API's UserSecretsId. Its sources are Auth appsettings, environment, Auth user secrets, API user secrets, optional isolated tool secrets, then environment again; final in-memory source sets only TenantProvisioning:SqlAssetsPath. Establish the isolated operator profile's **normal P/T key names**, not API AP/AT names, before API shared-secret changes. Do not export operator overrides globally or into the API process. AuthDatabase/AuthServerConnection remain inherited and unchanged. A global environment override can defeat tool-profile isolation and must be checked at activation.

## 6. Startup, endpoint and restart dependencies

No circular dependency found: ordinary P resolves the trusted tenant assignment; T/AT then resolve their respective secrets. AP validates against normal P; AT validates against the assigned normal T value, without connecting through that value as a retry. Missing AP/AT does not participate in inspected API ValidateOnStart checks. Existing upload/scheduling/import option checks are unrelated and remain in place.

Normal P is required when its repositories/resolver/health are resolved; T is required for selected tenant operations. Static construction does not establish successful SQL startup or endpoint behavior. Missing AP blocks administrative listing/security review/access changes. Add User performs the AP membership query before Auth creation, but later Auth/P/T/AT failures can still leave recoverable partial results; these databases do not share an atomic transaction.

Supply administrative configuration **before** restarting the rebuilt API or switching normal credentials if administrative continuity is required. SQL grants are already complete; administrative readiness does not require reapplying them. Runtime password authentication and actual process identity remain unverified.

Source launch profiles identify API https `https://localhost:7003` / `http://localhost:5245` (http profile `5298`) and Web https `https://localhost:7002` / `http://localhost:5002` (http profile `5100`), all Development. A process-name check returned no MicroEMR.Api/Web/Auth executables; dotnet/IDE/service hosting and actual process environments were not established. Gate D must identify the real launcher, effective sources and profile rather than treating these source defaults as live state.

Plan a controlled API restart after approved configuration; Web needs restart only if its configuration/binary or established launch arrangement requires it, and its OIDC/API connectivity must be verified. Do not rely on secret-file reload to update constructor-captured connection strings. Auth need not restart for this API P/T change: its hosted `SeedData.StartAsync` calls MigrateAsync and seeding, so an Auth restart carries separate effects and is excluded from an implicit API/Web restart approval. Existing Web OIDC client-secret requirement remains unchanged.

## 7. Targeted security review and limits

- Ordinary interfaces do not expose administrative connection selection. Mixed repositories select explicit administrative methods; normal reads, modern templates and private-note repository use the ordinary factory. Infrastructure capabilities are DI-accessible within the same trusted process, not a security boundary against arbitrary code or process compromise.
- No normal SQL failure/permission denial triggers an administrative retry; missing AP/AT fails closed. Private procedure-selection code, not HTTP input, chooses ListByUser versus ListByTenant routing.
- Existing controller role/permission/entitlement gates in section 2 remain; selected-tenant assignment and identity validation remain enforced. Platform security review is intentionally entitlement-authorized across its supported scope, not an ordinary tenant clinical connection.
- Existing actor parameters, clinical import audit behavior, concurrency and soft-delete/history protections remain unchanged. No clinical procedure or fixture was executed. Private-note repository stays T; protected storage/helper negative results are retained from 127B, not rerun under a proposed administrative identity.
- API uses AddMicroEmrInfrastructure without AddMicroEmrTenantProvisioning; deployment runner/status services remain explicit tool registrations. Auth's independent existing EF identity/startup migrations are distinct from tenant deployment registration; no new migrations introduced.
- Retained 24 unit tests cover routing/no fallback/configuration/registration and pre-identity Add User failure; they do not prove live authorization, tenant isolation under every identity, administrative continuity or visual acceptance. No new source change justified rerunning them.

Residual risks: currently broad sa-backed local connections; no bounded admin candidate; same-process storage of administrative credentials; unknown real launch environment; untested runtime login credentials and functional behavior; cross-database partial results; and microemr_dev's previously recorded access to MicroEMR_Db (not a verified clinical-read permission). This step does not broaden or remediate those scopes implicitly.

## 8. Separate activation approval gates

Each gate requires its own explicit approval. A successful gate does not authorize the next one.

| Gate | Exact scope and preconditions | Expected result / verification | Rollback and remaining risk |
| --- | --- | --- | --- |
| **A - Administrative identity** | Approve a named identity and exact P/T CONNECT, 15+20 EXECUTEs and tenant identity-column scope after dependency/inheritance review. No suitable existing candidate verified; account creation/grants need explicit authorization | Approved bounded identity; targeted effective positive/negative token checks, mappings, roles/ownership and separate actual authentication check. No provisioning or clinical writes | On failure stop; reconcile only approved identity/grant delta against captured baseline under separate recovery authorization. No automatic drop/revoke. Dependencies and other-database exposure remain unproven until checked |
| **B - Administrative configuration** | A completed; approve exact local AP/AT targets, API secret mechanism and isolated DatabaseTool P/T operator values; preserve secure prior values privately | Presence/parser/target checks and effective launch-source review; API has bounded admin paths and tool keeps separately scoped operator values. No runtime credential switch or restart implied | Restore only approved settings from secure snapshot, including prior absence. Missing settings disable administrative methods; environment precedence may override secrets; no startup/clinical write test implied |
| **C - Runtime switch** | A/B complete; retain 127B grants and target assignment evidence, verify actual microemr_dev authentication safely, identify all affected processes and prior settings | Approve API normal P and this assigned T credential change only. Compare sanitized server/catalog/principal and ensure Auth, AP/AT and operator configuration remain unchanged | Restore only prior approved normal P/T values securely if needed; no SQL revocation. Existing processes may retain old connections until D; grant evidence alone is not process identity |
| **D - Restart and verification** | C complete; separately approve named API/Web launchers/processes, restart scope and exact functional checks, including any administrative write/audit side effects | Controlled restart; startup/health, actual effective runtime and administrative identities, tenant resolution, ordinary requests, role/entitlement denial/no fallback, protected-note boundary and administrative endpoint availability. Use existing approved nonclinical targets or seek separately scoped fixtures; visual/clinical acceptance remains E | Restore approved configuration and restart only affected services under the approved recovery plan. Preserve logs/audits and partial-result evidence; no clinical deletion or automatic migration. Auth restart/seeding excluded unless separately reviewed |
| **E - PC07.01 acceptance** | D complete; separately approve fictional fixtures and exact V1-V3 operations with audit/tenant/role prerequisites | Run only approved acceptance cases; preserve evidence and assess all PC07.01 criteria before any status change | Preserve clinical history/audit; approved soft-delete/correction only, no physical clinical deletion. Acceptance failure retains PARTIAL; do not infer success from administrative readiness |

## 9. Rollback, local sharing and remaining blockers

No operational rollback is needed for this documentation-only step. Later capture previous settings securely outside Git before approved edits; restore only the affected approved configuration and named processes. Never reset/clean the working tree, rewrite migration history, remove clinical records or automatically undo Step 127B's grants. Identity/grant rollback is separately reviewed against the immediate baseline, not an assumed blanket REVOKE or account drop.

This step's sole tracked-content addition contains configuration names and sanitized principal/target evidence, no credentials. Entry tracked diff was empty; no appsettings, user secrets, connection values or launch files were changed. This is a review of this task's changes, not a secret audit of committed history. New keys and environment names are documented above. `.tmp/step126e/step126e-routing.trx` and `step126e-add-user.trx` are Git-ignored and untracked (`git ls-files .tmp/step126e` empty); exclude temporary evidence from intended commits. No environment-specific secrets need committing.

An older Windows computer can retain its existing normal/Auth configuration without activating microemr_dev. It needs the existing compatible .NET 10 SDK and complete source change set; see 127A for focused test reproduction if separately desired. **Rebuilt administrative endpoints still require local AP/AT settings even when normal credentials remain unchanged**; therefore existing configuration alone does not preserve all administrative functionality. Do not copy this machine's secrets or change Smart App Control. Review actual old-computer configuration before any activation there.

Remaining blockers: approved bounded administrative identity/validated exact effective scope; both API AP/AT keys; isolated DatabaseTool operator configuration; actual launch-source identification; runtime password authentication/process evidence; administrative functional verification; separately approved fictional acceptance fixtures/V1-V3. Existing operator/system login privileges are observed, not authorization to repurpose them or change the wider SQL installation.

## 10. Recommended Step 127D and stop

Recommend **Step 127D - Gate A bounded administrative identity approval and controlled identity readiness**, limited to a named identity and exact reviewed permission manifest. Obtain that gate's approval before any account/grant mutation. If approval is unavailable, preparation may finalize the proposed name, dependency evidence and reversible delta without changing SQL. Do not bundle configuration, runtime switch, restart or acceptance into Gate A. B through E remain separate later approvals.

Certification remains **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique tests PASS; Step 127B 273/273 required permissions PASS, 35/35 exclusions preserved, 9/9 protected-object checks PASS**.

Focused checks performed: targeted static/configuration review; read-only SQL metadata and reviewer effective permissions; ignored/untracked TRX checks; final report/diff and whitespace review. No tests/builds, GRANT/DENY/REVOKE, principal/role changes, migrations, active configuration edits, service restart, source modification, clinical writes/fixtures, commits, merges, pushes or Windows policy changes. **STOP after this review and report.**
