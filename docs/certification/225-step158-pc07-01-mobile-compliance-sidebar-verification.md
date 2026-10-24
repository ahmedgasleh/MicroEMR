# Step 158 — PC07.01 mobile compliance sidebar verification

2026-10-23. **Sidebar qualification: DEFERRED, not closed.** A new targeted read-only Chromium run passed 320 px direct-entry content/history checks, then stopped on a navigation-locator timeout. **1 check PASS / 1 automation FAIL; remaining checks not executed.** No demonstrated product defect or overall mobile PASS. Prescription, Medication Plan and Allergy owner acceptance remain PASS. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

## Scope, authorization and existing evidence

Read AGENTS.md, the relevant sidebar/acceptance context in reports [222](222-step155-pc07-01-gate2-clinical-smoke-results.md), [223](223-step156-pc07-01-owner-acceptance-and-evidence-consolidation.md) and the owner PASS/preference record in [224](224-step157-pc07-01-owner-acceptance-checklist.md). Reused Step 155 integration results without rerunning clinical saves or owner wording checks.

Current branch **`main`** retained. Initial Git status showed report 224 untracked; preserved it unchanged. No branch creation, product changes, commits, merges, pushes or other Git mutation. Only this report added outside ignored local verification code/evidence. No database access, migration, restart, credential retrieval, clinical save, permission change or integration-suite replay.

Identified the existing screen `/PatientMedications/Details?medicationUid=2521d27e-172f-47a1-9326-72eea8b0f571` and its `/Edit` counterpart. `PatientMedicationsController` requires authentication; `_AppLayout`, `_Sidebar`, `app-layout.css` and the compliance field partial are the immediate rendering dependencies. Mobile CSS uses a 991.98 px breakpoint, a 252 px sidebar, offscreen translate when closed and a **0.2-second transform transition**. This source inspection alone does not prove runtime behavior or diagnose Step 155's screenshot.

Anonymous access cannot exercise these protected clinical screens. Before any login, requested explicit permission for one normal interactive login as `admin@microemr.local`, restricted to read-only mobile checks on the existing plan. Owner answered **“Authorize one read-only login and test session”**, then confirmed the login window was visible. Exactly **one** normal login succeeded. This is not inferred from prior consumed login allowances.

## Targeted browser method

Added only ignored verification script `.tmp/step158/mobile.cjs`, using the existing installed Playwright driver and Google Chrome/Chromium with a fresh isolated context. No package install/build. No authenticated state, cookies, credentials or tokens exported. Network guard permits normal localhost reads and the single normal Auth flow; **all clinical/non-authentication submissions are blocked**. Existing Bootstrap icon CSS/font requests were allowed from their exact configured CDN asset prefix so the mobile menu's presentation could be observed without the earlier missing-icon qualification. No stylesheet/font request failures were recorded.

Planned only four new mobile verification groups at each width: direct Details/history geometry; sidebar keyboard/navigation/overlay closure; form labels/focus/client validation; settled desktop-to-mobile resize. A 350 ms settling interval follows navigation/viewport/menu changes, exceeding the inspected 200 ms transition. No screenshots or assertions are supplied by mocked clinical content. No clinical form is submitted, including for validation.

## Executed viewports and counts

| Viewport | Executed result |
|---|---|
| **320×900** | Direct entry/history PASS. Sidebar navigation group automation FAIL before completion |
| **375×900** | NOT EXECUTED — run stopped before this viewport |
| **430×900** | NOT EXECUTED — run stopped before this viewport |

**Counts: 1 completed group PASS / 1 automation FAIL; 10 planned groups not executed** (two remaining at 320 and four at each later width). Failed group is not equated with a proven product accessibility failure. No 375/430 or complete responsive-layout PASS claimed.

### 320 px direct-entry/history PASS

- Actual authenticated Details returned 200 and rendered the expected tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`.
- Closed sidebar measured **left -252, right 0, width 252 px**: no overlap with the clinical content.
- Compliance section measured left **34.8**, right **270.0**, width **235.2 px**; client width and scroll width both **234 px**. Document horizontal overflow false.
- Expanded history contained two revision entries. Each was scrolled into view; per-entry width/right-edge checks passed. Both current/revised content were accessible through vertical scrolling.

This supports 320 px direct-entry readability and history access only. The older 390 px resize qualification is not automatically closed by a new direct-entry check at a different width.

### Navigation automation failure

Keyboard activation of **Open navigation menu** initially produced the open-body class and a sidebar box within the viewport. The next locator failed:

```text
locator.focus: Timeout 30000ms exceeded.
waiting for getByRole('navigation', { name: 'Application navigation' })
  .getByRole('link', { name: 'Dashboard', exact: true })
```

The helper caught this error, captured a screenshot and closed its browser in finally. Navigation activation, overlay closing, forms/labels/focus/validation and resize checks therefore did not complete. No second login or automatic rerun attempted.

The exact role/name chain did not resolve in the live page. Repository `_Sidebar` contains an Application navigation label and Dashboard text, but **the live accessibility tree was not captured before closure**. An accessible-name difference, runtime/source variation or menu-state change cannot be conclusively distinguished from this evidence. Do not claim that Dashboard is absent, that navigation is inaccessible, or that a product fix is proven necessary.

## Screenshots inspected

Evidence directory: **`.tmp/step158/run-bV8EJX`**.

- **`details-320.png`**: current Unknown assessment, actor/time and both expanded revisions readable; sidebar hidden and clinical content fits. Vertical scrolling is expected for the long page.
- **`failure-320-sidebar-keyboard-navigation.png`**: captured after the timeout; shows the content and closed-sidebar appearance, not a successful open-menu/navigation interaction. It does not establish the cause of the locator failure.

Both screenshots were inspected. No visual-regression claim or approved baseline. No screenshots at unexecuted widths. `results.json` records **loginAttempts=1**, **clinicalSubmissions=0**, **blockedWrites=0**, **browserClosed=true**, and no asset failures. The final helper error was an automation timeout, not a rejected clinical request. Normal authenticated read/audit side effects remain governed by the application; no direct SQL operation or clinical mutation was performed.

## Proposed changes and next step

**Product-code changes: none proposed or authorized from this evidence.** Stay on the current branch for verification-only work. If later evidence establishes a product defect, stop and propose the smallest fix before creating an explicitly authorized implementation branch.

Proposed verification follow-up: resolve sidebar links using stable existing sidebar/link text or href structure rather than relying solely on the failed exact role/name chain; capture the live accessibility tree and menu geometry on a locator failure, and retain a recoverable browser session rather than closing it immediately. Then finish the still-unexecuted 320/375/430 groups and the resize scenario. Those are test-harness follow-ups, not proof that the application needs modification. No helper repair or second login was performed after this failure.

A further authenticated attempt would need renewed explicit login authorization; this step used its one approved login. Do not ask the owner to repeat the three accepted wording/layout checks. No additional manual clinical acceptance checklist is warranted.

**Sidebar qualification: DEFERRED, unresolved. PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Restricted-user denial, omission-save and stale-write scenarios remain separate; no new closure inferred. Reviewed this report and retained evidence; whitespace check passed. Stop after documentation.
