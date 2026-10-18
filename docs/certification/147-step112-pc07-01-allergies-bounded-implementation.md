# Step 112 - PC07.01 bounded Allergies implementation

Date: 2026-10-18. Continues [Step 111](146-step111-pc07-01-allergies-field-date-design.md), reusing its authoritative mapping and [Step 103](138-step103-pc07-01-field-date-life-stage-design.md). **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** Implementation and automated evidence below do not constitute release certification or human visual acceptance.

## 1. Actual checkpoint and preserved work

- Started on `main`, HEAD `ad60b39be00518d3fa5a55408aff420c26c2548a`; only the untracked Step 110 and Step 111 reports were present.
- Working branch: `feature/step-112-pc07-01-allergies-bounded-implementation`. HEAD remains unchanged. No staging, commit, merge, reset or clean.
- Step 110 preserved SHA256: `BE1E1D31E4D41B27750994207776063CFAC7C625BC7F4824014A7ABEA5FFA2AA`; Step 111 preserved SHA256: `6273174D6FEA6242AD796C413BAFCB564C0AA8E199E544A7975EA6036892E464`.
- Read AGENTS.md and relevant retained evidence. No repeated specification extraction, external research, repository-wide scan or unrelated clinical implementation. SQL applied only to disposable automated test stores; no application tenant database was upgraded.

## 2. Implemented fields and compatibility

- **DIN:** nullable as-recorded string, maximum 100 characters (engineering limit). New entry accepts only explicit `Medication` classification. Leading zeroes survive. Unknown is NULL. No registry lookup or inferred classification. Contrast, Other, blank and arbitrary types remain ambiguous; a non-Medication DIN is rejected, without silent recoding or deletion. A type change carrying a recorded DIN requires deliberate reconciliation; omitted metadata preserves the DIN.
- **CT-016:** reuse `FamilyHistoryLifeStages` exact code/name identity: N New born, I Infant, C Child, T Adolescent, A Adult. Nullable unknown; no age inference. Allergy contract pins source `CDS-S 2.1 Final / 2026-06-05 / CT-016`. Source fidelity is based on Step 111's verified mapping, with automated exact code/label checks.
- **Onset:** reuse `ClinicalPartialDate`, supporting YYYY, YYYY-MM, YYYY-MM-DD and unknown. SQL validates parent parts and real calendar dates. No padded clinical dates or inferred onset. No unsupported future-onset cutoff.
- Legacy exact `OnsetDate` remains; reads derive exact parts without updating the row/token. New partial dates use nullable parts and a NULL exact date. Constraints prevent conflicting representations. Omitted new group preserves fields; explicit empty group clears them. Legacy scalar presence is tracked, dual scalar/group rejected, and a legacy client cannot clear an unrepresentable partial date accidentally.
- `CreatedAt` stays immutable system-entry provenance. UI and print label it **System entry recorded**; it is not an external original recorded date. No date backfill or new historical recording-date column.

## 3. Additive SQL and lifecycle safeguards

New manifest entry: `0081-allergy-fields-safeguards.sql`, ordered after existing prerequisites. No historical migration was changed. Adds nullable `DrugDIN`, `LifeStageCode`, `OnsetYear`, `OnsetMonth`, `OnsetDay` and constraints; no duplicate positive source or data rewrite.

Replaces `PatientAllergy_GetByPatientUid`, `PatientAllergy_GetByUid`, `PatientAllergy_GetDocumentationState`, `PatientAllergy_Create`, `PatientAllergy_Update`, `PatientAllergy_Resolve`, `PatientAllergy_AssertNoKnownAllergies`, `PatientAllergy_RevokeNoKnownAllergies`; adds `PatientAllergy_GetSnapshot`. Reads join a live patient. The snapshot returns positive rows and documentation state together under the same parent-patient lock used by mutations.

- NKA stays a separate explicit assertion: empty/only inactive/resolved positives never imply NKA. Assert is idempotent and refuses active positives; duplicate assertion does not change verifier/time or add a success mutation audit.
- Replacement requires confirmation plus the reviewed assertion UID and eight-byte RowVersion. Revoke also binds UID and token; SQL NULL, stale and malformed paths fail closed. Positive and assertion tokens remain distinct. Revocation history is retained.
- Replacement revokes the reviewed assertion and creates the positive row in one transaction. Assert/create, replacement/replacement, replacement/revoke and revoke/revoke races cannot silently replace a new assertion or produce contradictory snapshot state.
- Update/Resolve require current eight-byte tokens and atomic row-count checks. Edit/edit, edit/resolve and resolve/resolve permit only one current-version mutation. Already-resolved Resolve remains a no-op only after current-token validation. Reason is retained in the clinical audit snapshot.
- Preserve existing Active/Inactive/Resolved status editing and reactivation policy, with NKA guard on reactivation. No physical deletion, invented archive state, new terminal-status policy or clinical history overwrite.

## 4. Audit, authorization and Web

All mutations require an active tenant actor. Tenant identity comes from the trusted store, actor from the existing authenticated clinical context and API correlation from the server request. Client actor/correlation properties cannot override them. Existing AuditLog receives full before/after clinical rows (including new fields, row versions and assertion provenance), patient, tenant, actor, action and correlation. Required audit persistence is in the clinical transaction; audit faults, missing storage and caller rollback leave no successful mutation. Replacement's two audit destinations are tested separately. Rejections/no-ops produce no false success mutation audit.

Canonical detail route is `GET api/patients/{patientUid}/allergies/{allergyUid}`. The legacy route requires explicit `patientUid` query context; missing context is 400. Resource ownership is checked before successful read-audit recording. Denied/missing/foreign reads cannot become a successful resource-open audit, and read-audit persistence failure prevents successful disclosure. Internal unscoped repository compatibility remains; affected API/Web callers use patient-scoped contracts.

Web controller now requires Patients.View and ClinicalData.Manage on mutations/editor access, with existing antiforgery. Full chart reads use a coherent Allergy/NKA snapshot and return denied/unavailable outcomes instead of empty clinical data. Read-only users retain chart/detail viewing; mutation buttons are disabled or omitted. API remains independently permission-enforced.

New TypeScript captures the displayed positive RowVersion for Resolve, preserves reason/token on conflict and never refreshes/retries a stale write. The legacy inline Resolve handlers are removed; a frontend regression verifies one module inclusion and no inline tokenless fetch. NKA create conflict preserves the reviewed assertion and unsaved fields with reload guidance. No hidden refreshed token is substituted.

## 5. UI, CPP and selected print

Full Create/Edit and both chart quick-add entry points share DIN, CT-016 and year/month/day controls. Existing DIN is never erased or disabled by type-selection script. Details/history show precision-aware onset, and Details includes DIN, stage and labelled system timestamp.

CPP keeps its five-active-entry card limit and explicit NKA state. It adds OnsetDate/LifeStage preference targets while preserving DisplayName/Reaction/Severity keys and saved preferences; new fields default visible. All returned positive and assertion owners are checked before filtering, including foreign inactive rows.

Selected print captures the same coherent owned source once, uses every active Allergy without the card limit, and has an explicit full projection: agent, classification, status, reaction, severity, precise onset, life stage, notes and system-entry timestamp. Inactive/Resolved/unselected rows remain excluded. Missing data remains unknown; explicit NKA remains distinguishable. Print is independent of screen preferences, escapes text and fails if complete details are unavailable. DIN is store-only per the retained authority and has no mandatory card/print projection. Actual multipage PDF/tail content is tested.

## 6. Automated coverage matrix

Existing NKA, derived CPP, preference and selected-print coverage was reused before adding missing boundaries. Real HTTP/API plus disposable two-store SQL integration establishes database guarantees; Web/client tests supplement those guarantees.

| Requirement | Executed focused evidence | Result |
|---|---|---|
| Drug DIN/unknown/non-drug/ambiguous handling; all five CT labels/codes | `RealApiPersistsDinStagePrecisionAndNativeProvenanceWithCompleteAudit` (5), `NonDrugUnknownAndAmbiguousDinBehaviorAndInvalidDatesNeverInferClinicalFacts`, spoof/direct-SQL test | PASS |
| Four onset precisions, invalid hierarchy/calendar, legacy upgrade/omission/clear, immutable native provenance | Same five real API cases; `UpgradeLegacyAndOldClientOmissionPreservePreciseNewFieldsAndAuditHistory`; direct invalid-field SQL | PASS |
| Explicit/idempotent NKA, reviewed replacement/revoke, concurrency and coherent state | NKA lifecycle case; 3 assertion race cases; assert/create race; existing 7 NKA tests | PASS |
| Required Resolve token, stale edit/resolve, retained lifecycle | 3 positive race cases; real API lifecycle assertions; direct SQL NULL token case | PASS |
| Permissions, cross-patient/live patient, two tenants, inactive/spoofed actor, read-audit ordering/failure | `ScopedReadsAreOwnedBeforeSuccessAuditAndDenialsLeaveBothStoresUnchanged`, spoof/direct-SQL case | PASS |
| Full required audit and rollback | 6 fault cases (Create/Update/Resolve/Assert/Revoke/Replace, both replacement audits), missing table/direct NULL and outer rollback case | PASS |
| Web serialization/scoped URL/presence/permissions/antiforgery; rejected Resolve status; stale NKA fields/token | 7 `AllergyFieldWebTests` cases | PASS |
| CPP coherent ownership/complete print/NKA/multipage PDF | `CppPrintUsesOneCoherentOwnedSourceWithAllDetailsAndExplicitNkaOnly`; 16 selected-print and 9 derived CPP regression cases | PASS |
| Preferences/DOM defaults and existing keys | 2 focused .NET preference cases; existing 6 frontend preference cases | PASS |
| New frontend controls, token capture/conflict/read-only, no duplicate Resolve | 4 `patient-allergies-fields.test.cjs` cases | PASS |

### Exact execution results

- Isolated Debug build command: `dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step112/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q`. All six executed builds succeeded with **0 warnings / 0 errors**; elapsed 1:42.04, 13.92s, 1:19.33, 25.29s reviewed 17.44s and final 16.66s. No separate Release gate was required by the repository rules; no Release/deployment evidence is claimed.
- First focused .NET run: **6 passed, 24 failed, 0 skipped**. All 24 SQL cases failed shared migration initialization because the new-column constraints needed a separate SQL batch. Added the missing GO in the new migration. Corrected SQL-only run: **24 passed, 0 failed, 0 skipped**, 12s, `artifacts/step112/step112-sql-corrected.trx`.
- After adding spoof/direct-SQL and stale-Web feedback cases, relevant regression: **66 passed, 0 failed, 0 skipped**, 8s, `artifacts/step112/step112-final-relevant.trx`.
- Final diff review found a remaining duplicate legacy inline Resolve handler. Removed it, corrected an Allergy field separator and strengthened the existing frontend check; this new concern justified a final build and relevant regression repeat. Reviewed regression repeat: **66 passed, 0 failed, 0 skipped**, 8s, `artifacts/step112/step112-final-reviewed.trx`. No unrelated Family/Risk/Medication/History/Problems/Immunization acceptance suites were rerun.
- Final scope review also restored an accidentally removed, unrelated medication-discontinue handler exactly from HEAD. A direct baseline comparison confirmed identical handler content; the four Allergy frontend cases passed again. A final Razor build validates the restored file. No medication behavior change is retained.
- Final .NET scope: **25 Allergy SQL/API + 7 Allergy Web + 7 existing NKA + 9 derived CPP + 16 selected-print + 2 preference cases = 66**. New .NET cases total 33 (25 SQL, 7 Web, 1 preference); the other preference case is reused.
- `node tests/patient-allergies-fields.test.cjs`: **4 passed / 0 failed / 0 skipped**, including final duplicate-handler assertion. An intermediate assertion edit had a JavaScript quoting syntax error before test discovery; fixed immediately and reran successfully. Earlier runs of the four cases also passed.
- `node tests/pc07-cpp-display-preferences.test.cjs`: **6 passed / 0 failed / 0 skipped**. `node tests/pc07-selective-cpp-print.test.cjs`: **PASS**, one script-level scenario group (not a fabricated individual-test count).
- TypeScript emission of the new module: exit 0. Project `tsc --project src/MicroEMR.Web/tsconfig.json --noEmit`: exit 0. A preceding attempt used nonexistent `ClientApp/tsconfig.json` and returned TS5058; corrected to the existing project path.
- Final diff/whitespace, migration-manifest uniqueness and preserved-report hashes checked. Generated JS/map accompany the TypeScript. Build/TRX artifacts are ignored local evidence, not tracked release artifacts.

## 7. Essential human visual checks - PENDING

Use fictional records only in the local test tenant. Existing Step 109 Immunization V1-V3 remain user-reported PASS; none is reopened. No Step 112 user-reported visual PASS exists yet. API, SQL, audit, security, race and isolation checks above are automated; no manual SQL/API checklist is required.

### V1 - Allergy editor and partial-date controls

Fixtures: fictional patient **Morgan Allergy-Test**; agent **TEST ONLY Medication A**, Medication, DIN `00123456`, reaction `TEST ONLY rash`, Mild, Child, onset year `2000`, month `2`, day blank, notes `TEST ONLY visual fixture`.

1. Patients -> Search -> open Morgan Allergy-Test -> Allergies -> Add Allergy. Inspect the quick-add modal, choose Medication, fill the fixture and save.
2. Allergies -> View/Details for that row -> return to chart -> Edit. Verify DIN, Child and year/month remain intact, with day blank. Inspect year-only by clearing month/day, then unknown by clearing all parts; save the desired test state. Inspect a full leap date `2000/2/29` as well.
3. For full Add Allergy screen, navigate to `/PatientAllergies/Create?patientUid=<the same chart patientUid>` (copy UID from chart URL). Choose Food and observe DIN hidden when empty; choose Medication and observe it appear. Inspect blank life stage and onset. Cancel without adding another row.
4. In Edit of the DIN row, change type to Contrast and inspect the still-visible DIN, then return to Medication and cancel. No hidden deletion should occur.

PASS: readable Bootstrap layout, five correct stage labels plus Not recorded, independently blank date parts, no invented day/month, full/quick forms visually consistent, DIN preserved and leading zeroes visible. FAIL: clipping/overlap, padded dates, wrong labels or silent DIN disappearance. This is an appearance check; server rejection rules already have automated evidence.

### V2 - Allergy/NKA history and permission-sensitive appearance

Fixtures: Morgan Allergy-Test above; separate fictional **Morgan NKA-Test** with no positive Allergy records; a permitted read-only clinical account in the same test tenant.

1. On Morgan Allergy-Test, open Allergies -> Details: inspect precise onset, stage, notes and **System entry recorded** label. Return to chart; inspect Active/Resolved/All filters and status badges using retained fictional test records.
2. Open Morgan NKA-Test -> Allergies: the initial state must say Allergy status not documented, without an NKA assertion. Click Document No Known Allergies; inspect explicit banner and verifier/time. Open Add Allergy: inspect replacement warning and confirmation checkbox. Cancel; inspect Revoke NKA control.
3. Sign in as the permitted read-only account; reopen both fictional charts. Inspect readable history/details/NKA labels, disabled Add/Document/Revoke controls as applicable, and absent Edit/Resolve actions. Do not exercise manual endpoint/security probes.

PASS: explicit negative banner differs clearly from unknown; partial/unknown onset remains honest; system timestamp is separately labelled; read-only appearance is clear and readable. FAIL: unknown looks like NKA, date precision is invented, or usable mutation actions appear for read-only user. Restore the writer account afterward.

### V3 - CPP preferences and selected PDF appearance

Fixture: Morgan Allergy-Test with at least the known year-month/Child row; retain a fictional long note for line-wrap inspection. For visual pagination only, reuse an existing >5-active-row fictional fixture if available; the full-count/multipage guarantee is already automated.

1. Patient chart -> Summary -> Active Allergies: inspect precise onset/stage beside existing agent/reaction/severity and the View all allergies link.
2. Customize CPP -> Allergies: hide Onset date and Life stage -> Save. Confirm those fields disappear cleanly with separators and other fields readable. Restore Defaults; confirm both return. Saved old preferences must not hide new fields by default.
3. Print CPP -> select a configured test clinician for letterhead -> select only Allergies -> Print selected categories. Inspect agent, type/status, reaction, severity, exact partial onset, life stage, full note and labelled System entry recorded timestamp. Check line wraps, page breaks and footer legibility. DIN need not appear because it is store-only. Screen hide preferences must not suppress selected-print fields.
4. Repeat selected Allergy printing on Morgan NKA-Test while its assertion is active. Inspect explicit No Known Allergies wording. An unknown/empty fixture must instead remain not documented.

PASS: readable, unclipped fields/notes and page layout, accurate partial/unknown dates, explicit NKA distinct from unknown, screen preferences respected on screen and full selected details retained in PDF. FAIL: missing mandatory details, truncated/overlapping notes, preference leakage into print, invented dates or implicit NKA.

## 8. Qualifications and recommended Step 113

- External/historical original recorded-date definition, precision/provenance/correction policy remains unresolved. Some old CreatedAt values may be compatibility-schema backfills. Native provenance tests do not certify external original recording-date coverage.
- DIN coverage is bounded to explicit Medication; ambiguous Contrast/Other/arbitrary classifications need authoritative resolution. No inferred recoding, numeric-format claim or registry verification.
- Preserve OntarioMD release qualifications: Baseline folder 5.5/document 1.7 versus CDS-S folder 5.2 DFU/workbook 2.1 Final, external OntarioMD/CPSO acceptance, matching controlled migration/application deployment, target SQL engine and deployed OIDC/session/role/tenant provenance. Local fixture evidence does not verify deployment or release applicability.
- Preserve deferred Immunization numeric dose/unit/pairing policy, Family M7/M8 and all prior completed evidence. Inventory remains 5 prescribing IDs (PC04.01/.06/.07/.09/.14), 4 CPP IDs (PC07.01/.04/.07/.10), 1 scheduling ID (PC09.03), three packages. No SATISFIED - VERIFIED promotion.
- Recommended Step 113: record Allergy V1-V3 outcomes and perform bounded requirement resolution of the external recorded-date and ambiguous DIN questions using the retained exact source locators before proposing any further implementation. Do not infer clinical policy or start additional coding under Step 112.

## 9. Files changed

The following are Step 112 changes (the two pre-existing untracked reports are preserved and excluded). Shared fixture edits register real Allergy/read-audit services; selected-print fixture changes supply explicit Allergy full-detail content. No unrelated clinical code was refactored.

- `db/tenant-clinical/manifest.json`
- `src/MicroEMR.Api/Controllers/PatientAllergiesController.cs`
- `src/MicroEMR.Application/PatientAllergies/Contracts/CreatePatientAllergyRequest.cs`
- `src/MicroEMR.Application/PatientAllergies/Contracts/NoKnownAllergiesModels.cs`
- `src/MicroEMR.Application/PatientAllergies/Contracts/PatientAllergyDetailsResponse.cs`
- `src/MicroEMR.Application/PatientAllergies/Contracts/PatientAllergyListItemResponse.cs`
- `src/MicroEMR.Application/PatientAllergies/Contracts/ResolvePatientAllergyRequest.cs`
- `src/MicroEMR.Application/PatientAllergies/Contracts/UpdatePatientAllergyRequest.cs`
- `src/MicroEMR.Application/PatientAllergies/Repositories/IPatientAllergyRepository.cs`
- `src/MicroEMR.Application/PatientAllergies/Services/IPatientAllergyService.cs`
- `src/MicroEMR.Application/PatientAllergies/Services/PatientAllergyService.cs`
- `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs`
- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppModels.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.cs`
- `src/MicroEMR.Infrastructure/PatientAllergies/PatientAllergyRepository.cs`
- `src/MicroEMR.Web/Controllers/PatientAllergiesController.cs`
- `src/MicroEMR.Web/Controllers/PatientsController.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/CreatePatientAllergyRequest.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/CreatePatientAllergyViewModel.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/EditPatientAllergyViewModel.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/NoKnownAllergiesModels.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/PatientAllergyDetailsResponse.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/PatientAllergyListItemResponse.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/ResolveAllergyViewModel.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/ResolvePatientAllergyRequest.cs`
- `src/MicroEMR.Web/Models/PatientAllergies/UpdatePatientAllergyRequest.cs`
- `src/MicroEMR.Web/Services/PatientAllergies/IPatientAllergyApiClient.cs`
- `src/MicroEMR.Web/Services/PatientAllergies/PatientAllergyApiClient.cs`
- `src/MicroEMR.Web/Views/PatientAllergies/Create.cshtml`
- `src/MicroEMR.Web/Views/PatientAllergies/Details.cshtml`
- `src/MicroEMR.Web/Views/PatientAllergies/Edit.cshtml`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml`
- `src/MicroEMR.Web/Views/Patients/_SummaryQuickActionModals.cshtml`
- `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs`
- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs`
- `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs`
- `db/tenant-clinical/migrations/0081-allergy-fields-safeguards.sql`
- `src/MicroEMR.Application/PatientAllergies/Contracts/AllergyClinicalFields.cs`
- `src/MicroEMR.Web/ClientApp/patients/patient-allergies.ts`
- `src/MicroEMR.Web/Views/Shared/_AllergyClinicalFields.cshtml`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-allergies.js`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-allergies.js.map`
- `tests/MicroEMR.Api.Tests/AllergyFieldSqlTests.cs`
- `tests/MicroEMR.Api.Tests/AllergyFieldWebTests.cs`
- `tests/patient-allergies-fields.test.cjs`
- `docs/certification/147-step112-pc07-01-allergies-bounded-implementation.md` (this report)

## 10. Local development follow-up - snapshot 503

On 2026-10-18 the user reported a GET Allergy snapshot 503 after switching to main (trace `fff66bf53d8bb177200cd10b3c6682d1`). Actual checkout was main at the same HEAD, carrying all Step 112 uncommitted changes. Switched back to the existing feature branch, preserving the working tree and reports. No source fix, commit or merge was required.

The previously built database tool used its old copied manifest: it reported 81/81 migrations current, ending at 0080. Rebuilt the tool with current controlled assets into ignored `artifacts/step112/database-tool`: build PASS, 0 warnings/errors, 18.90s. Its read-only status then confirmed 82 available / 81 applied, **only 0081-allergy-fields-safeguards missing**, valid tenant identity, no hash mismatch or unexpected migration. This missing migration supplies the new snapshot procedure and fields required by the endpoint; the pasted telemetry itself contains no inner exception, so it is not treated as a complete exception trace.

Applied the single pending migration to **local-dev / MicroEMR_Db** using the existing audited provisioning runner. Result: Migrated, applied migrations 1. Follow-up status: **82/82, Current YES, latest 0081, no missing/hash mismatches/failure**. This follow-up is the first application-tenant upgrade; the earlier implementation-stage statement about disposable test stores describes the earlier checkpoint.

A temporary ignored diagnostic reused trusted configuration, tenant resolution/connection validation and the real Allergy repository. Read-only snapshot smoke test against a live local-dev patient: **PASS for procedure execution, field mapping and ownership**, with no patient data printed or changed. Diagnostic build succeeded with 0 errors and 2 NU1900 warnings because the NuGet vulnerability feed was unreachable. No authenticated browser HTTP replay was performed; reload the chart to confirm the user session. Final whitespace check passed. Only this report is edited by the follow-up; temporary diagnostics/build assets are ignored. Certification and visual-check status remain unchanged.
