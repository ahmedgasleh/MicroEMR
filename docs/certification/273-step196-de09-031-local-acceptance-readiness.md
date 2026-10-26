# Step 196 — DE09.031 local migration and manual acceptance readiness

2026-10-26, America/Toronto. **Readiness: CONDITIONAL. Migration 0094: UNAPPLIED at the read-only observation. Manual acceptance: DEFERRED.** No migration, clinical write, service restart, deployment, build or test was performed in this step. No Git operations or external correspondence were performed.

The owner subsequently stated, “let me apply 0094.” The owner will perform that action; this report does not claim it occurred. A completion statement and fresh SELECT-only verification are required to change the observed migration status. This does not authorize the assistant to perform clinical saves or runtime changes.

## 1. Reused evidence

Sources: [Step 194 contract](271-step194-de09-031-dispensing-facility-contract.md), [Step 195 implementation](272-step195-de09-031-dispensing-facility-implementation.md), and [Step 192 acceptance conventions](269-step192-de09-033-local-acceptance-readiness.md). Targeted inspection covered migration 0094, existing migration runner/hash policy, launch settings and selected effective development configuration, relevant SQL definitions and existing evidence receipts. No certification-wide reassessment was repeated.

| Recorded Step 195 evidence | Result reused; not rerun |
|---|---|
| Focused backend facility-name cases | 37 PASS |
| Focused frontend facility-name cases | 6 PASS |
| Relevant backend regression | 199 PASS, including focused cases |
| Relevant frontend regression | 24 PASS, including focused cases |
| Affected-project build / TypeScript | PASS / PASS |
| NuGet warnings | Six NU1900 vulnerability-metadata warnings; no claim that dependencies are vulnerability-free |

These results cover validation/normalization, omitted versus explicit-clear behavior, authorization and tenant/patient scope, original-prescriber ownership, Draft/state restrictions, RowVersion conflicts, audit atomicity, linked-correction inheritance/review/finalization, frozen artifacts and UI scope. Migration tests used authorized disposable databases. They do not establish application of 0094 to the persistent development tenant or manual UI PASS. Recorded regression receipts were inspected, not executed. Manual security/concurrency/correction lifecycle tests need not be repeated for this bounded check.

## 2. Verified local environment

Read-only database observation completed **2026-10-26T16:57:02Z**, with zero database writes. Configuration secrets remained in memory and were not printed. Connections were guarded against any endpoint/catalog other than the known development bindings. OS listener/process inspection was read-only.

| Item | Verified finding |
|---|---|
| Configured SQL endpoint | `192.168.50.1,50013` |
| SQL-reported server | `EC2AMAZ-TC26CQK\TELEMED52` |
| Intended tenant | `local-dev-fresh`; Active |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Clinical catalog | `MicroEMR_LocalDev_Fresh`; Active; server key `local-sql` |
| Supporting catalogs | `MicroEMR_Platform`, `MicroEMR_Auth` |
| Tenant clinical timezone | `America/Toronto`; no timezone transformation applies to this name field |
| Suitable existing account | `admin@microemr.local`; active, email confirmed, no lockout timestamp |
| Auth subject | `56c3a21b-3207-4954-a970-f32f1feb2ac2` |
| Tenant access | Active membership; `ClinicAdministrator` role; active Clinic Administrator profile |
| Required permission metadata | `Patients.View` and `Prescriptions.Prescribe` effectively allowed |
| Clinical actor / provider | Active UserId `2`; active ProviderId `1`, ProviderUid `4432ffba-69f1-4d68-9969-202022051ab0` |

Permission metadata is verified, not an authenticated browser session. Reconfirm the signed-in account and selected tenant before later authorized UI work; do not change permissions or create users to bypass a prerequisite. The standing owner clarification establishes development data as synthetic; this existing tenant is not a disposable test database.

### Launch configuration and actual listeners

All profiles set `ASPNETCORE_ENVIRONMENT=Development`.

| Service | HTTPS profile URLs | Alternative HTTP profile | Observed HTTPS listener process |
|---|---|---|---|
| Web | `https://localhost:7002;http://localhost:5002` | `http://localhost:5100` | `MicroEMR.Web` |
| API | `https://localhost:7003;http://localhost:5245` | `http://localhost:5298` | `MicroEMR.Api` |
| Auth | `https://localhost:7179;http://localhost:5080` | `http://localhost:5080` | `MicroEMR.Auth` |

Actual process paths point to the repository's respective `bin/Debug/net10.0` executables. Ports 5002, 5080, 5245, 7002, 7003 and 7179 were listening. Effective Web API base URL is `https://localhost:7003`; Web/API authority is `https://localhost:7179`. The HTTPS profile set matches those bindings; selecting another launch profile does not automatically rewrite them. No command lines, credentials or tokens were exposed.

**Loaded Step 195 feature version remains unverified.** Process names, paths and listeners identify the runtime but do not prove loaded binaries contain the facility-name implementation. Browser navigation, login and endpoint requests were not performed. No runtime activation or restart is authorized by this step.

### Existing fixture; no setup writes needed

| Record | Verified identifier/state |
|---|---|
| Patient | `9f65d4ca-c347-406a-bb32-c709a8941f80` |
| Owned correction Draft R1 | `ffd80110-e565-46de-b660-3d3f9dc7e463`; Draft; PrescriberUserId `2`; no artifact |
| R1 RowVersion at observation | `0x000000000000272B` |
| Linked original R0 | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`; Finalized; owner `2`; RowVersion `0x0000000000002718` |
| Original artifact | `2a0416d4-546c-4de8-a83e-58c0424beff2` |

Use R1 without creating another patient/order/correction. Refresh its state and token before later saves; older Step 192 tokens are stale. No finalization, signing, cancellation or supersession is needed. A correction review checkbox can appear when the current name is known; its finalization enforcement remains covered by automation and is not a reason to sign this Draft.

## 3. Migration 0094 verification

Inspected `db/tenant-clinical/migrations/0094-prescription-target-facility-name.sql` and the existing `TenantDatabaseMigrationRunner`/hash policy.

| Concern | Finding |
|---|---|
| Additive storage | Nullable `TargetDispensingFacilityName nvarchar(200)` and `FacilityNameReviewed bit`; no clinical backfill |
| Length/Unicode | 200 normalized UTF-16 units / 400 bytes; wide input validated before narrowing; paired surrogates accepted; unpaired surrogates, C0/C1 controls and line/paragraph separators rejected |
| Normalization | Explicit defined boundary-space trimming; internal text/case/accents preserved; empty or space-only becomes NULL; no inferred facility identity |
| SQL enforcement | `PrescriptionFacilityNameNormalize`, `PrescriptionFacilityNameValid`, `CK_PatientPrescription_TargetFacilityName` |
| Stored procedures | Existing `PatientPrescription_GetByPatientUid`, `GetByUid`, `CreateDraft`, `UpdateDraft`, `Finalize`, `SignAndFinalize` extended |
| Request compatibility | Optional wide name plus independent specified flag distinguishes omitted/preserve from explicit null/clear; legacy rows remain nullable/unknown |
| Security/audit/token | Existing clinical scope, ownership, state and RowVersion predicates retained; transactional before/after audit; failed audit rolls back data/token |
| Correction | Locked source carry-forward; review required for known source/current correction name; replacement/clear invalidates review unless explicitly reviewed in the same save; both finalization paths enforce it |
| History/output | Prospective artifact JSON freezes name/review; old artifacts untouched. No PDF row or list Actions field introduced |

**Observed current state:** manifest 95 entries (0000–0094), ledger 94 entries ending at `0093-prescription-earliest-pickup`; the only pending entry is `0094-prescription-target-facility-name`. No unexpected applied IDs or unapproved hash mismatches; 18 historical hashes are recognized by existing approved legacy policy. Canonical 0094 hash:

`7FA09B20F5704679309333787158BA733E877BD4FB8AED9CB7AE92B818EA9EF3`

Current metadata has neither new column, neither facility-name scalar function, no new constraint and no new name/specification/review procedure parameters. Existing RowVersion is non-null eight-byte timestamp. This corroborates **UNAPPLIED**, rather than merely an absent ledger entry. Migration 0093 remains applied.

The raw 0094 script is **not idempotent**: column/constraint ADD statements cannot safely be replayed. The existing runner checks history/hashes, takes its migration lock and runs each migration's GO batches plus ledger insertion in a transaction, rolling back on failure. It applies all pending migrations, so recheck the complete pending set/hash immediately before owner application. Stop on an unexpected migration, identity/hash mismatch, partially present objects or runner error; do not edit history or manually replay fragments.

## 4. Recovery and authorization prerequisites

Existing procedure follows Step 192: verify exact target, current recovery evidence, controlled runner transaction and fresh metadata before UI saves. `msdb.dbo.backupset` reports the latest full (`D`), copy-only backup for this catalog finished **2026-10-22T22:57:49**, expressed as server wall time, not asserted UTC. History does not prove the file exists, is readable or can restore, and predates subsequent changes. No backup or restore was performed.

| Prerequisite | Status before subsequent action |
|---|---|
| Exact server/catalog/tenant identity | VERIFIED above; refresh before migration/saves |
| Development-only synthetic data basis | ESTABLISHED by standing owner instruction; production excluded |
| Current recoverability and acceptable recovery point | **BLOCKED** until current backup availability/restoration approach is confirmed |
| Migration state | VERIFIED UNAPPLIED at observation; owner intends to apply; **completion unverified** |
| Assistant migration execution | Not requested; owner will apply. Prior disposable-test approval does not authorize assistant application here |
| Step 195 runtime compatibility | **BLOCKED/unverified** until confirmed; no restart/deploy in this step |
| Exact owned fixture and required permission metadata | VERIFIED; refresh Draft ownership/state/token and browser context |
| Clinical-save authorization | **BLOCKED** pending authorization for the bounded three-save manual check |
| Expected audit impact | Three `PrescriptionDraftUpdated` records for the three successful changed saves, actor `2`, scoped patient/order and before/after name; ordinary read/output audits may also occur |
| Recovery approach | **BLOCKED** pending confirmation: runner rollback before migration commit; after accepted clinical writes use authorized corrective Draft updates/fix-forward while preserving audit/history |

Do not drop the columns, delete clinical/audit records, reset the database or overwrite historical artifacts to undo acceptance. A database restore can discard later work and needs its own approved target/recovery plan. Unexpected audit failure, changed ownership/state, stale token, different tenant, schema mismatch or runtime failure means stop and document; no bypass or ad hoc fixture creation.

## 5. Minimal manual acceptance — prepared, not executed

Common prerequisites for **every** scenario: all blocked prerequisites resolved; 0094 verified applied with matching hash/schema; compatible Web/API/Auth; sign in as the existing account at `https://localhost:7002`; select **local-dev-fresh**; open the verified patient chart, **Prescriptions**, owned R1 Draft **Edit**. Confirm the patient/order identity before saving. Reuse existing synthetic order facts unchanged. Capture baseline query/audit boundary before writes; use fresh UI-loaded RowVersion each time.

The scenarios combine into **three intended Draft saves**, with no fixture creation or lifecycle action. All results below are **DEFERRED** pending prerequisites and execution; none is a manual PASS.

| Scenario | Navigation/setup and action | Expected result / verification | PASS / FAIL / DEFERRED |
|---|---|---|---|
| A — enter and reopen | R1 Edit: enter `  Maple Community Pharmacy  ` in Target Dispensing Facility Name; Save Draft; reopen Edit, then full Detail | Exact normalized `Maple Community Pharmacy` persists in both views; Q2 confirms it, Q3 records update; other facts retained | PASS only after save/reopen and Detail agree. FAIL on mismatch/truncation/unrelated change. DEFERRED if prerequisites prevent execution |
| B — replace and clear | After A, Edit R1 and replace with the Unicode name in C; save/reopen. Then **Clear name**, Save Draft, reopen Edit and full Detail | Replacement persists; final value NULL and Detail **Not recorded**; Q2/Q3 after each save; independent of “reviewed” state | PASS only for both replacement and persisted clear. FAIL on silent preserve/clear or lost unrelated data. Otherwise DEFERRED |
| C — Unicode and layout | Combined with B replacement: enter `Pharmacie Étoile 薬局 — Centre`; Save Draft and reopen; inspect desktop and one narrow browser window | Exact accents/script/case/interior spacing preserved; controls/help/validation remain readable; name does not overlap buttons; Q2 exact value | PASS on preservation and readability. FAIL on corruption/overlap/unusable controls. Otherwise DEFERRED |
| D — approved scope | Inspect existing Create form without submitting, R1 Edit and full Detail. Return to chart prescription list. Inspect existing R0 View/Print only if needed to compare with the established PDF baseline; do not finalize a new order | Name entry only in approved Create/Edit surfaces (including existing modal), read display in full Detail. No name/review text added to list Actions and no new PDF content. Q4 may compare original artifact baseline; it does not itself prove PDF appearance | PASS only on observed scope; record PDF visual check DEFERRED if not viewed, retaining automated unchanged-PDF evidence. FAIL on unexpected Actions/PDF changes |

Capture exactly what was observed; do not turn automated PDF/security evidence into a claimed manual execution. No adversarial permission, concurrent editing or correction finalization exercise is required here. An optional existing View/Print read can generate its normal output audit and is not one of the three Draft writes.

## 6. SELECT-only verification SQL

The identity/ledger/sys-catalog and existing fixture checks were executed read-only on the verified development catalog. The following statements reference inspected schema; post-0094 value queries are **prepared/schema-verified, not executed against the presently unmigrated tenant**. Use a trusted connection already bound to the exact catalog above. Bind parameters through the existing SQL client; no credentials or guessed object names are needed.

### Q1 — identity and migration preflight (available before application)

```sql
SELECT DB_NAME() AS DatabaseName,
       CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
       TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,ScriptHash,AppliedAt
FROM dbo.SchemaMigration
WHERE MigrationId IN(N'0093-prescription-earliest-pickup',N'0094-prescription-target-facility-name');
SELECT COL_LENGTH(N'dbo.PatientPrescription',N'TargetDispensingFacilityName') AS NameColumnBytes,
       COL_LENGTH(N'dbo.PatientPrescription',N'FacilityNameReviewed') AS ReviewColumnBytes;
```

Observed before application: no 0094 ledger row/new columns. After owner application expect matching 0094 hash plus 400/1 bytes, and verify functions/constraint and affected procedure parameters against 0094. A ledger row alone is insufficient if schema disagrees.

### Q2 — exact owned fixture and persisted value

Bind `@PatientUid = 9f65d4ca-c347-406a-bb32-c709a8941f80`, `@PrescriptionUid = ffd80110-e565-46de-b660-3d3f9dc7e463`, `@ActorUserId = 2`. Existing fixture preflight executed with only existing columns; the name/review/length projection below must wait until 0094 is applied.

```sql
SELECT PrescriptionUid,PatientUid,Status,PrescriberUserId,
       TargetDispensingFacilityName,FacilityNameReviewed,
       DATALENGTH(TargetDispensingFacilityName)/2 AS NameUtf16Units,
       SupersedesPrescriptionUid,ArtifactUid,
       CONVERT(varchar(18),CONVERT(varbinary(8),RowVersion),1) AS RowVersionHex
FROM dbo.PatientPrescription
WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid
  AND PrescriberUserId=@ActorUserId AND Status=N'Draft';
```

Exactly one row is required. Compare name after each save to A, Unicode replacement, then NULL; each successful changed save must advance RowVersion. Never submit a token copied from this report.

### Q3 — scoped audit baseline and after-save comparison

```sql
SELECT a.AuditLogId,a.UserId,a.ActionName,a.CreatedAt,a.OldValue,a.NewValue
FROM dbo.AuditLog a
JOIN dbo.PatientPrescription p ON a.EntityId=CONVERT(nvarchar(100),p.PrescriptionUid)
JOIN dbo.Patient patient ON patient.PatientUid=p.PatientUid AND patient.PatientId=a.PatientId
WHERE p.PatientUid=@PatientUid AND p.PrescriptionUid=@PrescriptionUid
  AND a.EntityName=N'PatientPrescription'
ORDER BY a.AuditLogId;
```

Capture the last ID before approved saves; inspect new `PrescriptionDraftUpdated` entries for actor `2`, correct normalized before/after name and review metadata. No new original-order clinical update is expected because this plan never signs/finalizes. Do not equate all access/output audit records with Draft updates.

### Q4 — optional original artifact retention baseline

Bind `@OriginalPrescriptionUid = f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8` and the same patient.

```sql
SELECT ArtifactUid,PrescriptionUid,
       CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),ArtifactJson)),2) AS ArtifactJsonSha256
FROM dbo.PatientPrescriptionArtifact
WHERE PatientUid=@PatientUid AND PrescriptionUid=@OriginalPrescriptionUid;
```

Capture a fresh baseline and compare after the authorized Draft-only checks. No current artifact hash is asserted from older reports, and no PDF change is inferred from hash equality alone.

## 7. Remaining qualifications and Step 197

Security/audit automated evidence is sufficient to prepare this focused manual plan, not a new live security acceptance result. Local environment identification is complete; readiness remains **CONDITIONAL**, with actual saves blocked by the prerequisites above. The migration is verified **not applied at observation**; the owner's intended application is not proof of completion. Recovery availability, loaded Step 195 runtime and manual execution remain unverified. Six NU1900 warnings remain qualified as recorded.

**Recommended Step 197:** after the owner reports 0094 applied, perform SELECT-only identity/ledger/hash/schema and fixture rechecks, confirm recovery readiness and runtime version, then obtain/confirm authorization for the bounded three-save A–D UI check. Reuse Step 195 tests without rerunning passing suites. Record owner-reported or directly observed results with their actual provenance, audit/token evidence and any deferred PDF observation. Stop on any mismatch rather than expanding scope. No additional feature implementation is proposed.

DE09.031 supplied-name acceptance remains pending; it is not a verified registry identifier, prescription routing or actual dispensing confirmation. **PC07.01 remains PARTIAL; mandatory-gap inventory remains 7 IDs / 3 packages.** No unrelated requirement status changes.

Documentation deliverable: this report only. Ignored local `.tmp/step196` read-only inspection helpers/receipts were used; application source and SQL were not changed.
