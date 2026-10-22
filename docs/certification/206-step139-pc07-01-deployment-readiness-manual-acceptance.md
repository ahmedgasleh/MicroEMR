# Step 139 — PC07.01 deployment readiness and consolidated manual acceptance

> Subsequent acceptance update: [Step 140 automated evidence reconciliation and three-item owner review](207-step140-pc07-01-automated-acceptance-reconciliation.md) replaces the need to replay section 6. The owner has reported all four Drug Description checks PASS and migrations 0086–0088 run; those reports and their qualifications are recorded there. The original readiness findings below remain the historical Step 139 checkpoint.

2026-10-22. **Migration/schema readiness: PASS for the identified development target. Deployment execution: BLOCKED pending explicit owner authorization, verified backup/recovery and coordinated API/Web rollout.** No migration, backup, restart or clinical change was executed. **PC07.01: PARTIAL. Inventory before/after: 10 mandatory-gap IDs / three packages.**

## 1. Git checkpoint and evidence boundary

Read AGENTS.md and only the relevant implementation/migration/testing/manual-acceptance sections of [Step 138](205-step138-pc07-01-coordinated-implementation-focused-regression.md). Reused its evidence; no new gap assessment or deferred-feature implementation.

Branch `main`; HEAD `b7fa5d7c12bcecc76a04f44ab40012495238218d`. Branch/HEAD/working-tree status recorded once. Starting tree contained the Step 138 modified manifest, Application/Infrastructure/API/Web/frontend/test files, plus untracked migrations 0086–0088, reports 199–205, new plan/text DTO helpers and prescription/Step 138 tests. Step 138 section 10 identifies its exact changed files; the earlier Product/Brand Name line and rendering test also remain present. All existing changes preserved. No reset, clean, branch switch, staging, commit, merge or remote Git operation.

Only this certification report is added as a task deliverable. Ignored `.tmp/step139` holds read-only helper scripts, sanitized metadata and command output. No source/configuration edits, clinical requests, permission changes, builds, test execution or service restarts. Secret values were read only to establish existing connections and were not printed or copied into this report.

## 2. Exact development target — live identity verified

The historical selected TenantUid was a candidate, not proof of the target. Read-only platform assignment and actual tenant database identity independently agreed:

| Property | Verified value |
|---|---|
| Tenant key | `local-dev-fresh` |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Display name | Local Development Clinic Fresh |
| SQL endpoint | `192.168.50.1,50013` |
| Database-server key | `local-sql` |
| Clinical database | `MicroEMR_LocalDev_Fresh` |
| Platform assignment database | `MicroEMR_Platform` — only this tenant's assignment was inspected |
| Tenant / database status | Active / Active |
| Actual server machine / SQL version | `EC2AMAZ-TC26CQK` / `15.0.2000.5` |
| Compatibility / recovery / state | 150 / FULL / ONLINE |
| Identity evidence | Exactly one TenantDatabaseIdentity row with matching DB_NAME(), TenantUid and TenantKey |

Existing API runtime and operator secret bindings agree on endpoint/database. No production or other-tenant deployment was attempted. The development role is supported by the known selected development tenant plus live matching identities, not inferred from a connection-string filename. Readiness metadata timestamp: **2026-10-22 16:40 UTC (12:40 EDT)**; later checks remain separately recorded in the local logs.

Evidence: `.tmp/step139/readiness.json`, `server-facts.json`, `migration-status.log`. Read-only compatibility queries returned aggregate counts only; no patient/prescription/allergy content or new clinical fixture was retrieved.

## 3. Migration readiness findings

The **existing DatabaseTool read-only command** was executed, not provision:

```powershell
dotnet .tmp/step138/build/bin/MicroEMR.DatabaseTool/debug/MicroEMR.DatabaseTool.dll tenant migration-status --tenant-key local-dev-fresh
```

Exit **3** is the expected **not current/pending** result, not a migration failure. It reported valid identity, **89 manifest migrations / 86 applied**, latest **0085-private-prescription-notes**, exactly the following three pending entries, no unexpected applied entries, no hash mismatches and no last migration failure. Existing 0000–0017 legacy hashes match the runner's explicitly approved compatibility registry; no hash was rewritten or newly exempted. All entries retain SchemaVersion `1.0.0`, so migration IDs/hashes—not that version string alone—establish currency.

| Pending migration, in order | Expected changes | CanonicalV2 hash approved for this proposal |
|---|---|---|
| 0086-prescription-description-identity-intent | PatientPrescription nullable TreatmentType, SubstitutionProhibited and three independent native identity snapshot columns; six replacement prescription procedures | `0838882E02F28693B4E287AE470C8CBEEC880FC5412A42CAA78DA373FFD16F30` |
| 0087-medication-plan-intent-safeguards | PatientMedication nullable LongTermIntent; five replacement medication procedures including atomic audited Discontinue | `1775FF4542D20F02C094FAFC1D6901BAD66235846A9063634CADC8F1B86E6B18` |
| 0088-allergy-drug-applicability | PatientAllergy nullable DrugApplicability, allowed-value/DIN constraint and two replacement Allergy procedures | `8D64EAB587D06B151D3400BF42FBB7475C930BA62B7691DED1C67761EA3C2B8B` |

Live prerequisites passed: all **23 targeted existing objects** (ten tables and thirteen procedures) present; Provider.FirstName/LastName are nvarchar(100), BillingNumber nvarchar(50); all **seven proposed columns absent**; existing CK_Allergy_DIN enabled/trusted. Existing operator permission metadata reports CREATE PROCEDURE and ALTER dbo available, db_owner membership; no rights were granted. Compatibility level 150 supports the JSON features used. This is target readiness, not a new execution test on that SQL engine.

Expected procedure replacements:

- Prescription: GetByPatientUid, GetByUid, CreateDraft, UpdateDraft, Finalize, SignAndFinalize.
- Medication: GetByPatientUid, GetByUid, Create, Update, Discontinue.
- Allergy: Create, Update.

Names above use their existing `dbo.PatientPrescription_`, `dbo.PatientMedication_` and `dbo.PatientAllergy_` prefixes. Get/List retain existing independent fields; stored procedures continue to govern clinical changes, ownership, state/version and audit.

### Data compatibility and nondestructive scope

New columns are nullable, with no default clinical fact or UPDATE/backfill outside procedure definitions. No table/clinical-record deletion, column drop, identifier replacement, existing artifact rewrite, legacy type conversion or name-based association is performed by these scripts. **0088 does drop/recreate one CHECK constraint**, not clinical data; the new definition retains the existing Medication/DIN path for legacy NULL applicability. It runs within the migration transaction.

Targeted existing-data aggregates: **zero descriptions containing unsupported line separators; zero recorded DINs outside the existing explicit Medication path**. These support the inspected compatibility conditions; they do not certify clinical meaning, scan all unrelated data or replace acceptance. Existing provider fields fit the proposed identity columns. Existing signed records retain NULL new snapshots rather than being enriched from current profiles. Prospective writes/finalizations use the new procedures after deployment.

### Existing migration mechanism and recovery semantics

Inspected the existing DatabaseTool provision/status routes, FileTenantDatabaseMigrationSource, TenantDatabaseMigrationRunner, status reader, hash policy and batch parser. The source validates controlled paths/duplicate IDs, computes canonical hashes and orders IDs. Provision acquires a tenant-database application lock, validates identity, compares applied hashes and runs every pending entry for the selected tenant. **It has no numeric-range deployment switch**: execution must abort unless the pending set is exactly these three approved hashes.

Each migration's GO batches and SchemaMigration insertion commit in **one transaction per migration**. A current-migration failure rolls back that migration; previously committed migrations remain. There is no all-three atomic transaction and no automatic down migration. Provision also updates the selected tenant's platform provisioning status and verifies completion. Do not manually alter the ledger, drop new columns, rerun raw script batches or assume restoring only a procedure undoes a committed deployment.

## 4. Backup/recovery and application prerequisites

**Blocking prerequisites before execution:**

1. Explicit owner approval of the exact tenant, three hashes and proposed rollout. Recheck identity/pending status immediately before execution; stop on any drift, unexpected migration or reassignment.
2. A fresh **verified recoverable backup** of `MicroEMR_LocalDev_Fresh` at a SQL-server-accessible owner-approved location. Read-only msdb history was accessible but returned **no completed full-backup entry for this database**. That is not proof no external backup exists; none was verified here. FULL recovery mode alone does not establish a log chain or restore readiness.
3. A controlled write-free maintenance window covering backup, migrations and API/Web rollout. No such window or service stop/restart is performed/authorized by this preparation step. Preserve the existing application/configuration deployment so rollback has a matching application version.
4. Confirm the unchanged trusted operator configuration and actor identity in the execution shell. The effective declared configuration, including DatabaseTool's API UserSecretsId and tool-specific overrides, has administration enabled and an actor configured. No configuration was changed. Tool-file-only inspection initially omitted inherited settings; `.tmp/step139/operator-gates.json` records the corrected combined observation. Secret values and actor credentials are not disclosed.
5. Coordinate the matching Step 138 API/Web application and affected frontend artifacts. Current processes exist (API PID 26024, Web PID 19272, Auth PID 8432), but the API/Web runtime-path DLLs differ from the isolated Step 138 outputs. Disk comparison does not attest loaded module versions; **Step 138 application deployment is not verified**. No Auth change/restart is required merely for these migrations.

Proposed backup/verification procedure, **not executed**: the owner/approved operator chooses and validates the server-side backup path, capacity and SQL service access, takes a fresh COPY_ONLY full backup with CHECKSUM, retains its identity/timestamp/metadata, and validates it with RESTORE VERIFYONLY WITH CHECKSUM. A restoration rehearsal to a separate approved disposable database offers stronger recovery evidence but is not authorized here. Do not overwrite an existing backup, restore over another tenant, or assume a locally named path is on the SQL server.

Recovery plan: if the current migration fails, preserve logs/status and inspect which IDs actually committed. Keep application writes paused. Either continue through the supported runner after an approved correction, or restore the verified pre-change clinical backup with the matching prior application under separate owner authorization. Reconcile only this tenant's platform status through the supported operator path; do not restore the shared platform database over other tenants or hand-edit migration history. Once writes resume, any backup restore can lose subsequent work and requires a new explicit recovery decision.

## 5. Exact proposed deployment procedure — authorization checkpoint

**STOP: the following provision/rollout steps are proposed only.** The user explicitly required: “Do not apply migrations automatically” and “STOP and request explicit owner authorization.” This is the source of the approval requirement; no additional skill-based gate is being imposed.

After authorization, verified backup and the controlled maintenance prerequisite:

1. Run the same read-only migration-status command above from `D:\MasterProgram_Ahmed\MicroEMR`. Verify tenant UID/database again; require only 0086–0088 pending, with the hashes above. Confirm the staged tool manifest/assets still match reviewed source; the three staged SQL files matched byte-for-byte in this step.
2. Apply using the existing tenant runner, **not manual SQL**:

```powershell
dotnet .tmp/step138/build/bin/MicroEMR.DatabaseTool/debug/MicroEMR.DatabaseTool.dll tenant provision --tenant-key local-dev-fresh
```

3. Re-run the read-only migration-status command. Require 89 applied/89 available, no missing/unexpected/hash mismatch, matching identity and successful selected-tenant status. Inspect the seven new columns and thirteen procedures read-only. Capture actual execution logs/times and backup reference.
4. Deploy the matching Step 138 API/Web and targeted frontend output in the approved maintenance window using the established local application launch/configuration procedure. Any stop/start/restart requires that rollout's explicit approval. Do not copy over locked running binaries or switch to unreviewed configuration to make deployment work. Resume only after matching application/schema readiness is confirmed.
5. Begin owner acceptance only with its clinical-action scope explicitly approved. This report alone authorizes neither fixture creation nor record edits/finalization/correction.

The CLI provision route itself has no tenant confirmation prompt; the explicit owner checkpoint and verified key/UID are therefore essential. The command targets one tenant; `--all`, tenant reassignment, provisioning another tenant, permission changes and deferred-feature integration are outside this proposal.

## 6. One consolidated manual acceptance checklist — prepared, not executed

Use the verified development tenant and suitable **existing synthetic records/accounts**. Record actual deployed build/migration state, patient/resource identifiers, role/actor, timestamps, expected/observed result and evidence reference. Do not include private clinical text in general logs/screenshots. If an appropriate fixture/role is unavailable, record DEFERRED and obtain specific authorization rather than manufacturing records or granting permissions. Acceptance involving save/finalize/correction requires a subsequent explicit clinical-action scope.

### A. Drug Description

- [ ] In the existing prescription editor, check the Drug Description/Product display label/help and readable full intended synthetic compound text, including multiple ingredients, concentrations, punctuation, ampersand and angle brackets displayed safely. Brand and generic remain independent. Reuse the previous DE09.003 acceptance; this is compound-context preservation, not a repeat brand closure.
- [ ] Check exactly **300 UTF-16 code units**, then the 301-unit attempt. ASCII `A` repeated 300/301 times gives unambiguous boundary inputs; supplementary Unicode characters occupy two units. The HTML maxlength may prevent the 301st unit; record that as client prevention, not an observed server rejection. Reuse Step 138's server-boundary evidence rather than bypassing browser controls or handcrafting a clinical API call.
- [ ] Check attempted multiline entry/paste. Supported behavior is a single line; no clinically meaningful text may be silently shortened. Native single-line controls may prevent/normalize line breaks; distinguish that observation from the automated CR/LF/NEL/Unicode separator rejection evidence. Record unclear loss as FAIL/deferred, not PASS.
- [ ] In the approved acceptance action scope, save/reload Draft, view finalized data and inspect a linked correction; confirm complete accepted description/treatment content retained and original artifact unchanged. If no lifecycle action is authorized, inspect suitable existing records and mark the unexercised transition portion DEFERRED, reusing automated persistence results.

### B. Prescriber identity

- [ ] For a newly governed native record, first and last names come from the original prescriber's separate profile fields, not DisplayName/current reader. OHIP is a string and preserves leading zeroes where available; unknown remains Not recorded.
- [ ] Inspect retained finalized snapshots independently of the current profile. An older finalized record without new snapshots must not acquire live names/OHIP or parsed credentials. Do not edit a provider profile simply to create this observation; reuse automated history evidence or a suitable existing comparison.

### C. Medication plan

- [ ] Check standalone, quick-add and edit-modal long-term intent Yes/No/Unknown; compare with Ongoing/Acute/Unclassified to confirm independence. In the approved save scope, reload false and unknown without classification inference.
- [ ] Check order-owned optional Treatment type separately from plan intent, prescription status and actual dispensing; omission/clear behavior uses Step 138 automation as the primary wire-contract evidence.
- [ ] Inspect a suitable explicitly Discontinued plan's Past indicator. Inactive/Acute/prescription cancellation or supersession must not silently become Discontinued plan treatment. A future-start discontinue must not invent an earlier clinical end; do not trigger it without approved write scope.

### D. Prescription order

- [ ] Inspect Substitution prohibited Yes/No/Not recorded independently of brand, treatment type and plan classification. In approved Draft actions, save, reopen and deliberately clear; omitted older data must not erase the fact.
- [ ] Reuse the combined A lifecycle pass for finalized/correction preservation and original-artifact integrity. Observe existing private-note visibility/read-only/history and status affordances while on Details. Do not replay private-note create/edit/clear/denial/print V1/V2/V3; their accepted evidence and Step 138 regression remain controlling unless a new contradiction appears.

### E. Allergy

- [ ] Inspect Drug/Non-drug/Unknown and the separate Not recorded state without changing legacy AllergenType text automatically. Check that an ambiguous type can receive a deliberately confirmed Drug applicability without forced type recoding.
- [ ] Confirm conditional DIN entry and leading-zero presentation: Drug supports recorded DIN; Non-drug/Unknown cannot retain a contradictory new DIN. Switching applicability must not silently erase the existing text; deliberate reconciliation is required. Do not equate unknown with non-drug.
- [ ] In approved acceptance saves, inspect persisted applicability and retained audit/history through existing authorized surfaces/observer evidence. No prescription-to-allergy copying, NKA assertion change or reclassification fixture is authorized by this report.

### F. Cross-cutting and evidence

- [ ] Verify the visible tenant/patient context and authorized role before each section. Reuse existing denied/read-only accounts if available; no new role grants or hand-crafted cross-tenant identifiers. Automated patient/tenant boundary assertions remain separate evidence.
- [ ] Inspect readable Bootstrap layout, labels, Not recorded/Unknown distinctions, validation feedback and retained unsaved text on errors. One coordinated pass covers the changed controls; no screen redesign or repeat whole-CPP print/customization acceptance.
- [ ] If a two-session stale-save action is explicitly authorized, confirm the conflict requires reload/review, does not silently retry and leaves clinical/audit state consistent. Otherwise retain the automated RowVersion/conflict evidence and mark this manual observation unexecuted.
- [ ] Record trusted actor and audit correlation using existing authorized metadata/observer mechanisms. Reuse automated atomic audit/rollback and permission coverage; manual appearance does not establish audit atomicity or general security certification. Check no unintended extra clinical records/actions, without deleting history.

## 7. Reused evidence and deferred scope

Reuse Step 138's **135 .NET PASS + 12 frontend PASS = 147 passing tests**, TypeScript PASS, and successful affected-project build with six NU1900 vulnerability-metadata warnings. Nothing was rerun. No new UI PASS is claimed. Step 139 checks are read-only deployment inspection and documentation, not acceptance execution.

Deferred Q5–Q7 scope remains unchanged: compliance assessor/value/history, authority actors/electronic policy, interval units/domain/precision; administered immunization dose numeric/unit/pairing policy; drug-data/facility/reason/protocol sources and identifiers, pickup zone/precision and new source/audience relationships. No feature, numeric default, external integration or new clinical interpretation is implemented. Substitution prohibition is already implemented under Step 138; it is not re-deferred.

## 8. Certification, inventory and stop

**PC07.01: PARTIAL. Before: 10 mandatory-gap IDs / three packages. After: 10 mandatory-gap IDs / three packages.** No parent closure or coverage recount. Prior verified DE09.003/.019/.023/.026 and other accepted slices retain their qualifications. Schema readiness and a prepared checklist do not establish deployment, owner acceptance or external certification.

**Authorization requested for the concrete provision/rollout procedure in section 5, conditional on the verified backup and maintenance prerequisites in section 4.** No action is inferred from silence. The owner must authorize any backup operation, API/Web stop/start and clinical acceptance actions included in the next execution scope; they have not been performed here.

Final checks: filename availability, migration order/hash/identity consistency, expected objects/compatibility, backup-history limitation, report links/whitespace and final scope review. Only this report is added outside ignored local evidence. Stop before executing migrations or clinical changes.
