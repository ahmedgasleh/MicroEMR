# MicroEMR Step 97 — M8/M9 focused automation supplement

Date: 2026-10-17. Supplements, without replacing prior evidence, the exact M8/M9 definitions in [report 131](131-step97-pc07-01-family-history-implementation.md). **PC07.01 remains PARTIAL. Inventory remains 10 mandatory IDs / three packages.** No application database, production data, shared profile, migration history or production behavior changed. No commit or merge.

## Assessment and requirement-to-test mapping

New tests are in `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs`. Existing tests below are in `FamilyHistorySqlTests.cs` and `FamilyHistoryContractTests.cs`; their assertions were inspected rather than recreated. “Existing” records coverage in source/prior report 131, not an additional execution in this task.

| Exact assertion | Existing coverage / gap before this task | Added or reused evidence | Automation and manual boundary |
|---|---|---|---|
| M8.1: F under Blair: get/update/archive 404; no changes or successful clinical audit | `ForeignPatientAndSeparateTenantCannotReadUpdateOrArchive` checks repository nulls, retained F and limited audit counts; no HTTP or whole-database comparison | New `M8BothTenantDirectionsReturn404WithoutDisclosureOrDatabaseChanges`: all three HTTP operations, real SQL and complete before/after snapshots | Backend verified; normal signed-in deployment session remains manual |
| M8.2: Tenant B list contains its local B only; F under Casey and foreign Avery is denied without disclosure/mutation | Existing isolation test uses two SQL stores but no local B, reverse list or HTTP response checks | Same new M8 test creates B through API, asserts exact local lists and both route combinations; body contains no foreign condition/UID/version | Backend verified; real login/tenant selection and normal form creation remain manual |
| M8.3: Tenant A rejects B under Avery and foreign Casey; both stores unchanged | Reverse permutations and both stores' complete audit/state evidence missing | Same new M8 test covers both combinations; compares every family row, token and audit row in both databases and explicitly checks foreign UID absence | Backend verified with real tenant identity validation; platform assignment/session integration remains manual |
| M8.4: optional nonmember Tenant B selection denial | Explicitly conditional in M8; no new membership-negative test required | No new test or PASS claim | Not exercised; use only an existing approved account if separately required |
| M9.1: entity UID, patient, actor/AuthSubjectId, UTC window, old/new clinical JSON in A and B | `SixFieldsPersistWithTrustedActorAndCorrelatedAudit` checks create actor/count and two JSON fragments; complete correlation absent | New `M9HttpSavesCorrelateCompleteAuditAndRejectedRequestsPreserveState`: real production subject-to-clinical-user lookup; all seven JSON properties (six clinical fields + status), exact patient/entity/actor/subject and per-operation SQL UTC windows | Automated and verified for isolated API/SQL saves; no claim about previously captured application records |
| M9.2: exactly one Create, one Update per edit, Archive only when performed; immutable creation, changed versions | Existing create/update/archive tests have partial counts/attribution/version checks, but not one fully correlated lifecycle | Same new M9 test: exactly three correlated A events and one B Create; different generated creator/editor identities; preserved CreatedAt/By, changed versions, recorded UpdatedBy/time; retained archived row | Automated and verified; unrelated fixture rows excluded by entity UID, complete denial snapshots still inspect all audit rows |
| M9.3: rejected stale, wrong-scope, permission attempts preserve state/version and have no successful clinical mutations/audits | `UpdatePreservesCreationAndRejectsStaleSaveAndArchive` verifies SQL stale rejection; read-only contract test uses a repository double; HTTP + real SQL evidence missing | New M9 test: stale update/archive 409, wrong-patient update/archive 404, reader create/update/archive 403, unchanged complete SQL snapshots in A/B. New M8 test supplies full cross-tenant denials | Automated and verified; clinical audit counts are separate from platform security events |
| M9.4: audit failure rolls back clinical row and audit atomically | Existing `AuditFailureRollsBackEveryClinicalMutation` already injects audit failure separately for Create/Update/Archive | Reused unchanged and rerun: three SQL cases, each throws synthetic SQL error 52997 and preserves pre-operation clinical state | Automated and verified; no failure injection in an application database or manual rollback test needed |

**M8: PARTIALLY AUTOMATED — MANUAL CHECK REMAINS.** The full backend route/denial/state assertions pass; ordinary OIDC login, platform tenant selection/assignment and normal Web form/session transport were not exercised.

**M9: FULLY AUTOMATED — VERIFIED** for the specified clinical audit correlation, rejected-mutation preservation and transactional failure semantics using isolated authenticated API/SQL fixtures. This does not retrospectively certify live M1/M2/M5 captures, a deployed database, OIDC configuration or the browser UI.

## Fixture design and scope

- Reuses `FamilyHistorySqlDatabase` and its existing generated `MicroEMR_Step97_<32 hex>` databases, actual 0075 procedures, master-connection environment override and validated prefix cleanup. The sole helper addition exposes the generated test connection internally; no alternate production connection is used. Both the new acceptance fixture and reused SQL fixture own two distinct databases. Passing runs include fixture disposal.
- New `FamilyHistoryAcceptanceDatabase` extends only these disposable schemas with tenant identity and clinical-user lookup columns, and applies actual `0018-clinical-user-auth-subject.sql`. Every patient UID, tenant UID, subject and resource UID is generated. New clinical actors use database-allocated next IDs rather than hard-coded IDs. The older reused fixture retains its existing 7/8/9 actors unchanged.
- New hosts follow the existing loopback Kestrel test pattern, binding `127.0.0.1:0`. Test authentication accepts only registered fictional subjects. Separate hosts represent valid tenant A/B sessions; subject credentials cannot be used on the other host.
- Actual production components: API controller/routes/status mapping, `PermissionPolicyProvider`, `PermissionAuthorizationHandler`, Application service, `ClinicalUserActorResolutionMiddleware`, `AuthenticatedClinicalUserAccessor`, `ClinicalUserRepository`, `TenantSqlConnectionFactory` with database identity verification, family-history repository and SQL procedures. Mutations use trusted actor context, not request actor IDs.
- Fixture inputs: effective permission sets, authentication, platform database assignment/secret providers and tenant context establishment. These are explicit inputs, not mocked clinical persistence. No mocked repository or second in-memory store substitutes for SQL isolation or transactional evidence. Platform security audit is a fail-fast test double on an unexpected path, not a claim of persisted security events.
- Snapshot comparisons include ALL family rows and ALL clinical AuditLog rows, avoiding false acceptance from a narrow time/UID filter. Successful-save audit checks use separate SQL clock boundaries for each operation, with one-second outward tolerance for AuditLog `datetime2(0)` rounding. All clinical JSON properties are compared, including NULLs. No sleeps, fixed database IDs or shared production patients.
- Cleanup disposes HTTP hosts before fixture teardown, clears SQL pools and drops only validated generated disposable databases. It does not delete or reactivate application clinical records. No dependency added.

## Executed checks

Initial focused command (repository root):

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -m:1 -nr:false -p:UseAppHost=false -o artifacts/step97/m8-m9 --filter 'FullyQualifiedName~FamilyHistoryAcceptanceTests|FullyQualifiedName~FamilyHistorySqlTests.AuditFailureRollsBackEveryClinicalMutation' --logger 'trx;LogFileName=m8-m9-focused.trx' --results-directory artifacts/step97/m8-m9-results
```

The focused build succeeded. Initial sandbox execution: **0 passed / 5 failed / 0 skipped** during fixture setup, before scenario assertions. SQL Network Interfaces error 50: LocalDB could not create/start its automatic instance (`0x89c50118`). This was an environment failure, not acceptance evidence.

Same selection retried outside the sandbox, without rebuilding or restoring:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore -m:1 -nr:false -p:UseAppHost=false -o artifacts/step97/m8-m9 --filter 'FullyQualifiedName~FamilyHistoryAcceptanceTests|FullyQualifiedName~FamilyHistorySqlTests.AuditFailureRollsBackEveryClinicalMutation' --logger 'trx;LogFileName=m8-m9-focused-retry.trx' --results-directory artifacts/step97/m8-m9-results
```

**Final: 5 passed / 0 failed / 0 skipped** (2 new HTTP/SQL cases + 3 existing rollback theory cases). Reported test duration: **2 seconds**. Evidence: `artifacts/step97/m8-m9-results/m8-m9-focused-retry.trx` (ignored local artifact; initial failure retained beside it). The two new cases contain multiple route and audit assertions; five is the runner case count, not a count of individual assertions. No full solution build or regression suite ran. Focused whitespace/diff review also passed.

## Short remaining M8 procedure

This checks the deployment/session integration omitted by the fixture hosts. Use only the fictional patients and existing writer accounts from report 131; retain archived history and reuse an active replacement if needed.

1. In normal signed-in Writer A / Tenant A and Writer B / Tenant B browser sessions, create or reuse Avery's active Mother/type-2-diabetes F and Casey's Maternal-aunt/asthma B through **Family Medical History**. Use the six-field values in report 131. Record actual patient/resource/tenant UIDs. Verify the selected tenant and each local list; no other tenant's entry should appear.
2. Using each writer's own browser session, open these read-only Web URLs on the application's origin. Replace placeholders with actual UIDs. Every foreign-resource URL must return **404**, not 401/403 or a record. This checks ordinary session-to-tenant routing without replaying mutation requests that were already verified through the automated API path:

   | Signed-in tenant | GET Web path |
   |---|---|
   | A | `/patients/{BlairUid}/family-history/{FUid}` |
   | B | `/patients/{CaseyUid}/family-history/{FUid}` |
   | B | `/patients/{AveryUid}/family-history/{FUid}` |
   | A | `/patients/{AveryUid}/family-history/{BUid}` |
   | A | `/patients/{CaseyUid}/family-history/{BUid}` |

3. Record the observed statuses and local list results. Do not mark this residual check PASS until executed. If a full deployed Web mutation/antiforgery replay is required by the reviewer, use the original M8 own-session procedure; that transport has no new automated acceptance claim here. No new manual M9 audit-failure exercise is required.

For an operator additionally correlating actual application saves, reuse the schema-verified read-only **Tenant A / Tenant B batches in report 131, section 7**. They query `TenantDatabaseIdentity`, `PatientFamilyMedicalHistory`, `AuditLog`, `Patient` and `ApplicationUser.AuthSubjectId`, guard the expected tenant, and compare actual non-NULL resource UIDs and UTC windows. Connect separately to each tenant's **clinical database**, not platform/Auth. The schema/projection used by the automated audit query was also executed successfully in this run. Do not replace those guarded batches with a query using placeholder NULL foreign IDs, and do not alter an application database to provoke audit failure.

## Operator-reported acceptance update — 2026-10-17

Source: operator's follow-up in this development conversation. These are reported outcomes, not additional agent-executed tests. The earlier report 131 execution statements and the automated results above remain historical evidence. No patient/resource UIDs, row versions, UTC windows, screenshots or SQL captures accompanied this update; none are invented here.

| Scenario | Reported status and scope |
|---|---|
| M1 — Add and retrieve | **PASS — operator reported.** One intended patient-owned F, all six values preserved/readable, trusted creation user/time, matching Create audit, no forced patient/relative linkage. |
| M2 — Edit persistence | **PASS — operator reported.** Same identity/patient, only intended clinical field changed, creation attribution preserved, new token and trusted update actor/audit. |
| M3 — CPP integration | **PASS — operator reported.** Correct source/card/tab; hide/show and selection change presentation only; selected print includes complete available active data. Duplicate PASS text in the update represents one scenario result. |
| M4 — Unrecorded versus negative | **PASS — operator reported and explicitly confirmed in follow-up.** Unknown remains distinct from negative; a useful incomplete positive entry persists; all six data types are supported without fabrication. |
| M5 — Archive lifecycle | **PASS — operator reported.** Archived row retained, new token/actor and Archive audit, clinical values preserved, no false disease resolution; no Restore expected. |
| M6 — Stale-save/archive | **PASS — operator reported.** One successful session-1 update/audit; stale edit/archive produces no successful clinical mutation/audit; data/token retain session-1 values. |
| M7 — Read-only/direct denial | **INCOMPLETE.** Operator states API routes were not checked. The isolated M9 test already verifies API create/update/archive 403 and unchanged SQL state/audit, but does not establish deployed Web replay, UI controls or normal permission configuration. No whole-scenario M7 PASS reported or inferred. |
| M8 — Resource scope | **PARTIALLY AUTOMATED — MANUAL CHECK REMAINS.** No new residual browser/session check result supplied. |
| M9 — Clinical audit | **FULLY AUTOMATED — VERIFIED for isolated API/SQL audit behavior**, as executed above. No new deployed audit-capture claim. |

Remaining acceptance items: M7 deployed UI/Web direct-denial checks (and live API checks if required beyond the existing isolated evidence); M8 normal session/tenant-selection check using the short procedure above. Retain actual supporting captures with the reported M1–M6 results. **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** This update changes documentation only; no tests rerun and no commit/merge.

## Files and Git state

Task changes only:

- Added `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs` (two tests plus scoped host/fixture helpers).
- Added one internal generated-connection helper to the already-untracked `tests/MicroEMR.Api.Tests/FamilyHistorySqlTests.cs`; existing tests unchanged.
- Added this supplement; report 131 and all earlier evidence preserved.

Branch: `feature/step-97-pc07-01-family-history`. HEAD: `859789331e57b94334a68bba70a7e24975af89bd`, unchanged. Starting working tree contained **16 modified tracked entries / 21 untracked entries** (directory entries counted as Git reports them); final tree **16 modified tracked entries / 23 untracked entries**. New entries are this report and the acceptance test file; the existing untracked SQL test retains its prior work plus the one-line helper. Existing Step 97, CPP/family-history and permission-repair changes preserved. Artifacts are ignored. Nothing staged, committed or merged.
