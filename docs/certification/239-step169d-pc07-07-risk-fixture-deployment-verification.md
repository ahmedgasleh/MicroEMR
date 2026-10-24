# Step 169D — PC07.07 synthetic Risk fixture and deployment verification

2026-10-24 (America/Toronto). **PASS. Recommend PC07.07 — SATISFIED — VERIFIED (qualified).** One normal authorized login and exactly one authorized normal Web creation succeeded. Real Chromium deployment checks: **10 PASS / 0 FAIL**, desktop 6 and mobile 4. **Clinical writes total: 1 Create; subsequent verification writes: 0.** Fixture retained Active. **PC07.01 PARTIAL; mandatory-gap inventory remains 10 IDs / three packages pending Step 170 formal reconciliation.**

## 1. Authority, authorization and evidence retained

PC07.07, Primary Care Baseline Requirements **1.7 Final, p.28**:

> The EMR Offering MUST display risk factors.

Use [Step 169A](236-step169a-pc07-07-requirement-recovery-verification-plan.md) for the recovered primary source, adjacent CDS-S guideline and bounded display/reference mapping. Reuse [Step 169B](237-step169b-pc07-07-authenticated-deployment-verification.md) for the prior empty-fixture blocker and [Step 169C](238-step169c-pc07-07-risk-fixture-readiness.md) for the exact approved fixture and audit/retention plan. No source re-extraction, completed test-suite run, broad repository scan, build or repeat appearance checklist.

Owner explicitly authorized one normal `admin@microemr.local` login, one Web Add submission with the four specified values on patient `9f65d4ca-c347-406a-bb32-c709a8941f80`, retention Active, SELECT-only SQL and bounded subsequent read-only Chromium. No update/archive/cleanup, other clinical save, direct SQL write, migration or service restart permitted or performed.

Retain Step 100's **35 .NET PASS / 0 FAIL**, **8 frontend PASS / 0 FAIL**, three owner-reported appearance PASS and isolated permission/tenant/patient/audit/rollback/concurrency evidence in their recorded scopes. They are not new results or extra Chromium checks in this report.

## 2. Preflight and synthetic-patient confirmation — PASS

Targeted probes: Web `https://localhost:7002/` returned **302** without following login; API `https://localhost:7003/health/platform` **200**; Auth `https://localhost:7179/.well-known/openid-configuration` **200**. Development certificate handling was limited to existing localhost probe/browser settings; security configuration unchanged. No full health or migration-ledger reassessment.

SELECT-only baseline at **2026-10-24T18:58:46.1067911Z** confirmed:

| Property | Actual result |
|---|---|
| Fixed SQL endpoint / database | `192.168.50.1,50013` / `MicroEMR_LocalDev_Fresh` |
| Tenant identity | `local-dev-fresh`, `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Existing patient | `9f65d4ca-c347-406a-bb32-c709a8941f80`, internal PatientId **6**, IsDeleted **false** |
| Existing selected-patient Risk rows | **0**; no equivalent marked fixture existed |
| Existing account binding | `admin@microemr.local`, active tenant clinical UserId **2**, subject binding matches the established account |
| Baseline audit boundary | AuditLogId **1127** |

Synthetic/test-only provenance comes from the project owner's standing AGENTS.md clarification that **all development patient/clinical data is fictional/synthetic/test data**, together with the verified development target and this same patient's previously established Step 166 identity. It was not inferred from the GUID or appearance of the name. No separate patient or fixture was created outside the authorized Risk row.

Relevant source definitions were checked against Steps 169A/C: four Risk fields, Add form/controller path, server actor, transactional Create audit, generated version and retained Active state. Database SELECTs successfully used those actual columns. No direct mutation procedure was invoked. SQL credentials remained in memory and were not printed.

## 3. Login, permissions and trusted actor — PASS

Used the established Playwright installation, normal visible Chrome, fresh browser context and verified helper command transport. Owner entered the password directly in Chromium. Exactly **one Login POST** occurred; expected tenant was explicitly selected and its rendered `microemr-tenant-id` value matched. No credential retrieval, cookie/token export, authentication bypass, saved browser state or automatic login retry.

Before creation, `#patientRiskFactorRoot` reported normal `data-can-manage=true`; authenticated scoped Risk collection read returned **200 / success=true**, establishing allowed read and exposed normal creation controls. SELECT at **2026-10-24T19:00:11.1234430Z** correlated new `PatientChartOpened` / `ClinicalRead` / `Succeeded` entries for this patient to **UserId 2**, matching the active subject binding. The later Create audit independently attributes the single successful mutation to the same trusted actor. No role, grant or actor record was altered to enable the feature.

## 4. Exactly one normal Web fixture creation — PASS

Actual chart route:

`https://localhost:7002/Patients/Details?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&tab=risk-factors`

Confirmed empty All collection immediately before Add. Used `#addRiskFactorButton`, filled only the four authorized clinical fields in `#riskFactorForm`, then clicked `#saveRiskFactorButton` **once**. The antiforgery-protected normal Web **POST `/patients/9f65d4ca-c347-406a-bb32-c709a8941f80/risk-factors`** returned **200 / success=true** with the server record. The normal Web→API/Application/Infrastructure path performed persistence and audit. No direct SQL modification or separate API-authentication path.

| Actual field | Exact stored value | Rendered correlation |
|---|---|---|
| RiskFactor | `TEST ONLY - tobacco exposure` | Exact text |
| ExposureDetails | `TEST ONLY - 2 cigarettes per day` | Exact text |
| LifeStageCode | `A` | **Adult**, under **Life stage at first exposure** |
| Notes | `TEST ONLY - synthetic risk-factor fixture - PC07.07 Step169C` | Exact text, independent of exposure |

Resulting fixture:

- RiskFactorUid **`8265897f-1b22-4bc9-9abc-fa7f5d40fa1b`**, correct patient/tenant, **Active**.
- CreatedBy **2**, CreatedAt **2026-10-24T19:00:15Z**; UpdatedAt/UpdatedBy remain NULL.
- RowVersion bytes **`0x0000000000002723`**, response Base64 **`AAAAAAAAJyM=`**, equal to SQL-returned version bytes.
- Exactly one normal creation request; its network allowance closed immediately after that request. No response ambiguity, second submission or repair.

SELECT validation at **2026-10-24T19:00:25.6237137Z** found precisely one new row and **one Create audit**, AuditLogId **1132**: EntityName `PatientRiskFactor`, EntityId matching new UID, PatientId **6**, UserId **2**, OldValue NULL and NewValue containing the four exact values plus Status `Active`. Audit timestamp **2026-10-24T19:00:15.412Z**. Its legacy PatientId/EntityId columns are the expected 0076 mutation-audit contract; structured PatientUid/ResourceUid fields are NULL on this Create, as defined by that procedure. This is not mistaken for missing attribution.

The SQL helper's version string was preserved losslessly as eight bytes and normalized to hex/Base64 for comparison; it was not compared as unlike encodings. Create validation passed before remaining browser checks began.

## 5. Real Chromium deployed verification — PASS

Actual deployed application, real Web GETs and rendered DOM; no API mocks or fabricated pages. Used the same authenticated context after creation. Each unique final check is counted once; creation/SQL checks are separate and not added to these browser totals.

| Unique final browser group | Desktop 1440 × 900 | Mobile 375 × 900 |
|---|---|---|
| Normal chart, selected tenant/patient, actual scoped GET and four-field SQL correlation | PASS | PASS |
| Read-only Active / All / Archived history filters | PASS | Not repeated |
| History labels, wrapping/visibility, contained layout and screenshot | PASS | PASS |
| Existing editor exact values/version, accessible names, keyboard order and Cancel | PASS | PASS |
| CPP four-field values and saved preference-control/visibility agreement | PASS | PASS |
| Print CPP Risk category visible without submission | PASS | Not repeated |
| **Final counts** | **6 PASS / 0 FAIL** | **4 PASS / 0 FAIL** |

### SQL-to-rendered correlation and history

Actual collection GETs returned one matching Active record with the correct patient UID, server Risk UID, actor and Base64 RowVersion. All four response properties matched SQL; rendered card `dt/dd` values matched the expected factor, exposure, **Adult**, and notes exactly. Both viewport runs reloaded real application data, preserving the newly created values.

Active and All views each returned/displayed **1** row. Archived GET returned **0** and the truthful empty-view text. No existing populated archived fixture was available; its content-specific display is not claimed as newly exercised. No archive was performed merely to manufacture that case; accepted isolated retention/archive tests remain evidence.

### CPP/preferences and print

Summary navigation and real preference GET succeeded. Saved Risk category is visible and **none of its four fields is hidden**. All four CPP values matched SQL and `data-cpp-field` targets; customization category/field checked states matched the saved preference response. The full-record navigation control was present with the existing Risk card. No checkbox was toggled and no preference form/default-restoration save occurred.

Opened the Print CPP disclosure and inspected its **Risk Factors** checkbox within `#cppPrintCategories`. **No PDF-generation POST, print form submission or new PDF output claim**. Retain existing full-source/print tests and owner V3 appearance acceptance; PC07.07's exact clause does not introduce a new print requirement.

The first print-control locator matched both customization and print checkboxes labelled Risk Factors. It was narrowed to the print fieldset; that assertion then passed. This was a helper selector correction, not an application defect. Previously passed desktop groups were retained; only the failed selector assertion and still-unexecuted mobile groups were continued. No extra clinical submission or login.

### Labels, keyboard and screenshots

All four editor inputs were discoverable by their associated accessible names: Risk factor, Exposure details, Life stage at first exposure, Clinical notes. Keyboard Enter opened the existing editor; Tab moved factor→exposure→life stage→notes, with Shift+Tab returning to life stage. Hidden UID/version and existing values matched the created baseline. Used **Cancel** to dismiss, respecting the modal's disabled Escape/backdrop behavior. Save was not clicked after creation.

Geometry checks and inspection confirm readable/wrapped clinical text, reachable controls and no document horizontal overflow at either width. Five representative screenshots captured and visually inspected in `.tmp/step169d/`: `risk-history-1440.png`, `risk-editor-1440.png`, `risk-history-375.png`, `risk-editor-375.png`, `risk-cpp-375.png`. Actual Bootstrap/sidebar transitions were settled for captures. These are focused deployed layout/name/focus checks, not a complete WCAG or physical-device certification.

## 6. Post-create integrity, audit and clinical writes — PASS

Final SELECT snapshot: **2026-10-24T19:02:35.4258915Z**. Exact selected-patient Risk rows, four values, status, timestamps, actors and RowVersion match the **post-create** snapshot. Fixture remains **Active**, version **0x0000000000002723 / AAAAAAAAJyM=**.

Full-row selected-patient Risk SHA-256 matches post-create→final:

`5F17D401878E2B53A203C5486DECBC8DDE55312D3A9A4B895354CBFEA3C5B94F`

Patient-row SHA-256 is unchanged pre-create→final:

`0996AE65596A34BC5249521CA2C5D3B8F3C9CAF6AABCCE6A48ABCCD4AF7EE63F`

The bounded audit-ID window after **1127** contains exactly **one clinical mutation**, Create **1132**, plus **20 PatientChartOpened / ClinicalRead / Succeeded** entries attributed to **actor 2** for the selected patient. No update/archive or other clinical mutation audit appeared. Direct SQL calls were SELECT-only. This is selected Risk/patient state and bounded audit evidence, not a whole-database content comparison.

Request guard allowed exactly the single authorized Web creation and normal authentication requests; all later clinical/non-authentication writes were denied. **Five automatic CDS-evaluation POSTs** were blocked and logged from the start; these are unexecuted requests, not clinical writes. The associated CDS-unavailable banner is guard-induced and outside Risk acceptance. No preference/print/update/archive submissions occurred. Credentials, cookies and tokens were not logged.

**Counts: one login; one authorized Add submission; one clinical Create; zero clinical writes after creation.** Baseline T0 empty→T1 one row is the deliberate approved change; unchanged-state acceptance uses T1→T2. No claim that creation itself was read-only. Fixture retained Active; no cleanup/deletion attempted. Browser/context closed cleanly through the existing helper's CLOSE command, without logout submission or exported authenticated state.

## 7. Recommendation, qualifications and Step 170

**Recommend PC07.07 — SATISFIED — VERIFIED (qualified).** The recovered mandatory display requirement and referenced four-field minimum are supported by retained implementation/isolated tests/owner appearance acceptance plus this normal-session tenant/actor, deployed SQL-to-UI/CPP correlation, representative layout and audit/version evidence. The earlier missing-fixture/deployed-read blockers are resolved within the authorized development scope. No product defect or implementation change was needed.

Preserve separately:

- Future clinician review is planned and not performed; technical acceptance is not clinical authority or external OntarioMD/CPSO acceptance.
- Accepted-release/source applicability remains qualified: Baseline folder 5.5 versus document 1.7 and CDS-S DFU folder/workbook distinctions. Original wording recovery does not establish external release acceptance.
- Live restricted-user denial, optional deployed omission-save and deliberate live stale-write remain deferred within their prior scopes. Existing isolated Risk permission/isolation/audit rollback/concurrency PASS remains valid; this one-session positive read/create is not those live adversarial tests.
- Family M7/M8 remains its own evidence lane; no Family closure from this Risk session. Optional NULL/date/volume and populated archived-view observations were not manufactured; retain their prior scope/exclusions.
- No new full PDF acceptance, general accessibility certification, production readiness or unrelated requirement closure.

**Proposed Step 170:** documentation-only formal PC07.07 requirement-level closure/inventory reconciliation using Steps 169A/C/D and retained Risk evidence. Apply the established top-level counting rules; consider removing only PC07.07 if that formal decision is supported, then select the next coherent remaining mandatory package. No testing replay, additional clinical login/write or automatic fixture cleanup needed.

**Inventory remains unchanged in this report:** prescribing PC04.01/.06/.07/.09/.14 **5**, CPP PC07.01/.04/.07/.10 **4**, scheduling PC09.03 **1** = **10 mandatory-gap IDs / three packages**. PC07.01 remains PARTIAL; DE08.008's qualified closure retained. No automatic decrement before formal closure.

## 8. Files, checks and stop

Current branch **main** retained. Existing untracked reports 233–238 preserved unchanged; only this report added outside ignored verification artifacts. `.tmp/step169d/` contains SELECT-only `read-state.ps1`, `before.json`, `authenticated.json`, `created.json`, `after.json`, adapted browser/helper command sources/results and five screenshots. No direct SQL modifications, product-code edits, branch creation, migrations, restart, build, completed suite rerun, commit, merge, push, reset, clean, stash or remote/GitHub operation. Focused browser/SQL/audit checks and final report whitespace/status review completed. Stop after verification and documentation.
