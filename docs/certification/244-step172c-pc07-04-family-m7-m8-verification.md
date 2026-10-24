# Step 172C — PC07.04 authenticated M7/M8 verification

**Current disposition after separately owner-authorized retry, 2026-10-24: M7 PASS; M8 PASS. Recommend PC07.04 SATISFIED — VERIFIED (qualified).** Two new normal logins succeeded, three expected-denied Web POSTs returned 403, five cross-scope GETs returned 404, and both Family rowsets/RowVersions stayed unchanged. Clinical writes 0. See section 9 for retry evidence. Sections 1–8 retain the original blocked attempt as historical evidence. Mandatory inventory remains 9 IDs / three packages pending formal reconciliation.

2026-10-24 (America/Toronto). **Verification BLOCKED. M7 BLOCKED; M8 BLOCKED.** Reader A's one normal login succeeded; Writer B did not establish an authenticated session after its one permitted submission. No denial POST or cross-scope GET executed. Both contexts closed; no retry. During the run the owner additionally reported **local-dev is not up to date**; matching deployment readiness is therefore unresolved. **Clinical writes 0. PC07.04 PARTIAL — IMPLEMENTED; PC07.01 PARTIAL; inventory 9 IDs / three packages unchanged.**

## 1. Scope and retained evidence

Owner authorized exactly one normal login per named account, five specified cross-scope Web GETs, up to three specified expected-denied Reader A Web POSTs, normal existing Family display, SELECT-only before/after checks and ordinary read/denial audit. No intended clinical writes, fixture/permission changes, additional login attempts, deployment updates or source changes authorized.

Reuse [Step 171](241-step171-pc07-04-family-history-evidence-reconciliation.md), [Step 172A](242-step172a-pc07-04-family-readiness.md) and [Step 172B's exact execution plan](243-step172b-pc07-04-m7-m8-readiness-resolution.md). PC07.04, Baseline 1.7 Final p.28, requires **“The EMR Offering MUST display family medical history.”** M7/M8 are internal acceptance scenarios. Six-field implementation, owner M1–M6 PASS, isolated security/audit/concurrency results and prior reported API denials remain valid in their named scopes; none rerun or promoted to deployed M7/M8 PASS.

## 2. Preflight observations and deployment qualification

Executed targeted status probes: Web root **302** without following authentication; API `/health/platform` **200**; Auth discovery **200**. Reused request-specific localhost development certificate handling; no security configuration change or service start/restart. These prove reachability, not matching/current deployment versions.

Fresh SELECT-only baselines confirmed both actual identities and existing records:

| Scenario | Actual tenant/database | Account, clinical actor and relevant permissions | Baseline Family state |
|---|---|---|---|
| A | **local-dev**, `8531aab9-e647-4d3f-852c-8fbcf9b4dd7b`; **MicroEMR_Db** | **a.reader@example.invalid**, clinical **8**, active subject binding; Patients.View allowed, ClinicalData.Manage denied | **3 rows**: populated Active F_A, existing Archived row, other-patient Active row |
| B | **local-dev-fresh**, `f94c6ecd-4417-483e-8a94-0507cf8384c2`; **MicroEMR_LocalDev_Fresh** | **b.writer@example.invalid**, clinical **5**, active subject binding; read/manage allowed | **1 populated Active row** F_B |

Both fixed development connections use **192.168.50.1,50013**. Actor bindings, Active memberships and effective profile/override permissions matched the plan. Auth metadata showed both accounts active and not locked out. Those metadata checks do not establish password correctness or successful OIDC login.

Existing record identifiers/versions remained:

- F_A **500e500f-c3a9-4d8a-bba1-fd091cf0a001**, patient **2509f6b0-430e-4503-a837-b9e25782122e**, Active, **0x00000000000065A2**. Six values: Mother; `TEST ONLY - type 2 diabetes`; `45 years`; A; `TEST ONLY - oral therapy reviewed`; `TEST ONLY - first editor saved`.
- A's retained Archived row **80b4da7e-0889-449e-ac89-9c54a9caa803**, **0x00000000000065A0**; other-patient row **118787dd-eb4b-4e17-9254-a4c0116f6eb0**, **0x000000000000659C**. Wrong-patient target **d44f99a3-467c-484a-8a50-b6e4785caddc** remains live.
- F_B **f491203c-da0e-477e-b5c5-7217d50ecf1b**, patient **1cf3493f-d4b1-4d75-a327-c39f50adb9db**, Active, **0x0000000000002712**. Six values: Maternal aunt; `TEST ONLY - asthma`; `12 years`; C; `TEST ONLY - inhaled therapy`; `TEST ONLY - Tenant B fixture`.

**Later owner steering:** “local-dev is not upto date.” Record this as an owner-reported deployment freshness issue, not an independently measured migration/artifact mismatch. No version-gap assessment was executed and no specific missing migration, stale assembly or code defect inferred. Its identity/schema/fixture/permission observations remain factual, but **current matching deployment readiness cannot be accepted from them**. No old-database update, migration or restart was attempted. This issue is separate from the Writer B authentication blocker; causation is not established.

The SELECT baselines completed and were saved. A strict-mode console-summary formatting error occurred after baseline validation/file save; only the helper's summary was corrected, without repeating the baseline queries or changing product code.

## 3. Authentication and session isolation

Used existing verified Playwright/Chrome installation and command transport, with **two separate fresh Chromium browser contexts**. Each context prefilled only its authorized username and had a per-account one-submission guard. Owner entered credentials directly in the browser; no password retrieval/output, cookie/token export, saved authenticated state or bypass.

| Account/context | Permitted login POSTs sent | Outcome |
|---|---:|---|
| Reader A / a.reader@example.invalid | **1** | **Successful** normal login; rendered tenant UID matched local-dev; actual Family root showed `data-can-manage=false` |
| Writer B / b.writer@example.invalid | **1** | **No authenticated session established**; navigation remained/returned at Auth `/Account/Login` and failed with `net::ERR_FAILED` |

The guard observed and **blocked a further B `/Account/Login` POST before transmission**. It was not allowed as a second normal login, and no subsequent retry was made. Exactly **two login submissions were transmitted, one per account; successful logins: one**. The helper did not capture the first B login's validation response, so an incorrect password, a specific account defect or an OIDC/application cause is **not established**. Do not describe that request-guard failure as a proven Family defect.

Helper stopped before entering its verification-command loop and closed the browser/contexts cleanly. Reader A's established context was not retained/exported for reuse. No session cookie or tenant claim was shared between contexts. B tenant/actor runtime binding remains **NOT VERIFIED**; its SQL/catalog binding alone is not runtime proof.

## 4. Exact planned request execution counts

| Request set | Authorized | Executed | Result |
|---|---:|---:|---|
| M7 Reader A collection/item/archive **Web POSTs** | **3** | **0** | NOT EXECUTED; no server-denial or side-effect assertion claimed |
| M8 cross-scope **Web GETs** | **5** | **0** | NOT EXECUTED; no 404/no-disclosure assertion claimed |
| Normal logins | Maximum **2**, one each | **2 submissions; 1 success** | B context blocked; no additional login transmitted |

Actual allowed initial chart navigation/read-audit is ordinary application navigation, not one of the five cross-scope probes. **No planned POST was assigned to M8** in Step 172B: all three belong to M7. No other clinical/denial test request was sent, and the earlier three reported API denials were not repeated.

One automatic Reader A CDS-evaluation POST was blocked by the read-only request guard. It was not executed as an authorized test or clinical write. The blocked further B login request is recorded separately, not counted as one of the three M7 POSTs.

## 5. M7/M8, display and capture disposition

- **M7 BLOCKED.** Reader A's permitted login, expected tenant metadata and restricted Family root flag were observed, but the six-value SQL-to-rendered correlation, complete UI-control assertion and exactly three server-denial requests never executed. Do not report M7 PASS from source definitions, hidden-root metadata or earlier reported API responses.
- **M8 BLOCKED.** No normal B session, positive B display/read correlation or five cross-scope GET results. No foreign-resource access attempt or disclosure occurred during an unexecuted test; absence of an attempt is not a 404/isolation PASS.
- **SQL-to-UI correlation NOT EXECUTED.** Existing SQL baseline values are confirmed, not newly correlated by rendered assertions. No new CPP/preferences/history/print presentation PASS.
- No representative application screenshots captured or independently inspected for this run; no screenshot/accessibility claim. Prior owner M1–M6 and scoped tests remain retained, not repeated.

## 6. SELECT-only final integrity and audit

The final state was captured in `.tmp/step172c/authenticated.json` after the helper had stopped/closed. This serves as the **final integrity snapshot**, despite its phase name. Both tenant identities remained correct; both complete Family rowsets/values/states/versions matched the pre-login baseline. Existing records and archived history were preserved.

| Database | Before/final complete Family SHA-256 | New audit window |
|---|---|---|
| MicroEMR_Db / A | `1EE78DFF7DB202010B9EF22C6088E0944B5CF37AB5A2702FFE3509E2066DAEC1` | After ID **319**: **4** PatientChartOpened / ClinicalRead / Succeeded entries; actor **8**, selected A patient; **0** Family mutations |
| MicroEMR_LocalDev_Fresh / B | `29D831140A7776A376EB33EC0D0E284B4A788428DCBFB45856C350B724BAAFDE` | After ID **1148**: **0** new clinical audit entries; **0** Family mutations |

New scoped platform security-denial events for the two subjects: **0**. No planned denial request ran, so no denial-audit PASS is claimed. Static review of the existing Web missing-permission audit handler also shows capability metadata is required; Family endpoints do not carry that sensitive-capability annotation. Do not invent a mandatory platform event for every Family 403. Any future denial run must document actual implemented audit behavior and unchanged clinical state.

**Clinical writes: 0; intended clinical saves: 0.** All direct database statements were SELECT-only. No clinical or audit modification, fixture change, cleanup, permission/membership change or compensating write. This is valid unchanged-state evidence for this limited run, not unchanged-state proof surrounding unexecuted M7 POSTs.

## 7. Recommendation, qualifications and proposed Step 173

**Retain PC07.04 — PARTIAL — IMPLEMENTED.** The deployed remaining assertions are blocked; a SATISFIED — VERIFIED recommendation is not supported by this run. No functional Family/security defect was demonstrated and no product change is proposed. Baseline metadata is compatible, but owner-reported local-dev freshness and B authentication must be resolved before retrying acceptance.

**Proposed Step 173:** resolve environment/authentication readiness under separately approved scope. Identify what local-dev being out of date means against the owner's intended matching build/database; do not infer a migration or run provisioning automatically. Owner updates, if required, need controlled deployment authorization outside this verification scope. Confirm Writer B's normal credential/session readiness privately without additional attempts under the consumed allowance. No password reset, grant, account change or fresh login is authorized here.

After those prerequisites are resolved, obtain renewed explicit authorization for the exact two named normal sessions and the still-unexecuted **three M7 POSTs / five M8 GETs**. Both prior contexts are closed, so a future run needs fresh authorized sessions. Refresh only safety identity/state/permission/audit boundaries, reuse fixtures and the exact plan; do not repeat M1–M6, completed suites or broad assessments. No fixture creation or permission repair is justified from current evidence.

Preserve clinician review, accepted release/source applicability and external OntarioMD/CPSO qualifications. Preserve historical reported API-denial capture limits, isolated-versus-deployed security boundaries, optional live no-view/nonmember/full-mutation qualifications and accepted Family appearance/print evidence. The owner freshness report does not reopen PC07.07/DE08.008's separate previously verified scopes or close PC07.01.

**Inventory unchanged:** 9 mandatory-gap IDs / three packages — prescribing PC04.01/.06/.07/.09/.14 (5); CPP PC07.01/.04/.10 (3); scheduling PC09.03 (1). Formal requirement closure/count reduction remains unperformed.

## 8. Files, safety and stop

Only this report added outside ignored `.tmp/step172c/` verification helpers/results: `services.json`, SELECT-only `state.ps1`, `before.json`, final `authenticated.json`, `browser.cjs`, `browser-results.json`. Current branch **main** retained; existing untracked reports 233–243 preserved unchanged. Final report whitespace/status review only after the limited verification. No tests/builds, product source edit, migration, restart/update, feature branch, commit, merge, push, reset, clean, stash or remote/GitHub operation. Stop after verification and documentation; no additional authentication or deployment action.

## 9. Separately authorized retry — M7 PASS / M8 PASS

After the owner supplied DatabaseTool output showing **local-dev: valid identity, 92/92 migrations, Current YES, latest 0091-immunization-dose, no missing/unexpected migrations or hash mismatches**, the owner requested another attempt. This was treated explicitly in commentary as authorization for one fresh login per previously named account and the same bounded M7/M8 request set. No automatic login retry occurred. No migration was applied by this verification. This resolves the measured schema-readiness concern; it does not independently certify every deployed artifact or establish the cause of the earlier B authentication failure.

### Preflight, sessions and correlation

Refreshed only required safety prerequisites and current integrity/audit boundaries, using the existing SELECT-only helper and the same constrained development connections. Web root returned **302**, API platform health **200**, Auth discovery **200**. Both tenant identities, existing fixtures, active actor bindings, account activity/lockout metadata, memberships and effective permissions matched Step 172B. No fixture/account/permission changes.

The owner entered passwords directly into Chromium. Exactly **one normal login submission per account**, **two successes**, in separate fresh contexts:

| Context | Verified tenant/database | Account / actor | Effective behavior |
|---|---|---|---|
| A | local-dev / MicroEMR_Db | a.reader@example.invalid / **8** | Patients.View allowed; ClinicalData.Manage denied; management controls absent |
| B | local-dev-fresh / MicroEMR_LocalDev_Fresh | b.writer@example.invalid / **5** | Patients.View and ClinicalData.Manage allowed; existing management controls rendered, never submitted |

Rendered tenant metadata matched each expected UID. Per-tenant read-audit entries independently correlated the trusted runtime clinical actors and expected patient IDs. Session material was never shared or exported.

All **six fields per existing fixture** matched fresh SQL and real rendered Family cards, including **A → Adult / C → Child** labels. Both own-patient/resource detail GETs returned **200** with the correct UID and six stored values. Each CPP Family summary's six DOM values matched its local fixture, and both existing `/cpp/display-preferences` GETs returned **200**, without saving preferences. CPP content checks were DOM/read checks; no new full CPP visual or print acceptance claim is made. Prior owner M1–M6/print acceptance remains retained.

### M7 exact server denials — 3/3 PASS

Reader A alone sent these requests once each, with its own valid antiforgery material, existing six-field payload and freshly captured Base64 RowVersion where relevant. No token values were recorded.

| Method and path | Result |
|---|---|
| POST `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history` | **403** |
| POST `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **403** |
| POST `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001/archive` | **403** |

After **each** response, SELECT-only complete Family fingerprints and audit checks passed in **both** databases before the next request was permitted. No unexpected success, clinical mutation or version change. These are actual Web authorization responses, not client guard rejections or repeated historical API tests.

### M8 normal-session routing — 5/5 PASS

Exactly the five Step 172B routes were executed once, in their assigned contexts:

| Context | GET path | Result |
|---|---|---|
| A | `/patients/d44f99a3-467c-484a-8a50-b6e4785caddc/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **404** |
| B | `/patients/1cf3493f-d4b1-4d75-a327-c39f50adb9db/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **404** |
| B | `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **404** |
| A | `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/f491203c-da0e-477e-b5c5-7217d50ecf1b` | **404** |
| A | `/patients/1cf3493f-d4b1-4d75-a327-c39f50adb9db/family-history/f491203c-da0e-477e-b5c5-7217d50ecf1b` | **404** |

Each response was checked for absence of fixture clinical text, Family UID property and RowVersion property. Selected tenant metadata stayed correct after each probe. Positive local detail reads established that actual existing records were tested. No live foreign mutation or nonmember scenario was added.

### Desktop/mobile and accessibility

Both contexts passed displayed-card visibility and document-width checks at **1440×900** and **390×844**. Four screenshots were captured and visually inspected: `.tmp/step172c-retry/A-desktop.png`, `A-mobile.png`, `B-desktop.png`, `B-mobile.png`. Six-field content remained readable, mobile text wrapped, vertical scrolling worked, and chart tabs used their existing internal horizontal scroller without page-wide overflow. Writer B's narrow Add button wraps across lines but remains visible; Reader A has no Add/Edit/Archive controls. Both status selectors were found by their accessible name **Family history status**, accepted focus, and allowed Tab navigation. This is bounded accessibility evidence, not a comprehensive accessibility certification.

Automatic CDS-evaluation POSTs were blocked in both contexts by the existing read-only guard. A screenshot consequently shows the CDS-unavailable notice; no CDS result or defect is inferred. No Family/CPP preference or print submission occurred.

### Integrity, audit and cleanup

Baseline **2026-10-24T19:52:47.0295495Z**; final SELECT snapshot **2026-10-24T19:55:29.5209472Z**. Snapshots: `.tmp/step172c-retry/before.json`, `authenticated.json`, `post1.json`, `post2.json`, `post3.json`, `after.json`; browser evidence `browser-results.json`.

| Database | Complete Family SHA-256 before/after | Final audit window |
|---|---|---|
| MicroEMR_Db | `1EE78DFF7DB202010B9EF22C6088E0944B5CF37AB5A2702FFE3509E2066DAEC1` unchanged | After **323**: **324–327**, four successful PatientChartOpened / ClinicalRead entries, actor **8**, A patient |
| MicroEMR_LocalDev_Fresh | `29D831140A7776A376EB33EC0D0E284B4A788428DCBFB45856C350B724BAAFDE` unchanged | After **1148**: **1149–1152**, four successful PatientChartOpened / ClinicalRead entries, actor **5**, B patient |

Both complete rowsets, all clinical values/states and every RowVersion remained unchanged at each post-request checkpoint and final comparison, including A's archived/other-patient rows. F_A remains **0x00000000000065A2**; F_B **0x0000000000002712**. **Zero Family mutation audits; zero clinical writes.** Scoped new platform security-denial events: **0**. As established in section 6, these Family endpoints do not carry the sensitive-capability metadata needed for that handler's platform event; absence is recorded as existing behavior, not invented denial-audit coverage.

The first helper CLOSE command contained a trailing newline, so the command transport treated it as JavaScript and returned ReferenceError without an application request. A corrected exact CLOSE command succeeded; both browser contexts closed, process exited **0**, and no authenticated state was saved. No authentication or clinical request was repeated because of this local transport issue.

### Acceptance recommendation and next step

**M7 PASS; M8 PASS. Recommend PC07.04 — SATISFIED — VERIFIED (qualified).** The exact display requirement is supported by retained implementation/owner evidence and this authenticated deployed six-field correlation; the bounded remaining Web permission and session-routing assertions now have real server outcomes and unchanged-state evidence.

Preserve clinician review, release/source applicability and external OntarioMD/CPSO qualifications; historical API-capture limitations and optional live no-view/nonmember/full mutation scenarios remain qualified. The current bounded run does not certify unrelated requirements or prove all deferred security scenarios. Writer B succeeded on this authorized attempt; the cause of its earlier failed attempt remains undetermined.

**Proposed Step 173: documentation-only formal PC07.04 closure and requirement-count inventory reconciliation.** Do not repeat these checks. Inventory is **still 9 IDs / three packages**, pending that formal decision: PC04.01/.06/.07/.09/.14; PC07.01/.04/.10; PC09.03. PC07.01 stays PARTIAL. No automatic removal or tracker update in this retry.

Only this existing report was updated outside ignored retry helpers/results. Previous blocked-attempt evidence and all other uncommitted work were preserved. No product-code edit, build/suite rerun, migration, service restart, fixture/permission change, branch, commit, merge, push, reset, clean, stash or remote operation. Verification and documentation complete; stop.
