# Step 136 — PC07.01 consolidated gap assessment and implementation plan

2026-10-22. **PC07.01: PARTIAL. Mandatory inventory: 10 IDs / three packages, unchanged.** Planning only. This report consolidates the remaining scope; subsequent work should use these grouped packages rather than another sequence of individual-field assessments.

## 1. Checkpoint, method and counting

Read AGENTS.md first. Git checkpoint recorded once: branch `main`; HEAD `b7fa5d7c12bcecc76a04f44ab40012495238218d`. Existing working tree:

```text
 M src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml
?? docs/certification/199-step132-de09-003-recorded-brand-name-details-display-fix.md
?? docs/certification/200-step133-de09-003-manual-acceptance-evidence-closure.md
?? docs/certification/201-step134-de09-004-drug-description-contract-evidence-assessment.md
?? docs/certification/202-step135-de09-004-drug-description-contract-resolution.md
?? tests/MicroEMR.Api.Tests/PrescriptionDetailDisplayTests.cs
```

Report number 203 was available; only this report is added. All prior changes preserved. Used targeted report sections, not fresh source analysis, specification extraction or a restart of certification investigation. No database queries, clinical/browser/API requests, builds, test execution, fixtures, source/schema changes, service restarts, Git mutations or remote operations.

**Counting convention for this report:** 92 mandatory assessment units = eight PC07.01 category clauses + 83 mandatory referenced data elements + the PC07.06 ongoing-default assertion. These are coverage units, **not 92 distinct inventory IDs**. Parent categories and child elements overlap in implementation and must not be counted as independent defects. Eight optional dictionary elements are tracked separately, giving 100 coverage entries in total. Shared safeguards/source mapping apply across entries and are not invented additional numbered requirements.

Primary blocker counts across the 92 mandatory units: **70 verified in their retained scopes; 16 with confirmed implementation gaps (A); one evidence-only (B); five mapping/design uncertainties (C)**. A takes precedence over C when functionality is missing and its implementation also needs a decision. Counts do not imply all A work is ready to implement, or that verified native support establishes every external-source variant. Optional entries and retained qualifications (D) are excluded from those four counts. Matrix ranges below explicitly identify the counted members.

## 2. Authority and complete scope

Reuse [Step 95 section 2](129-step95-pc07-01-cpp-category-completeness-assessment.md), [Step 113 reconciliation](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md), and [Step 115 authoritative DE09 ledger](150-step115-pc07-01-medication-de09-requirement-resolution.md). Primary Care Baseline document 1.7 Final, section 2.7, pp.27–29 supplies PC07.01(a–h), linked PC07.02–.08 and the ongoing medication default. CDS-S Data Dictionary 2.1 Final supplies the referenced groups and Store/Display distinctions. Accepted-release and CPSO/OntarioMD applicability remain qualified; this is an engineering consolidation, not external acceptance.

PC07.01's eight categories are Ongoing Health Conditions, Past Medical/Surgical History, Family Medical History, Immunization Summary, Allergies/Adverse Reactions, Medication Summary, Risk Factors, and Medical Alerts/Special Needs. All are mandatory. Required elements need appropriate authoritative storage/retrieval/display; a compact card need not contain every element or every row. Store-only does not mean optional or imply mandatory print placement. Mandatory support does not require fabricated known values for each patient.

Medication has **36 mandatory elements: 23 S/D and 13 store-only**. CT-022/023/024/025/026, treatment CT-034 and status CT-035 conformance are optional; missing adoption alone is not a blocker. Retain mandatory CT-016 life-stage and CT-020 immunization-type conformance in their previously accepted domains. Conditional applicability includes drug DIN, replacement linkage, compound text and administered versus refused immunization content; it does not turn mandatory capability into optional support.

Draft/finalization/correction/supersession and immutable artifacts are existing implementation contracts used to prove persistence, not new independently numbered PC07.01 workflows. Required tenant isolation, trusted actor, role checks, retained records, concurrency and transactional audit apply to affected clinical changes; dedicated read audits and private-note suppression retain their accepted contracts. PC07.10 encounter-managed updates, PC07.11 customization and PC07.13 selective printing remain separately tracked obligations. Reuse their established components when new PC07.01 content needs display; do not reopen their closures or add their whole workflows to this plan. No electronic transmission system, full catalogue purchase, compound calculator, reconciliation-by-name or universal every-field print requirement is inferred.

## 3. Reusable requirement-to-evidence matrix

**Evidence key:** R113 = [148](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md); R114 = [149](149-step114-pc07-01-chart-alert-safeguards-implementation.md); R115 = [150](150-step115-pc07-01-medication-de09-requirement-resolution.md); R116 = [151](151-step116-pc07-01-prescription-date-precision-implementation.md); R118 = [153](153-step118-pc07-01-prescription-generic-name-implementation.md); RK = [194](194-step127rk-de09-019-evidence-closure-pc07-01-reconciliation.md); R128 = [195](195-step128-de09-023-prescription-identifier-evidence-correlation.md); R129 = [196](196-step129-de09-026-prescription-status-evidence-correlation.md); R133 = [200](200-step133-de09-003-manual-acceptance-evidence-closure.md); R134 = [201](201-step134-de09-004-drug-description-contract-evidence-assessment.md); R135 = [202](202-step135-de09-004-drug-description-contract-resolution.md). Earlier implementation/tests are reachable through R113/RK rather than copied here. Historical pass totals overlap and are not summed into a new test result.

V means **SATISFIED — VERIFIED**, always with the recorded subset/source/release qualifications. A/B/C identify primary remaining action groups, not additional statuses. A category can remain PARTIAL while its completed child capabilities remain verified.

### Category/default coverage — nine mandatory units

| Requirement ID | Requirement | Obligation | Implementation | Evidence | Status | Remaining action |
|---|---|---|---|---|---|---|
| PC07.01(a)/.02 | Ongoing conditions | M summary/retrieval | Problems clinical fields, dates/stage/status/notes | R113 → Step 106, automated/user V1–V3 | V | Preserve; D qualifications only |
| PC07.01(b)/.03 | Past medical/surgical history | M summary/retrieval | Medical/Surgical kinds, clinical fields, retained archive | R113 → Step 104, automated/three user checks | V | Preserve; no repeat acceptance |
| PC07.01(c)/.04 | Family history | M category | Approved six-field minimum, preferences/output | R113/RK → Step 97 and supplement | IMPLEMENTED — EVIDENCE INCOMPLETE (B) | Complete only residual deployed M7/M8 assertions; retain approved minimum |
| PC07.01(d) | Immunizations | M category | Accepted product/outcome/date/refusal subset | R113 → Step 109; Step 110 | PARTIAL (A) | Add administered dose amount/unit after contract decision |
| PC07.01(e)/.05 | Allergies/reactions | M category | Accepted native DIN/stage/dates/NKA/provenance subset | R113/RK → Step 112 | PARTIAL (C) | Resolve external recorded-date and ambiguous drug-DIN applicability |
| PC07.01(f)/.06 | Medication summary | M category | Ongoing-list source plus separate native prescription support | R115/RK/R133/R135 | PARTIAL (A) | Packages below; settle source association and appropriate full-detail access |
| PC07.01(g)/.07 | Risk factors | M category | Approved four-field minimum | R113/RK → Step 100, automated/user V1–V3 | V | Preserve; optional fields not new blockers |
| PC07.01(h)/.08 | Alerts/special needs | M category | Title/message, safeguards, CPP/preferences/output | R114 automated/user V1–V3 | V | Old R113 missing-alert findings superseded; no new special-needs table |
| PC07.06 default | Ongoing treatment plan by default; acute permitted | M default; acute inclusion MAY | Ongoing/Acute/NULL, four print scopes | R113/RK → Step 102 | V | Preserve; not proof of .016/.025 equivalence |

Category/default subtotal: **five V, two A, one B, one C**. Four category clauses plus the default are V.

### Non-medication mandatory elements — 47 units

| Requirement ID | Requirement | Obligation | Implementation | Evidence | Status | Remaining action |
|---|---|---|---|---|---|---|
| DE05.001/.003–.007 (6) | Relative onset age/stage, condition/procedure, treatment, blood relationship, notes | M S/D; CT-016 | Approved family minimum | R113 → Step 97/supplement | V, approved minimum | B at parent covers residual deployment proof; no recapture of accepted fields |
| DE06.001–.007 (7) | Onset/stage/resolution, problem/description/status/notes | M S/D; full/partial dates, CT-016 | Accepted Problems contract | R113 → Step 106 | V | Preserve clinical versus system dates |
| DE07.001–.008 (8) | Clinical dates/stage, diagnosis/procedure, notes/status | M S/D; record-kind applicability, CT-016 | Accepted History contract | R113 → Step 104 | V | Preserve medical versus surgical applicability |
| DE08.001–.007/.009–.011/.013 (11) | Vaccine/DIN/type/manufacturer/lot/route/site, administration/refusal dates/indicator, notes | M S/D; CT-020; optional route/site terminology | Accepted immunization subset | R113 → Step 109 | V | Preserve unknown/refusal and partial dates |
| DE08.008 (1) | Administered dose amount and unit | M S/D | Missing; DoseNumber is different | Step 110; RK | NOT IMPLEMENTED (A) | Approve numeric/unit/pairing/omission policy; implement once |
| DE11.001/.002/.004–.009 (8) | Agent, drug DIN, onset/stage/severity/reaction, recorded provenance/notes | M; DIN store-only, others S/D; CT-016 | Accepted explicit-Medication DIN/native recorded-date subset | R113/RK → Step 112 | V, native subset | External/ambiguous applicability remains at parent C; do not reopen native/NKA results |
| DE12.001/.002/.006/.007 (4) | Risk factor, exposure, stage, notes | M S/D; CT-016 | Approved minimum | R113 → Step 100 | V | Preserve unknown semantics |
| DE13.001/.002 (2) | Alert/special-need description and notes | M S/D | Existing title/message plus accepted safeguards/CPP integration | R114 | V | Retain source/deployment limits |

Subtotal: **46 V, one A**. Grouped suffix membership follows retained mapping; these are logical elements, not required database column names.

### Medication mandatory elements — all 36 units

| Requirement ID | Requirement | Obligation | Implementation | Evidence | Status | Remaining action |
|---|---|---|---|---|---|---|
| DE09.001/.002 (2) | Written/start dates with full/year-month/year precision | M S/D | Bounded native date contract | R116/RK | V | Preserve chronology/unknown and historical snapshots |
| DE09.003 (1) | Marketed or provider-typed name | M S/D | Independent ProductName and explicit Details label | R133; Step 132 tests/owner observations | V | No repeat brand acceptance |
| DE09.004 (1) | Custom/complex Drug Description | M S/D | ProductDisplayText candidate, 300 UTF-16 units | R134/R135 | IMPLEMENTED — EVIDENCE INCOMPLETE (A) | Fix Application validation asymmetry; explicit purpose/format; compound-specific evidence |
| DE09.005 (1) | Drug-database DIN | M S, conditional source | Arbitrary identifier pair is only a container | R115/RK | PARTIAL (A) | Establish explicit source/namespace/extraction and uncoded policy; then fill proven omissions |
| DE09.006–.010 (5) | Strength, numeric dose, form, route, frequency | M S/D; CT adoption optional | Structured prescription contract | R115/RK → Steps 86/94/102 | V, native slice | Do not redesign; retain source-boundary qualification |
| DE09.011–.015 (5) | Initial/refill duration/quantity, repeat count | M S/D | Independent structured fields and output/correction | R115/RK → Steps 86/94 | V | No new refill-consumption ledger |
| DE09.016 (1) | Long-term medication intent | M S/D | List classification candidate | R115/RK | PARTIAL (C) | Decide plan/order authority and long-term equivalence, without inventing threshold |
| DE09.017 (1) | Past medication indicator | M S/D | Discontinued candidate; retained affected audit/actor safety gaps | R115/RK → Step 102/0077 findings | PARTIAL (A) | Resolve mapping and repair only coupled mutation integrity before extending it |
| DE09.018 (1) | Compliance assessment | M S/D | No explicit supported assessment | R115/RK | NOT IMPLEMENTED (A) | Approve assessor/value/unknown/history then add contract |
| DE09.019/.020 (2) | Private notes / pharmacist instructions | M S/D; private prescription-print exclusion | Separate approved contracts | RK; R115 → Step 94 | V | Reuse V1/V2/V3; preserve private audiences/exclusions |
| DE09.021/.022 (2) | Original prescriber names / OHIP billing identifier | M S/D | Native provider source and frozen credentials | R115/RK | IMPLEMENTED — EVIDENCE INCOMPLETE (C) | Correlate native name/OHIP semantics and original external-prescriber policy; no arbitrary ID substitution |
| DE09.023/.024 (2) | Unique EMR prescription ID / replacement reference | M S; replacement conditional | PrescriptionUid and supersession chain | R128; R115/RK | V | Preserve; no new external ID service |
| DE09.025 (1) | Treatment category | M S; CT-034 optional | No proven order/list equivalence | R115/RK | PARTIAL (C) | Define separately from .016; no blind classification copy |
| DE09.026 (1) | Prescription lifecycle status | M S; CT-035 optional | Existing retained states | R129 | V | No state renaming or lifecycle redesign |
| DE09.027 (1) | Non-authoritative/paper-view indicator | M S | No explicit stored indication | R115/RK | NOT IMPLEMENTED (A) | Resolve applicability/actor/unknown; no invented false default |
| DE09.028 (1) | Minimum dispense interval | M S | Absent; not days' supply | R115/RK | NOT IMPLEMENTED (A) | Approve unit/precision/unknown; add supplied value |
| DE09.029 (1) | Substitution-not-allowed intent | M S/D | Absent; brand choice does not imply prohibition | R115/RK | NOT IMPLEMENTED (A) | Approve nullable intent/ownership/correction/output semantics |
| DE09.030–.032 (3) | Destination facility address/name/identifier | M S | Missing per-order contract | R115/RK | NOT IMPLEMENTED (A) | Resolve source/association/namespace before reuse or extension |
| DE09.033 (1) | Earliest pickup date/time | M S | Missing | R115/RK | NOT IMPLEMENTED (A) | Approve zone/precision/provenance; no derivation from start date |
| DE09.034/.035 (2) | Coded reason / protocol reference | M S | Indication text is not either contract | R115/RK | NOT IMPLEMENTED (A) | Resolve supplied code/reference sources and applicability; no invented codes |
| DE09.036 (1) | Generic Name | M S/D | Independent nullable native recorded name | R118/RK | V | No inference of actual dispensing or generic from brand |

Subtotal: **19 V, 13 A, four C**. A rows can have unresolved design dependencies. This preserves .003/.019/.023/.026 closures and all other explicitly accepted native slices.

### Optional and shared coverage

| Requirement ID | Requirement | Obligation | Implementation | Evidence | Status | Remaining action |
|---|---|---|---|---|---|---|
| DE05.002 (1) | Family start date | Optional; conditional date conformance if supported | Not recertified in this consolidation | Step 95/R113 | NOT APPLICABLE as mandatory gap | Preserve applicable existing support; no new mandatory input |
| DE08.012 (1) | Immunization instructions | Optional | No new completeness assertion | Step 95/R113 | NOT APPLICABLE as mandatory gap | Do not confuse with mandatory dose |
| DE11.003 (1) | Reaction type | Optional | Existing allergy subset retained | Step 95/R113 | NOT APPLICABLE as mandatory gap | Preserve behavior; no new optional mandate |
| DE12.003–.005 (3) | Optional onset age/start/end | Optional; conditional dates | Approved risk minimum unchanged | Step 95/R113 | NOT APPLICABLE as mandatory gap | No invented required timing |
| DE13.003/.004 (2) | Optional clinical active/end dates | Optional; conditional dates | No new clinical dates introduced by Alert closure | R114/Step 95 | NOT APPLICABLE as mandatory gap | System lifecycle timestamps do not silently become clinical dates |
| PC07.01(f), authority/retrieval/output | List versus native/external order ownership and appropriate full supported-detail retrieval | Cross-cutting M mapping, not another counted ID | Separate authoritative list and prescription projections | R113/R115/RK | PARTIAL (C dependency) | Agree explicit association/access and audience matrix; no name-based join or every-field card mandate |
| Affected authorization/audit/retention | Tenant/patient ownership, trusted actor, permissions, clinical/read audit, concurrency, no physical deletion | Cross-cutting safeguard | Existing accepted mechanisms; .017 affected limitations retained | R113/R114/RK | V within accepted scopes; PARTIAL for .017 coupling | Test changed paths only; do not claim domain-wide security from another fixture |

Optional N/A here means **not a mandatory closure blocker**, not proof an optional feature is absent or a waiver of conformance where implemented. Cross-cutting rows do not change source-element counts. No numbered requirement is classified DEFERRED — SPECIFICATION UNCLEAR: retained definitions are available; the open C issues are clinical ownership/design/source decisions, not a reason to re-extract all specifications.

Arithmetic: mandatory data elements 47 + 36 = 83; category/default rows 8 + 1 = 9; 83 + 9 = **92**. V = 46 + 19 + 5 = **70**; A = 1 + 13 + 2 = **16**; B = **1**; C = 4 + 1 = **5**. Eight optional entries give 100 coverage entries. These aggregates explicitly overlap parent and child scope; unique implementation work is smaller than the obligation count.

## 4. Grouped gaps and dependencies

**A — Confirmed implementation gaps:** Application validation/explicit description contract (.004); DIN capability not established by arbitrary text container (.005); affected discontinue integrity (.017); missing compliance, authority, interval, substitution, destination, pickup and coded/reference support (.018/.027–.035); administered immunization dose. Parent Medication/Immunization remain partial for those same child gaps, not additional defects. Do not claim observed truncation or that 300 characters are clinically insufficient.

**B — Evidence only:** residual Family M7/M8 deployed assertions: retain three observed API denials and isolated backend/audit results, without upgrading them to whole deployed/OIDC/Web/state acceptance. Native prescriber correlation and compound preservation also need evidence, but are counted under their A/C primary blocker. No test/source presence is promoted to a new PASS.

**C — Mapping/design uncertainties:** long-term versus treatment category and plan/order authority; discontinued/past mapping; provider/OHIP and external-source attribution; external allergy recorded date and ambiguous DIN classification; native/external/list-detail retrieval and output audience; clinical value/unknown/unit/time/reference policies needed by A. Definitions of dose/interval/compliance are known; their exact entry policies are not. These must be resolved together, not as separate future field prompts.

**D — Retained qualifications, not reopening triggers:** accepted release/source/CPSO applicability; environment/app/migration/engine/OIDC provenance; native versus external/list coverage; owner-reported visual observations; Family approved minimum versus remaining deployed checks; observed privacy/layout limits and selected audit-correlation limits in RK; optional terminology and optional data. No repeated brand or private-note V1/V2/V3 acceptance, whole CPP print/customization retest or new screenshot collection is justified merely by this consolidation.

Dependency order: **one consolidated decision sheet → approved grouped contracts → affected storage/Application/UI changes → targeted automated verification → owner appearance acceptance → evidence reconciliation**. .004 guard work is independently definable; it must not be used to imply all other gaps are ready. Existing legacy/unknown data and signed artifacts must survive every package.

## 5. Coordinated implementation packages — proposals only

| Package / IDs | Defect and minimal change | Layers | Dependencies and principal risks | Focused automated / owner manual acceptance |
|---|---|---|---|---|
| P1 Text and medicine/provider identity: .004/.005/.021/.022 | Clarify complete bounded description purpose; add Application pre-persistence length/required/format validation, defensive repository check if needed. Reuse ProductDisplayText, identifier pair and original-provider source before adding fields. Explicit DIN/OHIP semantics only after source decisions | Application DTO/service; Infrastructure binding/mapping if needed; existing Web editor/Details. DB only for proven missing approved identity contract | Agreed 300-unit single-line baseline or justified alternative; catalogue and external provenance; risk of label-only closure, premature expansion, credential reinterpretation or truncation | Compound/independence/300–301/Unicode/encoding checks; original-prescriber/unknown identifier tests. Owner checks compound text and errors only; preserve .003 closure |
| P2 Plan, lifecycle and prescriber intent: .016–.018/.025/.027–.029 | Reuse list classification/status where legitimately mapped; repair coupled discontinue audit/actor gaps; add only approved compliance, category, authority, interval and substitution contracts | Application DTO/services; Infrastructure; new append-only SQL migration/procedures if required; affected editor/detail | Plan/order ownership, recorded/unknown semantics, actor/time/unit authority; risks of false default, lifecycle reinterpretation, stale writes and historical mutation | Explicit unknown/omission/clear, stale/concurrent and actor/audit rollback tests; draft/finalized/correction/supersession only as affected. Owner checks labels/intents in one coordinated view pass |
| P3 Destination and reference metadata: .030–.035 | Reuse approved facility source and explicitly supplied pickup/reason/protocol values; add grouped per-order contracts only where absent | DTO/service, repository, procedures/schema as justified; editor/detail | Facility namespace/source, time zone/precision, coded/reference provenance and applicability; risks of duplicate pharmacy master, fabricated codes and conversions | Grouped supplied/unknown/boundary/namespace/time round trips, retained snapshot/correction. Owner checks the grouped form/read view; no transmission workflow |
| P4 Remaining clinical-category contracts: DE08.008 and allergy parent residual | Add approved dose amount/unit/presence subgroup; address external original allergy date/ambiguous DIN only after applicability decision and only if a capability gap is confirmed | Existing Immunization/Allergy Application/Infrastructure/Web; additive SQL if approved | Dose domain/unit/pairing/old-client omission; external provenance. Risks: copying prescription dose restrictions, erasing newer data on old grouped saves, NKA/DIN recoding or invented historic dates | Administered/refused/unknown, overprecision/range/unit, omit versus clear, rollback/audit; conditional external-allergy cases only. Owner checks new controls/labels; no repeat accepted V1–V3 |
| P5 Shared CPP retrieval and evidence closure: category parents, full-detail source mapping, Family B | One approved source/audience projection adjustment where actually required; maintain existing summaries, scopes/preferences and full eligible-row access. Complete residual Family deployed assertions; update this evidence index | Existing CPP Application projection/output and thin Web components only as needed; evidence report | Depends on P1–P4 field owners/output policy and available controlled deployment; risks of private-note leakage, name-based linkage, loss of scope or overprinting | Focused full-detail access/owned-source/selection independence/privacy regression, plus only missing Family deployed checks. Owner performs one combined layout pass over newly exposed content; existing closures reused |

Do not redesign stable screens or create a second prescription abstraction. New data changes use existing stored-procedure, DTO, DI, audit and tenant patterns; no EF migrations or edits to migration history. Store-only fields need not be printed. Snapshot schema/version changes apply prospectively only when an approved field must be retained there; older artifacts must render unchanged. No record creation or deployment is authorized by these proposals.

## 6. One bounded consolidated acceptance plan — not executed

| Acceptance block | Automated focus, reusing existing infrastructure | Owner-performed manual focus / evidence gate |
|---|---|---|
| Validation and independence | P1 compound short/multi-ingredient text; distinct ProductName/GenericName/description; exact max/above-max/Unicode and supported whitespace; reject before mutation. Explicit unknown versus omission/clear for all added fields | Readable compound editor/errors/full saved description. Confirm capacity/format with representative synthetic content; no instruction to abbreviate essential meaning |
| Draft persistence and grouped identity/metadata | Native supplied identifier semantics, original provider, grouped destination/reference and approved clinical-intent fields; compare accepted data to read response | One coordinated editor/reload/Details pass; record actual test context if separately authorized |
| Lifecycle and history | Affected new fields survive finalization and correction; source artifact immutable; supersession linkage retained; original author/state/version controls. Reuse existing .023/.024/.026 assertions | Check only newly added content in Draft/finalized/correction views; no repeat acceptance of already verified lifecycle itself |
| Immunization and conditional allergy | Agreed dose numeric/unit/partial presence, refusal versus administration, no DoseNumber inference; legacy omission preserves data. Only approved external-allergy/DIN variants | New dose/unknown/refusal controls; external date/source label if added; accepted native Allergy/NKA UI not recertified |
| CPP/details/output | Correct authority/owned-resource retrieval, all eligible rows accessible, encoded full accepted text, selected/hidden scope independence, private exclusions and historical artifact hashes | One combined newly changed summary/full-detail/output readability check; only where the approved display/audience matrix requires it |
| Security/audit/concurrency | Affected permission denials and tenant/patient boundaries before reads; trusted active actor; stale writes rejected; clinical change/audit atomic; omit/clear history; no physical deletion. Include .017 coupled repair; reuse private-note V1–V3 evidence | No manual replay of prior security suites. Family deployed M7/M8 residual assertions are separately recorded as pass/deferred with actual environment, never inferred from unit tests |
| Evidence gate | Record focused commands/results per changed package; do not sum overlapping historical totals or run a broad suite by default | Attribute owner observations, capture actual environment/tenant/state and remaining qualifications; no Codex browser PASS without execution |

Release/accepted-source evidence is a governance gate, not something tests can manufacture. Full PC07.01 closure requires all applicable A/B/C assertions resolved and verified or explicitly justified against authority. Native subset closures alone cannot remove the parent inventory ID.

## 7. Inventory reconciliation

| Inventory package | Existing mandatory IDs | Before | After | Relationship to this scope |
|---|---|---:|---:|---|
| Prescribing | PC04.01/.06/.07/.09/.14 | 5 | 5 | Other package; reuse relevant evidence, no audit/closure in this step |
| CPP | PC07.01/.04/.07/.10 | 4 | 4 | PC07.01 is the single parent assessed here; .04/.07 overlap Family/Risk but remain independently tracked; .10 is separate workflow scope |
| Scheduling | PC09.03 | 1 | 1 | Other package, untouched |
| Total | Ten distinct top-level IDs / three nonempty packages | **10** | **10** | Internally consistent under retained counting rules |

The 83 data elements, category clauses and default above are subordinate coverage, not extra inventory IDs. Verified Family/Risk minima do not silently remove separate PC07.04/.07 inventory entries; their broader qualifications/closure decisions are outside this task. All category and DE closures listed above remain accepted in their recorded scope. No justified inventory reduction exists.

## 8. Recommended Step 137 and bounded execution sequence

**Recommend Step 137 — PC07.01 consolidated clinical/source-contract prerequisite resolution**, not an unconditional all-gap implementation pass yet. Missing field support is known, but sufficient approved value/source contracts are not available for safely implementing all remaining gaps together.

Produce **one approval-ready decision sheet**, covering these four related blocks in the same step:

1. **Authority and identity:** list/native/external ownership and full-detail access; .016/.025/.017 meanings; original prescriber/OHIP; DIN/facility/code/protocol source and namespace; external allergy applicability. Preserve supported native paths and reject inferred name-based linkage.
2. **Clinical values and lifecycle:** compliance assessor/value/unknown/history; substitution intent; authority indicator; dispense interval; pickup zone/precision; administered immunization dose domain/unit/pairing/presence. Distinguish proposed engineering defaults from authoritative requirements and resolve all necessary choices together.
3. **Description and display:** adopt/reject ProductDisplayText as complete bounded description; accept a supported capacity/format using representative intended content; approve required full-detail/CPP/output audiences while retaining private exclusions and old snapshots.
4. **Acceptance and readiness:** map each A/B/C row to P1–P5 and a focused automated/manual gate; identify the existing environment prerequisites for residual Family evidence and external applicability. Record explicit decisions or exact remaining blockers without another per-element investigation loop.

After those decisions, authorize **one controlled local implementation and focused regression pass using P1–P5**, with the decision sheet as the scope boundary. Independently defined P1 validation work can be included once authorized, without claiming that it closes other packages. Do not start implementation, SQL/deployment, clinical records or tests under this planning report. If the prerequisite sheet cannot settle an external-source policy, isolate that dependency and proceed only with the approved independent packages in the later execution phase; do not invent facts or weaken controls to force whole-PC07.01 closure.

Final checks: report-number availability, retained superseding decisions, matrix membership/arithmetic, local report links, whitespace and final scope review. No new runtime or acceptance evidence. Stop after this report.
