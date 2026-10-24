# Step 169A — PC07.07 requirement recovery and deployment verification plan

2026-10-24 (America/Toronto). **Requirement recovered: YES, from the original local specification PDF. PC07.07 remains PARTIAL — IMPLEMENTED pending its bounded deployment evidence.** Step 169B plan is prepared; execution is not authorized by this documentation step. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

## 1. Authoritative source recovered

| Property | Recovered source evidence |
|---|---|
| Title | **Primary Care Baseline Requirements** |
| Cover version/status/date | **1.7 – Final; March 20, 2026** |
| Actual source | [Primary Care Baseline - 1.7 Requirements.pdf](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>) |
| Locator | Section 2.7, **page 28 of 53**, row **PC07.07** |
| M/O; source status cell | **M; U**. These are source-table entries, not MicroEMR acceptance results |
| SHA-256 | `18F78EEA1BE2840664B5AAF209B34D6540BB657D3F01A92D7658BC51DD967C8D` |

**Exact requirement:**

> The EMR Offering MUST display risk factors.

**Exact adjacent guideline**, with PDF line wrapping normalized:

> Refer to OMD EMR Core Data Set Standards (CDS-S) – Data Dictionary

No lettered or separately numbered subrequirements occur in the PC07.07 row. Its referenced dictionary supplies the detailed element mapping below; neighboring clauses are separate obligations. In particular, PC07.06's ongoing-medication default is not a Risk requirement, and PC07.09's optional CPP ordering does not become a PC07.07 prerequisite. PC07.01(g) supplies the retained CPP context; PC07.10's encounter workflow remains separate.

This is a **primary local specification**, not a secondary quotation or a web summary. Bounded discovery inspected the known specification parent and its Functional/Baseline folder. The recovered location replaces the unavailable historical `D:/Development/Maui .Net 10/...` locator **for this source**. A local, read-only PDF stream extraction used existing Node/zlib, the PDF page tree and only cover/page 28 for text output. The guideline's en dash was checked against its font ToUnicode mapping (`0374 → 2013`). No browser, online search, dependency installation or product build was used. Ignored extraction helper/text: `.tmp/step169a/`.

**Applicability remains qualified:** the folder label is Baseline 5.5 Final / 2026-05-04, whereas the document cover is 1.7 Final / 2026-03-20. Recovering the original does not establish its acceptance for the tracked OntarioMD release. Retain the CDS-S 5.2 DFU folder versus workbook v2.1 Final and target-release qualifications from Steps 99/113/167. The recovered row introduces no new policy condition or implementation delta against the retained interpretation. It resolves Step 168's exact-clause retrieval gap, not external source-release acceptance.

## 2. Evidence already accepted — no repetition

Use [Step 99 section 2](134-step99-pc07-01-risk-factors-assessment-design.md), [Step 100 sections 2–9](135-step100-pc07-01-risk-factors-implementation.md), [Step 113](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md), [Step 167](234-step167-de08-008-evidence-closure-next-gap.md) and [Step 168's assertion matrix](235-step168-pc07-07-risk-factors-evidence-reconciliation.md).

| Recovered requirement / referenced support | Existing implementation and accepted verification | Remaining deployment assertion only |
|---|---|---|
| Display Risk Factors; DE12.001 factor, .002 exposure, .006 first-exposure life stage, .007 notes | Four independent fields through Application DTO/service, tenant Risk repository, migration 0076 and chart/CPP projection. Step 100 lifecycle/contract/CPP tests and owner V1/V2 PASS | Actual selected-tenant Web→API read and rendered four-field/label correlation with existing SQL values |
| Clinical storage/retrieval and truthful unknowns | Real isolated API/SQL round-trip, nullable fields, bounds and independent notes edit verified. CT-016 exact N/I/C/T/A and blank/no-default tested. No dose/date or current-age inference | Existing deployed rows remain correctly readable with exact text/code/unknown presentation; no new save required |
| CPP and patient/account preferences | Four stable field keys, category default, five-card/count/full navigation and saved preferences tested; owner V2 PASS | Deployed CPP visibility agrees with current saved account preferences; full Risk tab remains accessible. No preference write/repeated persistence test |
| History/retention | Active/Archived/All and retained archive, stale rejection and unchanged-state behavior tested | Read existing Archived/All views if suitable archived rows exist; no new archive. Active/Archived is record lifecycle, not clinical exposure timing |
| Print | Full-source selected Risk fields/all Active rows, escaping, Family coexistence and 12-entry projection verified; owner V3 appearance PASS | Inspect deployed Print CPP Risk category availability only. No new PDF generation/print acceptance requirement is introduced by PC07.07 |
| Security, actor, tenant/patient and audit | Step 100 real two-store isolation, foreign-patient/tenant rejection, reader/no-read denial, server actor correlation, RowVersion and transactional rollback PASS | Normal-session expected tenant/actor, allowed Risk reads and unchanged clinical state/read-audit correlation. No live adversarial/mutation replay |

Retain **35 .NET PASS / 0 FAIL**, **8 frontend PASS / 0 FAIL**, and **three owner-reported appearance PASS**. These are prior results, not new Step 169A execution or real-browser counts. DOM/fetch-double tests and fixture authentication do not establish normal deployed/OIDC behavior. Owner V1–V3 is accepted with its recorded absence of independently captured app/tenant/resource provenance; do not repeat the general checklist.

Optional DE12.003/.004/.005 age/date fields, severity, inferred risk modules and encounter Risk authoring remain outside this minimum. Mandatory product support does not compel known optional values on every existing record. No clinical policy or functional gap is newly identified.

## 3. Precise remaining assertions

1. **D1 — Normal development context:** selected approved tenant, trusted active clinical actor and effective allowed Risk read path, correlated in one normal session.
2. **D2 — Real deployed content:** existing patient/Risk UIDs, exact four values/CT-016 labels/NULL semantics agree between SQL, actual Web read response and rendered Risk tab/editor/CPP. A hidden CPP field is assessed against saved preferences, not falsely classified missing.
3. **D3 — Read-only presentation:** existing preference controls/status, retained history view and Risk print choice are accessible; one desktop and one mobile capture demonstrate the actual deployed shell. Reuse existing behavior/print tests.
4. **D4 — No clinical mutation:** before/after Risk rows and RowVersions unchanged; only acceptable read-audit activity, with normal-session actor correlation.

The full migration/health assessment is already retained in Step 165; do not rerun it. Its all-92 ledger supports historical inclusion of 0076, but immunization-specific comparisons/Step 166 screenshots are not D2. Capture only the minimum matching Risk runtime/asset evidence if the recorded deployment context no longer suffices.

## 4. Step 169B executable plan — definitions, not execution

### 4.1 Prerequisites and target

- Obtain **separate explicit owner authorization** for one normal `admin@microemr.local` login followed by the read-only Chromium/SQL checks described here. The Step 166 login was consumed; no session reuse or new login is assumed.
- Approved target retained: tenant **local-dev-fresh**, UID **f94c6ecd-4417-483e-8a94-0507cf8384c2**, SQL **192.168.50.1,50013**, database **MicroEMR_LocalDev_Fresh**. Never connect to production. Resolve the existing development secret in memory; do not print credentials/connection strings.
- Verify existing Web **https://localhost:7002**, API **https://localhost:7003**, Auth **https://localhost:7179** availability. These are repository HTTPS profiles and prior verified addresses, not freshly observed listeners in Step 169A. If stopped, stop and report; no service start/restart permission is implied.
- Discover suitable **existing** Risk fixtures only after target identity passes. No patient UID or Risk UID is invented. The Step 166 immunization patient is not assumed to have Risk records. Prefer an existing patient with an Active row and representative recorded values; use archived/NULL examples only if already available. No fixture creation/update, permission change or historical backfill.
- Reuse `.tmp/step166/browser.cjs` helper patterns and `.tmp/step153/command-transport.cjs`; replace the immunization-only assertions with the plan below in ignored verification tooling. Preserve the single-login guard, safe command transport and clean shutdown. Do not rerun prior helper gates or certification suites by default.

### 4.2 Actual routes and selectors

Definitions checked in [PatientsController](../../src/MicroEMR.Web/Controllers/PatientsController.cs), [Risk Web controller](../../src/MicroEMR.Web/Controllers/PatientRiskFactorsController.cs), [Risk API client](../../src/MicroEMR.Web/Services/PatientRiskFactors/PatientRiskFactorApiClient.cs), [Risk API controller](../../src/MicroEMR.Api/Controllers/PatientRiskFactorsController.cs), [Risk partial](../../src/MicroEMR.Web/Views/Patients/_RiskFactors.cshtml), [summary partial](../../src/MicroEMR.Web/Views/Patients/_RiskFactorsSummary.cshtml) and [Risk TypeScript](../../src/MicroEMR.Web/ClientApp/patients/patient-risk-factors.ts).

| Purpose | Actual route/control |
|---|---|
| Patient Risk chart | `GET /Patients/Details?patientUid={PatientUid}&tab=risk-factors`; `risk-factors` is explicitly accepted by NormalizePatientChartTab |
| Summary | Same chart route with `tab=summary`; normal visible tab controls can also be used |
| Risk tab | `[data-bs-target="#risk-factors"]`, pane `#risk-factors`; wait for active/show and completed fade before assertions/capture |
| Collection read | `GET /patients/{PatientUid}/risk-factors?status=Active`, `Archived` or `All`; Web JSON `{ success, items }` |
| Individual read | `GET /patients/{PatientUid}/risk-factors/{RiskFactorUid}`; Web JSON `{ success, item }` |
| Actual proxy API | `GET /api/patients/{PatientUid}/risk-factors?status=...` and `.../{RiskFactorUid}`. Browser uses normal Web proxy; do not extract tokens for a separate API login |
| Rendered history | `#patientRiskFactorRoot` with `data-list-url`, `#riskFactorList article`, `#riskFactorStatus` select; card fields are `dl`/`dt`/`dd` |
| Existing editor | `.edit-risk-factors[data-uid="{RiskFactorUid}"]`; `#riskFactorModal`, `#riskFactorModalTitle`, `#riskFactorForm`; controls `#riskFactorName`, `#riskExposure`, `#riskLifeStage`, `#riskNotes`; hidden `RiskFactorUid`/`RowVersion` |
| Close editor safely | `#riskFactorModal [data-bs-dismiss="modal"]` (**Cancel**). Its markup disables Escape/backdrop dismissal; do not reuse Step 166's Escape assumption |
| CPP | `#cppSummaryCards [data-cpp-category="RiskFactors"]`, `#riskFactorSummary`, `[data-cpp-field="RiskFactors.RiskFactor"]`, corresponding `.ExposureDetails`, `.LifeStage`, `.Notes`; summary navigation `[data-tab-target="risk-factors"]` |
| Preferences read | `GET /cpp/display-preferences`; `#cppDisplayPreferences`, `#cppPreferenceEditor`, `[data-preference-category="RiskFactors"]`, `[data-preference-field="RiskFactors.ExposureDetails"]` and other four keys |
| Print visibility | Expand `#cppPrint > summary`; inspect `#cpp-print-RiskFactors` and `#cppPrintCategories`; optional existing options read is `GET /cpp/print/options?patientUid={PatientUid}` |

Preferences [controller](../../src/MicroEMR.Web/Controllers/CppDisplayPreferencesController.cs) and [client script](../../src/MicroEMR.Web/ClientApp/patients/cpp-display-preferences.ts) confirm GET returns `hiddenCategories`, `hiddenFields`, `rowVersion`, `isAvailable`; checkbox changes automatically save via POST. **Do not toggle preference controls, submit their form or Restore Defaults.** Inspect saved states and resulting visibility only. If Risk is saved hidden, confirm intentional hiding plus full-tab access; reuse prior preference save/show-hide tests rather than change the setting for a screenshot.

[CPP print controller](../../src/MicroEMR.Web/Controllers/CppPrintController.cs) defines PDF generation as **POST /cpp/print**, not a GET. [Print partial](../../src/MicroEMR.Web/Views/Patients/_CppPrint.cshtml) has a submit button. Do not click it or whitelist that POST. Existing full-source/print and owner V3 evidence is sufficient; opening the disclosure/reading choices provides deployment visibility without a form submission.

### 4.3 Verified read-only SQL templates

These statements are **source-verified, not database-executed in Step 169A**. Names/columns come from [0076](../../db/tenant-clinical/migrations/0076-patient-risk-factors.sql), its repository mapping, existing Step 165/166 identity helpers and [chart read-audit procedure](../../db/tenant-clinical/migrations/0043-patient-chart-read-audit.sql). Bind parameters with typed ADO.NET parameters; do not interpolate patient/subject identifiers. The application read procedures below contain only reads; never invoke Create/Update/Archive.

First open only the fixed development connection, then check identity **before** any clinical query:

```sql
SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey
FROM dbo.TenantDatabaseIdentity;
```

Abort if not the unique expected identity. Discover candidate patients without creating fixtures:

```sql
SELECT TOP (10) r.PatientUid, COUNT_BIG(*) AS Records,
       SUM(CASE WHEN r.Status=N'Active' THEN 1 ELSE 0 END) AS ActiveRows,
       SUM(CASE WHEN r.Status=N'Archived' THEN 1 ELSE 0 END) AS ArchivedRows
FROM dbo.PatientRiskFactor AS r
JOIN dbo.Patient AS p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
GROUP BY r.PatientUid
ORDER BY ActiveRows DESC, Records DESC, r.PatientUid;
```

After choosing one existing patient, take before/after snapshots and authoritative application-read results:

```sql
-- @PatientUid uniqueidentifier, chosen from approved existing candidates
SELECT RiskFactorUid, PatientUid, RiskFactor, ExposureDetails, LifeStageCode,
       Notes, Status, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
       CONVERT(varchar(18), RowVersion, 1) AS RowVersionHex
FROM dbo.PatientRiskFactor
WHERE PatientUid=@PatientUid
ORDER BY RiskFactorUid;

EXEC dbo.PatientRiskFactor_List @PatientUid=@PatientUid, @Status=N'All';
-- @RiskFactorUid uniqueidentifier, selected from the rows above
EXEC dbo.PatientRiskFactor_Get @PatientUid=@PatientUid,
                              @RiskFactorUid=@RiskFactorUid;
```

Compare snapshot field values and version bytes before/after, including NULLs and archived rows. For JSON responses, convert RowVersionHex bytes to Base64 in the helper; compare the same version rather than a hex string against Base64. CT-016 label derives from the recorded code; normalize only the documented UI NULL/empty-to-Not recorded representation, not clinical text. Compare All UID set and Active subset to the corresponding API/rendered views. Do not claim a clinical exposure date from CreatedAt.

Use server time/audit-ID boundaries for the session, and existing account binding:

```sql
SELECT SYSUTCDATETIME() AS StartedAtUtc,
       ISNULL(MAX(AuditLogId),0) AS AuditStartId
FROM dbo.AuditLog;

SELECT UserId, Username, IsActive, AuthSubjectId
FROM dbo.ApplicationUser
WHERE Username=@Username; -- nvarchar, authorized admin@microemr.local

-- @AuditStartId bigint from pre-session query
SELECT AuditLogId, UserId, PatientUid, ActionName, EntityName,
       EventCategory, ResourceUid, Outcome, CreatedAt
FROM dbo.AuditLog
WHERE AuditLogId>@AuditStartId
ORDER BY AuditLogId;
```

Keep subject identifiers in memory/sanitized local evidence. Correlate authenticated account binding to active tenant UserId and new `PatientChartOpened` / `ClinicalRead` events for the selected patient, rather than infer actor from a rendered email. Review new events for clinical mutations, distinguish concurrent unrelated activity if present and do not attribute all global audit changes to the helper. Ordinary read-audit persistence is acceptable. No new audit insert procedure is called directly by verification. Snapshot equality supports unchanged selected Risk content, not a claim that every clinical table was globally hashed.

### 4.4 Minimal real Chromium sequence

1. **Context/preflight group (once):** confirm availability/target, discover suitable existing fixtures and baseline snapshot. Permit exactly one normal authorized login; select expected tenant and assert `meta[name="microemr-tenant-id"]`. Reuse existing binding/read-audit mechanisms for actor correlation. No password, cookie or token output, saved context or automatic retry.
2. **Desktop content group (1440 × 900):** navigate the actual Risk chart route. Intercept only for request logging/blocking, never fake responses. Assert successful collection read, correct scoped UID set, four exact rendered values/NULL and CT-016 labels against SQL. Open one existing Active editor if its normal authorized controls exist; compare unchanged field values and hidden UID/version, inspect labels/focus, then **Cancel**. Do not Add, Save, Archive or edit fields.
3. **CPP/preferences/history/print visibility group (same session):** visit Summary, await Risk loading and preference response, compare current saved hidden keys/control states to visible content and full-tab navigation. Read Archived/All through the existing status select; no mutation. Inspect the Print CPP Risk checkbox/label without changing selections or submitting. Reuse accepted print persistence/output evidence. Archived content-specific assertions are DEFERRED if no archived fixtures exist; do not manufacture them.
4. **Mobile deployment-layout group (375 × 900):** resize the same session and inspect the existing Risk tab and, where visible under saved preferences, CPP. Open/cancel the same unchanged editor only if needed for controls not shown in the first capture. Check scrolling, viewport overflow, readable recorded values/labels and keyboard focus on the affected controls. Capture one representative history/CPP view and editor view per needed width; wait for Bootstrap/sidebar/font transitions and inspect the actual captures. No broad sidebar/accessibility rerun or V1–V3 repeat checklist.
5. **Integrity/finalization group (once):** final snapshot/audit comparison, login and allowed/blocked request counters, actor/read correlation, clean context/browser shutdown and focused report. No logout POST required. Count executed assertions and optional DEFERRED items separately, without inventing advance PASS counts.

Network guard: permit local GET/HEAD reads, narrowly enumerated normal authentication POSTs from the existing helper and necessary known static assets. Deny **all non-authentication POST/PUT/PATCH/DELETE**, including Risk mutations, preference saves and CPP print submission. Log sanitized method/path for every blocked request from the start. The chart's automatic CDS evaluation POST may be blocked as in Step 166; record its banner as a guard-induced limitation, not a Risk defect. Do not weaken the guard to make a check pass. A successful clinical mutation ends the run as FAIL and requires investigation; no compensating write is authorized.

## 5. Expected outcome classification and closure criteria

| Observation in future Step 169B | Classification / next action |
|---|---|
| Required D1–D4 assertions execute successfully; primary text and accepted implementation align | **PASS** for bounded deployment; recommend **PC07.07 SATISFIED — VERIFIED (qualified)** and separate formal tracker reconciliation |
| Missing source-release acceptance or clinician review only | Keep explicit governance/clinical qualifications; do not manufacture approval or describe tests as certification acceptance |
| Services unavailable, authorization absent, no suitable existing Risk records or inaccessible read-evidence prerequisite | **DEFERRED** for affected assertions; retain **PARTIAL — IMPLEMENTED**. Identify the exact prerequisite; no restart, fixture creation or permission change |
| No archived rows, no representative optional NULL/volume case, or saved preference intentionally hides a field | Record that specific optional deployed observation **DEFERRED/not exercised**, retaining isolated and accepted appearance evidence. Do not force extra scenarios as new PC07.07 requirements. Required positive display correlation still needs suitable existing data |
| Actual Risk GET fails, values/ownership disagree, required field/control absent, or clinical data changes attributable to the run | **FAIL** for that assertion; preserve evidence and stop. Distinguish a reproducible application defect from helper error before proposing a separately authorized implementation |

PC07.07 closure needs the recovered mandatory display/reference mapping, accepted four-field implementation and fixture verification, successful scoped deployment correlation and preserved qualifications. It does not require replaying CRUD, security attacks, preferences persistence or printing already accepted outputs. A read-only check cannot newly prove mutation behavior; rely on its named earlier evidence. No whole-PC07.01 closure, Family closure or inventory decrement follows automatically.

## 6. Readiness, effort and authorization

**Step 169B plan ready: YES, conditional on explicit execution authorization and successful live preflight.** Current service availability, existing Risk fixture suitability, account binding/permissions and saved preference state have not been queried in this documentation step. No operational readiness PASS is claimed.

Refined remaining engineering effort, assuming available running services and suitable existing fixtures:

| Work | Remaining active effort |
|---|---:|
| Requirement recovery | **Complete** for PC07.07 text; no source-path recovery wait remains |
| Helper adaptation and focused SQL/snapshot correlation | **1–2 hours** |
| One authorized automated session, captures and review | **0.5–1 hour** |
| Final disposition / evidence documentation | **0.5–1 hour** |
| **Total** | **2–4 hours**, excluding owner/external waiting |
| Implementation / migration / rebuild | **0 planned**; none justified by recovered wording/evidence |

External accepted-release evidence and future clinician review remain independent qualifications with no supported completion date. Owner decision needed: authorize **one normal development login plus read-only Chromium and SQL verification** against the fixed target, reusing existing records only. This report does not grant that authorization. If preflight lacks a fixture, any later creation/write needs its own approval and estimate; do not silently expand Step 169B.

## 7. Documentation and safety record

Current branch **main** retained. Existing untracked reports 233–235 preserved unchanged. Only this report added outside ignored extraction artifacts. Performed bounded local specification recovery, exact clause/font mapping inspection, relevant source/reference review and report whitespace/status checks. No browser execution, clinical login, authenticated patient read, database connection/write, test rerun, build, migration, restart, product edit, branch creation, commit, merge, push, reset, clean, stash or GitHub operation.

**PC07.07 remains PARTIAL — IMPLEMENTED. PC07.01 PARTIAL; 10 IDs / three packages unchanged:** prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). DE08.008's qualified closure and previously deferred security/Family/Risk qualifications remain intact. Stop after documentation.
