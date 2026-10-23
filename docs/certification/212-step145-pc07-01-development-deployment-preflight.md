# Step 145 — PC07.01 development deployment preflight and acceptance preparation

2026-10-22. **Live schema is already current: 91/91 migrations, zero pending, no hash drift. 0089/0090 must NOT be reapplied. Installed schema/procedure preflight PASS. Backup/recovery and matching running application acceptance remain unverified.** PC07.01 **PARTIAL**; inventory **10 mandatory-gap IDs / three packages**, unchanged. Read-only checks and this report only.

## 1. Git checkpoint and scope

Read AGENTS.md and relevant contract/implementation/readiness sections of [209](209-step142-pc07-01-compliance-authority-interval-contract.md), [210](210-step143-pc07-01-compliance-authority-interval-implementation.md) and [211](211-step144-pc07-01-integrated-acceptance-readiness.md). No previous investigation or test repeated.

Recorded branch/HEAD/status once: **`feature/step-144-pc07-01-integrated-acceptance`**, **`0a561092e595d2d963de40065a9eb0727094fa92`**. Existing untracked report 211 and `tests/MicroEMR.Api.Tests/Step144IntegratedAcceptanceTests.cs` preserved. Stayed on that branch. No product/test/configuration/migration change, build, browser/test execution, clinical write, backup/restore, service action, staging, commit, merge, reset, cleanup or push.

Only this report is a deliverable. Ignored `.tmp/step145` and read-only helper scripts contain sanitized metadata, not credentials. Secret settings were used only in memory to resolve existing connections; passwords, connection strings and tokens were not printed. A local helper syntax correction occurred before its SQL execution; no execution-policy setting was changed.

## 2. Development target discovery — selected configuration plus actual SQL identity

The previously selected tenant UID in `.tmp/step127j/selection.json`, current platform assignment, API/operator secret bindings and actual `TenantDatabaseIdentity`/DB_NAME() agree. Target inspection **2026-10-22 20:05:57 UTC**, installed-schema comparison **20:07:56 UTC**.

| Property | Current verified/configured value |
|---|---|
| Intended candidate | **local-dev-fresh — Local Development Clinic Fresh** |
| Tenant UID | **f94c6ecd-4417-483e-8a94-0507cf8384c2** |
| Clinical database | **MicroEMR_LocalDev_Fresh** |
| Endpoint / actual instance | **192.168.50.1,50013** / **EC2AMAZ-TC26CQK\TELEMED52** |
| SQL version/edition | **15.0.2000.5**, SQL Server 2019 Standard Edition (64-bit) |
| Environment | Established local-development target; API launch profiles explicitly configure **Development**. Actual per-process environment variables were not freshly attested |
| Database state | ONLINE, writable, FULL recovery, compatibility **150** |
| Tenant / database assignment | Active / Active; database-server key `local-sql` |
| Application configured for this database | **Yes:** current API development secret binding and operator binding agree on endpoint/catalog; one actual identity row matches the selected UID/key. Configured binding is distinct from proof of the running process's loaded settings/build |
| Migration currency | **91 manifest / 91 applied; latest 0090**, zero pending, zero unexpected applied IDs, zero hash drift |

Multiple local-development candidates exist: `local-dev` → `MicroEMR_Db` (Active), and `local-dev-new` → `MicroEMR_Tenant_LocalDev` (Provisioning/MigrationFailed), besides `local-dev-fresh`. **No arbitrary choice:** this preflight followed the persisted selected UID and previously established development target. Only its clinical database was opened. Other candidates' clinical databases were not queried or changed. Step 146 authorization must explicitly confirm this exact candidate, not a generic “development database.”

Platform assignment was read from `MicroEMR_Platform`. The current actor/operator configuration is enabled and has an actor configured; metadata confirms BACKUP DATABASE, CREATE TABLE and ALTER dbo permissions available. No permission was granted. This does not prove server-filesystem backup access/capacity or restore permission.

## 3. Baseline reconciliation and migration readiness

The prompt's baseline that existing tenants have not received 0089/0090 is superseded **for this selected tenant** by current read-only evidence. This step did not execute them and does not infer who executed them.

| Applied migration | Ledger timestamp as recorded | Matching CanonicalV2 hash |
|---|---|---|
| 0089-medication-compliance-assessments | `2026-10-22T19:22:27.4838370` | `365AB7C5A14AF6EB775D3FD3C342C8D17DB17FDF250C36D1DD2FB39F8EFCD9EA` |
| 0090-prescription-paper-interval | `2026-10-22T19:22:27.8619010` | `96517929F7A107EBF258DA389188F38C8F37B7C9696820FFA6F510E499E34242` |

Applied hashes match current approved source. Eighteen older ledger representations match the existing pinned legacy compatibility registry; no new exception or hash rewrite. SchemaVersion remains `1.0.0`, so IDs/hashes establish currency.

**Execution order if ever pending on an approved target:** 0089 then 0090, after existing manifest prerequisites. 0089 relies on the PatientMedication unique UID, Patient/ApplicationUser/AuditLog and effective 0087 procedures; 0090 relies on effective 0086 prescription procedures and their prior date/private-note/signing dependencies. Both add nullable fields without clinical backfill or default facts. No clinical deletion, historical artifact rewrite or migration-history edit. Existing read-only source/evidence establishes compatibility intent; this preflight does not certify every current clinical row through a new test.

**Installed checks PASS:** expected current columns and revision table present; unique MedicationUid index enabled; four new CHECK constraints and revision FK enabled/trusted; assessment parameters are `nvarchar(max)`. All **eleven** installed procedures match the approved SQL source after line-ending/CREATE-header normalization: five medication Get/List/Create/Update/Discontinue, six prescription Get/List/CreateDraft/UpdateDraft/Finalize/SignAndFinalize. Existing date functions, private-note procedure, audit/provider/patient tables and artifact dependencies are present.

Raw scripts are **not safely rerunnable**: ALTER ADD, new table and constraint creation have no replay guards. CREATE OR ALTER for procedures does not make the entire scripts idempotent. Existing runner tracks IDs/hashes and applies GO batches plus each ledger entry in one transaction per migration; a later migration's failure does not undo earlier committed migrations. No automatic down migration. **Current procedure: no provision/apply command is necessary.**

If future drift/pending work is detected, stop and investigate before execution. The standard provision command runs all pending entries, not an arbitrary numeric range; never run it unless the exact authorized pending set/hashes are established. Do not rerun raw scripts or manufacture ledger entries.

**Locking/downtime:** additive DDL and procedure replacement can require schema-modification locks and block active sessions; trusted constraints can require checks. A momentary metadata snapshot showed zero granted target-database locks from other sessions, not proof a later window is safe. Migrations are already applied, so the present plan avoids this DDL/downtime entirely. Any application refresh still requires a coordinated, authorized API/Web window if needed.

Evidence `.tmp/step145/preflight.json`, `schema-current.json`, `module-comparison.json`. Earlier helper booleans assumed two pending migrations and explicit assessment text in every procedure: these are not controlling for an already-current database or a Discontinue audit using SELECT *. The exact eleven-module source comparison and installed constraints resolve those heuristic flags; no product defect was inferred.

## 4. Backup/recovery preparation — not executed

Actual server default backup directory is **`C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup`**, on the SQL server machine, not this workstation. This is a **candidate destination requiring owner approval** and verification of free space, SQL service write access, retention and restore access. Do not create a directory or assume a local file path maps to the server.

Readable msdb history returned **no completed full backup for this database**. This is not proof no external backup exists; no recoverable backup was verified. FULL recovery mode alone proves neither a usable full backup nor a valid log chain.

Proposed exact SQL procedure after explicit authorization, using the approved destination and a unique new file; **not run**:

```sql
DECLARE @BackupFile nvarchar(4000) =
 N'C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\'
 + N'MicroEMR_LocalDev_Fresh_step146_'
 + REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'') + N'.bak';

SELECT @BackupFile AS ProposedServerSideBackupFile; -- retain this exact path
BACKUP DATABASE [MicroEMR_LocalDev_Fresh]
 TO DISK = @BackupFile
 WITH COPY_ONLY, CHECKSUM, NOINIT, STATS = 10;

RESTORE VERIFYONLY FROM DISK = @BackupFile WITH CHECKSUM;
RESTORE HEADERONLY FROM DISK = @BackupFile;
```

Approve/replace the directory before execution; keep the unique filename and do not overwrite an existing backup. Retain exact file, database identity, timestamp and checksum/verification outcomes. VERIFYONLY is an integrity/readability check, **not** proof a full restore succeeds; a separate authorized restore rehearsal to a disposable database offers stronger recovery evidence.

**Recovery:** preserve current application/configuration artifacts. A known compatible prior application may be redeployed only after compatibility review; never assume dropping additive columns/constraints/procedures safely reverses the feature. Reverting the full database to a prior state requires a suitable backup/restore (and logs if an established point-in-time chain is used), or an independently reviewed forward repair. A **new backup now contains 0089/0090** and protects the current state before future actions; it cannot by itself restore the pre-0089 schema. No verified pre-migration recovery point was found. Do not promise pre-feature rollback without it.

Any restore over this tenant requires separate explicit authorization, paused writes and a verified target; it loses changes after the chosen restore point and restores that backup's migration ledger. Do not use WITH REPLACE by default or restore the whole platform database to repair one tenant. Reconcile only this tenant's platform state through the approved mechanism if needed. No backup/verification/restore operation was performed here.

## 5. Smallest safe Step 146 sequence — prepared, not executed

1. **Recheck exact target/environment:** selected UID/key, platform assignment, DB_NAME()/identity and loaded API tenant binding. Confirm local Development launch/environment and matching application version. Stop on reassignment or ambiguity; no other tenant substitution.
2. **Obtain the bounded authorization in section 7.** Confirm the approved synthetic acceptance record scope and maintenance/recovery window; no new role grants or cross-tenant records.
3. **Create and verify the approved backup** before authorized existing-tenant clinical writes or rollout. If no recoverable backup/destination can be established, stop dependent work.
4. **Migration decision:** currently **skip application of 0089/0090** because both are recorded/applied and source-matched. Fresh pending=0 means no deployment command. If anything changes, stop for a revised exact migration proposal/authorization; do not apply unapproved extra IDs.
5. **Verify installed schema/procedures read-only:** reuse this check, refreshed only for relevant drift. No record mutation to test schema existence.
6. **Refresh API/Web only if necessary and authorized:** current API PID **3184**, Web PID **13728** run from normal Debug output; their disk DLL hashes differ from isolated tested artifacts. This does not prove they are outdated or attest loaded memory. Confirm build/source/artifact identity first, then perform only the needed affected-project build/copy/start or stop/start. Auth is unchanged and does not require restart for these features. No refresh occurred in Step 145.
7. **Run one bounded automated deployed smoke session** through real browser → actual API → this SQL database, within approved existing synthetic records and actor scope, below. Do not rerun the isolated suites.
8. **Confirm approved existing records remain readable** without editing them or their historical artifacts. Record deployed version, target, results, audit/version references and any failure/recovery action.
9. **Stop on failure:** retain logs/history, do not delete clinical records or retry stale writes automatically. Recovery/restore needs its own explicit authorization; completion is not permission to broaden scope.

**Environment/application limitation:** configured API/operator SQL bindings agree and active process paths are known, but current in-process environment/configuration and feature build are not fully attested. Disk DLL inequality is a qualification, not an instruction to restart blindly. Evidence `.tmp/step145/application-state.json`; no process command-line secrets or tokens logged.

## 6. Automated deployed smoke plan and only three owner checks

Reuse all Step 143 functional/security/lifecycle evidence and Step 144's two integrated desktop/mobile PASS results; no new execution in Step 145. After authorization, reuse suitable owner-approved synthetic patients/records and authorized assessor/original-prescriber identities in this exact tenant. Do not assume a record can be revised just because it exists; new fixture creation requires explicit additional scope.

One automated session should confirm: current plan assessment/retained history loads; one approved assessment change saves/reloads with trusted actor/time and expected audit/revision; an invalid time-offset attempt presents feedback without a write; one eligible prescription Draft saves/reloads explicit paper Yes and a positive whole-day interval independently of supply/repeats; an unauthorized/read-only attempt is denied without mutation if an appropriate existing account is available. Read an approved existing finalized record and artifact without rewriting it. Finalize/correction need not be repeated; if a deployed transition is specifically required, request that action explicitly rather than infer it from smoke authorization. Do not physically remove acceptance history afterward.

Use actual browser HTTP/API clients, not Step 144 fixture authentication/tenants. Existing loopback test harness is **not directly reusable against a real tenant without a scoped deployed harness/account configuration**. Required prerequisites: matching running app, current authorized login/session, approved synthetic record IDs and explicit write/actor scope; unavailable accounts/records yield an exact deferred observation, not new grants or manufactured fixtures. Reuse DOM/keyboard checks and capture screenshots only for newly relevant presentation; no baseline comparison PASS without an approved baseline.

Keep **exactly these three subjective owner observations**, all **PENDING**:

- **Prescription wording/layout** — existing prescription fields plus paper requirement/interval meaning and presentation.
- **Medication Plan wording/layout** — assessment states, known/unknown time, retained history and independent plan intent.
- **Allergy wording/layout** — unchanged terminology/help and layout, using retained functional evidence.

No new manual boundary, SQL, security or lifecycle checklist. Automated displayed-label/value assertions remain technical evidence, not unreported owner acceptance. Prior four Drug Description owner PASS results and all verified closures remain intact.

## 7. Exact requested authorization for Step 146

Proposed owner authorization, **not assumed or executed**:

> Authorize Step 146 only for tenant **local-dev-fresh**, UID **f94c6ecd-4417-483e-8a94-0507cf8384c2**, database **MicroEMR_LocalDev_Fresh** on **192.168.50.1,50013 (EC2AMAZ-TC26CQK\TELEMED52)**. Approve the server-side backup directory in section 4 (or specify its replacement), a fresh COPY_ONLY/CHECKSUM backup and VERIFYONLY/HEADERONLY verification. Authorize matching API/Web build/refresh and minimal stop/start **only if necessary after version checks**. Authorize the bounded automated smoke writes in section 6 against explicitly approved existing synthetic record IDs and authorized actors. **Do not reapply 0089/0090 while the ledger is current; no other migrations, new fixtures, role grants, finalize/correction, restore, other tenants or production operations are authorized.** Stop if the target or pending set changes or recovery prerequisites fail.

Record IDs/actors and maintenance availability may be supplied by the owner or proposed through a separately authorized read-only suitability review before any write. They were not selected from clinical content in this metadata-only step. Restore and any newly pending schema operation still require a separate concrete authorization. No blanket deployment permission is inferred from the Step 145 request.

## 8. Disposition and next step

**Schema/procedure readiness PASS — already deployed. Backup/recovery readiness NOT VERIFIED. Application rollout/smoke readiness CONDITIONAL on build/context attestation, recovery prerequisites, appropriate approved records/session and owner authorization.** Main risks: rerunning non-idempotent scripts, mixing app/schema versions, wrong tenant selection among multiple candidates, unverified backup/restore ability and losing later data on restore. This plan avoids unnecessary migrations and stops dependent actions until ready.

**PC07.01 remains PARTIAL. Before/after: 10 mandatory-gap IDs / three packages.** DE09.004 qualified verified closure and prior closures retained; recorded migration currency alone does not close owner acceptance, external authority or other remaining requirements. Existing electronic-authority, source, immunization-dose, pickup and CPP qualifications/dependencies remain outside scope.

**Proposed Step 146:** controlled **application-state reconciliation and authorized backup/smoke acceptance**, with **no migration rerun while current**, followed by the three owner observations and evidence reconciliation. No implementation change is proposed; if one becomes necessary, stop and propose a dedicated local Step 145/next implementation branch before editing product code.

Final documentation checks: new report filename, relative links and whitespace checked. **Only this report added outside ignored read-only evidence; stop after preflight.**
