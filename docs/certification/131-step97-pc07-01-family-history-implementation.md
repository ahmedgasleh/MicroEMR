# Step 97: PC07.01 Family Medical History Bounded Implementation

Date: 2026-10-16. Implements the separately authorized [Step 96 design](130-step96-pc07-01-family-history-design-acceptance.md), reusing [Step 95 scope](129-step95-pc07-01-cpp-category-completeness-assessment.md). **Family Medical History - IMPLEMENTED - MANUAL VERIFICATION OUTSTANDING. PC07.01 remains PARTIAL; Risk Factors remains MISSING; inventory remains 10 mandatory IDs / three packages.** No manual PASS or OntarioMD acceptance is claimed.

## 1. Actual Git checkpoint and work boundary

Started on `main`, HEAD **859789331e57b94334a68bba70a7e24975af89bd** (`pc04-05 precription print closure`), with only untracked reports 128-130. All three are preserved unchanged. Created **feature/step-97-pc07-01-family-history** after a sandbox ref-lock denial and successful approved retry. HEAD is unchanged. No staging, commit, merge, application-database migration, browser operation or operator manual acceptance was performed.

Only family-history, CPP integration, relevant DI/chart/test and this report changed. No specification reassessment, risk-factor work, chart redesign, historical clinical rewrite or unrelated cleanup. Disposable LocalDB fixtures are automated implementation tests, not application-tenant operations.

## 2. Implemented data and behavior

- One new tenant-local `dbo.PatientFamilyMedicalHistory` with stable FamilyHistoryUid, patient FK, six independently supported clinical fields, Active/Archived state, trusted created/updated actors and UTC instants, RowVersion and scoped index.
- Relationship (100) and ConditionDescription (1000) are required. AgeAtOnset (100), LifeStageCode (1), Treatment (1000) and Notes (1000) are nullable. Lengths/requiredness/text age are documented engineering policies. CT-016 codes are exactly N/I/C/T/A with shared Core display labels; no inferred age, life stage or negative history.
- Blank optional input normalizes to NULL. Unknown displays Not recorded. Empty current list says **Family medical history not documented.** No age at death, optional clinical onset-date field, negative-history attestation, relative patient links, restore or physical deletion.
- Add/edit/archive use new stored procedures. Existing patient medical/surgical history, Problems, prescribing and alerts are not repurposed or changed.

## 3. Additive SQL, deployment and integrity

Migration **0075-patient-family-medical-history.sql** is appended once after 0074 in `db/tenant-clinical/manifest.json`. Manifest check: **76 unique migration entries**, every script path exists, 0075 last. Historical migration files are untouched; no EF migration introduced.

Actual procedures: `dbo.PatientFamilyMedicalHistory_List`, `_Get`, `_Create`, `_Update`, `_Archive`. List/Get verify a nondeleted patient; compound patient/resource predicates prevent foreign record disclosure. Missing patient maps to 404; wrong-patient resource returns no row/404. All connections use ITenantSqlConnectionFactory and current trusted tenant routing.

Each mutation validates active local clinical actor. Create/Update/Archive and mandatory AuditLog insertion share a transaction with XACT_ABORT and rollback-on-catch; no conditional audit bypass. Audit uses EntityName `PatientFamilyMedicalHistory`, EntityId family UID, local PatientId, UserId, Create/Update/Archive and clinical old/new JSON including NULL fields. Actual SQL error allocation after bounded collision check: 52700 missing patient, 52701 stale version, 52702 archived, 52703 inactive actor, 52704 invalid payload.

Edit/archive lock the scoped row, compare binary(8) token, require Active state, and update with the expected version predicate/one-row assertion. Malformed Base64 token is 400; stale or archived transition 409. Creation attribution stays immutable; archive changes status/update attribution only and retains clinical fields/audit. No Delete/Restore endpoints/procedures.

**Deployment prerequisite:** apply 0075 to each intended isolated clinical tenant using the established migration workflow, then deploy matching API/Web binaries. Do not run it against Auth/platform or copy records between tenants. This step did not deploy to a real tenant. Until upgraded, the optional CPP source can be unavailable and family-tab calls fail; do not interpret that as an empty or negative history.

## 4. Application/API/Web and CPP integration

Core provides exact CT-016 value labels. Application owns DTO validation, permission checks, ownership verification, service orchestration and CPP composition. Infrastructure provides typed async SQL access and ILogger failure logging. Thin controllers preserve existing server actor resolution and response conventions; no client actor/tenant/time override.

Actual API routes (all under `/api/patients/{patientUid:guid}/family-history`): GET list with `status=Active|Archived|All`, GET `/{familyHistoryUid}`, POST create, PUT `/{familyHistoryUid}` edit, POST `/{familyHistoryUid}/archive`. Web routes use `/patients/{patientUid:guid}/family-history`: GET list/single, POST create/edit/archive, with existing antiforgery protection. Reads require **Patients.View**; mutations independently require **ClinicalData.Manage** in Web/API and Application, plus the trusted active clinical actor. OIDC/token refresh configuration is reused unchanged.

Patient Chart adds **Family Medical History** tab, list/filter, Add/Edit/Archive and a Bootstrap 5 modal with the six fields, safe errors and no accidental backdrop/Escape dismissal. Read-only users can review entries without edit buttons; server denial is independent of hidden controls. Conflicts keep entered fields/token and offer explicit reload of the saved entry; no automatic overwrite. Archived rows remain visible via Archived/All without edit/restore/delete.

CPP aggregation returns a new FamilyHistory projection, active-only/top five plus total count and View all. The category is appended to the existing catalogue before History without changing the relative order/keys of old categories. Six field visibility keys integrate with the existing preference store; old settings remain valid and the new category is visible by default. Dynamic summary refresh still receives saved visibility through the existing observer. No preference backfill/store migration.

Selective CPP print reads all active family entries when selected, with six clinical fields and human-readable life-stage label, escaped values, patient ownership checks and existing print context/page numbering. Excluded categories remain excluded; unavailable family source cannot produce a misleading selected print. Preserved historical artifacts are not rewritten. Family history contributes to PC07.04 and the family-category portion of PC07.10, but not risk factors, encounter procedure/medication work or full PC07.01 closure.

## 5. Executed automated evidence

| Check | Exact result and boundary |
| --- | --- |
| Initial affected-project development build | Passed, **0 warnings / 0 errors**, using a separate output directory to avoid running-app locks. Not a full solution gate. |
| First focused family selection compile | No test execution: **five CS0118 test-alias errors**, plus one xUnit2031 test-style warning. Corrected new test aliases and the predicate assertion; no application workaround. |
| Focused family selection after correction | **35 passed / 1 failed / 0 skipped (36 total)**. All ten real-SQL cases passed; the failed CPP case used a test double returning Task instead of the chart audit's Task<Guid>. |
| Only failed CPP case after fixture correction | **1 passed / 0 failed / 0 skipped**. This resolves the failed case; overlapping selections are not added as another suite. |
| New frontend test launch with node --test | Runner failed before case execution with sandbox `spawn EPERM`; not a functional assertion result. |
| Same new frontend file run directly with node | **6 passed / 0 failed / 0 skipped**: escaping/unknowns, edit/token load, successful POST+antiforgery, conflict retention/reload, read-only controls, archive/retained view. |
| Existing preference frontend file | **6 passed / 0 failed / 0 skipped**. |
| Existing selective-print frontend file | Its single script emitted **PASS** for lazy loading, safe labels, selections, unavailable options and retry; not an invented numbered case total. |
| TypeScript | Strict project `--noEmit` passed; targeted module compilation emitted only the new JS/source-map. |
| Final relevant Release build | **Passed: 0 warnings / 0 errors**, recorded elapsed **54.50 seconds**. Referenced Core/Application/Infrastructure/API/Web/DatabaseTool/test projects, not a full solution build. |
| Final relevant Release regression gate | **106 passed / 0 failed / 0 skipped**, recorded test duration **2 seconds**, no-build/no-restore. Includes new family contracts/SQL and relevant CPP print/preferences/summary/chart-audit/history regressions. |

The ten SQL cases apply actual 0075 to minimal isolated schemas in **two disposable LocalDB databases** named `MicroEMR_Step97_<32 hex>`. Cleanup validates the generated prefix/format before dropping only those test databases. Tests cover six-field persistence/trusted actors/audit, nullable unknowns, edits/stale writes/stale archive, archive retention/archived rejection, cross-patient and separate-database isolation, missing/deleted patient/inactive actor, three injected audit rollback cases and invalid SQL life-stage rejection. No live application-tenant authorization chain is claimed from database routing fixtures.

Service/controller tests exercise effective-permission denial, no repository access on prohibited writes, foreign returned-record rejection, missing-patient 404 and no client actor fields. Attribute checks cover independent Web/API permissions and Web antiforgery; direct controller/service tests are not a full HTTP middleware or authenticated browser test. CPP tests verify five-row summary versus all active source rows, selected twelve-entry print projection, escaping, unavailable-source failure, selection exclusion and old/new preferences. Frontend checks use DOM/fetch doubles, not browser/manual acceptance. Actual rendering/page behavior reuses the existing relevant CPP print regression cases; no new visual patient-chart acceptance is claimed.

Commands actually used (development retries intentionally limited to failed/affected cases):

```powershell
# Development build (successful)
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -m:1 -o artifacts/step97/focused -p:UseAppHost=false
# Initial compile attempt and corrected focused retry used the same selection:
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -m:1 -o artifacts/step97/focused -p:UseAppHost=false --filter FullyQualifiedName~FamilyHistory --logger "trx;LogFileName=step97-focused.trx" --results-directory artifacts/step97
# Only failed test after correcting its audit double:
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -m:1 -o artifacts/step97/focused -p:UseAppHost=false --filter FullyQualifiedName~FamilyHistoryAggregationLimitsSummaryButPrintReadsAllActiveOwnedRows --logger "trx;LogFileName=step97-focused-retry.trx" --results-directory artifacts/step97
node src/MicroEMR.Web/node_modules/typescript/bin/tsc --project src/MicroEMR.Web/tsconfig.json --noEmit
node src/MicroEMR.Web/node_modules/typescript/bin/tsc src/MicroEMR.Web/ClientApp/patients/patient-family-history.ts --strict --target ES2020 --module ES2020 --skipLibCheck --sourceMap --outDir src/MicroEMR.Web/wwwroot/dist/patients
node --test tests/pc07-family-history.test.cjs # spawn restriction; no cases executed
node tests/pc07-family-history.test.cjs
node tests/pc07-cpp-display-preferences.test.cjs
node tests/pc07-selective-cpp-print.test.cjs
# One final relevant Release build and gate:
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-restore -m:1 -o artifacts/step97/release -p:UseAppHost=false
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-build --no-restore -o artifacts/step97/release --filter "FullyQualifiedName~FamilyHistory|FullyQualifiedName~CppSelectivePrintTests|FullyQualifiedName~CppDisplayPreferencesTests|FullyQualifiedName~DerivedCppSummaryFoundationTests|FullyQualifiedName~CumulativePatientProfileCertificationTests|FullyQualifiedName~PatientChartReadAuditTests" --logger "trx;LogFileName=step97-release.trx" --results-directory artifacts/step97
```

TRX evidence: `artifacts/step97/step97-focused.trx`, `step97-focused-retry.trx`, `step97-release.trx`. LocalDB/required existing renderer launches used approved execution outside sandbox. Initial compilation output and frontend runner restriction remain recorded above; do not describe every attempt as green. No unrelated test/build reruns or full API/Auth suite claim.

## 6. Complete manual acceptance guide - NOT EXECUTED

This refines Step 96 M1-M9 using actual implemented labels/routes/schema. Execute only after controlled tenant upgrade/deployment. Existing local endpoints are Web https://localhost:7002, API https://localhost:7003, Auth https://localhost:7179 if unchanged; record actual endpoints/build. Current migration must include 0075 in each tested clinical tenant. No manual test was performed by Codex.

Web base is `/patients/{patientUid}/family-history`; GET list/status or `/{familyHistoryUid}`; POST base creates, POST `/{familyHistoryUid}` edits, POST `/{familyHistoryUid}/archive` archives. API base is `/api/patients/{patientUid}/family-history`; GET list/single, POST create, PUT edit, POST archive. Capture a valid own-session request through supported developer/test tools rather than substituting credentials. All Web writes need own-session antiforgery; all edits/archives require current RowVersion.

### Shared fictional fixtures, accounts and rules

Use isolated development **Tenant A** and **Tenant B**, each selected normally through the current authenticated tenant picker. Record actual tenant UID/key, clinical database name, deployed build/migration and action UTC windows. No Auth/platform database queries. Have an authorized read-only observer for each tenant; unavailable tenant/access means DEFERRED, not bypass.

Reuse existing accounts with effective permissions; an authorized administrator may prepare them later using existing user/access workflows, without changing shared profiles. **Writer A**: Tenant A Patients.View + ClinicalData.Manage, active mapped clinical user. **Reader A**: Tenant A Patients.View with ClinicalData.Manage explicitly denied. **Writer B**: same two allowed permissions and active mapped clinical user in Tenant B. For stale-save use two browser sessions of Writer A; no unnecessary new users. Obtain actual subjects/UserIds from supported administration and read-only observer queries; don't embed tokens/credentials in evidence.

Patients: Tenant A **Avery Family-Test** (DOB 1990-01-01, phone 416-555-0120, 10 Mock Family Lane, Toronto ON M5V 0A1), **Blair Family-Test** (DOB 1992-02-02, phone 416-555-0121, 12 Mock Family Lane, same city/postal); Tenant B **Casey Family-Test** (DOB 1991-03-03, phone 416-555-0122, 14 Mock Family Lane, same city/postal). Reuse suitable fictional patients, or Patients.Edit administrator creates them at `/Patients/Create`; optional identifiers blank, required HCN only from approved non-production fixtures. Never use production records or real identifiers. Capture actual PatientUids after creation.

Primary positive entry **F** on Avery: Relationship **Mother**; Condition **TEST ONLY - type 2 diabetes**; Age at onset **45 years**; Life stage **Adult (A)**; Treatment **TEST ONLY - diet and oral therapy**; Notes **TEST ONLY - patient-reported maternal history**. Entry **U** on Blair: Relationship **Father**; Condition **TEST ONLY - hypertension**; other fields blank. Tenant B entry **B** on Casey: Relationship **Maternal aunt**; Condition **TEST ONLY - asthma**; Age **12 years**; Life stage **Child (C)**; Treatment **TEST ONLY - inhaled therapy**; Notes **TEST ONLY - Tenant B fixture**. No real clinical supply or advice occurs.

For **every scenario**, PASS requires all its expected observations; any wrong patient/tenant, silent overwrite, false negative, missing committed mutation audit or inappropriate disclosure is FAIL. Missing implementation/schema/account/permission/fixture/observer is DEFERRED for that assertion, never guessed PASS. Capture only synthetic/redacted evidence and actual IDs/windows. Retain records/audit; cleanup uses supported archive only, never SQL writes or physical deletion. Detailed per-scenario criteria follow.

### M1 — Add and retrieve family history

Prerequisites/account/tenant: shared setup, Writer A in Tenant A, Avery and observer; no existing F unless explicitly reusing a documented prior acceptance (don't repeat it).

1. Patients → Avery Family-Test → **Family Medical History** → **Add family history**.
2. Enter F exactly; Save. Record returned resource UID, new RowVersion and UTC window. Close/reopen the chart and use the same tab/filter Active.
3. Open Edit, confirm all six values, then Cancel without changes. Observer compares Tenant A row query below and Tenant A audit query below.

Expected/PASS: exactly one intended new patient-owned F, six values preserved and readable; trusted creation user/time, Create audit for the same UID, no forced patient/relative linkage. FAIL if loss/duplication from one confirmed submit, omitted treatment/age/stage, wrong actor/ownership or audit missing. DEFERRED under shared prerequisites. Cleanup: retain F for M2/M3/M5/M6/M9, no archive yet.

### M2 — Edit persistence and attribution

Prerequisites/account/tenant: M1 F and baseline capture, Writer A/Tenant A/Avery, observer.

1. Open Avery → Family Medical History → F → Edit.
2. Change Treatment only to **TEST ONLY - oral therapy reviewed**; Save. Record new token/time; reopen and cancel after verification.
3. Observer compares original UID/CreatedAt/CreatedBy, updated treatment/UpdatedAt/UpdatedBy/token and one Update audit for this successful save.

Expected/PASS: same identity/patient, only intended clinical field changed, creation attribution unchanged, new token and trusted update actor/audit. FAIL for lost unchanged fields, different patient or missing audit. DEFERRED if no baseline/observer. Cleanup: retain F for later cases.

### M3 — CPP visibility, customization and bounded print integration

Prerequisites/account/tenant: F after M2, Writer A/Tenant A/Avery; existing CPP print letterhead/clinician configured by supported admin workflow. Record original display preferences.

1. Reload chart → **Summary**. Verify Family Medical History card shows Mother, condition, recorded age/stage/treatment/notes and links to the correct full tab.
2. Customize CPP: hide only Family Medical History; Save, reload and confirm hidden. Re-enable it and its fields; Save. Record that F's clinical token/data did not change from presentation actions.
3. CPP Print: select Family Medical History and an available authorized clinician, generate output; verify six values and existing patient/clinic identity/page numbers. Generate with that category excluded and another supported category selected; F must be absent. Do not infer completed physical printing.
4. If the list has more than five active fictional family entries from an approved test fixture, confirm summary count/View all and selected print includes all; otherwise retain the volume assertion as automated evidence (seven active source rows and twelve print projection entries were tested), not an unexecuted manual PASS.

Expected/PASS: correct source/card/tab; category hide/show and selection affect presentation only; selected print includes complete available active data. FAIL for hidden-data deletion, missing selected fields or excluded content leaked. DEFERRED per unavailable print prerequisites or volume fixture. Cleanup: restore original display preferences through normal UI, retain PDF and F; do not repeat the rest of PC07.11/.13 acceptance.

### M4 — Unrecorded versus negative history

Prerequisites/account/tenant: Writer A/Tenant A, Blair initially without active family entries; observer and same route implementation.

1. Blair → Summary and Family Medical History. Confirm **Family medical history not documented**, not No family history/No hereditary disease/verified negative.
2. Add U with only relationship/condition and other controls blank. Save/reopen; confirm blanks remain unrecorded, not zero years/Adult/No treatment. SQL shows NULL optional values; UI/print label Not recorded.
3. Confirm the UI offers no verified-negative or patient-level Unknown attestation in this bounded slice. Do not enter a fake condition named None/Unknown to simulate one.

Expected/PASS: unknown remains distinct from negative and useful incomplete positive entry can persist; all six data types are supported without fabrication. FAIL for derived negative/default code. DEFERRED if initial clean fictional fixture cannot be established without changing unrelated history. Cleanup: retain U, or archive using the supported action after observations and keep its audit; archived-only list returns to Not documented, never negative.

### M5 — Archive-only retained lifecycle

Prerequisites/account/tenant: F captured after M2, Writer A/Tenant A/Avery, observer; execute after tests that need Active F, or create a separately audited same-value fixture with a new captured UID.

1. F → Archive; explicitly confirm. Record token/time.
2. Check Active list/Summary no longer treats F as current. Change filter to **Archived**, then **All**; verify original six values and identity remain readable.
3. Confirm no Edit, Restore or Delete action is offered for the archived row. If testing direct archived update with authorized own-session request replay, expect controlled 409 and unchanged row/audit.

Expected/PASS: retained Archived row, new update token/actor, Archive audit; no clinical-value rewriting or false disease resolution. No Restore is expected in this design. FAIL for hard deletion, silent editing/restoration or current-summary inclusion. DEFERRED for missing version/observer. Cleanup: leave retained archived fixture; don't run SQL to reactivate it.

### M6 — Stale-save and stale-archive rejection

Prerequisites/account/tenant: active F or separate active same-value fixture; two normal authenticated Writer A sessions in Tenant A on Avery; observer.

1. Both sessions open the same entry in Edit, recording the same initial token.
2. Session 1 changes Notes to **TEST ONLY - first editor saved**, Save; capture refreshed token/audit.
3. Session 2 changes Notes to **TEST ONLY - stale editor attempt**, Save using its loaded token. Expect 409, retained unsaved text and reload/conflict guidance. Do not allow automatic overwrite/retry.
4. With the same stale token, use an approved own-session Web request replay to the archive route with valid own-session antiforgery. Expect 409. Reload list/SQL: first saved notes and Active status persist.

Expected/PASS: one successful update/audit from session 1; no successful clinical mutation/audit from stale edit/archive; data/token remain the session-1 values. FAIL for last-writer overwrite or silent archive. DEFERRED if approved replay/observer unavailable (UI stale edit can still be recorded separately). Cleanup: keep synthetic first-editor note, close sessions; archive only later with fresh token if desired.

### M7 — Read-only permissions and direct mutation denial

Prerequisites/account/tenant: active F and own scoped endpoints captured from authorized fixture operation; Reader A in Tenant A with Patients.View and effective ClinicalData.Manage denied; observer. Do not change a shared profile.

1. Reader A opens Avery → Summary and Family Medical History. Read values; Add/Edit/Archive must be absent/disabled.
2. In Reader A's own authenticated session, replay the Web create/update/archive requests with a valid own-session antiforgery token and the fixture's IDs/payloads; expect **403** for each. No Writer A cookie/token may be reused.
3. Under the established authorized API test-client workflow, using Reader A's own existing credentials/token handling (never captured into report/logs), call API POST/PUT/archive routes; expect 403 independently of Web controls. If this tooling is unavailable, defer the direct API assertion, not fabricate it from Web denial.
4. Compare SQL before/after: same rows/tokens and no successful family Create/Update/Archive by Reader A. A user lacking Patients.View must also be denied list/get under existing authorization; exercise only if an already approved such account exists, otherwise retain that assertion deferred/automated.

Expected/PASS: allowed read is preserved; server denies all prohibited writes. FAIL for merely hidden controls with a successful direct write. DEFERRED per missing account/token workflow/observer. Cleanup: sign out; no clinical change to undo, no permission escalation.

### M8 — Cross-patient and cross-tenant resource scope

Prerequisites/account/tenant: active F in Tenant A/Avery, Blair also Tenant A; B in Tenant B/Casey created later through the same normal form by Writer B. Capture actual patient/resource/tenant UIDs/tokens and baseline SQL in both isolated databases. Writer A and Writer B each use their own normal tenant session; no production data.

1. In Tenant A, use Writer A's authorized request replay/API client: get/update/archive **F's UID under Blair's PatientUid**, preserving method/payload and own-session tokens. Expect **404**, no F/Blair mutation and no successful mutation audit.
2. Sign in/select Tenant B normally as Writer B. List Casey family history: only B/local records. Request **F's UID under Casey's PatientUid**, then repeat with Avery's foreign PatientUid; get/update/archive must return 404 without revealing F or mutating either database.
3. In Tenant A session, request B's UID under Avery and under Casey's foreign PatientUid with the same get/update/archive permutations. Expect 404. Query both tenant databases with the separate SQL below; no foreign row/clinical audit mutation.
4. If testing a caller not assigned to Tenant B, use only existing approved membership-denial procedure/account; denied selection/access is expected, not fabricated tenant switching by request IDs. No new membership-negative test is necessary to observe the valid-session resource-scope cases above.

Expected/PASS: patient/resource compound ownership and tenant-selected routing prevent disclosures/mutations in both directions. FAIL for any foreign row, overwrite or new clinical success audit from these attempts. DEFERRED for unavailable second registered tenant/account/replay; a two-store mock does not become live manual PASS. Cleanup: retain fixtures, close tenant sessions, archive only each tenant's own entries through authorized normal workflow if needed.

### M9 — Clinical audit correlation and failure semantics

Prerequisites/account/tenant: M1/M2/M5 (or equivalent separately captured fixture) records/windows, Writer A/Tenant A/Avery and observer; Tenant B observer for its B fixture if M8 ran.

1. Observer runs the Tenant A batch below (and the separate Tenant B batch for the B fixture). Match entity UID, clinical actor/AuthSubjectId, patient, action UTC windows and clinical old/new JSON to actual saves.
2. Expect one Create, one Update per actual successful edit, and Archive only when performed. Check immutable creation attribution and version changes; exclude unrelated fixture creation and security-failure rows from clinical success counts.
3. Correlate rejected stale/wrong-scope/permission attempts: no changed clinical row/token and no successful family-history Create/Update/Archive. Missing rows are not explained away as a too-small window without checking actual captures.
4. Do **not** sabotage a tenant database to test audit failure manually. Require the planned disposable-SQL automated rollback case for row+audit atomicity; if no such executed evidence exists, mark atomic-failure assertion unverified rather than claim it from ordinary successful saves.

Expected/PASS: complete successful-save correlation and preserved state on rejected requests; atomic failure supported only by separately recorded automated result. FAIL for missing/wrong clinical actor/entity/action or mutation without audit. DEFERRED if observer/window/reference missing. Cleanup: retain evidence/audit and clinical rows, no direct cleanup SQL.


## 7. Implemented-schema read-only SQL - separate tenant connections

These are actual 0075/established schema names, replacing Step 96's proposed-schema templates. Verified from migration/procedure definitions and the existing TenantDatabaseIdentity/Patient/ApplicationUser/AuditLog contracts. No query below was run against an application database or live-compiled there. Run only with authorized read access in the selected tenant's **clinical** database, not Auth/platform. Record actual IDs, actor subjects and UTC window; round window boundaries outward to whole seconds when comparing datetime2(0) audit stamps. NULL selectors are placeholders, never invented IDs.

### Tenant A - Avery or Blair, family rows, foreign UID and audit

```sql
DECLARE @TenantA uniqueidentifier, @PatientA uniqueidentifier, @FamilyUid uniqueidentifier, @ForeignUid uniqueidentifier;
DECLARE @FromUtc datetime2(7), @ToUtc datetime2(7);
SET @TenantA = NULL; -- actual Tenant A UID
SET @PatientA = NULL; -- actual Avery UID; rerun with Blair UID for U/cross-patient comparison
SET @FamilyUid = NULL; -- optional actual F/U UID; NULL returns all this patient's family audit
SET @ForeignUid = NULL; -- actual B family UID from Tenant B for M8; otherwise leave NULL
SET @FromUtc = NULL; -- actual UTC start
SET @ToUtc = NULL; -- actual UTC end
IF @TenantA IS NULL OR @PatientA IS NULL OR @FromUtc IS NULL OR @ToUtc IS NULL OR @FromUtc > @ToUtc
    THROW 50000, 'Supply actual Tenant A, patient and UTC window.', 1;
IF NOT EXISTS(SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid=@TenantA)
    THROW 50001, 'Wrong Tenant A clinical database.', 1;
SELECT DB_NAME() AS DatabaseName,TenantUid,TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,AppliedAt FROM dbo.SchemaMigration WHERE MigrationId=N'0075-patient-family-medical-history';
SELECT PatientUid,FirstName,LastName,DateOfBirth,IsDeleted FROM dbo.Patient WHERE PatientUid=@PatientA;
SELECT h.PatientUid,h.FamilyHistoryUid,h.Relationship,h.ConditionDescription,h.AgeAtOnset,
       h.LifeStageCode,h.Treatment,h.Notes,h.Status,h.CreatedAt,h.CreatedBy,cu.AuthSubjectId AS CreatedSubject,
       h.UpdatedAt,h.UpdatedBy,uu.AuthSubjectId AS UpdatedSubject,h.RowVersion
FROM dbo.PatientFamilyMedicalHistory h
LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=h.CreatedBy
LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=h.UpdatedBy
WHERE h.PatientUid=@PatientA ORDER BY h.CreatedAt,h.FamilyHistoryUid;
SELECT PatientUid,FamilyHistoryUid FROM dbo.PatientFamilyMedicalHistory WHERE FamilyHistoryUid=@ForeignUid;
SELECT l.AuditLogId,l.UserId,u.AuthSubjectId,p.PatientUid,l.ActionName,l.EntityName,l.EntityId,
       l.OldValue,l.NewValue,l.CreatedAt
FROM dbo.AuditLog l JOIN dbo.Patient p ON p.PatientId=l.PatientId
LEFT JOIN dbo.ApplicationUser u ON u.UserId=l.UserId
WHERE p.PatientUid=@PatientA AND l.EntityName=N'PatientFamilyMedicalHistory'
  AND (@FamilyUid IS NULL OR TRY_CONVERT(uniqueidentifier,l.EntityId)=@FamilyUid)
  AND l.CreatedAt>=@FromUtc AND l.CreatedAt<=@ToUtc
ORDER BY l.CreatedAt,l.AuditLogId;
```

### Tenant B - Casey, own rows, foreign UID and audit

Connect separately to Tenant B's clinical database. Substituting a B UID into an A connection is not tenant selection.

```sql
DECLARE @TenantB uniqueidentifier,@PatientB uniqueidentifier,@ForeignUid uniqueidentifier;
DECLARE @FromUtc datetime2(7),@ToUtc datetime2(7);
SET @TenantB=NULL; -- actual Tenant B UID
SET @PatientB=NULL; -- actual Casey UID
SET @ForeignUid=NULL; -- actual F family UID from Tenant A for M8; otherwise NULL
SET @FromUtc=NULL; -- actual UTC start
SET @ToUtc=NULL; -- actual UTC end
IF @TenantB IS NULL OR @PatientB IS NULL OR @FromUtc IS NULL OR @ToUtc IS NULL OR @FromUtc>@ToUtc
    THROW 50000,'Supply actual Tenant B, patient and UTC window.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid=@TenantB)
    THROW 50001,'Wrong Tenant B clinical database.',1;
SELECT DB_NAME() AS DatabaseName,TenantUid,TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,AppliedAt FROM dbo.SchemaMigration WHERE MigrationId=N'0075-patient-family-medical-history';
SELECT PatientUid,FirstName,LastName,DateOfBirth,IsDeleted FROM dbo.Patient WHERE PatientUid=@PatientB;
SELECT h.PatientUid,h.FamilyHistoryUid,h.Relationship,h.ConditionDescription,h.AgeAtOnset,
       h.LifeStageCode,h.Treatment,h.Notes,h.Status,h.CreatedAt,h.CreatedBy,cu.AuthSubjectId AS CreatedSubject,
       h.UpdatedAt,h.UpdatedBy,uu.AuthSubjectId AS UpdatedSubject,h.RowVersion
FROM dbo.PatientFamilyMedicalHistory h
LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=h.CreatedBy
LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=h.UpdatedBy
WHERE h.PatientUid=@PatientB ORDER BY h.CreatedAt,h.FamilyHistoryUid;
SELECT PatientUid,FamilyHistoryUid FROM dbo.PatientFamilyMedicalHistory WHERE FamilyHistoryUid=@ForeignUid;
SELECT l.AuditLogId,l.UserId,u.AuthSubjectId,p.PatientUid,l.ActionName,l.EntityName,l.EntityId,
       l.OldValue,l.NewValue,l.CreatedAt
FROM dbo.AuditLog l JOIN dbo.Patient p ON p.PatientId=l.PatientId
LEFT JOIN dbo.ApplicationUser u ON u.UserId=l.UserId
WHERE p.PatientUid=@PatientB AND l.EntityName=N'PatientFamilyMedicalHistory'
  AND l.CreatedAt>=@FromUtc AND l.CreatedAt<=@ToUtc
ORDER BY l.CreatedAt,l.AuditLogId;
```

**Expected values/correlation:** F retains Mother/type-2-diabetes/45 years/A/original treatment/notes after M1; M2 changes only treatment to the exact reviewed text, updates token/actor and preserves CreatedAt/By. U has Father/hypertension and NULL optional values, not fabricated codes/zeros. B belongs only to Casey in Tenant B. M5 retains Archived row/fields and Archive audit. M6/M7/M8 rejected operations must preserve prior row/token and produce no successful clinical Create/Update/Archive. For M8 capture actual nonnull foreign UIDs; a zero-row query with ForeignUid left NULL proves nothing. Inspect all patient family rows and all relevant audit events, not just one expected UID, to catch an unintended new record. Unanticipated UID collision with a legitimate local resource must be evaluated by ownership/response contents, never treated as UUID-based authorization. No migration row, empty audit result or absent baseline by itself proves acceptance.

Use separate DECLARE/SET as shown to avoid SSMS inline-variable parameterization trouble; preserve connection encryption/security. Missing 0075/schema, observer access or comparison captures means DEFERRED for affected assertions. Do not insert/update/delete through SQL to prepare or clean up clinical fixtures. Audit rollback injection was automated in disposable databases; never create a failure trigger in an application tenant for manual testing.

## 8. Files changed

The list below is the bounded implementation set; reports 128-130 are pre-existing untracked work and are not changed by this task.

- `db/tenant-clinical/manifest.json`
- `src/MicroEMR.Application/DependencyInjection.cs`
- `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs`
- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppModels.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.cs`
- `src/MicroEMR.Infrastructure/DependencyInjection.cs`
- `src/MicroEMR.Web/Controllers/PatientsController.cs`
- `src/MicroEMR.Web/Program.cs`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml`
- `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs`
- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs`
- `db/tenant-clinical/migrations/0075-patient-family-medical-history.sql`
- `src/MicroEMR.Api/Controllers/PatientFamilyHistoryController.cs`
- `src/MicroEMR.Application/PatientFamilyHistory/PatientFamilyHistoryModels.cs`
- `src/MicroEMR.Application/PatientFamilyHistory/PatientFamilyHistoryService.cs`
- `src/MicroEMR.Core/Domain/FamilyHistoryLifeStages.cs`
- `src/MicroEMR.Infrastructure/PatientFamilyHistory/PatientFamilyHistoryRepository.cs`
- `src/MicroEMR.Web/ClientApp/patients/patient-family-history.ts`
- `src/MicroEMR.Web/Controllers/PatientFamilyHistoryController.cs`
- `src/MicroEMR.Web/Services/PatientFamilyHistory/IPatientFamilyHistoryApiClient.cs`
- `src/MicroEMR.Web/Services/PatientFamilyHistory/PatientFamilyHistoryApiClient.cs`
- `src/MicroEMR.Web/Views/Patients/_FamilyHistory.cshtml`
- `src/MicroEMR.Web/Views/Patients/_FamilyHistorySummary.cshtml`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-family-history.js`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-family-history.js.map`
- `tests/MicroEMR.Api.Tests/FamilyHistoryContractTests.cs`
- `tests/MicroEMR.Api.Tests/FamilyHistorySqlTests.cs`
- `tests/pc07-family-history.test.cjs`
- `docs/certification/131-step97-pc07-01-family-history-implementation.md`

## 9. Remaining limitations, status and recommended Step 98

No implementation blocker remains in this bounded slice after the recorded gates. **Tenant deployment and M1-M9 manual verification remain outstanding.** The current client list loads the authoritative family list and renders full-tab entries; CPP cards are bounded, but no new database paging/cache is introduced. Age text is not numeric analytics; no negative assertion, optional clinical dates, restore or family pedigree. Clinical audit records old/new values under the established tenant audit policy; routine application logging does not log clinical payloads.

Status: Family Medical History **IMPLEMENTED - MANUAL VERIFICATION OUTSTANDING**; PC07.01 **PARTIAL**; Risk Factors **MISSING**; PC07.04 is not yet closed. Inventory remains **10 mandatory functional-gap IDs / three packages**, unchanged from Step 94/95. Family recording contributes to PC07.10 but does not close encounter procedures/medication summary or missing categories.

Recommended **Step 98 - Family Medical History Evidence Reconciliation**: collect the targeted user-reported manual outcomes and captures from this guide after authorized tenant deployment, reconcile any failures and decide the bounded PC07.04/family-category evidence status. Do not jump to whole-PC07.01 closure or start Risk Factors automatically. No manual repetition of accepted prescription/NKA/diagnosis/customization/print suites is requested beyond the new category's bounded integration assertions.

Preserve all prior qualifications: external OntarioMD/CPSO acceptance and Baseline/CDS-S release applicability; engineering versus mandatory source distinctions; historical Step 46 33/1/0 and manifest/test-gate limitations; limited fresh-schema SQL and mock/DOM evidence versus authenticated tenant runtime; Step 70 deferred security and deferred PC10.02 audit; qualified PC04.05/PC04.16 closures, immutable historical data and physical-print limits. No broad certification/legal opinion or new manual PASS.

Final checks: focused diff/new files, manifest uniqueness/path existence, report links/SQL names, whitespace and Git scope reviewed. Final tree: **13 modified tracked files / 21 untracked files**, including the three preserved prerequisite reports. Branch remains `feature/step-97-pc07-01-family-history`, HEAD `859789331e57b94334a68bba70a7e24975af89bd`. No staging, commit, merge, live application database mutation or manual/browser tests. Stop after Step 97 implementation and this report.
