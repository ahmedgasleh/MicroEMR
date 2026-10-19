# Step 114 - PC07.01 Chart Alert safeguards and CPP integration

Date: 2026-10-18. Continues [Step 113](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md), reusing Step 101 G7/G8 and retained DE13 mapping. **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** Automated evidence is separate from user-reported visual acceptance and release/deployment qualification.

## 1. Actual Git checkpoint

- Started on main, HEAD `c48a820be77318b8f8e1401a6e6d6cbda1522f7a`.
- Initial tree: modified Step 112 report and untracked Step 113 report. Preserved exactly: Step 112 SHA256 `42C40F9A5C6910E62304205977EA885E5371040372749B88B8B568B3241056C7`; Step 113 SHA256 `667975A87CEA74702D29DDFCFAF1E6105D330634C40EEF1298AD7413A4E383D8`.
- Created `feature/step-114-pc07-01-chart-alert-safeguards`. HEAD unchanged; no staging, commit or merge. Read AGENTS.md first. No specification extraction, external research, repository-wide scan or unrelated clinical implementation.

### Visual acceptance closure checkpoint - 2026-10-19

- Actual branch: `main`; HEAD: `6f381a8fa488bb167e12c33e5a88596a4e4ddf64`. The implementation checkpoint above is historical.
- Initial working tree: modified `AGENTS.md` only; preserved without edits. This closure changes only this report. No commit or merge.

## 2. Existing meaning and bounded implementation

Existing PatientChartAlert identity/patient association, AlertTitle/AlertMessage, General/Clinical/Administrative/Safety/Scheduling type, Low/Normal/High/Critical priority, Active/Resolved status and resolution attribution/reason remain. There is **no Archive API/status** in the existing workflow. The requested retained-close safeguards protect **Resolve**, without creating another lifecycle or physically deleting data.

Retained authority: PC07.01(h)/PC07.08; DE13.001/.002, Data Dictionary A203:K207, mandatory description/notes. Existing title/message are reused as that text. They can record alert or special-need information; a general Chart Alert is not automatically a verified special-needs assessment. No new clinical fields, dedicated SpecialNeeds type/table, optional clinical dates or life-stage vocabulary. Priority remains a recorded Alert value, with no inferred severity or Results interpretation.

Introduced an Application Alert service for validation/ownership and DTO contracts, using the existing Infrastructure repository and DI. Controllers remain authorized thin transport boundaries. All mutations still use stored procedures.

## 3. SQL, audit and concurrency

Additive tenant migration **0082-chart-alert-safeguards.sql**, one manifest entry, replaces the five existing Alert read/create/update/Resolve procedures. No table columns, clinical backfill, existing migration or old audit changes.

- List/detail join a live patient; missing patient/list is not empty clinical data. Detail scopes patient+alert UID. Mutations lock the live parent patient and owned Alert consistently.
- Every write requires a valid tenant-store identity and active tenant clinical actor. API actor comes from ClinicalUserActorContext; correlation is server HttpContext.TraceIdentifier and DTO correlation is JsonIgnored. SQL receives no client tenant identity authority.
- Create initializes its normal SQL RowVersion. Update/Resolve require the exact reviewed eight-byte token, check it atomically under locks and require one affected Active row. Application/Infrastructure validate base64 length; direct SQL VARBINARY(MAX) parameters reject missing, short and long tokens without pre-validation truncation.
- Update cannot edit a Resolved record. A current-token repeated Resolve is explicitly a no-op: no token/time/reason/verifier change and no duplicate mutation audit. A stale repeated Resolve conflicts. Original resolution content is retained. No silent token refresh/retry or reactivation workflow.
- Mandatory AuditLog inserts persist complete before/after Alert rows including token/clinical fields/created/updated/resolution provenance, tenant, patient, actor, action, server correlation and existing structured audit metadata. Create before is NULL. Actions are ChartAlertCreated/Updated/Resolved. Every actual mutation and audit is one transaction with XACT_ABORT/rollback. Audit failure, missing audit storage and outer caller rollback cannot leave a successful mutation or false success event.
- SQL errors translate to Application validation/not-found/concurrency/authorization exceptions and API 400/404/409/403; unavailable persistence returns safe 503. Existing type/priority fallback to General/Normal is retained, not a new clinical default.

## 4. Authorization, patient-context reads and Web

API preserves Patients.View and ClinicalData.Manage write policies. Dedicated List/Get validate resource ownership before successful chart-read audit, and return no clinical data if required read-audit persistence fails. Missing/foreign resources generate no successful open audit. Trusted tenant connection selection and existing denial-audit middleware conventions remain unchanged.

Web now requires matching read/manage permissions and preserves antiforgery. Read-only users can view history; Add is disabled, Edit/Resolve omitted, and recorded Active status still displays Active. Scoped client URLs remain patient-bound. Denied/missing/conflicting/unavailable responses preserve their HTTP status and no denied read becomes an empty list.

TypeScript carries the reviewed row token in Edit and Resolve forms. Inline conflict feedback retains unsaved fields/reason/token; only a successful mutation reloads. Missing token cannot initiate an edit/Resolve POST. Failed list loads show unavailable/denied feedback rather than a blank or negative finding. User-provided text is escaped. Generated JS/map accompany the TypeScript.

## 5. CPP placement, preferences and selected print

- Added **Alerts / Chart Alerts** to the existing CPP catalogue after existing categories; their relative ordering/keys remain. The matching card is appended after existing cards, retains header badge and full Alerts tab/navigation, and displays up to five Active rows ordered by recorded priority/time/UID, with count/navigation.
- Mandatory description/notes come from the existing title/message. The card also shows recorded type/priority. Empty says **No active chart alerts recorded**, not No special needs. Unavailable remains unavailable; unknown notes are Not recorded.
- New preference targets: Alerts.Description, Alerts.Notes, Alerts.Type, Alerts.Priority. Existing stored keys/values are retained; new keys default visible; hide/show and default restoration remain configuration-only.
- Selected Alert printing loads all owned rows before Active filtering, includes all eligible rows without the five-card cap, and projects description, notes, type, priority, status. Unselected Alert source is not read/printed. Screen hiding cannot suppress explicitly selected content. No separate print storage or duplicated clinical records.
- Ownership is checked before filtering, including foreign Resolved rows. Unavailable/foreign selected data fails closed. HTML escaping and actual multipage PDF tail content are covered. Existing shared patient/clinician/layout/page-numbering architecture remains.
- CPP uses its existing shared patient-chart audit boundary; dedicated Alert HTTP reads additionally enforce resource-owner-before-success-audit. The new loader is in PatientCppService.Alerts.cs, matching the existing partial-service structure. Missing source fails unavailable rather than implying an empty clinical finding.

## 6. Automated coverage matrix

Existing shared CPP preference/full-source/denial/PDF fixtures were inspected and reused. The new SQL fixture uses real controllers/policies/actor resolution, tenant connections, Application service/repository and actual stored procedures across two disposable stores; authentication/session inputs are controlled fixture identities, not deployed OIDC proof.

| Acceptance area | Executed focused coverage | Evidence boundary |
|---|---|---|
| Create/edit/retained Resolve, full snapshots/actor/correlation/token, no-op and terminal guard | RealHttpLifecycleRequiresCompleteAtomicAuditsAndPreservesResolutionHistory | Actual HTTP/SQL/audit reconstruction |
| Stale edit, stale Resolve, edit/edit, edit/Resolve, Resolve/Resolve | Three ConcurrentEditsAndResolve cases plus lifecycle assertions | Real concurrent HTTP/SQL; exactly one current-version mutation |
| Required mutation audit/rollback | Three RequiredAuditFailure cases, missing table and outer rollback | Actual INSERT-trigger faults, no state/token/success-audit change |
| Permissions, patient/tenant ownership and live patient | ScopedReadsWritesPermissionsAndTwoTenantDirections... | Two stores/both directions, reader/no-read denials, wrong/deleted patient; complete clinical/audit snapshots |
| Active/unresolved/spoofed actor, trusted identity | InactiveUnresolvedAndSpoofedActors... | Real SQL + HTTP; client actor/correlation cannot override attribution |
| Direct SQL/invalid input/token bypass, legacy compatibility | ValidationDirectSqlTokens... and AdditiveUpgrade... | Missing/short/long token, bad HTTP base64, title/message bounds, legacy row/version/audit retained |
| Resource ownership/read-audit ordering and audit failure | SQL scoped/read-audit fault cases; two faulty-source controller cases | No success audit/disclosure for rejected reads |
| Web scoped transport, permissions/antiforgery/status/no retry | Ten ChartAlertWebTests cases | Actual HTTP client serialization plus controller boundary tests; complements real API/SQL guarantees |
| CPP mapping/default eligibility/full source/output | CppUsesOwnedActiveSourceCompleteSelectedOutputEscapingAndRealMultipagePdf | Five vs eight, Resolved exclusion, foreign nonactive owner, empty unknown, unselected no-read/no-output, actual PDF/tail/escaping |
| Preferences and existing targets | New AlertKeys... plus updated existing target catalogue test | Persisted old/new overrides, fresh service/session, defaults/restoration, all 14 category targets |
| Browser behavior/read-only/conflict feedback | Five patient-chart-alerts.test.cjs cases | Emitted module executed against controlled DOM/fetch; no manual concurrency/security/SQL checklist |
| Shared regression | Focused derived CPP, selected-print, denial/audit and actual pagination cases | Reused affected boundaries only; no unrelated domain acceptance suites |

### Exact execution log

- Build command: `dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step114/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q`. First four builds succeeded with **0 warnings / 0 errors**: 1:06.68, 18.10s, 43.06s, 43.55s. Final reviewed build also succeeded with **0 warnings / 0 errors**, 1:21.21. Repository rules require focused checks but no separate Release/full-solution gate; these are isolated Debug outputs, not Release/deployment claims.
- Initial focused .NET run: **22 passed / 2 failed / 0 skipped**, 24 total, 10s, `artifacts/step114/step114-focused.trx`. New preference assertion incorrectly compared record-held list references; new unselected-print fixture retained another patient UID. Corrected fixture/assertions without weakening production safeguards. Only those two cases reran: **2 passed / 0 failed / 0 skipped**, 2s, `step114-corrections.trx`.
- First relevant gate: **52 passed / 1 failed / 0 skipped**, 53 total, 5s, `step114-final-relevant.trx`. Existing Results source guard rejected the word Critical from the new recorded Alert-priority sort in the shared file. Moved the newly added Alert loader to its own partial file; Results behavior and the existing test remain unchanged. Final reviewed gate: **53 passed / 0 failed / 0 skipped**, 6s, `artifacts/step114/step114-final-reviewed.trx`. Breakdown: 13 Alert SQL/API + 10 Alert Web/read-ownership + 2 preferences + 9 derived CPP + 19 selected-print cases. New .NET cases total 24 (13 SQL/API, 10 Web, 1 preference); shared cases are not new Alert-specific tests.
- New frontend: first four cases passed; after adding failed-list feedback, **5 passed / 0 failed / 0 skipped**, including final run. Existing preferences frontend: **6 passed / 0 failed / 0 skipped**. Existing selected-print frontend script: **PASS**, exit 0 (one script-level scenario group; no fabricated test count).
- TypeScript project `--noEmit` and targeted ES2020 module/JS-map emission: exit 0. No completed Family/Risk/Medication/History/Problems/Immunization/Allergy acceptance suites or visual checks rerun.
- Final diff review restored baseline Unicode bytes on unaffected chart lines after detecting an editor encoding conversion. Only intended Alert chart hunks remain; generated module re-emitted from corrected TypeScript. Manifest uniqueness, unchanged historical SQL, preserved-report hashes and whitespace checked. Build/TRX artifacts are ignored local evidence.

## 7. Essential human visual acceptance - USER-REPORTED PASS

Recorded 2026-10-19 from the user's report:

| Visual acceptance | Result |
|---|---|
| V1 - Chart Alert editor, saved state and conflict feedback | USER-REPORTED PASS |
| V2 - CPP Alert placement and preference visibility | USER-REPORTED PASS |
| V3 - Selected-print appearance and unselected exclusion | USER-REPORTED PASS |

Step 114 visual acceptance is closed on this user-reported basis. No tests, builds or visual checks were rerun for this documentation closure, and no migrations were run. Retained automated results: **53 .NET tests PASS; 11 frontend tests PASS (5 Alert + 6 preferences); print script PASS; TypeScript PASS; final build 0 warnings / 0 errors.** These remain the existing execution evidence in section 6.

The user-reported visual outcomes do not establish application-tenant deployment or migration 0082 verification. The deployment qualification below remains unchanged. Earlier Family/Risk/Medication/History/Problems/Immunization/Allergy evidence and Family M7/M8 deferrals are unchanged. No manual API/SQL, audit, permission, tenant or atomic-concurrency verification is requested; those guarantees are automated. The retained visual checklist follows for traceability.

**Deployment prerequisite:** apply 0082 with the matching API/Web before these checks. This task migrated disposable test stores only, not the application tenant. Rebuild the database tool so it has the current copied manifest (an old --no-build tool can incorrectly report current). For an authorized local-dev deployment, the existing runner command is `dotnet run --project src/MicroEMR.DatabaseTool -- tenant provision --tenant-key local-dev`. Use the intended tenant key; this is deployment, not a manual SQL check. No automatic application-tenant deployment is claimed.

### V1 - Editor, saved state and conflict feedback

Fictional fixture: Morgan Alert-Test; title **TEST ONLY assistance with communication**; message **TEST ONLY written instructions requested**; type Clinical; priority High. Use an existing permitted writer and, for readonly appearance, an existing read-only test account in the same approved test tenant.

1. Patients -> Search -> open Morgan Alert-Test -> Alerts -> Add Alert. Enter the fixture and save. Open Edit and inspect title/message/type/priority layout and saved values.
2. For conflict-banner appearance only, open this same row's Edit modal in two tabs. Save an amended note in tab A; in tab B enter a different unsaved note and Save. Inspect inline Reload and review feedback, intact unsaved note and readable buttons. Do not inspect SQL or use refreshed tokens/retries; race rejection is already automated. Cancel and reload tab B afterward.
3. Use Resolve on the fictional row, enter TEST ONLY finished, save; choose Resolved/All and inspect retained text/status/reason. This is the existing retained-close action, not a new Archive state.
4. With the readonly test account, inspect readable history, disabled Add and absent Edit/Resolve. Restore writer account for remaining checks.

PASS: readable Bootstrap modal/history, accurate Active/Resolved labels, clear inline stale feedback with no lost draft, appropriate readonly appearance. FAIL: clipping/overlap, misleading state, lost draft or usable readonly mutation controls.

### V2 - CPP placement and preference visibility

Fixture: create a fresh Active TEST ONLY Alert on Morgan Alert-Test (or retain another Active fixture); keep the resolved fixture for full-history navigation.

1. Chart -> Summary -> Chart Alerts. Inspect description/notes/type/priority, count if applicable, and View all chart alerts link. Follow the link and inspect Active/Resolved/All retrieval.
2. Summary -> Customize CPP -> Chart Alerts: hide Notes -> Save; inspect clean field visibility. Hide the category -> Save; verify it disappears from Summary while the Alerts tab remains accessible. Re-show category/Notes and Save.
3. On a separate fictional chart with no recorded alerts, inspect No active chart alerts recorded. It must not claim assessed absence of special needs.

PASS: stable relative category layout, readable values/navigation, clean preference hiding/restoration, unknown/empty wording honest. FAIL: clipping, wrong category/field hidden, loss of full-record access or inferred negative special-needs claim. Persistence/default semantics are already automated.

### V3 - Selected-print appearance and unselected exclusion

Fixture: Active TEST ONLY communication Alert above with a long fictional multiline note for wrapping. A pre-existing >5-row test fixture may be used for visual pagination; full-row/multipage correctness is already automated.

1. Summary -> Print CPP -> select configured test clinician -> select only Chart Alerts -> Print selected categories. Inspect description, full note, type/priority/status, patient/clinician context and page-footer readability.
2. Hide Alert Notes on screen and repeat explicit Alert printing: full selected note should remain readable. Restore the screen preference.
3. Print only another available category (for example Active Problems), leaving Chart Alerts unselected; inspect absence of Alert text.

PASS: readable, unclipped and escaped text, sensible note wrapping/page breaks/context, screen hiding independent from print, no unselected Alert content. FAIL: overlapping/truncated notes, missing selected text/context or unselected Alert leakage. No manual API or database probes.

## 8. Qualifications and recommended Step 115

- DE13 minimum text is supported through existing recorded title/message; this is not proof every Alert is a verified special-needs assessment or that every external special-needs workflow is covered. No unsupported field/date/type claim.
- PC07.01 remains PARTIAL; inventory 10 mandatory IDs / three packages: prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No SATISFIED - VERIFIED promotion.
- Preserve accepted-release/source applicability (Baseline folder 5.5/document 1.7 versus CDS-S folder 5.2 DFU/workbook 2.1 Final), external OntarioMD/CPSO acceptance, controlled matching migration/deployment, target SQL-engine and deployed OIDC/session/role/tenant qualifications. Disposable fixture authentication is not deployed identity proof.
- Preserve Medication DE09 source/date/identifier/clinical blockers, Immunization dose numeric/unit/pairing decisions, Allergy external recorded-date/ambiguous DIN decisions, Family M7/M8 deferrals and separate PC07.10 obligations. None implemented or reopened here.
- **Recommended Step 115:** bounded Medication DE09 field-to-source/date/identifier and output requirement resolution, reusing Step 113's matrix; approve clinical/unknown/default/source decisions before any independent implementation. Step 114 V1-V3 visual outcomes are recorded above as USER-REPORTED PASS; no Step 115 work is performed in this closure.

## 9. Files changed

Step 114 files below exclude the two preserved reports. Shared fixture edits only register real Alert service/repository and provide its optional CPP source dependency; no unrelated scenario changes.

- `db/tenant-clinical/manifest.json`
- `src/MicroEMR.Api/Controllers/PatientChartAlertsController.cs`
- `src/MicroEMR.Application/DependencyInjection.cs`
- `src/MicroEMR.Application/PatientChartAlerts/PatientChartAlertModels.cs`
- `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs`
- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppModels.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.cs`
- `src/MicroEMR.Infrastructure/PatientChartAlerts/PatientChartAlertRepository.cs`
- `src/MicroEMR.Web/ClientApp/patients/patient-chart-alerts.ts`
- `src/MicroEMR.Web/Controllers/PatientChartAlertsController.cs`
- `src/MicroEMR.Web/Models/PatientChartAlerts/PatientChartAlertModels.cs`
- `src/MicroEMR.Web/Services/PatientChartAlerts/PatientChartAlertApiClient.cs`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-chart-alerts.js`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-chart-alerts.js.map`
- `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs`
- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs`
- `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs`
- `db/tenant-clinical/migrations/0082-chart-alert-safeguards.sql`
- `src/MicroEMR.Application/PatientChartAlerts/PatientChartAlertService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.Alerts.cs`
- `src/MicroEMR.Web/Views/Patients/_ChartAlertsSummary.cshtml`
- `tests/MicroEMR.Api.Tests/ChartAlertSqlTests.cs`
- `tests/MicroEMR.Api.Tests/ChartAlertWebTests.cs`
- `tests/patient-chart-alerts.test.cjs`
- `docs/certification/149-step114-pc07-01-chart-alert-safeguards-implementation.md` (this report)
