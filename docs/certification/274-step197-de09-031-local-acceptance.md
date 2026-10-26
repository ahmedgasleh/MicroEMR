# Step 197 — DE09.031 local migration verification and manual acceptance

2026-10-26 (America/Toronto). **0094 APPLIED; schema and bounded feature runtime compatible; existing fixture ready at preflight. Recovery readiness CONDITIONAL. Owner-reported enter/save, replace/save/reopen and clear/Not recorded workflow PASS; remaining manual observations unconfirmed. DE09.031 pending remaining manual verification.** PC07.01 remains PARTIAL; inventory remains **7 IDs / 3 packages**.

The assistant performed repository inspection, SELECT-only development-database checks, local process/module inspection and an unauthenticated static-asset GET. The assistant performed no migration, clinical save, fixture creation, backup/restore, deployment, restart, build, test rerun, Git operation or external correspondence. The owner's subsequent UI-save results are recorded separately below. The only repository deliverable is this report; ignored `.tmp/step197` inspection helpers/receipts support it. Earlier performance-diagnostic configuration and all other existing work were preserved. Performance investigation is separate and remains unresolved.

## 1. Evidence reused and verified target

Start point: [Step 196](273-step196-de09-031-local-acceptance-readiness.md), with [Step 195 implementation/evidence](272-step195-de09-031-dispensing-facility-implementation.md) and its approved [Step 194 contract](271-step194-de09-031-dispensing-facility-contract.md). No completed assessment or acceptance suite was repeated.

Step 195 evidence retained: **37 focused backend / 6 focused frontend PASS; 199 relevant backend / 24 frontend regression PASS**, with focused cases included in regression totals. Affected-project build and TypeScript PASS; seven final targeted Web cases PASS. Six NU1900 vulnerability-metadata warnings remain qualified; they are not compiler/test failures or evidence of vulnerability-free dependencies. Security, tenant/patient isolation, original-prescriber ownership, audit rollback, RowVersion, correction carry-forward/review and immutable-artifact checks are reused rather than repeated manually.

Fresh identity/migration observation: **2026-10-26T19:41:01Z**. Remaining database readiness observation: **2026-10-26T19:42:43Z**. Secret bindings were read in memory, guarded against a different endpoint/catalog and never printed.

| Identity | Verified value |
|---|---|
| Configured endpoint | `192.168.50.1,50013` |
| SQL-reported server | `EC2AMAZ-TC26CQK\TELEMED52` |
| Database | `MicroEMR_LocalDev_Fresh` |
| Tenant | `local-dev-fresh`, Active; assigned database Active |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Supporting development catalogs | `MicroEMR_Platform`, `MicroEMR_Auth` |

Actual clinical identity was verified before proceeding past the migration gate. The missing-migration STOP condition did not trigger: 0094 is now present. This updates Step 196's historical observation, not its contents.

## 2. Migration and SQL readiness — VERIFIED

`0094-prescription-target-facility-name` is recorded with schema version `1.0.0`, applied timestamp `2026-10-26T17:02:07.5665096` as returned by the ledger, and the expected canonical hash:

`7FA09B20F5704679309333787158BA733E877BD4FB8AED9CB7AE92B818EA9EF3`

0093 remains recorded. No migration was applied or replayed by the assistant.

| Expected SQL component | Fresh result |
|---|---|
| `PatientPrescription.TargetDispensingFacilityName` | Nullable `nvarchar`, max_length 400 bytes: 200 UTF-16 units |
| `PatientPrescription.FacilityNameReviewed` | Nullable `bit`, one byte |
| Existing `RowVersion` | Non-null eight-byte timestamp |
| `CK_PatientPrescription_TargetFacilityName` | Present, enabled, trusted; inspected definition enforces the expected normalized/valid name rule |
| `PrescriptionFacilityNameNormalize` / `PrescriptionFacilityNameValid` | Present; definitions match 0094 |
| `PatientPrescription_GetByPatientUid`, `GetByUid`, `CreateDraft`, `UpdateDraft`, `Finalize`, `SignAndFinalize` | All six present; definitions match 0094 |
| Name/explicit-presence/review input parameters | Present on CreateDraft and UpdateDraft; finalization derives persisted review applicability rather than receiving these input parameters |

Comparison normalized line endings, surrounding whitespace and only the declaration's `CREATE OR ALTER` versus SQL Server's stored `CREATE` form/spacing. Procedure/function bodies were compared without general whitespace or logic normalization. All eight module definitions matched. Reuse Step 195's Unicode, 200-unit, legacy-NULL, omission/preserve, explicit-clear, ownership, audit, concurrency and correction evidence; no write probes were used.

## 3. Runtime compatibility — VERIFIED for the bounded feature

| Service | Listener/process | Start UTC |
|---|---|---|
| Web | `https://localhost:7002`; `MicroEMR.Web`, PID 26520 | `2026-10-26T17:35:59Z` |
| API | `https://localhost:7003`; `MicroEMR.Api`, PID 1772 | `2026-10-26T17:29:19Z` |
| Auth | `https://localhost:7179`; `MicroEMR.Auth`, PID 15364 | `2026-10-26T14:32:46Z` |

Effective Web API and Web/API Auth authority bindings match these endpoints. Loaded Web/API modules point to the repository's respective `bin/Debug/net10.0` outputs. Web/API Application and Infrastructure DLL copies have equal hashes. Main API/Web DLL write times precede their process starts.

Reused the existing portable-PDB reader without building or running tests. DLL/PDB identities match for Application, Infrastructure, API and Web, both current outputs and the retained build artifacts used by Step 195 (`.tmp/step191b/artifacts/bin/.../debug`, as identified in Step 195's logs). **All seven selected feature source documents** match current source and accepted-build PDB checksums: facility validation, prescription models, repository, API controller, Web controller, `_Form.cshtml` and `Detail.cshtml`. Allowed encoding normalization was restricted to line endings/BOM when needed. The running Web's public facility-name JavaScript GET returned successfully and byte-hash matched the repository asset.

This supplies bounded local build/source/module-path compatibility evidence, not an in-memory binary hash or whole-system attestation. No authenticated form rendering or save was executed here. The earlier owner-supplied request log showed successful prescription reads, but that is not a DE09.031 save/reopen PASS. The separate 29-second page-load observation remains a usability qualification; do not assume a save succeeded while a request is still pending.

## 4. Recovery readiness — CONDITIONAL

Fresh SELECT-only metadata confirms the latest recorded full backup for this catalog finished **2026-10-22T22:57:49**, expressed as server wall time, not asserted UTC. It is a disk, copy-only full backup, reports backup checksums present and `is_damaged = 0`. A SELECT of `sys.dm_os_file_exists` for that exact recorded device returns **file_exists = 1**, directory = 0, parent directory exists = 1.

Thus backup file existence is now verified, resolving Step 196's file-availability uncertainty. **Readability of the entire backup, successful restore, and owner acceptance of this older recovery point are not proven.** No RESTORE VERIFYONLY, restore or new backup was run. The backup predates 0094 and subsequent development work; it cannot be described as a current post-migration recovery point.

Before manual clinical saves, the owner must confirm an acceptable recovery point and usable recovery procedure. Until then, recovery readiness remains a blocker to declaring the manual exercise ready to execute. Reuse the established procedure: exact-target checks, controlled migration runner transaction for future authorized migrations, and authorized corrective Draft updates/fix-forward after clinical saves. Preserve clinical/audit history; never delete records, drop the new columns, replay migrations or restore over later work as an informal cleanup. A restore or additional backup would need its own scope and is not performed by this report.

## 5. Synthetic fixture and permissions — VERIFIED

Standing owner instructions establish development data as fictional/synthetic; no provenance investigation or fixture creation was needed.

| Prerequisite | Fresh result |
|---|---|
| Account | `admin@microemr.local`; Auth subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`; active, email confirmed, no lockout timestamp |
| Tenant access | Active membership, `ClinicAdministrator` role and active Clinic Administrator profile |
| Required permission metadata | `Patients.View` and `Prescriptions.Prescribe` effectively allowed |
| Clinical actor/provider | Active UserId `2`; active ProviderId `1` |
| Patient P | Nondeleted `9f65d4ca-c347-406a-bb32-c709a8941f80` |
| Owned Draft R1 | `ffd80110-e565-46de-b660-3d3f9dc7e463`; Draft, PrescriberUserId `2`, no artifact |
| R1 current name/review | Both NULL; RowVersion at observation `0x000000000000272B` |
| Original R0 | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`; Finalized; owner `2`; name/review NULL; RowVersion `0x0000000000002718` |
| R0 artifact | `2a0416d4-546c-4de8-a83e-58c0424beff2` |

R1 already links to R0; no new correction or prescription is needed. Permission metadata is not proof of the currently signed-in browser context. Before owner execution, verify the displayed tenant/account/order and refresh Draft state and RowVersion through the normal UI. Never submit the token copied from this report. Stop if ownership/state changes or the fixture disappears; do not manufacture a replacement without authorization.

## 6. Exact minimal owner UI instructions

**Prepared for owner execution only after recovery confirmation. The assistant performs no clinical writes.** Use `https://localhost:7002`, sign in as the account above, select **local-dev-fresh**, open patient P's chart → **Prescriptions** → owned Draft R1 → **Edit**. Confirm its identity. Leave all other prescription facts unchanged. Do not sign/finalize, cancel, create a correction or create fixtures. Correction review/finalization security remains covered by automation; this exercise does not require checking a review box merely to save the Draft.

The Step 196 three-save plan is retained: combine B's replacement with C's Unicode name, then clear. This exercises all requested checks without a redundant fourth save.

| Check | Exact owner action | Required observation | Actual result |
|---|---|---|---|
| A — enter/reopen/full Detail | Enter `  Maple Community Pharmacy  `; **Save Draft** once; reopen Edit; return to list and open full **Details** | Exactly `Maple Community Pharmacy`, with boundary spaces removed, persists in Edit and full Detail; other facts unchanged | **Owner-reported entry/save workflow PASS**; exact trimming and full Detail observations not separately confirmed |
| B/C — Unicode replacement and responsive layout | Edit the same Draft; replace with `Pharmacie Étoile 薬局 — Centre`; **Save Draft** once; reopen Edit and full Detail; narrow the browser window | Exact Unicode name preserved, including accents/script/interior spaces; controls and full Detail readable without overlap with buttons | **Owner-reported replacement/save/reopen PASS**; exact Unicode preservation, narrow layout and full Detail observations not separately confirmed |
| B — explicit clear | Use **Clear name**, **Save Draft** once; reopen Edit and full Detail | Empty editor and full Detail **Not recorded**; other facts unchanged | **Owner-reported clear/save and Not recorded display PASS**; specific view and separate full Detail check not identified |
| D — scope and output | Inspect Create without submitting, existing Edit and full Detail; return to the prescription list. Open existing finalized R0 **View/Print** to inspect its output; do not generate a new finalized order | Name controls confined to approved Create/Edit surfaces (including existing modal); display in full Detail; no name/review text in list Actions, no added PDF field or altered original output | **DEFERRED** |

For each subcheck, PASS requires the stated observations and owner confirmation; FAIL means an observed mismatch. An unexecuted subcheck is DEFERRED. Record PDF inspection separately if it is not performed: existing automated unchanged-PDF evidence does not become a manual PASS. Opening existing output may generate its ordinary access/output audit; that does not count as a Draft update.

Expected clinical write impact, if the owner later completes this exact plan: **three successful `PrescriptionDraftUpdated` changes** on R1, actor `2`, with before/after normalized name/review metadata and advancing RowVersion. Ordinary read/output audits may also occur. No status/link/original-artifact change is expected because no lifecycle action is requested. Reuse automated audit/concurrency/security tests; do not repeat adversarial manual tests. Stop on an error, unexpected data change, wrong tenant, unresolved save or concurrency conflict.

## 7. Verified SELECT-only SQL

The identity, ledger, schema, module definitions, backup/file metadata, fixture and permission checks described above were executed read-only. These concise templates reuse Step 196's schema; bind the exact fixture parameters listed below. Q2 is the bounded version of the executed fixture SELECT. Q3 is retained for later before/after observations and **was not executed as evidence of a manual save**. No statement below applies a migration or mutates data.

### Q1 — identity and migration/schema

```sql
SELECT DB_NAME() AS DatabaseName,
       CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
       TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,SchemaVersion,ScriptHash,AppliedAt
FROM dbo.SchemaMigration
WHERE MigrationId IN(N'0093-prescription-earliest-pickup',N'0094-prescription-target-facility-name');
SELECT COL_LENGTH(N'dbo.PatientPrescription',N'TargetDispensingFacilityName') AS NameColumnBytes,
       COL_LENGTH(N'dbo.PatientPrescription',N'FacilityNameReviewed') AS ReviewColumnBytes;
SELECT name,is_disabled,is_not_trusted,definition
FROM sys.check_constraints
WHERE parent_object_id=OBJECT_ID(N'dbo.PatientPrescription')
  AND name=N'CK_PatientPrescription_TargetFacilityName';
```

Module comparison used SELECT of `sys.objects` joined to `sys.sql_modules`, restricted to dbo and the eight names in section 2. Parameter/column SELECTs used `sys.parameters`/`sys.columns` with the existing prescription object IDs. Actual definitions, not just object presence, were checked.

### Q2 — exact patient, actor and Draft

Bind `@PatientUid = 9f65d4ca-c347-406a-bb32-c709a8941f80`, `@PrescriptionUid = ffd80110-e565-46de-b660-3d3f9dc7e463`, `@ActorUserId = 2`.

```sql
SELECT r.PatientUid,r.PrescriptionUid,r.Status,r.PrescriberUserId,
       r.TargetDispensingFacilityName,r.FacilityNameReviewed,
       r.SupersedesPrescriptionUid,r.ArtifactUid,
       CONVERT(varchar(18),CONVERT(varbinary(8),r.RowVersion),1) AS RowVersionHex
FROM dbo.PatientPrescription r
JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
WHERE r.PatientUid=@PatientUid AND r.PrescriptionUid=@PrescriptionUid
  AND r.PrescriberUserId=@ActorUserId AND r.Status=N'Draft';
```

Expect exactly one row. Later owner-approved comparisons should show A's normalized name, then Unicode replacement, then NULL. No new name is expected from this read-only step itself.

### Q3 — later audit observations, not a claimed manual result

```sql
SELECT a.AuditLogId,a.UserId,a.ActionName,a.CreatedAt,a.OldValue,a.NewValue
FROM dbo.AuditLog a
JOIN dbo.PatientPrescription p ON a.EntityId=CONVERT(nvarchar(100),p.PrescriptionUid)
JOIN dbo.Patient patient ON patient.PatientUid=p.PatientUid AND patient.PatientId=a.PatientId
WHERE p.PatientUid=@PatientUid AND p.PrescriptionUid=@PrescriptionUid
  AND a.EntityName=N'PatientPrescription'
ORDER BY a.AuditLogId;
```

Capture a fresh boundary before subsequent authorized actions; compare new scoped Draft-update records afterward. Step 196's Q4 original-artifact hash SELECT remains available for optional baseline comparison; no fresh hash or visual PDF verification is claimed here.

### Q4 — backup availability, SELECT-only

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

The file check bound `@BackupPath` to the exact disk device returned above, not a guessed path. It establishes existence only, not a restore test. Supporting permissions were read from the existing tenant membership/profile/override tables for the exact tenant/subject, and active Auth/clinical actor metadata was checked without modifying permissions.

## 8. Actual results, closure and next element

**Owner-reported follow-up, 2026-10-26:** “all that staps worked, entered save replaced saved open clear save the shows nor recorded”. Record this as **PASS for the described entry/save, replacement/save/reopen and clear/save/Not recorded workflow**. It is owner-reported evidence, not an assistant-executed browser test or a fresh SQL/audit observation. The account, exact fixture UID, input strings, precise whitespace/Unicode preservation, narrow-window readability, full Detail, unchanged list Actions and existing PDF were not individually specified in this follow-up. Do not promote those additional observations to manual PASS by inference; they remain unconfirmed/DEFERRED. No manual FAIL was reported.

Earlier pickup/date UI PASS applies to DE09.033 and cannot be transferred to this name field. The earlier migration application is independently corroborated by database evidence. Section 5 values and RowVersions are preflight snapshots, not post-save observations. No automated UI saves were run: the owner's report supplied the workflow result after automation was proposed.

DE09.031 remains **IMPLEMENTED — PENDING REMAINING MANUAL VERIFICATION**, not SATISFIED/VERIFIED. Recovery readiness remains qualified; the follow-up does not confirm a usable recovery procedure/acceptable recovery point. Remaining manual observations are those identified above, rather than repetition of the successful save/replace/clear workflow. Runtime compatibility and migration/schema gates are verified within the limits stated above. There is no basis to close PC07.01 or reduce the mandatory-gap inventory.

**Recommended next action:** retain the owner-reported workflow PASS without repeating its saves; clarify the remaining Unicode/layout/full Detail/list/PDF observations before complete manual closure. Record recovery qualifications separately. Stop after documentation; no automatic UI saves or additional implementation follows.

**Next PC07.01 element to consider after DE09.031 acceptance: DE09.030, target dispensing facility address.** Existing mapping identifies it as the neighboring unresolved destination-address element. Recommend a bounded contract/read-only assessment first: verify the authoritative address definition, required structure/precision and unknown/clear/correction/audit conventions, and obtain owner/clinician policy for manually supplied address versus verified directory data. Its independent implementation readiness is **not established here**. Do not infer address or DE09.032 identity from the supplied name, initiate a directory integration, or reopen deferred drug-source/external work. Other retained parent residuals remain as documented in Steps 190/194/195.
