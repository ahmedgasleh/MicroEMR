# Targeted fix 01 — Patient document upload identity

2026-11-04 (America/Toronto). **FIXED — source and isolated automated testing. Not deployed.** The confirmed new-upload demo blocker is resolved in the verified test environment; this is not a new readiness assessment or deployed-runtime acceptance claim.

## Root cause and correction

`PatientFileService.UploadAsync` generated GUID A for the tenant/patient storage key, while `dbo.PatientFile_Create` independently generated GUID B for the persisted metadata and creation audit. The content lookup used B and correctly rejected the key ending in A.

The upload service still generates the GUID once. It now carries that GUID through the internal `CreatePatientFileMetadata.FileUid` and `PatientFileRepository.CreateAsync` to a required SQL `@FileUid` parameter. The altered procedure persists and audits that supplied identifier instead of calling NEWID(). The repository rejects Guid.Empty; SQL rejects null/empty identifiers with error 51705, while existing unique constraints reject duplicates. Patient/actor validation, transaction and creation audit behavior remain intact.

No public API contract, controller, storage provider, storage naming, retrieval ownership check, authentication, authorization or lifecycle implementation changed. Retrieval still requires the exact trusted tenant/patient/file key. No table/column change is needed.

## Files and SQL objects changed

- `src/MicroEMR.Application/PatientFiles/PatientFileModels.cs`: required internal FileUid.
- `src/MicroEMR.Application/PatientFiles/PatientFileService.cs`: passes its existing server-generated GUID with upload metadata.
- `src/MicroEMR.Infrastructure/PatientFiles/PatientFileRepository.cs`: validates and supplies `@FileUid`.
- `db/tenant-clinical/migrations/0106-patient-file-upload-identity.sql`: new additive migration altering **dbo.PatientFile_Create** only.
- `db/tenant-clinical/manifest.json`: appends migration 0106. Existing migration files/history were not rewritten.
- `tests/MicroEMR.Api.Tests/DemoWorkflowRoundTripTests.cs`: extends the existing document regression and disposable setup, with focused SQL/repository identity cases. Prior vitals test and owner changes are preserved.
- This report. Previous assessment/deployment reports were not edited.

## Automated regression results

**27 passed / 0 failed / 0 skipped**, using the current affected build. Four cases use real procedure/repository/storage integration; the other 23 are directly affected document service/controller/Web/storage/contract tests.

| Required observation | Result / evidence |
| --- | --- |
| Upload completes, appears in list | PASS — existing `DocumentUploadListOpenRetainsBytesMetadataPatientOwnershipAndAudit` |
| Identifier agrees across response, metadata and canonical storage key | PASS — explicit assertions in that same round trip |
| Owned open/download returns original bytes, filename, MIME and length | PASS — real service/SQL/local storage and controller FileStreamResult; byte comparisons before and after archive/restore |
| Successful download audit and fail-closed audit behavior | PASS — controller round trip checks the action/file/patient event through a recording audit fixture; `PatientFileDownloadAuditTests` verifies attribution, repeated reads and audit-failure disclosure prevention. No claim of a new deployed structured-audit database check. |
| Wrong-patient and foreign-tenant denial | PASS — real scoped repositories/service against two generated tenant stores |
| Invalid identity handling | PASS — `FileCreateProcedureRejectsNullOrEmptyGuidWithoutRowsOrAudit` (2 cases) and `RepositoryRejectsEmptyAndDuplicateFileIdentityWithoutOverwritingMetadataOrAudit`; no extra rows/audits or overwrite |
| Archive/restore, concurrency and immutable stored identity/bytes | PASS — extended SQL round trip plus existing `PatientFileLifecycleTests` |

Other executed suites: `PatientFileFoundationTests`, `PatientFileApiFoundationTests`, `PatientFileWebTests`. These include streaming/header lifetime, multipart transport, scoped authorization contracts, safe error mapping and local storage boundaries. Controller execution/authorization contracts are not a new live HTTP/browser-login acceptance claim.

One affected test-project/dependency build passed with **0 warnings / 0 errors**, using isolated output `.tmp/demo-readiness/document-guid-fix/bin/`. No full solution build or full 166-case suite ran; no passing cases needed repeat execution. Receipt: `.tmp/demo-readiness/document-guid-fix/results/documents.trx`; build log: `build.log` in that evidence directory.

Before test writes, read-only preflight confirmed the current user's `(localdb)\MSSQLLocalDB`, connected database master and `IsLocalDB=1`. The test process explicitly selected that integrated-security connection. Existing fixture guards generate independent GUID-named databases and synthetic tenant identities; the new migration ran only in that disposable setup. Fixture teardown removed its own generated stores/harmless bytes. No shared development/production database, membership, grant or existing patient file was changed.

## Deployment requirements

**A separately authorized SQL and API rollout is required.** Prepare/apply migration **0106-patient-file-upload-identity** through the existing tenant migration process, together with the matching Application/Infrastructure API changes. It alters a stored procedure contract, not table schema. No migration or application deployment outside the isolated tests occurred here.

The procedure deliberately requires FileUid: an older application omits this parameter, while the updated repository cannot call the old procedure. Mixed versions therefore fail uploads rather than silently recreating split identities. Coordinate activation during a controlled API rollout; deploying just the DLL or just the procedure is insufficient. No legacy NEWID fallback was added. The public upload request still has no client-controlled file identifier.

## Existing documents and recovery

The migration changes only future creation behavior. It does not update, rename, delete or regenerate existing records or storage objects. Existing canonical documents retain their identities/paths. Documents created through the earlier mismatched path may remain unreadable; their count and deployed state were not queried. This fix does not repair them.

Recovery needs a separately authorized process: establish the correct tenant and row/file ownership, compare stored bytes against recorded length/hash, and retain metadata FileUid, references and clinical/audit history. A candidate approach is to copy verified original bytes to the canonical key for the existing metadata FileUid, verify the copy, then change the metadata StorageKey through a narrowly scoped audited stored-procedure operation with concurrency protection. Preserve original bytes and diagnostic evidence until recovery is explicitly validated; stop on missing files, conflicts or identity/hash mismatch. No recovery procedure was created or executed in this task, and retrieval guards must not be relaxed to bypass the inconsistency.

## Completion and limitations

The previously failing new-upload scenario now passes in isolated testing. No remaining defect was found within the focused regressions. Deployment and any historical-document recovery remain outstanding, explicitly outside this task; the running development application is not claimed fixed yet.

Review: inspected Git status once, reviewed the minimal production diff and new procedure against its prior definition, validated the manifest addition, and ran focused build/tests plus whitespace checks. Existing owner changes were preserved. No service restart, shared-data write, remote Git operation, commit, historical recovery or additional assessment phase occurred.

## Targeted Fix 01B — Coordinated development deployment

2026-11-04 (America/Toronto). **DEPLOYED — PARTIALLY VERIFIED.** This section supersedes the earlier not-deployed status. The owner authorized migration 0106 and API deployment as one controlled operation. The deployed authenticated upload/download scenario remains unverified; the prior 27 passing isolated tests were reused, not rerun.

### Target and recovery gates

- Confirmed the established DatabaseTool and API secret configuration privately: development SQL endpoint `192.168.50.1,50013`, database **MicroEMR_LocalDev_Fresh**. A fresh read-only clinical identity query returned server `EC2AMAZ-TC26CQK\TELEMED52`, tenant UID **f94c6ecd-4417-483e-8a94-0507cf8384c2**, key **local-dev-fresh**. This is the established fictional development target, not a production/shared clinical deployment. Owner-provided Active catalog mapping remains accepted; no restricted-account platform SELECT or permission investigation was repeated.
- Exact API target: repository `src/MicroEMR.Api/bin/Debug/net10.0/`, content root `src/MicroEMR.Api`, existing `https` launch profile, **Development**, HTTPS 7003 / HTTP 5245. Before deployment API PID was **15124**; Web **20824** and Auth **16368** used their expected repository paths.
- Before migration: ledger contained 106 applied entries through **0105**, and the installed `dbo.PatientFile_Create` had no `@FileUid` parameter. The controlled helper revalidated that **0106 was the sole pending manifest entry** and that its source exactly matched the accepted tested build's migration asset. No unrelated migration was eligible for execution.
- The newest recorded full database backup was October 28. Created a fresh **COPY_ONLY full backup with CHECKSUM**, then successfully ran **RESTORE VERIFYONLY WITH CHECKSUM** before stopping the API or applying 0106. Backup: `C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_guidfix_20261104T204844Z_29110746c2fe4a978a9d88c78878f896.bak`. Existing backup files and differential backup base were preserved. Verification is media/checksum verification, not a test restore or authorization to restore the live database.
- Preserved current API/Core/Application/Infrastructure DLL/PDB pairs under `.tmp/demo-readiness/document-guid-deployment/previous-api/` and captured the installed pre-0106 procedure definition. Retained the complete accepted tested fixed API assembly set at `.tmp/demo-readiness/document-guid-fix/bin/Debug/net10.0/` as the recovery source compatible with 0106. API dependency declarations agree with the existing target; mutable DLL hashes were not used as recurring gates. **The old API pair alone is not a safe rollback after 0106.** Recovery would require diagnosing the actual failure and using a compatible fixed API set, or separately reviewed database recovery; no automatic database rollback was prepared or executed.

### Actual coordinated operation

Used the established stopped-process file-copy/API launch method, scoped to this fix. Adapted the repository migration-runner pattern—tenant provisioning lock, GO-separated batches, SQL transaction, canonical migration hash and `dbo.SchemaMigration` ledger insertion—to execute only 0106. Did not invoke unrestricted all-pending provisioning or change platform permissions/authentication settings.

1. Verified recovery/backup/profile and stopped only API PID **15124**; confirmed exit before SQL mutation.
2. Applied **0106-patient-file-upload-identity** and its ledger entry in one transaction. Only `dbo.PatientFile_Create` changed; no patient/document rows were updated. Recorded completion at approximately **20:51 UTC**.
3. Copied only the accepted tested `MicroEMR.Application.dll/.pdb` and `MicroEMR.Infrastructure.dll/.pdb` to the API directory. Immediate byte comparisons confirmed successful copies. API host/Core, runtime/dependency files and configuration were not replaced.
4. Preserved five API source/output appsettings/launch files byte-for-byte and launched `dotnet run --no-build --no-restore --launch-profile https` with hidden execution and captured logs. Launcher **27200**, API **2600**. Startup confirms Development and the expected content root/ports; inspected standard error was empty. Application/Infrastructure loaded module paths point to the intended API directory.
5. Web/Auth retained their original PIDs and were neither deployed nor restarted. No build or automated regression rerun was needed. No rollback occurred.

Two backup-helper issues were corrected before backup execution: F# string-binding syntax, then conversion of SQL file-existence flags. They caused no migration or clinical changes. Automatic approval review rejected a validation invocation of the migration helper because it judged SQL could run before the API stopped. No migration executed through that rejected action. A safer reviewed coordinated wrapper explicitly stopped the API before invoking the actual migration and continuing with the matching files; that deployment was approved and completed. No rejected action was disguised or executed as validation.

### Focused smoke results

| Check | Result and scope |
| --- | --- |
| API health | **PASS — HTTP 200**, `/health/platform`, TLS validation retained |
| Web/Auth availability | **PASS — Web 302**, `/ClinicConfiguration`; **Auth 200**, OIDC discovery |
| Tenant/database | **PASS — direct development identity and API configured target agree**; no new authenticated request-resolved context claim |
| Migration/contract | **PASS — 0106 ledger hash matches its execution receipt; uniqueidentifier @FileUid parameter exists** |
| Existing document listing | **PASS at database procedure layer** — `dbo.PatientFile_GetByPatientUid` returned one existing document row for an existing patient; authenticated API listing remains unverified |
| Existing document metadata | **PASS — ordered FileUid/PatientUid/StorageKey/RowVersion snapshot unchanged before/after deployment** |
| New synthetic upload and appearance in list | **BLOCKED / NOT RUN** — no reusable authenticated deployed client/session or designated isolated upload fixture established |
| Open/download and byte equality for a new upload | **BLOCKED / NOT RUN** — depends on the unexecuted upload scenario |
| New upload/download audit behavior in deployed runtime | **BLOCKED / NOT RUN** — no authenticated document operation executed; prior isolated audit tests remain passing evidence only |

The backup and migration were authorized environment operations, not clinical fixture writes. No new patient/document record, branding change, signing, document regeneration or historical file operation was performed. No owner manual clinical testing or credential/token extraction was requested. Test-host identities were not used to bypass the deployed API authentication boundary. The 27 isolated passes are not relabeled deployed runtime passes.

### Remaining limitation and recommendation

**Migration 0106: APPLIED. API: DEPLOYED/HEALTHY. New upload/retrieval in development: NOT YET RUNTIME VERIFIED.** No new deployment failure was observed. The remaining verification blocker is an automated authenticated upload/list/download/byte/audit check using an explicitly qualified isolated synthetic fixture. Existing mismatched historical documents remain untouched and may still be unreadable; recovery remains separately authorized work. No database restore, procedure downgrade or historical recovery occurred.

Recommendation: deployment is complete with these limitations; do not claim full document-demo acceptance until the authenticated smoke scenario is observed. The previously confirmed future-upload defect remains resolved in isolated testing and the matching fix is now deployed. Use only explicitly qualified demonstration paths; do not infer historical-document repair or begin another assessment phase.

Only this tracked report was appended in 01B. Prior source/test/report edits were preserved. Temporary preparation/deployment helpers, previous API files, original procedure, verified-backup/migration/deployment receipts, startup logs and SQL smoke receipt remain under `.tmp/demo-readiness/document-guid-deployment/`. Checks were limited to the required gates, backup verification, controlled execution, current health and read-only SQL integrity/listing checks. No application code, permissions, OAuth settings, Web/Auth deployment files, existing patient document records/storage, Git commits or remotes changed.
