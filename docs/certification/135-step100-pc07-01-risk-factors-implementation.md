# Step 100 — PC07.01 Risk Factors bounded implementation

Date: 2026-10-17. Implements the separately authorized minimum in [Step 99](134-step99-pc07-01-risk-factors-assessment-design.md). **Risk Factors — IMPLEMENTED; automated checks PASS; V1–V3 visual checks PASS (user-reported)**, with scope/deployment qualifications below. **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** No OntarioMD acceptance or source-release applicability resolution is claimed.

## 1. Actual Git checkpoint

Started on `main`, HEAD **6f932e6fcd15a638f60a51d4916fccfc00dc56d8**, with only untracked report 134. That report is preserved unchanged. Created **feature/step-100-pc07-01-risk-factors**; initial sandbox Git ref-lock write was denied, and the approved retry created the branch. HEAD remains unchanged. No staging, commit or merge.

Final implementation working tree: **14 modified tracked entries / 17 untracked entries**, counting directory entries as Git reports them. This includes preserved report 134. Task scope: 14 modified tracked files and 18 added files (32 files total), listed below; new Application/Infrastructure/Services directories are compressed entries in `git status --short`. Build/test artifacts are ignored. The agent performed no application database operation or manual acceptance during implementation; subsequent operator visual outcomes are recorded in section 8.

## 2. Implemented clinical scope and architecture

Four independent fields only:

- **Risk factor:** required trimmed text, maximum 200 characters.
- **Exposure details:** optional recorded text, maximum 1000.
- **Life stage at first exposure:** optional exact CT-016 N/I/C/T/A, using existing `FamilyHistoryLifeStages` mapping/validation without changing it.
- **Clinical notes:** optional independent text, maximum 1000.

Unknown optional values persist as NULL, displayed as Not recorded. Empty list/CPP says **No risk factors recorded.** This describes missing documentation, not a verified absence of clinical risk. No age/start/end dates, severity, clinical current/former enum, automatic BMI/diagnosis inference, negative attestation, separate smoking/alcohol/occupational module or migration/backfill of narrative history.

Core adds the `PatientRiskFactor` record entity. Application response DTO derives from it and adds human-readable stage/actor names; request DTOs expose only editable clinical fields and concurrency tokens. Application owns validation/permissions and returned ownership checks. Infrastructure owns typed SQL calls and ILogger error logging. API/Web controllers remain thin. All relevant operations use async/cancellation and DI. Existing OIDC/token-refresh, role/permission definitions and chart navigation are preserved.

Bounded preflight found no new authoritative risk/social-history source in the relevant paths and no use of migration 0076 or SQL errors 52800–52804. Risk records have their own source; Family History, vitals/BMI and patient medical/surgical history remain distinct and unchanged.

## 3. Tenant SQL and stored procedures

New **`db/tenant-clinical/migrations/0076-patient-risk-factors.sql`**, appended once to the tenant manifest after 0075. **This is a tenant clinical migration, not a platform repair.** No older migration or EF history changed. No platform migration added.

`dbo.PatientRiskFactor` has internal bigint identity, server-generated unique RiskFactorUid, immutable PatientUid FK, four clinical fields, Active/Archived lifecycle, trusted created/updated actor FKs and UTC timestamps, SQL RowVersion and patient/status/recent-created index. Life-stage CHECK is case-sensitive and excludes invented codes. No cascade, physical delete or restore path.

Procedures: `PatientRiskFactor_List`, `_Get`, `_Create`, `_Update`, `_Archive`.

- List/Get validate a nondeleted patient; individual reads require both patient/resource UIDs. List supports Active (default), Archived and All.
- Create checks/locks the live patient, validates positive factor/stage and active actor, generates resource identity and writes clinical row plus mandatory AuditLog Create in one transaction.
- Update/Archive use scoped UPDLOCK/HOLDLOCK reads, valid expected RowVersion, Active state, final version predicate and exactly-one-row check. Update preserves patient/UID/creation attribution; Archive changes lifecycle/update attribution only. Both write complete old/new clinical audit JSON in the same transaction.
- SQL errors: **52800** patient missing, **52801** stale version, **52802** archived transition, **52803** inactive/unavailable actor, **52804** invalid fields. Infrastructure maps these to existing typed application outcomes.
- Missing/wrong-scope resources do not disclose records. Audit insertion failure rolls back mutation and audit, never returns success.

The migration was executed successfully against generated disposable LocalDB databases by the six SQL/HTTP tests. **It has not been applied to an application tenant database.** Deploy through the established tenant migration/manifest workflow for the intended tenant after 0075; restart the updated API/Web before eventual visual verification. No manually prepared or production records were touched.

## 4. Application/API and security boundaries

Application adds `IPatientRiskFactorService`, service, repository interface and Create/Update/Archive/response contracts. Requests cannot supply actor, tenant, owner reassignment or created/updated timestamps. Responses include patient/resource identity, four values, lifecycle, attribution and Base64 version.

Actual API routes under `/api/patients/{patientUid:guid}/risk-factors`:

| Method | Suffix | Outcome |
|---|---|---|
| GET | list, optional `status=Active|Archived|All` | 200 with scoped records |
| GET | `/{riskFactorUid:guid}` | 200 or 404 |
| POST | create at collection | 201 with saved record and Get location |
| PUT | `/{riskFactorUid:guid}` | 200 refreshed record; stale/archived 409 |
| POST | `/{riskFactorUid:guid}/archive` | 200 retained archived record; stale/archived 409 |

Invalid payload/version maps to 400; wrong/missing scope to 404; authenticated permission denial to 403. No DELETE/Restore. Web routes use `/patients/{patientUid:guid}/risk-factors`, GET list/single and antiforgery-protected POST create/update/archive, following Family History's established proxy pattern.

Reads require **Patients.View**. Mutations additionally require **ClinicalData.Manage**, independently enforced at Web/API policies and Application service. Actual server actor resolution maps the authenticated subject to an active tenant-local ApplicationUser; SQL rechecks active actor. Tenant SQL factory validates assignment and database identity. Patient scope is established before clinical mutation/audit; the resource UID alone never grants authority.

Audit uses EntityName **PatientRiskFactor**, actual PatientId/UserId, resource UID EntityId, Create/Update/Archive, UTC timestamp and all four clinical fields plus Status in old/new JSON (including NULL). Successful creation metadata remains immutable; successful writes return changed versions/update actor. Denied/stale/foreign requests leave persisted values, versions and successful clinical audits unchanged. Security events are not miscounted as successful clinical mutations.

## 5. Patient Chart, CPP, preferences and print

- Added **Risk Factors** tab beside Family Medical History, without rearranging stable tabs. Read/list, Active/Archived/All, Bootstrap modal Add/Edit/Archive, escaped clinical content and trusted creation display follow existing patterns. Archived rows remain readable; mutation controls appear only for authorized Active rows.
- TypeScript retains inputs/token on 409 and offers explicit reload. Save disables in-flight resubmission; a repeated submit during a pending save is rejected. Existing antiforgery, static backdrop/Escape protection and error-feedback conventions remain.
- CPP adds `RiskFactors` from the authoritative risk service, verifies patient ownership, defaults to Active, orders CreatedAt DESC then UID, shows up to five with total additional count and View all navigation. Empty/unavailable/restricted states remain distinct; existing fail-closed chart-read audit remains.
- `CppDisplayCatalog` adds **RiskFactors** plus four field keys: `.RiskFactor`, `.ExposureDetails`, `.LifeStage`, `.Notes`. Existing saved settings keep the category visible unless explicitly hidden; FamilyHistory settings survive. Hiding is presentation only, not deletion or permission change. No new preference schema/store/backfill.
- Selected CPP print loads **all Active** risk records from source, includes all four clinical fields, escapes values and reuses existing clinician/clinic context, letterhead and page numbering. Screen hidden-field preferences do not suppress explicit selected print content. Excluded risk category is not fetched. Selected unavailable/restricted/wrong-owner source cannot yield a misleading PDF. Existing Family History output is preserved and was regression-checked.

## 6. Coverage assessment and automated matrix

Before implementation, there was no Risk Factors functional coverage. Existing Family History tests already proved reusable validation/SQL/HTTP/client patterns, while existing CPP/preference/print tests covered shared mechanisms. Those prior PASS results were not counted as risk evidence. Added only risk-specific cases and the small fixture extensions required below.

| Required assertion | Added/reused automated evidence executed in Step 100 | Result and boundary |
|---|---|---|
| 1 Add/retrieve | `RiskFactorSqlTests.HttpLifecycleCorrelatesAllClinicalAuditAndRejectsStaleAndReadOnlyWrites` | Real API create/get, four exact values, owner/actor and database audit; PASS |
| 2 Edit | Same lifecycle case | Same UID/patient and created metadata; only notes changed, new version/trusted editor; PASS |
| 3 Archive | Same lifecycle case | Retained fields, Archived listing, Active exclusion, new token/actor, archived edit/rearchive 409; PASS |
| 4 Validation/optional fields | 22 `RiskFactorContractTests` cases; `OptionalFieldsRemainNullAndInvalidSqlOrDeletedPatientCannotMutate` | Required text, bounds, all CT-016/null/invalid codes, version validation, nullable SQL values/JSON, direct invalid SQL rejection, missing/deleted patient and inactive actor; PASS |
| 5 Stale RowVersion | HTTP lifecycle | Stale update/archive 409, unchanged complete SQL/audit snapshots; PASS |
| 6 Permission denial | Contract checks + HTTP lifecycle with generated writer, reader and no-read user | Reader retains allowed read; all three writes 403; caller lacking Patients.View gets list/get 403; no SQL/audit changes; PASS |
| 7 Cross-patient | `BothTenantDirectionsAndForeignPatientReturn404WithoutAnyPersistenceOrDisclosure` | Same-tenant foreign patient GET/PUT/archive 404, no payload/UID/version disclosure; PASS |
| 8 Cross-tenant | Same two-database case | Both receiving tenant directions, local/foreign patient UID permutations; all 15 foreign requests 404; local lists exact and both complete risk/family/audit stores unchanged; PASS |
| 9 Clinical audit correlation | HTTP lifecycle plus nullable-record case | Actual patient/entity/actor/AuthSubjectId, one Create/Update/Archive, complete four-field/status JSON and SQL-clock lifecycle window, including NULLs; PASS |
| 10 Transactional rollback | `AuditFailureRollsBackRiskMutationAndAllAuditState`, Create/Update/Archive theory | Actual SQL error 52997 from disposable-only audit trigger; complete before/after risk/family/audit snapshots unchanged in both stores; PASS |
| 11 CPP visibility/source | `RiskFactorsSummaryIsBoundedWhileSelectedPrintReadsAllOwnedActiveRows`; all-category markup check; frontend cases | Five versus seven Active records, count, archive exclusion, selected full source, wrong-owner/unavailable and empty semantics, four field targets/navigation; PASS |
| 12 Preferences | `RiskFactorsCategoryDefaultsAndFourFieldPreferencesPreserveFamilySettings`; existing family settings test and all-category check | New category defaults, four keys, save/reload, preserved family configuration; PASS |
| 13 Selective print | `RiskFactorsPrintAllFourFieldsAndAllActiveRowsWithoutChangingFamilyHistory` plus source-loader case | Twelve projected entries/four fields, escaping, exclusion/no source fetch, unavailable-source failure, coexistence with family category; PASS for projection/composition, not new visual PDF acceptance |
| 14 Family History regression | Existing family print projection, family summary/full-source and family preference cases, plus mixed risk/family print | Three existing family integration cases PASS; all-category check also verifies prior fields. No deferred family scenario reopened or full Family History suite rerun. |

### Fixture reuse and evidence limits

- `RiskFactorSqlDatabase` composes existing `FamilyHistoryAcceptanceDatabase`, which composes `FamilyHistorySqlDatabase`. Real 0075/0018 setup and generated `MicroEMR_Step97_<32 hex>` database cleanup remain; new risk migration is applied only inside those databases. Two real stores, generated patients/subjects/clinical IDs and server resource IDs, no fixed application fixture IDs.
- Reused loopback Kestrel host and actual production controller, permission policies/handler, Application service, actor middleware/accessor, clinical-user lookup, tenant SQL identity checks, risk repository and procedures. Host extension registers risk service/repository and adds default-true CanRead support so no-read policy rejection can be tested; original family user behavior remains the default.
- Audit helper now accepts a parameterized entity name, defaulting to its previous family value. No family test assertions were weakened. Risk snapshots include every risk row/token, every existing family row and all clinical AuditLog rows. SQL rollback tests do not use mock repositories.
- Authentication/effective permission sets and platform assignment/context are fixture inputs, as explicitly qualified in report 132. This does not certify ordinary deployed OIDC/session selection, platform permission configuration, or browser rendering. Existing family M7/M8 qualifications remain deferred; no new manual API/SQL/security test is requested for already automated risk assertions.
- Contract tests use doubles only for validation/boundary behavior, not database isolation or transactional claims. Frontend tests use DOM/fetch doubles. Print tests verify composed HTML/projection and actual source selection, not a new pixel-level PDF run. Existing renderer/page-numbering code is unchanged.
- Hosts/connections dispose before prefix-validated disposable database cleanup. Passing fixture runs include cleanup. No test failure trigger was created in an application tenant.

## 7. Exact executed tests/build checks

Initial development command (repository root):

```powershell
node tests/pc07-risk-factors.test.cjs
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -m:1 -nr:false -p:UseAppHost=false -o artifacts/step100/focused --filter 'FullyQualifiedName~RiskFactor|FullyQualifiedName~EveryExistingDisplayedFieldAndCategoryHasACustomizationTargetWithoutRemovingFullChartLinks|FullyQualifiedName~FamilyHistoryPrintIncludesCompleteSixFieldProjectionAndRespectsSelection|FullyQualifiedName~FamilyHistoryAggregationLimitsSummaryButPrintReadsAllActiveOwnedRows|FullyQualifiedName~ExistingPreferencesPreserveDefaultsForNewFamilyCategoryAndCanHideItsFields' --logger 'trx;LogFileName=step100-focused.trx' --results-directory artifacts/step100/results
```

Initial frontend: **6 passed / 0 failed / 0 skipped**. Initial .NET build failed with one **CS0117** in new `RiskFactorContractTests` (a copied FamilyHistoryUid fixture property). Corrected to RiskFactorUid. No .NET scenario ran in that failed build; no initial acceptance PASS inferred. Output for the corrected run uses the established five-level test output directory so existing source-locator tests retain their expected repository root, and avoids the running application's locked apphost.

Corrected focused contract/CPP build and tests:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -m:1 -nr:false -p:UseAppHost=false -o tests/MicroEMR.Api.Tests/bin/step100/net10.0 --filter 'FullyQualifiedName~RiskFactorContractTests|FullyQualifiedName~CppSelectivePrintTests.RiskFactors|FullyQualifiedName~CppDisplayPreferencesTests.RiskFactors|FullyQualifiedName~EveryExistingDisplayedFieldAndCategoryHasACustomizationTargetWithoutRemovingFullChartLinks|FullyQualifiedName~FamilyHistoryPrintIncludesCompleteSixFieldProjectionAndRespectsSelection|FullyQualifiedName~FamilyHistoryAggregationLimitsSummaryButPrintReadsAllActiveOwnedRows|FullyQualifiedName~ExistingPreferencesPreserveDefaultsForNewFamilyCategoryAndCanHideItsFields' --logger 'trx;LogFileName=step100-contract-cpp.trx' --results-directory artifacts/step100/results
```

**Build succeeded. 29 passed / 0 failed / 0 skipped**, reported test duration **314 ms**. Evidence: `artifacts/step100/results/step100-contract-cpp.trx` (ignored local artifact).

Real SQL/HTTP selection, no rebuild/restore, outside sandbox under the approved LocalDB execution context (the earlier Step 97 LocalDB start restriction is already established):

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore -m:1 -nr:false -p:UseAppHost=false -o tests/MicroEMR.Api.Tests/bin/step100/net10.0 --filter 'FullyQualifiedName~RiskFactorSqlTests' --logger 'trx;LogFileName=step100-sql-http.trx' --results-directory artifacts/step100/results
```

**6 passed / 0 failed / 0 skipped**, reported duration **3 seconds**. Evidence: `artifacts/step100/results/step100-sql-http.trx`. Combined .NET results: **35 passed / 0 failed / 0 skipped** (31 new risk cases + four relevant existing integration/markup cases). No full solution build/test or full Family History acceptance run.

Strict TypeScript check and targeted module generation ran successfully before the initial frontend checks. Final review added a save-in-flight guard plus explicit empty/duplicate-submit tests; only the affected TypeScript/frontend file checks were repeated for that change:

```powershell
node src/MicroEMR.Web/node_modules/typescript/bin/tsc --project src/MicroEMR.Web/tsconfig.json --noEmit
node src/MicroEMR.Web/node_modules/typescript/bin/tsc src/MicroEMR.Web/ClientApp/patients/patient-risk-factors.ts --strict --target ES2020 --module ES2020 --skipLibCheck --sourceMap --outDir src/MicroEMR.Web/wwwroot/dist/patients
node tests/pc07-risk-factors.test.cjs
git diff --check
```

Final frontend: **8 passed / 0 failed / 0 skipped**. Both TypeScript commands passed and emitted only the new module/source-map. Manifest uniqueness and all migration-path existence checks passed. Reviewed tracked diff and new source; no newly introduced trailing whitespace (pre-existing Program.cs whitespace preserved). Final only-Razor indentation alignment was presentation whitespace, not behavior, and required no repeat test run.

No separate final Release/full regression gate was run: applicable AGENTS.md requires focused checks and contains no mandatory Release gate. The successful focused project build and recorded .NET/frontend selections cover this change; no remaining demonstrated failure justified a broader or repeat gate.

## 8. Essential human visual checks — procedure and reported results

### Operator-reported visual acceptance — 2026-10-17

The operator explicitly reported the following outcomes after receiving V1–V3. These are **user-reported manual PASS**, separate from the agent-executed automated results in section 7.

| Check | Reported observed outcome |
|---|---|
| V1 — Editor, required input and feedback appearance | **PASS:** labels, text and validation visible/readable without clipping or overlap. |
| V2 — CPP presentation and preferences appearance | **PASS:** category/field changes visually clear without disturbing Family History or adjacent cards. |
| V3 — Selected-print appearance | **PASS:** no clipped text, overlap or broken page context. |

No screenshots, PDF artifact, environment/version identifiers, actual patient/resource UIDs or deployment log accompanied this update; none are invented. Do not infer a particular multi-record/page-volume exercise from the general V3 result. The agent did not rerun tests or independently perform these visual checks. The procedures below remain as the acceptance definitions; the three reported appearance outcomes are no longer outstanding. This update does not establish deployed OIDC/platform permission behavior or close Family History M7/M8.

Prerequisites for later visual verification: deploy 0076 to the intended isolated test tenant, updated API/Web running, existing Writer A/Reader A permissions and active mapped writer, fictional **Avery Family-Test**. Do not create real clinical data. Use current normal login/tenant selection; no new permissions/profile changes needed. These are appearance checks, not requests to repeat automated persistence/API/SQL/audit/concurrency/isolation tests.

### V1 — Editor, required input and feedback appearance

1. Writer A / Tenant A → **Patients → Avery Family-Test → Risk Factors → Add risk factor**.
2. Leave factor blank and attempt Save. Expected: readable required-field feedback; modal remains usable, labels/buttons do not overlap.
3. Enter **Risk factor:** `TEST ONLY - tobacco exposure`; **Exposure details:** `TEST ONLY - patient-reported 2 cigarettes per day`; **Life stage at first exposure:** Adult; **Clinical notes:** `TEST ONLY - synthetic risk-factor fixture`. Save once.
4. Inspect the card and reopen Edit. Expected: all four independently labelled values readable, sensible spacing on the available viewport, Not recorded choice available, no age/date/severity controls. Cancel explicitly.

PASS: labels, text and validation are visible/readable without clipping or overlap. FAIL: inaccessible controls, misleading empty/negative wording, unreadable values or missing field presentation. Do not record PASS until observed. API/SQL outcomes were already automated above.

### V2 — CPP presentation and preferences appearance

1. Avery → **Summary**. Locate **Risk Factors** and **View all risk factors**. Expected: same four-value presentation, clear additional-count/navigation when applicable.
2. Use existing CPP customization to hide/show Risk Factors and its Exposure details field. Expected: intended card/field presentation changes, informative hidden-field message where applicable, full tab remains navigable.
3. Reader A opens Avery's Risk Factors tab. Visually confirm readable records and absent mutation buttons. Do not replay security requests; automated denial/state evidence already exists.

PASS: category/field changes are visually clear without disturbing Family History or adjacent cards. FAIL: wrong category hidden, clipped or missing labels, misleading risk-negative wording, or mutation controls visible for the reader. Restore original visual preferences via normal UI when finished.

### V3 — Selected-print appearance

1. Avery → existing CPP print dialog → select **Risk Factors** and an available clinician using normal configuration.
2. Open the generated PDF. Expected: Risk Factors heading, four clinical fields readable with proper wrapping; existing clinic/clinician context and page numbering retained. Explicit selection includes clinical fields even if hidden on screen.
3. If an approved multi-record synthetic fixture is already available, inspect page breaks/wrapping for it; otherwise leave that visual volume assertion unexecuted rather than manufacture PASS. Automated tests already check 12 projected entries and full-source versus five-card behavior.

PASS: no clipped text, overlap or broken page context. FAIL: missing selected risk content, unreadable long text or broken layout. Preserve historical printed artifacts; do not alter unrelated print behavior.

No manual Postman/API, SQL mutation, audit-failure trigger, stale-write or tenant-isolation exercise is needed for these visual checks. Normal-deployment integration qualifications remain distinct from fixture-authenticated automated evidence.

## 9. Deferred Family History register and remaining qualifications

| Item | Preserved status |
|---|---|
| M1–M6 | User-reported PASS; not reopened |
| M7 | Three Postman mutation-denial 403 responses, user reported; remaining preflight/database/audit/Web checks deferred to consolidated verification, not whole-scenario PASS |
| M8 | Backend automation PASS; browser/normal tenant-session checks deferred to consolidated verification, not whole-scenario PASS |
| M9 | Isolated API/SQL audit verification PASS; not reopened |

Step 100's limited Family/CPP regression cases are implementation regression evidence, not execution or closure of those deferred scenarios. Applicable CDS-S publication/release qualification from Step 99 remains. Other Step 95 CPP element gaps and PC07.10 encounter-managed diagnosis/procedure/medication-summary scope are not changed or closed. No OntarioMD acceptance claim.

Risk implementation has no unresolved code/test blocker. The essential V1–V3 appearance outcomes are now user-reported PASS; no repeat is requested. The agent's migration execution remains limited to disposable test databases; actual deployment identifiers/logs were not supplied with the operator's visual result. Do not infer deployed OIDC/platform configuration from fixture tests, or treat user-reported visual acceptance as independently inspected artifacts. **Risk Factors IMPLEMENTED with automated PASS and user-reported visual PASS, subject to these qualifications; PC07.01 PARTIAL; 10 mandatory IDs / three packages.**

## 10. Files changed

Added (18 files):

- `db/tenant-clinical/migrations/0076-patient-risk-factors.sql`
- `src/MicroEMR.Core/Domain/PatientRiskFactor.cs`
- `src/MicroEMR.Application/PatientRiskFactors/PatientRiskFactorModels.cs`
- `src/MicroEMR.Application/PatientRiskFactors/PatientRiskFactorService.cs`
- `src/MicroEMR.Infrastructure/PatientRiskFactors/PatientRiskFactorRepository.cs`
- `src/MicroEMR.Api/Controllers/PatientRiskFactorsController.cs`
- `src/MicroEMR.Web/Controllers/PatientRiskFactorsController.cs`
- `src/MicroEMR.Web/Services/PatientRiskFactors/IPatientRiskFactorApiClient.cs`
- `src/MicroEMR.Web/Services/PatientRiskFactors/PatientRiskFactorApiClient.cs`
- `src/MicroEMR.Web/Views/Patients/_RiskFactors.cshtml`
- `src/MicroEMR.Web/Views/Patients/_RiskFactorsSummary.cshtml`
- `src/MicroEMR.Web/ClientApp/patients/patient-risk-factors.ts`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-risk-factors.js`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-risk-factors.js.map`
- `tests/MicroEMR.Api.Tests/RiskFactorContractTests.cs`
- `tests/MicroEMR.Api.Tests/RiskFactorSqlTests.cs`
- `tests/pc07-risk-factors.test.cjs`
- This report.

Modified (14 files):

- `db/tenant-clinical/manifest.json`
- `src/MicroEMR.Application/DependencyInjection.cs`
- `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs`
- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppModels.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs`
- `src/MicroEMR.Infrastructure/DependencyInjection.cs`
- `src/MicroEMR.Web/Program.cs`
- `src/MicroEMR.Web/Controllers/PatientsController.cs`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml`
- `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs`
- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs`
- `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs` (shared fixture helper extensions only).

## 11. Recommended Step 101

Preserve the completed Risk Factors automated and user-reported visual outcomes; do not repeat V1–V3 or request manual API/security/audit repetitions. Retain Family History M7/M8 in the consolidated certification verification queue. Reconcile remaining Step 95 category-element qualifications as separately authorized Step 101 work; do not claim PC07.01 closure from adding this category. This follow-up changes only this report. No tests rerun, code/database changes, commit or merge.
