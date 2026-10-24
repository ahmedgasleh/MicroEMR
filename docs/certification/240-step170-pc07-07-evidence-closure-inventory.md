# Step 170 — PC07.07 formal evidence closure and inventory reconciliation

2026-10-24 (America/Toronto). **PC07.07 — SATISFIED — VERIFIED (qualified). Removed from the unresolved mandatory-gap inventory: 10 → 9 distinct IDs. Package count: 3 → 3, independently recalculated. PC07.01 remains PARTIAL.** Documentation-only closure using accepted evidence; no new implementation or verification.

## 1. Requirement and formal closure decision

Authoritative requirement, Primary Care Baseline Requirements **1.7 Final, page 28, PC07.07, Mandatory**:

> The EMR Offering MUST display risk factors.

Adjacent guideline:

> Refer to OMD EMR Core Data Set Standards (CDS-S) – Data Dictionary

[Step 169A section 1](236-step169a-pc07-07-requirement-recovery-verification-plan.md) recovered the original local PDF and exact row, replacing Step 168's unavailable-source-path limitation. Its actual path, cover/version and SHA-256 are retained there; no source recovery is repeated. No additional subrequirement was found in that row. The accepted referenced minimum supports four independent Risk fields, including exact CT-016 life stage, while retaining optional-field and unknown-value semantics.

| Closure assertion | Accepted evidence and its scope |
|---|---|
| Display actual Risk Factors | [Step 169D sections 4–5](239-step169d-pc07-07-risk-fixture-deployment-verification.md): real deployed chart and CPP show the authorized populated record; exact factor, exposure details, Adult first-exposure life stage and notes match tenant SQL and actual Web read responses. No mocks or code-presence-only claim |
| Normal tenant/actor and allowed read/create path | Step 169D sections 2–4: one authorized normal login as `admin@microemr.local`, selected `local-dev-fresh`, trusted actor 2 and correct patient; exactly one authorized normal Web Create with matching audit |
| Deployment presentation and preferences | Step 169D section 5: **desktop 6 PASS / 0 FAIL; mobile 4 PASS / 0 FAIL**. Saved CPP preferences/control states and four visible values agree; labels, keyboard order, layout and five inspected screenshots support the deployed display |
| Persisted identity, audit and read-only integrity | Step 169D sections 4/6: record `8265897f-1b22-4bc9-9abc-fa7f5d40fa1b`, Active; Create audit 1132 attributes patient/actor and four values. Post-create RowVersion `0x0000000000002723` / `AAAAAAAAJyM=` remains unchanged. **One authorized Create overall, zero subsequent clinical writes**, plus 20 permitted chart-read audit entries |
| Broader implemented behavior, safeguards and output | [Step 168 sections 2–3](235-step168-pc07-07-risk-factors-evidence-reconciliation.md) maps retained Step 100 **35 .NET PASS**, **8 frontend PASS**, owner V1–V3 appearance PASS, isolated patient/tenant/permission/audit/rollback/concurrency, CPP/preferences/full-source print and retained history coverage. These are prior results in their original scope, not extra deployed browser cases |

**Formal decision: PC07.07 — SATISFIED — VERIFIED (qualified).** The mandatory display requirement and accepted referenced minimum are supported by implementation, retained scoped automated/owner evidence and successful Risk-specific normal-session deployment correlation. Step 168's incomplete exact-source and deployed-read assertions are resolved by Steps 169A and 169D respectively. The earlier fixture blocker was resolved by the specifically authorized single synthetic Create, not by an assumed fixture or unsupported PASS.

This closes the independently tracked PC07.07 requirement under the established internal evidence/counting convention. It is not an external OntarioMD acceptance decision, a whole-CPP closure or evidence for another domain's deferred assertions.

## 2. Qualifications preserved

- Future clinician review remains planned and not performed. Technical owner approval and automated results are not clinical authority or external OntarioMD/CPSO acceptance.
- Accepted-source/release applicability remains qualified: Baseline folder 5.5 versus document 1.7, and CDS-S DFU folder/workbook/target-release distinctions. The exact recovered wording does not establish external release acceptance.
- Live restricted-user denial, optional deployed omission-save and deliberate live stale-write remain deferred within their prior scopes. Isolated Risk safeguards remain PASS; the positive Step 169D session does not claim execution of those live adversarial scenarios.
- Family M7/M8 remains its own incomplete evidence lane. No Family closure follows from Risk tenant/actor or audit success.
- Print category visibility was checked without generating another PDF; retained full-source/print tests and owner V3 appearance acceptance remain sufficient in their recorded scope. Populated archived/optional NULL/date/volume observations were not manufactured. Focused label/focus/layout evidence is not full WCAG or physical-device certification.
- Fixture remains Active as authorized. No cleanup, archive, deletion, production-readiness or unrelated requirement acceptance is implied.

These qualifications remain explicit after removal from this **functional mandatory-gap** list. They are not silently completed, erased or converted into extra counted IDs. An external acceptance finding or new requirement-specific defect could require reassessment; no such finding is established here.

## 3. Tracker location, counting rules and update method

The confirmed existing requirement-level tracking format is the per-step Markdown inventory table, controlled by [Step 136 section 7](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md), [Step 156 section 7](223-step156-pc07-01-owner-acceptance-and-evidence-consolidation.md), [Step 161 section 2](228-step161-pc07-01-remaining-gap-prioritization.md), and most recently [Step 167 section 3](234-step167-de08-008-evidence-closure-next-gap.md). The earlier ten-ID checkpoints are historical; they are preserved unchanged.

Targeted documentation filename checks did not establish a separate maintained certification-tracker file/index. The coarse module inventory, preliminary gap map, early source-readiness register and migration-status documents are not the current requirement closure ledger. No unidentified external tracker or unrelated status document is edited. **The table below is the new current inventory entry in the confirmed append-only per-step tracking format**, superseding the unresolved-list counts in those historical reports. Only this report is changed in Step 170.

Counting rule: count each distinct unresolved **top-level requirement ID** once. Subordinate DE elements, category assertions, test cases and cross-cutting/deferred scenarios are not additional IDs. PC07.07 is independently counted in the baseline, so its formal requirement-specific closure removes exactly one. DE08.008 is subordinate to PC07.01; its earlier closure does not remove that parent. No historical overlapping coverage-unit total or automated-case aggregate is recounted as a gap inventory.

## 4. Mandatory-gap inventory — before and after

| Inventory package | Previous unresolved IDs | Closed in Step 170 | Remaining unresolved IDs | Count before → after |
|---|---|---|---|---:|
| Prescribing | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | None | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | **5 → 5** |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 | **PC07.07** | PC07.01, PC07.04, PC07.10 | **4 → 3** |
| Scheduling | PC09.03 | None | PC09.03 | **1 → 1** |
| **Total distinct mandatory-gap IDs** | **10** | **1** | **9** | **10 → 9** |

Package count was recalculated from the remaining memberships: prescribing has **5**, CPP **3**, scheduling **1**; all are nonempty. Therefore **three packages remain**, rather than assuming that package count from the prior heading. Arithmetic: **5 + 3 + 1 = 9**.

| Remaining ID | Actual residual retained; no new implementation assessment |
|---|---|
| PC04.01 | Supported/licensed Canadian COTS catalogue prescribing path and supplier/source mapping not established |
| PC04.06 | COTS drug–drug checking, supplied severity and override path; depends on actual licensed safety data and medication activity semantics |
| PC04.07 | COTS drug–allergy/adverse-reaction checking, severity and override; accepted allergy CRUD is not that safety integration |
| PC04.09 | User alert threshold and required precedence; dependent safety-alert foundation remains incomplete |
| PC04.14 | Ordinary-user licence name/last-update information using actual supplier metadata/integration |
| **PC07.01 — PARTIAL** | Remaining source-backed DIN DE09.005, grouped destination/pickup/coded-reason/protocol DE09.030–.035, wider electronic-authority DE09.027 semantics and complete applicable ownership/retrieval/audience mapping. Retain Family/source qualifications. **DE08.008 dose and the accepted Risk slice are closed**, not residual missing fields |
| PC07.04 | Family minimum implemented/accepted; deployed M7 Web/permission/state/audit and M8 normal-session tenant-routing assertions remain incomplete |
| PC07.10 | Separate encounter-managed procedure/medication-summary minimum not established; accepted diagnosis-to-Problem work retained |
| PC09.03 | Scheduling-to-billing HCN/service-date handoff without duplicate entry; receiving billing target/transfer contract unresolved |

Only PC07.07 is removed. No requirement-specific closure evidence was assessed for another counted ID. Preserve accepted native medication/allergy, DE09.004/DE11.008, owner appearance, dose and sidebar decisions without reopening them or expanding the scope.

## 5. Next priority — PC07.04 Family History evidence closure

**Recommend one next candidate: PC07.04, within the CPP package.** This is the smallest supported independently closable candidate after Risk: its minimum implementation and accepted appearance already exist, while M7/M8 are explicit evidence residuals. It does not presently require a commercial data supplier, new clinical-field policy or demonstrated code change. Reuse the existing Family evidence before authorizing runtime work.

[Step 98](133-step98-family-history-m7-m8-verification-closure.md), [M8/M9 supplement](132-step97-m8-m9-automation.md) and Step 161 distinguish accepted minima/isolated safeguards from deployed Web/session/state/audit proof. Preserve the reported three M7 API 403s; do not promote them to whole-scenario PASS. M8 normal-session tenant routing needs actual own-session evidence and established cross-tenant fixtures/identities; Risk's one-tenant session is not a substitute. No new missing Family field is inferred.

Comparison with alternatives from Step 167:

- Grouped DE09.030–.035 is a coherent implementation slice but needs source/namespace, pickup-time and coded-reference decisions, and cannot alone close PC07.01.
- PC07.10 has remaining workflow implementation and association decisions; it is larger than a bounded evidence lane.
- Prescribing's five IDs have high safety impact but depend on licensed catalogue/safety access and real supplier metadata; there is no supported short completion bound without those inputs.
- PC09.03 requires an identified receiving billing component and transfer/failure/duplicate contract.

Thus prioritize Family evidence reconciliation and only its genuinely missing automated deployment assertions. Do not start multiple implementation packages or request general repeat visual/security checklists. Longer-lead supplier/clinical-source decisions remain owner dependencies, not additional workstreams executed here.

## 6. Estimated effort, dependencies and proposed Step 171

Planning ranges, not a claim of observed delivery time or an overall certification completion date:

| Work | Active engineering estimate | Preconditions |
|---|---:|---|
| **Step 171: focused PC07.04 evidence reconciliation and executable residual plan** | **1–2 hours** | Relevant Family reports, exact retained source mapping and current inventory; documentation/source definitions only |
| Subsequent targeted automated deployed verification and final disposition | **2–6 hours**, only if needed | Suitable existing patients/resources, appropriate existing restricted/writer identities and tenant contexts, available services, precise separately approved session/request scope |
| Product implementation | **0 hours planned** | No defect established. If one is found, stop for separate implementation approval and estimate |
| **Conditional remaining Family effort** | **3–8 hours** | Excludes approval, fixture/identity or external-source waiting; no guarantee of closure before mapping |

No new Family clinical policy is currently indicated. Existing fixture/account/permission availability is **not freshly checked here**. If the residual needs a normal restricted session, foreign-tenant GETs or explicit denied mutation attempts, identify and obtain authorization for those exact actions before execution. Do not create users/patients, alter grants, generate foreign IDs or treat rejected mutation attempts as implicitly authorized. Use unchanged-state/audit observations to distinguish denied requests from clinical writes. Existing isolated audit-failure/stale tests do not need replay in the development database.

**Proposed Step 171 — PC07.04 Family History evidence reconciliation and bounded M7/M8 readiness plan:**

1. Recover/reuse the exact Family clause and accepted six-field/source mapping; retain existing source-release qualifications. No broad specification extraction.
2. Map each acceptance assertion to existing Step 97/98/supplement and later relevant evidence; separate code presence, executed fixture results, reported API denials, accepted appearance and missing deployed assertions.
3. Define only residual normal-session Web permission/state/audit and tenant-routing checks, actual routes/selectors/SELECTs, required existing identities and fixture prerequisites. Reuse shared environment evidence where applicable without calling another domain's checks Family PASS.
4. Produce one bounded executable automated plan with explicit login/tenant/request/write limits and stop conditions. If all existing evidence already suffices, justify qualified closure; otherwise request only the missing authorization/prerequisite. Do not promise another count reduction in advance.
5. **Documentation only by default:** no login, SQL access, tests/browser execution, fixture/permission changes or implementation under this proposed scope. Execute a subsequent verification step only when separately authorized. Stop for any demonstrated product defect before code or feature-branch work.

## 7. Files, checks and stop

Only **240-step170-pc07-07-evidence-closure-inventory.md** added. Current branch **main** retained; existing untracked reports 233–239 preserved unchanged. Reviewed relevant source/evidence reports and confirmed the inventory-table format and nine distinct remaining IDs/three nonempty memberships. Final report cross-reference, whitespace and Git status checks only. No clinical login/read/write, SQL modification/query, migration, restart, source-code edit, tests, browser, build, new branch, commit, merge, push, reset, clean, stash or GitHub operation.

**Final disposition: PC07.07 SATISFIED — VERIFIED (qualified); PC07.01 PARTIAL; 9 unresolved mandatory IDs / 3 packages.** Stop after documentation.
