# Step 42B: tenant migration 0059 hash drift investigation

## Scope and result

Investigated on 2026-09-16, branch `feature/ontariomd_certification_step42b_tenant_0059_hash_drift_investigation`, from clean local main `96caef95319eef0c598291e231b0cb5fb0eb40b4`. All SQL access in this run was read-only against the controlled `local-dev-fresh` tenant. No migration, ledger, product code, permissions, or database object was changed. Step 41 A–L was not run.

**Result: BYTE-ONLY / LINE-ENDING DRIFT.** The applied 0059 ledger hash exactly matches the current SQL text with **CRLF** line endings. The only reachable committed version of 0059 and the current repository file use **LF**. The two live procedures have the same SQL bodies as current source after accounting for SQL Server's stored `CREATE OR ALTER` wording. This is an immutable-byte governance mismatch even though no SQL semantic difference was found.

The earlier SQL error 20 was an artifact of the restricted tool environment: the same DatabaseTool reads succeeded with normal host network access, and operator-observed MicroEMR SQL sessions had `encrypt_option = TRUE`. This investigation used normal host access for read-only SQL. No SQL host or connection setting was changed.

## Current source and hash algorithm

| Property | Finding |
| --- | --- |
| File | `db/tenant-clinical/migrations/0059-medication-discontinuation-concurrency.sql` |
| Git blob SHA-1 | `6880a3f99ae4f4deaeadc72f5a8463563ee05c3c` |
| Size and lines | 4,170 bytes; 96 lines |
| Encoding and endings | UTF-8 compatible ASCII content, no UTF-8 BOM, 96 LF endings, no CRLF, final LF |
| Current runner SHA-256 | `6814E23381495844343B86EA9E8FF73AFF0BEC900CCAB81CBCCBF5F35EF8EE05` |
| Manifest entry | `migrationId` = `0059-medication-discontinuation-concurrency`, `schemaVersion` = `1.0.0`, `script` = `tenant-clinical/migrations/0059-medication-discontinuation-concurrency.sql`. The manifest stores **no hash**. |

`FileTenantDatabaseMigrationSource.GetAvailableMigrationsAsync` reads the file with `File.ReadAllTextAsync`, then computes uppercase hexadecimal `SHA256.HashData(Encoding.UTF8.GetBytes(script))`. It hashes **decoded text re-encoded as UTF-8**, not the original raw file bytes. CRLF and LF remain different in the decoded text; the final newline is significant. UTF-8 BOM is consumed by text reading, so this hash **cannot establish whether the applied file had a BOM**. The runner inserts the hash alongside the script in the same migration transaction. `dbo.SchemaMigration` has MigrationId, SchemaVersion, ScriptHash, AppliedAt, and AppliedBy; it has no filename or original-script bytes.

Independent recomputation with that algorithm:

| Reconstructed text | Size before text decoding | Runner SHA-256 | Matches applied ledger? |
| --- | ---: | --- | --- |
| Current/introduced LF with final newline | 4,170 bytes | `6814E23381495844343B86EA9E8FF73AFF0BEC900CCAB81CBCCBF5F35EF8EE05` | No |
| Same text, 96 CRLF endings | 4,266 bytes | `2C05150F9DE76BEC0CF2A02C83B92F6355742F43C5D6CA3753907DB0FD031980` | **Yes, exact** |
| LF without final newline | 4,169 bytes | `B329CDA2A1BD80509B3C615B212AF1AD4B0C092CC8F07B2B16126242F5B9D007` | No |
| CRLF without final newline | 4,264 bytes | `3B836406B1CADB26D0E9B48FD4A736138452E3E79160EE9A6195573570F5AEA1` | No |

Adding a UTF-8 BOM to either form does not change the runner hash after text decoding. No SQL comment, whitespace within lines, or logic change is needed to explain the recorded value.

## Applied ledger and Git chronology

Read-only direct ledger query from `MicroEMR_LocalDev_Fresh` returned exactly one 0059 row:

| Ledger field | Value |
| --- | --- |
| MigrationId | `0059-medication-discontinuation-concurrency` |
| SchemaVersion | `1.0.0` |
| ScriptHash | `2C05150F9DE76BEC0CF2A02C83B92F6355742F43C5D6CA3753907DB0FD031980` |
| AppliedAt (UTC) | `2026-09-16T15:13:36.7522782Z` |
| AppliedBy | `LAPTOP_DELLAL` |
| Filename metadata | No such ledger column |

| Event | Time (UTC) | Evidence |
| --- | --- | --- |
| 0059 applied to controlled tenant | 2026-09-16 15:13:36.752 | Ledger row; both affected procedure `modify_date` values fall within the same second. |
| First reachable Git commit introducing 0059 | 2026-09-16 16:25:17 | `106018055b62c6b5de66fb71b784621d7ffb31c4`, subject “updated medication discontinuation concurency”; committed file is identical to current LF bytes/blob. |
| Step 38 branch merge | 2026-09-16 16:26:06 | `90eab7f052a0eeb813bcbcf766693b4a7f9ff08b`; the 0059 blob is unchanged. |
| Current local main base | 2026-09-16 20:57:18 | `96caef95319eef0c598291e231b0cb5fb0eb40b4`; 0059 still has the same blob. |

`git log --all --follow` shows **one reachable commit introducing the file and no later commit changing it**. Thus no reachable committed 0059 version has the applied hash. The database application predates the first reachable commit by about 72 minutes. The applied CRLF text can be reconstructed exactly from the committed LF text, but the pre-commit working file itself was not retained as an artifact. The tool/editor that supplied CRLF before application is **not proven**.

The repository had `db/tenant-clinical/migrations/*.sql text eol=lf` in `.gitattributes` at the introduction commit; current `git ls-files --eol` reports `i/lf w/lf`. Global Git `core.autocrlf=true` is configured, but the path-specific `eol=lf` rule controls this migration. Git therefore records LF for the migration even if the pre-commit working file presented CRLF to DatabaseTool. The merge did not introduce a later text change.

## Live database object comparison

Read-only `sys.sql_modules`/object metadata inspection found:

| Procedure affected by 0059 | Live `modify_date` UTC | Comparison to current 0059 |
| --- | --- | --- |
| `dbo.PatientMedication_GetByPatientUid` | `2026-09-16T15:13:36.687Z` | Body equal after line-ending normalization and SQL Server's stored `CREATE OR ALTER` to `CREATE` wording; live definition retains the Step 38 leading comment. |
| `dbo.PatientMedication_Discontinue` | `2026-09-16T15:13:36.720Z` | Body equal after the same normalization/rewrite allowance. |

Both live definitions retain CRLF endings. Their modification times align with the ledger application and no later object modification is evidenced. The live stale-RowVersion guard, conditional update, and audit SQL match the current source text. This is **no observed semantic SQL drift**; it is not a claim that medication concurrency behavior has passed the two-session Step 41 test.

## Governance decision and remediation

The ledger hash is credible evidence of the **CRLF text applied**. The current Git source is semantically equivalent but **not authoritative as the exact applied immutable text**. The mismatch is not a runner defect: the documented algorithm correctly distinguishes line endings. The repository's LF policy and application of a CRLF pre-commit file created the mismatch.

**Recommend one path: Case B, governed immutable-text restoration.** In a separate, approved repair, preserve the current and applied evidence, then restore 0059 in the repository to the exact **CRLF text** that matches the ledger. Because `.gitattributes` currently enforces LF for every tenant migration, that repair must first approve a narrowly scoped exception for 0059 that preserves its CRLF line endings in Git and on checkout. Use a declared BOM policy (prefer the repository's current no-BOM form); the original pre-commit BOM state cannot be recovered from the ledger hash. Verify its runner hash equals the applied value on clean checkouts and re-run read-only status before any further tenant work. If repository policy will not allow that exception, stop for a governance decision; do not silently weaken the hash check.

**Do not change the ledger hash or replay 0059.** No migration 0060 is needed for this line-ending difference. A 0060 would be considered only for a separately identified intended SQL behavior change after 0059 identity is settled; it cannot fix historical hash integrity. This branch performs no restoration.

## Verification and Step 41 status

| Gate | Result |
| --- | --- |
| Fresh Release solution build | PASS: 0 warnings, 0 errors |
| Full API suite, fresh Release binaries | PASS: 831/831, 0 skipped, using established permitted Chromium access |
| Full Auth suite, fresh Release binaries | PASS: 30/30, 0 skipped |
| Read-only DatabaseTool status with normal host access | Identity valid; 60 manifest / 60 applied; latest 0059; no missing or unexpected IDs; **0059 hash mismatch; Current: NO** |
| Direct ledger/procedure reads | PASS, read-only; findings above |
| `git diff --check` and new-file whitespace | PASS: no reported diff whitespace errors; the untracked new document was separately checked for trailing whitespace |

Step 41 remains **blocked** until an approved 0059 immutable-text restoration is reviewed and the controlled tenant reports `Current: YES` without hash mismatch. No clinical runtime protocol was resumed.
