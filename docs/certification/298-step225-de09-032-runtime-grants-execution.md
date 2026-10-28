# Step 225 — Three authorized runtime EXECUTE grants executed

2026-10-28 (America/Toronto). **Transaction COMMITTED; all three exact object-level EXECUTE grants verified. Actual runtime effective EXECUTE changed from 0/0/0 to 1/1/1. No unrelated permission, role, schema, migration, clinical or Web/API/Auth process changes were observed.** No directory stored procedure was invoked. Authenticated workflow acceptance remains separate; DE09.032 and PC07.01 remain PARTIAL, inventory 7 IDs / 3 packages.

## 1. Branch and existing work

Read AGENTS.md and reused the relevant Step 224 grant-preparation evidence. Actual branch was **`main`**. The pre-existing untracked reports 294–297 were identified and preserved byte-for-byte, with SHA-256 comparisons before/after this task. Remained on main; no checkout, branch creation, staging, commit, merge, push, pull, fetch, reset, clean or stash occurred.

This report did not exist before execution. Created only this report. Execution helpers, metadata snapshots and results were held in memory/tool output; no helper/receipt file, application/configuration/SQL/migration source change was created. No builds or test suites were run.

## 2. Explicit owner authorization

The supplied Step 225 instruction explicitly authorized exactly three object-level EXECUTE grants to `microemr_dev` on `EC2AMAZ-TC26CQK\TELEMED52` / `MicroEMR_LocalDev_Fresh`, together with read-only guards, transactional execution, read-only verification and this report.

No other database/application modification was authorized or performed. Backup/recovery evidence was reused without a new assessment or operation. No credentials or connection secrets were printed or included in the report. Production databases were not accessed.

## 3. Verified target and pre-execution security state

The operator connection was independently checked by SQL metadata, not merely by its configuration name. The actual runtime connection was also independently checked against the same target:

| Check | Verified result |
|---|---|
| Server | **`EC2AMAZ-TC26CQK\TELEMED52`** |
| Database | **`MicroEMR_LocalDev_Fresh`** |
| Tenant | **`local-dev-fresh`**, exactly one UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`** |
| Database state | **ONLINE** |
| 0098 and 0099 | **Both APPLIED**, approved recorded hashes match |
| Runtime principal | **`microemr_dev`**, principal ID **5**, SQL_USER / INSTANCE authentication |
| Runtime effective EXECUTE | Get **0**, Search **0**, Save **0** |
| Runtime database/schema-wide EXECUTE | **0 / 0** |
| Runtime roles / sysadmin | **public only / 0** |
| Applicable unexpected grants or DENYs | **None** at the relevant user/public database, dbo-schema and object scopes |
| Operator authority | Effective CONTROL **1** on each of the three objects; sufficient existing authority to issue the grants |

Procedure identities matched Step 224:

| Procedure | object_id | Type / effective owner |
|---|---:|---|
| `[dbo].[DispensingLocation_Get]` | 333244242 | SQL_STORED_PROCEDURE / dbo, owner ID 1 |
| `[dbo].[DispensingLocation_Search]` | 349244299 | SQL_STORED_PROCEDURE / dbo, owner ID 1 |
| `[dbo].[DispensingLocation_Save]` | 365244356 | SQL_STORED_PROCEDURE / dbo, owner ID 1 |

An initial local helper precheck stopped **before any transaction or GRANT was attempted** because it compared SQL Server's padded `char(2)` procedure type `P ` against `P` without trimming. Read-only inspection confirmed that the IDs, SQL stored-procedure types, owners and operator authority matched the plan. The in-memory comparison was corrected for fixed-width metadata padding, including permission type `EX  `. No persistent script was edited, no security condition changed, no SQL error occurred and no submitted grant transaction was retried. The completed guard then passed and the single authorized transaction was executed.

## 4. Single guarded transaction — COMMITTED

The prepared transaction core was extracted from the unchanged Step 224 report, whose SHA-256 matched **`DD05114C5EEB756118E7BE811BFBB8E7410C9DB53DAFCA619654AE58A8C38603`**. Its scope was checked to contain exactly the three approved GRANT statements, with no backup/restore, revoke, schema or application DML statements.

The **same verified operator connection/database context** was retained for execution. The core used SET XACT_ABORT ON and TRY/CATCH, rejected existing ambient transactions, rechecked target/tenant/migrations/principal/object identities, role/ownership conditions and applicable permissions, and used **one SQL Server transaction**. It checked explicit/effective rights before commit; its failure handler would revert impersonation, roll back an active transaction and rethrow. No failure of the grant transaction occurred.

Exactly these persistent permission changes were executed once:

```sql
GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Get]
    TO [microemr_dev];

GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Search]
    TO [microemr_dev];

GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Save]
    TO [microemr_dev];
```

| Execution event | UTC receipt | Toronto local time (EDT) |
|---|---|---|
| Transaction submitted | `2026-10-28T16:45:11.4064282+00:00` | `2026-10-28 12:45:11.4064282` |
| Successful COMMIT observed | **`2026-10-28T16:45:11.4888536+00:00`** | **`2026-10-28 12:45:11.4888536`** |

Post-execution `@@TRANCOUNT = 0` and `XACT_STATE() = 0`. **Transaction result: COMMITTED.** Three GRANT statements were executed; no REVOKE or automatic retry occurred.

The transaction's EXECUTE AS USER / REVERT operations performed only safe effective-permission SELECTs. They did not invoke any directory procedure or grant impersonation privileges, and returned to the operator context before commit.

## 5. Explicit and effective post-execution permissions — PASS

Operator catalog metadata returned:

| Object | Grantee | Permission | State | Grantor | Scope |
|---|---|---|---|---|---|
| `[dbo].[DispensingLocation_Get]` | microemr_dev | EXECUTE | GRANT | dbo | OBJECT_OR_COLUMN, minor_id 0 |
| `[dbo].[DispensingLocation_Search]` | microemr_dev | EXECUTE | GRANT | dbo | OBJECT_OR_COLUMN, minor_id 0 |
| `[dbo].[DispensingLocation_Save]` | microemr_dev | EXECUTE | GRANT | dbo | OBJECT_OR_COLUMN, minor_id 0 |

All three rows have state **G**, not grant-with-grant-option. The complete database permission snapshot changed from **485 to 488 rows**: exactly three added rows for the specified object IDs, principal ID 5, EXECUTE, object scope and state G; **zero removed or otherwise changed permission rows**.

The actual configured runtime connection, independently verified as `microemr_dev` on the same server/database/tenant, returned:

| Effective check | Before | After |
|---|---:|---:|
| Get EXECUTE | 0 | **1** |
| Search EXECUTE | 0 | **1** |
| Save EXECUTE | 0 | **1** |
| Database-wide EXECUTE | 0 | **0** |
| dbo schema-wide EXECUTE | 0 | **0** |
| Sysadmin | 0 | **0** |
| Effective database roles | public only | **public only** |

**Effective runtime EXECUTE VERIFIED — PASS**, through the real runtime credential as well as the transaction's safe database-user permission checks. No stored procedure execution or API smoke/write request was used to prove the permission. In particular, **DispensingLocation_Save was not invoked**.

## 6. No unrelated changes — PASS

Full read-only before/after snapshots were compared, including the permission/security delta and the following unchanged metadata:

| Area | Result |
|---|---|
| Server/database/tenant identity and database state/collation | Unchanged |
| Complete migration ledger | Unchanged; **0098 and 0099 remain applied**, no migration executed |
| Role memberships | Unchanged |
| Database principals and ownership metadata | Unchanged |
| Schemas | Unchanged |
| Non-system objects, definition hashes and modification metadata | Unchanged |
| Column definitions | Unchanged |
| Database rowversion indicator | Unchanged, `0x0000000000002730` |
| Audit aggregate indicators | Unchanged, **1,420 rows / max AuditLogId 1,420** |
| Local Web/API/Auth name/PID/creation-time snapshots | Unchanged; no process operation issued |
| Pre-existing worktree files | Hash-preserved |

No table permissions, schema/database-wide grants, roles, ownership, other principals or unrelated permissions were changed by this task. No patient/prescription/directory record was created, edited, activated/deactivated or deleted. No clinical write, mutating API call, migration, backup/restore/verification operation, application change, rebuild, regression suite, deployment or service restart occurred. The only new project file is this report.

## 7. Recovery and certification qualification retained

**OWNER-APPROVED LOCAL RECOVERY POINT — BACKUP VERIFIED; RESTORE UNTESTED.** Reused the Step 223 owner approval and Step 222 evidence without repeating backup/recovery analysis or operations. This permission operation does not create a new recovery point or demonstrate restoration.

Keep **DE09.032 PARTIAL; PC07.01 PARTIAL; mandatory-gap inventory 7 IDs / 3 packages**. Preserve DE09.034 and DE09.035 qualified closures, Stage 1/2 automated evidence, authenticated UI acceptance qualification, earlier NU1900 qualification and Auth restart restrictions. The three grants resolve the runtime SQL permission gap; they do not establish authenticated clinical workflow acceptance or full directory activation acceptance.

## 8. Remaining authenticated checks and recommended Step 226

**Recommended Step 226:** separately authorize a bounded authenticated acceptance check for directory maintenance visibility, active search/detail retrieval, Draft Create/Edit selection, optional name/address confirmation, inactive-location behavior and historical prescription Detail preservation. Reuse suitable existing fictional development fixtures first and the already passing automated evidence.

Form-only preview/cancel/selection and read-only detail observations need not repeat clinical saves already covered by automation. Any directory/clinical fixture creation, edit, deactivate/reactivate or prescription save must have separate explicit scope/authorization. No grant should be repeated, no migration is needed, and no service restart is implied. Stop Step 225 after this report; do not perform that later acceptance workflow under the grant-only authorization.
