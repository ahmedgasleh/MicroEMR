# Step 106 - PC07.01 Problems fields, concurrency and audit implementation

Date: 2026-10-17. Implements only the bounded Problems scope approved in [Step 105](140-step105-pc07-01-problems-fields-concurrency-design.md), reusing [Step 103's mapping](138-step103-pc07-01-field-date-life-stage-design.md). **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** No complete OntarioMD acceptance claim.

## 1. Actual Git checkpoint and scope

- Started on **main**, HEAD **9cb4e68da95a1293e37b200a55f063477217745c**. Initial `git -c core.fsmonitor=false status --short --untracked-files=normal` contained only untracked `docs/certification/140-step105-pc07-01-problems-fields-concurrency-design.md`; preserved unchanged.
- Created **feature/step-106-pc07-01-problems-fields-concurrency**, HEAD unchanged. No staging, commit or merge.
- Read AGENTS.md and Step 105; consulted the relevant Step 103 Problems mapping. Reused Baseline document 1.7 p.27 PC07.01(a)/p.28 PC07.02 and CDS-S v2.1 Final Data Dictionary DE06.001-.007 A94:K101. No specification extraction or repository-wide scan.
- Extended existing Problems/encounter consumer/CPP contracts and established test fixtures. No new clinical source/table, dependency, EF migration, authentication scheme, unrelated category implementation or shared print redesign.

## 2. Implemented fields and clinical semantics

The existing `dbo.PatientProblem` remains authoritative. IDs/patient association, name/description, Active/Resolved status, legacy exact OnsetDate, resolution reason and server provenance remain intact.

| Element | Implemented contract | Unknown/legacy behavior |
|---|---|---|
| Life stage | Nullable LifeStageCode; unchanged CT-016 helper N New born, I Infant, C Child, T Adolescent, A Adult | NULL/blank = Not recorded; no age/date inference |
| Onset | Nullable year/month/day parts plus legacy exact DATE compatibility field | Existing exact dates project as exact without row backfill; unknown remains unknown; partial values never acquire invented month/day |
| Clinical resolution date | Separate nullable ClinicalResolutionDate parts recorded by Resolve | Never copied from ResolvedAt; existing resolved rows retain unknown clinical dates |
| General notes | Nullable Notes, 1000 characters, blank normalized to NULL | Distinct from ProblemDescription, ResolutionReason and encounter narrative; older clients omitting the group preserve it |
| Status/provenance | Existing explicit create Active / Resolve transition, system ResolvedAt/by and optional reason | No status inference from dates; resolved content remains retained/immutable |

M means supported product fields, not compulsory known patient values. Create has no existing RowVersion to compare; it creates and returns a new token. Existing-row edit and resolve require their exact read token.

### Precision and input compatibility

- Supported dates: unknown, YYYY, YYYY-MM, valid YYYY-MM-DD. Reuse ClinicalPartialDate and the SQL date-parts function from Step 104; validate missing-parent parts, real calendar dates and CT-016 in Application/SQL. No new speculative future-date cutoff.
- Interval validation rejects resolution only when its latest possible day is before onset's earliest possible day. Overlapping partial intervals are accepted. Internal validation bounds are JSON-ignored; clinical date parts are not timezone-shifted.
- Existing OnsetDate is retained. Rows with no new parts read its exact date without a migration UPDATE. New exact dates may populate identical parts/legacy day; partial dates store parts with legacy DATE NULL. SQL constraints prevent conflicting representations.
- Omitted/null **ClinicalFields** on edit preserves onset/stage/notes. A supplied object replaces that group; `{}` explicitly clears it. Updated Web forms send the explicit included marker, including when all controls are blank.
- Legacy scalar OnsetDate presence is tracked. Omission preserves onset; an explicit non-null exact date replaces onset while retaining other omitted clinical fields. Explicit legacy NULL clears an exact onset but rejects clearing a stored partial onset with instructions to use the updated editor. New editors submit the group instead of both representations; supplying group plus legacy onset is rejected.
- The Web API client preserves explicit legacy-null presence without adding an unset null scalar to new group requests. Standard typed JSON serialization omits a null legacy scalar; raw legacy JSON can explicitly send null, and the group `{}` is the explicit new-client clearing operation.
- Tokenless old update/resolve calls intentionally fail closed; no server-side token refresh or blind-write fallback. Deploy matching API/Web together.

## 3. Additive tenant SQL and deployment boundary

New **0079-problem-fields-concurrency.sql**, appended once to the existing tenant manifest:

- Adds eight nullable columns: OnsetYear/Month/Day, ClinicalResolutionYear/Month/Day, LifeStageCode, Notes. Date parts use SMALLINT/TINYINT; CHECK constraints enforce valid precision, stage, legacy-day consistency and no clinical resolution date on an Active row.
- No clinical UPDATE/backfill during migration. Existing row tokens/content/status/provenance and audit history remain unchanged. No existing migration, base script or SchemaMigration hash was edited.
- Replaces Problem List/Get/Create/Update/Resolve through the new migration. Reads return the added columns and join an undeleted patient. Mutations validate an active trusted actor, patient/resource ownership and trusted tenant database identity.
- Uses single-argument ISJSON plus explicit whitespace-aware object-root validation for the optional clinical-fields transport, preserving the target-engine compatibility correction from Step 104. Typed columns remain the clinical store.
- Alters the existing encounter diagnosis save procedure in the same migration to accept correlation and capture the extended Problem result.

**No application tenant database was migrated/deployed by the agent.** Apply 0079 through the existing controlled tenant migration runner before deploying the matching API/Web in an isolated environment. Do not rerun historical base scripts or change recorded migration hashes. All executed SQL tests used generated disposable fixture databases, never local-dev/MicroEMR_Db or another application tenant.

## 4. Concurrency and Application/API repair

Update/Resolve DTOs require base64 RowVersion. Infrastructure decodes exactly eight bytes; SQL separately rejects NULL tokens. Procedures lock the live patient then owned Problem, compare expected version, require Active, update with patient UID + problem UID + token and check one affected row before auditing. This lock order remains compatible with encounter saves.

The modal captures the row's token on Edit/Resolve and submits it unchanged through FormData, Web mapping, bearer API client and repository. A stale request is not silently retried. Repeat resolution or editing a terminal row also conflicts without refreshing metadata or generating another success event.

Business input validation lives in Application/request contracts, with state/ownership/date invariants rechecked atomically in SQL. Controllers remain thin and preserve existing Patients.View/ClinicalData.Manage API policies and trusted subject-to-clinical-user middleware. New exception mapping returns 400 for invalid/missing token/input, 404 for missing/foreign live resources, 409 for stale/terminal writes and safe 503 on database/audit failure. Existing authentication/actor denials remain 403.

Problems Web routes now also use existing matching Web permission attributes. Mutation affordances respect manage permission; read-only filters initialize normally. Antiforgery and bearer-token handling remain. Web relays 400/403/404/409 instead of converting them to generic 502 or calling every conflict Resolved. Conflicts retain unsaved input and instruct reload/review; no automatic overwrite.

## 5. Required transactional clinical audit

- Create/Update/Resolve unconditionally INSERT AuditLog inside the clinical transaction; absent/failed audit cannot allow clinical commit. Old conditional skip-if-table-missing behavior is removed from the active procedure definitions.
- Full JSON snapshots include all Problem columns (including precise parts, notes, reason, status, actors/timestamps and token) plus trusted tenant UID and request correlation. The existing audit columns also store patient/resource, actual actor, action, server time, success outcome, correlation and one generated AuditEventUid.
- API sets correlation from HttpContext.TraceIdentifier on JSON-ignored request context. Client-supplied actor/correlation fields cannot override attribution. Direct trusted procedure callers without correlation receive a generated server correlation. Tenant UID comes from the connection's TenantDatabaseIdentity, not payload.
- Create has NULL old snapshot; edit/resolve capture full clinical before/after. Older audit rows retain their original formats and content. No audit subsystem/schema redesign.
- Permission, actor, ownership, validation, stale/terminal or audit-failure paths produce no successful clinical mutation audit. Trigger fault injection proves row/token/audit rollback for all three writes. A separate disposable fixture temporarily renames/restores its audit table and verifies a create cannot succeed with AuditLog absent.

## 6. Encounter diagnosis result-capture compatibility

The new migration updates `PatientEncounterDiagnoses_Save`'s fixed capture from 16 to **24 columns**, matching the extended canonical Problem read/create result exactly. Original five positional create parameters remain in order. Optional trailing request correlation is generated/forwarded through the existing controller/Application DTO/repository to the nested Problem create.

Same-name Active Problem reuse still links without overwriting existing description, exact/partial onset, stage, notes or RowVersion. New Problem creation still uses the canonical audited procedure. Encounter-only saves remain separate, with existing destination-specific permissions. Existing encounter ownership/Open status/RowVersion checks and outer transaction remain; no separate child commit, swallowed error or alternative blind Problem update was introduced.

The encounter fixture now applies the new migration with its actual date-function/audit/actor/tenant-identity dependencies. Tests verify result shape, new Problem audit correlation, existing Problem field/token retention and rollback across Problem/diagnosis/encounter/history/audit destinations. Skipped-by-default SQL tests were explicitly enabled using the isolated master connection; no skipped result is called PASS.

## 7. Patient Chart and CPP integration

- Actual navigation remains **Patients -> patient -> Problems -> Add Problem/Edit/Resolve**. Existing Bootstrap modal/layout/navigation and Summary Add are extended with year/month/day inputs, life stage, clinical notes and separate optional resolution date. Unknown fields stay blank/Not recorded.
- Full table displays onset precision/stage/notes and distinguishes **Clinical resolution** from **Recorded transition** timestamp/user/reason. Resolved/All filters retain historical retrieval and no edit/resolve affordance on terminal rows.
- Active Problems CPP card keeps five-row bound/count/full-tab access and existing category/field keys. Onset uses precision-aware display; **Problems.LifeStage** is the only new preference key. Existing hidden settings survive; absent new key defaults visible; Restore defaults changes only presentation.
- Compact PatientCppProblem still has no Notes property. A JSON-ignored typed **ProblemsForPrint** section captures full Active details from the same source read used for the full compact projection. General notes are available in full chart/selected output without leaking into ordinary summary JSON.
- Selected print includes all eligible owned Active Problems, description, precision/stage and notes, independently of screen hiding. Resolved records are not silently added to this scope; their clinical dates remain retrievable in full records. Missing/unavailable print details fail closed rather than silently falling back to compact content.
- Horizontal label/value layout, escaping, long-value wrapping, patient/clinician context and other category behavior remain. A real rendered Problems PDF test verifies alignment, multiple pages and retained last-line content. Family/Risk/medication/Medical-Surgical implementation and visual evidence are preserved.

## 8. Automated assertion-to-coverage matrix

Existing tests were mapped first as required by Step 105. SQL/HTTP evidence uses production controllers, authorization handler/actor middleware, Application services, repository, tenant identity-validated connections and real procedures. Fixture authentication/role assignments are controlled test inputs, not deployed OIDC certification.

| Required assertions | Existing coverage/pattern reused | New or extended focused evidence |
|---|---|---|
| Create/edit, fields/unknown, all CT-016 values, precision, complete audit | Step 104 round-trip fixture pattern | ProblemFieldSqlTests.ApiRoundTripPreservesClinicalFieldsPrecisionAndCompleteAudit: 5 real HTTP/SQL cases, exact stored values, original narrative, changed token, trusted actor/time/patient/tenant/correlation and full old/new snapshots |
| Clinical resolution separate from system timestamp, unknown date, terminal retention | Existing Problems lifecycle | ClinicalResolutionIsSeparateFromServerTransitionAndTerminalHistoryIsRetained: actual Resolve/readback/audit and nonmutating terminal conflicts |
| Legacy exact date/audit/token, omit/clear and scalar partial-date safety | Step 104 legacy-upgrade fixture pattern | LegacyUpgradeAndOmittedFieldsPreserveValuesAndExplicitClearIsSafe: old procedures before migration, unchanged Active/Resolved tokens and old audit, exact fallback, group omission, explicit clear and protected partial onset |
| Successful current-version write; stale edit/resolve races, no duplicate success | Existing token columns only; no prior Problem token enforcement | ConcurrentEditAndResolveAllowOnlyOneCurrentVersionWrite: 4 actual concurrent HTTP combinations, one success/one conflict, exact audit count and no-change stale retries |
| Permission/actor, missing/malformed token, patient/tenant/live-patient isolation | Existing API policies/actor/two-store acceptance host | PermissionsActorsMissingTokensAndForeignScopesCannotMutateOrDisclose: authenticated preflight, no-read/read-only/inactive/unresolved/spoofed actor, both tenant directions and wrong-patient permutations, complete both-store snapshots, deleted-patient checks |
| Invalid calendar/stage/notes, dual input, chronology, direct SQL token bypass | Existing date helper and clinical validation patterns | InvalidClinicalInputsAndChronologyLeaveStateAndAuditUnchanged: API/direct SQL rejection, overlapping interval support, raw legacy NULL partial-date protection; final review adds direct old SQL Update/Resolve calls without expected version |
| Audit required/atomic and no misleading success | Step 104 fault-injection pattern | AuditFailureRollsBackEveryMutationAndNoAuditTableCannotAllowSuccess: real create/edit/resolve INSERT failures and absent audit-name rejection in a separate disposable fixture |
| Encounter capture, existing new fields/token untouched, correlation, outer rollback/ownership/concurrency | EncounterDiagnosisSqlTests | 10 executed SQL cases with new migration; reuse case strengthened with new fields/token/correlation. No-overwrite, both destinations, encounter-only, stale/signed, foreign scopes and four failure destinations |
| Encounter destination permission boundary | Existing EncounterDiagnosisTests | 7 reused service cases: EncounterOnlyIsAllowedWithoutProblemsMutationPermission plus UnauthorizedOrSignedSaveCannotReachPersistence theory. Mock service evidence is not substituted for SQL isolation |
| CPP bound/full source, stage/precision/notes, privacy/escaping/selection, real PDF | Existing source/composer/renderer fixtures | CppUsesRealFullSourcePrintsAllFieldsAndKeepsCompactNotesPrivate: real 8-row SQL source, five-row card, full single-read print, notes excluded from summary JSON, resolved/unselected exclusion, escaping and actual PDF geometry/pagination/tail |
| Preference compatibility/new key/default/restore and targets | CppDisplayPreferencesTests service/target fixtures | ProblemsLifeStagePreservesOldOnsetKeyAndSavedPreferences plus existing EveryExistingDisplayedFieldAndCategoryHasACustomizationTargetWithoutRemovingFullChartLinks |
| Web form/client token/group/legacy presence and errors | Existing Web proxy/authenticated HTTP-client patterns | ProblemFieldWebTests: 1 mapping/serialization case and 5 response-status cases; exact token in edit/resolve, explicit-null legacy transport, antiforgery attributes and retained 400/403/404/409/503 |
| Actual emitted TypeScript state/form behavior | Existing frontend VM/DOM fixture approach | tests/patient-problems-fields.test.cjs: 3 cases for exact token/partial parts, unsaved conflict input/no refresh, resolution token/date, Add reset and read-only filters |
| Existing CPP safe states/access/filter/source/audit/context | Existing focused CPP regression cases | 9 DerivedCppSummaryFoundationTests, full source once/read-audit case, 9 restricted/unavailable/ownership print cases, selected full/context case; no completed category acceptance suite repeated |

No mock-only claim is made for clinical persistence, concurrency, isolation or audit. Web serializer/DOM and composer fixtures cover their specific boundary; actual deployed session/browser/physical-print provenance remains qualified. All proposed API/SQL/security/concurrency checks are automated; none needs manual repetition.

## 9. Exact executed commands and results

Commands ran from repository root. Builds use isolated test output, not running application output. AGENTS.md requires focused checks but no final Release/full-solution gate; the focused build and affected regression gate below satisfy that policy.

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step106/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q

node src/MicroEMR.Web/node_modules/typescript/bin/tsc --project src/MicroEMR.Web/tsconfig.json --noEmit
node src/MicroEMR.Web/node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --strict --skipLibCheck --sourceMap --outDir src/MicroEMR.Web/wwwroot/dist/patients src/MicroEMR.Web/ClientApp/patients/patient-problems.ts
node tests/patient-problems-fields.test.cjs

dotnet vstest tests/MicroEMR.Api.Tests/bin/step106/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~ProblemFieldSqlTests" /Logger:"console;verbosity=minimal" /Logger:"trx;LogFileName=step106-sql.trx" /ResultsDirectory:artifacts/step106

$env:MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=15'
dotnet vstest tests/MicroEMR.Api.Tests/bin/step106/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~ProblemFieldWebTests|FullyQualifiedName~ProblemFieldSqlTests.CppUsesRealFullSource|FullyQualifiedName~EncounterDiagnosisSqlTests|FullyQualifiedName~EncounterDiagnosisTests.EncounterOnlyIsAllowedWithoutProblemsMutationPermission|FullyQualifiedName~EncounterDiagnosisTests.UnauthorizedOrSignedSaveCannotReachPersistence|FullyQualifiedName~CppDisplayPreferencesTests.ProblemsLifeStage|FullyQualifiedName~CppDisplayPreferencesTests.EveryExistingDisplayed|FullyQualifiedName~DerivedCppSummaryFoundationTests|FullyQualifiedName~CppSelectivePrintTests.FullReadLoadsOnlyRequestedSource|FullyQualifiedName~CppSelectivePrintTests.RestrictedUnavailableOrWrongContext|FullyQualifiedName~CppSelectivePrintTests.OneOperationPrints" /Logger:"console;verbosity=normal" /Logger:"trx;LogFileName=step106-focused-gate.trx" /ResultsDirectory:artifacts/step106

dotnet vstest tests/MicroEMR.Api.Tests/bin/step106/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~ProblemFieldSqlTests.InvalidClinicalInputsAndChronologyLeaveStateAndAuditUnchanged" /Logger:"console;verbosity=minimal" /Logger:"trx;LogFileName=step106-sql-token-review.trx" /ResultsDirectory:artifacts/step106
```

- Focused builds ran incrementally as implementation/tests were added: all succeeded, zero warnings/errors. Recorded durations 86.63, 14.14, 13.77 and final **12.47 seconds**. Final build **0 warnings / 0 errors**. No build/test failure occurred in this step.
- TypeScript project checks/emission exited 0. Frontend: **3 passed / 0 failed / 0 skipped**.
- Initial real Problems SQL/HTTP run: **15 passed / 0 failed / 0 skipped**. Gate: **46 passed / 0 failed / 0 skipped**, including one rerun of the CPP source case after adding actual-PDF/escaping assertions. Encounter SQL cases executed with the configured isolated master connection; none skipped.
- Final review strengthened the existing invalid-input case to prove direct SQL legacy Update/Resolve cannot omit the expected token. After the final build, only that changed case reran: **1 passed / 0 failed / 0 skipped**.
- Consolidated latest outcomes: **60 distinct .NET cases passed, zero unresolved failures/skips**, comprising 22 new cases and 38 relevant reused cases. This is cumulative evidence across the recorded runs, not a claimed single 60-case execution. Latest strengthened outcomes supersede earlier versions of the same case.
- SQL/HTTP/headless-render tests ran outside the sandbox so LocalDB/Chromium could run, against generated disposable stores/fictional data. No application database, browser/manual scenario, completed Family/Risk/medication/History acceptance suite or deployment operation was executed.
- TRX files remain local under ignored `artifacts/step106`. No test result is substituted for deployed OIDC/provider/tenant-assignment or target SQL Server release provenance.

## 10. Essential human visual checks only - PASS (user reported)

Prerequisite: matching API/Web and 0079 applied through the controlled runner in an isolated test environment; authorized test writer, configured print clinician and fictional **Morgan Problem-Test** patient. These are appearance checks only. Persistence, authorization, audit, concurrency and isolation already have automated assertions; no manual API/SQL checklist is requested.

**V1 - Editor appearance: PASS**

1. Patients -> Morgan Problem-Test -> **Problems** -> **Add Problem**.
2. Name `TEST ONLY - active childhood condition`; Description `TEST ONLY - original description`; onset Year **2000**, Month **2**, Day blank; Life stage **Child**; Clinical notes `TEST ONLY - original clinical note`. Save, then Edit that row.
3. Inspect labels/controls/spacing and known/blank date parts at normal browser zoom. PASS: clear separate description/notes, readable stage/date controls, no clipping or overlapping modal buttons. FAIL: obscured labels, clipped fields or a layout suggesting a fabricated date part. Exact stored values/token behavior are automated; no two-session concurrency exercise is required.

**V2 - Clinical date and full-record appearance: PASS**

1. In Problems, add a second row `TEST ONLY - resolved childhood condition`, onset Year **2000** only, stage blank, notes `TEST ONLY - retained resolved note`.
2. Select **Resolve** for that second row. Enter Clinical resolution Year **2010**, Month/Day blank and Resolution Reason `TEST ONLY - historical resolution`. Inspect the separate clinical-date/reason controls, then resolve.
3. Set **Show -> Resolved** (or All) and inspect the row. PASS: clinical resolution displays **2010** separately from the **Recorded transition** timestamp/user; notes/description and Not recorded life-stage text remain readable. FAIL: clipped/overlapping content or labels confusing clinical date with recorded transition. Lifecycle/retention/data correctness is automated.

**V3 - CPP preferences and selected-print appearance: PASS**

1. Morgan -> **Summary -> Active Problems**. Inspect the first active fixture's **2000-02** and Child labels, count/navigation and compact layout. In CPP customization, hide/show the Problems life-stage field using existing controls.
2. Summary -> **Print CPP** -> choose the existing configured clinician -> select **Active Problems only** -> Print.
3. PASS: card/preferences remain readable; PDF includes readable description/notes/stage/date fields with horizontal label/value alignment and intact patient/clinician/page context. FAIL: clipped fields, overlapping text or confusing partial-date layout. Full source membership/escaping/notes boundary/pagination are automated; do not manually inspect SQL/audit.

The user reported all three visual checks **PASS** after the onset-grid display correction. These are user-reported appearance results, not agent-executed checks or additional automated evidence. No manual API verification is unavoidable, so no manual request recipe or database testing is required. Existing user-reported completed-category visual PASS results remain unchanged.

## 11. Remaining gaps, qualifications and recommended Step 107

- This implements the approved Problems slice with isolated automated PASS evidence and user-reported V1-V3 visual PASS results. The visual results do not independently establish actual tenant deployment/migration provenance, target-engine execution or deployed OIDC/session/role assignments; those qualifications remain.
- Tokenless old clients are deliberately rejected. General group saves replace the group; clients must send the complete intended group. Legacy exact/partial compatibility and terminal immutability are explicit; no historical correction/reopen workflow was added.
- **PC07.01 PARTIAL; 10 mandatory IDs / three packages** unchanged: prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No whole-requirement promotion.
- Preserve Family M1-M6/M9 evidence and M7/M8 deferrals, Risk V1-V3, medication classification visual checks and all three Medical/Surgical visual PASS results. None rerun/promoted. Other unresolved category and PC07.10 encounter-procedure/medication-summary obligations remain outside scope.
- Preserve Baseline folder 5.5/document 1.7 and CDS-S folder 5.2 DFU/workbook v2.1 Final applicability ambiguity, external OntarioMD/CPSO acceptance and earlier deployment/release qualifications.
- Recommend **Step 107: bounded Allergies field/date/life-stage and recorded-date source assessment**, reusing Step 103's confirmed conditional DIN/onset/stage findings and preserving explicit NKA. Do not begin that implementation or reopen completed Problems/History evidence here. Alert safeguards retain their separately identified clinical-integrity priority.

## 12. Files changed and final review

Modified:

- `db/tenant-clinical/manifest.json`
- `src/MicroEMR.Api/Controllers/PatientProblemsController.cs`
- `src/MicroEMR.Api/Controllers/EncounterDiagnosesController.cs`
- `src/MicroEMR.Application/PatientProblems/Contracts/PatientProblemResponse.cs`
- `src/MicroEMR.Application/PatientProblems/Services/PatientProblemService.cs`
- `src/MicroEMR.Application/PatientEncounters/EncounterDiagnoses.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppModels.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs`
- `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs`
- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Infrastructure/PatientProblems/PatientProblemRepository.cs`
- `src/MicroEMR.Infrastructure/PatientEncounters/EncounterDiagnosisRepository.cs`
- `src/MicroEMR.Web/Controllers/PatientProblemsController.cs`
- `src/MicroEMR.Web/Models/PatientProblems/PatientProblemModels.cs`
- `src/MicroEMR.Web/Services/PatientProblems/PatientProblemApiClient.cs`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml` (Problems card/table/modals only)
- `src/MicroEMR.Web/ClientApp/patients/patient-problems.ts`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-problems.js` and `.js.map`
- `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs`
- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs` (fixture print-details contract)
- `tests/MicroEMR.Api.Tests/EncounterDiagnosisSqlTests.cs` (new migration dependencies and stronger existing-row preservation/correlation assertions)
- `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs` (register real Problems service/repository in reusable host; completed scenarios unchanged)

Added:

- `db/tenant-clinical/migrations/0079-problem-fields-concurrency.sql`
- `src/MicroEMR.Application/PatientProblems/Contracts/ProblemClinicalFields.cs`
- `src/MicroEMR.Application/PatientProblems/PatientProblemWriteExceptions.cs`
- `tests/MicroEMR.Api.Tests/ProblemFieldSqlTests.cs`
- `tests/MicroEMR.Api.Tests/ProblemFieldWebTests.cs`
- `tests/patient-problems-fields.test.cjs`
- This report, `docs/certification/141-step106-pc07-01-problems-fields-concurrency-implementation.md`.

Pre-existing untracked Step 105 report preserved. Final review confirmed only these intended surfaces plus that preserved report; branch `feature/step-106-pc07-01-problems-fields-concurrency`, HEAD `9cb4e68da95a1293e37b200a55f063477217745c`, unchanged. Manifest JSON parses with exactly one 0079 entry. `git diff --check` and separate new-file whitespace checks are clean; Git's LF/CRLF normalization warning is informational. Historical base Problem script, 0069 and 0078 have no diff; no staged files. No commit or merge. Stop after Step 106.

### Follow-up: Problems grid onset display

The reported onset value appeared in Edit but not in the Problems grid because the grid still rendered only the legacy full `OnsetDate`. Partial onset dates intentionally leave that legacy field null. The grid now uses the existing `PatientProblemViewModel.OnsetDisplay`, displaying the recorded precision (`YYYY`, `YYYY-MM` or `YYYY-MM-DD`), falling back to legacy exact dates and showing `Not recorded` for missing dates. Only the grid binding changed; no database or clinical record changes are needed. Existing working-tree changes are preserved.

Validation: the test-project build (including the Web Razor views) passed with zero warnings/errors; all six existing `ProblemFieldWebTests` and all three `patient-problems-fields.test.cjs` tests passed. These existing tests check surrounding form/client behavior; browser appearance was not manually verified. `git diff --check` passed. PC07.01 remains PARTIAL, with 10 mandatory IDs / three packages; no commit or merge.

Subsequent user acceptance: V1 Editor appearance, V2 Clinical date and full-record appearance, and V3 CPP preferences and selected-print appearance were all reported PASS. Section 10 records this acceptance, superseding the earlier pending visual status. This documentation update runs no tests/builds and changes no application code or database. PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages. No commit or merge.
