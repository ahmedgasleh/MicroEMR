# Step 85: PC04.16 Scope and Dependency Assessment

Date: 2026-10-11. Assessment only, continuing [Step 84](118-step84-pc10-02-evidence-closure-next-gap.md). **PC04.16 remains MISSING.** Recommend **C — dependency resolution before implementation**. Inventory remains **12 mandatory IDs / three packages**.

## 1. Git checkpoint

Branch **main**, HEAD `a1645ae`, `pc10-02 referral list evidence assessment`. Starting working tree contains only the untracked Step 84 report, preserved unchanged. Recent relevant commits are `4c06681` (referral preview fix) and `ba6136f` (Step 80 encounter work); they establish no new refill implementation. No branch is created or switched; evidence-only work follows the existing policy. Only this report is added, with no staging, commit or merge.

## 2. Requirement wording, source and interpretation

The existing [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md) records PC04.16 as mandatory, PDF p.21, with this exact repository summary:

> Differing refill quantity/days-supply, CDS-S referenced. R4 provides initial quantity/unit/repeat count but no independent refill quantity/duration. Missing authoring/data capability, dictionary before design.

This is a recorded interpretation, **not a verbatim quotation of the authoritative clause**. Step 45 identifies the user-designated *Primary Care Baseline — 1.7 Requirements.pdf*, Version 1.7 Final, as its source. Step 85 reuses that mapping without reopening the PDF or obtaining a new standard. Full clause wording, validation notes and applicable Medication CDS-S definitions are not reproduced in the inspected repository records; exact representation remains unresolved.

Required behavior under that mapping: an authorized prescriber can specify refill quantity or days-supply independently of the initial prescription amount/duration. A repeat count alone cannot express a differing refill amount. Persisting the independent instruction through saving and finalization is necessary for that behavior; field names, units, cardinality, defaults, quantity-versus-days-supply alternatives and validation rules must follow the applicable dictionary rather than be invented.

Related output obligation: the mapped **PC04.05** prescription print includes refill duration-or-quantity. It is a separately unresolved requirement, not evidence that PC04.16 mandates every PC04.05 demographic or multipage change. A future refill slice should preserve its instructions in the immutable prescription snapshot and expose them correctly in the existing output path, with exact display rules confirmed first.

No supplied PC04.16 mapping mandates medication reconciliation, active-medication calculations, dispensing/refill-use tracking, a renewal service, DHDR, pharmacy transmission or safety-engine integration. Do not expand this authoring gap into those workflows. The earlier [PC04 family assessment](primary-care/PC04-medication-management.md) predates both the prescription foundation and Step 45's numbered mapping; its statements that prescriptions do not exist are historical, not current coverage findings.

## 3. Existing implementation coverage

Bounded source inspection: Application `PatientPrescriptions/PatientPrescriptionModels.cs`; Infrastructure `PatientPrescriptions/PatientPrescriptionRepository.cs`; API `PatientPrescriptionsController.cs`; relevant sections of tenant migration `0050-patient-prescription-foundation.sql`; `PatientPrescriptionFoundationTests.cs`. Migration content was read only. UI behavior is reused from [Step 27A](35-step27a-local-structured-prescription-foundation.md), not newly browser-verified.

| Capability | Coverage and limitation |
| --- | --- |
| Prescription authoring | DTOs and repository parameters carry `Quantity`, `QuantityUnit`, `AuthorizedRepeats`, directions, dose, frequency and dates. No independent refill quantity/duration or days-supply property exists in these contracts. Free-text directions do not establish the mapped structured refill representation. |
| Persistence | Foundation schema/procedures retain quantity/unit/repeat count; create/update and response mappings contain no independent refill amount/duration. This confirms the mapped omission in the existing prescription path. |
| Finalization/output | SQL freezes quantity/repeats and instructions in artifact JSON; Application PDF renders quantity/repeats. No independent refill quantity/days-supply is captured or rendered. |
| History/correction | Draft-only editing; Finalized, Cancelled and Superseded clinical content is retained. Correction copies existing quantity/unit/repeats into a linked Draft. Correction is not proof of differing refill instructions within one prescription. |
| Clinical boundaries | Authenticated patient-view reads; dedicated prescribing permission for mutations; server actor/provider resolution; tenant connection factory; compound patient/prescription lookup; RowVersion checks and transactional lifecycle audit. Preserve all these controls in any later slice. |
| Medication list | Step 27A keeps current medications separate and finalization does not mutate that list. Discontinuation/reconciliation is not established as a PC04.16 prerequisite. |

The defect is a known missing capability, not a newly demonstrated runtime failure. Manual verification of repeat count cannot close it.

## 4. Dependencies and readiness

| Dependency | Classification | Practical consequence |
| --- | --- | --- |
| Local prescription lifecycle, dedicated permission and immutable artifact foundation | Already implemented and verified — historical automated evidence | Reuse existing aggregate and controls; historical tests do not verify a new refill field. |
| Deployed end-to-end prescription/security/audit workflow | Implemented but verification outstanding | Step 27A leaves its complete interactive workflow and separate disposable fresh-database execution outstanding. Do not infer manual acceptance from tests or referral acceptance. |
| Independent refill instruction end to end | Missing implementation | Requires authoring, persistence, projection, correction copying and finalized snapshot/output support after definitions are resolved. |
| Applicable Medication CDS-S version and refill data definitions | External dependency | Not established by the inspected evidence. Obtain the authoritative referenced definitions; this blocks defensible data design, not all local prescribing operation. |
| Exact refill semantics, validation and tracked-release applicability | Specification interpretation unresolved | Resolve quantity/days-supply alternatives, units/cardinality/defaults and acceptance examples before designing fields. This is a certification/design evidence blocker. |
| PC04.05 refill output | Missing implementation — within separately PARTIAL PC04.05 | Coordinate only the refill output affected by this slice; its other omissions remain outside scope. |
| Licensed catalogue/safety supplier for PC04.01/.06/.07/.09/.14 | External dependency for those requirements | No evidence makes supplier integration a technical prerequisite to PC04.16. Preserve those gaps without bundling them into this step. |

The missing authoring/data path is a genuine functionality gap. Dictionary/release interpretation is the immediate readiness blocker. Existing identity/tenant/audit code provides a foundation, not a declaration of complete deployed or certification verification.

## 5. Evidence reused and missing

Step 27A reports **22/22 combined focused Step 27A/27P tests**, **725/725 API tests**, **30/30 Auth tests** and a successful Release build. These are previously executed results, not a current gate or PC04.16-specific acceptance. Relevant existing test contracts cover quantity/repeat validation, permissions, provider revalidation, patient-scoped lookup, lifecycle/audit/artifact behavior and isolation from medication-list mutations. Source inspection does not execute those tests or establish runtime SQL results.

Step 27A also records a healthy controlled `local-dev-fresh` successor upgrade through migration 0050, while distinguishing source parsing from separate fresh SQL execution. Its interactive checklist is planned evidence, not recorded PASS. No PC04.16-specific manual acceptance, independent-refill automated evidence or resolved dictionary mapping was found in the relevant records. No tests/builds/browser checks are performed here; unrelated prior test counts are not combined.

## 6. Recommendation and proposed Step 86

Choose **C — dependency resolution before implementation**. A missing capability is already established, so evidence-only manual verification cannot close PC04.16. A narrow implementation is premature while refill representation is unresolved. The available mapping supports the general clinical intent, so a complete restart of specification analysis is unnecessary.

Proposed **Step 86: Medication CDS-S refill definition and acceptance mapping**:

1. Locate the applicable authoritative Medication CDS-S version and the PC04.16 clause/validation notes referenced by Step 45; record the tracked-release relationship or unresolved authority explicitly.
2. Map only initial versus refill quantity/duration, units, cardinality, optional/default rules and validation to the existing prescription contracts. Resolve whether both alternatives must be supported and how differing values are represented; do not presume a per-refill schedule.
3. Produce the smallest implementation proposal and acceptance examples for save/reopen, correction, finalization and immutable output, retaining permission, tenant/patient binding, trusted actor, RowVersion and transactional audit controls. Preserve old artifacts and migration history.

Step 86 should remain assessment-only unless implementation is separately authorized. If definitions are unavailable, report the precise material needed instead of inventing schema. Evidence-only work can remain on main; a later authorized implementation branch could be `feature/step-86-pc04-16-independent-refill-instructions`. No next-step work or branch creation occurs here.

## 7. Status, inventory and qualifications

**PC04.16: MISSING; dependency resolution required before implementation.** No requirement is marked satisfied. Inventory stays **12 IDs / three original packages**: seven medication/prescribing, four CPP and scheduling PC09.03. No unrelated status changes.

PC10.01 and PC10.02 remain SATISFIED — VERIFIED with their existing qualifications. Preserve deferred PC10.02 audit, the historical Step 46 **33 passed / one failed / zero skipped** gate and its pre-existing manifest assertion, and all earlier PC08/PC09/PC10.01 qualifications. Preserve the Baseline 5.5 folder / Version 1.7 / tracked-release uncertainty and external OntarioMD acceptance boundary.

Documentation checks cover local report links, whitespace and final Git scope. Step 84 remains unchanged. No application/database edits, migrations, browser operations, automated test/build reruns, commits or merges. Stop after this report.
