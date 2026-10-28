# Step 222 — Current development backup and checksum verification

2026-10-28 (America/Toronto). **CURRENT BACKUP CREATED AND VERIFIED.** Exactly one owner-authorized normal FULL database backup completed with CHECKSUM, followed by exactly one successful RESTORE VERIFYONLY WITH CHECKSUM. **RESTORATION TESTED — NO. Recovery readiness remains CONDITIONAL** pending owner approval of this recovery point and the proposed restoration procedure. No migrations, grants, clinical changes or service restarts occurred.

## 1. Branch, worktree and authorization

Read AGENTS.md and reused the relevant Step 221 recovery and Step 220 activation/permission findings. Actual branch was **`main`**. The two pre-existing untracked reports, 294 and 295, were preserved byte-for-byte along with AGENTS.md, confirmed by SHA-256 checks. This report did not exist before execution or creation. Remained on main; no Git mutation occurred. Added only this report outside ignored `.tmp/step222/` operational receipts/helpers.

The supplied Step 222 owner authorization covers **one new uniquely named full database backup, CHECKSUM, one checksum VERIFYONLY, and documentation of a proposed restore procedure**. It does not authorize a database restore, migration, GRANT/REVOKE, clinical/fixture write, service restart, deployment, old-backup replacement or general administration.

No application/source/configuration/migration edits, builds or automated tests were performed. Operator/runtime credentials were read only from existing protected configuration and were not printed or saved in receipts. Production databases were not accessed.

## 2. Pre-execution safety — PASS

Read-only checks, including immediate revalidation before BACKUP, established:

| Check | Evidence |
|---|---|
| Exact connected server | **`EC2AMAZ-TC26CQK\TELEMED52`** |
| Exact connected database | **`MicroEMR_LocalDev_Fresh`** |
| Tenant | **`local-dev-fresh`**, single expected UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`** |
| State / recovery model | **ONLINE / FULL** |
| Migrations | 0098 and 0099 recorded as applied; neither rerun |
| Established destination | Matches SQL Server's instance default backup directory and exists |
| SQL backup privilege | Effective BACKUP DATABASE permission present |
| Service identity / OS access | `NT Service\MSSQL$TELEMED52`; directory ACL explicitly grants this account inherited object/container **Full Control** |
| New filename | UTC timestamp plus random GUID; exact destination absent and not a directory; parent exists |
| Disk capacity | **292,005,801,984 bytes free** on C: (about 272 GiB), versus **150,994,944 allocated database bytes**; exceeded the conservative twice-allocation-plus-1-GiB guard |
| Conflicting operations | No active backup/restore requests on inspection/revalidation |

OS access was inspected using read-only `whoami /user` and `icacls` through the already-enabled SQL command facility under the service identity. No server setting, ACL, role or permission was changed. The actual output names the same service account and its Full Control entry. Directory/file existence and capacity used SQL read-only metadata/functions.

### Backup strategy decision recorded before execution

History contained only the October 22 manual copy-only full backup, no recorded differential/log backup chain, and no enabled SQL Agent backup/maintenance job was found. No established differential strategy requiring COPY_ONLY was identified. Therefore the owner's requested default **normal FULL backup** was used: **COPY_ONLY was not specified**. A normal full establishes a new differential base; it does not overwrite the old backup media. Recovery model FULL alone is not evidence of continuous point-in-time protection.

## 3. New backup file and exact operation

SQL-server-side directory:

```text
C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup
```

Exact new file:

```text
C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_step222_20261028T160740Z_c4d43cffea5c499cb4778f83dd0898c6.bak
```

The filename timestamp was generated during preflight; actual execution times are below. The exact path was checked absent in preflight and again immediately before BACKUP in the SQL batch. INIT was used only on that new, verified-nonexistent destination; no append, replacement or deletion of existing media occurred. A local create-once operation marker prevents the helper being rerun as an automatic retry.

The **single executed BACKUP command** was parameterized:

```sql
BACKUP DATABASE [MicroEMR_LocalDev_Fresh]
TO DISK = @File
WITH INIT, CHECKSUM, STOP_ON_ERROR, NAME = @BackupName, STATS = 10;
```

`@File` was bound to the exact new path above; `@BackupName` was a descriptive Step 222 name containing the unique filename. Read-only overwrite guards preceded it. No COPY_ONLY or CONTINUE_AFTER_ERROR was used.

## 4. Execution and VERIFYONLY results — PASS

| Event | UTC receipt timestamp | Toronto local time (EDT) |
|---|---|---|
| Backup started | `2026-10-28T16:10:25.6259780+00:00` | `2026-10-28 12:10:25.6259780` |
| Backup completion observed | **`2026-10-28T16:10:25.9203909+00:00`** | **`2026-10-28 12:10:25.9203909`** |
| VERIFYONLY started | `2026-10-28T16:10:25.9239593+00:00` | `2026-10-28 12:10:25.9239593` |
| Verification completion observed | **`2026-10-28T16:10:26.0975990+00:00`** | **`2026-10-28 12:10:26.0975990`** |

SQL's separately recorded backup start/finish timestamps are both **`2026-10-28T16:10:26`**. They have different precision from the client-observed receipt timestamps and are retained as separate observations. Preflight showed the server wall clock and UTC aligned.

SQL Server reported:

> BACKUP DATABASE successfully processed 2394 pages in 0.128 seconds (146.087 MB/sec).

The **single executed verification operation** was:

```sql
RESTORE VERIFYONLY
FROM DISK = @File
WITH FILE = 1, CHECKSUM, STOP_ON_ERROR;
```

SQL Server reported:

> The backup set on file 1 is valid.

**CHECKSUM and VERIFYONLY PASS.** No backup/verification failure, automatic retry or second backup occurred. No RESTORE DATABASE, restore rehearsal, HEADERONLY or FILELISTONLY was executed. Later local formatting corrections affected only read-only receipt/process comparisons, not the completed SQL operations.

## 5. Backup metadata verification — PASS

Current msdb media/history and file metadata matched the new unique file:

| Property | Verified value |
|---|---|
| Backup set ID | **45** |
| Backup set UUID | `028F4272-0FAA-4ECB-A2C9-F7F6352683D4` |
| Database / server | Exact intended target above |
| Backup type / copy-only | **D — FULL / false** |
| Position / family sequence | **1 / 1**; exactly one matching set/media row |
| SQL start/finish | `2026-10-28T16:10:26` / `2026-10-28T16:10:26` |
| Checksums / damaged | **true / false** |
| Backup payload / compressed payload | **21,065,728 / 21,065,728 bytes** |
| Physical backup file | **21,094,400 bytes**, existence confirmed |
| First / last LSN | `42000001660800001` / `42000001663200001` |
| Logical data/log files | `MicroEMR_LocalDev_Fresh` / `MicroEMR_LocalDev_Fresh_log` |

Unique filename, successful set-1 checksum verification, matching database/server/name/path/type/checksum/media-position metadata and file existence establish the intended new backup without an unexpected set mismatch. The October 22 backup set **44**, its path and checksum/copy-only metadata remain present; its file still exists. No operation wrote to, replaced or deleted that older file.

Receipts: `.tmp/step222/preflight.json`, `directory-acl.txt`, `strategy-decision.txt`, `result.json`, `process-checks.json`, and `runtime-permissions-after.json`. They contain target/backup/aggregate metadata and native messages, not credentials, tokens or clinical record contents.

## 6. Proposed restoration runbook — NOT EXECUTED

**Status: documented proposal requiring owner approval; restoration tested NO.** This backup is a candidate current **post-0099 / pre-directory-grant** recovery point. It is not a retroactive pre-0099 backup. The owner must approve the exact point and procedure before it is treated as activation recovery readiness.

### Target, prerequisites and access

The source recovery point is backup **45**, position **1**, at the exact Step 222 path above, on the verified development instance. The protected live database is MicroEMR_LocalDev_Fresh / local-dev-fresh and its expected tenant UID. A rehearsal should restore under a **new isolated database name**, with separate new MDF/LDF destinations, leaving the live tenant intact.

The proposed example below uses **`MicroEMR_LocalDev_Fresh_RecoveryCheck_Step223`** and correspondingly named files. These names/paths are proposals only, not reserved, created or approved. Before an authorized restore, independently verify that the isolated database/files do not exist, paths stay within the explicitly approved development SQL data directory, sufficient disk is available, and the SQL service can read the backup and write those destinations. Confirm the approved operator's restore/create-database privileges and any required key material. Do not grant new rights or assume the runtime application user should restore databases.

A later restore must freshly validate the selected media's identity, position and logical file layout under its own authorization. No new HEADERONLY/FILELISTONLY or second VERIFYONLY was run by Step 222. Restore validation and checksum verification alone must not be described as an already successful rehearsal.

### Application isolation and active connections

For an **isolated new-database rehearsal**, keep current Web/API/Auth running against their current verified target; do not repoint them or the platform tenant assignment to the recovery copy. The proposed isolated target must have no existing database/connections. If it already exists or has users, stop for a new explicit target decision instead of killing sessions or overwriting it.

An actual live-tenant recovery/cutover is a different operation: obtain separate approval for its outage, loss boundary, evidence/history preservation, write pause and exact connection handling. Drain only the approved target's connections and preserve active work using the approved procedure. This runbook does not authorize SINGLE_USER, ROLLBACK IMMEDIATE, session termination, WITH REPLACE, overwrite of live data/log files, shared/production routing changes or restoration of unrelated databases.

### Restoration SQL — DOCUMENTATION ONLY, NOT EXECUTED

After explicit authorization and the prerequisite/target checks above, an isolated full-only restore could use:

```sql
RESTORE DATABASE [MicroEMR_LocalDev_Fresh_RecoveryCheck_Step223]
FROM DISK = N'C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_step222_20261028T160740Z_c4d43cffea5c499cb4778f83dd0898c6.bak'
WITH FILE = 1,
     MOVE N'MicroEMR_LocalDev_Fresh'
       TO N'C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\DATA\MicroEMR_LocalDev_Fresh_RecoveryCheck_Step223.mdf',
     MOVE N'MicroEMR_LocalDev_Fresh_log'
       TO N'C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\DATA\MicroEMR_LocalDev_Fresh_RecoveryCheck_Step223_log.ldf',
     RECOVERY, CHECKSUM, STOP_ON_ERROR, STATS = 10;
```

This restores only the selected full point. No verified subsequent differential/log chain or point-in-time recovery guarantee is established. Do not append hypothetical RESTORE LOG steps or run migrations automatically on the restored copy.

### Post-restore validation, only under separate restore authorization

1. Run approved integrity checks, including DBCC CHECKDB, on the isolated restored database and record actual outcome/duration. Do not claim measured recovery time beforehand.
2. Verify the single expected TenantDatabaseIdentity UID/key, source database identity and SchemaMigration. Expect **0098 and 0099 already applied**, with their approved hashes. Do not rerun either migration to validate restoration.
3. Verify relevant directory constraints/collations/procedure definitions and clinical/audit/artifact preservation using approved bounded reads. The backup includes SQL-resident prescription artifact JSON. External patient files/file-backed artifacts require a matching approved recovery set; this SQL backup alone does not establish their recovery consistency.
4. Expect the backup's **pre-grant state**: microemr_dev lacks execution on the three directory procedures. A later recovery must account for that state; this runbook does not automatically reapply grants or alter roles.
5. If application validation against the isolated copy is required, obtain separate approval for a dedicated test configuration/instance. Confirm Web/API match the restored schema and the approved routing, while retaining tenant/patient/authorization controls. Do not repoint the running tenant as an implicit test or use mutating clinical smoke operations without explicit scope.
6. Leave Auth untouched. Its startup runs migrations/seeding; a restart is not a restoration-validation shortcut and would need separate approval covering those effects. Do not restore Auth/Platform solely because a single tenant copy is being checked.
7. Have the owner review evidence and preservation/loss limitations before any actual cutover decision. A successful isolated rehearsal would still not authorize replacing the live tenant or physically deleting later clinical/audit history.

### Recovery limitations

The new file is current and checksum-verified, but **no actual restore has been demonstrated**. It resides on the same SQL server/C: volume, so alone it does not protect against loss of that host/volume. No off-host durability, paired external-file checkpoint, retention policy, continuous log chain, restoration RTO or owner-approved cutover procedure is established by this operation. Changes after backup completion require their own preservation/recovery strategy; restoration must not silently discard newer clinical history.

## 7. No unrelated changes — confirmed

| Postcheck | Result |
|---|---|
| 0098 and 0099 ledger/hash/applied timestamps | Unchanged before/after; both remain applied |
| Directory procedure grant rows | Unchanged |
| Runtime `microemr_dev` effective EXECUTE | **0 / 0 / 0** for Get / Search / Save; no grant or revoke executed |
| Database rowversion indicator | Unchanged, `0x0000000000002730` |
| Audit aggregate indicators | Unchanged, 1,420 rows / max AuditLogId 1,420 |
| Auth / API / Web processes | Same PIDs **10124 / 5696 / 27556**, creation timestamps agree at receipt millisecond precision |
| Pre-existing worktree files | Hash-preserved; no Git mutation |
| Old backup | Still present; not overwritten/deleted |

The only intended persistent write operation was the authorized new BACKUP, including its normal SQL backup/history/recovery metadata effects. The verification and other SQL inspections did not execute application DML, schema changes, grants or migrations. No clinical record mutation was issued; the limited rowversion/audit indicators also remained unchanged. No Web/API/Auth process was restarted by this task. No broad application test or rebuild occurred.

## 8. Classification, owner decisions and Step 223

**CURRENT BACKUP CREATED AND VERIFIED.**

**RESTORATION TESTED — NO.**

**RECOVERY READINESS — CONDITIONAL** until the owner approves this exact recovery point and proposed restoration procedure, with their limitations. Creating/verifying the backup does not imply that approval and does not authorize a restore or pending grants.

Required next decisions:

- Accept or reject backup **45**, position **1**, its exact path and October 28 completion point as the recovery baseline for the remaining local activation work. Acknowledge that it is post-0099 and captures the currently missing directory execution rights.
- Approve a specific usable restoration procedure/operator, isolation/file targets and preservation/loss boundary; record that actual restoration remains untested. If an isolated restore rehearsal is desired, authorize it separately rather than treating VERIFYONLY as that test.
- Only after accepting recovery readiness, separately authorize the three narrow EXECUTE grants from report 294 and bounded read-only/authenticated acceptance. No migration reapplication is needed; compatible existing Web/API may be reused. Any fixture/clinical save or process operation requires its own scope; Auth remains excluded.

**Recommended Step 223:** record owner approval of this current recovery point/runbook and decide whether a separately scoped isolated restore rehearsal is required. If the owner accepts the stated recovery limitations, obtain explicit authorization for the three still-missing object grants and minimal activation acceptance; do not execute them based on this backup-only authorization.

Retain Stage 1/2 automated evidence without reruns, DE09.034/DE09.035 qualified closures, authenticated UI qualification, earlier NU1900 qualification and Auth restart restrictions. **DE09.032 PARTIAL; PC07.01 PARTIAL; mandatory-gap inventory 7 IDs / 3 packages.** Backup verification alone does not change certification status or declare activation complete.
