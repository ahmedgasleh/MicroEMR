# Step 127G - isolated DatabaseTool operator configuration

Date: 2026-10-20 (America/Toronto). **DatabaseTool operator independence PASS; configuration prerequisite READY for separately approved runtime activation. No runtime switch or restart performed.**

## 1. Git checkpoint and authorization

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Entry: reports 168-171 untracked; no tracked modifications. Existing reports/source were preserved; this report is the only added intended repository deliverable. No staging, commit, merge, push or remote Git operation.

The user approved exactly two local DatabaseTool operator overrides and read-only verification. Section 5 required explicit operator authorization rather than inference from credential availability. The user separately answered **Authorize existing sa for DatabaseTool only**, then expressly reiterated that the existing sa operator may be retained strictly in the isolated local-development profile when no suitable separate operator is configured. That authorization persists across the interrupted preparation turns.

The interruption checks found no tool profile or configuration mutation; only ignored verifier preparation was present. Configuration proceeded only after verifying the existing authorized source and actual SQL operator identity. Neither restricted application identity was substituted for the operator. No password, login, SQL permission or role was changed.

## 2. Confirmed mechanism and credential source

Read [Step 127F](171-step127f-pc07-01-databasetool-connection-independence.md) first; reused Step 127E/127C readiness and key contracts. Targeted inspection confirmed `src/MicroEMR.DatabaseTool/Program.cs` and its project still implement:

1. Optional repository-relative Auth appsettings.
2. Process environment.
3. Auth user secrets.
4. Tool assembly user secrets, whose UserSecretsId is **MicroEMR.Api-local-development**.
5. Optional **MicroEMR.DatabaseTool-local-development** user secrets.
6. Process environment again.
7. In-memory SQL-assets path only.

Last provider containing a key wins. The values are **complete connection strings**. `AddMicroEmrTenantProvisioning` selects `DeploymentPlatformConnectionStringProvider` and `DeploymentTenantSqlConnectionFactory`, which use the normal P/T key names from this tool-specific configuration root. No new source format, provider or application abstraction was introduced.

**Before writing**, the isolated verifier loaded the existing DatabaseTool assembly's UserSecretsId metadata, reconstructed the actual Program provider chain and resolved the actual deployment adapters through DI. Both winning providers were the API user-secret store; the tool profile was absent. Read-only authentication succeeded as **sa**, database user **dbo**, existing sysadmin flag **1**, in both approved databases. The actual tenant factory performed the trusted Active assignment lookup and matching tenant identity check. This establishes the verified credential source and operator independently of merely parsing an available secret.

That existing API local user-secret store supplied the two operator connection values securely in memory. No new credential was created, reset, displayed or passed through process arguments. API AP/AT values were checked to remain present for microemr_admin_dev at the expected targets.

## 3. Exact configuration change

Created **%APPDATA%\Microsoft\UserSecrets\MicroEMR.DatabaseTool-local-development\secrets.json** with exactly these two keys:

| Key | Approved development target | Operator |
| --- | --- | --- |
| `ConnectionStrings:PlatformDatabase` | `192.168.50.1,50013` / `MicroEMR_Platform` | sa |
| `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | Same endpoint / `MicroEMR_LocalDev_Fresh` | sa |

The existing strings were retained exactly, preserving connection/encryption/trust options. No value was copied into tracked configuration or a report. No existing tool profile was overwritten: a conflict/existing profile would have stopped the operation. A temporary file in the intended user-secret directory was moved atomically to the previously absent target; no credential-bearing temporary file remained.

Configured **16:44:46.499-16:44:49.534 -04:00 / 20:44:46.499-20:44:49.534 UTC**. Hash/absence comparisons before and after covered API/Web/Auth user-secret files, relevant appsettings/launch files, prior reports, the original administrative credential and relevant tool/DI source. All matched. Thus normal API connections, AP/AT, Auth and unrelated deployment/tenant settings remained unchanged. No environment variables or Windows permissions/security settings were changed.

This is user-specific local Development configuration for the exact approved SQL targets. DatabaseTool explicitly loads its profiles rather than automatically restricting secret loads to ASPNETCORE_ENVIRONMENT; the helper rejected any observed non-Development environment declaration. Actual other launchers/environments remain outside this evidence.

## 4. Read-only post-configuration verification - PASS

Final verification completed **16:44:49.375 -04:00**. A non-mutating helper used existing DatabaseTool/Infrastructure binaries without executing Program's entry point, a tool command, migration or provisioning runner. It used the actual provider order and deployed connection adapters; ordinary P performs only the read-only assignment lookup before the tenant factory opens T.

| Check | Platform | Tenant |
| --- | --- | --- |
| Final winning provider | **Tool-only user-secret profile** | **Tool-only user-secret profile** |
| Returned SQL instance | EC2AMAZ-TC26CQK\TELEMED52 | EC2AMAZ-TC26CQK\TELEMED52 |
| Returned database | MicroEMR_Platform | MicroEMR_LocalDev_Fresh |
| Actual authentication / ORIGINAL_LOGIN / effective login | **sa / sa / sa** | **sa / sa / sa** |
| Database user | dbo | dbo |
| Deployment adapter resolved | DeploymentPlatformConnectionStringProvider | DeploymentTenantSqlConnectionFactory |
| Target/tenant validation | Approved P | Factory verified TenantUid **F94C6ECD-4417-483E-8A94-0507CF8384C2** |

There was no unexpected credential fallback: last-provider checks establish the tool profile won even though the old and new credential values currently match. Queries were only identity/role metadata, the existing read-only tenant assignment lookup and TenantDatabaseIdentity SELECT. No other tenant database was entered. Retaining sa does **not** restrict that login's global capabilities to these two databases; this step scopes configuration/usage, not SQL authorization.

**Independence PASS:** the helper inserted synthetic microemr_dev P/T values immediately after the shared API provider and before the tool profile, representing the earlier provider's future restricted values. They existed only in memory and were never used for authentication. The later actual tool provider continued to win for both keys. The actual deployment Platform provider and tenant secret provider still returned the exact verified tool-only operator values. Final environment precedence was retained; an overriding environment value would have failed the winning-provider assertions. Active API settings were never changed for this exercise.

The current configuration is therefore **B - Shared configuration but safely isolated for these two operator keys**, improving Step 127F's C classification. Auth and other unchanged settings still intentionally inherit their existing sources. An environment override or another tenant reference requires its own later review; this is not proof of independence for every possible deployment configuration.

## 5. Security, rollback and verification limits

The new user-secret profile is outside the repository. Ignored `.tmp/step127g/rollback.clixml` records the previous **profile absent** state, intended keys/path and Git checkpoint as a Windows DPAPI-encrypted snapshot. Recovery was verified in memory under **ALIENWARE_AHMED\inadh** before writing. There were no previous credential values to back up for this absent profile. No credentials appear in tracked changes, helper source, sanitized JSON, command arguments or terminal output; local evidence and rollback artifacts are Git-ignored and untracked.

.NET user-secret JSON is user-local and outside Git, **not encrypted at rest by the provider or a production vault**. Existing Windows profile protections were used without weakening permissions. DPAPI rollback recovery is tied to this Windows account/machine. The operator remains an explicitly approved broad **sa development limitation**, not a least-privilege deployment identity or a credential for routine application operations. Profile separation does not protect against compromise of the same Windows account or broad SQL login. No new privileged identity/grant design was attempted.

Rollback is ready and **was not performed**. To restore the prior configuration under approved recovery scope, remove only the two added tool keys after comparing current content against this task's captured two-key file. If still unchanged, removing that new secrets.json restores prior absence; preserve any later unrelated additions rather than deleting them. Do not touch API/AP/AT/Auth values or restore older whole API snapshots. No SQL state requires rollback. The controller would remove only its exact newly created file on a verification failure; concurrent edits would stop cleanup instead. Neither condition occurred.

The helper was compiled separately against installed .NET 10 references and existing application artifacts; **no application project/full build or application test suite ran**. Local verifier namespace/disposal compile errors were corrected before any configuration write; two existing SqlClient/.NET reference-unification warnings remained, and actual execution succeeded. DatabaseTool and Infrastructure copies of MicroEMR.Infrastructure.dll matched SHA-256. No application source was modified. This is connection/configuration evidence, not proof of migration execution, administrative endpoint behavior or complete deployment recovery.

## 6. Activation readiness and next step

| Prerequisite | Result |
| --- | --- |
| Step 127B normal runtime grants | **PASS retained: 273/273** |
| Step 127D bounded application administrative identity | **PASS retained** |
| Step 127E AP/AT configuration | **PASS retained; files unchanged** |
| Step 127G operator selection, authentication and simulated future-switch independence | **PASS** |
| Tool configuration rollback | **READY; protected prior-absence snapshot verified** |
| Controlled restart/functional plan | Existing separate Gate D sequence retained; not executed or approved by this step |

**Configuration prerequisite READY for separately approved runtime activation.** This does not mean running application identity/functional acceptance has passed. Before Gate C writes, confirm actual microemr_dev password authentication, real API/Web launchers and effective providers, and securely snapshot the **current** normal settings while preserving AP/AT/tool configuration. Unknown process environments and normal runtime authentication are still preconditions for that later operation, not reasons to repeat the completed SQL grant work here.

Recommend **Step 127H - separately approved Gate C normal development runtime connection switch**, limited to exact API normal P/T credentials after those fresh checks. Do not change Auth, AP/AT or this tool profile. Prepare key-specific rollback. Gate D requires separate approval for named process restart, startup/health, tenant resolution, actual runtime/admin identities and focused functional/security verification; exclude Auth restart/migration/seeding unless separately authorized. Gate E fictional fixtures and V1-V3 acceptance remain a further separate approval.

## 7. Evidence and stop

Ignored `.tmp/step127g/`: non-mutating verifier source/binary/compiler response file, control script, before/after sanitized resolution/authentication evidence, configuration status, preservation hashes and DPAPI rollback snapshot. Only the intended user-secret file and this report were added as configuration/documentation outputs; existing source, reports, credentials and API files were preserved. Final status/diff/whitespace and exclusion checks completed.

No runtime activation, API/Web/Auth restart, endpoint invocation, SQL account/password/role/permission change, migration, provisioning, clinical modification, fixture, Windows security change or Git remote operation occurred.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique tests PASS; Step 127B 273/273 runtime grants PASS; Step 127D administrative identity PASS; Step 127E administrative configuration PASS**. No acceptance status was upgraded. **STOP after configuration, read-only verification and documentation.**
