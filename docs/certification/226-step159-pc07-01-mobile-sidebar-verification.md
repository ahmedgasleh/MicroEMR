# Step 159 — PC07.01 mobile sidebar verification

2026-10-23. **DEFERRED — stopped at the helper-change and authentication authorization boundaries.** No new browser test, login or clinical write. Step 158's successful 320 px direct-entry/history evidence retained. No application defect established. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

## Inspection and Git scope

Read AGENTS.md, [report 225](225-step158-pc07-01-mobile-compliance-sidebar-verification.md), the failed helper/retained result, the existing sidebar markup and the medication controller's authorization attribute. No broad search or prior suite rerun. Current branch **`main`** retained; reports 224 and 225 were untracked and preserved unchanged. No branch creation, helper/product edit, Git mutation/remote operation, database access, credential retrieval, login, migration, restart or clinical request/write. Only this report added.

## 1. Timeout cause and invocation blocker

The exact hardcoded locator at `.tmp/step158/mobile.cjs:68` is:

```js
page.getByRole('navigation', { name: 'Application navigation' })
    .getByRole('link', { name: 'Dashboard', exact: true })
```

**Immediate cause:** that exact role/name chain did not resolve within 30 seconds. The live accessibility tree was not retained, so the underlying runtime mismatch remains undetermined. Repository `_Sidebar.cshtml` contains `#appSidebar`, `a.sidebar-link`, a descendant `.sidebar-text` with Dashboard text and a navigation label. Source markup does not establish the deployed accessible name or menu state. Do not classify the timeout as missing/inaccessible application navigation.

The helper has only the `--authorized-read-only` gate; **no selector override or command-dispatch interface**. Its finally block closed the prior browser. There is no supported invocation-only way to replace this locator or resume that session. Running the same command would repeat the failing assumption and attempt a new login without authorization.

Step 159 explicitly says **“If the existing test helper requires a code change, STOP and report the smallest proposed fix.”** Accordingly, no helper edit was made.

## 2. Smallest proposed correction — not applied

Propose replacing only the brittle navigation locator with the existing sidebar/link/text structure, followed by a uniqueness assertion before focus:

```js
const dashboard = page.locator('#appSidebar a.sidebar-link').filter({
  has: page.locator('.sidebar-text', { hasText: /^\s*Dashboard\s*$/ })
});
assert.equal(await dashboard.count(), 1);
```

This avoids depending on the parent navigation accessible-name chain. It is a candidate selector based on inspected markup, **not a proven deployed correction**. The resulting link's real accessible name/focus/navigation must still be checked. No product-code fix is proposed.

For the same narrowly approved helper work, replace its existing arbitrary 350 ms delay with bounded conditions for actual sidebar position/open state and content geometry. Retain sidebar DOM/accessibility diagnostics on failure before closing the browser, so another timeout is explainable. These are proposed test-helper edits only and require explicit approval first; any required local implementation branch must also wait for that approval.

## 3. Credential-free feasibility and authentication boundary

`PatientMedicationsController` is `[Authorize]`; the compliance Details/Edit and permission-filtered sidebar require a normal authenticated context. An anonymous login page cannot establish this screen's navigation, history or control accessibility. No prior cookies/tokens/session state were saved, and the Step 158 browser is closed. No alternative browser profiles, credentials or authentication bypass were sought.

Before a future login, the proposed selector can be exercised against anonymous nonclinical markup with the same sidebar structure to check selector semantics. Such a fixture would not prove deployed navigation. That test requires an approved corrected helper/harness; **it was not executed under this stop instruction**. No credential-free result is invented or substituted for the protected clinical page.

After the helper correction/credential-free check, completing the actual screen needs **new explicit authorization for ONE normal interactive login as `admin@microemr.local`**, tenant `local-dev-fresh`, read-only on existing plan `2521d27e-172f-47a1-9326-72eea8b0f571`, patient `9f65d4ca-c347-406a-bb32-c709a8941f80`. The previous allowance was consumed. No login attempted or authorization inferred here.

## 4. Exact remaining read-only browser plan

Use `https://localhost:7002/PatientMedications/Details?medicationUid=2521d27e-172f-47a1-9326-72eea8b0f571` and the corresponding `/Edit` route, within one newly authorized session. Block all clinical form submissions; no direct API mutations or SQL access.

| Viewport | Remaining targeted checks |
|---|---|
| **320×900** | Reuse completed direct-entry/history PASS. Finish sidebar open/closed geometry, keyboard opening, unique Dashboard focus/Enter navigation and overlay closure; form labels/descriptions, focus order and no-submit client-validation layout; desktop-to-mobile resize with condition-based settling |
| **375×900** | Direct-entry sidebar/content geometry, horizontal and nested clipping, both revisions accessible by scrolling; the same sidebar/navigation, form/labels/focus and settled resize checks |
| **430×900** | Same remaining checks as 375 px |

Capture/inspect sidebar open/closed states, mobile compliance/history, form/validation and settled resize screenshots where needed. Check actual content bounds and scrolling, not only document overflow. If a reproducible application defect appears, record it and propose the smallest product fix before editing. Preserve the three existing owner PASS decisions and Step 155 integration evidence; no repeated clinical saves or broad suites.

## 5. Results, screenshots and disposition

**Step 159: 0 new browser groups PASS / 0 FAIL; execution DEFERRED.** No new viewports completed and no screenshots captured. Retain Step 158 separately: **1 group PASS / 1 automation failure** at 320 px; 375/430 not reached. Do not count those historical results as new tests.

Reused screenshot findings from report 225: `details-320.png` showed readable current/history content with closed sidebar right edge 0 and no measured horizontal clipping; `failure-320-sidebar-keyboard-navigation.png` showed a closed-sidebar appearance after timeout, not successful navigation. No new screenshot inspection or full mobile PASS claimed.

**Sidebar qualification: DEFERRED, not CLOSED and not a demonstrated application layout defect.** The Step 158 raw helper result's `OPEN` flag represented its generic exception path; the evidence-based disposition remains DEFERRED, as report 225 already recorded.

**Required changes:** none to product code; proposed helper locator/condition/diagnostic correction above awaits approval. Recommended next step: explicitly approve that bounded helper correction, validate the navigation selector without credentials where possible, then authorize a single read-only login/session for only the remaining mobile checks. Do not consume another login by rerunning the unchanged helper.

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Prescription, Medication Plan and Allergy owner wording/layout remain PASS. No new requirement closure, clinical acceptance replay or inventory reduction. Report whitespace/source reference checks completed. Stop at authorization boundary.
