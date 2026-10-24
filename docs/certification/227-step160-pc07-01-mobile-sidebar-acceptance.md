# Step 160 — PC07.01 mobile sidebar acceptance

2026-10-23. **Sidebar qualification CLOSED — VERIFIED** at 320, 375 and 430 px. **Credential-free gate: 5 PASS / 0 FAIL. Real authenticated Chromium: 12 PASS / 0 FAIL.** One normal login, zero clinical submissions or value changes. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

## 1. Authorization, Git checkpoint and bounded changes

Read AGENTS.md, the explicit Step 160 instruction and relevant failure/repair details in reports [225](225-step158-pc07-01-mobile-compliance-sidebar-verification.md) and [226](226-step159-pc07-01-mobile-sidebar-verification.md). Owner authorized the bounded helper correction and ONE read-only normal login conditional on a passing credential-free gate. Previous clinical integration suites and the three accepted owner wording/layout checks were not repeated.

Recorded once before edits: branch **`main`**, HEAD **`c5f5fe1660d7110074cd188d9d38cf9a89ea6ace`**. Reports 224–226 were untracked and preserved unchanged. Created/switched to expressly requested LOCAL branch **`feature/step-160-mobile-sidebar-helper-correction`** before helper edits. No commit, merge, push, reset, clean, stash or remote operation. No product UI/route/permission/clinical/SQL-object changes, migration, restart or build.

Infrastructure changes only, ignored under `.tmp`:

- **Modified `.tmp/step158/mobile.cjs`:** use the structural sidebar target and uniqueness check; replace the arbitrary 350 ms settling delay with bounded geometry/animation-state waits; capture sidebar geometry/accessibility diagnostics on failure. Add a credential-free mode and passing-gate check before any login. Record existing validation containers/text without changing any field values or submitting forms. Preserve all non-authentication submission blocking.
- **Added `.tmp/step160/sidebar-tools.cjs`:** shared target selection, sidebar settling and keyboard navigation completion used by both credential-free and live tests.
- **Added `.tmp/step160/credential-free.cjs`:** focused anonymous verification of those shared functions through the actual existing publisher/dispatcher/Chromium/result path. Before-copy retained as `mobile-before.cjs`.

No changes to Step 154's command schema, atomic publisher, lock-tolerant reader, shared dispatcher or UTF-8 result transport. Only this report is a new tracked deliverable.

## 2. Navigation correction and credential-free gate — PASS

The old exact parent-navigation/link accessible-name chain was replaced with `#appSidebar a.sidebar-link` filtered by its descendant `.sidebar-text` matching Dashboard. Exactly one match is required; missing/duplicate targets produce counts and sanitized sidebar-link diagnostics. Navigation now focuses that link, presses Enter on the link itself, and waits for the actual destination URL and visible main content.

Sidebar settling waits for fonts, completed sidebar/wrapper animations, expected open/closed position and mobile wrapper alignment. It does not assume a fixed elapsed time guarantees the layout. Short command-file publication polling in the credential-free harness is I/O synchronization, not a visual delay.

**Five focused checks PASS, zero FAIL**, before any login:

1. Unique structural target selection without requiring a named navigation parent.
2. Missing-target diagnostic.
3. Duplicate-target diagnostic.
4. Actual PowerShell publisher → unchanged command file/dispatcher → real Chromium keyboard navigation and visible destination → exact returned UTF-8 result.
5. Clean browser shutdown.

The anonymous fixture verifies helper behavior, not the deployed clinical screen. The subsequent live run separately establishes the deployed correction. No clinical/Auth requests or credential retrieval in this gate. Evidence: **`.tmp/step160/gate-RNjpeK/results.json`** and `.tmp/step160/gate.json`. All relevant checks passed on the first gate run; the conditional login authorization was then used.

## 3. Login, tenant and read prerequisites — PASS

At **2026-10-23T20:30:51.1016652+00:00**, a narrowly scoped read-only metadata check confirmed the fixed development database identity, active `admin@microemr.local` binding to **clinical actor 2**, and active **Patients.View** assignment. Only tenant/account/permission metadata was queried; no patient records or schema objects changed. Existing configured SQL connection bindings were used in memory, never printed or persisted. Evidence: `.tmp/step160/read-prerequisites.json`.

Opened the single authorized normal interactive Chromium login outside the sandbox. Exactly **one** credential submission succeeded; no retry, impersonation, reset or bypass. Clinic selection and every medication Details/Edit navigation were checked against tenant UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`** (`local-dev-fresh`). Actual API-backed read pages returned 200; existing API read authorization requires Patients.View. Edit patient UID matched **`9f65d4ca-c347-406a-bb32-c709a8941f80`** for plan **`2521d27e-172f-47a1-9326-72eea8b0f571`**.

No credentials, tokens, cookies or browser state exported. Same fresh authenticated context served all widths and then closed normally.

## 4. Live browser results by viewport — 12 PASS / 0 FAIL

Used actual installed Google Chrome/Chromium through the existing Playwright driver, production pages and their normal requests. No mocked API responses or altered layout markup. Viewports were **320×900, 375×900 and 430×900**, plus 1440×900 as each desktop-to-mobile resize origin.

| Targeted group | 320 px | 375 px | 430 px |
|---|---|---|---|
| Direct entry, readable compliance content and both retained revisions accessible by scrolling | PASS | PASS | PASS |
| Keyboard menu opening, unique Dashboard focus/Enter navigation, return to plan, open sidebar and overlay closure | PASS | PASS | PASS |
| Compliance control visibility, labels/descriptions, keyboard focus order and existing validation presentation | PASS | PASS | PASS |
| Settled desktop-to-mobile resize, hidden sidebar and fitted content | PASS | PASS | PASS |

Measurements:

- Closed sidebar at every mobile width: **left -252, right 0, width 252 px**; no overlap with main content.
- Open sidebar: **left 0, right 252 px** at all widths, leaving an operable overlay region. Dashboard navigation completed and main content was visible; overlay closure restored the unobstructed page.
- Compliance section bounds: **34.8–270.0 px** at 320, **34.8–325.2 px** at 375, **34.8–380.4 px** at 430. Client/scroll widths matched within tolerance: **234/234**, **289/289**, **344/344 px** respectively.
- Document horizontal-overflow assertions passed. Revision entries and all three controls were individually scrolled into view and checked against viewport bounds; no material clipped clinical content.
- Compliance state, assessment-time and explanation controls each had an associated label and valid described-by references. Tab advanced state → time → explanation. The mobile-menu accessible name worked for keyboard opening; Dashboard received focus and navigated via Enter.
- Existing three compliance validation containers were present with `role=alert`, and were empty in the valid record state. No invalid values were entered or errors induced; server-side validation and error announcement behavior are not newly tested here.

The original locator failure did not recur with the structural target. Its historical accessible-name mismatch remains unproven; this successful live correction is not a claim that an application defect was fixed.

## 5. Screenshots and accessibility review

Captured and inspected **12 screenshots** in **`.tmp/step160/live-VWpygf`**, four at each width:

- `details-320.png`, `details-375.png`, `details-430.png` — readable current assessment and expanded history; both revisions accessible through vertical scrolling.
- `sidebar-open-320.png`, `sidebar-open-375.png`, `sidebar-open-430.png` — full sidebar labels and Dashboard target visible; expected temporary overlay over main content, with closing interaction verified.
- `controls-320.png`, `controls-375.png`, `controls-430.png` — state/time/explanation controls, labels and help fit and remain usable.
- `resized-320.png`, `resized-375.png`, `resized-430.png` — settled resize no longer shows the partially visible sidebar/content squeeze from the earlier qualification.

No stylesheet/font request failures. Existing Bootstrap icon assets loaded from their configured static CDN prefix; no product asset replacement. The Edit screenshots show an empty red summary region above the fields; it does not obstruct the required controls. No validation text was present, so no assertion of an actual clinical error or newly verified error semantics is made.

This is focused Chromium viewport/keyboard/DOM evidence, not a complete WCAG, physical-device or screenshot-regression certification. No approved screenshot baseline exists. The three owner wording/layout PASS decisions are retained without asking the owner to review again.

## 6. Read-only confirmation and disposition

Live evidence **`.tmp/step160/live-VWpygf/results.json`** records:

```text
loginAttempts: 1
clinicalSubmissions: 0
blockedWrites: 0
12 live checks: all pass
assetFailures: []
browserClosed: true
```

No save/submit control activated, assessment or other field value changed, clinical API mutation or SQL write performed. Network guard blocked the possibility of non-authentication submissions. Ordinary authorized page reads may generate existing governed read-audit entries; their exact count was not queried or represented as zero. The metadata prerequisite query was read-only and separate from clinical view/audit activity.

**Sidebar qualification: CLOSED — VERIFIED** for required content/control usability at all three widths, including direct entry, menu interaction and settled resize. This supersedes the mobile qualification/deferred outcome in reports 222–226; those historical reports remain preserved. No product-code fix is required by this sidebar evidence.

Other deferred scenarios remain separate: live restricted-user denial, optional deployed omission-save and intentional stale-write tests were not run. Prior isolated security/concurrency PASS results and Step 155 integration evidence remain retained.

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Closing this subordinate qualification does not close the remaining requirement-level gaps or remove a top-level ID.

Recommended **Step 161:** proceed to an actually unfinished PC07.01 package from report 223, such as resolving the recorded administered-dose policy prerequisite for DE08.008 before bounded implementation. Do not repeat this closed mobile verification, the three owner acceptances or the successful clinical smoke absent a relevant change/new defect. No further clinical action authorized here.

Reviewed the bounded helper diff and evidence; `git diff --check` passed. No broad suite, unrelated fix or subsequent test rerun after passing completion. Stop after documentation.
