# Step 149 — Gate 1 development database backup and verification

2026-10-22. **Gate 1 PASS. Gate 2 NOT APPROVED / NOT EXECUTED.** One owner-authorized full backup completed and RESTORE VERIFYONLY with checksum verification succeeded. No database restore or clinical testing performed.

## Scope and checkpoint

Read AGENTS.md; reused the backup/recovery proposal and runtime evidence in [213](213-step146-pc07-01-recovery-and-controlled-smoke-acceptance.md), [214](214-step147-pc07-01-api-runtime-diagnosis.md) and [215](215-step148-pc07-01-runtime-refresh-authenticated-verification.md). No previous investigation or automated suite repeated.

Branch **`feature/step-147-api-swagger-runtime-fix`**, HEAD **`0a561092e595d2d963de40065a9eb0727094fa92`**. Existing API Program.cs/Web permission-guard modifications, untracked reports 211–215, Swagger configuration and Step 144/147 tests preserved. No branch change, staging, commit, push, reset, clean or merge. No product/test/configuration edits or service restarts. Only this report added outside ignored Gate 1 helper/evidence.

## Target identity and destination

Before destination inspection or backup, the fixed connection and actual database identity were checked at **2026-10-22 22:57:48.1798875 UTC**:

| Property | Verified value |
|---|---|
| SQL endpoint | `192.168.50.1,50013` |
| Actual server / instance | `EC2AMAZ-TC26CQK\TELEMED52` |
| Database | `MicroEMR_LocalDev_Fresh` |
| Tenant key | `local-dev-fresh` |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Approved SQL-server-side directory | `C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup` |

Directory metadata confirmed existence/accessibility; SQL BACKUP DATABASE permission was present. Server C:\ volume had **292,117,041,152 bytes free** (about 272 GiB), versus **150,994,944 bytes** allocated database/log files. The unique destination did not exist before execution. Successful creation and subsequent verification demonstrate SQL Server service write/read access for this file; no ACL or service configuration changed.

## Backup execution — PASS

Server-side file:

```text
C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_step149_20261022T225747921Z_7d074a52fb5f41e8a27959fa0fec86c6.bak
```

The filename includes UTC timestamp and a unique GUID, with a prior existence guard. Executed only:

```sql
BACKUP DATABASE [MicroEMR_LocalDev_Fresh]
 TO DISK = @File
 WITH COPY_ONLY, CHECKSUM, STOP_ON_ERROR, NOINIT, STATS = 10;
RESTORE VERIFYONLY FROM DISK = @File WITH CHECKSUM, STOP_ON_ERROR;
RESTORE HEADERONLY FROM DISK = @File;
```

`@File` was bound to the exact new path above. No existing backup overwritten; no database restore. COPY_ONLY does not replace the differential base. No CONTINUE_AFTER_ERROR used.

| Evidence | Result |
|---|---|
| Client-observed backup start UTC | `2026-10-22T22:57:48.3344566+00:00` |
| Client-observed backup completion UTC | **`2026-10-22T22:57:48.5751311+00:00`** |
| Client-observed verification completion UTC | **`2026-10-22T22:57:48.6434590+00:00`** |
| Actual filesystem file size | **20,045,824 bytes (about 19.12 MiB)** |
| SQL backup_size / compressed_backup_size | **20,017,152 / 20,017,152 bytes**; payload size differs from whole-file size |
| Backup history ID / header position | **44 / 1** |
| Header database / server | Fixed database and actual instance above |
| BackupType / IsCopyOnly | **1 (full) / true** |
| HasBackupChecksums / IsDamaged | **true / false** |
| File existence after execution | Confirmed |

SQL Server reported:

> BACKUP DATABASE successfully processed 2250 pages in 0.118 seconds (148.934 MB/sec).
>
> The backup set on file 1 is valid.

**Checksum and VERIFYONLY PASS. No SQL warnings or errors reported**; other messages were progress/page counts. Header/history and filesystem metadata corroborate the completed backup. Sanitized evidence: `.tmp/step149/gate1-result.json`; helper `.tmp/step149-gate1.ps1`. Credentials were used only in memory and not printed.

An initial local helper connection-option alias error occurred **before opening SQL**. Corrected the PowerShell option names, then performed the single backup/verification above. No failed SQL backup/verification was retried and no second backup created.

## Recovery limitations and disposition

**A verified backup is not a tested full restore.** VERIFYONLY/checksums validate this backup's readability/integrity; no restore rehearsal, recovery-time objective, off-server durability, retention policy or restore permissions were proven. The backup resides on the same server/volume, so it does not by itself protect against loss of that server/volume. No point-in-time log chain is established by this operation.

This backup captures the current state with already-applied migrations 0089/0090. It cannot by itself restore pre-migration schema. Any future restore requires separate explicit approval, verified target and controlled writes; changes after this backup would need a suitable recovery chain or would be lost. No down migration or restore command executed.

**Gate 2: NOT APPROVED. NOT EXECUTED.** No clinical content writes, fixture creation, smoke tests, migration execution, API/Web/Auth restart, database restore or Git write operation. Existing authenticated-read evidence from Step 148 retained; no new acceptance claims beyond Gate 1.

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Prior certification closures/qualifications and the three pending subjective owner wording/layout checks remain unchanged. Stop after Gate 1; bounded synthetic clinical smoke requires its own explicit Gate 2 record/account/action authorization.
