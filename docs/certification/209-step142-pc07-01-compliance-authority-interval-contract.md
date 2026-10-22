# Step 142 — PC07.01 compliance, paper authority and dispense interval contract

2026-10-22. **Definitions resolved; proposed application policies require owner approval. Coordinated implementation: conditionally ready, not authorized or executed.** PC07.01 remains **PARTIAL**; inventory **10 mandatory-gap IDs / three packages**, unchanged.

## 1. Git checkpoint and evidence boundary

Read AGENTS.md first. Branch **`main`**, HEAD **`e1adc3f73bebe3e3dd949e197385149d54021f2a`**; status recorded once: only untracked `docs/certification/208-step141-pc07-01-acceptance-closure-next-implementation-scope.md`. Preserved it. This step adds only this report. No branch creation, staging, commit, merge, reset, cleanup or remote operation.

Targeted reads: [Step 141 sections 4–8](208-step141-pc07-01-acceptance-closure-next-implementation-scope.md), [Step 137 Q5](204-step137-pc07-01-clinical-source-contract-resolution.md), [Step 115 authority/mapping](150-step115-pc07-01-medication-de09-requirement-resolution.md), existing presence-aware plan DTO, prescription text validation, affected controller permission declarations, Step 140 browser renderer and existing isolated prescription fixture. No broad assessment, build, test execution, database query/write, browser launch, download, installation or service restart.

## 2. Direct authoritative findings

Directly read only **Data Dictionary rows 144, 153 and 154** as workbook XML from the established [CDS-S Data Dictionary v2.1 Final](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>). Definitions below are paraphrased. Reused Step 115's legend/release qualification; no new PDF or internet investigation.

| Element / source row | Definition and mandatory support | Resolution |
|---|---|---|
| **DE09.018 Patient Compliance — 144** | Whether the patient complies with medication as prescribed; **M S/D** | **Resolved from authoritative definition:** assessment of adherence to the prescribed medication. Not inferred from active status, repeats, classification or supply. The row does not prescribe resource ownership, assessor, assessment method, value set, timing, unknown handling or score |
| **DE09.027 Non Authoritative Indicator — 153** | Whether dispensing requires viewing a paper prescription; whether an electronically retrieved prescription is authoritative by itself must be explicitly stated; **M S** | **Resolved from authoritative definition:** paper-view/electronic-representation authority fact. It is not a new legal prescribing permission, transport acknowledgment or electronic transmission capability. The row does not establish who may make a declaration or the governing authority policy |
| **DE09.028 Dispense Interval — 154** | Minimum elapsed time required between dispenses; **M S** | **Resolved from authoritative definition:** independent minimum interval, not days' supply, duration, repeats or pickup date. No unit, numeric range, precision or terminology is prescribed in the inspected row |

Mandatory means product support, not that every historical record contains a known value. Store-only does not itself require printing. Unknown policy, technical capacities and owner-authorized clinical declarations below are application design, not invented certification requirements. No universal cardinality or code set is inferred.

## 3. Proposed coordinated contracts

### A. Compliance assessment

**Resolved from established MicroEMR contract:** PatientMedication is the independently owned plan; PatientPrescription is the order; there is no safe implicit association by name. Existing plan mutations require `Patients.View` and `ClinicalData.Manage`, and order prescribing uses `Prescriptions.Prescribe`. These are current permissions, not proof every permitted user is a clinically authorized assessor. Active trusted actor, scope, RowVersion and atomic clinical audit remain compulsory.

**Proposed design requiring owner approval — D1:** record a current assessment plus retained revisions **on the explicitly selected PatientMedication plan**, not a new independent clinical-record subsystem and not a mutable field on a finalized prescription. The assessment must refer to medication as prescribed; if the chosen plan does not establish that clinical context, do not fabricate a link or infer a prescription. Owner must approve this plan mapping for the intended native use, or require an explicit order-owned alternative before code changes.

Proposed entry values are **Compliant / Not compliant / Unknown / Not assessed**. These are local clinical labels, not a required terminology or score. Optional explanatory text supplies nuance. NULL means **Not recorded** (including legacy data), distinct from explicit **Unknown** (assessment unable to determine) and **Not assessed** (recorded absence of assessment). Do not label unknown as noncompliance. Whether these choices suffice for partial/intermittent adherence needs owner approval; no numeric score or inferred threshold is proposed.

Proposed explanation capacity **1,000 UTF-16 units**, plain text with safe display and preserved internal content; this is a technical proposal, not a specification minimum. Reject over-capacity before SQL binding; do not silently shorten. Explanation may be blank but a supplied assessment group must have an explicit state or deliberate clear.

Capture **system-recorded UTC time and trusted actor** on every successful assessment revision, plus a separately explicit **assessment instant with offset** when known. Do not substitute save time for an unknown assessment time. Do not accept client actor/recorded-time spoofing. Proposed assessor rule: active clinical user with existing plan-write permission **and an owner-approved clinical assessor eligibility rule**. Reuse an existing trusted provider/clinical identity rule where suitable; do not assume original-prescriber ownership of all longitudinal assessments, invent a role mapping or change permission grants.

Omitted assessment subgroup/property preserves current values; explicit state NULL deliberately clears the current assessment and retains audit/revision history. Corrections append a revision with actor/time; no revision deletion or retrospective timestamp overwrite. Finalized prescription artifacts are unchanged: this plan assessment is not automatically copied into orders, corrections or CPP printouts. Historical/as-recorded assessments stay distinguishable from current assessment. **Owner must approve ownership, assessor eligibility, state set/capacity and these temporal/correction rules.**

### B. Paper-view authority

**Resolved from authoritative definition:** a paper-view requirement must be explicit, and unknown does not establish independent electronic authority. **Resolved from established contract:** a local/non-transmission disclaimer, finalized status or signature is not the missing stored declaration; original-prescriber Draft/state/version rules and immutable finalization/correction already exist.

**Proposed design requiring owner approval — D2:** order-owned nullable `PaperCopyRequiredBeforeDispense`, with **Yes / No / Not recorded**, attributed to the trusted original prescriber when entered/changed. Yes records that paper must be viewed. NULL makes no authority assertion. No is a substantive declaration that paper viewing is not required; **do not enable a No declaration merely because the UI can store false**. Owner must identify its permitted scope/declaring actor and governing authority basis. A local record of a declaration does not itself create legal validity, dispensing permission or transmission.

Conservative current-native option: permit **Yes and Not recorded only** until the owner supplies a governing basis for No/electronic-authority declarations. Reserve a separate explicit authority interpretation if the approved wider policy needs it; do not conflate absence of a paper requirement with a fabricated independently authoritative electronic prescription. This restricted option is implementable after approval but leaves full DE09.027 electronic-authority capability **qualified/incomplete**, not automatically verified. Alternatively approve all three states only with explicit policy covering their meaning. No legal-policy investigation or conclusion is claimed here.

Omission preserves; explicit NULL clears in Draft with audit. Snapshot the approved recorded fact prospectively on finalization; correction Draft inherits the source value as a recorded fact and requires original-prescriber review before changing it. Copying a value does not renew or prove legal authority. Existing signed artifacts/legacy NULLs are never backfilled. No source prescription edits, new transmission, signature redesign or print/PDF changes in this package.

### C. Minimum dispense interval

**Proposed design requiring owner approval — D3:** explicit order-owned interval in **whole days**, stored as a nullable positive SQL `int`; **1–2,147,483,647 days** is the proposed representational domain, with **zero decimal precision**. The upper bound is the storage type's limit, not a clinically recommended maximum or an OntarioMD limit. No DateTime addition, dispense event scheduling or date derivation is introduced. Owner must confirm whole days meet intended use, approve a narrower clinically meaningful upper bound if desired, or require fractional days/hours before implementation. This proposal cannot silently exclude an approved use requiring sub-day intervals.

NULL = no recorded interval, not zero/no restriction. Reject zero, negatives, fractions, exponent/overflow or unsupported unit input according to the approved wire contract; reject rather than round, clamp or parse into another unit. Client controls assist entry; Application and direct stored-procedure inputs enforce the same domain before narrowing. If DTO integer deserialization rejects fractions, retain that explicit API validation outcome rather than expecting a service-layer call.

Omitted subgroup/property preserves; explicit NULL clears with audit; no default from supply/repeats/duration and no historical backfill. Original-prescriber Draft edits use RowVersion; finalization snapshots the supplied interval; correction inherits it without modifying the old artifact. No actual-dispensing ledger or enforcement integration is implied. **Exact units/domain/precision remain proposed until D3 approval.**

## 4. Exact remaining owner questions and readiness

| Decision | Owner answer required | Readiness classification |
|---|---|---|
| **D1** | Approve plan-owned retained assessment (or choose order ownership); name eligible clinical assessor rule beyond broad write permission; approve Compliant/Not compliant/Unknown/Not assessed/NULL, optional 1,000-unit explanation, separate known assessment instant versus recorded UTC and revision/clear behavior. If partial adherence needs another state, specify it | **Proposed design requiring owner approval**; assessor eligibility is **blocked by missing policy** until named |
| **D2** | Approve original-prescriber order declaration and choose **restricted Yes/Not recorded**, retaining incomplete wider authority support, or supply the governing meaning/authorized actor for **No** and independently authoritative electronic representations. Approve prospective snapshot/correction rules | **Proposed design requiring owner approval**; unrestricted No/electronic path **blocked by missing authority policy** |
| **D3** | Approve whole-day positive integer intervals with the stated storage cap, or specify units, precision and bounds; confirm omission/clear and lifecycle rules | **Proposed design requiring owner approval**, not a missing authoritative definition |

Definitions, existing isolation/lifecycle safeguards and automated-test architecture are resolved. **No code implementation is unconditionally ready until D1–D3 are explicitly answered and implementation is authorized.** Restricted D2 is a concrete safe option, not an assumed approval. Do not ask for Q1–Q4 again. Immunization dose, reference sources, pickup and new CPP audiences remain outside this package and do not block approved native facts.

## 5. Coordinated implementation plan after approval

| Work area | One package, respecting separate clinical owners |
|---|---|
| DTOs / Application | Presence-aware assessment subgroup on approved owner; independently presence-aware authority/interval order properties. Separate response projection, validation and normalization; omitted versus NULL handled per property so old grouped clients cannot erase newer values. No inferred assessment, authority or interval |
| Actors / permissions | Thin controllers use existing authenticated trusted actor; require patient-view and existing applicable write/prescribe permission plus approved assessor eligibility/original-prescriber rules. Enforce owned patient/tenant scope before success; no client actor, role grants or permissive production fallbacks |
| Infrastructure / SQL | Reuse repositories and stored procedures. At implementation time select the next unused migration ID; add nullable fields and only required retained assessment-history storage. No old migration edits, EF migration or clinical backfill. SQL validates raw parameters before bounded assignment; keeps eligibility, RowVersion, full old/new audit and state changes atomic |
| Audit / concurrency | Audit assessment states/text/known assessment instant and order facts with trusted actor/time. Failed audit rolls back clinical row, history, token and prospective artifacts. Stale save returns review/reload conflict; no silent retry, mutation or double revision |
| UI | Existing Bootstrap plan forms/quick-add/edit modal and read-only details show approved assessment states/provenance; existing prescription form/Details show approved authority options and interval units. Accessible labels/help/errors; retain unsaved values. Avoid redesign and do not hide explanation/DIN/private text by unrelated changes |
| Lifecycle | Plan assessment revision history remains independent. Approved order facts persist Draft→finalized→correction with original artifacts unchanged and no live historical enrichment. If D1 chooses order ownership, revise the assessment snapshot/immutability plan explicitly before implementation |
| Outputs | Store/display assessment in governed full-detail UI. Store-only authority/interval do not automatically expand CPP/PDF/print audience. Preserve existing private-note suppression and independent plan/order sources |
| Verification | Build only affected project if required for changed tests; run focused new/directly affected coverage, no blanket rerun of 147 prior tests. Produce a single evidence report with exact test types/results/limits and small residual clinical review |

Before code: create a dedicated local feature branch (proposed `feature/pc07-01-native-clinical-facts`) while preserving report 208 and any other modifications. Stop/request guidance if branch creation would risk existing work. No commit/merge/push. Test database setup is separately authorized as part of the later isolated-test task; no live tenant migration, clinical action or service restart follows automatically.

## 6. Maximum feasible automated acceptance matrix — planned, not run

| Layer | Required scenarios / expected proof |
|---|---|
| Unit / Application | Approved states and time/text/domain boundaries; Unknown versus Not assessed versus NULL; raw overlength/overprecision before repository; JSON omission versus clear for each independent property; no derivation from status/supply/repeats; actor never sourced from payload |
| API / controllers | Valid round trips and exact 400/domain errors, unauthenticated/forbidden, inactive or ineligible assessor, non-original order author, foreign patient and both tenant directions. Test existing production guards through an isolated authenticated test host; no production bypass or role changes |
| SQL integration | Existing generated LocalDB disposable fixtures, migrations only there; direct procedure invalid inputs, legacy NULL/token preservation, older-client group omissions, exact audit/history, denied/foreign writes unchanged. Correctness at boundary and no narrowing/rounding before rejection |
| Concurrency / audit | Two stale/concurrent edits, one winning current-token mutation; forced required-audit failure rolls back row/revision/token/artifact; clear retains history; known assessment time does not overwrite recorded UTC |
| Order lifecycle | Both existing finalize paths, snapshot new facts, correction copying/change review, cancellation/supersession unaffected, immutable original artifact/old NULLs; plan assessment update never mutates signed prescription |
| Actual Chromium UI | Production full pages rendered by Razor in isolated local Web/API test hosts, with local Bootstrap/theme assets and real emitted scripts. Exercise actual controls and normal form submission/reload, entry/modal loading, clear, error retention, read-only Details and conflict behavior. Verify DOM labels/help/options, safe punctuation, units, unknowns and actor/time rendering. Do not count literal source-select extraction as full-page evidence |
| Desktop / mobile | Representative full-page screenshots at **1440×900** and **390×844** for plan entry/details and prescription entry/details, plus modal and error states as affected. Assert visible controls, no unintended horizontal overflow, no clipped error text/primary actions; wait for local CSS/fonts and stable content |
| Accessibility | Browser DOM accessible names/label association, keyboard navigation, focus/error summary linkage, roles/live announcements and applicable disabled/read-only states. These are focused accessibility checks, not WCAG certification. Run a broader scanner only if an existing approved executable/library is actually available; otherwise record scanner unavailability, do not install one |
| Scoped regression | Existing plan intent/classification, prescription text/identity/substitution/private audience, and affected correction/permission/audit tests only where new edits could affect them. Preserve prior PASS evidence; no replay of unrelated brand, allergy, CPP or scheduling suites |

All test data are isolated synthetic fixtures. Include service/controller tests even when browser validation blocks an invalid input, rather than bypassing browser protections against a clinical tenant. Intercepted browser error responses may test UI conflict/error retention, but label them **stubbed transport** and pair with separate real API/SQL rejection tests; do not call a mocked response an end-to-end transaction.

## 7. Browser-rendered verification feasibility and exact prerequisites

**Feasible using existing assets, subject to later runtime permission:** Infrastructure already references **Microsoft.Playwright 1.61.0**, transitively available to the test project. Existing `.tmp/step116-browsers/chromium_headless_shell-1228` and the isolated output `.playwright` driver directory are present by read-only inspection. Step 140 demonstrates Chromium execution; the subsequent layout tests cover `_AppLayout`. None is launched here; asset presence is not a fresh launch-success claim. No package install/browser download is authorized or needed by this plan.

Upgrade the scope of the subsequent tests beyond Step 140's medication source fragments: render actual full production Razor pages with `_AppLayout`, serve existing local CSS/JS at loopback test-host URLs, and operate the real controls in Chromium. Use the existing test-only actor/tenant fixtures and ephemeral DataProtection for host-generated antiforgery; retain antiforgery/cookie handling on normal browser posts. Application endpoints resolve only approved disposable databases; deny external/live-service routes. Do not replace production auth guards with permissive fallbacks. The isolated test-auth fixture proves scoped application behavior, not real OIDC deployment.

Evidence types must be recorded separately:

- **Actual browser execution:** executable Chromium case and DOM interactions/assertions, viewport, console/page failures and network prerequisites.
- **HTML/source assertions:** useful narrow contract checks; not browser/layout verification and not substitutes for rendering production forms with model binding.
- **Screenshot capture:** timestamped synthetic state/viewport PNG; capture alone does not mean someone inspected it or that layout is correct.
- **Screenshot comparison:** run only against an explicitly reviewed/approved baseline at the same viewport/environment, with documented tolerance and dynamic GUID/time masking. **No approved baseline was established in this step**; absent that prerequisite, mark comparison NOT RUN and retain screenshots/DOM results. Never auto-approve generated baselines as a visual PASS.
- **Manual clinical acceptance:** short owner check of the new state wording, paper-authority interpretation and interval suitability; automation cannot approve clinical policy. Previous three wording/layout checks remain PENDING until explicitly reported. Do not repeat completed Drug Description checks.

If later execution fails, state the precise missing prerequisite and affected case: launch permission or missing matching Chromium/driver; LocalDB access for disposable stores; loopback-host permission; built matching Web/API assets and local CSS/script routes; required isolated identity/actor mapping; accessibility scanner; or approved screenshot baseline. Missing one prerequisite must not be reported as an unexplained blanket inability to automate. Do not download/install, use a working tenant, grant roles, restart services or expand external access to resolve it without authorization. Current facts: installed assets exist, fixture code exists; accessibility-scanner and visual-baseline availability are **not established**, and nothing was executed.

## 8. Certification, inventory and recommended Step 143

**DE09.004 remains SATISFIED — VERIFIED, qualified. PC07.01 remains PARTIAL.** Four Drug Description owner PASS results, 147 earlier test passes, 13 Step 140 browser passes and subsequent layout-fix evidence are retained, not rerun or summed into a new run. Three owner wording/layout checks remain **PENDING**; overall stability does not supply their individual results. DE09.018/.027/.028 remain implementation gaps pending policy/implementation/evidence; this plan is not their closure. Prior closures, external release/source qualifications and deferred Q6/Q7 remain unchanged.

**Inventory before: 10 mandatory-gap IDs / three packages. After: 10 / three packages.** No parent closure, new 92-unit recount or reduction from planned automation.

**Recommended Step 143:** obtain one explicit D1–D3 decision response, then authorize the single coordinated package and its isolated automated/browser acceptance. With approved policies, create the dedicated local branch first and implement/test together; if a decision is withheld, stop the affected work at its concrete proposal rather than guessing. Restricted authority support must retain its stated qualification. Produce one combined implementation/evidence report, including full rendered desktop/mobile browser results and exact screenshot/comparison/accessibility limits. No external integration, live tenant migration or record change is included by implication.

Final documentation checks: filename was available; controlling relative links and whitespace checked. **Only this report created. No builds/tests/browser/service/database/Git mutation operations performed. Stop after documentation.**
