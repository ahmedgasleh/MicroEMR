# Step 200 — DE09.030 local migration, runtime and recovery readiness

2026-10-26 (America/Toronto). **Overall acceptance readiness: BLOCKED by incompatible running Web/API. Migration 0095: APPLIED and verified. Recovery readiness: CONDITIONAL. Existing synthetic owned Draft and prescribing permissions: verified. A–D manual acceptance: DEFERRED.**

Documentation/read-only work only. No application/SQL edits, migration application, clinical save, fixture creation, backup/restore, deployment, service restart, build or test rerun occurred. No Git/remote/GitHub operations or external correspondence. Existing Step 199 implementation and unrelated work were preserved. Only this report is a repository deliverable; ignored `.tmp/step200` helpers/receipts support the inspection.

## 1. Existing implementation evidence reused

Start point: [Step 199 implementation report](276-step199-de09-030-dispensing-facility-address-implementation.md), its manual checklist and migration 0095; [Step 197](274-step197-de09-031-local-acceptance.md) provides the established target, fixture, runtime-provenance and recovery procedure. No certification reassessment or passing test was repeated.

| Reused Step 199 evidence | Recorded result |
|---|---|
| Address-focused backend | 42 PASS: 14 contract, 21 SQL/API, 7 Web |
| Address-focused frontend | 8 PASS |
| Relevant backend regression | 295 PASS, including focused cases |
| Relevant frontend regression | 32 PASS, including focused cases |
| Three enhanced production-view tests | PASS, desktop 1280 px / narrow 360 px; included in Web/regression counts |
| Affected-project build / TypeScript | PASS / PASS |
| Six NU1900 warnings | Unavailable vulnerability metadata, retained qualification |

Security, audit atomicity, original-prescriber ownership, patient/tenant scope, Draft-only mutation, RowVersion, normalization/limits, legacy behavior, correction inheritance/review, both finalizations and artifact/PDF retention remain supported by this automation. Its isolated LocalDB/HTML-browser evidence does not prove that the currently running development binaries contain the feature. No new manual PASS is claimed.

## 2. Verified target environment

Identity/migration SELECT observation: **2026-10-26T20:40:15Z** (16:40:15 America/Toronto). Remaining database readiness receipt: **2026-10-26T20:40:40Z**. Configuration secrets were read only in memory and not displayed. Connections were guarded to the known development endpoint/catalogs. Actual clinical identity was verified before proceeding beyond the migration gate.

| Identity | Fresh verified value |
|---|---|
| SQL endpoint | `192.168.50.1,50013` |
| SQL-reported server | `EC2AMAZ-TC26CQK\TELEMED52` |
| Tenant | `local-dev-fresh`, Active |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Clinical database | `MicroEMR_LocalDev_Fresh`, Active; server key `local-sql` |
| Supporting development databases | `MicroEMR_Platform`, `MicroEMR_Auth` |
| Tenant timezone | `America/Toronto`; no timezone conversion applies to address text |

The standing owner clarification establishes development records as synthetic. No production database was accessed. This is an existing persistent development tenant, not a disposable test database.

## 3. Migration 0095 — APPLIED / schema verified

Ledger `0095-prescription-target-facility-address`, schema version `1.0.0`, records application at `2026-10-26T20:37:15.3883116` as returned by the ledger. Stored hash matches Step 199's expected canonical value:

`FF459CC885462877200D1F1DF1949AB52513647AF3EB79AE35E215750306B688`

0094 remains applied with its expected hash. Current manifest-to-ledger ID comparison has no pending migration. The assistant did not apply or replay 0095; this is observed database state, not an inferred result of the owner's request.

| Expected component | Fresh result |
|---|---|
| `TargetDispensingFacilityAddress` | Nullable `nvarchar`, max_length 2000 bytes: 1,000 UTF-16 units |
| `FacilityAddressReviewed` | Nullable `bit`, one byte |
| Existing `RowVersion` | Non-null eight-byte timestamp |
| Address constraint | `CK_PatientPrescription_TargetFacilityAddress` present, enabled and trusted; definition includes validation, normalization and canonical binary equality |
| Address functions | `PrescriptionFacilityAddressNormalize`, `PrescriptionFacilityAddressValid` present; definitions match 0095 |
| Prescription procedures | `PatientPrescription_GetByPatientUid`, `GetByUid`, `CreateDraft`, `UpdateDraft`, `Finalize`, `SignAndFinalize`: all six definitions match 0095 |
| Input parameters | CreateDraft/UpdateDraft have address, specified and review parameters; finalization uses persisted review, not client applicability flags |

Module comparison normalized line endings, surrounding whitespace and only SQL Server's stored CREATE declaration versus source CREATE OR ALTER. Bodies were not generally whitespace/logic normalized. All eight module definitions matched. Only 0095 and immediate prescription dependencies were considered; no broad migration/certification scan was repeated.

Reused implementation/source inspection confirms nullable additive storage with no backfill, CRLF/CR → LF, explicit boundary trimming, valid Unicode, 1,000 normalized-unit validation before narrow storage, wide SQL input and no truncation. Legacy absence stays unknown; omitted update preserves, explicit null/empty clears with transactional audit. Linked corrections inherit under locks, review is saved against RowVersion, replacement/clear invalidates prior review as specified, and both finalization paths enforce address review. Original facts/artifacts remain retained under existing supersession conventions.

**Guards/recovery:** the raw script's ADD statements are not idempotent. Do not reapply it now that ledger/schema agree. The existing runner checks history/hash/pending migrations and uses its lock plus migration/ledger transaction. Unexpected partial objects, mismatched hash/identity or a different pending set require STOP, not manual fragment replay or edited migration history. No rollback/drop-column operation is prepared as acceptance cleanup.

## 4. Running runtime compatibility — NO

| Service | Verified listener/process | Start UTC |
|---|---|---|
| Web | `https://localhost:7002`, PID 26012, `MicroEMR.Web` | `2026-10-26T19:50:46Z` |
| API | `https://localhost:7003`, PID 25424, `MicroEMR.Api` | `2026-10-26T19:50:39Z` |
| Auth | `https://localhost:7179`, PID 15364, `MicroEMR.Auth` | `2026-10-26T14:32:46Z` |

Configured Web API URL and Web/API authority match these endpoints. Loaded Web/API, Application and Infrastructure module paths point to the respective repository `src/MicroEMR.Web/bin/Debug/net10.0` and `src/MicroEMR.Api/bin/Debug/net10.0` outputs.

**Feature provenance fails for these running outputs:**

- Current DLL/PDB pairs are internally consistent, but `TargetDispensingFacilityAddress.cs` is absent from the current Application PDB.
- The six other selected address-relevant source documents (models, repository, API controller, Web controller, `_Form` and Detail) do not match current source or the accepted Step 199 build checksums.
- Those binaries were written at approximately **17:01–17:05 UTC**, before their current process starts, and predate the accepted Step 199 address build. Starting those same old binaries again would not establish Step 199 compatibility.
- The accepted Step 199 DLL/PDB pairs under `.tmp/step199/artifacts/bin/.../debug` are internally consistent. The isolated build output is distinct from the old normal launch output; no new build is necessary merely to identify the mismatch.

Thus listening ports, refreshed process start times or a new static JavaScript file cannot establish feature readiness. The current Web/API are **not compatible with Step 199 acceptance**. No authenticated page, API mutation or manual acceptance was attempted to work around this mismatch. No build, deployment, binary copy or restart was performed under Step 200.

Required next prerequisite: owner-authorized activation of compatible Step 199 Web/API outputs using the existing local launch/configuration conventions, followed by fresh read-only loaded-module/source compatibility verification. This report does not authorize or execute that activation. No Auth change is established as necessary.

## 5. Recovery — CONDITIONAL, not a restore request

Fresh metadata still identifies the latest recorded full backup for **MicroEMR_LocalDev_Fresh** as **2026-10-22T22:57:49**, expressed as SQL server wall time, not asserted UTC. It is full/disk/copy-only, has backup-checksum metadata and `is_damaged = 0`. SELECT of `sys.dm_os_file_exists` for that exact recorded disk device returns file_exists 1, directory 0, parent-directory-exists 1.

| Recovery question | Finding |
|---|---|
| Does a recorded backup cover the intended catalog? | YES; database name matches |
| Does its recorded disk file exist? | YES, freshly checked |
| Is it a suitable current recovery point? | NOT ESTABLISHED; predates 0094, 0095 and later development changes |
| Has owner approved that recovery point? | No confirmation supplied |
| Is a recovery approach documented? | YES, reuse Step 197's controlled target/recovery approach and history-preserving fix-forward guidance |
| Is an operationally usable restoration procedure verified? | NOT ESTABLISHED; no full-file readability or restore test, no approved recovery-point/target confirmation |

File existence and checksum flags do not prove recoverability. Owner approval of an acceptable recovery point and usable recovery procedure remains required before acceptance execution. **Do not restore this backup for the UI test.** A restore could overwrite newer development work. No backup, restore, RESTORE VERIFYONLY or other non-SELECT operation was performed. Creating a newer backup or validating/restoring one would need a separate explicit scope.

For recovery after future authorized clinical saves, preserve history with approved corrective Draft updates/fix-forward. Do not delete test clinical/audit records, rewrite finalized artifacts, drop columns or reset the tenant as cleanup. The rollback property of an uncommitted migration transaction does not undo already committed clinical saves.

## 6. Synthetic fixture and prescribing permission readiness — YES

| Prerequisite | Fresh verified result |
|---|---|
| Account | `admin@microemr.local`; Auth subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`; active, email confirmed, no lockout timestamp |
| Tenant access | Active membership, `ClinicAdministrator` role and active Clinic Administrator profile |
| Permissions | `Patients.View` and `Prescriptions.Prescribe` effectively allowed |
| Clinical actor/provider | Active UserId `2`; active ProviderId `1`, ProviderUid `4432ffba-69f1-4d68-9969-202022051ab0` |
| Patient P | Nondeleted `9f65d4ca-c347-406a-bb32-c709a8941f80` |
| Owned correction Draft R1 | `ffd80110-e565-46de-b660-3d3f9dc7e463`, Draft, PrescriberUserId `2`, no artifact |
| R1 address/review | Both NULL; observed RowVersion `0x000000000000272F` |
| Original R0 | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`, Finalized, owner `2`, address/review NULL |
| Original artifact | `2a0416d4-546c-4de8-a83e-58c0424beff2`; original RowVersion `0x0000000000002718` |

No fixture creation or permission change is needed. Browser context is not independently authenticated by these metadata checks. Before subsequent authorized work, confirm account/tenant in the browser and reload the Draft to obtain its fresh state/token; never use the RowVersion copied from a report. Stop if ownership/state changes or the fixture disappears. Existing correction Draft status does not require finalizing it for this test.

## 7. Minimal owner UI instructions — prepared, execution blocked

A–D are the remaining **deployed runtime/save/reopen observation** checks. Step 199 already covers negative validation, security, audit rollback, RowVersion and correction lifecycle, as well as isolated browser layout; do not repeat those manual security/edge-case vectors.

**Do not execute saves until section 9's prerequisites are met.** This is an owner checklist, not authorization for Codex to save. After compatible runtime/recovery confirmation and explicit owner save authorization: open `https://localhost:7002`, sign in as the existing account, select **local-dev-fresh**, open patient P → **Prescriptions** → existing owned R1 Draft → **Edit**. Confirm identity. Use the **Target Dispensing Facility Address** textarea beside the name, leaving all other prescription facts unchanged. No new prescription/correction, signing, finalization or cancellation.

### A — enter this multiline address

```text
Maple Community Pharmacy
123 Example Street
Toronto, ON M5V 2T6
```

Click **Save Draft** once. Reopen **Edit**, then the prescription's full **Details**. Expect those exact three lines, characters and punctuation in both views. This is synthetic test address text, not postal validation or verified pharmacy identity.

### B — replace with this Unicode address

```text
Pharmacie Étoile 薬局
45 Rue de l'Exemple
Montréal, QC H2X 1Y4
```

Save Draft once and reopen Edit/full Details. Expect exact accents, non-Latin characters, apostrophe and three lines. Narrow the browser window once; controls and full Detail must remain readable without overlap with buttons or new horizontal page overflow attributable to the address.

### C — clear

Click **Clear address**, then **Save Draft** once. Reopen Edit/full Details. Expect an empty address editor and **Not recorded** in full Detail; all other order facts retained.

### D — scope protection

Inspect Create without submitting a new order, the existing Edit/editor and full Detail; the field must be confined to those approved surfaces. Return to chart prescription list and confirm no address/review text in **Actions**. View the existing finalized R0's **View/Print** output without finalizing a new order; confirm no added destination-address content or unexpected existing PDF change. Normal read/output audit may occur.

| Scenario | PASS / FAIL / DEFERRED criteria | Actual result |
|---|---|---|
| A | PASS only for observed saved/reopened exact multiline text and full Detail; mismatch/lost unrelated facts FAIL; unavailable prerequisites DEFERRED | **DEFERRED** |
| B | PASS for exact Unicode/line preservation and readable narrow layout; corruption/truncation/overlap FAIL; unexecuted portion DEFERRED | **DEFERRED** |
| C | PASS for persisted clear and Not recorded; silent preserve/lost unrelated facts FAIL; unexecuted DEFERRED | **DEFERRED** |
| D | PASS for observed Create/Edit/Detail scope, unchanged Actions and existing output; unexpected addition/change FAIL; any uninspected subcheck DEFERRED | **DEFERRED** |

Three intended future owner Draft saves. Do not mark PASS from automated evidence alone, repeat earlier facility-name acceptance as address evidence, or repeatedly click Save during the separate unresolved page-load issue.

## 8. Verified SELECT-only SQL

Identity, migration, sys-catalog/module, fixture, permission and backup/file checks were executed using existing schema/configuration. The following compact templates use that verified schema. Exact fixture/actor predicates may narrow the executed fixture observation; no values/objects are invented. Audit and artifact before/after queries below are prepared observers, **not executed manual-save evidence**. Bind parameters through the existing SQL client on the verified target; do not print credentials.

```sql
SELECT DB_NAME() AS DatabaseName,
       CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
       TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,ScriptHash,AppliedAt
FROM dbo.SchemaMigration
WHERE MigrationId IN(N'0094-prescription-target-facility-name',N'0095-prescription-target-facility-address');
SELECT COL_LENGTH(N'dbo.PatientPrescription',N'TargetDispensingFacilityAddress') AS AddressColumnBytes,
       COL_LENGTH(N'dbo.PatientPrescription',N'FacilityAddressReviewed') AS ReviewColumnBytes;
SELECT name,is_disabled,is_not_trusted,definition
FROM sys.check_constraints
WHERE parent_object_id=OBJECT_ID(N'dbo.PatientPrescription')
  AND name=N'CK_PatientPrescription_TargetFacilityAddress';
```

Bind `@PatientUid = 9f65d4ca-c347-406a-bb32-c709a8941f80`, `@PrescriptionUid = ffd80110-e565-46de-b660-3d3f9dc7e463`, `@ActorUserId = 2`, after rechecking the exact context:

```sql
SELECT r.PrescriptionUid,r.PatientUid,r.Status,r.PrescriberUserId,
       r.TargetDispensingFacilityAddress,r.FacilityAddressReviewed,
       DATALENGTH(r.TargetDispensingFacilityAddress)/2 AS AddressUtf16Units,
       r.SupersedesPrescriptionUid,r.ArtifactUid,
       CONVERT(varchar(18),CONVERT(varbinary(8),r.RowVersion),1) AS RowVersionHex
FROM dbo.PatientPrescription r
JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
WHERE r.PatientUid=@PatientUid AND r.PrescriptionUid=@PrescriptionUid
  AND r.PrescriberUserId=@ActorUserId AND r.Status=N'Draft';

SELECT a.AuditLogId,a.UserId,a.ActionName,a.CreatedAt,a.OldValue,a.NewValue
FROM dbo.AuditLog a
JOIN dbo.PatientPrescription p ON a.EntityId=CONVERT(nvarchar(100),p.PrescriptionUid)
JOIN dbo.Patient patient ON patient.PatientUid=p.PatientUid AND patient.PatientId=a.PatientId
WHERE p.PatientUid=@PatientUid AND p.PrescriptionUid=@PrescriptionUid
  AND a.EntityName=N'PatientPrescription'
ORDER BY a.AuditLogId;
```

Expect one current Draft. After separately authorized execution, compare address A → B → NULL and advancing RowVersion. Capture a fresh audit boundary first; expect three scoped `PrescriptionDraftUpdated` before/after records, actor `2`; ordinary read/output records are separate. No new lifecycle update to R0 is expected.

Bind `@OriginalPrescriptionUid = f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8` for an optional original-artifact baseline:

```sql
SELECT ArtifactUid,PrescriptionUid,
       CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),ArtifactJson)),2) AS ArtifactJsonSha256
FROM dbo.PatientPrescriptionArtifact
WHERE PatientUid=@PatientUid AND PrescriptionUid=@OriginalPrescriptionUid;
```

Backup metadata/file existence checks, actually executed read-only:

```sql
SELECT TOP(1) CONVERT(varchar(33),b.backup_finish_date,126) AS BackupFinishedServerWallTime,
       b.is_copy_only,b.has_backup_checksums,b.is_damaged,
       m.device_type,m.physical_device_name
FROM msdb.dbo.backupset b
JOIN msdb.dbo.backupmediafamily m ON m.media_set_id=b.media_set_id
WHERE b.database_name=N'MicroEMR_LocalDev_Fresh' AND b.type='D'
ORDER BY b.backup_finish_date DESC;
SELECT file_exists,file_is_a_directory,parent_directory_exists
FROM sys.dm_os_file_exists(@BackupPath);
```

`@BackupPath` was the exact returned disk device, not a guessed path. Procedure/function definition comparison used `sys.objects`/`sys.sql_modules`, restricted to dbo and the eight names in section 3; parameter/column queries used their actual object IDs. Supporting access checks reused the established exact tenant/subject membership/profile/override and active actor/Auth SELECTs. No clinical stored procedure was executed as a write probe.

## 9. Authorization prerequisites

| Gate before execution | Status |
|---|---|
| Verified development target | **VERIFIED**; refresh before subsequent action |
| Migration 0095 applied/hash/schema | **VERIFIED**; do not reapply |
| Compatible Step 199 Web/API | **BLOCKED — current loaded outputs predate address feature** |
| Existing synthetic owned Draft and prescribing rights | **VERIFIED** in metadata; fresh browser/account/state/token confirmation still required |
| Owner-approved recovery readiness | **UNRESOLVED / CONDITIONAL**; existing file is older, approved recovery point/procedure not confirmed |
| Explicit owner authorization for manual saves | **NOT SUPPLIED** by the read-only Step 200 request |

Migration application, runtime activation and clinical save authorization are separate. Prior disposable-test authorization is not persistent migration/save permission. No approval question or execution follows automatically from this readiness report.

## 10. Remaining blockers and qualifications

The immediate blocker is stale Web/API feature binaries despite the correct migrated database. Recovery approval and save authorization also remain open. No A–D manual result is available. Existing test evidence and six NuGet warnings retain their qualifications; runtime mismatch is a deployment/activation gap, not a failed rerun of the accepted implementation. File/path/PDB evidence is bounded local provenance, not an in-memory/whole-system attestation. The separate performance issue remains outside scope.

DE09.030 remains **IMPLEMENTED — PENDING LOCAL RUNTIME/MANUAL ACCEPTANCE**. DE09.031 and DE09.033 keep their qualified closure. PC07.01 remains PARTIAL; mandatory inventory stays **7 IDs / 3 packages**. No unrelated certification status changed.

## 11. Recommended Step 201

**Step 201 — controlled local Step 199 runtime activation and acceptance gate recheck.** Obtain the owner's exact authorization for compatible local Web/API activation using the accepted build/launch configuration; no additional feature work or repeat passing test suite is needed for the readiness diagnosis. After activation, verify loaded feature compatibility read-only and refresh target/migration/fixture/permission metadata. Resolve owner-approved recovery readiness. Only after explicit manual-save authorization should the owner execute the three-save A–D checklist and report actual results for documentation/qualified closure. Apply no migration again and perform no backup/restore, fixture creation or clinical save by inference.

Step 200 stops after this report. No blocked action was performed or silently replaced by a build, deployment or restart.
