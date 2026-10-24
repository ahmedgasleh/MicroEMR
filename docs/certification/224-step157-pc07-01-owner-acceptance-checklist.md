# Step 157 — PC07.01 owner acceptance checklist

2026-10-23. Prepared from [report 223, sections 3–4](223-step156-pc07-01-owner-acceptance-and-evidence-consolidation.md). **Subsequent owner response: Prescription PASS, Medication Plan PASS, Allergy PASS.** These reported decisions supersede the earlier PENDING wording/layout statuses. Separate mobile-sidebar observation remains QUALIFIED; viewport/menu details were not supplied.

Use an existing authorized owner session at `https://localhost:7002`, tenant **local-dev-fresh**, synthetic patient **`9f65d4ca-c347-406a-bb32-c709a8941f80`**. Review desktop and mobile width (390 px), recording the width used. If the session or a suitable existing record is unavailable, mark DEFERRED with the reason. No new login, record creation, field changes or saves are part of this session; inspect editors and close without saving.

## 1. Prescription — PASS / FAIL: ______

**Open:** [Chart → Medications](https://localhost:7002/Patients/Details?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&tab=medications) → **Prescriptions** → approved Draft row **Details** → **Edit draft**.

Direct [Draft Details](https://localhost:7002/PatientPrescriptions/Detail?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&prescriptionUid=ffd80110-e565-46de-b660-3d3f9dc7e463) / [Draft Edit](https://localhost:7002/PatientPrescriptions/Edit?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&prescriptionUid=ffd80110-e565-46de-b660-3d3f9dc7e463). Optional existing [finalized read-only comparator](https://localhost:7002/PatientPrescriptions/Detail?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&prescriptionUid=f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8).

**Check:** Product/Brand Name and Drug Description are distinct; Prescriber first name, Last name and OHIP Billing Number are separately labeled. Treatment type and Substitution prohibited are understandable. Paper copy required before dispensing offers **Yes / Not recorded** only; Not recorded makes no statement of electronic authority. Minimum dispense interval is **whole days**, independent of days' supply/repeats. Unknown facts may remain Not recorded; old snapshots need not acquire missing identity.

**PASS:** meanings, units, unknowns and labels/help are clear and readable at the reviewed width. **FAIL:** misleading conflation, ambiguous units/unknowns, or unreadable/clipped wording. Current Draft paper/interval are cleared; reuse Step 155's Yes/7 screenshots instead of re-entering values.

## 2. Medication Plan — PASS / FAIL: ______

**Open:** same Chart → **Medications** → existing plan row **Open** → expand **Compliance assessment history**. Row **Edit** opens the chart modal; standalone [Plan Edit](https://localhost:7002/PatientMedications/Edit?medicationUid=2521d27e-172f-47a1-9326-72eea8b0f571) is also available. Direct [Plan Details](https://localhost:7002/PatientMedications/Details?medicationUid=2521d27e-172f-47a1-9326-72eea8b0f571).

**Check:** Long-term treatment intent **Yes / No / Unknown** is separate from Classification **Ongoing / Acute / Unclassified / unknown**. Past medication (discontinued plan) means an explicitly Discontinued plan. Compliance states **Compliant / Not compliant / Unknown / Not assessed / Not recorded** are distinct. Optional Assessment time differs from server Recorded UTC and Assessor. Current Unknown and retained Compliant revision/explanations should be readable.

**PASS:** distinctions and history/actor/time wording are understandable and readable. **FAIL:** clinically ambiguous meaning or unusable wording/placement. Include the separate mobile observation below in this decision.

## 3. Allergy — PASS / FAIL: ______

**Open:** [Chart → Allergies](https://localhost:7002/Patients/Details?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&tab=allergies) → an existing row **Open**, then that row **Edit**. Use the actual row links; no allergy UID was designated in Step 155.

Exact route patterns: `/PatientAllergies/Details?allergyUid={existing-row-UID}&patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80` and `/PatientAllergies/Edit?allergyUid={same-UID}&patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80`.

**Check:** Drug applicability (as recorded) distinguishes **Drug / Non-drug / Unknown / Not recorded**. Drug DIN (confirmed drug) help explains deliberate DIN reconciliation, unknown remaining unknown and legacy type text not being automatically reclassified. Details includes Drug applicability, Drug DIN and System entry recorded.

**PASS:** these distinctions and help are clear/readable. **FAIL:** absent/unknown/non-drug are conflated or presentation is misleading/unreadable. Do not change DIN/applicability, resolve an allergy or alter No Known Allergies.

## Separate mobile visual observation

**Medication Plan, 390 px:** compare direct mobile entry with desktop-to-mobile resize while the sidebar is open, then its normal closed-menu state. Record whether the sidebar squeezes content or clips labels/history. Step 155's open-sidebar screenshot remains **QUALIFIED**, not PASS; desktop success does not close it. Readable/usable wording and layout is PASS; clipping or unusable placement is FAIL. An unavailable observation is DEFERRED. This is part of check 2, not a fourth clinical acceptance procedure; no technical fix is authorized here.

## Record the decisions and stop

```text
Date / viewport(s):
Prescription: PASS / FAIL / DEFERRED — comment:
Medication Plan: PASS / FAIL / DEFERRED — comment:
  Mobile sidebar/menu state and finding:
Allergy: PASS / FAIL / DEFERRED — comment:
```

Reuse [Step 155](222-step155-pc07-01-gate2-clinical-smoke-results.md): **7 browser groups PASS / 0 FAIL**, SQL/audit/actor/RowVersion and finalized preservation verified. Do not repeat its four clinical saves. Live restricted-user denial, optional omission-save and intentional stale-write scenarios remain separate/deferred; isolated passing security/concurrency evidence remains retained. They are not owner visual acceptance tasks.

## Owner response and future verification preference

Recorded from the owner's subsequent message on **2026-10-23**:

| Owner wording/layout decision | Reported result |
|---|---|
| Prescription | **PASS** |
| Medication Plan (owner wrote “Medication”) | **PASS** |
| Allergy | **PASS** |

No viewport, exact review time or explicit sidebar/menu finding was provided; none is invented. These are owner-facing technical wording/layout acceptances. They do not constitute clinician validation or external certification. Preserve Step 155 technical evidence without rerunning the successful tests or asking for the same three manual decisions again.

**Standing owner preference:** automate technical verification wherever feasible. Request further manual work only when necessary, explaining the specific decision or observation that automation cannot establish. The owner handles the technical work and plans review by a more qualified clinician after the gaps are completed; that clinician review is future work, not a completed sign-off. Resolve the outstanding mobile qualification through appropriately scoped automated evidence where possible rather than another routine manual checklist. This preference does not itself authorize new logins, clinical writes, code changes or deferred scenario execution.

**PC07.01 PARTIAL; 10 mandatory-gap IDs / three packages, unchanged.** Three wording/layout decisions are now reported PASS, while other requirement-level gaps and the separate mobile qualification remain. No top-level closure or inventory reduction inferred. This follow-up updates only this report; no branch creation, login, test execution, clinical/database/code change, migration, restart, Git mutation or remote operation. Existing work preserved.
