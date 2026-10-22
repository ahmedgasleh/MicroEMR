# Step 141 — PC07.01 acceptance closure and next coordinated implementation scope

2026-10-22. **DE09.004: SATISFIED — VERIFIED (qualified approved native contract). Three owner-facing wording/layout checks: PENDING. PC07.01: PARTIAL. Inventory before/after: 10 mandatory-gap IDs / three packages.** This is evidence reconciliation and planning, not implementation or deployment.

## 1. Git checkpoint and boundaries

Read AGENTS.md first. Recorded branch, HEAD and working-tree status once: **`main`**, **`e1adc3f73bebe3e3dd949e197385149d54021f2a`**, **clean**. The previous reports' earlier HEAD and uncommitted-tree descriptions remain historical; they are not the current checkpoint. No branch creation is required for this documentation task. No staging, commit, reset, cleanup, merge, branch switch or remote operation performed.

Read relevant sections only of [Step 136](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md), [Step 137](204-step137-pc07-01-clinical-source-contract-resolution.md), [Step 138](205-step138-pc07-01-coordinated-implementation-focused-regression.md), [Step 139](206-step139-pc07-01-deployment-readiness-manual-acceptance.md) and [Step 140](207-step140-pc07-01-automated-acceptance-reconciliation.md). Reused subsequent layout-fix evidence from this conversation. No repository-wide assessment, code edit, database access/write, clinical request, build, test rerun or service restart. Only this report is added.

For subsequent implementation, **first create a dedicated local feature branch**, proposed `feature/pc07-01-native-clinical-facts`, preserving any then-existing modifications. Stop before code changes if policy decisions below are still unresolved. Branch creation is a future implementation prerequisite, not performed in Step 141.

## 2. Step 138–140 acceptance evidence reconciliation

Owner confirmation **“The current changes are stable”** is accepted as overall stability confirmation. It does not supply individual clinical wording/layout PASS results, actual migration-ledger hashes or deployed application identity.

| Evidence or slice | Disposition | Closure and qualifications |
|---|---|---|
| Four Drug Description A1–A4 observations | **PASS — supported by owner-reported evidence** | Preserve compound/safe display, boundary attempt, single-line behavior and Draft/Finalized/correction acceptance. No replay. Individual screenshots/record IDs and exact client boundary observations were not supplied; do not invent them |
| Step 138 automation | **PASS — supported by retained evidence** | Reuse 135 .NET + 12 frontend = 147 passing tests; TypeScript PASS and build PASS with six NU1900 metadata warnings. No rerun or new execution claim |
| Step 140 browser coverage | **PASS — supported by retained evidence** | Preserve 13 passing Chromium cases and documented fragment/rendering limits; not authenticated live-tenant acceptance |
| Subsequent Prescription Details layout defect | **PASS — automated correction verified** | Generic-layout defect corrected to `_AppLayout` and content-panel header. Subsequent 16 focused rendering/browser cases and build passed, as recorded in conversation. Includes overlapping prior tests; do not add 16 as distinct new tests or claim a single combined run. No service restart/deployed visual verification claimed |
| Overall current stability | **PASS — owner confirmation** | Supports stability only; does not complete the three checks below |
| Migration execution / matching runtime | **PENDING — deployment attestation** | Owner reported 0086–0088 run; matching ledger/hash and API/Web build were not freshly attested. Do not rerun migrations to resolve an evidence question; any later verification must be scoped read-only first |
| Actual clipboard/browser variants beyond tested interactions | **DEFERRED — bounded limitation** | Chromium typed-length and fill normalization evidence remains distinct from OS clipboard behavior and server rejection. Not a reason to replay all technical checks |

**DE09.004 closure:** the Application/repository/SQL correction, compound-specific persistence/lifecycle tests, safe-rendering/browser evidence and four explicit owner PASS results together justify **SATISFIED — VERIFIED**, limited to the approved complete, single-line, maximum-300-UTF-16-unit native description contract. This is not closure from automated tests alone. It does not establish arbitrary-length/multiline support, universal clinical sufficiency, undisclosed deployed versions or external certification acceptance. If a future intended description cannot fit without losing meaning, treat that concrete example as a new capacity issue. Product/Brand Name and Generic Name remain independent; DE09.003's prior qualified closure is retained.

## 3. Three owner-facing acceptance statuses

These are exactly Step 140 section 5; no additional manual state matrix or lifecycle/security replay is requested.

| Owner check | Status | Remaining observation |
|---|---|---|
| Prescription wording/layout | **PENDING — owner confirmation required** | Confirm separate first/last/OHIP, Treatment type, Substitution prohibited and Not recorded wording and readable current layout. Automated app-shell correction is PASS; explicit owner acceptance is unreported |
| Medication-plan wording/layout | **PENDING — owner confirmation required** | Confirm Long-term treatment intent and Past medication meaning alongside classification and entry/modal placement |
| Allergy wording/layout | **PENDING — owner confirmation required** | Confirm Drug / Non-drug / Unknown / Not recorded distinctions and DIN reconciliation help are understandable/readable |

No current specific defect is established by the available evidence; therefore none of these is classified FAIL. No lack of an owner reply is treated as PASS or a technical defect. If a suitable existing screen is unavailable, that observation may later be recorded DEFERRED with its reason. No clinical writes are needed for these three observations.

Native identity (.021/.022), long-term intent (.016), treatment type (.025), discontinued-plan mapping/integrity (.017), substitution prohibition (.029) and the allergy applicability extension remain **IMPLEMENTED — AWAITING OWNER WORDING/LAYOUT ACCEPTANCE**, with technical PASS evidence retained. Do not reopen Q1–Q4, re-defer implemented substitution or require repeat technical proofs. Step 137 resolved the supposed separate external-original allergy recording-date requirement: DE11.008 concerns recording in this EMR; do not recreate that feature gap. Prior native allergy/NKA and other closures remain qualified and accepted.

## 4. Remaining requirements — grouped delta only

Step 136 remains the controlling 92-unit matrix. This report changes only the relevant evidence disposition; it does not recreate/recount the matrix. Its historical 70/16/1/5 primary-blocker totals must not be presented as newly recomputed totals after Step 138 implementation.

| Remaining group | Requirements | Readiness and retained boundary |
|---|---|---|
| Compliance and clinical authority | DE09.018; DE09.027 | **Requires owner policy decision.** Missing compliance assessor/value/history and paper/non-authoritative-view applicability/actor contract. Original-prescriber identity and substitution already implemented; not part of this missing work |
| Prescription interval and related clinical values | DE09.028 | **Requires owner policy decision.** Explicit minimum interval amount/unit/precision/domain; independent of days' supply, refills, actual dispensing and treatment intent |
| Immunization administered dose | DE08.008 and immunization category residual | **Requires owner policy decision.** Amount + unit, domain/precision and new-entry/legacy partial-pair rules. DoseNumber and refusal are distinct; retain accepted immunization fields |
| Drug/facility/reason/protocol references | DE09.005, .030–.032, .034/.035 | **Requires actual approved source** for source-backed DIN/extraction and governed identifiers/codes; **requires owner source/policy choice** for entered facility/reference facts and applicability. No assumption that arbitrary text or an identifier container proves provenance |
| Pickup time | DE09.033 | **Requires owner policy decision.** Earliest pickup precision/time-zone/unknown representation and provenance; no derivation from written/start dates |
| CPP retrieval/audience and Family deployed evidence | Medication category parent; shared plan/order source and audience boundaries; Family M7/M8 | Family residual is **evidence-only**. New association/output behavior **requires owner policy decision** if an extension is required; existing independently owned sources, navigation, selection/preferences and private-note exclusions remain **retained qualifications**. No new printing requirement inferred from store-only fields |

**Ready now:** evidence indexing, the three short existing-screen observations, and planning against the already-approved safeguards. **No remaining new clinical-field package is unconditionally implementation-ready:** the missing actor/numeric/time/source decisions are substantive. An additive DTO/column alone would not resolve them. The present work must not manufacture an unblocked feature by guessing those policies.

## 5. Smallest next coordinated implementation package

Recommend one **native clinical facts package** covering **compliance + paper-view authority + minimum dispense interval (DE09.018/.027/.028)**, conditional on decisions D1–D3 below. These share clinical ownership, unknown/omission/clear, retained history and governed read/write behavior and do not require an external catalogue integration. Do not split them into three field investigations. Dose and reference metadata remain separately gated subpackages; they need not hold this smaller package after D1–D3 are approved.

Proposed boundaries after explicit contract approval:

- Reuse existing Clean Architecture DTO/service/repository/UI and stored-procedure patterns; add only necessary append-only migration(s), with no edits to 0086–0088 or historical artifacts.
- Record explicitly supplied facts. Omitted subgroup/property preserves; deliberate clear is audited; no guessed values, inferred authority, default numeric amount or legacy backfill. Determine actual owner per D1/D2 before adding fields; no name-based plan/order synchronization.
- Apply original-prescriber/state/RowVersion rules to order-owned Draft facts and preserve them prospectively through finalized snapshots/corrections. Compliance history follows its approved owner and temporal contract; do not attach a mutable longitudinal assessment to immutable orders by convenience.
- Keep tenant/patient permissions, active trusted actor, clinical audit/rollback and private-note audience boundaries. No transmission workflow, actual-dispensing inference, expanded CPP printing or external integration.
- Add only focused contract/persistence/rendering tests for the new package, with boundary/domain, omitted/cleared, unauthorized/stale and audit rollback cases. Reuse all unaffected acceptance; one grouped clinical wording/layout review for new content.

## 6. Exact blocking decisions and conservative proposals

The proposals are engineering/clinical-policy options for approval, **not OntarioMD-specified numeric limits or preapproved contracts**. Existing Q1–Q4 decisions are not asked again.

| Decision | Exact missing answer | Conservative proposed choice / effect |
|---|---|---|
| **D1 Compliance (Q5 residual)** | Owning resource (plan assessment versus order fact), authorized assessor/permission, assessment representation, effective/recorded time and correction/history rules; if text, approved capacity | Prefer explicitly supplied optional assessment text without an invented compliance scale; trusted assessor and recorded timestamp, retained revisions. A longitudinal plan assessment is a candidate, not an approved ownership change. Unknown remains absent. Owner must select ownership/actor/time and capacity before implementation |
| **D2 Paper-view authority (Q5 residual)** | Exact meaning of Yes/No/unknown for non-authoritative/paper-view requirement; authorized declaring actor; applicable states and effect on current authority behavior | Prefer nullable explicit paper-view requirement with no automatic electronic-authority claim, no default from non-transmission disclaimer, no backfill. Owner must approve the declaration semantics and actor; do not build an electronic-authority path |
| **D3 Minimum interval (Q5 residual)** | Allowed unit(s), integer versus decimal, precision/scale, valid minimum/maximum and zero policy | Prefer a single explicit approved time unit and positive value; reject unsupported precision rather than round. Days as whole numbers is a possible conservative option, not a requirement or selected policy. Owner must supply the permitted range and confirm suitability; never substitute days' supply |
| **D4 Administered dose (Q6)** | Numeric precision/range, zero/negative behavior, allowed units, amount+unit pairing for new entries versus as-recorded partial history, refused-record behavior | Prefer positive explicitly recorded amount, explicit unit, both-or-neither for new known doses, no rounding and no fabricated dose on refusal; preserve old unknown/as-recorded content. Actual precision/range/unit policy remains unapproved; do not reuse prescription dose limits |
| **D5 Real sources (Q7)** | Approved accessible drug-data source and DIN extraction/provenance; facility directory or entered-fact alternative and identifier namespace; reason-code/protocol sources/namespaces and applicability; import/service/access approval | Keep source-qualified supplied facts and unknowns distinct from verified source extraction. Require a real sample/accessible approved source before claiming integration readiness. Do not create a pharmacy master, invent codes or infer them from indication |
| **D6 Pickup policy (Q7)** | Zone/offset representation, supported precision, unknown/partial handling and source | Prefer an explicitly supplied timestamp with known offset, preserving the supplied meaning and no default from server local time. Owner must approve whether incomplete local/date-only input is permitted and how it is represented |
| **D7 CPP relationships/audience (Q7)** | Whether existing separate-source navigation satisfies the required retrieval; if not, exact owned relationship and audience/projection needed; controlled environment for residual Family M7/M8 | Prefer existing independent authoritative sources/navigation and current privacy/selection rules. Add an explicit owned link only when a real association and requirement are established. Family residual needs scoped evidence execution, not a new field/schema |

OntarioMD's retained mapping establishes required content/support, including drug-database DIN and source-qualified references; it does not configure an accessible data service, grant access/licensing or settle local numeric/actor/time policy. Step 138's public DPD documentation finding is retained historical context, not a current connectivity check or proof of a usable MicroEMR integration. No web lookup/integration assessment is repeated here. External integration requires separate approval.

D1–D3 block the recommended native package. D4–D7 block their dependent later work only. Missing acceptance observations do not invalidate passed technical tests, and a missing external source does not block unrelated native facts once their policies are approved.

## 7. Certification and inventory reconciliation

**PC07.01 remains PARTIAL.** Qualified DE09.004 closure is supported by implementation plus explicit owner acceptance; remaining implemented slices retain their three specific owner-facing acceptance gates. Earlier DE09.003/.019/.023/.026 and all other accepted native/category closures and release qualifications remain intact. No full CPP, external prescribing, deployed/OIDC/Family or whole-parent verification is inferred.

| Inventory package | Before | After |
|---|---:|---:|
| Prescribing — PC04.01/.06/.07/.09/.14 | 5 | 5 |
| CPP — PC07.01/.04/.07/.10 | 4 | 4 |
| Scheduling — PC09.03 | 1 | 1 |
| **Mandatory-gap IDs / nonempty packages** | **10 / 3** | **10 / 3** |

Subordinate DE closure and test totals do not remove an independently counted parent ID. No verified evidence justifies an inventory reduction. No new coverage-unit total is asserted.

## 8. Recommended Step 142 and stop

**Step 142 — PC07.01 grouped native clinical-fact contract approval and implementation readiness.** Obtain explicit answers to D1–D3 together, and record the three short owner wording/layout observations as PASS/PENDING/DEFERRED/FAIL. Return one concrete approved DTO/ownership/lifecycle/validation/migration/test plan for compliance, authority and interval, with D4–D7 retained as dependent subpackages. Do not reopen the full matrix, Q1–Q4, completed Drug Description acceptance or V1/V2/V3.

If the Step 142 request supplies D1–D3 and explicitly authorizes implementation, create the dedicated local feature branch first and execute the coordinated package with focused tests. Otherwise stop at the approved plan; do not infer policy from stability confirmation or implement guessed fields. Any deployment verification is separately scoped read-only first; no automatic migrations, external integration, clinical writes, restart or Git commit/push.

Documentation checks: report filename available, controlling relative links resolve, whitespace checked, Git checkpoint recorded once, prior decisions preserved. No builds/tests executed. **Only this report added; stop after documentation.**
