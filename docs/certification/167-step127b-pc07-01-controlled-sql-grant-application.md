# Step 127B - controlled SQL grant application

Date: 2026-10-20 (America/Toronto). **273 approved grants COMMITTED; effective-permission and security-negative postflight PASS. Runtime activation and administrative deployment remain BLOCKED.**

## 1. Git checkpoint and authorization

Branch `main`; HEAD `a8d5a6ac57dcdd5222c7b8b05d32fa7765441dec`. Entry: 15 modified tracked files, seven new Infrastructure files, one new routing-test file and reports 157-166 untracked. Existing source, tests and reports preserved. Only this report is added as a deliverable; local evidence is Git-ignored.

The attached Step 127B instruction explicitly authorized the exact 273 grants reviewed in [Step 127A](166-step127a-pc07-01-final-sql-grant-preflight.md): existing `microemr_dev`, SQL endpoint `192.168.50.1,50013`, databases `MicroEMR_Platform` (P) and `MicroEMR_LocalDev_Fresh` (T), conditional on fresh preflight. It did not authorize additional privileges, administrative credential configuration, runtime activation or clinical operations.

## 2. Exact artifact and fresh preflight - PASS

Used the complete SQLCMD block in [Step 126B section 7](163-step126b-pc07-01-sql-runtime-identity-and-permission-scope.md#7-revised-normal-runtime-grant-candidate---not-executed), retained as `.tmp/step127a/reviewed-candidate.sql`. Exact text comparison against the report passed; SHA-256 remains `7F204F47903184933C5AD8E2C15C5DB3590D2708B806C4D4FD1C12A8AB37CE0B`. No permission list was reconstructed or replaced. The unchanged allowlists expand to 193 procedure EXECUTEs and 80 column SELECTs.

Fresh preflight completed **14:25:34 -04:00** / `18:25:34 UTC`, before execution. Metadata was compared with Step 127A, including complete definitions and ownership of all 228 required/excluded procedures, all 80 required columns, direct dependency rows, identities, mappings, roles, explicit user/public permissions and effective rights. No unexpected difference was found.

- Server returned `EC2AMAZ-TC26CQK\TELEMED52`; only P and T database contexts were opened.
- P's expected Active assignment remains tenant `F94C6ECD-4417-483E-8A94-0507CF8384C2`, `local-sql`, T and reference `development:MicroEMR_LocalDev_Fresh`. T returned exactly one matching identity, key `local-dev-fresh`.
- Login `microemr_dev` remained enabled; both INSTANCE-authenticated SQL_USER mappings matched its SID. No assigned server/database roles or database/schema/object ownership was found. All five broad-role flags remained 0; CONNECT remained effective.
- All **273 required additions were missing**, with no NULL result. All **35 exclusions were unavailable** and all **nine protected-object checks were 0**. Existing public DENYs were intact.
- Current explicit rights matched the prior CONNECT-only user baseline and existing public rights. No object/column/definition/dependency discrepancy required a changed grant set.

Named database-access boundary checks were captured before execution without entering those databases: `HAS_DBACCESS(MicroEMR_Auth)=0`; `HAS_DBACCESS(MicroEMR_Db)=1`. The latter is **pre-existing database access**, not a newly granted permission or a verified right to read clinical data. Neither database was connected to or modified. P/T TRUSTWORTHY and database chaining were both OFF.

## 3. Controlled execution - COMMITTED

Execution identity: existing **sa**, solely as the controlled operator, not as an application-runtime verification identity. Existing credential was supplied to the `sqlcmd` child through its environment; no password appeared in arguments, tracked files or output. Existing encryption/trust options were preserved.

Execution window: **2026-10-20 14:25:52.626-14:25:53.327 -04:00** (America/Toronto), equivalent to **18:25:52.626-18:25:53.327 UTC**. SQLCMD used the exact approved input, exact endpoint/P initial context, `:On Error exit`, `-b -V 16`, and the existing script's `XACT_ABORT`, per-database TRY/CATCH transactions and effective-token postchecks. No alternate grant or corrective statement was issued.

| Database | Attempted EXECUTE | Attempted column SELECT | Committed | Failed | Rolled back |
| --- | ---: | ---: | ---: | ---: | ---: |
| MicroEMR_Platform | 12 | 0 | **12** | 0 | 0 |
| MicroEMR_LocalDev_Fresh | 181 | 80 | **261** | 0 | 0 |
| Total | **193** | **80** | **273** | **0** | **0** |

SQLCMD returned **exit 0** and two `Required-only permission batch committed` result rows with counts P `12/0` and T `181/80`. These results occur after each COMMIT; independent postflight confirms the resulting exact permission delta. Standard error contained only the two expected database-context notices. No rollback/retry was needed. P and T committed separately; this was not a single atomic cross-database transaction.

## 4. Independent postflight - PASS

Completed **14:26:02.960 -04:00** / `18:26:02.960 UTC`. New metadata connections used isolated session-local `EXECUTE AS LOGIN=N'microemr_dev'`, TRY/CATCH/REVERT and connection disposal. Effective login and mapped user both returned `microemr_dev`. No clinical stored procedure or protected-content query was invoked.

| Effective check | Expected | Actual | Result |
| --- | ---: | ---: | --- |
| P required procedure EXECUTE | 12 allowed | 12 allowed | PASS |
| T required procedure EXECUTE | 181 allowed | 181 allowed | PASS |
| T required column SELECT | 80 allowed | 80 allowed | PASS |
| Missing/unavailable required rights | 0 | 0 | PASS |
| P excluded EXECUTEs | 15 unavailable | 15 unavailable | PASS |
| T excluded EXECUTEs | 20 unavailable | 20 unavailable | PASS |
| Protected current-note table SELECT/INSERT/UPDATE/DELETE | 4 unavailable | 4 unavailable | PASS |
| Protected revision table SELECT/INSERT/UPDATE/DELETE | 4 unavailable | 4 unavailable | PASS |
| Internal `PatientPrescriptionPrivateNote_Write` EXECUTE | 1 unavailable | 1 unavailable | PASS |

All required/excluded/protected checks returned 0 or 1 as expected; **no NULL**. Catalog delta contains exactly P **12 EXECUTE GRANT** rows and T **181 EXECUTE GRANT + 80 column SELECT GRANT** rows, each matching the approved object/column allowlist and existing principal. There are no additional object permissions or grant-option entries. Existing CONNECT, public grants and all nine protected DENYs are unchanged.

Login/user mapping, enabled state, server/database role membership, ownership, effective server/database-wide rights, definitions, columns and dependencies match fresh preflight. Sysadmin/db_owner/db_ddladmin/db_datareader/db_datawriter flags remain 0 in both databases. No CONTROL, ALTER, schema-wide permission or migration/provisioning privilege was added.

**Isolation:** executed SQL has only the two reviewed USE targets and one grantee. No grant targeted Auth or another tenant. Auth access remains **0**; the named secondary tenant's pre-existing database access remains **1**. P/T TRUSTWORTHY/database chaining remain OFF; no cross-database dependency or new effective server privilege appeared. These checks establish the scoped execution/delta and unchanged named access boundaries; they are not an inspection of permissions inside every other database or a claim that the secondary tenant had no prior rights.

## 5. Evidence, qualifications and remaining blockers

Ignored local evidence under `.tmp/step127b/`: `preflight.json`, `execution.json`, `postflight.json`, `sqlcmd-output.txt`, `sqlcmd-errors.txt`, `control.ps1`, and Git/file-preservation snapshots. The controller checks the exact candidate hash/canonical text, compares the fresh baseline, refuses automatic execution replay, and performs independent allowlist/delta/negative comparisons. No build or application test ran; Step 126E's **24 unique focused tests PASS** is retained without rerun.

No execution failure, rollback, unexpected permission change or security-negative failure was observed. SQL token verification is **not** proof of microemr_dev password authentication, a running API identity, end-user authorization, clinical workflow success or visual acceptance. No runtime was activated with these grants.

Step 127A's administrative blockers remain unresolved: a bounded administrative identity/rights have not been verified; `ConnectionStrings:AdministrativePlatformDatabase` and `AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` were absent at the prior check; isolated DatabaseTool operator configuration was absent. This step neither populated nor changed those settings. Missing administrative settings continue to fail closed on affected operations. Auth configuration/rights remain outside this change.

## 6. Recommended Step 127C and stop boundary

Scope Step 127C to verifying a named existing bounded administrative identity, its exact effective privileges and target bindings, and isolated DatabaseTool configuration readiness. Prepare a separately reviewed activation plan identifying the actual API/Web launcher and effective configuration sources. Do not infer approval to create accounts, add administrative grants, populate credentials, switch runtime strings or restart services from this completed grant step.

Only after separately authorized configuration/activation and actual runtime identity evidence should fictional fixtures and V1-V3 acceptance proceed under their own scope. Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique tests PASS** and existing 027/0085 postflight/recovery qualifications. SQL permission PASS does not satisfy PC07.01 or verify runtime security/visual acceptance.

No application source, earlier report, active connection string, administrative credential, SQL principal/role, Windows security setting or clinical record changed. No migration, fixture creation, service restart, staging, commit, merge or push occurred. **STOP after grant execution, verification and this report.**
