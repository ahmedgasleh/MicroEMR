# Step 185 — remaining seven mandatory gaps: priority assessment

2026-10-25 (America/Toronto). **Inventory confirmed and unchanged: 7 mandatory-gap IDs / 3 packages. Recommended dependency/delivery order: PC04.01 → PC04.14 → PC04.06 → PC04.07 → PC04.09 → PC07.01 → PC09.03.** Start with a bounded catalogue/source-contract readiness decision, not implementation. PC07.01 has substantial reusable evidence but is not currently an evidence-only or relatively bounded whole-requirement closure. Conditional remaining effort: **272–544 active engineering hours**, excluding external waiting and separately undefined integration scope. Documentation only; no requirement status changes.

## 1. Baseline, sources and assessment method

[Step 184](260-step184-pc07-10-qualified-closure.md), section 5, is the controlling append-only inventory. Its **after** column contains exactly:

| Package | Remaining IDs | Count |
|---|---|---:|
| Prescribing | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | 5 |
| CPP | PC07.01 | 1 |
| Scheduling | PC09.03 | 1 |
| Total | Seven distinct IDs; three nonempty packages | **7** |

PC07.10 is **SATISFIED — VERIFIED WITH QUALIFICATIONS** and is not reopened or counted. Its clinician/release/deferred-runtime ledger remains in Step 184. Other accepted requirement and subordinate closures retain their original scopes.

Requirement references reuse [Step 45](77-step45-baseline-1.7-reconciliation.md) and [Step 95](129-step95-pc07-01-cpp-category-completeness-assessment.md). The former's seven-ID descriptions are summaries, not verbatim clauses. To supply the exact wording requested here, the existing Step 174 PDF-text helper was reused only for the cited pages **16–19, 21, 27 and 32** of the known local source. No broad specification recovery or implementation scan was performed.

Source: [Primary Care Baseline — 1.7 Requirements.pdf](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), **Document Version 1.7 Final, March 20, 2026**; page numbers below match the retained mapping. All seven rows are **M**. The source's U cell is not MicroEMR's implementation status. Retain the existing Baseline folder 5.5/document 1.7, CDS-S v2.1/DFU and tracked-release applicability qualifications; this task does not settle external acceptance.

“Current status” below means the latest retained implementation/evidence disposition, not a new code audit, live database attestation or supplier-market search. Existing [Step 161](228-step161-pc07-01-remaining-gap-prioritization.md) and [Step 173](245-step173-pc07-04-evidence-closure-inventory.md) identify the residuals; Step 184 supplies the updated count. Old missing-field or pending-evidence descriptions are superseded where a later accepted report resolves them.

## 2. Exact mandatory clauses and relevant scope

The following quotes reproduce the **Requirement column**, normalizing PDF line wrapping and rejoining split word/hyphen fragments. Additional guideline obligations are explicitly summarized separately; optional examples are not promoted to MUSTs.

### PC04.01 — Medication Management, pages 16–17

> The EMR Offering MUST provide the ability to record patient prescriptions using a publicly available COTS (commercial off-the-shelf) drug database with Canadian drug codes.

Guideline minimum: use COTS for Drug Name, Drug Strength / Drug Strength UM and Drug Form. The continuation requires commercially licensed COTS with Health Canada-authorized information, clear licensing/accountability, regular updates, maintenance and support. An unsupported open-source data source does not satisfy those assurances. Reuse Step 45's corresponding interpretation; manual identifiers or native text are not evidence of this integration.

### PC04.06 — Medication Management, pages 18–19

> The EMR Offering MUST perform drug-to-drug interaction using a publicly available COTS drug database with Canadian drug codes.

Guidelines require severity and override for each identified interaction, limit triggering to **active prescriptions**, and specify **severity provided by COTS**. Licensing/support/maintenance assurances apply. A finalized order is not automatically proof of current treatment; the accepted activity semantics must be mapped explicitly.

### PC04.07 — Medication Management, page 19

> The EMR Offering MUST perform drug-to-allergy and drug-to-adverse reaction using a publicly available COTS drug database with Canadian drug codes.

Guidelines require severity and override for each identified interaction, with **severity provided by the EMR as recorded for the patient**. Do not reuse the PC04.06 supplier-severity rule for this requirement. Licensed/supportable data is required; allergy CRUD and a populated DIN are not independently a complete matching/alerting capability.

### PC04.09 — Medication Management, page 19

> The EMR Offering MUST provide options to manage medication alerting for drug-to-drug interactions at the EMR user level.

The guideline additionally requires user-level display thresholds and that user settings supersede organization settings. Optional organization-level capability under PC04.10 is not made a new mandatory ID. Per-warning suppression/reappearance workflows are presented as examples, not mandatory implementation of every illustrated workflow.

### PC04.14 — Medication Management, page 21

> The EMR Offering MUST provide the ability for an EMR user to view license information of the COTS drug database.

Guideline minimum: actual COTS **name and last update date**; version number is optional. Access must be within the EMR and must not require administrative permissions. A centralized licensing source is a recommendation, not a new compulsory screen architecture. An invented vendor name or application build date cannot establish compliance.

### PC07.01 — Cumulative Patient Profile Management, page 27

> The EMR Offering MUST display Cumulative Patient Profile (CPP), identifying the summary of patient information.

The guideline mandates eight categories: **Ongoing Health Conditions; Past Medical and Surgical History; Family Medical History; Immunization Summary; Allergies and Adverse Reactions; Medication Summary; Risk Factors; Medical Alerts and Special Needs**, with PC07.02–PC07.08 and CPSO references. Retain [Step 95](129-step95-pc07-01-cpp-category-completeness-assessment.md)'s dictionary mapping and [Step 136](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md)'s authoritative storage/retrieval/audience distinctions. A compact card need not display every field or all rows; required underlying support still needs evidence.

### PC09.03 — Schedule Management, page 32

> The EMR Offering MUST integrate patient appointment with the billing component to avoid duplicate patient data entry.

The guideline requires transfer of at least the two mandatory scheduling/billing elements, retained in [Step 45](77-step45-baseline-1.7-reconciliation.md) as **patient HCN and service date**; clinician billing number is optional. A chart link or copy/paste instruction is not a proven receiving-component handoff. This requirement does not by itself authorize building an entire billing subsystem.

## 3. Seven-requirement comparison matrix

Effort is a **new planning judgment**, not a previously executed estimate or certification commitment. SMALL/MEDIUM/LARGE describe relative scope/dependency risk; hour ranges are incremental active engineering effort after the stated contracts/access are available. They include new affected implementation, focused tests and bounded acceptance/documentation, without repeating completed suites. Shared foundation is counted once in PC04.01, not again in each dependent row.

| Delivery rank / ID | Current status and reusable functionality | Specific missing behavior or evidence | Dependencies | Effort | Main implementation / certification risk | Recommended next action |
|---|---|---|---|---|---|---|
| **1 — PC04.01** | **Missing licensed-catalogue capability**, retained by Steps 45/161. Reuse existing native prescription authoring, product identifier slots, clinical validation, governed correction/finalization, provider/tenant authorization, immutable artifacts and retained prescription tests. Native prescribing works; that is not COTS acceptance. | Designated licensed Canadian-code source; actual search/selection/population of required fields; namespace/DIN mapping and provenance; source-version/update/failure behavior and supplier evidence. | Owner-designated supported supplier, licence/use rights, actual Canadian catalogue/schema, development access and accepted release/source contract. Shared DIN dependency with PC07.01. | **LARGE: 64–120 h** | Wrong product/strength/form mapping, unsupported source presented as licensed, changing frozen orders/artifacts, source-outage behavior or an inappropriate medication activity assumption. Contract/supplier lead time unknown. | **Step 186 source-contract readiness**: decide the actual source/access/mapping and bounded integration/acceptance contract. Do not select or purchase a supplier in this task. |
| **2 — PC04.14** | **Missing ordinary-user COTS licence information**, Steps 45/161; reuse authenticated Web layout/permissions patterns. | Display the real database name and last-update date within the EMR for an authorized ordinary user; prove date/source provenance and non-admin access. | PC04.01 supplier metadata/update contract. No dependency on completing both safety engines. | **SMALL: 8–16 h**, incremental | Placeholder metadata, confusing application build time with supplier update time, admin-only visibility, stale metadata. Small screen alone cannot close this before source evidence exists. | Implement/test as a small companion to the catalogue foundation once the real metadata exists; it need not wait behind safety coding. |
| **3 — PC04.06** | **Missing licensed drug–drug check/override path**, Steps 45/161. Reuse retained prescription histories/activity work, clinical actor/tenant controls and structured medication identities. | Actual supplier checks for active prescriptions only; supplier-provided severity; review/override path and associated auditable state; current-source and unknown/outage acceptance. | Licensed interaction data/API, exact identity matching, reviewed active-prescription semantics and clinician-approved alert/override workflow. | **LARGE: 48–96 h**, incremental | False negatives/positives from status/date errors, treating Medication Summary classification as prescription activity, fabricated severity, repeated alerts or unsafe silent failure. High clinical-staff evaluation importance. | Define the active-input/severity/override/failure contract, then implement one explicit safety boundary with focused isolated and normal-session evidence. |
| **4 — PC04.07** | **Missing licensed drug–allergy/adverse-reaction check/override path**; authoritative allergy/NKA, recorded patient severity and accepted allergy CRUD remain reusable. | Product/substance-to-patient-allergy matching using actual COTS; indication of recorded patient severity and override; evidence for unknown/non-drug/adverse-reaction cases without rewriting allergy history. | PC04.01 identities and supplied matching rules; explicit drug applicability and handling of uncoded/unknown allergies. Shared transport/override shell from PC04.06 can be reused. | **MEDIUM: 32–64 h**, incremental | Unsupported inference from free text, wrong substance mapping or substituting COTS severity for patient-recorded severity; defaulting unknown to no risk. | Resolve the source/allergy matching and severity contract separately, then share only transport/feedback/audit infrastructure with .06. |
| **5 — PC04.09** | **Missing user-level drug–drug alert thresholds/precedence**, Steps 45/161. Existing CPP display preferences are adjacent UI patterns, not evidence of medication-safety thresholds. | Persist/read effective user thresholds, apply provider severity categories and prove user-over-organization precedence; safe defaults and scope. | Actual .06 severity/alert contract and approved user/organization policy. Organization-settings UI is not newly mandated. | **MEDIUM: 16–32 h**, incremental | Silent suppression, incorrect precedence, cross-user/tenant preference leakage, treating optional examples as unlimited scope. | Design thresholds alongside .06; implement once genuine alert categories exist and verify effective precedence without repeating unaffected preference tests. |
| **6 — PC07.01** | **PARTIAL; extensive accepted functionality/evidence.** All eight sources/mechanisms now exist. Reuse field/date/history/CPP projection, private audience/print controls, native order/plan/compliance contracts, accepted owner/mobile checks, dose/Family/Risk closures and closed PC07.10 encounter integration. | Retained specific residuals: source-backed **DE09.005 DIN**; destination name/address/source-qualified ID, earliest pickup and coded reason/protocol **DE09.030–.035**; broader **DE09.027** authority applicability; complete applicable owner/source/full-detail/retrieval/audience correlation. Separate missing metadata support from mapping/evidence-only rows. | DIN source shared with .01; actual facility/code/protocol sources/namespaces or an approved entered-fact policy; pickup precision/zone/unknown rules; authoritative policy on broader electronic/paper authority and applicable release. | **LARGE: 64–128 h**, incremental and conditional | Inferred order/list links, fabricated source codes or pickup times, overwriting historical snapshots, exposing private notes, falsely claiming electronic authority or reopening completed native work. Whole-parent closure is not bounded until these contracts are settled. | Prepare one updated residual/source decision sheet; then implement only proven grouped omissions. Do not rebuild all eight categories or claim one metadata feature closes the parent. |
| **7 — PC09.03** | **Missing scheduling-to-billing handoff**, Steps 45/161/173. Reuse existing patient HCN, appointment/service-date and clinician data, tenant authorization and accepted scheduling workflows. | Named receiving billing component and an actual no-duplicate-entry transfer of HCN/service date; acceptance by the receiver, provenance/failure/retry/duplicate semantics and scoped evidence. | Owner-approved billing destination/interface and minimum transfer contract. No receiving target is established in retained evidence. | **MEDIUM: 24–56 h** for one simple agreed receiver; otherwise **unbounded pending contract** | Wrong patient/date/zone, duplicate billing records, unintended cross-tenant disclosure, destabilizing scheduling or expanding into a full billing engine. | Select the receiver and approve a minimal handoff/acceptance contract before any implementation estimate is treated as committed. |

Prescribing missing functionality is carried forward from the controlling evidence, not inferred anew from the presence of adjacent features. No seven-ID status is upgraded, downgraded or closed here. Unexecuted runtime/security, clinician and release qualifications remain distinct from these functional residuals.

## 4. Why PC07.01 is not ranked first for whole-requirement closure

The preference was assessed rather than assumed. [Step 161](228-step161-pc07-01-remaining-gap-prioritization.md) and the later [Step 173 residual table](245-step173-pc07-04-evidence-closure-inventory.md) explicitly describe a multi-part, source/policy-dependent parent. The existing evidence does **not** support “one small CPP fix and close PC07.01.”

Completed work must be subtracted from old plans:

- [Step 161 section 1](228-step161-pc07-01-remaining-gap-prioritization.md) accepts native drug description/medication/allergy work, [Step 157](224-step157-pc07-01-owner-acceptance-checklist.md)'s **Prescription/Medication Plan/Allergy owner PASS**, and Step 160's mobile closure. Earlier pending wording/mobile checkpoints are historical, not new work. This does not erase the broader PC07.10 qualification ledger retained by Step 184.
- [Step 167](234-step167-de08-008-evidence-closure-next-gap.md) closes administered dose **DE08.008**; DoseNumber versus amount/unit is no longer a reason to restart that package.
- Step 173 closes Family PC07.04 after M7/M8; it retains the prior Risk PC07.07 closure. [Step 184](260-step184-pc07-10-qualified-closure.md) closes PC07.10. Do not replay those acceptance sessions as PC07.01 prerequisites.
- Native system-entry allergy recorded-date semantics are accepted; do not recreate an unmandated external-original-date feature or require classification/authority inference. Native paper **Yes / Not recorded** is already accepted; the wider authority question is a specific remaining applicability decision.

PC04.01 is the stronger next dependency: it supports five independently counted Prescribing IDs and the CPP DIN residual. Catalogue/identity provenance must exist before claiming the related safety and licence capabilities. PC07.01 can retire the last CPP ID when its full residual is genuinely resolved, but catalogue completion alone will not settle facility/reason/protocol/pickup/authority mapping.

This ranking is a **dependency delivery order**, not a clinical-risk downgrading. Drug–drug/allergy checking has high clinical impact; the catalogue foundation is prioritized to enable it. PC04.14 is small and can be delivered with that foundation rather than postponing safety work for a separate administrative project. No partially finished safety package is represented as ready for clinical prescribing evaluation or release.

If the catalogue source/access cannot be obtained promptly, do not keep implementing against placeholders. Use that blocked time for the bounded independent PC07.01 source/authority decision sheet or the billing-receiver decision. Re-rank only when one of those becomes a supported, closure-ready contract. No such source/receiver readiness is presumed in this report.

## 5. Safe implementation sequence and combination opportunities

| Phase | Recommended scope | Exit condition / boundaries |
|---|---|---|
| **A — Step 186, decisions/readiness** | PC04.01 licensed Canadian catalogue and development-access contract, with explicit reusable DIN namespace/provenance for PC07.01. Identify the .06 interaction and .14 metadata capabilities supplied by that same source. | Approved source/licence/access and mapping/acceptance contract, or an exact named external blocker. No supplier purchase, product implementation, credentials disclosure, SQL or login under this plan. |
| **B — separately authorized foundation** | Catalogue selection/recording (.01) and its real licence/update display (.14); reuse existing native fallback and immutable order/artifact contracts. | Required source fields, owned snapshots, update/failure behavior and ordinary-user metadata evidence. No fabricated DIN/metadata or wholesale prescription redesign. |
| **C — separately authorized safety** | Drug–drug (.06), drug–allergy/adverse reaction (.07), effective user thresholds (.09). Design their shared request/feedback/override/audit shell together, while keeping input population and severity authority distinct. | Per-ID source-backed checks, authorized override, required thresholds/precedence, correct scope and targeted accepted evidence. A shared shell does not by itself close all three IDs. |
| **D — finish CPP parent** | Reconcile DIN integration evidence, implement the approved grouped .030–.035 omissions, settle .027 applicability and complete full-detail/owner/audience evidence. Independent reference/pickup decisions can be prepared earlier. | Every applicable residual explicitly satisfied/verified or justified against accepted authority; no inference that the finished native slices or .01 integration alone close PC07.01. |
| **E — billing handoff** | One approved receiving component for PC09.03; deliver only the required scheduling-to-billing transfer. Receiver selection can be resolved earlier without touching stable scheduling. | Actual receiving acceptance and no re-entry, with patient/tenant/date/failure/duplicate evidence. No new full billing subsystem implied. |
| **F — closure/release disposition** | Consolidate each changed requirement's actual evidence, scoped clinical-staff observations and retained release/security obligations. | Owner-approved per-ID decisions and count changes in later authorized documentation steps, not automatic package closure from code presence. |

The five Prescribing IDs share a foundation and can eventually retire that whole five-ID package. PC07.01 and PC09.03 each offer a one-ID package closure, but each has unresolved contracts; neither is presently an evidence-only shortcut. Source-contract decisions for all seven precede code where their needed inputs are unknown.

Clinical-staff evaluation should reuse existing stable CPP/native medication/SOAP workflows and Step 182B artifacts. Ask clinicians to resolve specific remaining authority, pickup, matching/severity or override questions that require clinical judgment; do not ask them to repeat the owner visual checks. Distinguish a technical demonstration from validation of live prescribing safety. No clinical evaluation, new login or save is authorized here.

## 6. Conditional effort and uncertainty

| Requirement | Relative effort | Active engineering hours |
|---|---|---:|
| PC04.01 | LARGE | 64–120 |
| PC04.14 | SMALL, incremental | 8–16 |
| PC04.06 | LARGE, incremental | 48–96 |
| PC04.07 | MEDIUM, incremental | 32–64 |
| PC04.09 | MEDIUM, incremental | 16–32 |
| PC07.01 | LARGE, incremental | 64–128 |
| PC09.03 | MEDIUM, receiver-dependent | 24–56 |
| **Seven-ID engineering subtotal** | Conditional on the stated inputs | **256–512** |
| Cross-package closure/release evidence consolidation and bounded clinical-review coordination | Additional; not a second implementation/test allocation | **16–32** |
| **Conditional total** | Approximately **34–68 eight-hour engineer-days** | **272–544** |

These are rough engineering planning ranges from retained residuals, not code-based estimates, supplier quotations, elapsed-time promises or dates for OntarioMD certification. PC04.01 includes the shared catalogue/identity/transport foundation; child rows are incremental. PC07.01 excludes already completed dose/Family/Risk/PC07.10/native visual work and excludes rebuilding that shared DIN foundation. Per-ID ranges already include focused new affected tests and bounded acceptance; those are not charged again as a separate full regression campaign.

External supplier/licence acquisition, contract negotiation, unavailable sandbox/source data, owner/clinician scheduling and accepted-release policy decisions have **unbounded waiting time** here. Licence/subscription fees are not estimated. A new electronic-prescribing/authority platform, a full billing subsystem, unsuitable supplier APIs, broad terminology remediation or additional external certification obligations are outside the modeled scope and would require re-estimation, potentially beyond the upper bound. **There is no defensible unconditional all-seven closure or calendar-completion estimate before these dependencies are resolved.**

## 7. Proposed Step 186 — exact next bounded action

**PC04.01 Canadian COTS source-contract and integration readiness**, documentation/targeted read-only decision preparation first:

1. Identify the owner-approved actual supplier/source, supported Canadian catalogue scope, licence/update/support evidence and permitted development access. No supplier recommendation/procurement or credential disclosure is assumed.
2. Map required name, strength/unit, form and Canadian-code/DIN provenance to the existing native prescription fields; record uncoded/free-form/outage boundaries and historical snapshot preservation. Reuse completed prescribing tests/evidence as anchors rather than rerunning them.
3. Determine the supplied .06 interaction inputs/severity and .14 licence-name/update metadata contracts. Keep .07 patient-recorded severity distinct. Record the shared PC07.01 DE09.005 boundary without claiming other CPP metadata is supplied by the catalogue.
4. Produce one approval-ready source/DTO/persistence/ownership/acceptance plan with exact missing access/clinical/source decisions. Return READY / BLOCKED with a refined implementation estimate. If no supported source is named, stop with that dependency; do not fabricate mock supplier data as certification evidence.
5. Request separately scoped implementation/deployment/runtime authorizations only when a concrete contract and bounded change are reviewable. Step 185 authorizes none of those actions.

The independent facility/reason/protocol/pickup/authority and billing-receiver decisions remain visible in this sequence; no additional investigation campaign or product work begins automatically.

## 8. Documentation checks, inventory preservation and stop

Only `261-step185-remaining-mandatory-gap-prioritization.md` was added outside ignored `.tmp/step185/` source-text/document-check receipts. Existing reports and manual/uncommitted work were preserved. Documentary checks: Step 184 after-table identifiers/counts; targeted report links and superseding accepted closures; relevant source-page clause transcriptions; estimate arithmetic; UTF-8/whitespace and preservation hashes. The reused PDF helper is a document reader, not a product build/test or browser action.

**Inventory remains 7 IDs / 3 packages; all statuses unchanged.** No source-code edit, SQL access/change, clinical write, login/browser test, build/automated test, migration/deployment/restart, feature branch, Git command, commit/push/merge/reset/clean/stash or GitHub operation occurred. Stop after documentation.
