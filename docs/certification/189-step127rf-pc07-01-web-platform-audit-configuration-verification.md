# Step 127R-F - Secure Web Platform audit configuration and verification

## Outcome and checkpoint

**Step 127R-F: PASS, with request-correlation/context qualifications below.** Added only Development Web's missing secure Platform connection, restarted only Web, and verified one owner-performed C denial produced exactly one governed security audit. Private access remained denied. The same Draft, author, version, four revisions and A/B/C private permissions are unchanged. Retain the verified secure configuration. No finalization or V2/V3 execution.

Date: 2026-10-21 (America/Toronto). Verified branch `main`, HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Filename was available. Existing AGENTS.md, four tracked telemetry source/test changes, reports 168-188 and correlation diagnostics files preserved. Entry/final tracked-diff hash `3234c02843caa14f1ab399e18f9121b19b8314ef`. Changes are this report, ignored `.tmp/step127rf/` operational evidence and the single authorized project-specific Web user-secret entry outside Git. No application source, tracked configuration, schema, grant, role, membership, provider, API/Auth/DatabaseTool configuration, migration, build/test, commit or remote operation changed.

## Scope and root cause

[R-E](188-step127re-pc07-01-private-note-denial-audit-investigation.md) established a runtime Web audit failure: SqlPlatformSecurityAuditRepository could not resolve `ConnectionStrings:PlatformDatabase`. Denial remained intact, but no governed event could persist. The old failure's exact association with C's previous attempt remains qualified; this step performs a new explicitly authorized single attempt. No need to reproduce earlier tests or investigate fictional provenance. All development data is test data under the owner clarification.

Approved server `192.168.50.1,50013`, Platform `MicroEMR_Platform`, tenant `MicroEMR_LocalDev_Fresh` / `F94C6ECD-4417-483E-8A94-0507CF8384C2`. Target patient `9f65d4ca-c347-406a-bb32-c709a8941f80`, prescription `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`; A=2, B=5, C=4. No other tenant or production access. Only the expected denial audit write was authorized; no clinical note or artifact mutation.

## Contract, identity and minimum privileges

[Web result handler](../../src/MicroEMR.Web/Authorization/MissingPermissionAuthorizationResultHandler.cs) resolves [SqlPlatformSecurityAuditRepository](../../src/MicroEMR.Infrastructure/SecurityAudit/SqlPlatformSecurityAuditRepository.cs), which creates its own Platform SQL connection using [PlatformDatabaseConnection](../../src/MicroEMR.Infrastructure/Tenancy/PlatformDatabaseConnection.cs). It does not reuse Web's HTTP API client connection or an administrative factory. Required operation is `dbo.PlatformSecurityAudit_RecordMissingPermission` EXECUTE in Platform, with normal connection/authentication; the stored procedure performs the governed insert through the existing ownership chain. Direct table INSERT/SELECT and database-owner/server administration rights are unnecessary.

An existing normal API Platform binding was evaluated as a **candidate**, not copied blindly: parsed target/identity and actual SQL authentication/permission checks passed before reuse. It targets only MicroEMR_Platform as **microemr_dev**, the established restricted normal runtime identity. No new identity/grant or administrative credential was introduced into Web.

| Actual normal-identity preflight | Observed |
|---|---|
| DB_NAME / ORIGINAL_LOGIN / SUSER_SNAME / USER_NAME | MicroEMR_Platform / microemr_dev / microemr_dev / microemr_dev |
| Execute dbo.PlatformSecurityAudit_RecordMissingPermission | 1 |
| sysadmin / db_owner / database CONTROL | 0 / 0 / 0 |
| Direct audit-table SELECT / INSERT | 0 / 0 |
| SQL Encrypt / TrustServerCertificate | True / True, exactly preserved from the existing approved Development binding |

The SQL trust-server-certificate setting is an existing Development transport qualification; it was not weakened or presented as certificate-chain validation. The actual post-attempt Web SQL session reports encryption TRUE. No broad permission inventory or assertion that this identity has no other existing normal application procedure rights is made.

Exact identity-check SELECT is retained in `.tmp/step127rf/preflight.ps1`: DB_NAME, ORIGINAL_LOGIN, SUSER_SNAME, USER_NAME, IS_SRVROLEMEMBER(sysadmin), IS_MEMBER(db_owner), and HAS_PERMS_BY_NAME for that exact procedure/table/database. SQL preflight used only normal microemr_dev credentials. Because direct audit SELECT is correctly denied, the owner separately approved **bounded reviewer reads** using the existing separate DatabaseTool reviewer (sa) for C's audit baseline/result and selected Draft metadata. That reviewer was never configured in Web or used for writes/grants/protected text. Tool sandbox escalation is recorded separately from SQL credential privilege; both read/write scopes were explicitly authorized.

## Secure setting, precedence and rollback

Only **ConnectionStrings:PlatformDatabase** was added to the project-specific .NET user-secret store **MicroEMR.Web-local-development**. No password or connection string was placed in source, appsettings, reports, arguments or terminal output. Web CreateBuilder's Development configuration uses appsettings, Development appsettings, project user secrets and then applicable environment/command-line overrides. Prior missing runtime key was established by R-E's exception; preflight confirmed absence in Web secrets and no connection argument. The new controlled launch uses explicit Development, no connection argument, and rejects conflicting inherited connection overrides; actual SQL-session identity/target was verified afterward.

Before write, the original Web secret file hash and unrelated store hashes were captured. A DPAPI-encrypted original-file rollback was saved to ignored `.tmp/step127rf/web-secret-rollback.clixml`; it contains no plaintext report/terminal secret. The apply script checked the old file hash, exactly one flattened key added, every pre-existing Web value unchanged, and API/Auth/DatabaseTool secret hashes unchanged. Final checks confirm the retained Web file matches the applied version and all three other stores retain their original hashes. Existing file access controls were not deliberately altered. Raw secret material is never copied into this report.

`.tmp/step127rf/configuration.ps1 -Mode Restore` can restore the exact original bytes/hash; the activation script would stop only its replacement Web and restore/relaunch Web if startup failed. Rollback was **not needed**. The successful secure correction is intentionally retained, not removed after testing. No temporary diagnostic/logging/correlation override was introduced.

## Web-only activation and reachability

Preflight: Web PID **18456**, API **24020**, Auth **6120**. Safe launcher metadata identified Web's parent as ordinary `dotnet run` with HTTPS profile, not watch; the inspected profile specifies Development and local Web URLs. Pre-change nonclinical unauthenticated GET `/` returned **302**, using default HTTPS certificate validation and no redirect-following/session material. This establishes reachability/authentication challenge, not every application feature.

The approved Web-only restart stopped the verified exact Web apphost and launched the existing executable from the Web project working directory, hidden, with explicit Development and the reviewed local HTTPS/HTTP URLs. New Web PID **21240**, activated at **2026-10-21T20:35:29.9810739Z**. Post-start GET `/` returned **302**; explicit Development was checked in sanitized process metadata. API PID 24020 and Auth PID 6120 remained running; neither service nor SQL/DatabaseTool was restarted.

After the denial, the narrowly selected SQL session for host_process_id **21240** directly reported login/original login **microemr_dev**, database **MicroEMR_Platform**, encryption **TRUE**. This establishes use of the intended Web binding beyond merely checking the secret file. No test clinical event was generated for connectivity; only the authorized denial produced the audit write.

## Single C attempt and audit correlation

Before the attempt, bounded metadata verified the exact approved tenant/patient/prescription, Draft, original author 2, one private note with four revisions, no C revision, and effective private-read permissions A/B true, C false, all memberships Active. Owner confirmed displayed C identity **ID 4**; instructions required the approved clinic and exact supported Web PrivateNoteEditor URL. No repeat sign-in-readiness investigation or additional chart navigation was requested.

The first owner reply confirmed C identity only; it was not counted as an access attempt. A follow-up clarified not to repeat if the URL had already been opened. The owner then stated the URL would be supplied and subsequently reported **Access Denied / Your account does not have permission to access this page.** Record one instructed/reported deliberate protected request after restart, with no refresh/retry or second endpoint instructed. No private content was reported or read by Codex. Browser method is the instructed address-bar GET; initial HTTP status/redirect trace was not captured independently.

| Verified audit metadata | Result |
|---|---|
| SecurityAuditEventUid | **72c388b8-32db-4c9e-b401-fc5575a2a0e6** |
| EventType / Outcome / DenialReason | SecurityAccessDenied / Denied / MissingPermission |
| Capability / RequiredPermission | PrescriptionPrivateNoteView / Prescriptions.ViewPrivateNotes |
| SourceApplication | MicroEMR.Web |
| RequestCorrelationId | **0HNPFMBC6PM17:00000023** |
| OccurredAtUtc | **2026-10-21T20:38:06.1847565Z** (16:38:06.1847565 America/Toronto) |
| Actor | Exact stored ActorSubject matches the Auth subject mapped from approved-tenant clinical actor **4**; raw subject omitted |
| ClinicalUserId / TargetTenantUid | NULL / NULL, expected Web contract; no invented tenant enrichment |
| Verification window | 20:34:33.1768455Z through 20:38:43.0640753Z; exactly one matching new event |

The implementation passes Web `context.TraceIdentifier` directly into the event repository's RequestCorrelationId parameter. The persisted value is verified. No independent browser/server completion trace carrying the same value was captured, so this is **bounded actor/capability/source/window correlation to the one controlled attempt**, not independently joined HTTP-trace proof or timestamp-only direct attribution. The event intentionally has no patient/prescription fields or raw URL; exact target is supported by the controlled instruction, owner action and selected fixture pre/post checks, not by an invented resource field in the security event. Tenant is verified in those preconditions and subject mapping; the event's NULL tenant remains explicit.

## Exact bounded audit query

Database: MicroEMR_Platform. Purpose: compare the new verification boundary with the one intended C Web denial. Expected/observed: zero at the pre-attempt boundary; **one** afterward. Parameters are derived from verified C mapping and database UTC; no guessed clinical-ID-as-subject substitution.

```sql
SELECT TOP(3) SecurityAuditEventUid,EventType,Outcome,DenialReason,
       ClinicalUserId,TargetTenantUid,Capability,RequiredPermission,
       SourceApplication,RequestCorrelationId,OccurredAtUtc
FROM dbo.PlatformSecurityAuditEvent
WHERE ActorSubject=@Subject
  AND (TargetTenantUid=@Tenant OR TargetTenantUid IS NULL)
  AND Capability=N'PrescriptionPrivateNoteView'
  AND RequiredPermission=N'Prescriptions.ViewPrivateNotes'
  AND SourceApplication=N'MicroEMR.Web'
  AND OccurredAtUtc>=@Start AND OccurredAtUtc<=@End
ORDER BY OccurredAtUtc;
```

`@Tenant` is the approved tenant; `@Subject` is C's mapped Auth subject; `@Start` is the database baseline UTC above; `@End` is the post-attempt database UTC. No other tenant/actor/event history is scanned. Safe exact query and parameters' derivation, selected prescription/count/private-permission checks, and process-ID-scoped SQL-session query are retained in `.tmp/step127rf/verify.ps1`; evidence in `before.json` / `after.json`. Audit schema/column allowlist was inspected in R-E; the table has no free-form clinical note/body/query payload. No protected NoteText was selected or inserted in verification, and no audit contents were rewritten.

## Post-verification integrity and retention

All nine recorded post-checks passed: exactly one denial; zero at the new baseline boundary; Draft unchanged; author 2 unchanged; parent RowVersion unchanged; selected note/revision/prescription counts unchanged; A/B/C private permissions unchanged; no finalization/artifact; no C revision. Current selected patient has one prescription, one private-note record, four revisions, zero actor-4 revisions. Bounded reads avoid opening additional clinical pages.

These checks establish unchanged selected fixture metadata and permissions, not a comprehensive unrelated-table before/after scan. No unrelated clinical change was requested/performed by Codex, and the owner was instructed only to make the protected denied request. API/Auth/DatabaseTool secret hashes and API/Auth process identities remain unchanged. The Web secret is outside Git and retained as the verified correction; encrypted rollback stays ignored. No secrets, note text or session material were exposed by the scripts/report.

## Certification and next scope

O/P/Q/R-A/R-B/R-C PASS preserved; R-E investigation PASS/classification D. **R-F PASS** resolves the demonstrated Web configuration failure and verifies the fresh denial audit with the explicit correlation/context qualifications. R-D remains recorded BLOCKED before finalization until a separately authorized acceptance continuation; this step alone does not mark its full V1-V3 PASS. V2/V3 NOT EXECUTED. PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages unchanged. No chart/encounter audit investigation reopened.

The separate prescription Details layout mismatch (`_Layout` versus chart `_AppLayout`) and unconfirmed visual items in R-D remain open; no UI repair was made. Recommended next step: separately authorize disposition/correction of that presentation issue and the precise remaining same-prescription V2/V3 acceptance, preserving actor/session checks, finalization immutability, selected output exclusion and audit integrity. Do not finalize or generate output under R-F authorization.

Checks completed: Git/checkpoint/path, targeted contract/configuration inspection, actual normal-identity privilege preflight, secure single-key change and encrypted rollback preparation, Web-only activation/nonclinical reachability, separately approved bounded reviewer baseline/post reads, owner single denial, live Web SQL identity/encryption and preservation/link/whitespace checks. No builds/tests, clinical mutation, grant, commit or push.
