# Step 146 — PC07.01 recovery readiness and controlled smoke acceptance

2026-10-22. **Read-only target identity PASS. Recovery readiness CONDITIONAL; backup NOT EXECUTED; existing-tenant integrated smoke NOT EXECUTED.** PC07.01 **PARTIAL**, inventory **10 mandatory-gap IDs / three packages**, unchanged. Stop before the unapproved backup, service and clinical-write gates.

## 1. Git checkpoint and evidence scope

Read AGENTS.md, the attached Step 146 instruction and relevant deployment/recovery/acceptance sections of controlling [Step 145 report 212](212-step145-pc07-01-development-deployment-preflight.md). Reuse [Step 143 report 210](210-step143-pc07-01-compliance-authority-interval-implementation.md) and [Step 144 report 211](211-step144-pc07-01-integrated-acceptance-readiness.md); no suites repeated.

Recorded once: branch **`feature/step-144-pc07-01-integrated-acceptance`**, HEAD **`0a561092e595d2d963de40065a9eb0727094fa92`**. Existing untracked reports 211/212 and `tests/MicroEMR.Api.Tests/Step144IntegratedAcceptanceTests.cs` preserved. No branch change necessary: no product/test code changed. No staging, commit, push, merge, reset, clean or stash.

Only this report is a deliverable. Ignored `.tmp/step146` evidence and read-only helper scripts contain local metadata; no credentials, tokens or connection strings printed. No schema/data changes, migration execution, backup/restore, service restart, build or browser execution. Read-only connection/localhost inspection used approved tool escalation; that is not owner authorization for backup or clinical writes.

## 2. Fixed development target — PASS

At **2026-10-22 20:18:20 UTC**, the first identity query on the fixed connection confirmed the following before further database inspection. Persisted selection matches this tenant. No other clinical tenant database queried.

| Property | Confirmed value |
|---|---|
| Tenant key / display name | `local-dev-fresh` / Local Development Clinic Fresh (display name retained from Step 145) |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| SQL endpoint / actual instance | `192.168.50.1,50013` / `EC2AMAZ-TC26CQK\TELEMED52` |
| Actual database | `MicroEMR_LocalDev_Fresh` |
| Configured routing | Existing API/operator connection binding and persisted selection agree, as established in Step 145; this does not attest an authenticated running session's tenant claim |
| Schema baseline reused | 91/91 current, zero pending/drift; 0089/0090 already applied; eleven procedures source-matched in Step 145 |

**Do not rerun migrations, edit schema or manufacture ledger entries.** No additional migration verification suite was needed. All development records are fictional under the standing owner clarification; write authorization remains separate.

## 3. Running application verification — partial, read-only

Live process and listening-port inspection found normal `src/<project>/bin/Debug/net10.0` executables. Version values below are adjacent disk DLL metadata, not an attestation of loaded memory or exact source state.

| Process | PID | HTTPS / HTTP endpoint | Disk product version |
|---|---:|---|---|
| MicroEMR.Api | 3184 | `https://localhost:7003` / `http://localhost:5245` | `1.0.0+e1adc3f73bebe3e3dd949e197385149d54021f2a` |
| MicroEMR.Web | 13728 | `https://localhost:7002` / `http://localhost:5002` | `1.0.0+e1adc3f73bebe3e3dd949e197385149d54021f2a` |
| MicroEMR.Auth | 8432 | `https://localhost:7179` / `http://localhost:5080` | `1.0.0+b7fa5d7c12bcecc76a04f44ab40012495238218d` |

All disk file versions are `1.0.0.0`. Detailed hashes/process metadata: `.tmp/step146/processes.json`. Older product-version commits do not by themselves prove features are absent: a build can include uncommitted changes.

Read-only localhost HTTP observations:

- **Web static feature evidence PASS:** `/dist/patients/medication-classification.js` returned **200**, includes compliance and assessment-time handling, and its SHA-256 equals the repository's emitted file: `5665AFC5C27073AA7788B36E770A7DE1E50BCF95B7F3548957158AD81F8E86A5`. Disk Web assembly also contains paper/compliance property markers. These checks do not prove authenticated Razor workflow persistence.
- **Auth discovery PASS:** `/.well-known/openid-configuration` returned **200**, issuer `https://localhost:7179/`. No login/session/token operation performed.
- **API metadata verification BLOCKED:** `/swagger/v1/swagger.json` returned **500**. It establishes an HTTP response, not a healthy feature contract. No clinical endpoint was called and no diagnosis, fix or restart was attempted. Swagger is enabled unconditionally in the inspected Program.cs, so its availability would not establish Development environment anyway. Disk API assembly contains the paper property marker; an absent compliance string in that assembly is inconclusive because DTOs can live in dependencies.

Local metadata requests used curl's certificate-validation bypass **only for the explicit localhost HTTPS URLs**; this does not establish TLS certificate trust. No application authentication or transport security setting changed. Evidence: `.tmp/step146/http-summary.json` and local metadata responses.

**Environment/routing qualification:** launch profiles configure **Development**; Web settings point to API `https://localhost:7003` and Auth `https://localhost:7179`. Actual in-process environment and authenticated tenant/account routing were not independently attested. No authenticated browser session or loaded API feature contract is confirmed. Do not claim current deployed acceptance.

**Tooling:** existing Playwright/Chromium infrastructure is available from prior evidence. Step 144's test redirects production clients to isolated fixture hosts and fixture authentication; running it unchanged would not test this tenant. A bounded deployed session must use the actual endpoints, existing authorized login and approved records, with host/tenant guards. No browser was launched or test code changed here. Resolve the API metadata failure or establish an independently verified healthy feature path before writes; do not assume Swagger failure necessarily means every clinical endpoint fails.

## 4. Backup destination, integrity and recovery readiness

Proposed **SQL-server-side**, not workstation, directory:

`C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup`

Read-only SQL metadata confirms this is the default backup directory and **exists as a directory**. The SQL principal has **BACKUP DATABASE permission**. SQL database files are on server volume `C:\`, the volume containing the proposed path: **292,119,912,448 bytes free (about 272 GiB)** at inspection, versus **150,994,944 bytes (144 MiB)** allocated database/log files. This is ample observed headroom, not a reserved-space guarantee or exact predicted backup size.

**SQL service filesystem write permission is NOT VERIFIED.** Directory existence and SQL permission do not prove the service account can create a file there. No write probe, ACL modification or backup is authorized. Owner must approve the destination and backup attempt; successful backup then demonstrates actual file access. Do not enable extra SQL features or weaken permissions to test it.

Readable msdb history contains **no completed full backup for this database**. External backups might exist, but none has been verified. **Backup NOT EXECUTED; integrity NOT VERIFIED; recovery readiness CONDITIONAL.** Evidence: `.tmp/step146/readiness.json`.

Exact proposed command after explicit approval, using a new unique file and an existence guard; **prepared only, not executed**:

```sql
USE [MicroEMR_LocalDev_Fresh];
IF DB_NAME() <> N'MicroEMR_LocalDev_Fresh'
 OR NOT EXISTS (
   SELECT 1 FROM dbo.TenantDatabaseIdentity
   WHERE TenantUid = 'f94c6ecd-4417-483e-8a94-0507cf8384c2'
     AND TenantKey = N'local-dev-fresh')
 THROW 50000, 'Unexpected tenant/database; stop.', 1;

DECLARE @BackupFile nvarchar(4000) =
 N'C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\'
 + N'MicroEMR_LocalDev_Fresh_step147_'
 + REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'') + N'.bak';
IF EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@BackupFile)
           WHERE file_exists = 1 OR file_is_a_directory = 1)
 THROW 50001, 'Backup destination already exists; stop.', 1;

SELECT @BackupFile AS ServerSideBackupFile;
BACKUP DATABASE [MicroEMR_LocalDev_Fresh]
 TO DISK = @BackupFile
 WITH COPY_ONLY, CHECKSUM, NOINIT, STATS = 10;
RESTORE VERIFYONLY FROM DISK = @BackupFile WITH CHECKSUM;
RESTORE HEADERONLY FROM DISK = @BackupFile;
```

Run only against the fixed verified endpoint/instance. Record the unique path, completed backup result, header database/time/checksum information and verification result. Recheck free space/identity immediately beforehand. COPY_ONLY preserves the differential base; CHECKSUM/VERIFYONLY improve integrity evidence. VERIFYONLY does not prove a successful restore. Unique filenames, existence guard and NOINIT avoid overwriting an existing backup; retain files according to an owner-approved policy.

Recovery would require a separately authorized restore of a verified backup to the correct target, with controlled writes and awareness that later changes would be lost, or a reviewed forward repair. No restore rehearsal, restore permission or retention/access plan has been verified. A new backup contains already-applied 0089/0090 and protects the present state; it cannot roll back to pre-migration schema. Do not reverse additive migrations or assume FULL recovery implies a usable point-in-time log chain. Any restore, including a disposable rehearsal, needs separate approval; none proposed for execution now.

## 5. Existing synthetic candidates — identified, none approved for writes

The persisted selection identifies non-deleted patient **`9f65d4ca-c347-406a-bb32-c709a8941f80`**, in this fixed tenant. Candidate queries were restricted to this patient and necessary state/ownership metadata; no notes/artifacts were changed.

| Candidate | ID | Suitability / limitation |
|---|---|---|
| Medication plan | `2521d27e-172f-47a1-9326-72eea8b0f571` | Active; no compliance assessment/revisions currently. Existing synthetic development record, but no explicit dedicated-test marker. Owner must explicitly designate it for the bounded assessment/history smoke; no fixture created or renamed |
| Draft prescription | `ffd80110-e565-46de-b660-3d3f9dc7e463` | Explicit `TEST ONLY date precision` product marker; Draft, linked correction of the finalized record below. Original prescriber clinical user ID **2**, provider UID `4432ffba-69f1-4d68-9969-202022051ab0`, active. Candidate only for approved Draft paper/interval edits; no finalization or new correction |
| Finalized prescription | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8` | Same explicit test marker/prescriber; proposed read-only preservation check. Original artifact must remain unchanged |

**Approved synthetic record set: none.** The prescription marker supports dedicated test suitability; the plan is a proposed existing synthetic candidate requiring owner designation. These records are fictional under the standing clarification, not a clinical-provenance blocker. Their existence does not authorize writes. Account-to-clinical-user binding and current permissions still require verification in the real session. Prescriber ID 2 is an eligibility prerequisite for this Draft, not permission to impersonate a user. Compliance requires an active actor with Patients.View and ClinicalData.Manage and authorized patient/tenant access; no additional professional-role rule.

## 6. Exact approval gates and bounded next work

No subsequent approval was supplied in this step. Request the following concrete scope for **Step 147**:

1. **Backup:** approve the exact server directory and COPY_ONLY/CHECKSUM backup plus VERIFYONLY/HEADERONLY above, exclusively for the fixed tenant/database/endpoint. No restore or schema operation. If file access fails, stop and report the SQL service access requirement rather than changing ACLs.
2. **Runtime:** permit targeted read-only diagnosis of the Swagger 500 and confirmation of actual Development environment/session tenant. Any needed API/Web build or minimal restart must be specifically proposed and separately approved after diagnosis; no Auth restart is currently justified. Product/test changes require a dedicated local implementation branch before editing.
3. **Records and clinical writes:** explicitly approve the listed patient, medication plan and Draft IDs, an existing authorized assessor account and the original-prescriber account for the Draft. Only after successful backup verification and runtime/tenant checks, approve one bounded real-browser smoke session: an explicitly selected synthetic compliance assessment and one revision to verify history, Draft paper Yes then Not recorded and a positive whole-day interval (e.g. 7), save/reload, no-write validation/permission-denial checks using suitable existing accounts, audit attribution and original finalized read-only preservation. Preserve all resulting history/audit; no deletion/cleanup, new fixtures, grants, finalize or new correction. Agree the synthetic assessment values before writing; no clinical judgment is inferred by the test.

No write is authorized merely by approving read-only diagnosis or the backup. If appropriate accounts/record scope remain unavailable, report the exact deferred check. Recheck RowVersion before any permitted save; stop on stale state rather than overwrite. Permission-denial checks must use real existing restricted credentials, never altered grants or fixture authentication.

After these gates, run one scoped real Web → API → SQL session, with desktop/mobile DOM, labels, keyboard and useful screenshots. No screenshot-baseline PASS without an approved baseline. Do not rerun the isolated suites. Stop on unexpected state, wrong tenant, failed backup, unknown app context or an authorization/concurrency error.

## 7. Evidence reconciliation and disposition

| Evidence | Step 146 outcome |
|---|---|
| Fixed tenant identity and backup location/capacity metadata | **PASS — read-only** |
| Web served compliance script / Auth discovery | **PASS — limited read-only runtime evidence** |
| API running feature contract, actual environment and authenticated tenant session | **NOT VERIFIED**; Swagger 500 and context limitations above |
| Backup and integrity verification | **NOT EXECUTED** — destination/execution approval outstanding; filesystem write access unverified |
| Existing-tenant browser → API → SQL smoke | **NOT EXECUTED** — recovery/runtime/record/write gates outstanding |
| New automated tests/builds/browser/screenshots/accessibility checks | **None executed**; no new PASS or visual acceptance claim |
| Prior Step 143 evidence | Retain 30 backend/API/SQL, 10 rendering and 10 Playwright PASS results; other focused evidence as report 210, no rerun |
| Prior Step 144 isolated integrated evidence | Retain both desktop/mobile real Web → API → SQL PASS results against disposable fixtures; no rerun and no relabeling as this tenant's acceptance |

The only subjective owner checks remain **PENDING**: Prescription wording/layout; Medication Plan wording/layout; Allergy wording/layout. No added manual functional checklist. Preserve the four recorded Drug Description PASS results, prior 13 browser-test PASS results, earlier 147 passing tests and previously verified certification closures/qualifications. Overall stability confirmation is not unreported owner acceptance.

**PC07.01: PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Readiness adds evidence without closing outstanding acceptance/source obligations. No new assessment of deferred requirements.

**Proposed Step 147:** owner-approved recovery checkpoint and bounded deployed smoke, preceded by read-only runtime clarification; no migration rerun. If approvals are withheld, keep dependent execution NOT EXECUTED. Stop after this report; no backup, restart or clinical write performed.
