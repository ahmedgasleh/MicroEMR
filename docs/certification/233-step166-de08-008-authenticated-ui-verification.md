# Step 166 — DE08.008 authenticated immunization UI verification

2026-10-24 (America/Toronto), updated after owner restored services. **DE08.008: DEPLOYED — VERIFIED.** One normal authenticated login succeeded; final Chromium results are **16 PASS / 0 FAIL** (desktop 8, mobile 8). Existing immunization content and NULL doses are unchanged; clinical writes 0. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

Sections 1–8 retain the **initial blocked attempt as historical evidence only**. Section 9 records the resumed verification and supersedes that attempt's acceptance decision and recommended next step. No service was started or restarted by the verification helper.

## 1. Scope and retained evidence

Verification only, with owner authorization for one normal development login as `admin@microemr.local`, followed by read-only Chromium testing. No authentication bypass, credential retrieval, clinical submission, source change, build, migration, deployment or service restart performed.

Relevant retained evidence:

- [Step 162](229-step162-de08-008-immunization-dose-implementation.md): **49 automated PASS / 0 FAIL**, including **2 real Chromium PASS** in isolated fixtures. These are retained counts, not additional Step 166 browser results. Approved policy: amount > 0 and <= 9999.999, maximum three fractional digits; canonical case-sensitive local units `mL`, `mg`, `mcg`, `units`; both amount/unit present or both absent. Unknown historical dose remains NULL, without inferred values or backfill.
- [Step 165](232-step165-de08-008-post-deployment-verification.md): **22 deployment checks PASS / 0 FAIL**. Migration **0091 applied and verified**; legacy records preserved. No completed suite, migration or deployment assessment rerun. Step 165's service availability was a historical observation; the service absence below is the current blocker.

## 2. Login and tenant verification

Before opening Chromium, targeted read-only SQL verified the fixed endpoint `192.168.50.1,50013`, database `MicroEMR_LocalDev_Fresh`, and `dbo.TenantDatabaseIdentity`:

| Check | Result |
|---|---|
| Actual database and tenant identity | PASS: `MicroEMR_LocalDev_Fresh`, `local-dev-fresh` |
| Tenant UID | PASS: `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Existing clinical account binding | PASS: `admin@microemr.local`, active `ApplicationUser.UserId=2`, subject binding matches the previously established account |
| Normal login | NOT RUN: application connection refused before login; attempts **0** |
| Selected tenant in authenticated browser | NOT VERIFIED |
| Trusted clinical actor in authenticated runtime | NOT VERIFIED; database binding alone is not runtime proof |
| Effective authenticated read permissions | NOT VERIFIED; no authenticated read response available |

Initial SQL snapshot: `2026-10-24T16:57:48.6261451Z`. Connection information and any database credentials remained in memory; no credentials, cookies or tokens were recorded in evidence.

## 3. Actual Chromium attempt and counts

Reused the existing verified Playwright installation and helper patterns from `.tmp/step160/mobile-before.cjs`, together with the existing `.tmp/step153/command-transport.cjs` and Step 160 sidebar helper. Used actual Google Chrome with a fresh browser context, desktop viewport **1440 × 900**, and the established localhost development certificate handling. No mocked API responses or substituted pages.

The helper permitted only local read requests, narrowly enumerated normal authentication POSTs, and the existing Bootstrap icon assets. It limited login to one submission for the authorized username and blocked other mutation methods. No login submission or clinical write request occurred.

Actual application navigation:

`https://localhost:7002/Patients/Details?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&tab=immunizations`

Result: **`net::ERR_CONNECTION_REFUSED`**. Chromium closed cleanly. No automated login retry occurred.

| New Step 166 browser checks | PASS | FAIL | Incomplete scope |
|---|---:|---:|---|
| Desktop initial chart navigation | 0 | 1 | Connection refused before authentication/rendering |
| Mobile | 0 | 0 | Not run because application unavailable |
| **Total executed browser checks** | **0** | **1** | Remaining requested UI checks not executed |

This single availability failure is not counted again as multiple history, editor, mobile or accessibility failures. Retained Step 162 browser PASS results do not count as deployed authenticated checks.

Targeted corroboration outside the sandbox at `2026-10-24T16:58:56.8426027Z` found **no listeners** on expected Web **5002/7002**, API **5245/7003**, or authentication **7179** ports, and no processes named `MicroEMR.Web`, `MicroEMR.Api` or `MicroEMR.Auth`. Independent `http://localhost:5002/` probe also could not connect. This reproduces a development service availability blocker; it does not establish a dose implementation defect. No corrective code change is proposed on this evidence.

## 4. History, editor, controlled units and legacy compatibility

The selected existing patient is `9f65d4ca-c347-406a-bb32-c709a8941f80`; its existing influenza record is `942eff7c-afae-4306-bf3f-6ce913a5c63a`. SQL confirms its amount and unit remain NULL. All **four** existing immunization records across **two** patients still have NULL dose pairs.

Actual deployed history rendering, “Not recorded” display, editor opening, blank legacy controls, canonical option values/labels, pairing guidance and visible validation attributes remain **NOT VERIFIED in this step**. Retain Step 162's isolated component evidence and approved policy; inspected source markup is not substituted for authenticated rendering. No clinical values were entered and no form submitted.

## 5. Desktop/mobile, screenshots and accessibility

Desktop/mobile visibility, scroll reachability, responsive table behavior, body/modal overflow, accessible dose names/labels, focus order and keyboard navigation remain **NOT VERIFIED**. No application page rendered, so no representative application screenshots could be captured or inspected. A browser error image would not demonstrate the required UI behavior. Step 162's prior screenshot and accessibility findings remain limited to its isolated fixtures and do not close these deployed checks.

## 6. Read-only and audit confirmation

Final SQL snapshot: `2026-10-24T16:59:04.3203179Z`. It reconfirmed the expected identity and active actor binding. Before/after full-row SHA-256 over all `PatientImmunization` columns, including NULLs and RowVersions, matched exactly:

`180FC87DA8EAC071E14847C7184CF0940CE50478CA409EFCEE8350B8E2DB4B65`

All four record identities, content and write versions were unchanged during verification. A targeted `dbo.AuditLog` query from the initial snapshot timestamp returned **0 new entries**, including no clinical mutation entries. No authenticated chart read occurred, so no new read-audit event is claimed. Browser counters: login attempts **0**, clinical requests **0**, blocked writes **0**. **Clinical writes by this verification: 0.** No migration or write stored procedure executed.

The initial sandboxed SQL connection was denied by the network boundary before queries ran; the authorized read-only helper succeeded outside the sandbox. This tooling restriction was resolved and is separate from the reproduced application connection refusal.

## 7. Acceptance and remaining qualifications

**DE08.008: BLOCKED**, because unavailable development application services prevent the required authenticated verification. Previously verified database deployment, migration 0091, legacy preservation and integration behavior remain supported; no acceptance regression in dose behavior is inferred from service absence. The requirement cannot be classified DEPLOYED — VERIFIED until authenticated history/editor and responsive/accessibility checks complete.

Preserve future clinician review as separate and not performed. Preserve the previously deferred live restricted-user denial, optional deployed omission-save, deliberate stale-write and Family-specific qualifications. No new restore-readiness, backup or external certification claim. These qualifications are not closed by the read-only SQL observations.

**PC07.01 remains PARTIAL. Mandatory-gap inventory: prescribing 5 + CPP 4 + scheduling 1 = 10 IDs / three packages, unchanged.** No automatic parent-status change or inventory reduction.

## 8. Git, artifacts and recommended Step 167

Current branch was **`main`** at entry and remained unchanged; the historical branch recorded in Steps 162/165 was not assumed to be current. Initial working tree was clean. No branch creation, commit, merge, push, reset, clean, stash or remote Git operation. No product code changed. Only this report is added as a non-ignored repository file; local verification artifacts are under ignored `.tmp/step166/`:

- `read-state.ps1`, `before.json`, `after.json`: targeted SQL identity, actor binding, legacy snapshot and audit evidence.
- `browser.cjs`, `browser-results.json`: reused helper components and actual Chromium connection failure/counters.
- `availability.json`: independent listener/process and HTTP observations.

Focused checks: real Chromium initial navigation, targeted local availability probes, before/after SQL snapshot and audit query, report review and `git diff --check`. No completed test suites rerun.

**Recommended Step 167:** have the owner restore the normal matching development services, then resume the already authorized single normal login and remaining read-only Chromium checks against this same tenant/patient. The login allowance is unused. Do not rebuild, redeploy, rerun migration 0091, repeat the 49 integration cases or 22 deployment checks, or submit clinical values to demonstrate persistence. If a code fix becomes necessary, stop, document it and request approval before creating a dedicated local feature branch or editing product code.

Stop after verification and documentation.

## 9. Resumed authenticated verification — final disposition

### Login, tenant and actor

After the owner reported the applications running, targeted listener checks found Web 7002, API 7003 and Auth 7179 active. Reused the existing Playwright helper and its command transport; no completed deployment checks or integration suites were repeated. The original unused login authorization remained applicable.

Refreshed pre-login SQL identity/snapshot at **2026-10-24T17:12:09.6983199Z**, against the fixed approved development endpoint/database. Tenant remains `local-dev-fresh`, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`; active clinical actor **UserId 2** retains the previously established subject binding. The owner entered the password directly in Chromium. Exactly **one** normal login POST for `admin@microemr.local` occurred; no credential retrieval, token/cookie export or login retry. The helper explicitly selected the expected tenant and verified its rendered tenant meta value. The shell displays System Administrator rather than the login email; email text was not used as proof of identity.

The authenticated chart and real Web immunization collection GET returned **200**, with `success=true`, the expected patient UID and the exact existing immunization UID set. This demonstrates effective read authorization through the normal Web→API path. Read-audit actor attribution below corroborates trusted runtime actor binding without attempting a mutation.

### Final browser checks and findings

| Unique final check group | Desktop 1440 × 900 | Mobile 375 × 900 |
|---|---|---|
| Authenticated chart, tenant/patient and real immunization read | PASS | PASS |
| Legacy NULL history and responsive table/action reachability | PASS | PASS |
| Existing editor opens by keyboard with blank dose values | PASS | PASS |
| Controlled unit values/labels and accessible dose names | PASS | PASS |
| Pairing guidance and rendered numeric validation attributes | PASS | PASS |
| Amount→unit Tab and reverse Shift+Tab focus | PASS | PASS |
| Editor controls/help visibility, scrolling and screenshot capture | PASS | PASS |
| Escape dismissal without submission | PASS | PASS |
| **Final counts** | **8 PASS / 0 FAIL** | **8 PASS / 0 FAIL** |

History contains the selected patient's existing influenza record, correlated to SQL by UID, with administered dose **Not recorded**, independent ordinal **1**, and legacy refused indicator **Not recorded**. Editor opens as **Edit Immunization**, retains the existing vaccine, leaves amount/unit blank, and preserves the hidden legacy NULL refused indicator. No clinical field value was entered or changed.

Unit selection contains exactly blank/**Not recorded**, then **mL**, **mg**, **mcg**, **units**, with matching code/label pairs. Amount renders `type=number`, `min=0.001`, `max=9999.999`, `step=0.001`, `inputmode=decimal`; its help association resolves to visible text: leave amount and unit blank if unknown, up to three decimal places, no inferred dose. Dynamic invalid-value rejection and persistence were not re-exercised; retain Step 162 for those checks. No Save or error action was triggered.

Dose amount/unit are discoverable by their exact accessible names. Keyboard Enter opens the existing editor; amount receives focus, Tab moves to unit, Shift+Tab returns, and Escape dismisses. Geometry and browser hit testing confirm dose controls/help remain inside the viewport and unobstructed after scrolling. Desktop retains the rendered two-column editor; mobile stacks and wraps labels/help. The mobile history uses contained horizontal scrolling and its Edit action is reachable. No document horizontal overflow was detected. This is focused Chromium evidence, not a full WCAG or physical-device certification.

Captured and visually inspected **four** final screenshots under `.tmp/step166/`: `history-1440.png`, `history-375.png`, `editor-1440.png`, `editor-375.png`. Editor controls and help are readable at both widths; mobile table clipping is the expected horizontally scrollable container. History captures were refreshed after the Bootstrap tab fade completed; early transition captures are not final evidence.

The initial resumed probe incorrectly assumed `tab=immunizations` activated the tab. The existing controller normalizes that query to Summary. A second probe assumed button semantics; Bootstrap exposes the control as a tab. The final helper uses the real `[data-bs-target="#immunizations"]` UI control. Preliminary visibility failures were helper assumptions, not dose defects; final counts above are unique final checks and do not add preliminary attempts. No product code was changed or fix requested.

### Read-only and audit confirmation

The guard blocked **7 non-authentication write-method requests** during chart loads. Logged post-login requests were the chart's automatic `/patients/{patientUid}/cds/evaluate` POST; the first blocked request occurred before path logging was attached. None was permitted through the guard. The visible CDS-unavailable banner is expected with that request blocked and is not assessed as a CDS defect. Normal immunization GETs and editor inspection remained functional. No clinical form was submitted; no clinical write endpoint was permitted, no authentication bypass used, and no security configuration changed.

Final SQL snapshot at **2026-10-24T17:19:31.4780184Z** matches the refreshed pre-login full-row hash exactly:

`180FC87DA8EAC071E14847C7184CF0940CE50478CA409EFCEE8350B8E2DB4B65`

All **four records across two patients**, including full content, NULL dose pairs and RowVersions, remain unchanged. The only new audit group is **28 PatientChartOpened / ClinicalRead events attributed to UserId 2**; no clinical mutation audit entries appeared. Read entries include preliminary chart loads, so they are not a browser PASS count. **Clinical writes: 0. Login attempts: 1, successful.** Chromium closed through the helper's normal CLOSE command; no logout submission or saved authenticated context.

### Acceptance, qualifications and Step 167

**DE08.008: DEPLOYED — VERIFIED.** Final authenticated history/editor evidence, the deployed database and retained Step 162 integration evidence jointly support the approved dose requirement. Retain **49 automated PASS, including 2 isolated real-browser PASS**, and **22 Step 165 deployment PASS**; migration **0091 remains applied/verified**, never rerun. The initial service-availability blocker is resolved by the owner.

Future clinician review remains separate and not performed. Existing live restricted-user denial, optional deployed omission-save, deliberate stale-write and Family-specific qualifications remain open. The read-only session does not close those scenarios or establish external certification acceptance.

**PC07.01 PARTIAL; prescribing 5 + CPP 4 + scheduling 1 = 10 mandatory-gap IDs / three packages, unchanged.** Recommended **Step 167**: continue the next owner-approved metadata/source package from the retained Step 162 prioritization, keeping clinician review and deferred security scenarios separate. No additional dose persistence demonstration or repeat owner visual checklist is needed for this technical acceptance.

Only this report changed outside ignored local evidence. Current branch `main` and existing work were preserved; no source edits, build, migration, service start/restart, branch creation, commit or remote operation. Original unavailable-service artifacts were preserved under `.tmp/step166/initial-blocked/`; resumed `before.json`, `after.json`, `browser-results.json`, `ui-checks.json`, command source files and screenshots are in `.tmp/step166/`. Final diff/whitespace and status reviewed. Stop after verification and documentation.
