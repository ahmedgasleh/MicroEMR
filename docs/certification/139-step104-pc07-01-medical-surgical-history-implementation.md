# Step 104 - PC07.01 Medical/Surgical History bounded implementation

Date: 2026-10-17. Implements only the History G2 slice approved in [Step 103](138-step103-pc07-01-field-date-life-stage-design.md), reusing [Step 101](136-step101-pc07-01-remaining-field-default-gap-assessment.md). **PC07.01 remains PARTIAL. Inventory remains 10 mandatory IDs / three packages.** No specification extraction or whole-category certification acceptance claim.

## 1. Actual Git checkpoint

- Started on `main`, HEAD `24329b174dad0628707e5fb5ffcd277b65f56744`.
- Starting `git -c core.fsmonitor=false status --short --untracked-files=normal` contained only untracked `docs/certification/138-step103-pc07-01-field-date-life-stage-design.md`. Preserved unchanged (SHA256 `1DDDD74BF42F2E659646406F41573CDFD4AA52B8639356F3E194D43A2ADCB390`).
- Created `feature/step-104-pc07-01-medical-surgical-history`; HEAD unchanged. Initial branch creation was denied by the filesystem sandbox; the authorized Git-only elevated retry succeeded. No automatic approval rejection, staging, commit or merge.
- Read AGENTS.md and Step 103; reused its DE07.001-.008 A102:K110 and Baseline PC07.01(b)/.03 mappings. Relevant Step 101 G2/G9 and deferred qualifications retained. Only affected History/CPP contracts and named existing test/fixture patterns were consulted. No repository-wide scan or source PDF/workbook extraction.

## 2. Implemented scope and clinical source

The existing `dbo.PatientClinicalHistory` remains the sole authority. Medical/Surgical kinds, Description, legacy RelevantDate, Active/Archived lifecycle, patient association, actors and RowVersion remain intact. No duplicate history source or new clinical table.

| Field | Medical | Surgical | Meaning |
|---|---|---|---|
| Description | Existing diagnosis/problem | Existing procedure | Existing required positive text retained |
| RelevantDate | Existing generic date | Existing generic date | Kept separately with its original label; never automatically reclassified |
| OnsetDate | Supported | Inapplicable | Nullable year/month/day parts |
| ResolutionDate | Supported | Inapplicable | Clinical resolution, independent of audit/archive timestamps |
| ProcedureDate | Inapplicable | Supported | Nullable year/month/day parts |
| LifeStageCode | Supported at onset | Inapplicable | Existing CT-016 N/I/C/T/A; unknown NULL |
| ClinicalStatus | Active/Resolved/NULL | Inapplicable | Clinician-recorded clinical state, independent of record lifecycle |
| Notes | Supported | Supported | Nullable clinical notes, maximum 1000 characters |

Mandatory product support does not make unknown patient values compulsory. No clinical status, life stage or clinical date default/backfill. An explicitly Active condition cannot simultaneously have a resolution date. A resolution date alone does not imply Resolved. Life stage is never calculated from DOB/current age or a date.

Changing kind while existing kind-specific fields are populated is rejected. The editor explains that the clinician must first clear and save those fields under the current kind, then change kind. Both actions are versioned/audited; no hidden automatic discard. Notes and legacy RelevantDate remain independently editable and retained. Archived records remain immutable and retrievable.

## 3. Tenant SQL changes and deployment

New migration **0078-clinical-history-fields**, appended once to `db/tenant-clinical/manifest.json`:

- Adds nine nullable typed date-part columns (`Onset`, `Resolution`, `Procedure` x Year/Month/Day), nullable LifeStageCode, ClinicalStatus and Notes.
- Adds a narrowly scoped SQL date-parts validation function and CHECK constraints for real supported dates, CT-016/status values, kind applicability and explicit Active/resolution contradiction.
- Replaces History List/Get/Create/Update/Archive procedures through the new migration. No existing migration or migration hash/history was rewritten; no EF migration.
- List/Get include new fields and require an undeleted patient. List ordering compares known date parts with deterministic fallback/ties; no synthetic clinical date is stored. CPP print keeps its existing deterministic RelevantDate/UID ordering.
- Create/Update accept optional trailing `@ClinicalFields` JSON as the transport for a group of typed columns. SQL validates supplied values and chronology; JSON is not a replacement clinical blob store. Old procedure callers can omit it.
- Update/Archive enforce expected RowVersion under locking, active record state and live patient/resource ownership. All mutations require an active clinical actor, and use a transaction with unconditional AuditLog insertion. Full clinical before/after JSON includes patient/resource, kind, legacy date, lifecycle, all date parts, stage/status/notes (including NULLs). Archive retains those fields.
- No UPDATE/backfill during schema upgrade; existing row tokens/clinical content and audit rows remain unchanged. Existing historical audits retain their original formats.

**No application tenant database was migrated or deployed by the agent.** Apply 0078 through the existing tenant migration runner before deploying the matching API/Web in an isolated environment. All executed SQL changes were confined to generated disposable fixture databases using the existing validated create/cleanup pattern.

## 4. Application/API and compatibility contract

Application DTOs expose nullable `ClinicalFields` with nullable OnsetDate/ResolutionDate/ProcedureDate objects, LifeStageCode, ClinicalStatus and Notes. A small Application helper validates/formats date parts and interval comparisons; the existing Core `FamilyHistoryLifeStages` helper is reused unchanged. There was no separate Core History entity to replace or duplicate.

On **update**:

- Omitted or null ClinicalFields preserves the recorded group, allowing older clients to edit legacy fields without erasing new clinical content.
- A supplied object replaces that group. `{}` explicitly clears it; fields absent within a supplied group become unrecorded. Consumers must send the complete intended group, rather than treating it as a field-level PATCH.
- The updated Web form sends an explicit group-included marker; even an entirely blank group reaches the API as an object, so deliberate clearing works.
- RelevantDate retains its existing independent full-date contract. It is never interpreted as OnsetDate or ProcedureDate and never supplies missing precision parts.

Validation runs in Application as well as API model validation and persisted SQL constraints. Controllers remain thin; existing list/create/update/archive routes and Patients.View/ClinicalData.Manage policies remain. Invalid clinical input returns 400, missing patient/resource writes return 404, stale token/archived mutation conflicts return 409. Missing required update tokens fail model validation; malformed or non-eight-byte tokens fail safely before SQL mutation. Actor resolution remains server-owned; extra payload actor fields do not control attribution.

Infrastructure owns procedure calls, JSON transport, typed parameter/read mapping and SQL-to-domain error translation. Existing DI registrations are reused. No dependencies or authentication configuration were added.

## 5. Patient Chart, CPP preferences and selective printing

- Existing History tab/modal/list/navigation remain. Medical controls show onset/resolution date parts, life stage, clinical status and notes; Surgical shows procedure date parts and notes. All unknown choices remain blank/Not recorded. Bootstrap 5 and TypeScript extend the existing flow.
- Known Year/Month/Day inputs make precision explicit without date padding. Editor reopening preserves missing month/day. Hidden inapplicable controls are disabled for submission; switching type never silently clears their current values.
- Full list displays clinical fields for both Active and retained Archived records; edit/archive affordances remain restricted to Active and manage permission. After saves/archive, the Active CPP source refreshes even when the tab is viewing All/Archived.
- Existing History CPP summary remains bounded to six with full-tab access. It displays applicable new clinical fields and keeps the legacy Relevant date label. Text and notes are escaped; clinical notes preserve line breaks.
- Category ordering/key remains History. Existing HistoryType/Description/RelevantDate preference keys survive. Six new keys: History.OnsetDate, ResolutionDate, ProcedureDate, LifeStage, ClinicalStatus, Notes. Old absent overrides leave them visible. Hide/default operations affect presentation only. Existing mutation-observer preference application handles asynchronous History rendering.
- Selected History printing reads every eligible Active row from the existing service, including explicit clinical Resolved records whose record lifecycle remains Active. Archived rows remain excluded. New fields preserve exact precision, with applicable fields selected by kind; no medical-only labels are emitted for a procedure. Legacy RelevantDate remains independent. Unknown applicable values show Not recorded.
- Selected print is independent of screen hiding, checks patient ownership, escapes clinical content and preserves existing layout/patient/clinician context. Unselected categories are not loaded just to obtain History. Retained artifacts and unrelated category projections are unchanged.

## 6. Date-precision semantics

- All parts NULL: unknown. Year only: YYYY. Year/month: YYYY-MM. All parts: real YYYY-MM-DD, including leap-year validation.
- Reject month/day without year, day without month, invalid calendar dates and unsupported years. No default day/month is persisted or returned as an event date.
- ClinicalPartialDate interval helpers compute bounds for validation only and are JSON-ignored. Those internal bounds never become stored clinical dates or output defaults.
- Reject definitely future dates under the existing History policy. A partial date is not rejected merely because some possible days are in the future; compare its earliest possible day. Resolution is invalid only when its latest possible day is before onset's earliest possible day. Overlapping partial intervals do not establish a contradiction.
- Date parts and legacy DATE values are calendar dates, never timezone-shifted. Audit timestamps remain server UTC instants. Blank clinical text normalizes to NULL; unknown is never a clinical negative or inferred state.

## 7. Security, audit, concurrency and retention

The new fields use the same patient route, tenant connection factory, real permissions, clinical actor middleware, SQL ownership checks and Web antiforgery/session token mechanism. No tenant or actor identifiers are accepted as trusted clinical request fields. No security/OIDC control was relaxed.

Real HTTP tests exercise controllers, permission handler, clinical actor resolution, Application service, repository and procedures; fixture identities/effective permission sets are controlled test inputs, not deployed OIDC acceptance. Separate tenant identity/connection-validated databases establish cross-tenant data isolation. Wrong-owner, foreign-tenant, stale and permission-denied writes leave complete clinical/audit snapshots unchanged in both stores.

Tests correlate actual patient/resource/actor/subject/time and clinical old/new values. Fault-injected AuditLog INSERT failures roll back create/update/archive rows and tokens without a successful audit. Retained archive returns the same clinical fields and rejects subsequent edits. Upgrade tests create actual legacy records/audits through old procedures, then verify unchanged tokens, content, generic dates and historical audit formats after migration.

## 8. Assertion-to-coverage matrix

Existing tests were mapped first; fake/source-only tests are not used as proof of persistence or isolation. Names below refer to `tests/MicroEMR.Api.Tests` unless noted.

| Required assertions | Existing coverage reused | Focused new/extended coverage |
|---|---|---|
| Medical/Surgical add/edit, all new fields, exact precision, all five CT-016 values, independent clinical status, notes | CumulativePatientProfileCertificationTests request/service/security/source contracts | HistoryFieldSqlTests.MedicalAndSurgicalRoundTripPrecisionStageStatusNotesAndAudit (5 real HTTP/SQL cases); direct stored readbacks and correlated audit |
| Invalid/year-only/month/full/leap dates, future, chronology, applicability and contradictions | Existing History full-date validation | InvalidDatesApplicabilityAndContradictionsLeaveRowsTokensAndAuditsUnchanged: API and direct SQL rejection, exact domain exceptions/no-change; overlapping intervals accepted without status inference |
| Legacy Active/Archived records, original RelevantDate, unchanged upgrade tokens and audits, omitted group vs clear, kind reconciliation | Existing History archive/source patterns | LegacyUpgradeOmittedGroupExplicitClearAndKindChangePreserveHistory; pre-upgrade procedures/audits and versioned old/new behavior |
| RowVersion stale/malformed/missing, role denial, patient/tenant isolation | Existing History version/service/policy contracts; established two-store acceptance fixture | DenialsStaleWritesForeignPatientsAndBothTenantDirectionsDoNotChangeClinicalState: real authenticated HTTP and complete both-store SQL/audit snapshots; missing token checked in actor case |
| Trusted actor, spoofed payload attribution, inactive/unresolved actor denied | Existing clinical actor middleware/fixture | TrustedActorCannotBeSpoofedAndUnresolvedOrInactiveActorsCannotWrite: actual subject resolution and 403/no-write |
| Transactional audit, exact actor/patient/values, rollback, retained archive/immutability | SQL 0041 audit/version pattern | Round-trip audit assertions; AuditFailureRollsBackCreateEditAndArchiveAndArchiveRetainsAllFields: real trigger fault injection and exact state/token comparison |
| CPP full selected History source, > card cap, precision, kind applicability, encoding, archive/unselected exclusions, wrong ownership | CppSelectivePrintTests existing context/failure/long-text cases | SelectedHistoryPrintUsesAllRealActiveRowsExactPrecisionAndEscapedFields uses real History persistence/service and established print composer/renderer-boundary HTML fixture |
| Old/new preference keys/default/hide/restore | Existing preference service/target test infrastructure | CppDisplayPreferencesTests.HistoryClinicalFieldsPreserveOldKeysAndDefaultNewFieldsVisible; target test updated for generated History fields |
| Web typed create/edit, legacy date/token, omit vs explicit clear | Existing authenticated API-client fixture pattern | HistoryFieldWebTests.WebClientCarriesClinicalFieldsLegacyDateAndVersionAndExplicitClear (2 kinds), real Web API-client serialization with captured HTTP |
| Actual CPP/editor TypeScript mapping, partial date display, applicability, escaping, six-row bound and generated preference targets | Existing History TypeScript/Bootstrap flow | tests/history-clinical-fields.test.cjs (3 cases), including running the emitted History client against a small DOM/fetch fixture and reopening the editor |
| Existing chart/print safeguards | Existing focused cases, no completed Family/Risk/medication scenarios repeated | Four relevant CumulativePatientProfileCertificationTests cases, OneOperationPrintsCompleteSelectedCategoriesInCatalogOrderWithRequiredContext, nine RestrictedUnavailableOrWrongContextProducesNoPdf cases, EveryExistingDisplayedFieldAndCategoryHasACustomizationTargetWithoutRemovingFullChartLinks |

SQL/security/audit/concurrency results use actual persistence. Web serializer/DOM/composer fixtures establish their specific boundary, not full deployed browser/session or physical print acceptance. Existing authorization/antiforgery attributes are preserved; no manual API/SQL/security/concurrency checklist is required.

## 9. Exact execution and results

Commands ran from repository root. No full solution build or completed domain regression suite. AGENTS.md requires focused checks but does not mandate a Release gate, so the final relevant gate uses the isolated focused build and selected regression cases below.

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step104/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q

node src/MicroEMR.Web/node_modules/typescript/bin/tsc --project src/MicroEMR.Web/tsconfig.json --noEmit
node src/MicroEMR.Web/node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --strict --skipLibCheck --sourceMap --outDir src/MicroEMR.Web/wwwroot/dist/patients src/MicroEMR.Web/ClientApp/patients/patient-clinical-history.ts
node tests/history-clinical-fields.test.cjs

dotnet vstest tests/MicroEMR.Api.Tests/bin/step104/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~HistoryField|FullyQualifiedName~CppDisplayPreferencesTests.HistoryClinicalFields|FullyQualifiedName~CumulativePatientProfileCertificationTests.MedicalAndSurgicalHistoryValidation|FullyQualifiedName~CumulativePatientProfileCertificationTests.ServicePreserves|FullyQualifiedName~CumulativePatientProfileCertificationTests.ApiAndWebUse|FullyQualifiedName~CumulativePatientProfileCertificationTests.MigrationIsPatient|FullyQualifiedName~CppSelectivePrintTests.OneOperationPrints|FullyQualifiedName~CppSelectivePrintTests.RestrictedUnavailableOrWrongContextProducesNoPdf|FullyQualifiedName~CppDisplayPreferencesTests.EveryExistingDisplayed" /Logger:"console;verbosity=normal" /Logger:"trx;LogFileName=step104-focused.trx" /ResultsDirectory:artifacts/step104

dotnet vstest tests/MicroEMR.Api.Tests/bin/step104/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~HistoryFieldSqlTests" /Logger:"console;verbosity=minimal" /Logger:"trx;LogFileName=step104-sql.trx" /ResultsDirectory:artifacts/step104

dotnet vstest tests/MicroEMR.Api.Tests/bin/step104/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~HistoryFieldSqlTests.TrustedActor|FullyQualifiedName~HistoryFieldSqlTests.InvalidDates|FullyQualifiedName~HistoryFieldSqlTests.LegacyUpgrade|FullyQualifiedName~CppDisplayPreferencesTests.EveryExistingDisplayed" /Logger:"console;verbosity=minimal" /Logger:"trx;LogFileName=step104-final.trx" /ResultsDirectory:artifacts/step104
```

- Builds ran incrementally for changed source/tests using the same isolated output. Initial source build succeeded, zero warnings/errors. One intermediate test build failed on reuse of a private CPP fixture helper and reported two xUnit2031 warnings; helper visibility and Assert.Single predicates were corrected. Subsequent builds succeeded with zero warnings/errors.
- Initial .NET run: **17 passed / 11 failed / 0 skipped**, 28 total. Ten SQL cases could not initialize LocalDB inside the filesystem sandbox (automatic-instance runtime error). One source-target test expected literal new preference attributes instead of generated ones. No SQL correctness PASS is inferred from that run.
- Authorized outside-sandbox SQL retry, confined to generated disposable databases: **10 passed / 0 failed / 0 skipped**. No clinical/application database was used or migrated.
- Final focused correction/new-assertion run: **4 passed / 0 failed / 0 skipped**. Includes one new trusted-actor case, strengthened invalid-SQL/legacy-audit assertions and corrected preference-target check. Existing passing SQL cases were not rerun merely to recreate evidence.
- TypeScript emission and project strict check exited 0. Initial Node helper test setup appended its export capture after a source-map comment; fixed the fixture newline. Final frontend results: **3 passed / 0 failed / 0 skipped**, including emitted-client DOM mapping.
- Consolidated latest outcomes: **29 distinct .NET cases passed, zero unresolved failures/skips** (14 new cases and 15 relevant reused cases). This is cumulative evidence across recorded runs, not a single 29-case execution. No completed Family History, Risk Factors or medication acceptance case was rerun.
- TRX records retained locally under ignored `artifacts/step104`; failed initial results retained for provenance. No live browser/manual verification occurred.

Final review limited selected-print labels to applicable Medical/Surgical fields. Final focused build: **succeeded, 0 warnings / 0 errors**, 21.26 seconds. Only the affected real SQL/print case reran:

```powershell
dotnet vstest tests/MicroEMR.Api.Tests/bin/step104/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~HistoryFieldSqlTests.SelectedHistoryPrintUsesAllRealActiveRowsExactPrecisionAndEscapedFields" /Logger:"console;verbosity=minimal" /Logger:"trx;LogFileName=step104-print-review.trx" /ResultsDirectory:artifacts/step104
```

**1 passed / 0 failed / 0 skipped**. Consolidated total remains 29 distinct .NET cases with passing latest outcomes; no new distinct case in this rerun. Final manifest JSON parses and contains exactly one 0078 entry.

## 10. Essential human visual checks only - USER-REPORTED PASS

Prerequisite: matching API/Web with tenant migration 0078 in an isolated test environment, an authorized test writer and configured print clinician. Fictional patient **Morgan History-Test**. These checks concern appearance; value persistence, API/SQL/permissions/audit/concurrency/isolation are automated and do not require manual repetition.

1. **Editor presentation:** Patients -> Morgan History-Test -> Patient Chart -> **Medical / Surgical History** -> **Add History**. Alternatively, on Summary, use **View Medical / Surgical History** in the **Past Medical and Surgical History** card, then **Add History**. Select Medical; Description `TEST ONLY - childhood asthma`; onset Year 2000, Month 2, Day blank; resolution Year 2010, Month/Day blank; Life stage Child; Clinical status Resolved; Clinical notes `TEST ONLY - historical note`; Relevant date blank. Save and reopen Edit. Add Surgical with Description `TEST ONLY - appendectomy`, procedure 2004/5/6 and notes `TEST ONLY - procedure note`. Inspect Add/Edit presentation at normal browser zoom. PASS: labels, known date parts, choices and feedback readable, applicable controls visible, no clipping/overlap; FAIL: obscured labels, clipped controls or confusing precision layout. Do not manually inspect SQL/audit.
2. **CPP appearance/preferences:** Morgan -> Summary -> Past Medical and Surgical History. Inspect the fixture rows and visible partial date labels. Open CPP customization, hide/show History clinical notes using the existing controls. PASS: compact History content and legacy Relevant date label remain readable, partial dates look distinct from full dates, preference controls/layout remain usable, full History navigation visible. FAIL: overlapping content, misleading date label, clipped preference controls. Exact data/eligibility/hiding semantics already have automated assertions.
3. **Selected-print appearance:** Morgan -> Summary -> Print CPP -> select existing test clinician -> select only Past Medical and Surgical History -> Print. Inspect the resulting output visually. PASS: applicable date/stage/status/notes labels and partial dates readable, no clipped/overlapping text, patient/clinician/page context intact. FAIL: unreadable field wrapping, clipped content or broken context/layout. Full-row membership and escaping are automated; do not repeat manual API/SQL assertions.

User confirmation recorded on 2026-10-17:

| Visual check | Recorded outcome |
|---|---|
| Editor presentation | USER-REPORTED PASS |
| CPP appearance/preferences | USER-REPORTED PASS |
| Selected-print appearance | USER-REPORTED PASS |

These results record the user's visual acceptance, including selected-print appearance after the horizontal label/value layout correction. No agent browser verification or additional API/SQL/security/concurrency assertion is implied. Actual deployment/migration and identity/session provenance were not independently verified. No manual API recipe or database checklist is required.

## 11. Remaining gaps, qualifications and Step 105

- This completes the implemented History field/date/stage/status contract within isolated automated evidence, with all three changed-surface visual checks USER-REPORTED PASS. Actual tenant deployment/migration and deployed identity/session provenance remain independently unverified.
- PC07.01 remains PARTIAL; **10 IDs / three packages** unchanged. Prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No ID reclassification or OntarioMD acceptance claim.
- Preserve Family M7/M8 deferred checks and prior M1-M6/M9 evidence; Risk and medication automated/user-visual evidence retain existing deployment/release qualifications. No deferred result promoted to PASS.
- Problems, Allergies, Immunizations, medication DE09 reconciliation, alert safeguards/CPP integration and PC07.10 encounter-managed procedure/medication minimum remain outside this implementation. No new encounter procedure workflow is implied by a Surgical History editor.
- Keep Baseline folder 5.5/document 1.7 and CDS-S folder 5.2 DFU/workbook v2.1 Final applicability ambiguity and external OntarioMD/CPSO/release acceptance qualifications. No source mapping was re-extracted or silently settled.
- Recommend **Step 105: bounded Problems field/date/life-stage implementation and directly required audit/RowVersion safeguards**, using Step 103 G1. Include the known encounter diagnosis fixed-column INSERT EXEC consumer compatibility and focused rollback regression. Do not include other categories or redo completed History evidence. This is a recommendation only, not work begun. Alert safeguards retain their separately identified clinical-integrity priority.

## 12. Files changed and final review

Modified:

- `db/tenant-clinical/manifest.json`
- `src/MicroEMR.Application/PatientClinicalHistory/PatientClinicalHistoryModels.cs`
- `src/MicroEMR.Application/PatientClinicalHistory/PatientClinicalHistoryService.cs`
- `src/MicroEMR.Infrastructure/PatientClinicalHistory/PatientClinicalHistoryRepository.cs`
- `src/MicroEMR.Api/Controllers/PatientClinicalHistoryController.cs`
- `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs`
- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Web/Models/PatientClinicalHistory/PatientClinicalHistoryModels.cs`
- `src/MicroEMR.Web/Services/PatientClinicalHistory/PatientClinicalHistoryApiClient.cs`
- `src/MicroEMR.Web/ClientApp/patients/patient-clinical-history.ts`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml` (History modal only)
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-clinical-history.js` and `.js.map`
- `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs`
- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs` (reusable History source/helper fixture extension)
- `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs` (register real History service/repository in existing host only; Family scenarios unchanged)

Added:

- `db/tenant-clinical/migrations/0078-clinical-history-fields.sql`
- `src/MicroEMR.Application/PatientClinicalHistory/HistoryClinicalFields.cs`
- `src/MicroEMR.Web/ClientApp/patients/history-clinical-fields.ts`
- `src/MicroEMR.Web/wwwroot/dist/patients/history-clinical-fields.js` and `.js.map`
- `tests/MicroEMR.Api.Tests/HistoryFieldSqlTests.cs`
- `tests/MicroEMR.Api.Tests/HistoryFieldWebTests.cs`
- `tests/history-clinical-fields.test.cjs`
- This report, `docs/certification/139-step104-pc07-01-medical-surgical-history-implementation.md`.

Pre-existing Step 103 report remains untracked and unchanged. Diff review checks only the intended History/CPP/test/manifest surfaces; no old migration edits, unrelated category code, staging, commit or merge. Final branch `feature/step-104-pc07-01-medical-surgical-history`, HEAD `24329b174dad0628707e5fb5ffcd277b65f56744`, unchanged. `git diff --check` reports no whitespace errors; Git's normal LF/CRLF normalization warning is informational. Final status contains the modified/added files above plus preserved untracked Step 103 only; no staged files. New-file whitespace and Step 103 hash checked separately. Test TRX/build outputs remain ignored artifacts. Stop after Step 104.


## Provisioning compatibility follow-up - 2026-10-17

User-reported `tenant provision --tenant-key local-dev` failure: SQL error 174, `The isjson function requires 1 argument(s).` The uncommitted 0078 script used `ISJSON(@ClinicalFields,OBJECT)` in Create/Update, accepted by the isolated test engine but unsupported by the user's target SQL engine. Initial green fixture results did not establish target-engine compatibility.

Corrected those two guards to single-argument ISJSON plus a separate object-root check after JSON whitespace handling. Arrays, scalars and malformed JSON still fail with 52107; valid object roots with spaces/tab/CR/LF remain accepted. No clinical semantics, audit/version safeguards, earlier migration, manifest or schema ledger was changed by this correction. This repairs the current uncommitted, failed-to-apply migration, rather than rewriting recorded tenant migration history.

Inspected TenantDatabaseMigrationRunner.ApplyMigrationAsync: all batches and the SchemaMigration insertion share one transaction; failures roll back the transaction. The reported failed 0078 application therefore does not call for manual column cleanup or migration-ledger/hash editing. Retry the same provisioning command with the corrected source. The agent did not provision or query local-dev/MicroEMR_Db; successful target deployment remains unverified.

Files touched for this follow-up only: 0078 migration, HistoryFieldSqlTests.cs and this report. Added seven real SQL Create/Update JSON-root cases (arrays, scalar/null/string, malformed JSON, whitespace-prefixed object) and reused the legacy omission/clear/kind-preservation case.

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step104/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q

dotnet vstest tests/MicroEMR.Api.Tests/bin/step104/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~HistoryFieldSqlTests.ClinicalFieldJsonRequiresAnObjectForCreateAndUpdate|FullyQualifiedName~HistoryFieldSqlTests.LegacyUpgradeOmittedGroupExplicitClearAndKindChangePreserveHistory" /Logger:"console;verbosity=minimal" /Logger:"trx;LogFileName=step104-isjson-compatibility.trx" /ResultsDirectory:artifacts/step104
```

Build succeeded, **0 warnings / 0 errors**, 23.50 seconds. Focused isolated SQL run: **8 passed / 0 failed / 0 skipped**. Local TRX: artifacts/step104/step104-isjson-compatibility.trx. Cumulative latest distinct .NET count becomes **36 passing cases**, not a single 36-case run; no previously completed domain suite rerun. Tests used the existing disposable LocalDB fixture outside the sandbox, not the user's target SQL Server. Final whitespace/scope review clean. Branch/HEAD unchanged; no commit/merge. PC07.01 PARTIAL; 10 IDs / three packages and all prior qualifications retained.


## CPP print spacing follow-up - 2026-10-17

User requested horizontal label/value alignment to save space. Updated only the CPP print definition-list styling in PatientCpp/CppPrintService.cs: fixed 42mm label column, flexible value column, 3mm column gap and 2pt row gap; removed the stacked value margin. Long values retain line breaks/wrapping and records can continue across pages. Clinical content, selection/eligibility, permissions and stored records are unchanged. This applies to the selected CPP print layout.

Added CppSelectivePrintTests.PrintedLabelsAlignWithValuesAndLongHistoryContinuesAcrossPages, which renders the existing fictional long History fixture through the real headless Chromium PDF renderer and checks label/value baseline alignment, multiple pages and retained last-line content. Reused the existing selected-category/context/escaping case.

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step104/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q

dotnet vstest tests/MicroEMR.Api.Tests/bin/step104/net10.0/MicroEMR.Api.Tests.dll /TestCaseFilter:"FullyQualifiedName~CppSelectivePrintTests.PrintedLabelsAlignWithValuesAndLongHistoryContinuesAcrossPages|FullyQualifiedName~CppSelectivePrintTests.OneOperationPrintsCompleteSelectedCategoriesInCatalogOrderWithRequiredContext" /Logger:"console;verbosity=minimal" /Logger:"trx;LogFileName=step104-horizontal-print.trx" /ResultsDirectory:artifacts/step104
```

Build succeeded, **0 warnings / 0 errors**, 37.56 seconds. **2 passed / 0 failed / 0 skipped**, including actual PDF geometry/pagination verification. Cumulative latest distinct .NET results: 37 passing cases across recorded runs. No database operation, completed domain suite, manual visual acceptance, commit or merge. Changed files for this follow-up: CPP print service, CPP selective-print tests, this report. Diff whitespace check clean. At that print-layout checkpoint, visual acceptance was pending; the subsequent user-reported PASS outcomes are recorded in section 10. PC07.01 PARTIAL and 10 IDs / three packages remain unchanged.


Documentation follow-up, 2026-10-17: recorded the user's three visual PASS outcomes in section 10 and reconciled the corresponding qualifications. Documentation only; no code/database changes, tests/builds, manual scenarios rerun, commit or merge. Other existing working-tree changes preserved. PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.
