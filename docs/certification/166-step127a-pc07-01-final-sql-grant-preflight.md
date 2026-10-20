# Step 127A - final SQL grant preflight

Date: 2026-10-20 (America/Toronto). **Normal-account grant candidate READY FOR SEPARATE APPROVAL; administrative deployment and runtime cutover BLOCKED. No SQL permissions or configuration changed.**

## 1. Git checkpoint and evidence

Branch `main`; HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry: 15 modified tracked files, seven new Infrastructure files, one new routing-test file and reports 157-165 untracked. Step 126C routing and Step 126D Add User correction remain present. All existing changes preserved; this report is the only new deliverable. No commit, merge, push, build, test rerun or repository-wide scan.

Reused [Step 126B sections 6-7](163-step126b-pc07-01-sql-runtime-identity-and-permission-scope.md#6-final-classification-and-security-conditions), [Step 126C](164-step126c-pc07-01-runtime-admin-sql-connection-separation.md), [Step 126D](165-step126d-pc07-01-connection-routing-test-recovery-and-deployment-readiness.md) and [Step 125](161-step125-pc07-01-private-notes-runtime-and-visual-readiness.md). Existing Step 126E TRX definitions/results confirm:

| Retained evidence | Discovered | Executed | Passed | Failed | Skipped/not executed |
| --- | ---: | ---: | ---: | ---: | ---: |
| `.tmp/step126e/step126e-routing.trx` | 23 | 23 | 23 | 0 | 0 |
| `.tmp/step126e/step126e-add-user.trx` | 1 | 1 | 1 | 0 | 0 |

**24 unique focused tests PASS.** These use test doubles and do not establish live SQL runtime PASS. The user's reported manual Smart App Control change was not repeated or inspected as a security-policy task.

## 2. SQL target identity and current security state

Fresh metadata-only queries connected solely to `192.168.50.1,50013`; SQL returned `EC2AMAZ-TC26CQK\TELEMED52`. Database contexts were exactly `MicroEMR_Platform` (P) and `MicroEMR_LocalDev_Fresh` (T). P has the expected Active assignment: TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`, server key `local-sql`, database T, reference `development:MicroEMR_LocalDev_Fresh`. T returned exactly one matching identity, key `local-dev-fresh`.

`microemr_dev` exists as an enabled SQL_LOGIN; each database has its matching INSTANCE-authenticated SQL_USER and login SID. No assigned server/database roles or database/schema/object ownership found. Direct server permission remains CONNECT SQL; direct database permission remains CONNECT in each database. Effective database rights are CONNECT and the two existing public column-encryption-definition visibility rights. Effective server rights are CONNECT SQL and public VIEW ANY DATABASE; no server administration right was observed. All five sysadmin/db_owner/db_ddladmin/db_datareader/db_datawriter flags are 0; effective CONNECT is 1 in both databases.

The existing reviewer authenticated as `sa` **only for metadata inspection**. Effective checks used isolated connection-local `EXECUTE AS LOGIN=N'microemr_dev'`, with TRY/CATCH, REVERT and connection disposal. No clinical procedure ran. This verifies the target security token, not its password authentication, application process or runtime usage. Existing reviewer credentials are not a proposed administrative API identity.

The initial sandbox connection failed with TCP Provider `Access is denied`; the same read-only query succeeded with expanded sandbox access. No SQL or Windows policy setting changed. Local PowerShell file execution was disabled; commands were submitted as an in-memory command block without changing execution policy.

## 3. Final candidate and effective-permission comparison

The exact candidate remains **Step 126B section 7's complete SQLCMD block**, replacing older candidates. It contains two database batches and four dynamic GRANT construction templates; their literal allowlists expand to the following individual object/column statements:

| Database/category | REQUIRED statements | Already effective | Missing | NULL/unavailable | EXCLUDED EXECUTEs |
| --- | ---: | ---: | ---: | ---: | ---: |
| P procedure EXECUTE | 12 | 0 | 12 | 0 | 15 |
| T procedure EXECUTE | 181 | 0 | 181 | 0 | 20 |
| T column SELECT | 80 | 0 | 80 | 0 | 0 |
| Total | **273** | **0** | **273** | **0** | **35** |

Classification remains **273 REQUIRED / 35 EXCLUDED / 0 UNRESOLVED**, without reclassifying the original 308 permissions. All 35 excluded EXECUTE checks are 0. Existing CONNECT rights need no additional statement. Because all 273 additions are missing now, this exact candidate would currently issue **193 EXECUTE + 80 column SELECT grants**. Reconcile again immediately before execution; a changed effective baseline requires a newly reviewed delta/count rather than assuming 273 remain missing.

Validated both USE targets, the single recipient `[microemr_dev]`, disjoint/unique procedure lists, all 228 required/excluded procedure objects and all 80 columns. All procedure owners are dbo. No other grant recipient, fixed-role/principal DDL, schema-wide EXECUTE, table-wide SELECT, clinical DML, protected-note column grant, internal-helper grant, grant option or migration/provisioning permission occurs in the candidate. Column grants cover exactly the 12 previously reviewed tables; no protected-note storage is included.

Fresh direct dependency metadata for required entries contains 21 P and 707 T rows: no unresolved object reference, cross-server/database reference or non-dbo target owner. No declared procedure uses an EXECUTE AS module context. Reused the prior dependency/security analysis; this was a bounded target/dependency check, not a new transitive inventory or a procedure-execution test.

**No unexpected permission or reviewed-module drift.** Of 228 declared procedures, 221 definitions/owners match the original cached metadata. The other seven definitions match retained, later 027/0085 postflight snapshots exactly: three P changes, two T Draft-procedure changes and two newly added T private-note entry procedures. Those are already accepted deployment changes, not new drift. No migration/constraint/ledger verification was repeated.

T still has the existing public DENY entries for SELECT/INSERT/UPDATE/DELETE on both protected private-note tables and EXECUTE on `dbo.PatientPrescriptionPrivateNote_Write`. All nine effective negative checks are **0**, with no NULL. No DENY needs adding/removing. Same-owner internal access through approved outer procedures remains the reviewed design, not a new live functional claim.

Local supporting artifacts, all ignored: `.tmp/step127a/preflight.ps1`, each database's `*-preflight.sql`, `preflight-results.json`, `latest-module-comparison.json` and `reviewed-candidate.sql`. Only metadata queries were submitted; the candidate file was never submitted. The extracted SQL block's SHA-256 is `7F204F47903184933C5AD8E2C15C5DB3590D2708B806C4D4FD1C12A8AB37CE0B` (UTF-8 without BOM, preserving report line endings). The versioned source of truth is report 163 section 7; reconstruct/review this block on another machine rather than relying on ignored files. A line-ending conversion can change its byte hash and must be distinguished from a semantic edit.

## 4. Runtime/admin routing compatibility

Focused source/diff inspection and the retained 24 tests support the unchanged Step 126C/126D split:

- Ordinary Platform lookups, effective access, entitlement and denial audits use `ConnectionStrings:PlatformDatabase`; ordinary clinical repositories use `ITenantSqlConnectionFactory` and the assigned tenant secret.
- Modern template authoring and all eight draft/version entry permissions remain normal runtime. Legacy template mutations use the administrative factory.
- Administrative access/membership/role/user/security-review methods use the designated Platform provider; actor provisioning, clinic save, provider administration and import use the designated tenant capability. The CLI-only SetAuthSubjectId path uses the deployment adapter. ListByUser's existing normal configuration path is used by the operator CLI, not justification for an extra API grant.
- Normal failures never retry with an administrative credential. Administrative settings are lazy, validated against the normal server/catalog, and have no normal-credential fallback.
- The tenant wrapper reuses trusted context, Active Platform assignment, assigned secret/database validation and TenantDatabaseIdentity checks. Credentials are not selected by an HTTP privilege flag.
- API registration excludes the migration runner/status services; DatabaseTool explicitly registers deployment adapters. AuthDatabase/AuthServerConnection remain separately configured and unchanged.
- Step 126D checks administrative membership access before creating an Auth identity. The new regression passed; later cross-database failures can still require existing partial-result recovery.

DI capability separation does not replace endpoint authorization or isolate secrets from a compromised API process. Live SQL/runtime security and visual acceptance remain unverified.

## 5. Administrative configuration readiness - BLOCKED

| Required setting | Target and dependent operations | Current local observation |
| --- | --- | --- |
| `ConnectionStrings:AdministrativePlatformDatabase` | Same P server/catalog; access profile/override management, tenant user list/create/membership/roles and privileged security review | Absent |
| `AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | Same assigned T server/catalog; clinical actor provisioning, clinic-profile save, provider mutations/eligible users, legacy template writes and clinical import | Absent |

Presence-only inspection of the API local user-secret store found normal Platform/primary-tenant/Auth settings present and both administrative settings absent. No corresponding overrides exist in this review process. This does not establish the configuration of another process. No bounded administrative identity is named/bound, so **an appropriately privileged existing administrative identity cannot be verified**; administrative deployment readiness is BLOCKED. Do not adopt the reviewer's sa credentials or create an account in this step.

Retain Step 126D's administrative requirement: separately scoped 15 P + 20 T excluded procedure EXECUTEs, database CONNECT and T SELECT on `dbo.TenantDatabaseIdentity(TenantUid)`, with explicit review of effective rights and inherited restrictions. Do not copy all 273 normal grants. Existing same-owner procedure/audit dependencies must remain valid. Deployment/operator rights are separate.

Missing administrative settings do not inherently prevent API construction/startup: these settings resolve on use and inspected ValidateOnStart paths do not resolve them. Actual startup was not run. The listed endpoints fail closed when invoked; normal Platform health can succeed while administration is unavailable. Optional Add User clinical provisioning retains its existing recoverable partial-result behavior.

Credentials can later use the existing `MicroEMR.Api-local-development` user-secret store or scoped process environment (`ConnectionStrings__AdministrativePlatformDatabase` / `AdministrativeTenantDatabaseSecrets__development__MicroEMR_LocalDev_Fresh`). The development store is not an encrypted production vault. No credential belongs in tracked configuration, command arguments or this report. DatabaseTool's optional isolated `MicroEMR.DatabaseTool-local-development` profile is absent; establish/verify its operator values separately before changing shared API secrets.

## 6. Exact Step 127B proposal - not executed

1. Obtain separate approval naming **only the report 163 section 7 block, 273 currently missing grants, existing microemr_dev and P/T on `192.168.50.1,50013` / `EC2AMAZ-TC26CQK\TELEMED52`**. Normal grant approval does not authorize administrative grants, settings, activation or clinical work. The normal grant can be approved independently while deployment remains blocked.
2. Immediately before execution, repeat the retained read-only assertions and capture security snapshots: exact server/databases/Active assignment/one tenant identity; enabled login and mapped SIDs; no assigned roles/ownership/excess rights; exact objects/columns/owners/dependencies; effective required/excluded/protected checks, including NULL detection. Preserve direct user/public permissions, relevant role memberships and inherited effective rights. If anything differs materially, stop and re-review the affected delta. The current snapshot is not a permanent execution authorization.
3. Extract the **entire** canonical SQLCMD block to an operator-reviewed file; compare content/hash and allowlists to section 3. Use SQLCMD mode with `:On Error exit` and fail-fast (`sqlcmd -b -V 16` if that client is used), exact server and P initial context. Authenticate through an existing approved operator mechanism; no password in the command/report and no new impersonation grant. Do not run report 160's older full grant or report 162's superseded subset.
4. Execute only after approval. The existing script asserts server/database, existing mapping, tenant identity/assignment, reviewer impersonation capability, role/ownership baseline, object/column existence and absence of unexpected positive permissions. Each database uses `SET XACT_ABORT ON`, TRY/CATCH and its own transaction; it emits exact-object EXECUTE and exact-column SELECT grants, impersonates microemr_dev for postchecks, REVERTs, then COMMITs. CATCH REVERTs when needed, ROLLBACKs an active transaction and rethrows. No persistent principal/role/configuration or clinical-data change is included.
5. Expect P **12 EXECUTE**, then T **181 EXECUTE + 80 column SELECT**. P and T commit separately: **the two-database operation is not atomic**. Do not proceed to runtime configuration/restart just because the grant script succeeds.
6. Verify within each transaction using the script's exact @Exec/@Read/@Omitted lists, then independently repeat the retained metadata-only preflight after completion. Require all 273 allowlist checks=1; all 35 exclusions=0; all nine protected checks=0; broad-role flags=0; expected mapping/CONNECT and only reviewed positive rights. NULL is BLOCKED. The script prints required-right counts after commit; retain those and the postflight counters. No clinical procedure is required for these checks.
7. On unexpected rights, missing objects, failed postchecks or target drift, fail closed and stop. Do not remove DENYs, broaden roles, retry under an administrative application credential or extend the grant list. On timeout/disconnect/uncertain COMMIT, reconnect and inspect each database before deciding whether to retry. If P committed and T failed, record the partial state and seek an explicit recovery decision; do not blindly replay both batches.
8. Before execution, review a reverse delta against the immediate snapshot. Current object/column grants are all absent, so an approved reversal would remove only successfully added explicit rights. REVOKE is **not** performed or authorized here. It does not inherently restore prior effective rights: public/roles, DENYs, ownership, pre-existing column exceptions and intervening changes must be reconciled. Never revoke a right that was pre-existing or concurrently approved. Do not automatically restore a database backup, remove clinical data, or assume prior backup/recovery qualifications have been closed.

Representative verification expressions (run under the microemr_dev token, with safe REVERT/disposal; full counts use the exact candidate lists):

```sql
SELECT SUSER_SNAME() AS EffectiveLogin, USER_NAME() AS EffectiveUser, DB_NAME() AS DatabaseContext;
-- P: repeat for every @Exec entry, expected 1 after the approved grant.
SELECT HAS_PERMS_BY_NAME(N'dbo.AccessProfile_GetEffective','OBJECT','EXECUTE') AS RequiredExecute;
-- T: every @Exec and @Read entry must be 1; every @Omitted entry must be 0.
SELECT HAS_PERMS_BY_NAME(N'dbo.TenantDatabaseIdentity','OBJECT','SELECT',N'TenantUid','COLUMN') AS RequiredIdentityColumn;
-- T protected checks: each of SELECT/INSERT/UPDATE/DELETE on both tables must be 0.
SELECT HAS_PERMS_BY_NAME(N'dbo.PatientPrescriptionPrivateNote','OBJECT','SELECT') AS ProtectedRead;
SELECT HAS_PERMS_BY_NAME(N'dbo.PatientPrescriptionPrivateNote_Write','OBJECT','EXECUTE') AS ProtectedHelper;
SELECT permission_name FROM sys.fn_my_permissions(NULL,'DATABASE');
```

These P/T examples are database-specific; do not run all samples indiscriminately in one context. Exact complete metadata-only queries are retained in the ignored per-database `*-preflight.sql` artifacts; their table-variable INSERTs only construct literal check lists and do not persist data.

## 7. Git and old-computer preparation

- Intended source changes are itemized in report 164 section 4 plus report 165 section 1: 13 Step 126C source modifications, seven new Infrastructure files, one new routing-test file, and the two Step 126D Application/test modifications. Reports 157-166 are separate documentation additions to review deliberately. Existing files were not staged, reverted or overwritten.
- Reviewed source additions contain no SQL password/connection-secret assignments; no tracked appsettings, secrets or launch configuration changed. New provider files contain setting names, not credentials; test connection strings use dummy test-host/integrated-security values. This bounded review is not a whole-history secret audit.
- `.tmp/step126e` TRX and `.tmp/step127a` evidence/candidate are confirmed Git-ignored. Review `git status --short`, selected `git diff`, `git diff --check` and the intended staged diff before the user's manual commit/push. Add the seven new Infrastructure files and routing test along with their callers; omitting them makes the change incomplete. Do not stage local secrets or force-add temporary results. This step performs no staging/commit/remote operation.
- Existing normal/Auth configuration names remain compatible. DatabaseTool's extra operator profile is optional until a separately approved runtime cutover. **Rebuilt administrative API endpoints require the new settings even before that cutover**; existing configuration alone does not preserve those endpoint operations. Missing settings fail closed. Never copy this computer's credentials or activate a restricted runtime switch implicitly on the older computer.

On the old computer, from the repository root, verify an installed .NET 10 SDK (`dotnet --list-sdks`; Step 126D used 10.0.401) and reproduce only the same 24 unique tests with the existing project/packages:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~RuntimeAdministrativeConnectionTests|FullyQualifiedName=MicroEMR.Api.Tests.AddTenantUserWorkflowTests.AdministrativePlatformFailureIsRejectedBeforeCreatingIdentityOrMembership" --logger "trx;LogFileName=step127a-old-computer.trx" --results-directory .tmp/step127a-old-computer -v minimal
```

This builds the affected test project/references and uses existing restored packages. If assets are absent/stale after checkout, first run `dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj` using its existing package configuration, then the command above. Do not change SDK/test packages simply to force success. Require **24 discovered, 24 executed, 24 passed, 0 failed, 0 skipped**. No SQL credentials/fixtures are needed for these unit cases. Do not assume the older computer has Smart App Control enabled; if loading fails, record its actual error and relevant Code Integrity event without modifying security policy.

## 8. Remaining blockers and approval readiness

**READY FOR APPROVAL: the exact normal-account 273-grant proposal only. No approval requested or inferred for execution in Step 127A.** No unexpected permission drift blocks that narrow proposal. Administrative identity/rights/settings and isolated operator configuration remain **BLOCKED**. Actual microemr_dev password login, attributable API runtime identity, controlled configuration/restart, live functional/security behavior, fictional fixtures and V1-V3 remain unverified or separately approval-gated. No administration identity is verified merely because the metadata reviewer could connect.

Recommended Step 127B: obtain explicit approval for the exact normal grant block, refresh the immediate preflight/snapshot, apply only the approved missing grants and verify their effective/negative checks. Stop before runtime activation; resolve bounded administrative readiness under its own named scope. No migration replay, Auth change or broader runtime assurance is implied.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique focused tests PASS** and the existing Platform 027/tenant 0085 postflight evidence and recovery qualifications. Runtime security and visual acceptance are not verified. No GRANT/DENY/REVOKE, principal/role change, configuration change, restart, migration, clinical/fixture write, Windows security-policy change, commit or push occurred.
