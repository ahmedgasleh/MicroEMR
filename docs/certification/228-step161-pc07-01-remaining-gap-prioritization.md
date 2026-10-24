# Step 161 — PC07.01 remaining mandatory-gap prioritization

2026-10-23. **PC07.01 PARTIAL. Inventory unchanged: 10 mandatory-gap IDs / three packages.** Documentation/read-only reconciliation; no new implementation or runtime verification. Current branch retained: `feature/step-160-mobile-sidebar-helper-correction`. Existing work preserved; no branch, commit or remote operation.

## 1. Evidence retained and work already finished

- [Step 155, report 222](222-step155-pc07-01-gate2-clinical-smoke-results.md): **7 integrated browser groups PASS / 0 FAIL**, including compliance revision, paper Yes / Not recorded, seven-day interval and clear, persistence, audit/actor attribution, RowVersion progression and finalized-prescription immutability within the recorded scope.
- [Step 157, report 224](224-step157-pc07-01-owner-acceptance-checklist.md): owner **Prescription PASS, Medication Plan PASS, Allergy PASS**. These supersede the pending owner checks in report 223.
- [Step 160, report 227](227-step160-pc07-01-mobile-sidebar-acceptance.md): **12 live checks PASS / 0 FAIL**, at 320, 375 and 430 px; sidebar **CLOSED — VERIFIED**. This supersedes the earlier mobile qualifications. No product fix required by that evidence.
- Native implemented fields, qualified DE09.004 closure, accepted allergy/NKA and DE11.008 system-entry date semantics remain accepted. Do not recreate an external-original allergy recorded-date requirement or reopen approved native medication policies.

These are actual subordinate closures. The unchanged parent inventory does not mean the same functionality remains untested. No successful suite or owner check was repeated for this report. Historical overlapping coverage totals are not recomputed.

## 2. Reconciliation of all ten counted IDs

The ten IDs span **prescribing (5), CPP (4), scheduling (1)**; they are not ten missing PC07.01 fields. [Step 136 section 7](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md) and [Step 156 sections 5–7](223-step156-pc07-01-owner-acceptance-and-evidence-consolidation.md) control the retained inventory. For the untouched prescribing/scheduling IDs, the bounded requirement descriptions below reuse [Step 45's authoritative requirement mapping](77-step45-baseline-1.7-reconciliation.md); their classifications are carried forward, not a new source-code audit.

| Counted ID | Classification of remaining work | Evidence and exact residual |
|---|---|---|
| PC04.01 | Missing functionality; policy/design and external evidence required | Supported/licensed Canadian COTS drug-catalogue prescribing path not established. Local text/snapshots and native drug-description PASS do not prove catalogue integration. Supplier, licence, supported source and mapping needed. |
| PC04.06 | Missing functionality; external dependency | COTS drug–drug checking, supplied severity and override path; depends on licensed safety data and active-prescription semantics. Compliance assessments are a different capability. |
| PC04.07 | Missing functionality; external dependency | COTS drug–allergy/adverse-reaction checking, severity and override. Allergy CRUD/owner PASS does not prove the safety matching path. Preserve authoritative patient allergy data. |
| PC04.09 | Missing functionality | User alert-threshold control and required precedence; depends on the safety alert foundation above. |
| PC04.14 | Missing functionality; external evidence required | Ordinary-user access to COTS licence name/last-update information. Needs actual supplier metadata and integration, not invented dates or an admin-only screen. |
| PC07.01 | Partially implemented; policy/design and external evidence required | Closed native medication/allergy/mobile work retained. Remaining dose, medication-source/reference and retrieval/applicability deltas are enumerated below. |
| PC07.04 | Implemented but insufficiently verified | Family minimum implemented/accepted; residual M7 deployed Web/permission/state/audit and M8 normal-session tenant-routing evidence remain. Medication-session success is not Family closure. |
| PC07.07 | Implemented but insufficiently verified | Accepted Risk four-field minimum retained. Broader requirement-level qualifications/closure mapping remain unclosed in the controlling inventory; no new missing Risk field or defect demonstrated. First reconcile existing Risk evidence against the parent before proposing code or a new test. |
| PC07.10 | Partially implemented | Accepted encounter-to-Problem diagnosis work retained; remaining encounter-managed procedure/medication-summary minimum not established by separate chart editors or finalized orders. Separate workflow slice, not a compliance UI retest. |
| PC09.03 | Missing functionality; policy/design dependency | Scheduling-to-billing HCN/service-date handoff without duplicate entry; supported billing target and transfer contract needed. No unrelated billing subsystem authorized. |

There is no requirement-level evidence here to remove any ID. Source/release and deployment qualifications are not assumed product defects, and local tests cannot establish external certification acceptance.

## 3. PC07.01 deltas and coherent work order

Priority reflects mandatory scope, dependencies and relative effort, not a new estimate from code inspection. The three inventory packages remain unchanged; the rows below are execution slices within them.

| Priority / inventory package | Coherent slice | Dependencies / relative effort |
|---|---|---|
| **1 — CPP** | **DE08.008 administered immunization dose amount + unit** | Approved numeric/unit/presence/outcome policy; medium bounded implementation, no commercial catalogue prerequisite. Recommended next implementation. DoseNumber is not this dose. |
| **1 — CPP, evidence lane** | Family M7/M8 residual; Risk parent evidence mapping | Reuse completed evidence first; only missing deployed Family assertions need separately authorized sessions. Low–medium verification effort; no assumed product change. Can progress separately from dose work. |
| **2 — CPP** | Grouped medication destination/reference metadata: DE09.030–.035 | Facility source/entered-fact policy, association and identifier namespace; pickup precision/zone/unknown/provenance; actual coded-reason/protocol sources. Medium–high effort after decisions. Indication text and prescription dates are insufficient substitutes. |
| **2 — Prescribing, shared CPP dependencies** | Licensed catalogue → safety checks/override → user thresholds/licence information | High certification importance but high effort and supplier dependency. Begin supplier/source decisions in parallel now. PC04.01 precedes dependent .06/.07/.09/.14; DE09.005 source-backed DIN selection/extraction/provenance shares this dependency. Do not buy or select a supplier in this task. |
| **2 — CPP** | PC07.10 encounter-managed procedures/medication summary | Existing encounter/category authority and approved interaction/association contract; medium–high effort. Preserve accepted diagnosis workflow; do not substitute name-based joins. |
| **3 — CPP, policy-dependent** | Wider DE09.027 electronic-authority/“No” support and complete PC07.01(f)/.06 field-to-owner/audience retrieval mapping | Governing authority/applicability and explicit ownership/output decisions. Restricted paper Yes / Not recorded already PASS. Implement only proven omissions; no blanket every-field printing requirement. |
| **3 — Scheduling** | PC09.03 billing handoff | Approved receiving billing component, minimum transfer/failure/duplicate contract; effort cannot be bounded until target is known. Stable scheduling must be preserved. |

External accepted-release/source applicability and missing deployment provenance remain separate evidence gates across relevant slices. This order favors an independent deliverable while external contracts are resolved; it does not downgrade prescribing safety importance.

## 4. Recommended Step 162 — approve and implement the bounded dose contract

**Recommend DE08.008 only, after explicit implementation authorization and the unresolved policy decisions.** Reuse [Step 110's resolved dose-source interpretation](145-step110-pc07-01-immunization-dose-rules-resolution.md); no workbook re-extraction or repeat assessment.

Required decisions: numeric precision/range/positivity; permitted unit representation; amount/unit pairing for new entries versus legacy partial records; applicable administered/refused/unknown behavior. Mandatory product support does not require a known dose on every historical record. The earlier `DECIMAL(18,6)` proposal and prescription-dose limits are not approved immunization policy. No conversion, inference, zero-as-unknown or historical backfill should be introduced.

Exact proposed implementation boundary: independent presence-aware administered amount/unit DTO and Application validation; existing Infrastructure/stored-procedure persistence and audit patterns; minimal additive database change if required and authorized; existing immunization editor, Details and required CPP retrieval. Preserve DoseNumber, outcomes, retained history, legacy unknowns, permissions and terminal-state rules. No medication metadata, catalogue integration or redesign in this slice. Create a dedicated local branch only after code work is authorized.

Acceptance criteria for that future package:

1. Approved known amount/unit survives actual API/SQL and browser save/reload and required retrieval with exact value/unit; existing unknown records remain readable and unchanged.
2. Omission preserves existing values; explicit clear follows approved policy and is audited. Invalid precision/range/unit/pairing/outcome fails without partial changes. No manufactured dose for refusal or historical unknowns.
3. Focused automated tenant/patient/permission, trusted actor, RowVersion/stale-write and transactional audit/rollback checks pass using appropriate isolated fixtures. Existing immutable/outcome behavior remains intact where affected.
4. Targeted browser controls/labels/validation and desktop/mobile retrieval work; inspect screenshots only for changed layouts. Reuse unrelated owner and mobile PASS evidence.

Automate validation, persistence, browser integration, access denial, audit, concurrency and display checks. Any future development login or clinical mutation requires its own explicit authorization; this report grants none. No broad suite or repeated manual functionality matrix is proposed.

## 5. Human decisions, retained deferrals and stop

Owner/appropriate clinical-policy input is needed only for unresolved dose semantics, facility/reference/authority ownership, commercial source/licence choices and billing target. Owner technical approval must not be represented as clinical authority; if a clinical decision cannot yet be approved, retain that exact prerequisite rather than invent it. Supplier/release acceptance needs actual external evidence. Future clinician review remains separate and planned after the gap work; it has not occurred. Do not ask the owner to repeat the three accepted visual checks. Prefer automation for subsequent technical verification; manual input only when necessary.

Keep **live restricted-user denial, optional deployed omission-save and intentional live stale-write scenarios DEFERRED** in their existing deployment scope. Step 143 API/SQL and Step 144 isolated Web/API/SQL security/concurrency evidence remains PASS; Step 155 normal RowVersion progression does not prove an intentional deployed conflict. These are separate qualifications, not new inventory IDs or proof of missing functionality. Family M7/M8 remains its own evidence lane.

**Disposition: PC07.01 PARTIAL; prescribing 5 + CPP 4 + scheduling 1 = 10 IDs / three packages, unchanged.** Only this report added. No code, tests, authenticated access, clinical writes, database/service operations or Git mutations performed. Stop after documentation.
