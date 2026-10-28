# Step 217 — DE09.032 Stage 2 directory/lookup implementation preflight

2026-10-28 (America/Toronto). **BLOCKED at the owner's mandatory identity-collation stop condition. No Stage 2 application, SQL, migration or test-code changes were made.** Stage 1 remains COMPLETE under its existing qualifications. DE09.032 and PC07.01 remain PARTIAL; inventory remains 7 mandatory IDs / 3 packages.

## Branch and worktree preservation

Read AGENTS.md and the supplied Step 217 request first. Actual starting branch was **main**, with a clean working tree and no modified/untracked files. The requested branch did not exist. Created and verified **`feature/step-217-de09-032-directory-lookup`** before any implementation. Initial sandboxed creation was denied by filesystem permissions; the authorized elevated local retry succeeded. Existing work was preserved; the committed Stage 1 files and earlier reports were not changed. This report did not exist before creation.

No commit, push, pull, fetch, merge, reset, clean, stash or remote Git operation occurred. Only this report was added. No directory schema, application code, permissions, tests or migration was introduced.

## Mandatory stop condition and evidence

The supplied Step 217 instructions explicitly require:

> If the current collation would incorrectly collapse distinct case-sensitive identifiers, STOP and report the required uniqueness design decision rather than silently changing comparison semantics.

The current execution host is `ALIENWARE_AHMED`. Read-only SQL metadata on its existing `(localdb)\MSSQLLocalDB` instance returned:

| Check | Actual result |
|---|---|
| ServerName | `Alienware_Ahmed\LOCALDB#8AC5E118` |
| Server collation | `SQL_Latin1_General_CP1_CI_AS` |
| Master database collation | `SQL_Latin1_General_CP1_CI_AS` |
| `CASE WHEN N'Ab-01'=N'ab-01' THEN 1 ELSE 0 END` | **1 — distinct case variants compare equal** |
| `sys.databases` entry for `MicroEMR_LocalDev_Fresh` on this instance | None returned |

The existing prescription fixture (`PrescriptionRefillSqlDatabase`) creates disposable databases using `CREATE DATABASE [random-name]` without an explicit collation. Stage 1 identity storage declares `nvarchar(200)` without a column-specific collation. Therefore using these existing defaults for a new directory unique pair would collapse case-distinct identifiers. Stage 1's binary comparisons for detecting clinical changes do not establish an approved directory uniqueness policy or change a unique index's column comparison semantics.

The historical development server recorded in retained Step 211 evidence (`EC2AMAZ-TC26CQK\TELEMED52`) could not be reached by the metadata connection attempts; no connection or data access succeeded there. This report does **not** claim to verify the current collation or migration state of that persistent development database. The owner's confirmation that 0098 is applied and the application is stable is retained as owner-reported evidence, without repeating activation checks.

The sandboxed LocalDB attempt failed on instance registry access; the elevated SELECT-only attempt succeeded. The existing LocalDB instance was stopped before that successful metadata connection and automatically started on connection. No explicit service start/restart command or application restart occurred. Queries read server/database metadata and evaluated constant strings; no patient records, application records, schema or database contents were changed. No migration was applied and no test database was created.

## Required uniqueness design decision

Owner approval is needed for the exact comparison policy of the new directory's **system and identifier**, including unique indexing and exact resolution/selection matching.

Recommended decision for review: use explicit column-scoped **`Latin1_General_100_BIN2`** comparison for both new identity components, with the existing approved boundary trim and no case folding or Unicode normalization. Enforce uniqueness of that pair across active and inactive directory records. Use the same exact comparison in resolution and selected-pair matching. Preserve the existing database collation and existing prescription columns; define any comparison to Stage 1 storage explicitly. Keep facility-name/address search behavior separate from identity matching.

This is a proposal, **not an implemented or approved comparison change**. The owner must confirm that case-distinct and canonically different Unicode sequences represent distinct directory keys for both components. Do not assume a namespace-specific case rule, silently apply a binary collation, or weaken uniqueness to bypass this stop condition.

## Implementation dispositions

| Requested area | Status at stop |
|---|---|
| Directory model | Not implemented; proposed tenant-local record, immutable pair, supplied details, active state, metadata and RowVersion remain pending |
| Identity uniqueness | **BLOCKED** pending explicit comparison decision; no index added |
| Validation | Stage 1 pair/name/address validators remain available and unchanged; directory and phone validation not implemented |
| Permissions and tenant isolation | Existing controls unchanged; Stage 2 maintenance/lookup permission mapping and endpoint enforcement not implemented or verified |
| Maintenance lifecycle | Create/detail edit/deactivate/reactivate not implemented; no physical deletion or identity reassignment performed |
| Search/detail retrieval | Not implemented; bounded active-default search and authorized inactive visibility pending |
| Prescription selection | Not implemented; server-side tenant/active/pair/RowVersion resolution pending; manual Stage 1 reference entry unchanged |
| Name/address confirmation | Stage 1 independence preserved; Stage 2 optional population, mismatch warning and explicit confirmation pending |
| Audit and RowVersion | Existing prescription mechanisms unchanged; directory mutation auditing and selection audit metadata pending |
| Historical preservation | No prescription, correction, artifact, PDF or stored facility value changed; Stage 2 historical-resolution behavior not implemented |
| UI | Existing Stage 1 UI unchanged; lookup and tenant-administration maintenance UI not implemented |
| Output boundaries | PDF generation, list Actions, external transmission and pharmacy integration untouched |
| Migration | Actual latest file/manifest is **0098**. No next migration created or number reserved; recheck sequence on resume |

## Acceptance mapping and execution results

Relevant Step 213 feasibility and Step 215 implementation evidence was reused. The current request retains Stage 1's **50 focused backend/view cases, 8 focused frontend cases, 445/445 backend regression, 56/56 frontend regression, and build/TypeScript PASS**. These are prior evidence, not executions in Step 217. No Stage 1 tests were duplicated or rerun.

| New acceptance requirements | Reusable evidence / remaining assertions |
|---|---|
| Backend 1–4: create, duplicate/inactive uniqueness, case/Unicode identity | Stage 1 pair preservation is reusable; governed directory uniqueness requires the blocked comparison decision and new database assertions |
| Backend 5–7: pair/name/address/phone validation | Reuse existing pair and facility validators; add directory-required fields and existing phone-convention assertions after implementation |
| Backend 8–14: lifecycle, immutable identity, concurrency, permissions, isolation | Reuse tenant/audit/security fixtures; add directory-specific behavior and rejection assertions |
| Backend 15–20: bounded search/filtering/detail/audit/no mutation | Add directory-specific assertions; existing clinical pair tests do not prove directory behavior |
| Backend 21–28: save-time resolution and confirmed independent fields | Reuse prescription save/audit infrastructure; add tenant/active/pair/stale-selection and explicit-population assertions |
| Backend 29–32: corrections, finalized/history preservation, PDF/Actions | Reuse existing preservation assertions; extend only where the new selection/resolution implementation affects them |
| Frontend 1–11: lookup, selection, preview, confirmation/mismatch/cancel/manual mode, stale results, permissions, layout/accessibility | Reuse production Razor/browser and TypeScript harnesses; all new lookup/maintenance assertions pending |
| Frontend 12: existing Create/Edit/Detail compatibility | Retain Stage 1 evidence and rerun affected coverage only after Stage 2 implementation |

Focused backend/frontend tests executed: **0 / 0**. Regression passes executed: **0**. Builds and TypeScript validations executed: **0**. These are **NOT RUN**, not failures or passes. No tests were added because implementation stopped at the expressly required preflight condition. All new Stage 2 acceptance scenarios remain unverified.

## Qualifications and remaining gaps

Retain **DE09.034 and DE09.035 — SATISFIED — VERIFIED WITH QUALIFICATIONS**. **DE09.032 — PARTIAL; PC07.01 — PARTIAL**. Mandatory-gap inventory remains **7 IDs / 3 packages**. No certification closure or inventory reduction is recommended.

Retain recovery **CONDITIONAL**, authenticated live UI qualifications, earlier six NU1900 warnings, Auth startup migration/seeding restrictions, and the absence of external authoritative directory validation. No authenticated UI/manual clinical save was performed. No persistent migration was applied, and no shared or production database was accessed or modified.

Remaining Stage 2 gaps include the complete directory model, governed identity comparison, maintenance, permissions, bounded search/detail retrieval, save-time selection validation, explicit optional name/address confirmation, audit/concurrency integration, historical display distinctions and all corresponding automated acceptance. Manual-only checks remain authenticated UI observation and owner/clinician evaluation of directory curation and workflow; these cannot substitute for the pending automated implementation tests.

## Recommended Step 218

Approve or specify the exact system/identifier comparison and uniqueness policy described above. Once that decision is explicit, resume the authorized Stage 2 implementation after a fresh branch/worktree and migration-sequence check. Keep migration application limited to disposable automated-test databases and preserve all Stage 1 and historical behavior. No implementation should proceed by silently choosing comparison semantics.
