# Step 127F - DatabaseTool connection independence

Date: 2026-10-20 (America/Toronto). **Classification C - Dependent. Shared-secret runtime activation BLOCKED pending operator configuration separation. Read-only investigation; no configuration change.**

## 1. Git checkpoint and scope

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Entry: reports 168-170 untracked; no tracked modifications. Existing files were preserved; this report is the only added deliverable. No commit, merge, push or remote Git operation.

Reused relevant configuration/readiness portions of [Step 127E](170-step127e-pc07-01-secure-admin-connection-configuration.md#6-startup-and-endpoint-readiness) and [Step 127C](168-step127c-pc07-01-admin-identity-and-runtime-activation-readiness.md#5-exact-configuration-inventory-and-missing-settings). Inspected only DatabaseTool, directly used connection/deployment helpers and relevant API registration metadata. No SQL connection, permission review, credential recovery/export, build, test, tool invocation or service restart was needed.

## 2. Entry point and exact provider precedence

Project: [MicroEMR.DatabaseTool.csproj](../../src/MicroEMR.DatabaseTool/MicroEMR.DatabaseTool.csproj:7), .NET 10 console executable. Entry point: [Program.cs](../../src/MicroEMR.DatabaseTool/Program.cs:14). Its **UserSecretsId is MicroEMR.Api-local-development**, the same as the API project. `AddUserSecrets<Program>()` therefore loads the API secret store even though Program belongs to DatabaseTool.

Last provider containing a key wins:

| Order | Source | Consequence |
| --- | --- | --- |
| 1 | Optional `src/MicroEMR.Auth/appsettings.json`, resolved relative to current working directory | Auth JSON, not API appsettings; working directory matters |
| 2 | Process environment | Can be superseded by later secrets |
| 3 | User secrets `MicroEMR.Auth-local-development` | Earlier Auth configuration, including a Platform value |
| 4 | `AddUserSecrets<Program>()`: **MicroEMR.Api-local-development** | Shared API normal P/T values override earlier sources |
| 5 | Optional user secrets **MicroEMR.DatabaseTool-local-development** | Supported tool-specific overrides; currently absent |
| 6 | Process environment again | Overrides even the isolated tool profile |
| 7 | In-memory `TenantProvisioning:SqlAssetsPath` | Forces SQL asset path to output `database`; no credential override |

DatabaseTool does not load API appsettings or appsettings.Development.json, use API's WebApplication builder, or add a command-line configuration provider. CLI options select commands/targets, not arbitrary configuration providers. Its user-secret loads are explicit, independent of ASPNETCORE_ENVIRONMENT being Development; this is an operator console configuration, not automatic web-host environment selection.

## 3. Connection consumers and privilege requirements

| Consumer | Exact key/path | Finding |
| --- | --- | --- |
| Platform tenant administration, tenant resolver, provisioning status and ordinary Platform helpers | `ConnectionStrings:PlatformDatabase` through `PlatformDatabaseConnection` | Reads the same normal key as API, from the tool's effective configuration |
| Tenant connections and migration runner | `TenantDatabaseSecrets:{assignedSecretReference}` through `ConfigurationTenantDatabaseSecretProvider` | For this assignment: `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh`; full connection string, no credential substitution |
| Tool administrative Platform capability | `DeploymentPlatformConnectionStringProvider` | Explicitly reads **normal PlatformDatabase**, not AdministrativePlatformDatabase |
| Tool administrative tenant capability | `DeploymentTenantSqlConnectionFactory` | Wraps ordinary ITenantSqlConnectionFactory; uses **normal TenantDatabaseSecrets**, not AdministrativeTenantDatabaseSecrets |
| Auth identity lookup | `ConnectionStrings:AuthDatabase`, falling back to `ConnectionStrings:AuthServerConnection` | Independent database concern; preserve both existing sources |

Program calls AddMicroEmrPlatformInfrastructure and then **AddMicroEmrTenantProvisioning**. The latter registers the deployment adapters after API-style providers and adds migration/status services. Thus merely inheriting Step 127E's AP/AT values does not provide DatabaseTool with an independent operator connection.

`TenantDatabaseMigrationRunner` reads the assigned normal tenant secret, opens SQL directly, runs actual migration batches in transactions, initializes/validates tenant identity and records SchemaMigration rows. `SqlTenantProvisioningStatusRepository` uses normal PlatformDatabase for provisioning status procedures. These require deployment-specific DDL/DML/status authority beyond restricted runtime grants and beyond microemr_admin_dev's bounded application-administration scope. Exact operator privileges depend on intended scripts; this inspection establishes neither a need for blanket sysadmin/db_owner nor a new deployment grant manifest.

The CLI checks PlatformAdministration:Enabled and a nonblank PlatformAdministration:ActorId, plus command-specific confirmations. These are application gates, not replacement SQL authority. No API server startup, API HTTP endpoint or Web/Auth host is required by the inspected tool entry point/project; the API does not invoke DatabaseTool at startup. They share configuration and SQL infrastructure, not a process-startup dependency.

## 4. Current local state and security findings

Read-only presence/parsing inspection reconstructed Program's provider ordering using repository-root working directory and this review process's environment. No secret was copied/exported or displayed. Sanitized results:

| Value | Current winning source | Current target/identity |
| --- | --- | --- |
| Normal PlatformDatabase | API user secrets | `192.168.50.1,50013` / MicroEMR_Platform / **sa** |
| Normal approved tenant secret | API user secrets | Same endpoint / MicroEMR_LocalDev_Fresh / **sa** |
| AP/AT keys | API user secrets | microemr_admin_dev; loaded but not selected by tool deployment adapters |
| AuthDatabase | API user secrets | MicroEMR_Auth; unchanged |
| AuthServerConnection | Auth user secrets | MicroEMR_Auth; unchanged |
| Tool-specific profile | **Absent** | No independent operator override |
| Relevant process environment overrides | **None** | No observed isolation through environment |

The inspected tracked Auth appsettings contains no normal/admin/tenant SQL connection keys or SQL credential values. The tool's tracked Program/project contain key names and provider setup, not SQL passwords. Credentials are inherited from local untracked user-secret stores. This is a bounded inspection of the tool's loaded configuration, not a whole-history secret audit or an assertion about every other launcher/environment.

There is **no permission-failure retry to sa** or automatic administrative credential promotion in the inspected connection paths. Configured sa is currently selected by ordinary provider precedence. Earlier Auth secrets also contain a Platform value: removing the later API key could expose that earlier value, whereas replacing the API key with a nonblank microemr_dev value would continue to win. This is configuration precedence, not SQL-error fallback. Missing assigned tenant secrets fail rather than selecting AP/AT.

Changing normal shared values does not itself publish a privileged password, but relying on their current broad credentials masks the deployment dependency. Tool-only overrides must never be placed in API secrets or globally shared environment variables. Separate profiles provide configuration isolation, not protection against compromise of the same Windows account; .NET user-secret JSON is outside Git, not an encrypted production vault.

## 5. Independence classification and activation effect

**C - Dependent**, for the verified local shared-secret setup. A and B are not supported: the tool's own profile is absent, its operative P/T values come from API user secrets, and no relevant environment override was observed. Evidence is sufficient to identify the dependency, so this is not merely D/Unverified.

Replacing the API **shared user-secret** normal P/T values with microemr_dev would make DatabaseTool inherit those restricted credentials on its next invocation. It could lose required tenant-administration/provisioning/migration permissions; ordinary read commands are not necessarily all broken, and a valid restricted string need not cause immediate startup failure. No live failing operation was executed to prove the consequence; the source paths and established grant separation establish the concrete risk.

An API-only process-environment override would not alter the tool's shared store or another process's environment. That is a different possible activation scope, not evidence that the current shared configuration is independent. Actual IDE/service launch sources remain to be confirmed before any such alternative is approved.

**Can DatabaseTool remain unchanged?** Its **source can remain unchanged**: the override mechanism already exists. Its effective configuration cannot remain as currently observed during a shared-secret runtime switch if deployment capability must be preserved. The smallest separation is an approved tool-only override for **ConnectionStrings:PlatformDatabase** and **TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh**, using the existing optional tool profile (or strictly tool-scoped process environment). No new provider/key/source refactor is necessary.

## 6. Readiness, rollback and exact next action

**Runtime activation: BLOCKED** for a shared API secret-store change until that concrete dependency is isolated. Step 127B runtime grants PASS, Step 127D bounded administrative identity PASS and Step 127E administrative configuration PASS remain established; none supplies migration/deployment authority to DatabaseTool or resolves this missing override.

Next action: separately authorize **Step 127G - minimal DatabaseTool operator configuration separation**. Approve the existing operator credential/targets and only the two normal-key overrides in **MicroEMR.DatabaseTool-local-development**; verify provider precedence, unchanged API/Auth values and safe read-only connectivity/identity. Do not assume microemr_admin_dev is a deployment account or create another identity unnecessarily. No migration/provisioning execution, runtime switch or restart is implied by that future configuration approval. All configuration work remains unperformed here.

After operator isolation, seek separate Gate C approval for the exact API runtime change and fresh credential/launcher evidence. Gate D restart/functional verification and Gate E fictional fixtures/V1-V3 acceptance remain separate.

Step 127E's ignored encrypted prior-secret snapshot exists and its report documents recovery. It predates AP/AT additions: before any later change, capture the **current** configuration securely and prepare restoration of only the approved keys, preserving AP/AT and concurrent unrelated edits. No rollback was necessary or performed here; no automatic whole-store restore, grant revocation, SQL account change or clinical-history change is appropriate.

## 7. Checks and certification

Performed targeted static consumer/provider analysis, sanitized local provider-state inspection and final Git/diff whitespace review. No source/configuration/credential/SQL modification, SQL connection, build, test suite, migration, provisioning, endpoint invocation, service restart or Windows setting change occurred. Only this report was added.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 127B runtime grants PASS; Step 127D administrative identity PASS; Step 127E administrative configuration PASS**. Earlier 24 routing-test, 273 runtime-grant, 35 exclusion and nine protected-check results remain retained evidence, not rerun results. **STOP after verification and this report.**
