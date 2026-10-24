# Step 163 — DE08.008 development deployment readiness

2026-10-23, updated after owner follow-up. **0091 is now registered in the repository manifest; the owner reports DatabaseTool output `Latest applied: 0091-immunization-dose`.** The original missing-registration/unapplied blocker below is superseded. Running API/Web artifact identity and dose-specific post-deployment reads remain unverified. The agent performed no deployment, migration, backup/restore, clinical login/write, service restart or test-suite execution during this assessment or documentation update.

Retain [Step 162](229-step162-de08-008-immunization-dose-implementation.md): **49 unique automated cases PASS / 0 FAIL**, including **two real Chromium integration cases** and isolated security/audit/concurrency evidence. Those browser cases are included in 49, not additional cases. **DE08.008 IMPLEMENTED — VERIFIED in isolated fixtures; migration application owner-reported; remaining deployment verification pending. PC07.01 PARTIAL; 10 mandatory-gap IDs / three packages.**

## Owner follow-up — current evidence

- The owner supplied the exact DatabaseTool line **`Latest applied: 0091-immunization-dose`**, following the previously supplied `tenant migration-status --tenant-key local-dev-fresh` command. Record migration application as **owner-reported**, not a fresh agent SQL verification.
- Repository inspection during this update confirms exactly one `0091-immunization-dose` entry in `db/tenant-clinical/manifest.json`, pointing to `tenant-clinical/migrations/0091-immunization-dose.sql`; the manifest now contains 92 entries. The owner's manifest edit is preserved, not rewritten by this update.
- The latest full status output was not supplied: do not infer `Current: YES`, `Missing: none`, no hash mismatches or an exact new applied count from the single line. No new SQL/status command was executed here.
- The owner also reported the application runs stably. This supports general owner-observed stability, but does not identify the active API/Web artifacts or prove legacy-dose read/render behavior.
- Do not reapply 0091 or repeat the 49 passing cases. Remaining work is the bounded post-deployment evidence in section 6. No new backup, recovery-point acceptance, restore rehearsal or deployment procedure is retrospectively inferred.

Sections 1–4 retain the **original pre-application checkpoint**. Section 5 retains the original deployment/rollback plan as reference; it is not an instruction to repeat the now owner-reported migration.

## 1. Checkpoint and confirmed development target

Branch retained: `feature/step-162-de08-008-immunization-dose`; HEAD `c5f5fe1660d7110074cd188d9d38cf9a89ea6ace`. Recorded status once; all Step 162 modifications/new files and untracked reports 224–229 preserved. No Git mutation. Only this report added outside ignored local read-only helpers/evidence.

Live read-only identity/schema checks at **2026-10-23T21:04:32.2551688Z**:

| Property | Confirmed value |
|---|---|
| SQL endpoint | `192.168.50.1,50013` |
| Actual SQL instance | `EC2AMAZ-TC26CQK\TELEMED52` |
| Database | `MicroEMR_LocalDev_Fresh` |
| Tenant key / UID | `local-dev-fresh` / `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Platform assignment | `local-sql`; tenant/database both Active; assigned database agrees with live identity |
| SQL version / compatibility | `15.0.2000.5` / `150` |

The fixed existing DatabaseTool connection was used only in memory; no connection secrets were printed. Evidence: `.tmp/step163/readiness.json`, produced by `readiness.ps1`; procedure follow-up: `procedure-comparison.json` / `compare-procedures.ps1`. These helpers issue only SELECTs. Initial sandbox TCP denial occurred before querying; authorized read-only execution outside the sandbox succeeded. No application authentication performed.

## 2. Original migration status and compatibility — before owner follow-up

- Ledger: **91 entries**, latest **`0090-prescription-paper-interval`**, schema version `1.0.0`. No `0091-immunization-dose` ledger entry; new dose columns/reference table are absent.
- Manifest: **91 entries, zero registered pending migrations**, but **0091 is not registered**. Thus “zero pending” against the current manifest does **not** mean the dose deployment is current. `FileTenantDatabaseMigrationSource` loads manifest entries, not every `.sql` file in the folder.
- This is a Step 162 deployment-packaging omission, not a new clinical policy question or invalidation of its direct-script fixture results. **Do not run provisioning expecting it to discover 0091.** No manifest change made in this read-only task.
- Entry originally required, **now present** in the repository manifest:

```json
{"migrationId":"0091-immunization-dose","schemaVersion":"1.0.0","script":"tenant-clinical/migrations/0091-immunization-dose.sql"}
```

Canonical SHA-256 of 0091 recorded at the original checkpoint, using existing BOM/line-ending normalization:
`B19C19624DF81FBAD7BA3C8CB458BF9AC22F4AA4ACEB75BF551B7FD8A078033C`.
Use migration ID/hash, not the shared `1.0.0` version alone, to attest deployment.

Compatibility findings:

- Required PatientImmunization, Patient, ApplicationUser, PatientEncounter, TenantDatabaseIdentity, AuditLog, ImmunizationTypeReference and HistoryPartialDateValid objects exist. Existing immunization constraints are enabled/trusted. AdministrationDate, SourceType and Refused retain the nullable legacy schema; RowVersion remains eight-byte timestamp.
- Applied 0080 hash matches its repository source: `65F398C0F22BDFB0C20D77BEB090B6A0F03EA61C80A5915B84A8A6DE1EF4FB21`.
- All **five** existing immunization procedure definitions match 0080 after normalizing only line endings/outer whitespace and SQL Server's persisted `CREATE   PROCEDURE` versus source `CREATE OR ALTER PROCEDURE` header. Initial literal comparisons differed for that header; follow-up confirms no body difference.
- 0091 adds nullable `DECIMAL(7,3)`/canonical unit columns, reference rows and constraints, and replaces the four read/create/update definitions. MarkEnteredInError remains intact. No old migration rewrite or backfill; SQL 2019/compatibility 150 supports the used JSON and DDL constructs. Operator metadata reports ALTER dbo / CREATE TABLE / CREATE PROCEDURE permission; actual deployment is not attempted.
- Existing Step 162 isolated upgrade evidence is reused. Current structural compatibility does not itself prove live deployed application behavior.

## 3. Backup availability and recovery qualification

[Step 149](216-step149-gate1-development-backup-verification.md) records the existing full COPY_ONLY/CHECKSUM backup, backup-set ID **44**, successful checksum `RESTORE VERIFYONLY`, and no damage. Live metadata still finds that backup and its SQL-server-side file:

```text
C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_step149_20261022T225747921Z_7d074a52fb5f41e8a27959fa0fec86c6.bak
```

Recorded completion: **2026-10-22T22:57:48.5751311Z**. It is the latest backup returned by current database backup history. No new backup, VERIFYONLY, HEADERONLY or restore was executed here; file existence does not revalidate its current byte integrity.

**Availability confirmed; complete restore readiness remains qualified.** No restore rehearsal evidence is supplied, and this destination has no returned restore-history entries. Restore permissions, recovery time, off-server durability and a point-in-time log chain remain unproven. The backup predates Step 155 clinical changes; restoring it would lose subsequent changes. Prefer a separately authorized fresh pre-deployment COPY_ONLY/CHECKSUM backup and VERIFYONLY rather than silently accepting that loss. A fresh verified backup still would not constitute a restore rehearsal. No recovery claim or backup policy is invented.

## 4. Legacy compatibility

Live aggregate inspection finds **four immunizations across two patients**, all `Completed`, all `Refused = NULL`. Do not convert those unknown indicators to false or refusal. No record content or values changed.

All four require legacy NULL dose compatibility: both added columns remain NULL without clinical UPDATE/backfill. Existing NULL pairing passes the proposed constraints. Existing RowVersion/audit must remain unchanged by schema-only upgrade. Reuse Step 162's proven legacy preservation, refusal/NULL distinction and omission-safe update behavior.

Pre-deployment UID/RowVersion-set SHA-256 baseline (no identifiers/content exposed):
`CFFA4613A25835E796FA99344602F4D236D452FC689A45C0A032FA37BE6EB46E`.
Rebaseline immediately before an authorized deployment if unrelated normal work has changed records; a later difference is not automatically migration-caused.

## 5. Original bounded deployment and rollback plan — retained reference

1. **Resolve packaging first:** after code-change authorization, create a dedicated local feature branch, add only the 0091 manifest entry, and perform a focused read-only migration-source discovery/hash check. Confirm the packaged DatabaseTool assets contain both the entry and script. Do not rerun the 49 passing cases.
2. Reconfirm this exact active tenant/database, ledger and unchanged script hash immediately before execution; require **only 0091 pending**. Capture current aggregate record/RowVersion/audit baseline and the active API/Web artifact paths/hashes. Preserve the complete current runtime bundles/launch configuration as the application fallback; Step 148's historical runtime backup directory exists but is not by itself proof of a complete current rollback bundle.
3. Agree the recovery point. Prefer the separately approved fresh backup/verification above. Prevent concurrent clinical changes during the deployment window; do not change permissions to achieve this.
4. Use the existing tenant provisioning/migration runner for **`local-dev-fresh` only**, with the registered assets. It acquires the existing application lock and applies each pending migration plus its SchemaMigration entry in one transaction. It also maintains existing provisioning metadata/platform status; those normal administrative writes must be included in authorization. Abort on any unexpected pending migration, drift or target mismatch; no raw script/handwritten ledger bypass.
5. Verify committed 0091 ID/hash, columns, trusted constraints, four codes and procedure signatures before activating new code. Existing `schemaVersion=1.0.0` alone is insufficient. **Database first:** Step 162's repository read mapping requires the new columns.
6. Refresh only the identified local API/Web using the matching tested Step 162 artifacts and emitted UI assets; preserve existing configuration/content roots. Those API/Web DLLs exist under `.tmp/step162/build/bin/<project>/debug`. Use a complete matching artifact bundle, not only two DLLs. Do not overwrite an active artifact directory or restart Auth/SQL/unrelated services. Do not imply the normal Debug output contains the verified release.
7. Execute only the small post-deployment evidence set below, record hashes/identity/results and stop.

Rollback boundaries:

- Failure inside 0091: the existing runner rolls back that migration's transaction, including its ledger insert. Inspect state before any retry; do not activate the new application. Provisioning failure status may still be recorded outside that transaction.
- Failure after SQL commit/application activation: prefer reverting API/Web to the captured prior runtime bundle **while retaining additive schema and dose data**. Optional `@Dose` preserves values for old callers; old readers use their existing columns. This compatibility is supported by existing contracts, but an actual previous-bundle rollback rehearsal is not claimed here.
- No destructive down migration, column drop, reference deletion or ledger deletion. A database restore is a separate exceptional recovery action requiring explicit target/recovery-point/data-loss approval; never automatically restore the old Step 149 backup over newer clinical/audit history.

## 6. Smallest post-deployment evidence and authorization

Automated, read-only first:

1. Verify target identity, exactly one 0091 ledger entry with the pinned hash, nullable exact types, four canonical codes, enabled/trusted constraints and dose-aware read/create/update signatures.
2. Compare the immediate pre/post four-row baseline, UID/RowVersion fingerprint and existing mutation-audit baseline; assert both dose columns NULL on legacy rows. Use current counts if unrelated work occurred before the deployment window.
3. Call existing list/detail stored procedures read-only for the scoped existing records, confirming legacy results include NULL dose and remain readable. Correlate matching API/Web artifact hashes, normal health and emitted asset availability without clinical login.
4. For actual development Web/API tenant routing and rendering, obtain **one separately authorized read-only normal login/session**. Open existing immunization history and editor without saving; confirm blank dose, controlled units and normal read/reload. Read auditing may occur under existing policy. No need to repeat create/edit/clear/security/concurrency writes already proven in isolated fixtures.

Metadata/legacy-read checks cannot prove a new non-NULL deployed write. That boundary must remain explicit: if requirement-level closure specifically requires live dose save/reload, request a separate bounded synthetic-record write authorization then, rather than doing so under this readiness task. No repeated owner visual checklist or clinician sign-off is required by this report. Future clinician review remains separate.

**Remaining authorization boundary after owner follow-up:** the manifest entry is present and migration application is owner-reported; do not request repeating either action. Any agent-performed API/Web deployment/refresh still requires explicit authorization if active-artifact inspection shows it is needed. One normal read-only clinical login/session requires separate authorization if the authenticated check is selected. Any new backup/VERIFYONLY or restore likewise requires its own authorization; this update does not infer that a fresh backup or recovery-point acceptance occurred. No clinical create/edit/clear, production access, Auth/SQL restart, commit or push is included. Documentation update only; no new operational authorization assumed.

## 7. Disposition

**Manifest registration: confirmed present. Migration application: owner-reported APPLIED.** The previous missing-registration/unapplied HOLD is superseded. Development deployment verification remains pending for the full ledger/hash status, active matching API/Web artifacts and legacy-dose read/render checks. Recovery qualifications remain as recorded; no new backup or restore evidence supplied. The next step is the smallest read-only deployment verification, not another migration or implementation-test cycle.

**DE08.008 IMPLEMENTED — VERIFIED in isolated fixtures; migration application owner-reported; remaining deployment verification pending. PC07.01 PARTIAL. Inventory unchanged: prescribing 5 + CPP 4 + scheduling 1 = 10 mandatory-gap IDs / three packages.** Retain all prior owner/mobile/integration passes and separate deferred deployment security/concurrency/Family qualifications. No full requirement-level closure or inventory reduction established by the owner's migration line. Only this report edited by the agent; no code, manifest or database changes, test reruns or operational actions during the update. Stop after documentation.
