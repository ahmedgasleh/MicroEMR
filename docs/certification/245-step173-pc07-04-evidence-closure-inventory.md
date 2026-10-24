# Step 173 — PC07.04 evidence closure and mandatory-gap inventory

2026-10-24 (America/Toronto). **PC07.04 — SATISFIED — VERIFIED (qualified). Mandatory-gap inventory: 9 → 8 distinct IDs; three → three nonempty packages. PC07.01 remains PARTIAL.** Documentation-only reconciliation of accepted evidence; no verification repeated. Next recommended requirement: **PC07.10**, starting with exact requirement/evidence reconciliation and a bounded remaining-work plan.

## 1. Exact requirement and formal closure

**Primary Care Baseline Requirements, version 1.7 Final, March 20, 2026, section 2.7, page 28 of 53, PC07.04, Mandatory:**

> The EMR Offering MUST display family medical history.

[Step 171 section 1](241-step171-pc07-04-family-history-evidence-reconciliation.md) records the recovered authoritative PDF, source provenance and adjacent CDS-S Data Dictionary guideline. Its source is [Primary Care Baseline - 1.7 Requirements.pdf](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>). No source re-extraction was needed here. M7/M8 are adopted internal acceptance scenarios, not additional OntarioMD clauses or separately counted requirement IDs.

| Closure assertion | Accepted evidence | Disposition |
|---|---|---|
| Display actual family medical history, distinct from the patient's own Problems | Step 171 sections 1–3: six independent fields and accepted dictionary mapping; Step 172C section 9: real authenticated chart cards in both tenants correlate with tenant SQL | SATISFIED — VERIFIED in recorded development scope |
| Six-field minimum, known/unknown semantics and storage/retrieval | Step 171 retains Relationship, ConditionDescription, AgeAtOnset, CT-016 LifeStageCode, Treatment and Notes; prior SQL/contracts and owner M1/M2/M4. Step 172C correlates all six populated values per fixture, including Adult/Child labels and positive owned-resource detail GETs | SATISFIED — VERIFIED; no new field or persistence test needed |
| CPP, preferences, history/state and print coverage | Step 171 retains owner M1–M6, CPP/preferences/full selected-print and archive/stale/audit coverage. Step 172C confirms six CPP summary DOM values and successful preference reads in both contexts | SATISFIED — VERIFIED in respective prior and deployed read scopes; no new preference-save or PDF claim |
| M7 restricted-reader deployed acceptance | [Step 172B sections 3/5](243-step172b-pc07-04-m7-m8-readiness-resolution.md) defines Reader A and exact Web requests; [Step 172C section 9](244-step172c-pc07-04-family-m7-m8-verification.md) records permitted reads, absent management controls and **three actual Web 403 responses**, with both databases checked after each | **M7 PASS**, bounded adopted scenario |
| M8 normal-session tenant routing | Step 172B defines actual tenants/resources and five requests; Step 172C section 9 records **five Web 404 responses**, no checked fixture/UID/version disclosure, unchanged selected context and positive local reads | **M8 PASS**, bounded GET/session scenario |
| Authenticated deployment, actors and presentation | Step 172C section 9: two normal successful logins, isolated contexts, expected tenants, actors **8 / 5**, six-value SQL-to-rendered correlation, desktop/mobile visibility, four inspected screenshots and named status-control focus/Tab checks | SATISFIED — VERIFIED for these assertions; bounded accessibility only |
| Integrity and audit | Step 172C complete Family fingerprints/RowVersions unchanged in both databases after each denied POST and finally; eight expected chart-read audits, zero Family mutation audits and **zero clinical writes** | SATISFIED — VERIFIED for this run |

**Formal decision: PC07.04 — SATISFIED — VERIFIED (qualified).** The exact mandatory display clause, accepted minimum and previously outstanding adopted M7/M8 deployment assertions now have sufficient requirement-specific evidence. This is an internal evidence closure, not external OntarioMD certification or full CPP acceptance.

The earlier Step 172C blocked attempt remains historical. Its separately authorized successful retry, explicitly identified in that report's current disposition and section 9, supplies the closure evidence. Do not combine the two attempts into a claimed two-login total: **two successes describes the successful retry**, while the earlier consumed submissions remain recorded separately. Readiness alone in Step 172B was not acceptance; the executed retry resolves that boundary.

Retain prior test totals in their original scopes: Step 171 records Step 97's final **106 PASS**, Family frontend **6 PASS**, preference frontend **6 PASS**, print-script PASS and supplement **5 PASS**, plus owner **M1–M6 PASS**. These overlapping historical totals are neither summed nor rerun. The eight negative requests and the two sessions are separate acceptance observations, not extra automated-suite cases.

## 2. Preserved qualifications

- Clinician review and planned medical-staff evaluation remain outstanding. Owner technical acceptance does not substitute for clinical review or external OntarioMD/CPSO acceptance.
- Accepted release/source applicability remains qualified, including Baseline folder 5.5 versus document 1.7 and referenced CDS-S folder/workbook/target-release distinctions. Local source recovery and technical tests do not settle external release acceptance.
- M7 proves the three named Web permission denials with current unchanged-state evidence. The earlier reported API 403 observations retain their historical capture limitations; this retry does not reconstruct those old snapshots.
- M8 proves the adopted normal-session GET routing residual. Optional live no-view, nonmember-tenant and full foreign-mutation scenarios were not executed here. Existing isolated security/audit/rollback/concurrency results retain their stated scope.
- Deferred live restricted-user, omission-save and deliberate stale-write checks in **other feature scopes** remain deferred. Family M7 PASS does not close them by analogy.
- No new full print, complete WCAG, physical-device, production-readiness or unrelated security acceptance is claimed. CPP checks in the retry were content/read checks. Prior accepted print/appearance evidence is retained.
- The existing Family endpoints did not produce platform sensitive-capability denial events because they lack that handler's required metadata; zero such events is recorded behavior, not proof that denial auditing was exercised. Expected chart-read attribution and zero clinical mutations were verified.
- Automatic CDS-evaluation POSTs were guarded out during the retry. Its CDS-unavailable banner is outside Family acceptance. Writer B's earlier login failure cause remains undetermined despite the later successful authorized session.

These remain explicit qualifications outside the functional mandatory-gap count. No evidence supports closing another requirement or reopening a separately accepted requirement in this step.

## 3. Confirmed tracker and counting method

The confirmed maintained requirement-level tracker is the **append-only per-step Markdown inventory table**, as established in [Step 170 section 3](240-step170-pc07-07-evidence-closure-inventory.md), with controlling predecessor tables in [Step 136 section 7](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md), [Step 156 section 7](223-step156-pc07-01-owner-acceptance-and-evidence-consolidation.md), [Step 161 section 2](228-step161-pc07-01-remaining-gap-prioritization.md) and Step 167. Step 170 formally removed PC07.07, leaving nine IDs; Step 172C retained that count pending this decision.

**This report's table is the new current tracker entry.** Historical reports are preserved rather than edited to appear contemporaneously current. No separate maintained tracker location was established by the accepted tracker evidence; no speculative index, external tracker or coarse historical module-status document is changed.

Count each unresolved **top-level requirement ID once**. Subordinate DE elements, acceptance scenarios, overlapping test coverage and deferred cross-cutting qualifications are not additional IDs. PC07.04 was independently present in the nine-ID baseline, so its requirement-specific formal closure removes exactly one. PC07.01 remains independently incomplete even though its Family and Risk slices and DE08.008 have accepted evidence.

## 4. Inventory before and after

| Package | Before: unresolved IDs | Closed here | After: unresolved IDs | Count before → after |
|---|---|---|---|---:|
| Prescribing | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | None | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | **5 → 5** |
| CPP | PC07.01, PC07.04, PC07.10 | **PC07.04** | PC07.01, PC07.10 | **3 → 2** |
| Scheduling | PC09.03 | None | PC09.03 | **1 → 1** |
| **Total distinct IDs** | **9** | **1** | **8** | **9 → 8** |

Package count is recalculated from actual remaining memberships: **5 + 2 + 1 = 8 IDs**; all three packages remain nonempty, hence **3 → 3 packages**. Only PC07.04 changes status here; prior PC07.07 and subordinate DE08.008 closures remain retained.

| Remaining ID | Unresolved scope carried forward from the tracker |
|---|---|
| PC04.01 | Supported/licensed Canadian COTS catalogue prescribing path and supplier/source mapping |
| PC04.06 | COTS drug–drug checks, supplied severity and override path; depends on licensed safety data and medication activity semantics |
| PC04.07 | COTS drug–allergy/adverse-reaction checks, severity and override; allergy CRUD is separate accepted coverage |
| PC04.09 | User alert thresholds and required precedence, dependent on the safety foundation |
| PC04.14 | Ordinary-user licence-name/last-update information backed by actual supplier metadata and integration |
| **PC07.01 — PARTIAL** | Source-backed DIN DE09.005; destination/pickup/coded-reason/protocol DE09.030–.035; wider DE09.027 authority semantics; complete applicable ownership/retrieval/audience mapping. Closed dose/Family/Risk slices are not remaining missing fields |
| PC07.10 | Remaining encounter-managed procedure/medication-summary minimum not established; accepted encounter-to-Problem diagnosis work retained |
| PC09.03 | Scheduling-to-billing HCN/service-date handoff without duplicate entry; receiving target and transfer contract unresolved |

These are retained residual descriptions, not a new product-code assessment or newly recovered exact clause for any other ID. No other status change or count reduction is justified here.

## 5. One next priority — PC07.10

Compare using [Step 167 section 4](234-step167-de08-008-evidence-closure-next-gap.md), Step 170 sections 4–5 and Step 161's retained priorities:

| Candidate | Existing coverage and closure impact | Decisions/dependencies | Relative path to verified closure |
|---|---|---|---|
| **PC07.10** | Accepted encounter-to-Problem diagnosis slice; one independently counted ID. Procedure/medication-summary encounter coverage needs exact reconciliation | Encounter authority, association and interaction contract; no identified commercial supplier prerequisite in the retained residual | Best next bounded candidate: reconcile first, then isolate genuine omissions. Strong potential for focused automated workflow/tenant/audit/concurrency checks after scope is defined |
| PC07.01 | Many accepted components, but multiple independently unresolved subordinate obligations; closing one metadata slice cannot close the parent | DIN supplier/source dependency, actual coded reference sources, destination/pickup and authority policy, audience mapping | Larger multi-part path; do not reopen accepted slices or promise parent closure after a single field package |
| PC09.03 | Scheduling exists; one counted handoff requirement remains unsupported | Named receiving billing component and transfer/failure/duplicate contract | Cannot bound end-to-end work until the receiving contract is selected; external/design dependency remains |
| Prescribing PC04.01/.06/.07/.09/.14 | High mandatory safety impact; native medication/allergy capability already exists, but it is not licensed catalogue/safety integration | Supplier/licence/access, actual safety/metadata contract and update evidence; .06/.07/.09 depend on the foundation | Five-ID opportunity with substantial shared dependency. PC04.14 may look small but cannot close with fabricated supplier metadata; no supported faster independent closure is established |

**Select only PC07.10 for Step 174.** The selection is a planning judgment from the retained residuals, not a claim that PC07.10 is already implemented or that supplier/billing work is less important. It offers a separately countable local workflow candidate with reusable encounter/diagnosis foundations and no identified external data purchase prerequisite. The exact minimum and existing evidence must be recovered before proposing implementation; separate chart editors or finalized orders must not be assumed to satisfy encounter-managed behavior.

No remaining candidate has been demonstrated here to be another evidence-only closure. A bounded PC07.10 reconciliation is therefore the shortest responsible next action, rather than committing to a product change or a completion date without its contract. No parallel package is started.

## 6. Estimated effort and dependencies

Planning estimate for the **recommended next step**, one engineer, existing local evidence available:

| Work | Active effort | Boundary |
|---|---:|---|
| Recover exact PC07.10 clause and relevant prior mapping | **0.5–1 hour** | Reuse known authoritative source and targeted references; no broad specification scan |
| Map existing encounter/diagnosis/procedure/medication evidence and precise omissions | **1–2 hours** | Distinguish implementation, isolated tests, owner acceptance and deployed evidence; no execution |
| Bounded next-step scope, owner decisions, acceptance plan and revised estimate | **0.5–1 hour** | Document actual dependencies and stop conditions |
| **Step 174 total** | **2–4 engineering hours** | Documentation/source assessment only |

**Implementation, additional automated checks and authenticated deployment acceptance are not reliably estimable yet.** The retained inventory establishes an unclosed workflow minimum, not its exact remaining implementation contract. Step 174 must estimate these phases separately after mapping. Zero implementation/runtime work is authorized or planned in that assessment; this is not an estimate that eventual implementation will take zero hours. If existing evidence unexpectedly supports the complete requirement, formal closure may follow without implementation; do not presume that outcome.

Assumptions: source and prior encounter evidence remain available; no conflicting requirement interpretation is found; existing architecture can be reused. Owner/appropriate clinical input may be needed for encounter association, authority, correction/retention and workflow interaction choices **only where unresolved**. Owner technical preference must not be represented as clinical approval. Future login, fixture creation, product changes or security-negative requests each need their own explicit scope/authorization. Clinician review and external release acceptance can extend elapsed time independently; no overall certification completion date is supported.

## 7. Proposed Step 174 and stop conditions

**Step 174 — PC07.10 encounter-managed CPP evidence reconciliation and bounded gap plan.**

1. Retrieve the exact authoritative clause, applicability and existing interpretation. Do not substitute the retained residual description for verbatim wording.
2. Reuse accepted encounter-to-Problem diagnosis results and inspect only directly relevant existing procedure/medication-summary workflow definitions and verification records.
3. Classify each assertion as verified, implemented/evidence incomplete, functional gap or not verified. Identify ownership/encounter linkage and existing clinical safeguards from actual definitions, without inventing routes/schema or proposing name-based joins.
4. If evidence alone supports qualified closure, document that conclusion. Otherwise specify **one coherent smallest remaining scope**, exact owner decisions, focused automated acceptance, deployment prerequisites and phase-specific effort ranges. Do not begin implementation.
5. Documentation/source assessment only: no login, SQL/runtime access, test/browser execution, fixture creation, source edits, migrations or services. A demonstrated product gap needs a separately authorized implementation step and local branch decision afterward.

Do not repeat Family M1–M8, its deployed correlation, security suites or migration-status checks. PC07.04 closure is complete for the qualified scope in this report.

## 8. Files, checks and safety

Only **245-step173-pc07-04-evidence-closure-inventory.md** added. Current local branch **main** retained; existing untracked/manual work, including the full blocked/retry Step 172C history, preserved. Checks were targeted documentation cross-reference review, eight-ID uniqueness/arithmetic and nonempty-package reconciliation, followed by report/whitespace/status review. No test reruns, login/browser execution, database operation, clinical write, migration, restart, product-code edit, feature branch, commit, merge, push, reset, clean, stash, GitHub or remote operation. Stop after documentation.
