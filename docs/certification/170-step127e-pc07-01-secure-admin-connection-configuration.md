# Step 127E - secure administrative connection configuration (Gate B)

Date: 2026-10-20 (America/Toronto). **Local administrative configuration and isolated connection resolution PASS. Normal runtime switch, service restart and administrative endpoint acceptance NOT performed.**

## 1. Authorization and Git checkpoint

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Entry: reports 168 and 169 untracked, no tracked modifications. Both reports were preserved by hash. This report is the only new intended repository deliverable; local verification artifacts are ignored. No staging, commit, merge, push or remote Git operation occurred.

The user's Step 127E explicitly approved **only two local development administrative configuration values**, reuse of the protected credential and read-only verification. Read [Step 127D](169-step127d-pc07-01-bounded-admin-sql-identity-setup.md) first; reused its identity/permission evidence and the relevant Step 127C/126C configuration findings. No permission inventory or permission suite was repeated.

## 2. Provider, credential and target preflight

The inspected API uses `WebApplication.CreateBuilder(args)`, UserSecretsId **MicroEMR.Api-local-development**, and `AddMicroEmrInfrastructure`. Administrative providers expect **complete SQL connection strings**, not references to the DPAPI file or another invented value format.

An isolated verifier used `WebApplication.CreateBuilder` with application name **MicroEMR.Api**, explicit **Development**, the API content root and empty command-line arguments. It loaded the existing API assembly's user-secrets metadata and actual .NET configuration providers. Before mutation, neither administrative key existed in the effective configuration or flattened secret-file keys. Normal P/T and Auth values were present. No conflicting value was overwritten.

Development JSON precedes development user secrets; environment and command-line providers can override application values. The verifier had no command-line configuration overrides, and checked the actual **last provider containing each key**, not just raw-file presence. Both final winning providers were the JSON user-secrets provider rooted at **MicroEMR.Api-local-development**. This proves this isolated process's effective configuration, not an undisclosed IDE/service process's environment or command line.

`.tmp/step127d/credential.clixml` existed, remained Git-ignored/untracked and decrypted under **ALIENWARE_AHMED\inadh**, the authorized Windows account. Credential username was microemr_admin_dev. No password reset, regeneration, plaintext export, process-argument secret or password output occurred. Proposed connections were authenticated before writing configuration; instance, database, original/effective login and database user matched, and T returned exactly the expected single TenantUid.

## 3. Exact local configuration change

Only the API development user-secrets file **%APPDATA%\Microsoft\UserSecrets\MicroEMR.Api-local-development\secrets.json** was changed outside the repository:

| Added key | Target | SQL identity |
| --- | --- | --- |
| `ConnectionStrings:AdministrativePlatformDatabase` | `192.168.50.1,50013` / `MicroEMR_Platform` | microemr_admin_dev |
| `AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | `192.168.50.1,50013` / `MicroEMR_LocalDev_Fresh` | microemr_admin_dev |

Connection options were copied in memory from each corresponding existing normal development connection; only the credential fields changed in the **new administrative value**. Existing encryption/trust options were preserved. The original normal values were not changed.

Before mutation, the complete prior secret-file content was backed up as a Windows DPAPI-encrypted SecureString at **.tmp/step127e/api-secrets-before.clixml**, then independently recovered and compared in memory. Both credential and rollback files remain ignored and untracked. No credential-bearing values appear in this report, tracked appsettings, launch settings, verifier source, command arguments or output.

The final file has **exactly two additional effective keys**; every original secret value compares equal, including normal Platform/tenant and Auth. Original file security descriptor was preserved. API/Web/Auth/DatabaseTool appsettings and launch files, other user-secret files or their absence, the original credential and prior reports matched the captured hashes. No environment variables were set.

The user-secrets provider stores complete strings in its local JSON file; **.NET user secrets are outside Git, not an encrypted production vault**. DPAPI protects the source credential and rollback snapshot. Existing file permissions were preserved without weakening Windows security. Do not commit or copy local secret JSON as a deployment artifact.

## 4. Interruption and bounded recovery

The first atomic replacement attempt failed because Windows PowerShell converted a null backup-path argument to an invalid empty path. State inspection established that the original secret file was **byte-for-byte unchanged**, both administrative keys remained absent, and one pending provider-directory temporary file existed. Credential recovery and proposed authentication had already passed.

The cause and explicit-null correction were reproduced using harmless local sample files. Bounded recovery rechecked the original against its encrypted snapshot, compared every pending-file value against the original plus exactly the approved two keys, checked targets/credential and preserved-file hashes, then completed the pending atomic replacement with an explicit NullString. It did not replay SQL identity/grant setup or overwrite a changed original. No provider-directory temporary file remained afterward; no automatic rollback was necessary.

Configuration operation window: **16:24:54.453-16:26:21.755 -04:00 / 20:24:54.453-20:26:21.755 UTC**. Final actual-resolution evidence completed **16:26:21.657 -04:00**. The original interrupted state and final successful state are retained separately in ignored local evidence.

## 5. Read-only actual configuration and connection verification - PASS

The isolated verifier loaded the **existing compiled application assemblies** and registered the actual `AddMicroEmrInfrastructure` services. API and Infrastructure output copies of MicroEMR.Infrastructure.dll matched SHA-256. No application project was rebuilt, restored or tested. A small ignored verifier executable was compiled against installed .NET 10 reference assemblies; two CS1701 version-unification warnings concerned the existing SqlClient assembly's .NET 9 references. Actual .NET 10 execution and both connections succeeded; no package/framework change was made. An initial compiler argument-length issue was resolved with a response file before configuration mutation.

| Verification | Result |
| --- | --- |
| Actual Development provider selects both administrative values from intended API user-secret store | **PASS** |
| Actual `IAdministrativePlatformConnectionStringProvider` resolves and validates same target as normal P | **PASS** |
| Platform connection authenticates via resolved administrative string | **PASS** |
| Platform SQL identity | Instance `EC2AMAZ-TC26CQK\TELEMED52`; database `MicroEMR_Platform`; ORIGINAL_LOGIN/SUSER_SNAME/USER_NAME all **microemr_admin_dev** |
| Actual `IAdministrativeTenantSqlConnectionFactory` resolves through DI | **PASS** |
| Tenant factory uses actual `SqlTenantDatabaseResolver` and selected reference | **PASS**; ordinary P performs only the read-only assignment lookup |
| Tenant context/Active assignment/catalog and factory TenantDatabaseIdentity check | **PASS**; expected TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2` |
| Tenant SQL identity | Same expected instance; database `MicroEMR_LocalDev_Fresh`; ORIGINAL_LOGIN/SUSER_SNAME/USER_NAME all **microemr_admin_dev** |
| Unexpected sa fallback on administrative connections | **None** |
| Normal runtime and Auth effective values unchanged | **PASS**, compared by hashes and all original secret values |
| Services/endpoints started or invoked | **None**; no host Build/Start/Run or HTTP request |

The tenant verifier supplied only the known approved development TenantContext; the actual resolver, assignment lookup, secret provider and administrative factory were used. No fake database assignment or replacement connection factory was used. Ordinary Platform lookup still intentionally uses its unchanged existing runtime credential; that is distinct from the two verified administrative SQL identities.

Only safe identity metadata, the read-only assignment lookup and TenantDatabaseIdentity SELECT ran. No administrative/clinical write procedure, provisioning operation, migration, fixture or permission mutation occurred. Full credential-bearing strings and sensitive exception diagnostics were never emitted. Existing Step 127D role, allowlist, protected-note and other-database-access evidence is retained, not re-run or upgraded into application functionality proof.

## 6. Startup and endpoint readiness

The two missing **administrative configuration dependencies are now satisfied for the verified local Development provider**. Actual administrative capabilities resolve through DI; tenant resolution and identity checks work in the isolated process. Static inspection confirms missing/invalid AP/AT still throws a named sanitized configuration error, with no normal-credential fallback or permission-failure administrative retry. Administrative settings remain lazily resolved; no new startup requirement was introduced. Existing API option validations, Auth configuration and Web OIDC dependencies remain unchanged.

No API/Web/Auth process was restarted or its effective launcher inspected as a live runtime. Existing processes might reload some settings, while other values are captured at construction; this step does not establish either behavior or endpoint availability. A separately approved controlled restart/functional verification must use the real launch profile/environment. Do not claim administrative endpoint or end-user authorization PASS from this isolated connection result.

The DatabaseTool profile **MicroEMR.DatabaseTool-local-development remains absent**, matching preflight. DatabaseTool shares the API user-secrets ID, but explicit deployment adapters read its normal P/T keys and do not use these AP/AT additions. Its configuration and current operator behavior were not changed or executed. Isolated operator P/T values are still a prerequisite before changing shared normal runtime values; the request explicitly limited this step to the two administrative keys and forbade migration/deployment configuration changes.

Auth remains independent; its existing hosted migration/seeding behavior is not authorized by any future API/Web restart implicitly.

## 7. Rotation, portability and rollback preparation

Step 127D established password policy and expiration **ON**; neither was changed. Rotation requires separate approval and an authorized operator using secure input: rotate the SQL password, replace the protected DPAPI credential, and update **both administrative keys in section 3** through the same protected local mechanism. Coordinate a maintenance window for administrative operations, validate both connections immediately, and obtain any necessary restart approval for cached connections. Never fall back to sa or disable expiration to avoid interruption. Ordinary runtime/Auth settings and SQL grants must remain outside that credential update. This step did not rotate credentials.

The source credential and rollback snapshot are tied to this Windows DPAPI account/machine. Do not assume they decrypt on the older development computer. Provision that computer's approved local configuration through a separately authorized secure delivery/input process; do not export plaintext or commit machine-specific secrets. Its existing normal/Auth configuration may remain in use until activation is separately approved, but administrative endpoint continuity requires its own AP/AT configuration.

No rollback was needed after successful verification. For a separately approved configuration rollback, remove/restore only these two settings and preserve any later unrelated secret edits. The encrypted prior-file snapshot supports comparison/recovery; do not blindly replace the whole store after concurrent changes. Do not reset/drop the SQL identity, revoke grants, delete clinical history or restart services as an automatic rollback.

## 8. Remaining blockers and recommended Step 127F

**Gate B local administrative configuration readiness PASS.** Remaining prerequisites are isolated DatabaseTool operator P/T configuration before a shared-secret cutover, actual launcher/provider confirmation for affected processes, normal microemr_dev authentication/activation evidence, separately approved restart and functional checks, and separately approved fictional fixtures/V1-V3 acceptance. The current normal development connections remain sa-based. Same-process administrative credential exposure, expiration/custody management and untested cross-database partial-result workflows remain qualifications.

Recommend **Step 127F - isolated operator configuration readiness before Gate C**, under separate explicit approval, because this step deliberately left deployment configuration untouched. Establish and verify the tool's isolated existing P/T key overrides using an approved operator credential without executing migrations/provisioning or switching runtime values. Once that prerequisite is met, seek Gate C approval for the exact normal runtime switch. Keep Gate D restart/verification and Gate E acceptance approvals separate; do not bundle them into the configuration result.

## 9. Evidence and stop

Ignored `.tmp/step127e/` evidence: control/recovery scripts, isolated Probe source/binary/compiler response file, provider before/after evidence, proposed sanitized identities, preserved-file hashes, interrupted/final configuration state and **encrypted** rollback snapshot. Credential remains at `.tmp/step127d/credential.clixml`. Git exclusion and absence from tracked files were verified. No temporary credential-bearing provider file remained after replacement.

Focused checks: script syntax, isolated Development provider/DI/factory and SQL identity verification, original-value/file-permission preservation, ignored-artifact checks and final report/diff whitespace review. No full build, application tests, repeated permission suite, SQL/login/role changes, clinical data changes, fixtures, migrations, service restarts, Windows security changes or remote Git operations.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique routing tests PASS; Step 127B 273/273 runtime grants PASS (35/35 exclusions and 9/9 protected checks retained); Step 127D bounded administrative identity PASS**. **STOP after secure administrative configuration and read-only verification.**
