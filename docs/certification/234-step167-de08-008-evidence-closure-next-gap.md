# Step 167 — DE08.008 evidence closure and next mandatory gap

2026-10-24 (America/Toronto). **DE08.008 — SATISFIED — VERIFIED (qualified). PC07.01 remains PARTIAL. Inventory: 10 mandatory-gap IDs / three packages → 10 IDs / three packages.** Recommended Step 168: bounded **PC07.07 Risk Factors requirement-level evidence reconciliation**, within the CPP package. No new runtime results or implementation authorization.

## 1. DE08.008 requirement and closure decision

Reuse the exact source extraction in [Step 110 section 2](145-step110-pc07-01-immunization-dose-rules-resolution.md): CDS-S Data Dictionary v2.1 Final, published 2026-06-05, Data Dictionary **A119:K119**. Element **DE08.008**, **Dose**, is **M**, **S/D**. Its definition is:

> Dose amount and unit of measure corresponding to the administered immunization.

M requires product support; S/D requires storage and display. It does not require a known dose on every historical record. The row names no unit code set, numeric type, precision or range. The Baseline PC07.01(d) immunization category is the parent context, not a separate prescription-dose contract. The retained source locator/extraction is reused; no workbook re-extraction or new external interpretation was performed.

| Required behavior / boundary | Existing implementation and verification supporting closure |
|---|---|
| Independent administered amount **and** unit | [Step 162 sections 1–2](229-step162-de08-008-immunization-dose-implementation.md): nullable Dose subgroup; decimal amount and controlled canonical unit; independent of ordinal DoseNumber. Approved local policy: > 0 to 9999.999, up to three fractional digits; `mL`, `mg`, `mcg`, `units`; both present or absent. These are owner-approved engineering rules, not vocabulary/range mandates attributed to the dictionary. |
| Store known values without loss | Step 162 API/SQL and real Chromium round-trips, edits and explicit clear; all four units and approved boundaries tested. Existing stored procedures, transactional audit, actor attribution and RowVersion retained. |
| Display and retrieval | Step 162 history/editor, standalone summary and selected CPP print evidence, including exact known dose text. [Step 166 section 9](233-step166-de08-008-authenticated-ui-verification.md): actual authenticated development history/editor and Web→API read path; correct tenant/patient, controlled options, labels, guidance, focus and responsive layout. No every-field compact-card requirement is invented. |
| Unknown and historical compatibility | Step 162 nullable/omission/refusal/terminal cases; [Step 165 sections 1–2](232-step165-de08-008-post-deployment-verification.md): additive 0091 applied/verified and legacy preservation; Step 166: all four records across two patients retain NULL dose pairs and unchanged full-row fingerprint. No backfill, inferred unit or zero-dose refusal. |
| Relevant integrity safeguards | Step 162 isolated permission, tenant/patient, concurrency, audit rollback and immutable-state results. Step 166 one normal authorized login, expected tenant, successful scoped read, trusted actor 2 read-audit attribution; no clinical writes. These support the changed slice, not domain-wide security acceptance. |

Retained counts, without double counting or rerunning:

- **Step 162: 49 unique automated cases PASS / 0 FAIL, including 2 real Chromium integration cases PASS.** The two browser cases are part of 49, not 49 + 2.
- **Step 165: 22 deployment checks PASS / 0 FAIL; migration 0091 applied and verified.** No migration rerun.
- **Step 166 final resumed verification: desktop 8 PASS / 0 FAIL; mobile 8 PASS / 0 FAIL.** Four final screenshots inspected. One successful normal login; correct `local-dev-fresh` tenant. Clinical writes **0**; ordinary read-audit entries only, with unchanged immunization content/RowVersions.

Step 166 section 9 explicitly supersedes its historical unavailable-service BLOCKED attempt. Preliminary helper selector/transition failures are documented probe corrections, not final application failures or extra PASS counts. Step 165's historical partially verified status is superseded for the authenticated UI boundary by that resumed evidence.

**Decision: DE08.008 — SATISFIED — VERIFIED (qualified), for the approved administered-dose contract and the recorded development/integration scope.** Its implementation, deployment and focused acceptance evidence is sufficient to close this subordinate gap. No additional clinical save, owner visual checklist or dose test suite is needed for this decision. This is an internal evidence disposition, not an OntarioMD acceptance decision or whole-PC07.01 closure.

## 2. Qualifications preserved

- Future clinician review remains separate, planned and not performed; technical owner approval is not clinical authority.
- Accepted certification release/source applicability and external OntarioMD/CPSO acceptance remain governance qualifications. Retain the Baseline folder 5.5/document 1.7 and CDS-S folder 5.2 DFU/workbook 2.1 distinctions; tests do not establish acceptance of those sources for the tracked release.
- Live restricted-user denial, optional deployed omission-save and deliberate deployed stale-write remain deferred in their prior scope. Existing isolated security/concurrency PASS is retained; no assertion that these deployed scenarios ran.
- Family M7/M8 remains its own incomplete evidence lane. Immunization login/read success does not close Family-specific Web/permissions/state/audit assertions.
- Step 166 was read-only: non-NULL persistence is supported by Step 162, not a new development-tenant save. The blocked automatic CDS POST/banner is outside dose acceptance. Focused Chromium labels/focus/layout evidence is not a complete WCAG or physical-device certification.
- No production deployment, restore readiness, supplier compliance or unrelated requirement is certified here.

## 3. Requirement tracker and inventory reconciliation

The controlling requirement-level tracking tables are [Step 136 section 7](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md), [Step 141 section 7](208-step141-pc07-01-acceptance-closure-next-implementation-scope.md), [Step 156 sections 5–7](223-step156-pc07-01-owner-acceptance-and-evidence-consolidation.md), and the updated residual list in [Step 161 sections 2–3](228-step161-pc07-01-remaining-gap-prioritization.md). They agree on the following ten distinct top-level IDs. This report is the latest DE08.008 closure entry; historical tables are not rewritten.

The coarse module inventory, original preliminary gap map and early source-gap/readiness register are not a current ten-ID closure ledger. They contain earlier source/implementation findings, so they cannot override the later requirement-specific evidence. No external tracker update or independent external acceptance is claimed.

| Package | Counted outstanding IDs | Before | After |
|---|---|---:|---:|
| Prescribing | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | 5 | 5 |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 | 4 | 4 |
| Scheduling | PC09.03 | 1 | 1 |
| **Total** | **Ten distinct top-level mandatory-gap IDs** | **10 / 3 packages** | **10 / 3 packages** |

Counting rule: subordinate DE elements, overlapping category clauses, acceptance scenarios and cross-cutting qualifications are **not additional inventory IDs**. Closing DE08.008 removes one residual from the PC07.01 coverage ledger; it does not remove PC07.01 while its other applicable obligations remain incomplete. The historical 92 assessment units and overlapping test totals are not recomputed as gap IDs.

| ID | Genuinely incomplete residual / disposition carried forward |
|---|---|
| PC04.01 | Supported/licensed Canadian COTS catalogue path and supplier/source mapping not established. Native text/snapshots do not satisfy it. |
| PC04.06 | COTS drug–drug safety checks, supplied severity and override path missing; depends on licensed safety data and medication activity semantics. |
| PC04.07 | COTS drug–allergy/adverse-reaction checks, severity and override missing; accepted allergy CRUD is not this integration. |
| PC04.09 | User alert thresholds and required precedence missing; depends on safety foundation. |
| PC04.14 | Ordinary-user licence name/last-update information path and actual supplier metadata not established. |
| PC07.01 | PARTIAL: DE09.005 catalogue-backed DIN/source provenance; grouped DE09.030–.035 destination/pickup/coded-reason/protocol support; wider DE09.027 electronic-authority/No semantics; complete applicable PC07.01(f)/.06 ownership/retrieval/audience mapping. Retain Family and release/source qualifications. **DE08.008 is now closed**, not part of the remaining implementation work. |
| PC07.04 | Family approved minimum retained; M7 deployed Web/permissions/state/audit and M8 normal-session tenant-routing assertions remain incomplete. |
| PC07.07 | Risk approved four-field minimum implemented with retained automated and owner appearance PASS. **Requirement-level evidence mapping and deployment/release qualifications remain unclosed**, not a demonstrated missing risk field. |
| PC07.10 | Encounter-managed procedure/medication-summary minimum not established; accepted encounter-to-Problem diagnosis work retained. Separate chart editors/orders are not that workflow. |
| PC09.03 | Scheduling-to-billing HCN/service-date handoff without duplicate entry missing; receiving billing target and transfer contract unresolved. |

All three packages remain incomplete, but their residuals differ: actual functionality is missing in several rows; PC07.04/.07 principally need their own evidence dispositions. Native medication, allergy/NKA, qualified DE09.004, DE11.008 date semantics, accepted owner checks and closed sidebar behavior are not reopened.

## 4. Comparison and one recommended next package

This is a planning comparison using retained evidence, not a new source-code audit or commercial estimate. All listed IDs are mandatory; sequencing reflects dependencies and likely time to a defensible closure.

| Candidate within inventory package | Existing coverage / mandatory impact | Decisions and external dependency | Test feasibility and relative effort |
|---|---|---|---|
| **CPP — PC07.07 Risk evidence closure** | Four-field minimum implemented; potential to resolve one separately counted ID after its own complete mapping | No new clinical field policy indicated. Applicable-release evidence remains external; do not assume it resolved | Reuse Step 100's 35 .NET and 8 frontend PASS and user V1–V3 PASS. Lowest expected effort: map evidence first, identify only missing deployed assertions. No code presumed |
| CPP — PC07.04 Family evidence closure | Implemented minimum, but named M7/M8 residuals explicitly remain | Appropriate existing identities/permissions, approved sessions and observation of unchanged state/audit | Feasible targeted automation, but more specific live security/session evidence and authorization needed than a Risk mapping pass; low–medium effort |
| CPP — DE09.030–.035 grouped medication metadata | Coherent next implementation slice; advances PC07.01 but cannot alone close it | Facility source/namespace/association; pickup precision/zone/provenance; real coded-reason/protocol sources | Strong potential for isolated automation after policies exist; medium–high implementation/acceptance effort, currently decision dependent |
| CPP — PC07.10 encounter workflow | Accepted diagnosis slice, remaining procedures/medication-summary workflow | Encounter authority and association/interaction contract | Feasible focused tests after contract; medium–high workflow and acceptance effort |
| Prescribing — PC04.01 → .06/.07/.09/.14 | Highest shared clinical safety dependency; five counted IDs | Licensed supplier, commercial access, supported data/API, update process and mapping | Automated integration feasible only with actual source/service contract; highest dependency and uncertain calendar lead time |
| CPP — wider authority/retrieval mapping | Native restricted authority and navigation exist; wider PC07.01 remains open | Governing electronic-authority semantics, field ownership and audiences | Map before implementing only proven omissions; policy dependent and not yet bounded |
| Scheduling — PC09.03 | One counted mandatory ID; scheduling exists, handoff not established | Named receiving billing component and duplicate/failure/transfer contract | Cannot responsibly estimate end-to-end delivery before target selected; currently blocked on design/external contract |

**Select ONE: PC07.07 Risk Factors requirement-level evidence closure, a bounded CPP work package.** [Step 100](135-step100-pc07-01-risk-factors-implementation.md) records an implemented authoritative Risk source, four independent fields, chart/CPP/preferences/full selected print and isolated safeguards; [Step 113](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md) accepts the minimum with deployment/release qualifications. Step 161 explicitly directs evidence reconciliation before proposing more Risk code or tests. Resolve that ledger first: it may enable a justified top-level closure with much less work than six new policy-dependent fields, while revealing any precise missing acceptance assertion early.

This is a deliberate evidence-first refinement of Step 166's metadata recommendation, not a decision to abandon metadata or reduce the prescribing safety priority. Finish this one Risk disposition before starting a new implementation package. No PC07.07 closure or count reduction is made in Step 167; suitability for qualified closure must be established by Step 168. Supplier procurement remains a longer-lead owner dependency, not an additional workstream authorized here.

## 5. Owner decisions and prerequisites

For recommended Step 168, **no new Risk clinical policy, optional field, vocabulary or redesign decision is currently indicated**. Owner action is to authorize the bounded evidence-reconciliation scope below. Do not ask for repeat V1–V3 visual acceptance.

If existing documents lack required Risk deployment/session proof, Step 168 should specify the exact missing assertion and obtain separate authorization for any clinical login, live fixture/record change or configuration change. Step 166's consumed login does not authorize another. No permission/fixture creation is presumed. Source-release applicability needs actual accepted-source evidence or an explicit retained qualification; owner preference alone cannot manufacture external acceptance.

For the deferred implementation candidates, preserve the known decisions in Step 161: actual licensed catalogue/safety supplier and access; actual destination/reference sources and authority; pickup precision/zone/unknown policy; encounter association contract; receiving billing target. Those decisions are not silently resolved or requested as prerequisites to the smaller Risk documentation step.

## 6. Effort and completion range for the selected package

Engineering planning ranges, **not measured delivery commitments**, assuming one engineer, relevant artifacts available and no newly discovered defect. One working day means approximately eight engineering hours. Do not apply these numbers to all ten IDs or overall certification.

| Phase | Estimated effort | Assumptions / exclusions |
|---|---:|---|
| Policy/design and requirement-to-evidence mapping | **2–4 hours** | Reuse Baseline PC07.07 mapping and DE12.001/.002/.006/.007 minimum. Record optional .003–.005 exclusions and release qualification. No new policy workshop presumed |
| Implementation | **0 hours planned** | No missing capability/defect established. If one is found, stop for a separately authorized and estimated implementation; no hidden code allowance |
| Automated-test work | **0–2 hours** | Reuse completed suites. Only design/prepare a focused check if the mapping proves an acceptance assertion absent; new execution requires applicable authorization. No broad rerun |
| Deployment/evidence acceptance | **0–6 hours** | Existing adequate provenance may need no runtime work. Upper range assumes one small separately authorized deployed read/correlation check and final disposition; no rebuild, migration or clinical mutation assumed |
| **Total active effort** | **2–12 hours (about 0.25–1.5 working days)** | Evidence-only mapping can finish at the low end; full technical disposition depends on the resulting evidence gaps |

Expected Step 168 documentation output itself: **2–4 hours**. A qualified technical closure may fit the **2–12-hour** range only if its remaining evidence is available or the bounded follow-up is authorized and succeeds. External release acceptance, access approvals, unavailable environments or newly established defects can extend elapsed time without a supportable upper bound. No overall OntarioMD certification completion date is given.

## 7. Proposed Step 168 scope and stop conditions

**Step 168 — PC07.07 Risk Factors evidence reconciliation and qualified closure readiness.**

1. Read only the applicable Baseline mapping, Steps 99/100/113, controlling inventory rows and any directly cited existing deployment evidence. Reuse the retained exact-source extraction; no broad specification or repository scan.
2. Map each mandatory Risk storage/display obligation, CT-016 conformance and required chart/CPP retrieval to the recorded four-field implementation and existing test/visual outcomes. Keep optional timing/severity and unknown semantics explicit; do not invent mandatory fields or require every optional value per patient.
3. Determine which Risk-specific environment/migration/app/session/tenant-read assertions are already established. Shared Step 165/166 evidence is context only where actually applicable; immunization success is not Risk execution proof. Do not repeat accepted tests or appearance checks.
4. If complete, record PC07.07 **SATISFIED — VERIFIED (qualified)** with exact limits and then reconcile its independently counted tracker row. Otherwise retain an evidence-pending status and list only the missing assertions, smallest future automated check and required authorization. Do not promise inventory 9 in advance.
5. Produce one evidence/decision report. No code, migration, rebuild, deployment, restart, clinical access/write, fixture changes or branch creation under this proposed documentation scope. Stop and request separate implementation authorization if a defect is established.

Step 167 checks performed: documentation cross-reference/count review and final whitespace/status checks only. No tests, builds, browser/clinical login, database queries/writes, service operations or remote calls. Current local branch **main** retained. Existing untracked Step 166 report preserved unchanged. Only **234-step167-de08-008-evidence-closure-next-gap.md** added; no commit, merge, push, reset, clean or stash. Stop after documentation.
