# Step 127D - bounded administrative SQL identity setup

Date: 2026-10-20 (America/Toronto). **Bounded SQL identity readiness PASS. Login/users created and exact permissions verified; application configuration and activation NOT performed.**

## 1. Checkpoint and authorization

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Entry: only report [168 / Step 127C](168-step127c-pc07-01-admin-identity-and-runtime-activation-readiness.md) untracked; no tracked modifications. That report and existing work were preserved. This report is the only new intended commit content; execution/evidence/credential artifacts remain Git-ignored.

The user's Step 127D request explicitly authorized creation of **microemr_admin_dev**, its users in **MicroEMR_Platform (P)** and **MicroEMR_LocalDev_Fresh (T)**, and only the exact Step 127C permissions. Endpoint: `192.168.50.1,50013`; expected instance: `EC2AMAZ-TC26CQK\TELEMED52`; TenantUid: `F94C6ECD-4417-483E-8A94-0507CF8384C2`. Runtime `microemr_dev`, Auth, other databases and application configuration were outside mutation scope.

The user separately approved generating a random password in memory and storing it as a **Windows DPAPI-encrypted credential file in Git-ignored .tmp**, recoverable by the current Windows account. No approval was inferred for user-secret/environment edits, runtime activation, restarts or subsequent gates.

## 2. Exact manifest and fresh preflight - PASS

Reused **Step 127C sections 2 and 4**, with the literal P/T `@Omitted` allowlists in [Step 126B section 7](163-step126b-pc07-01-sql-runtime-identity-and-permission-scope.md#7-revised-normal-runtime-grant-candidate---not-executed). Those entries are excluded from the runtime account and approved here for the separate administrative account. Each extracted name was checked against Step 127C; no additional procedure was inferred. The only column permission is exactly **SELECT on dbo.TenantDatabaseIdentity(TenantUid)** in T. No new permission inventory was generated.

Fresh preflight completed **16:15:32.804 -04:00 / 20:15:32.804 UTC**. Immediate checks ran again before creation, within the ten-minute freshness limit:

- Exact configured endpoint, returned instance and P/T contexts matched. P's one expected assignment was Active, server key `local-sql`, database T, reference `development:MicroEMR_LocalDev_Fresh`; T contained exactly one expected TenantUid.
- No server principal or P/T user named microemr_admin_dev existed. No existing identity was replaced or renamed.
- All **15 P and 20 T procedures** existed, were effectively dbo-owned, had no module EXECUTE AS context, and matched retained Step 127B definitions exactly. The approved tenant identity column existed.
- Targeted direct dependency checks found no unexpected cross-server/database, caller-dependent or non-dbo-owned dependency. No private-note storage/helper reference or actual dynamic SQL was found in the administrative entry definitions. This retains the earlier dependency analysis rather than claiming a new exhaustive transitive analysis.
- The retained runtime/public explicit-permission baseline matched Step 127B, with the same system-object filtering. All **35 administrative exclusions** remained unavailable under microemr_dev; all **nine protected-object checks** remained denied. Runtime sysadmin/db_owner flags were 0 and CONNECT was 1. No repeat of all 273 runtime checks was needed.
- P/T TRUSTWORTHY and database chaining remained OFF. Captured server principals/roles/permissions, database principals/roles/permissions/ownership, assignment and module state for preservation comparison.

Two local preflight-checker issues stopped the initial read-only attempts: EXEC return-value assignment to sys.sp_getapplock was initially mistaken for dynamic SQL, and the initial historical permission comparison included system-object rows excluded by the old baseline. Targeted inspection corrected both checks; neither was actual SQL drift. No SQL mutation or credential generation occurred before successful preflight.

## 3. Secure creation and controlled execution - COMMITTED

Execution window: **16:16:05.045-16:16:07.397 -04:00 / 20:16:05.045-20:16:07.397 UTC**.

The controller verified the successful fresh preflight, Git HEAD, its reviewed script hash, exact generated grant scripts, unchanged security/configuration snapshots and name availability before writing SQL. It refused execution if an attempt marker or credential artifact already existed. Reviewed scripts assert instance/database, expected assignment/identity, login availability, user mapping and procedure existence; each database uses XACT_ABORT, TRY/CATCH, transaction rollback on failure and effective required-right checks before COMMIT.

The existing sa reviewer was solely the controlled operator. The new password used 48 cryptographically random bytes plus a character-class prefix; it was never printed, passed in process arguments, written as plaintext to disk or put in application configuration. A PSCredential was exported through Windows DPAPI, reimported and recovery compared in memory **before CREATE LOGIN**. Password policy and expiration were both **ON**, with default database P. SQL login creation received the secret through an in-memory SQL parameter and quoted server-side CREATE LOGIN statement; error handling suppressed potentially sensitive execution text.

| Change | P | T | Result |
| --- | ---: | ---: | --- |
| Mapped INSTANCE SQL_USER named microemr_admin_dev | 1 | 1 | Correct login SID in each database |
| Explicit database CONNECT grants | 1 | 1 | Committed |
| Exact object EXECUTE grants | 15 | 20 | Committed |
| Exact column SELECT grants | 0 | 1 | T: TenantDatabaseIdentity(TenantUid) only |
| Total explicit database grants | **16** | **22** | **38 total**, all allowlisted |

Login creation and the two database transactions were separate boundaries, not one atomic server-wide transaction. Both database transactions returned their COMMITTED result; independent postflight confirmed the resulting state. No failure, uncertain commit, partial grant state, retry, rollback, role assignment or cleanup occurred during mutation. The login has the normal explicit **CONNECT SQL** server permission associated with login creation; no administrative server grant was added.

## 4. Independent authentication and permission postflight - PASS

Completed **16:16:18.237 -04:00 / 20:16:18.237 UTC**. New connections used the saved credential through SqlCredential and authenticated by actual SQL password login to P and T. ORIGINAL_LOGIN(), SUSER_SNAME() and USER_NAME() each returned **microemr_admin_dev** in the expected database. No clinical/provisioning procedure or clinical-content query ran.

| Check | P | T |
| --- | --- | --- |
| Login enabled; password policy/expiration checked | PASS | PASS |
| Login unlocked / unexpired | 0 / 0 | 0 / 0 |
| Correct INSTANCE SQL_USER and matching SID | PASS | PASS |
| Database CONNECT effective | 1 | 1 |
| Approved EXECUTEs effective | **15/15** | **20/20** |
| Approved identity-column SELECT effective | N/A | **1/1** |
| Explicit database grants exactly match allowlist | **16/16** | **22/22** |
| Non-public server/database role tokens | **0 / 0** | **0 / 0** |
| Database/schema/object ownership by new identity | **0** | **0** |
| Unapproved user-procedure EXECUTE violations | **0** | **0** |
| Direct user-table SELECT/INSERT/UPDATE/DELETE/ALTER/CONTROL/TAKE OWNERSHIP violations | **0** | **0** |
| Unapproved user-table column SELECT violations | **0** | **0** |
| Broad schema SELECT/DML/EXECUTE/ALTER/CONTROL/TAKE OWNERSHIP violations | **0** | **0** |
| Protected private-note current/revision table SELECT/INSERT/UPDATE/DELETE | N/A | **8/8 denied** |
| Protected internal PatientPrescriptionPrivateNote_Write EXECUTE | N/A | **1/1 denied** |

Complete negative object-name lists were obtained through reviewer-visible metadata **before** session-local EXECUTE AS LOGIN, then evaluated under the new token with REVERT/connection disposal. Thus denied objects could not disappear merely through restricted catalog visibility. These were aggregate negative checks, not regeneration of the existing runtime permission inventory. Unexpected NULL permission results would fail verification; none caused a failure.

Effective server-wide permissions were only **CONNECT SQL** and inherited public **VIEW ANY DATABASE**. Effective database-wide permissions were only **CONNECT** and the two existing public column-encryption-key-definition visibility permissions. No CONTROL, ALTER, IMPERSONATE, DDL, broad reader/writer permission or migration/deployment escalation was found. Public system facilities are not relabeled as application grants; public inherited rights and protected-object DENYs were preserved.

## 5. Database isolation and preservation - PASS

Reviewer-visible sys.databases supplied all 40 user-database names. Under actual new-login connections, HAS_DBACCESS returned **1 for P/T** and **0 for every one of the other 38 user databases**, including **MicroEMR_Auth**, **MicroEMR_Db**, **MicroEMR_Tenant_LocalDev** and **MicroEMR_Tenant_ProvisioningTest**. No other user database was entered or modified. This is an effective named access-boundary check; it is not an assertion that SQL's normal master/tempdb facilities are inaccessible or an audit of each unrelated database's internals.

Before/after comparisons matched for all pre-existing server principals/roles/permissions and captured P/T principals, role memberships, explicit permissions, ownership/trust/chaining state, assignment and reviewed module definitions/dependencies. New identity rows were the only permitted additions. The runtime/public explicit baseline and 35 exclusion/nine protected-denial checks matched. Existing microemr_dev was not altered; its previously reported secondary-database access remains a separate retained risk rather than a reason to give the new identity that access.

API/Web/Auth/isolated-DatabaseTool user-secret file hashes or absence matched preflight. No script set environment variables or edited connection strings, launch configuration, tenant assignment, application source, Auth or other database objects. No services restarted.

## 6. Credential custody and local evidence

Credential artifact: **.tmp/step127d/credential.clixml**, Windows DPAPI-encrypted PSCredential for microemr_admin_dev. It is Git-ignored and must not be force-added, printed or treated as disposable test output. Verified recoverable in independent normal Windows execution contexts as **ALIENWARE_AHMED\inadh**, including the separate postflight process. A subsequent sandbox-only import could not decrypt the account-bound file; an independent import outside that restricted context succeeded again without displaying the password. This is a sandbox/DPAPI context limitation, not an unknown login password.

Future approved configuration work should import this credential under the same Windows user and use it only in memory through a protected mechanism. DPAPI custody is tied to this Windows account/machine; copying the file to an older computer does not establish recoverability there. Preserve the artifact and Windows profile; arrange any durable backup/transfer separately without committing credentials or silently rotating the SQL password. Password expiration remains enforced and will need the normal approved credential-maintenance process.

Other ignored evidence: `.tmp/step127d/control.ps1`, `preflight.json`, `execution.json`, `postflight.json`, P/T grant scripts and P/T verification JSON. Evidence includes SQL metadata and sanitized results, not the password. No plaintext password file exists from this task.

Reviewed SHA-256 values:

| Artifact | SHA-256 |
| --- | --- |
| control.ps1 | `5A8DD67E9D59AE44443CC8462C12034DC919FCE258449D88E47CDDB45DCE281A` |
| MicroEMR_Platform-grants.sql | `3132EDD13C891E0DA526E997F3C8891C811313B25BB8C6CAEACB40966A60E2D1` |
| MicroEMR_LocalDev_Fresh-grants.sql | `943AC77788FFDF186A422188FDB9E733EB7BD161EDE97F2CA3D8AFB8F0D3F68E` |

Local scripts are execution evidence, **not replay instructions**. A later run must inspect existing state and approval scope rather than recreate/replace this login or blindly rerun grants. No unapproved automatic drop/revoke/cleanup is authorized. If later decommissioning is needed, review the exact new identity/grant delta and dependencies first; preserve clinical/audit history and unrelated identities.

## 7. Remaining prerequisites and Step 127E

**Bounded SQL identity readiness PASS** means actual authentication, exact SQL permissions, roles, protected negatives and observed user-database isolation passed. It does **not** establish administrative application functionality or deployment/activation readiness.

Still unconfigured, as established in Step 127C and preserved here:

- `ConnectionStrings:AdministrativePlatformDatabase`.
- `AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh`.
- Isolated `MicroEMR.DatabaseTool-local-development` operator P/T values before changing shared API configuration.

Recommend **Step 127E - Gate B secure administrative configuration**, with separate explicit approval for exact AP/AT targets, protected credential entry using the saved credential and isolated operator configuration. Reconfirm actual launcher/configuration precedence and same-server/catalog routing before any approved edit. Keep ordinary runtime credentials, Auth and service state unchanged within Gate B. Do not repurpose this bounded identity for migrations/provisioning or silently copy all runtime grants to it.

Gate C runtime switch, Gate D restart/functional verification and Gate E fictional fixtures/V1-V3 acceptance remain separate later approvals. Risks remain: current broad sa-backed application development connections until later activation; credentials available to the trusted API process after future configuration; expiration/custody management; unknown effective launcher state; and cross-database partial-result/application behavior not exercised here.

## 8. Checks, certification and stop

Performed PowerShell syntax validation, targeted SQL preflight, controlled approved creation/grants, actual new-login authentication, effective positive/negative/isolation checks, security/configuration preservation comparison, credential recovery and final report/Git whitespace review. No application builds/tests or migrations ran; no clinical writes, fixtures, application configuration edits, Windows policy changes, commits, merges, pushes or remote Git operations occurred.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique tests PASS; Step 127B 273/273 runtime permissions PASS, 35/35 exclusions preserved, 9/9 protected-object checks PASS**. Earlier evidence is retained without relabeling it as a new test run. **STOP after identity creation, verification and this report.**
