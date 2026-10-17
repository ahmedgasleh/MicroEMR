# Step 99 — PC07.01 Risk Factors assessment and bounded design

Date: 2026-10-17. Assessment/design only, continuing Steps 95–98. **PC07.01 remains PARTIAL. Family Medical History is implemented with deferred M7/M8 qualifications. Risk Factors remains MISSING until implementation. Inventory remains 10 mandatory IDs / three packages. No OntarioMD acceptance is claimed.**

## 1. Actual Git checkpoint and work boundary

- Branch **main**, HEAD **6f932e6fcd15a638f60a51d4916fccfc00dc56d8**. `git status --short` was empty at the start. This differs from the historical branch/HEAD recorded in earlier reports; those reports remain historical evidence, not the current checkout.
- Preserve all existing reports and implementation. This step adds only this document. No branch change, staging, commit, merge, code/configuration/database change, build, test run, browser operation or manual verification.
- Reused Step 95's Baseline/category mapping and Step 96's bounded family-history conventions; consulted current report 133 for the latest M7 result and report 132 for prior automation. No repeated whole-CPP assessment or Family History test run.
- Bounded source inspection: current patient demographic and clinical-history DTOs; family-history DTO/service/life-stage mapping and SQL 0075; CPP aggregation/models/preferences/full-source print/composer; relevant chart, patient/vitals/encounter paths, tenant migration manifest and scoped SQL text/name searches for risk/social-history fields. No authoritative risk/social-history domain was found in those paths. This is a source finding, not a claim about arbitrary live database content or a repository-wide absence proof.
- Additional authoritative extraction was limited to **Data Dictionary A195:K202** and **Code Tables A211:F216** in the existing CDS-S workbook. Read-only ZIP/XML extraction used the Python standard library after the system Python lacked openpyxl; no dependency installed and no workbook altered. Baseline page references below reuse Step 95's extraction rather than repeating it.

## 2. Authoritative requirement scope

### Explicit source requirements

1. [Primary Care Baseline — 1.7 Requirements.pdf](</D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), Version 1.7 Final, section 2.7: **p.27 PC07.01(g)** includes Risk Factors in the displayed CPP; **p.28 PC07.07** requires risk-factor display with reference to CDS-S; **p.29 PC07.10** requires recording/updating CPP categories. PC07.10's minimum encounter-managed set names diagnoses, procedures and medication summary; it does not independently require a new risk-factor encounter editor. These references are reused from [Step 95](129-step95-pc07-01-cpp-category-completeness-assessment.md), not a new Baseline interpretation.
2. [CDS-S Data Dictionary (v2.1).xlsx](</D:/Development/Maui .Net 10/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), **Data Dictionary A195:K202**, DE12 Risk Factors. Step 95 records publication **2026-06-05**, version **2.1 Final**. Directly checked DE12 rows and conformance columns for this design.
3. Same workbook, **Code Tables A211:F216**, CT-016: **N = New born; I = Infant; C = Child; T = Adolescent; A = Adult**. DE12.006 refers to CT-016 with mandatory conformance. No Unknown code is listed. Life stage is when the **patient was first exposed**, not the patient's current age or a relative's stage.

Applicability qualification retained: the available workbook's containing folder is labelled **5.2 DFU**; Step 95 did not establish that this local publication is the applicable certification release. This design follows that existing mapping conditionally. No current-release, external CPSO-policy reconciliation or OntarioMD acceptance determination is invented.

| Element and exact locator | Explicit source support/conformance | Proposed minimum behavior (engineering choice where stated) |
|---|---|---|
| **DE12.001**, Data Dictionary row **196**, C/F: **M, S/D**; D/E name/definition | Risk Factor: factor that may place this patient at health risk. Examples include drinking, smoking, obesity, asbestos exposure and premature birth. No risk-factor code set is specified in G/H. | Independent `RiskFactor` text, required nonblank for a positive entry. Free text supports examples and other clinically relevant factors without a closed mandatory type list. |
| **DE12.002**, row **197**, C/F: **M, S/D** | Exposure Details: agent/exposure specifics; examples include quantity/frequency of smoking or drinking. | Independent nullable `ExposureDetails` text. Preserve units, frequency, substance/agent and uncertainty as recorded; no invented unit, zero, computed pack-years or consumption conversion. |
| **DE12.003**, row **198**, C/F: **O, S/D** | Age at onset is optional. | Omit from minimum Step 100 slice. Do not substitute patient age or CreatedAt. |
| **DE12.004**, row **199**, C/F: **O, S/D**; G/H full/partial date, mandatory conformance | Optional first-exposure date. If supported, full YYYY-MM-DD and partial YYYY-MM / YYYY support are specified. | Omit optional clinical dates from minimum slice. A later date extension must retain precision, not add full-date-only controls. |
| **DE12.005**, row **200**, C/F: **O, S/D**; G/H full/partial date, mandatory conformance | Optional last-exposure date, with the same date conformance. | Omit alongside start date. Archive/update timestamps never represent cessation or last exposure. |
| **DE12.006**, row **201**, C/F: **M, S/D**; G/H CT-016, mandatory | Life stage at first exposure. | Independent nullable CT-016 code, exact label displayed. Blank means not recorded, never Adult or an invented Unknown code. Validate exact code on server and SQL. |
| **DE12.007**, row **202**, C/F: **M, S/D** | Notes: additional risk-factor notes. | Independent nullable `Notes` text. Do not collapse exposure details or life stage into notes. |

**Mandatory product support is not mandatory knowledge on every patient.** All four mandatory element types must be independently stored/retrieved/displayed. Requiring only a nonblank factor for entry, while preserving unknown exposure/stage/notes as NULL, follows the existing Step 95/96 interpretation; it is an engineering requiredness decision, not an explicit source requirement that a patient have any risk factor.

### Illustrative topics and excluded inferred requirements

| Topic in the request | Source conclusion | Bounded design consequence |
|---|---|---|
| Smoking/tobacco exposure | Smoking is an explicit example in DE12.001; exposure specifics are illustrated in .002. No dedicated smoking-status enum or cessation workflow is specified here. | Record smoking/tobacco and exposure detail as text; secondhand exposure can be explicitly recorded, never inferred. No mandatory smoker questionnaire/module. |
| Alcohol use | Drinking is an explicit example. | Supported text entry; no compulsory units/week field, score or consumption advice. |
| Substance use | Not independently named as a mandatory subcategory in these rows; general factor/agent text can represent clinically recorded substance exposure. | Supported without adding a substance registry or mandatory separate module. |
| Occupational/environmental exposure | Asbestos exposure is an explicit example; a specific occupation field is not mandated. | Record factor/agent/exposure text when relevant; no occupational registry or employer record. |
| Other factors | Obesity and premature birth are explicit examples, not an exhaustive list. | Open text accommodates relevant clinician-recorded factors. No automatic risk diagnosis from BMI, birth date, conditions or family history. |
| Dates/status/severity | Age/start/end dates are optional. The inspected DE12 rows do not mandate a separate clinical severity, current/former status, score, duration or coded negative-history state. | Omit optional age/dates, severity and clinical status from minimum model. Active/Archived is a retained-record lifecycle only. Clinically meaningful qualifications such as former exposure may be recorded in text, not inferred from archive. |
| Structured versus free text | Independently supported fields and coded CT-016 stage are explicit. No factor/exposure terminology binding is specified here. | Four distinct fields: factor/exposure/notes as recorded text; stage coded. No new supplier dependency or requirement to structure every exposure quantity. |

## 3. Existing functionality and reuse

| Inspected implementation | Existing capability | Reuse / source-of-truth boundary |
|---|---|---|
| `PatientDetailsResponse` and patient contracts | Demographics, contacts, identity, attribution/version | PatientUid identifies the owner. No smoking/alcohol/substance/occupation/risk/social-history field was identified in these DTOs; do not overload demographics. |
| `PatientClinicalHistoryModels` / tenant SQL 0041 | Medical/Surgical history, description/relevant date, active/archive, actor/version | Reuse lifecycle/SQL conventions only. Its constrained Medical/Surgical type and full-date RelevantDate are not a DE12 source. Do not put risk factors into a third ad hoc history type. |
| `PatientFamilyHistoryModels`, `PatientFamilyHistoryService`, tenant SQL 0075 | Six independent family fields, patient ownership, permission checks, trusted actor, concurrency and transactional audit | Closest additive feature pattern. Reuse structure and acceptance-fixture techniques, not the clinical rows/table; family relationships and relatives' conditions are not this patient's exposure records. |
| `FamilyHistoryLifeStages` in Core | Exact CT-016 keys/labels plus safe unrecorded display | Reuse its existing mapping/validation as-is via the risk feature; domain-specific class name is not permission to rename/refactor working Family History in this slice. If sharing ever requires a broader abstraction, make that a separate justified change. Do not duplicate divergent code/labels. |
| Vitals `PatientVitalResponse.Bmi`, CPP Latest Vitals | Authoritative measured BMI/vitals context | Keep measurements there. Do not copy/recalculate BMI into risk persistence or automatically create an obesity risk record; a separately and intentionally recorded clinical risk statement is different from the measurement. |
| Encounter note paths | Narrative clinical information | Preserve existing notes, even if clinicians have written social-history content there. No NLP extraction, bulk backfill or hidden synchronization into a second store. No structured authoritative social-history source was identified in the inspected paths. |
| `PatientCppService` / models | Derived sections, top-five active clinical summary with total count, owned-source checks, safe empty/unavailable/restricted states, fail-closed chart-open read audit | Add a projection from one new authoritative risk service; no CPP persistence copy, separate risk snapshot or caching authority. |
| `CppDisplayCatalog` / preferences | Stable category/field keys; absent hidden key stays visible; user/tenant-bound configuration | Add one category and four field keys; preserve old settings and full-chart navigation. Hiding is presentation only. |
| `PatientCppService.Print`, `CppPrintService` | Load selected sources fully, validate patient ownership, print all selected clinical fields, escaped content, existing letterhead and page numbering; unavailable selected source prevents PDF | Extend category switch/projection. Full selected risk output must not inherit the compact five-row limit or be suppressed by screen preferences. |
| Family History SQL/HTTP/contract/frontend fixtures and CPP tests | Actual disposable SQL stores; real API/Application paths; audit rollback injection; frontend DOM/fetch checks; section/preference/print tests | Adapt helpers and add risk-specific assertions; existing family PASS is evidence for the pattern, not a Risk Factors PASS. Keep fixture-authentication versus normal-deployment qualifications explicit. |

Scoped name/text searches covered patient chart/CPP, patient/vitals/encounter contracts, Infrastructure domain names, tenant migrations and patient SQL scripts. No authoritative patient risk/social-history table/service was identified. **Step 100 must reconfirm this bounded source check against its then-current HEAD before creating a new source.** If a new authoritative social-history domain appears, integrate/extend that source rather than maintain duplicate smoking/alcohol records. This condition prevents premature duplication without expanding Step 99 into an exhaustive repository scan.

## 4. Missing behavior

There is currently no demonstrated patient-linked DE12 risk-factor record contract, storage service or chart editor, no risk section in `PatientCppSummaryResponse`/aggregation, no RiskFactors preference/selection category and no selected risk-factor print projection. Vitals/BMI, generic notes, family history and the patient's diagnoses cannot be asserted to fill that contract.

Required additions are the four independent mandatory supported fields, patient ownership, add/edit/read/retained archive, audited/concurrent tenant-routed persistence, chart retrieval and CPP integration. Functional Risk Factors acceptance evidence does not exist yet. Existing Family/CPP tests are reusable patterns only. **Risk Factors remains MISSING.** Other Step 95 category deficiencies remain separate; this design does not reopen them.

## 5. Proposed minimum implementation — not existing schema or code

### One authoritative tenant-local record

Proposed **`dbo.PatientRiskFactor`**, one explicitly recorded factor per patient entry. No separate per-topic tables, patient-social-history copy, risk-scoring engine, negative-assertion table or record-version table. Text duplicates are not silently merged; legitimate distinct exposures may share a label. Creation disables duplicate submission in the UI but does not pretend POST is idempotent after ambiguous network failure.

| Proposed column | Proposed contract / engineering bound |
|---|---|
| RiskFactorId | bigint identity internal primary key |
| RiskFactorUid | uniqueidentifier, generated server-side, unique stable public resource UID |
| PatientUid | uniqueidentifier NOT NULL; FK to `dbo.Patient(PatientUid)`; immutable on update/archive |
| RiskFactor | nvarchar(200) NOT NULL, trimmed and nonblank |
| ExposureDetails | nvarchar(1000) NULL; trim whitespace-only input to NULL |
| LifeStageCode | nvarchar(1) NULL; case-sensitive CHECK N/I/C/T/A only when recorded |
| Notes | nvarchar(1000) NULL; independent of exposure details |
| Status | nvarchar(20), Active/Archived, default Active; record lifecycle, not exposure cessation |
| CreatedAt / CreatedBy | server UTC datetime2(0) / trusted active local ApplicationUser bigint FK; immutable |
| UpdatedAt / UpdatedBy | nullable server UTC datetime2(0) / trusted local ApplicationUser bigint FK |
| RowVersion | SQL rowversion, returned as Base64 eight-byte version token |

Add an index on `(PatientUid, Status, CreatedAt DESC)`; use RiskFactorUid as deterministic secondary list order. All sizes/types and the lifecycle are engineering choices following the current pattern, not dictionary-specified numeric limits. Omit AgeAtOnset, start/end dates and severity in this minimum. No physical deletion, cascading clinical delete or Restore endpoint. Archive retains historical exposure text and does not assert that exposure ceased, disease resolved or risk is absent. An empty category is “Risk factors not documented,” never “No risk factors.”

### SQL and deployment contracts — proposed only

One additive tenant migration, append once to the existing manifest. The inspected tail is **0075-patient-family-medical-history**; **0076-patient-risk-factors** is a candidate, not a reserved number. At implementation recheck the next unused migration and SQL error-number range. Do not edit 0075 or older migration history, add a platform migration for existing permissions, or introduce EF migrations.

- `PatientRiskFactor_List(@PatientUid,@Status)` / `_Get(@PatientUid,@RiskFactorUid)`: verify nondeleted owner patient; get uses compound patient/resource scope, never resource UID alone. Lists default Active with explicit Archived/All. Existing risk entries return only from the authenticated tenant database; no fallback source.
- `_Create`: accepts four clinical fields, PatientUid and trusted Actor; validates required text/lengths/exact stage and active clinical actor. Lock/check live patient, generate UID/time, and insert row plus mandatory Create AuditLog in a single transaction. Return saved fields/attribution/version.
- `_Update`: accepts four clinical fields, expected version and trusted actor; locks scoped row, validates ownership/version/Active, preserves owner/UID/creation attribution, updates with final RowVersion predicate and exactly-one-row assertion; transactional Update audit includes complete old/new values. Return refreshed token.
- `_Archive`: same locked scope/version/Active checks; change lifecycle/update attribution only, transactional Archive audit and refreshed token. Further archived edits/re-archives conflict; no silent restore.
- Error mapping: invalid payload or malformed/missing version 400; unknown/wrong-scope patient/resource 404 without disclosure; valid stale version or forbidden archived transition 409; missing effective permission 403 after valid authentication. Audit/connection failure must not return mutation success; reuse existing safe exception/logging conventions. Assign SQL error numbers only after the focused implementation-time collision check.

### Application, API and chart workflow — proposed only

| Layer | Bounded addition |
|---|---|
| Application | `PatientRiskFactorResponse`, Create/Update/Archive request DTOs; `IPatientRiskFactorService`/service and repository interface following Family History. Validate text, stage, token and returned patient/resource ownership; async/cancellation and DI. No request tenant, actor, owner reassignment or client timestamps. |
| Infrastructure | `PatientRiskFactorRepository` over `ITenantSqlConnectionFactory`, typed stored-procedure parameters, safe ILogger output and typed conflict/not-found exceptions. No database access in controllers/Application. |
| API | Proposed GET list/single, POST create, PUT update, POST archive under `/api/patients/{patientUid:guid}/risk-factors` and `/{riskFactorUid:guid}` / `/{riskFactorUid:guid}/archive`. Preserve trusted clinical actor resolution. No DELETE/Restore. |
| Web | Typed API client/thin controller; GET list/single and existing antiforgery-protected POST create/update/archive pattern under `/patients/{patientUid:guid}/risk-factors`. Token refresh/OIDC unchanged. |
| Chart | Add **Risk Factors** tab with Active/Archived/All filter; Add risk factor plus Edit/Archive on authorized Active entries. Bootstrap 5 modal: Risk factor, Exposure details, Life stage at first exposure (Not recorded + CT-016 choices), Clinical notes. Only factor required for a useful incomplete positive entry. No optional date/age/severity controls. |
| TypeScript | Adjacent `patient-risk-factors.ts`, escaped render, independent optional-field display, duplicate-submit guard, retained inputs on errors/409, explicit reload/cancel and unsaved-change protection using the existing modal pattern. No automatic retry/overwrite, raw SQL/clinical console logging or UI redesign. |

Preserve original narrative notes and existing data. Do not migrate/import values, infer a verified negative, derive life stage from age, or auto-create factors from a Problem, family relative or BMI. No social-history rewrite, terminology dependency, risk advice or treatment recommendations.

## 6. CPP integration and requirement dependencies

- Add proposed `PatientCppRiskFactor` projection containing RiskFactorUid plus the four clinical values (stage rendered with CT-016 label). Add `RiskFactors` section to summary. Load through the authoritative risk service, check patient ownership and include only Active entries.
- Follow the current **five-record** compact limit, deterministic CreatedAt DESC / resource UID order, total active count and **View all risk factors** navigation to the full tab. Show all four field types safely. Unrecorded fields say Not recorded; distinguish NotDocumented, Unavailable and NotAuthorized. Retain the normal chart's existing fail-closed read-audit boundary.
- Add stable category key `RiskFactors`, fields `RiskFactors.RiskFactor`, `RiskFactors.ExposureDetails`, `RiskFactors.LifeStage`, `RiskFactors.Notes` to `CppDisplayCatalog`. Existing settings lacking these hidden keys display the new category by default. Persist through existing preferences only; no new preference schema/backfill. Hide/show affects neither source rows nor access controls.
- Extend full-source print loading and composer selection switches to `RiskFactors`: all Active entries, all four clinical values, escaped content, safe unrecorded labels and existing layout/letterhead/page numbering. Print must read full source, not five-card projection; explicit selection prints complete clinical fields independently of hidden screen settings. Excluded categories are not fetched/rendered; selected unavailable/restricted source prevents a misleading PDF. Retain historical printed artifacts unchanged.

| CPP requirement | Contribution / reuse | Remaining boundary |
|---|---|---|
| **PC07.01(g)** | One missing mandatory category gains an authoritative source, CPP display/retrieval and output integration. | Overall PC07.01 stays PARTIAL; other Step 95 field/category gaps are not closed. |
| **PC07.04** | Reuse Family History's technical safety/CT-016/UI/fixture patterns. | Family Medical History remains its own category/source; risk records do not satisfy relatives' history or close deferred M7/M8. |
| **PC07.07** | Same risk record, tab and CPP projection support the risk-factor display requirement and DE12 field mapping. | No duplicate PC07.07 feature/table; implementation/verification and source applicability remain required before closure. |
| **PC07.10** | Same add/edit workflow supports recording/updating the Risk Factors category. | Does not establish all CPP categories or the separate minimum encounter-managed diagnosis/procedure/medication-summary obligation. No new encounter editor in this slice. |
| **PC07.11 / PC07.13** | Bounded new-category integration with existing customization and selective-print mechanisms. | Preserve existing qualified closures; do not rebuild/retest the entire systems or infer missing-category acceptance from their prior PASS. |

## 7. Security, audit, concurrency and retained history

- **Tenant boundary:** trusted authenticated tenant context, platform assignment validation and actual tenant-database identity checks remain unchanged. All queries stay in that tenant's clinical database. PatientUid/resource UID pairs are mandatory for individual records; no resource lookup followed by client-authorized reassignment. Validate owner on source results and print projection as existing patterns do.
- **Permissions:** existing Patients.View for reads, ClinicalData.Manage additionally for mutations, enforced independently through Web/API policies and Application checks. Tests must distinguish real 403 permission denial from 401 authentication failure or malformed-input failure. No new permission/profile/role or security-control reduction.
- **Trusted attribution:** actual OIDC subject resolves to active tenant-local ApplicationUser for mutation through existing actor middleware/accessor. Client actor/tenant/attribution fields are excluded. SQL independently requires active actor. Reads do not acquire a new write/provisioning requirement; existing print actor rules remain.
- **Clinical audit:** use `AuditLog`, proposed EntityName `PatientRiskFactor`, resource UID EntityId, real local PatientId, trusted UserId and UTC timestamp. Create/Update/Archive log old/new JSON with all four clinical values and lifecycle Status, including NULLs. Mutation and audit insert commit/rollback together; never skip a missing/failing audit. Attribution columns are not a substitute for audit history. Denials, stale or foreign requests change no row/version and create no successful clinical audit; preserve security audit handling separately.
- **Concurrency:** valid eight-byte Base64 token on update/archive; UPDLOCK/HOLDLOCK scoped validation, final version predicate and rowcount assertion. HTTP 409 retains unsaved text and offers explicit reload; no background overwrite/retry. Archived rows reject further writes. Preserve creation metadata across edits/archive, with version/update attribution changed only on successful mutations.
- **History:** only retained archive; no physical clinical delete or SQL reactivation. Audit history and old printed artifacts remain. Clinical exposure ending/negative attestation are distinct future requirements, not invented lifecycle semantics.

## 8. Automation-first acceptance plan — proposed, not executed

No new risk tests exist or ran in Step 99. Reused filenames below identify fixtures/patterns, not executed Risk Factors coverage. Extend scoped shared helpers only when needed; do not duplicate a whole host/test suite or refactor unrelated tests. Test tenants/patients/users/resource IDs are generated; use disposable LocalDB names with validated prefix cleanup, never application/production databases.

| Planned assertion | Existing fixture/pattern | Missing automated coverage to add in Step 100 |
|---|---|---|
| **R1: create/read four independent fields, owner and trusted creation metadata** | `FamilyHistorySqlTests.SixFieldsPersistWithTrustedActorAndCorrelatedAudit`; `FamilyHistoryAcceptanceDatabase` | Real risk repository/procedures + API create/get/list. Compare input with persisted four-field record, exact owner/UID, trusted subject/actor/time and one matching Create event. Factor-only create preserves NULL optional values; empty list never means no risks. |
| **R2: validation before SQL and at SQL boundary** | `FamilyHistoryContractTests` requiredness/length/CT-016/version patterns | Proposed risk contract tests: whitespace/lengths, all N/I/C/T/A and null, reject lower-case/UK/invalid stage, malformed/missing version. Direct SQL rejects invalid values without mutation/audit. No age/date defaults or clinical status invented. |
| **R3: edit/archive persistence and retained history** | Existing family update/archive SQL cases | Actual risk SQL create/edit/archive: only intended field changes, immutable patient/UID/CreatedAt/By, changed RowVersion and trusted update actor; archived fields readable under Archived/All and excluded from Active/CPP/print. Archived transitions conflict; no physical deletion/Restore route. |
| **R4: stale save/archive** | `UpdatePreservesCreationAndRejectsStaleSaveAndArchive` and existing HTTP fixture | Two loaded versions, one successful update, stale update and archive each 409 via real API/Application/SQL; full state/version/audit snapshots unchanged after rejection. Concurrency tests also assert exact successful-audit counts. |
| **R5: permission denials and preserved reads** | Existing actual API policy/actor middleware fixture and M9 HTTP denial pattern | Writer creates active risk fixture; reader with Patients.View only reads it, then create/update/archive 403. Real SQL snapshots show no new/changed risk rows or clinical success audits. Existing approved no-Patients.View fixture denies reads. Test direct Application denial as well. Normal OIDC/platform permissions are separate from fixture inputs; no false deployed PASS. |
| **R6: compound patient ownership and both tenant directions** | `FamilyHistoryAcceptanceTests.M8BothTenantDirectionsReturn404WithoutDisclosureOrDatabaseChanges` and two disposable SQL databases | Reuse host/database architecture for risk A/B. Local A/B rows/list exact; wrong same-tenant patient and both local/foreign patient UID combinations in both receiving tenants, GET/PUT/archive 404 without disclosure. Compare ALL risk rows/tokens and clinical audits in BOTH stores; resource UID possession is never authorization. |
| **R7: complete audit correlation and rollback** | Existing M9 actor/subject/UTC/JSON helpers and `AuditFailureRollsBackEveryClinicalMutation` | Risk successful lifecycle: exact entity/patient/actor/AuthSubjectId/actions/counts, SQL-clock windows rounded for datetime2(0), all four fields/status old/new JSON incl NULLs. Stale/wrong-scope/permission attempts leave full snapshots unchanged. Disposable-only injected audit failure for create/update/archive must roll back clinical row/version and audit. No failure trigger in an application tenant. |
| **R8: CPP category, ownership, empty/error and bounded/full loading** | `FamilyHistoryAggregationLimitsSummaryButPrintReadsAllActiveOwnedRows`, current derived-CPP/read-audit fixtures | Risk service data with >5 Active and Archived rows: summary top five + exact total, full tab all appropriate rows, foreign returned owner rejected, empty/Unavailable/NotAuthorized distinct; no inferred negative. Preserve existing fail-closed chart-read audit and correct source navigation. |
| **R9: display preferences remain presentation-only** | `CppDisplayPreferencesTests` old-settings/new-category/default and field tests; frontend preference fixture | Existing settings without RiskFactors retain default visibility. Save/reload category + four hidden field keys through resolved user/tenant repository; risk source unchanged, full-tab link retained. No broad preference regression rerun absent demonstrated failure. |
| **R10: selective complete print** | `FamilyHistoryPrintIncludesCompleteSixFieldProjectionAndRespectsSelection`; `CppSelectivePrintTests` full source/context/errors | Risk-specific full projection with >5 Active entries and long/executable-looking text: all four fields/labels, escaping, unrecorded wording, archive exclusion and no internal UIDs. Explicit selection unaffected by hidden screen fields; excluded risk source not loaded; unavailable/restricted/wrong-owner selection produces no PDF. Reuse existing multipage/letterhead rendering checks, adding risk content assertions only. |
| **R11: client workflow and safe state** | `tests/pc07-family-history.test.cjs` DOM/fetch fixture, current Bootstrap modal | Risk client tests for add/edit/filter/archive, validation, antiforgery submission, disabled duplicate save, retained text/409 reload, reader controls absent and escaped display. Add browser-level visual coverage if existing tooling supports it; do not treat DOM doubles as normal login/tenant-selection evidence. |

Plan proposed risk-specific `.NET` files alongside existing tests, e.g. `RiskFactorContractTests`, `RiskFactorSqlTests` and targeted `RiskFactorAcceptanceTests`; adapt existing CPP/preference tests and add adjacent TypeScript frontend tests. Allocate actual case names at implementation, then record only executed counts/commands. A focused project build/test selection is sufficient; no full solution or broad regression by default. A sandbox LocalDB start failure may require the established approved test execution context, not a mock substitute or fabricated PASS.

### Human verification reserved for presentation, at consolidated verification

No manual execution requested now. Most field, persistence, HTTP, audit/isolation and output assertions above can be automated reliably, so no manual API/Postman procedure is required by this design. If normal-deployment credentials/browser tooling is unavailable later, label that integration assertion deferred and provide the then-implemented exact URLs/payloads rather than invent endpoints or tokens today.

Minimal eventual visual check (only if automated browser/render coverage cannot reliably establish it):

1. Existing test Writer A / Tenant A → **Patients → Avery Family-Test → Risk Factors → Add risk factor** (proposed navigation). Enter factor **TEST ONLY - tobacco exposure**, exposure **TEST ONLY - patient-reported 2 cigarettes per day**, life stage **Adult (A)**, notes **TEST ONLY - synthetic risk-factor fixture**. Save once; the Risk Factors tab and CPP Summary show the same four independent values, readable labels and View all navigation without overlap or clipped text. This is a fictional software fixture, not advice.
2. Hide/show **Risk Factors** and its Exposure details field through existing CPP customization. Expected: correct presentation changes, full source still readable from the tab. Select Risk Factors in the existing CPP print dialog; preview/PDF remains readable and includes complete selected active records/fields despite hidden screen fields. Inspect page flow only if current automated layout coverage is inadequate.
3. Existing Reader A normal session: readable source with mutation controls absent/disabled. Actual HTTP denials and unchanged SQL state are automated by R5, not left to visual inspection. Normal-session integration evidence, if separately required, must retain its own provenance.

Record visual observations only when actually executed; no PASS in Step 99. Do not ask the operator to repeat Family History acceptance to prepare Risk Factors.

## 9. Deferred Family Medical History checkpoint

| Scenario | Preserved evidence / deferred work |
|---|---|
| M1–M6 | **User-reported PASS**, including explicit M4 confirmation. No rerun or upgrade to independently executed evidence. |
| M7 | Three deployed Postman create/update/archive requests returned **403 — user-reported PASS** in report 133. Remaining deployed database/audit comparison, preflight and unconfirmed Web/UI assertions are **deferred to consolidated certification verification**. Do not mark whole M7 PASS. |
| M8 | Prior automated backend checks **PASS**; normal browser/tenant-session observations remain **deferred to consolidated verification**. Remains PARTIALLY AUTOMATED, not whole-scenario PASS. |
| M9 | **PASS — isolated API/SQL audit verification**, preserving fixture-versus-deployed qualifications and prior execution record. No rerun. |

Step 99 explicitly supersedes the earlier “next manual check” scheduling in report 133 without rewriting its evidence. No new manual Family History tests requested. Preserve actual earlier results and absent capture qualifications; neither family-history implementation nor risk design closes overall PC07.01.

## 10. Step 100 implementation readiness

**READY FOR A SEPARATELY AUTHORIZED BOUNDED IMPLEMENTATION**, following the available-source applicability qualification. This is design readiness, not implementation, test PASS, certification acceptance or authorization to execute Step 100 now.

Fixed minimum: four supported clinical fields; factor required, unknown optional values NULL; existing CT-016 mapping; one authoritative tenant-local record source; audited add/edit/read/retained archive; existing permissions/actor/tenant boundaries; CPP summary/preferences/full selected print integration; no optional dates/age/severity, per-topic questionnaires, risk calculations, encounter workflow, negative attestation or broad refactor.

Implementation preflight: inspect then-current Git/manifest and new risk/social-history changes only; reuse any newly authoritative source; choose unused migration/error numbers; reuse helpers without changing historical Family/CPP behavior. Applicable CDS-S release remains a certification qualification to resolve through the existing governance path; do not silently assert external applicability or expand this design into optional dates to compensate.

Recommended next step: authorize Step 100 to implement this minimum and execute only new/affected focused automation. Keep Family History M7/M8 in the consolidated verification queue. Other Step 95 mandatory-element gaps, source-release applicability, and the PC07.10 encounter-managed minimum remain separate work. **PC07.01 PARTIAL; Family History implemented with qualifications; Risk Factors MISSING; 10 mandatory IDs / three packages.**

### Step 99 completion record

Only this report added. Read-only source/dictionary inspection and document/Git review performed; no builds, tests, manual verification or application database queries. Final expected working tree: **one untracked report**, all previously existing files unchanged. No commit/merge or inventory reduction. Stop after Step 99.
