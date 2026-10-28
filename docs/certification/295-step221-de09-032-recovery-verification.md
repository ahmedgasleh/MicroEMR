# Step 221 — DE09.032 read-only recovery verification

2026-10-28 (America/Toronto). **Recovery remains CONDITIONAL / not established as suitable for current local activation.** The latest recorded full backup is the existing Step 149 October 22 backup; its server-side file exists and historical checksum VERIFYONLY passed, but it predates later clinical/schema changes and there is no demonstrated full restore or owner-approved current recovery point/runbook. **Material state change: 0099 is now applied; the three runtime EXECUTE permissions remain absent.** No migration, grant, backup, verification operation, restore or restart was performed by this step.

## 1. Branch and preservation

Read AGENTS.md. Actual branch remained **`main`**. The only pre-existing worktree change was the untracked Step 220 report. Remained on main and preserved that file and the directly referenced recovery reports byte-for-byte, verified by SHA-256 comparison. This Step 221 report did not exist before creation.

Added only this report; ignored `.tmp/step221/` contains SELECT-only inspection/preservation receipts, without credentials or connection strings. No source/configuration/SQL/migration/prior-report edit, Git mutation, build, automated test, service operation, clinical read/write, backup creation/overwrite, RESTORE, HEADERONLY, FILELISTONLY or VERIFYONLY was executed. Existing metadata and recorded evidence were reused. Production databases were not accessed.

## 2. Verified development target and migration drift

The configured DatabaseTool operator connection independently returned:

| Property | Current read-only finding |
|---|---|
| Server/instance | **`EC2AMAZ-TC26CQK\TELEMED52`** |
| Database | **`MicroEMR_LocalDev_Fresh`** |
| Tenant key / UID | **`local-dev-fresh` / `f94c6ecd-4417-483e-8a94-0507cf8384c2`**, single identity row |
| Recovery model | **FULL**; this alone does not prove a usable log-backup chain |
| Observation clock | Server wall time and UTC both returned `2026-10-28T15:49:01.1780888` (11:49 EDT in Toronto) |
| 0098 | **APPLIED**, stored AppliedAt `2026-10-28T14:22:08.2936851` |
| 0099 | **APPLIED**, stored AppliedAt `2026-10-28T15:29:10.6446800` |

0098's recorded hash is `44B5AA6E2E9A2FC9732F5AEBE03869211911D843EEE32481DCD33ADAFFE42D85`; 0099's is `F99B770588FFDA3BF31321C5CF788D6E41DB61B37CB82BA01BA3DC9435543574`, matching the previously approved canonical hashes.

The three directory procedures exist. These observations supersede the supplied pending-0099 baseline and Step 219's earlier state. No attribution is made about the intervening application operation, and this report does not imply that it was performed or authorized by Step 221. **Do not rerun 0099 or seek authorization to apply it again.** A new backup now would capture a post-0099 state; it cannot retroactively establish a pre-0099 recovery point.

## 3. Latest existing backup identification

The current msdb history returned one recorded backup for this database. It is the latest full candidate and matches the Step 149 backup history ID/path:

```text
C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_step149_20261022T225747921Z_7d074a52fb5f41e8a27959fa0fec86c6.bak
```

This is a **SQL-server-side path**, not a path on the local development workstation or a newly created file.

| Backup property | Current history / retained evidence |
|---|---|
| Backup set ID / media position | **44 / 1** |
| Backup set UUID | `95992C6B-F5AA-4C12-83AA-48EA06EA5531` |
| Type | **D — full database**, copy-only |
| Start / completion, as stored in SQL | **`2026-10-22T22:57:49` / `2026-10-22T22:57:49`** |
| Age at inspection | **137 hours by SQL DATEDIFF(hour)**; approximately 5 days 17 hours, not a current checkpoint |
| Checksums / damage flag in history | Has checksums **true**; damaged **false** |
| Recorded backup payload size | **20,017,152 bytes**; this is not a new measurement of the file |
| Media layout | One disk family, sequence 1, mirror 0 |
| Current server-side file/parent existence | **Both true**, via SELECT from `sys.dm_os_file_exists` |
| Later recorded differential/log backups | **None returned** after this full backup |
| Target restorehistory | **No rows returned** for MicroEMR_LocalDev_Fresh |

Step 149 separately recorded a whole-file size of **20,045,824 bytes**, successful creation, and client-observed backup completion **`2026-10-22T22:57:48.5751311+00:00`**. Preserve the SQL-recorded and client-clock timestamps as separate observations; do not silently convert deserialized JSON dates or substitute one clock for the other. Current file size/content/unchanged media identity were not independently reverified here.

The file's existence is now corroborated, but existence and an msdb row do not prove present readability/integrity. Backups outside this server's recorded history are not ruled out; none was identified as an approved alternative by the evidence reviewed.

## 4. Verification evidence: distinct claims

| Recovery claim | Disposition |
|---|---|
| Backup file exists now | **VERIFIED** by server-side existence metadata |
| Backup completed historically | **VERIFIED** by current msdb history and retained Step 149 execution receipt |
| Historical checksum VERIFYONLY succeeded | **PASS**, Step 149; verification completion `2026-10-22T22:57:48.6434590+00:00` |
| Fresh VERIFYONLY/current media integrity | **NOT PERFORMED / not established** |
| Restoration procedure documented | General proposed design exists; this report supplies a bounded local proposal below |
| Procedure owner-approved and usable for this recovery case | **NOT ESTABLISHED** |
| Actual full database restore tested | **NOT EVIDENCED**; retained receipt explicitly says no restore, and target history has no restore rows |
| Current owner-approved recovery point | **NOT ESTABLISHED** |

Historical backup/verification evidence is in [Step 149](216-step149-gate1-development-backup-verification.md) and `.tmp/step149/gate1-result.json`. Step 208's [recovery qualification](285-step208-de09-034-ui-and-recovery-confirmation.md) remains applicable. A successful VERIFYONLY is not a successful restore, a proven recovery time, an approved data-loss boundary or proof of a complete SQL/file recovery set.

### Proposed fresh VERIFYONLY — authorization requested, NOT EXECUTED

If the owner wants the historical candidate's **current readability/checksums** established before considering its use, authorize this exact verification-only command against the verified development SQL instance:

```sql
RESTORE VERIFYONLY
FROM DISK = N'C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_step149_20261022T225747921Z_7d074a52fb5f41e8a27959fa0fec86c6.bak'
WITH FILE = 1, CHECKSUM, STOP_ON_ERROR;
```

Requested authorization wording:

> I authorize only the exact checksum RESTORE VERIFYONLY command in Step 221 report 295 for the existing Step 149 file, backup set position 1, on the verified development instance. This does not authorize backup creation/overwrite, a database restore, migrations, grants, clinical changes or service operations.

No verification operation was run in this step. Reverification cannot make the October 22 backup current or recover later changes absent a usable subsequent recovery chain. If the owner instead selects a newer existing recovery set or separately authorizes preparation of a current one, verify that approved set under its own explicit scope; do not run this old-file verification merely to manufacture activation readiness.

## 5. Proposed recovery point and suitability

**A. Exact existing candidate:** full/copy-only backup **set 44, position 1**, at the path and SQL completion timestamp in section 3. This is the only evidenced existing candidate, **not an owner-approved selection**.

**B. Verification status:** historically checksum-verified; file existence currently confirmed; fresh integrity and actual restore capability unestablished.

**C. Intended recovery boundary:** this candidate predates **both 0098 and 0099**, and Step 149 states that its captured schema included 0089/0090. It therefore does **not** represent the immediate pre-0099 Stage 1 baseline or current post-0099 state. Restoring it by itself would omit later schema/data/audit changes. No later recorded differential/log chain is available in the inspected metadata to close that gap.

**D. Suitability recommendation:** **not recommended as the current activation recovery point on present evidence**. Do not overwrite the current tenant with it or presume loss of newer clinical/audit history is acceptable. Prefer an owner-identified newer complete recovery set; if none exists, obtain separate authorization to prepare and verify a current recovery point. A newly prepared point would protect the post-0099 state and future grant/acceptance operations, not establish an unrecorded pre-0099 rollback point.

**E. Owner decision:** the owner must identify/approve the actual recovery set and loss boundary, confirm how all newer clinical/audit history and coupled files will be preserved, approve a usable procedure and operator, and distinguish any verification/rehearsal permission from later execution/cutover permission. No approval is inferred from the historical backup authorization or fictional-data clarification.

## 6. Restoration procedure proposal and assumptions

Existing [Step 29B](41-step29b-backup-restore-readiness.md) documents backup/restore design with operating evidence missing. [Step 29C's tenant-only sequence](42-step29c-recovery-policy-design.md) is **PROPOSED — REQUIRES OPERATIONAL APPROVAL**, not an approved or rehearsed local runbook. No later owner-approved usable restoration procedure was established by the relevant reports or current request.

The following is a **proposed isolated recovery/rehearsal procedure only**, requiring separate authorization before any execution:

1. Identify the responsible operator, exact approved backup/recovery set and acceptable recovery/loss boundary. Confirm access to all backup media, keys if required, sufficient SQL/server storage and the paired external-file recovery material. Preserve the current tenant and its complete later history; do not assume the old point contains newer records.
2. Obtain explicit approval for a **new, isolated restore database name and noncolliding data/log file destinations** on the development instance. Confirm those targets do not exist and are not current/shared/production locations. Do not use REPLACE, overwrite current MDF/LDF files, or restore into MicroEMR_LocalDev_Fresh as an implicit rehearsal.
3. Under that authorization, freshly verify the chosen media and confirm its header/file layout before restoring. Existing backup metadata lists logical files **`MicroEMR_LocalDev_Fresh`** (data) and **`MicroEMR_LocalDev_Fresh_log`** (log); verify these against the actual chosen media, then map them with MOVE to the separately approved isolated destinations. No destination path/name or MOVE operation is approved by this report.
4. Restore the approved set to the isolated database with RECOVERY for a full-only point, or the explicitly verified ordered full/differential/log sequence if a usable chain is separately evidenced. Do not infer point-in-time protection from recovery model FULL alone. Do not automatically migrate the restored copy to 0099: doing so would change the recovery point being evaluated.
5. Perform separately authorized integrity and tenant/ledger checks on the isolated copy, verify audit/history/artifact preservation and reconcile any external files. Only bounded approved clinical reads may be used in the rehearsal. Record measured success/failure and limitations; no RTO is presently demonstrated.
6. If isolated application validation is needed, authorize a dedicated recovery-test configuration separately. Do not repoint the running tenant, change production/shared routing, or start Auth with migration/seeding as a validation shortcut. Current Auth/Platform remain untouched.
7. Return evidence for owner review. Rehearsal success does not authorize replacing/cutting over the current tenant. Any real recovery/cutover needs independent target, preservation, authorization and operational decisions; no physical deletion of newer clinical history is authorized.

Assumptions still requiring approval/evidence include backup-service access, unchanged/readable media, encryption/key availability if applicable, isolated target/file layout, operator restore permissions, sufficient disk, paired SQL/file consistency and an acceptable handling of all post-backup changes. The existing backup resides on the same SQL server/volume, so it alone does not protect against loss of that server/volume. SQL-resident prescription artifact JSON is covered by the tenant database point; separate patient files/file-backed artifacts need their corresponding recovery material.

**Procedure status: documented proposal, owner approval and actual rehearsal pending. No restore command was executed.**

## 7. Runtime principal and remaining permission state

The runtime connection reverified **`microemr_dev`**, database **MicroEMR_LocalDev_Fresh** and the same expected tenant UID. Operator metadata confirmed the three procedures now exist. Current effective runtime checks returned:

| Procedure | Effective EXECUTE |
|---|---|
| `[dbo].[DispensingLocation_Get]` | **0** |
| `[dbo].[DispensingLocation_Search]` | **0** |
| `[dbo].[DispensingLocation_Save]` | **0** |

No directory-object permission rows were returned by operator inspection. Step 220's three narrow object grants remain a future separately authorized action; they were not executed here. No broad permission or role change is indicated. Do not attribute migration completion to completion of these grants or to end-to-end activation acceptance.

## 8. Owner decisions and exact future authorization

The immediate recovery decision is **CONDITIONAL / NOT READY for further activation writes**. There is no supported current owner-approved usable recovery point/procedure in the reviewed evidence.

Required decisions, in order:

1. **Acknowledge actual state:** 0099 is already applied. Do not authorize/apply it again; determine any intervening execution provenance separately if needed.
2. **Select the real recovery point:** identify a suitable newer existing complete recovery set, or explicitly authorize preparation of a current backup in a new approved destination under a separate task. The only observed existing set 44 is historical and is not recommended as the current point. No new backup filename, creation or overwrite is approved here.
3. **Authorize verification only when needed:** for the historical candidate, the exact requested wording/command is in section 4. A chosen different/new recovery set needs its own verification scope. VERIFYONLY approval does not approve a restore or grant execution.
4. **Approve a usable procedure:** identify the operator, approved isolated targets/file paths and restoration/validation steps; explicitly record whether a full restore remains untested. Any rehearsal, actual restore or cutover needs separate execution approval.
5. **Only after recovery is accepted, authorize remaining activation actions.** Suggested exact wording for the eventual grant step, not granted by this task:

> I approve recovery set [exact identifier/path/checkpoint] and procedure [approved revision/operator/isolated targets], with the recorded preservation and recovery limitations. After revalidating that recovery evidence, the development server/database/tenant and principal, I authorize only the three object-level EXECUTE grants in report 294 to microemr_dev on dbo.DispensingLocation_Get, dbo.DispensingLocation_Search and dbo.DispensingLocation_Save, plus nonmutating verification. Migration 0099 is already applied and must not be rerun. No backup creation/overwrite, database restore, clinical/fixture writes or Web/API/Auth restart is included; any such action requires separate approval.

Bracketed prerequisites must be supplied; this is not current approval. Existing compatible Web/API may be reused as described in Step 220. Auth restart remains unnecessary and excluded. Minimum authenticated acceptance and any fixture/clinical save retain their separately bounded authorization requirements.

## 9. Recommended Step 222 and status retained

**Step 222 should resolve the actual recovery-point/procedure decision**, using a newer existing set if the owner can identify one, or a separately authorized current-backup preparation/verification task. If the owner wants an isolated restore rehearsal, scope and authorize it explicitly; do not silently run VERIFYONLY, create media, restore or cut over. Recheck the already-applied-0099 state and authorize only still-pending grants after the recovery gate is met.

No automated evidence was rerun and no certification reassessment was repeated. Preserve **DE09.032 PARTIAL; PC07.01 PARTIAL; inventory 7 IDs / 3 packages**, DE09.034/DE09.035 prior qualified closures, authenticated UI qualifications, earlier NU1900 qualification and Auth startup restrictions. Recovery remains **CONDITIONAL**, not successful restoration or activation completion.
