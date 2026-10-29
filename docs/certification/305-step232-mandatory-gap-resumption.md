# Step 232 — Mandatory-gap implementation resumption

2026-10-28 (America/Toronto). **DE09.032: IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS.** The development checkpoint is closed by the owner's Step 232 decision. **Next selected mandatory implementation: PC04.01, source-backed Canadian drug selection within existing native prescribing.** Its minimum product change is proposed below; implementation stops at the unresolved source/contract design decision under Step 232 §5. No application changes or new acceptance loop were introduced.

## 1. DE09.032 development checkpoint

Record **IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS**, based on retained evidence:

- Step 230: directory Create without client RowVersion, search, detail retrieval and lookup selection **PASS**.
- Step 231: selected identifier and explicitly confirmed location details saved to an existing unsigned Draft, reopened and displayed correctly **PASS**.
- Step 229: RowVersion fix, **18 focused cases PASS**, isolated build PASS; mandatory update-token validation and stale conflict handling retained.
- Step 218: existing automated overwrite/warning/explicit-population evidence retained, including `DispensingDirectoryWebTests.LookupPreviewCancelConfirmationManualAndStaleStates`; existing affected backend/frontend evidence retained without rerun.

Qualifications remain factual: live conflicting-value warning was not exercised because the selected Draft's original name/address were blank; direct clinical audit-row inspection was deferred following the read-only SQL timeout; no database-wide preservation snapshot was obtained in Step 231. Automated transactional audit/history/security evidence remains retained. These are development-checkpoint qualifications, **not requirements for another DE09.032 acceptance step**.

The owner decision supersedes Step 231's recommendation to continue that acceptance work. **No further DE09.032 acceptance is required or scheduled at this development checkpoint.** No audit retry, conflicting-value fixture, dispensing-location fixture or clinical mutation was performed here. This status is **not formal OntarioMD certification approval**, does not settle source/release applicability, and does not close parent PC07.01.

## 2. Git/worktree preservation

Actual entry branch: **main**; tracked files were clean. The pre-existing untracked report `304-step231-de09-032-prescription-integration-acceptance.md` was preserved byte-for-byte (SHA-256 checked before/after).

Safely created **feature/step-232-mandatory-gap-resumption** from the same current HEAD, carrying that existing work intact. Only this report was added. No implementation/configuration/SQL/migration/test file changed. No commit, merge, push, pull, fetch, reset, clean or stash occurred. No remote operation occurred.

## 3. Retained mandatory inventory

| Package | Remaining mandatory IDs | Count |
|---|---|---:|
| Prescribing | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | 5 |
| CPP | PC07.01 | 1 |
| Scheduling | PC09.03 | 1 |
| Total | Seven distinct IDs / three packages | **7** |

**PC07.01 remains PARTIAL.** DE09.032 is a subordinate element; its development checkpoint does not decrement this seven-ID inventory. Previously satisfied requirements and DE09.034/DE09.035 qualified closures are not reopened.

Reused the retained comparison/mappings in [Step 185](261-step185-remaining-mandatory-gap-prioritization.md), PC04.01 source-contract findings in [Step 186](262-step186-pc04-01-canadian-cots-integration-readiness.md), metadata contract in [Step 188](264-step188-pc04-14-metadata-provenance-display-contract.md), source qualifications in [Step 189](265-step189-ccdd-feasibility-ontariomd-eligibility.md), dependency distinctions in [Step 190](266-step190-independent-gap-implementation-assessment.md), and the latest [Step 230](303-step230-de09-032-rowversion-live-activation.md)/[Step 231](304-step231-de09-032-prescription-integration-acceptance.md) evidence. Earlier pickup/reference work is completed evidence, not a new candidate. No repository-wide audit, source research or full certification reassessment was performed.

## 4. Selected next requirement and priority

**Select PC04.01 — one real catalogue search/selection-to-Draft path.** This completes a missing part of the existing prescribing workflow rather than creating a separate optional module. The retained priority places this source foundation first; it also supplies the Canadian identity/DIN foundation used by PC07.01 and authentic metadata needed by PC04.14. Catalogue integration does not itself satisfy interaction/allergy requirements.

| Candidate | Smallest retained gap / dependency | Decision for this step |
|---|---|---|
| **PC04.01** | Native recording exists; actual source-backed product selection and verified source provenance are missing. Requires a real source/delivery contract. | **Selected foundation**, gated on that concrete design decision. High clinical-evaluation value through reliable product/strength/form selection. |
| PC04.14 | Smaller two-field ordinary-user display, but authentic source name/update metadata and permitted display terms are still required. | Do not build a placeholder display ahead of the source. |
| PC07.01 | Prefer partial completion where possible; many local field slices are now implemented. Remaining source-backed DIN support shares PC04.01's dependency; broader authority/applicability residuals need policy decisions. | Do not reopen finished local slices or invent another metadata field. |
| PC04.06/.07/.09 | Real interaction/allergy knowledge, matching/severity and effective user alert controls depend on established source/clinical contracts. | A mock rule set or settings-only screen would not implement the mandatory behavior. |
| PC09.03 | Required HCN/service-date handoff needs a named actual billing receiver and transfer contract. | Do not invent a receiver or expand into a billing subsystem. |

This uses existing dependency findings. It does not assert that a commercial supplier has been selected, that CCDD is already accepted as COTS, or that no future independent implementation is possible.

## 5. Exact mandatory specification and existing coverage

Retained source: **Primary Care Baseline Requirements, Document Version 1.7 Final, March 20, 2026, §2.4 Medication Management, pp.16–17, PC04.01, mandatory M**; exact wording is retained in Steps 185/186:

> The EMR Offering MUST provide the ability to record patient prescriptions using a publicly available COTS (commercial off-the-shelf) drug database with Canadian drug codes.

Its guideline minimum uses the source for **Drug Name, Drug Strength / Drug Strength UM, and Drug Form**, with the retained commercial licensing, Health Canada-authorized information, accountability, maintenance/update and support assurances. Accepted-release/source applicability qualifications remain. Public availability or commercial reuse permission alone does not establish COTS certification eligibility.

**Already implemented:** native prescription Draft Create/Edit, structured product/identifier/strength/unit/form fields, clinician-entered text, validation, tenant/actor permissions, stored-procedure persistence, original-prescriber ownership, RowVersion, audit, governed finalization/correction and immutable history/artifacts. Existing identifier slots do not establish that a Canadian source selected or verified their values. Reuse this functionality; do not rebuild it.

**Minimum missing behavior:** source-backed selection populates the required drug facts and Canadian identity in the existing Draft workflow, the server validates that selection against the actual designated source, and the saved record retains truthful source/release provenance without changing frozen historical records.

**Optional enhancements to defer:** multiple suppliers, advanced ranking/facets, generic substitution or dose inference, a catalogue-maintenance administration suite, a new billing subsystem and optional organization-level alert settings. PC04.14 display and PC04.06/.07/.09 safety work remain separately scoped mandatory work, not optional additions to this one slice.

## 6. Proposed minimum implementation for owner approval

1. Use **one owner-designated real source and one actual delivery contract** (API or licensed dataset). Map its selectable product identity, Canadian code namespace/value, drug name, strength/unit and form without truncation, guessed units or inferred equivalence. Establish representative single/multi-ingredient and missing/unsupported-value cases from that source.
2. Add only the search/detail adapter and Application DTO/service boundaries needed by the existing prescription form. Preserve normal tenant permissions and safe unavailable/no-results distinctions. Keep existing manual entry available with truthful unverified provenance; do not label typed values as source-validated facts.
3. Add the minimal Bootstrap/TypeScript search/select interaction to existing unsigned Draft authoring. Resolve the chosen source identity server-side at Save; reject stale/mismatched supplied selections rather than trusting browser labels. Define how deliberate edits to sourced strength/form affect the identity/provenance claim before coding that behavior.
4. Retain the selected facts and necessary source/release provenance through existing stored-procedure/audit/concurrency/history conventions. Reuse existing fields where sufficient; determine additive persistence needs from the real contract rather than inventing a schema now. No persistent migration is authorized by this proposal.
5. Automate representative mapping, tampered/unsupported/stale/unavailable selection, fresh save/read, access/tenant, audit rollback and historical preservation checks relevant to this change. Run focused tests and one affected build/typecheck. Defer manual usability/source-suitability review as an explicit qualification, not a mandatory acceptance loop for each development step.

This is a concrete proposed scope, **not shipped code or a claimed integration**.

## 7. Blocking owner decision and implementation disposition

**No approved supplier/product, usable schema/sample/API contract or settled selectable-product/Canadian-code mapping is established by the retained source evidence or supplied in Step 232.** These determine the adapter, identifiers, strength/form mapping, permitted data use and source-failure behavior. Choosing them is a significant design/source decision, not routine implementation detail.

Step 232 §5 explicitly says to stop after documenting the minimum scope and request approval when such a decision is required. Accordingly, **no application implementation was attempted**. No mock clinical knowledge, invented supplier metadata, empty production provider abstraction or guessed severity vocabulary was introduced merely to produce a code diff.

**Owner input needed:** approve/revise the minimum scope above and identify the intended actual source/product plus its existing API schema or dataset dictionary/sample and permitted development use. Do not send credentials. If choosing CCDD for a development integration, explicitly retain the unresolved COTS-eligibility qualification from Step 189 and supply the actual dictionary/release/sample contract; development use is not an OntarioMD acceptance claim.

The existing deferral of external correspondence remains respected. No supplier was contacted/selected/purchased and no OntarioMD communication was sent. External formal approval is not being invented as a prerequisite for all development; the immediate implementation gate is an owner-approved, technically usable real-source contract and mapping.

## 8. Verification, remaining blockers and Step 233

**Focused tests/build/typecheck: not run — documentation and design gate only; no executable functionality changed.** Documentary checks verified the exact development-checkpoint label, retained qualifications, seven unique IDs/three packages, evidence links, worktree preservation and whitespace. Existing test/build results are reused, not represented as rerun or new PASS evidence.

No DE09.032 test, fixture, SQL timeout retry, backup/recovery assessment, database access, persistent migration, permission change, service restart, clinical mutation, broad scan or full suite occurred.

**Recommended Step 233 — implement the approved PC04.01 real-source drug-selection slice.** Obtain the source/contract decision above once, then proceed directly with the bounded implementation and focused automated verification when its field mapping is unambiguous. Do not schedule another broad readiness/certification audit or DE09.032 acceptance step. Keep PC07.01 and the seven-ID mandatory inventory unchanged until the respective requirement evidence supports a later status decision.
