# Step 176 — PC07.10 foundation and encounter Procedure management

2026-10-24 (America/Toronto). **Procedure foundation implemented and verified in focused isolated tests; NOT DEPLOYED.** Neutral Procedure records reuse `PatientClinicalHistory`. Open SOAP/Progress encounters can explicitly record/update procedures through an inline panel, with trusted actor, authorization, source/encounter concurrency and transactional provenance. **Medication-management actions are excluded and unimplemented. PC07.10 remains PARTIAL — IMPLEMENTED; PC07.01 PARTIAL; inventory 8 IDs / three packages unchanged.**

## 1. Authorization, branch and retained evidence

The owner explicitly stated **“I authorize Step 176 implementation”**, after the proposed step's authorization boundary was clarified. Read AGENTS.md first and inspected current schema/contracts. Started on **main**, with no tracked product modifications and existing untracked certification reports **233–247**. The requested branch did not exist; created **feature/step-176-pc07-10-encounter-procedures**, preserving all existing work. The sandbox initially prevented Git ref creation; the same explicitly authorized local branch operation succeeded with filesystem escalation. No automatic approval rejection, account change or remote operation occurred.

Reuse [Step 174 exact requirement/evidence mapping](246-step174-pc07-10-requirement-evidence-reconciliation.md) and [Step 175 design](247-step175-pc07-10-encounter-cpp-design.md). Primary Care Baseline Requirements **1.7 Final, p.29, PC07.10** requires the ability to record/update CPP categories; its guideline requires Diagnosis, Procedures and Medication Summary management from SOAP/Progress. Accepted diagnosis/category evidence remains retained. This step implements only the missing **procedure slice and its context foundation**, not whole-requirement closure.

## 2. Procedure semantics and persistence

**One authoritative patient-level store: dbo.PatientClinicalHistory.** Introduced neutral `HistoryType = Procedure`, alongside existing Medical/Surgical meanings. No new clinical table, duplicate CPP source, encounter-owned procedure collection or historical conversion. Existing Surgical records can be selected and updated without changing their kind; new records from this encounter panel are neutral Procedure records. Medical rows cannot be edited through the procedure endpoint.

The existing Description, optional partial ProcedureDate, Notes, source identity/status/actor/time and RowVersion are reused. Procedure/Surgical date applicability is preserved; Medical-only onset/resolution/life-stage/clinical-status fields remain excluded. Legacy RelevantDate remains independent and is preserved when updating an existing source row. Unknown date precision is not inferred or expanded. Future procedures/orders are outside the existing performed/history date contract.

New entries are explicit user actions, never inferred from narrative or an encounter type. CPP remains longitudinal: signing, re-opening the display or adding an amendment does not reverse clinical history changes. No Signed-to-Open transition, archive/delete shortcut or new amendment policy was introduced.

### Migration 0092

Added **db/tenant-clinical/migrations/0092-encounter-procedures.sql**, appended once to the manifest: **92 → 93 entries**.

- Extends existing History type/applicability constraints for neutral Procedure; no new columns, backfill or row updates during migration.
- Re-declares current History procedures in this new forward-only migration, preserving existing clinical validation, audit, source versions, omission semantics and retained lifecycle. No previous migration file/hash was edited.
- Adds an optional caller-supplied HistoryUid to History Create, preserving existing callers' server-generated identity behavior. The new encounter path supplies a stable GUID; the ordinary chart Create contract is unchanged.
- Adds **dbo.PatientEncounterProcedure_Save**, which owns one transaction across context checks/locks, the existing History Create/Update, mandatory clinical audit and encounter-history provenance.

Applied **only to generated disposable LocalDB databases** in the focused test fixture. No application/shared/production database was migrated or queried by this task. No EF migration.

## 3. Application, API and Web changes

| Component | Files / resulting behavior |
|---|---|
| Application context contract | **Application/PatientEncounters/EncounterProcedures.cs**: save DTO, response, service/repository contracts and service; read permission, trusted actor, ownership, field/token validation and Open-state rules |
| History contracts | **PatientClinicalHistoryModels.cs**, **HistoryClinicalFields.cs**: accept neutral Procedure with correct field applicability; existing Medical/Surgical semantics retained |
| Infrastructure | **PatientEncounters/EncounterProcedureRepository.cs**: selected-tenant stored-procedure call, typed parameters and conflict mapping; drains all results before returning success. Existing History response mapper made internal for reuse |
| API | **Controllers/EncounterProceduresController.cs**: thin authenticated GET/POST `/api/patients/{patientUid}/encounters/{encounterUid}/procedures`; actual permission and sensitive-capability metadata, safe errors, NoStore |
| Web transport | **Controllers/EncounterProceduresController.cs**, **Services/PatientEncounters/EncounterProcedureApiClient.cs**, Program registration: authenticated GET/POST `/encounter-procedures?patientUid=…&encounterUid=…`; antiforgery on POST; normal access-token forwarding, never client actor/tenant substitution |
| UI | **Views/Patients/_EncounterProcedures.cshtml**, Details composition and **ClientApp/patient-encounters/procedures.ts**: current Procedure/Surgical selector, explicit Record new, inline description/partial date/notes, save/reload and accessible status feedback |
| Shared encounter coordination | Narrow additions to **diagnoses.ts** and **contributions.ts** expose in-flight state. New panel consumes existing context/save events and blocks overlapping relevant saves/signing; existing narrative/diagnosis rules retained |
| Chart/CPP integration | Current History selector accepts Procedure; kind-specific fields and TypeScript rendering support it; source-save event refreshes existing History/CPP data without reloading encounter narrative. **CppPrintService.cs** treats Procedure like Surgical for field applicability, within the same History category/preferences |
| Registration/assets | Application/Infrastructure DI; compiled procedure JS/map plus affected existing TypeScript output/maps; no new package dependency |

The panel uses Bootstrap 5 and TypeScript and sits beside existing diagnosis/SOAP/structured Progress controls. Selecting an existing record edits the same UID; creating is an explicit separate action. Signed/restricted contexts can review current sources but cannot mutate. No nested modal stack or chart redesign.

Unsaved SOAP/structured text is not read, rewritten or submitted by procedure saves. New panel edits persist through relevant context refreshes, stale asynchronous results are ignored, and the current patient/encounter generation is checked. The UI prevents signing with pending procedure edits and overlapping source/note/diagnosis operations; server concurrency remains authoritative. Failed/uncertain saves preserve input and require explicit reload before another save. No automatic mutation retry.

## 4. Authorization, concurrency, provenance and duplicate handling

**Read:** Encounters.View and Patients.View, normal authentication, active trusted actor, correct patient/encounter and fail-closed chart-read audit.

**Mutation:** additionally Encounters.Edit and ClinicalData.Manage, valid encounter/source context and server-resolved actor. New requests contain no trusted actor or tenant field. Web validates antiforgery; API/Application enforce effective permissions independently of controls. Selected-tenant SQL factory and compound patient/resource checks retain tenant isolation.

The new SQL operation locks the live patient and matching encounter, confirms **Open** and the expected encounter RowVersion, validates active actor and owned source/state, then calls the existing audited History mutation. Existing updates independently require the reviewed History RowVersion. The context lock is held until clinical audit and provenance commit. New source writes do not modify narrative or artificially advance encounter RowVersion; later note changes still invalidate stale context normally.

New provenance uses existing **PatientEncounterHistory_Create** with **CppProcedureCreated / CppProcedureUpdated**, trusted actor, patient/encounter and source UID. The clinical **Create/Update / PatientClinicalHistory** audit retains actual before/after source data; no duplicate clinical store. Failure of domain audit or later encounter history rolls back the clinical row/version and audit. The repository consumes all result sets/errors, so a History row emitted before outer COMMIT cannot become a false success if provenance subsequently fails.

Create carries a stable HistoryUid. Within the patient/context transaction, a repeated create is accepted only when an existing neutral record and matching actor/encounter/payload creation provenance establish the same operation. It returns the recorded identity without another row/audit. Reusing the UID with unrelated provenance/payload is rejected. Updates retain the original UID and source kind. The UI also prevents concurrent double submissions and never automatically retries an uncertain write. No name-based merge or suppression of clinically legitimate repeated procedures.

The two signing-order tests exercise the actual shared encounter-row lock protocol against a concurrent Signed transition: sign first blocks/rejects the procedure; procedure first blocks the transition until source/provenance commit. They are **isolated SQL concurrency evidence**, not a claim of deployed OIDC or complete clinical signing/amendment acceptance. Existing signing code is unchanged.

## 5. Focused verification and affected regression

Mapped new assertions against accepted diagnosis, History field/SQL, CPP print and existing encounter tests before adding cases. No clinical login, browser execution, deployed clinical write or completed certification suite rerun.

| Check | Final result / scope |
|---|---|
| New **EncounterProcedureTests** and **EncounterProcedureSqlTests** | **26 PASS / 0 FAIL / 0 skipped** in final focused run. Application contracts/permissions/actor/read-audit, real two-store SQL/source round-trips, legacy compatibility, replay identity, source CPP selected-print readback, ownership, stale/Signed/Archived restrictions, late audit/provenance rollback and both signing orderings |
| New compiled-module frontend test **tests/pc07-encounter-procedures.test.cjs** | **4 behavior groups PASS**: create/update/identity/double-submit/CPP event/unsaved SOAP/save-sign guards; conflict/edit retention/no blind retry; signed/restricted/reset; stale response isolation. DOM/fetch fixture, not real Chromium |
| Existing diagnosis frontend script | **PASS** for explicit destinations, tokens, conflicts, controls and reset after the narrow busy-state addition |
| Existing **history-clinical-fields.test.cjs** | **3 PASS / 0 FAIL**, precision/applicability and actual History client/editor/preference rendering |
| TypeScript | Existing strict project compilation succeeded; subsequent procedure-module emit/check succeeded after status-focus refinement |
| Final affected .NET regression/Release build gate | **116 distinct cases have final PASS outcomes**: initial run **115 PASS / 1 FAIL**, then the single failing manifest case corrected and rerun **1 PASS / 0 FAIL**. No second full regression run |
| Builds | Core/Application/Infrastructure/DatabaseTool/API/Web/tests built successfully in isolated Debug/Release output. Running services/output remained untouched |

**142 distinct .NET cases have final PASS outcomes: 26 new + 116 affected regression.** The first focused run's 24 cases overlap the final 26 and are not added. The manifest rerun belongs to the 116, not a 143rd case. Frontend groups/scripts are separately reported, never counted as browser or .NET cases.

The final affected regression included only **EncounterDiagnosisTests**, **EncounterDiagnosisSqlTests**, **HistoryFieldSqlTests**, **CppSelectivePrintTests**, **EncounterDraftSaveTests**, **EncounterContributionTests** and **TenantDatabaseMigrationSourceTests**. Its one failure was a stale canonical-manifest assertion **expected 62**, despite the pre-step manifest already containing **92**. Updated that affected test to compare the loader's count to actual manifest entries and confirm that the Procedure operation is shipped, without tying the check to a fixed count or last entry. Other manifest validation/batch tests passed. Only that failed case was rerun; no unrelated product fix.

Evidence in ignored `.tmp/step176/`:

- `results/step176-focused-final.trx`: 26/26.
- `results/step176-affected-regression.trx`: 115/116 initial outcomes, including the recorded stale-count failure.
- `results/step176-manifest-final.trx`: that one case passes after correction.
- Isolated build output under `build/`; source/compiled-module tests retained in the repository.

Commands used the established bundled Node/TypeScript compiler and:

```powershell
dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/step176/build --ignore-failed-sources -p:NuGetAudit=false

# Focused filter: EncounterProcedureTests | EncounterProcedureSqlTests
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/step176/build --no-restore --filter 'FullyQualifiedName~EncounterProcedureTests|FullyQualifiedName~EncounterProcedureSqlTests' --logger 'trx;LogFileName=step176-focused-final.trx' --results-directory .tmp/step176/results -p:NuGetAudit=false

# Final affected gate: the seven classes listed above; Release and the same isolated output.
# After its one failure, rerun only:
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --configuration Release --artifacts-path .tmp/step176/build --no-restore --filter 'FullyQualifiedName=MicroEMR.Api.Tests.TenantDatabaseMigrationSourceTests.CanonicalManifestLoadsAndEveryScriptHasValidBatches' --logger 'trx;LogFileName=step176-manifest-final.trx' --results-directory .tmp/step176/results -p:NuGetAudit=false
```

SQL cases explicitly used **(localdb)\MSSQLLocalDB / master / Windows integrated authentication** through the existing `MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION` fixture setting. The fixtures generate validated `MicroEMR_Step69_<GUID>` database names and use the existing disposable-database cleanup convention. No shared tenant or production connection was substituted. No credentials/tokens were displayed.

## 6. Changed tests and compatibility checks

New tests: **EncounterProcedureTests.cs**, **EncounterProcedureSqlTests.cs**, **pc07-encounter-procedures.test.cjs**. The existing diagnosis SQL fixture exposes its already generated connection internally for reuse; its schema/migrations/legacy tests were not replaced. New fixture seeds genuine legacy Medical/Surgical rows and captures full values/versions/audits **before** applying 0092, then confirms unchanged snapshots. It uses two independent disposable databases for negative scope tests.

Selected CPP print reads the real newly persisted Procedure source and checks escaped content, date precision and absence of inappropriate Medical fields. Frontend tests assert that independent unsaved SOAP values remain unchanged; their controlled DOM is not deployed UI evidence. No new owner appearance or clinician acceptance is invented.

Final source/diff review confirmed no medication action, prescription mutation, old migration edit, narrative rewrite, status-reopen endpoint, physical clinical deletion or unintended product refactor. TypeScript compilation initially rewrote line endings on unrelated generated assets; their original content/line-ending convention was restored, including a byte-identical unchanged allergy asset. Final semantic/whitespace review is limited to this feature's files and generated counterparts.

## 7. Deployment prerequisites and exact future commands

**Not executed.** Deployment requires separate authorization. Confirm matching development tenant/catalog/database identity and approved backup/recovery readiness through the existing controlled process. Refresh source/manifest status and deploy **matching API/Web/generated assets** with 0092; do not expose the new panel against old procedures or treat a successful build as deployment proof. The existing DatabaseTool commands were verified from its actual command handler:

```powershell
# Read-only deployment-status preflight for the intended development tenant:
dotnet run --project src/MicroEMR.DatabaseTool -- tenant migration-status --tenant-key local-dev-fresh

# MUTATING: only after separate deployment authorization and verified target:
dotnet run --project src/MicroEMR.DatabaseTool -- tenant provision --tenant-key local-dev-fresh

# Read-only post-application status:
dotnet run --project src/MicroEMR.DatabaseTool -- tenant migration-status --tenant-key local-dev-fresh
```

Expected identity remains **local-dev-fresh / MicroEMR_LocalDev_Fresh / 192.168.50.1,50013**. Provisioning applies the verified pending manifest through the existing runner, not an ad hoc SQL script. Confirm its full pending set before authorization; do not assume only 0092 without reading that status. No command for local-dev, production or another database is authorized by this report. No service start/restart was performed.

Then perform the separate **SELECT-only fixture readiness assessment before proposing a login**, using Step 175's actual-schema queries. Confirm existing synthetic patient/Open note context, Surgical/Procedure resources and versions, active mapped actor and effective Encounters.View/Edit, Patients.View and ClinicalData.Manage. Do not infer fixture readiness from prior Family/Risk records or the owner account's name.

Only after readiness and separate explicit session/write authorization may a bounded real Chromium run create/update the approved synthetic values and correlate source/CPP/audit/versions. Normal reads/negative requests and clinical create/update counts must be individually scoped; a successful test Create/Update is an intended clinical write, unlike a denied Family POST. Retain such synthetic records by default; no unapproved cleanup.

### Forward recovery / rollback considerations

The migration is forward-only. Do not remove Procedure rows, change them to Surgical/Medical, delete the ledger row or replace old migration hashes to simulate rollback. The runner's normal transactional migration behavior and isolated upgrade tests support preservation, not a shared-environment restore claim.

If deployment fails before 0092 commits, diagnose the runner's recorded failure and verify identity/status before an authorized retry. If 0092 has committed, prefer a separately reviewed forward correction. An older binary may not validate/render/edit neutral Procedure correctly; **after Procedure records exist, blindly rolling back application binaries is unsafe**. Preserve their data and audit history and keep a compatible reader/editor. Any contingency that disables the new entry point or restores a prior application version must explicitly account for those records and is a separate deployment decision. No destructive down migration or automatic reverse-CPP action is supplied.

## 8. Remaining risks, status and Step 177

- Authenticated development UI, actual normal-session tenant/actor routing, deployed SQL/render correlation and desktop/mobile appearance for the **new procedure surface** remain **NOT VERIFIED**. No screenshots or real browser run in this step. Source/test evidence is not a substitute.
- The neutral kind is an owner-authorized engineering decision from Step 176; clinician semantic/usability review and external release/source applicability remain separate qualifications.
- The signing race tests validate the relevant SQL lock protocol. Full deployed signing/addendum/reopen behavior and previously deferred security scenarios retain their prior scope; this step does not claim every live adversarial permutation.
- Stable create identity prevents same-operation replay duplication, not universal deduplication of independent legitimate clinical procedures. A changed request with the same ID must reload; no automatic fuzzy merge or reversal.
- Matching migration/API/Web deployment and fixture/permission readiness are outstanding. No credentials, roles or memberships were altered to make testing pass.
- **Medication management from the encounter is still missing.** Prescription and Medication Summary remain separate authoritative workflows, unchanged here. No supplier, broader prescribing, billing or unrelated CPP work was started.

**PC07.10 remains PARTIAL — IMPLEMENTED.** Its procedure implementation boundary has progressed, but medication encounter management and subsequent deployed acceptance remain. PC07.01 stays PARTIAL; accepted PC07.04/.07 and DE08.008 closures are not reopened. Current tracker remains **8 IDs / three packages**: prescribing PC04.01/.06/.07/.09/.14; CPP PC07.01/.10; scheduling PC09.03. No inventory reduction.

Estimated remaining PC07.10 active engineering effort: **16–26 hours** — medication slice **8–12 h**, affected integrated verification **4–6 h**, combined readiness/deployment/authorized acceptance **3–6 h**, final evidence disposition **1–2 h**. These are planning ranges, assuming current source contracts/foundation can be reused; fixture creation, policy expansion, access/owner waiting and clinician/external decisions are excluded. Do not interpret this as an overall certification completion date.

**Proposed Step 177:** separately authorize the bounded **encounter Medication Summary management** slice using the accepted Step 175 action/source decisions and this procedure/context pattern. Keep prescription lifecycle/authority unchanged, add only missing select/record/edit/discontinue actions and focused tests. Prefer one later matching deployment/acceptance of both encounter slices. Deployment, clinical login or fixture mutation is not implicitly authorized by an implementation step. If the owner instead prioritizes procedure acceptance first, use the already documented readiness boundary before requesting any authenticated session; no new login is requested here.

## 9. Safety and stop

Current branch **feature/step-176-pc07-10-encounter-procedures** retained. Existing untracked reports 233–247 and manual work preserved. Only the feature's source/migration/generated assets/tests and this report changed. No medication implementation, shared/production database access/change, clinical login, deployed clinical write, service operation, commit, merge, push, reset, clean, stash or GitHub/remote operation. Final scope/whitespace/report checks completed. **Stop after implementation, focused verification and documentation.**
