# Step 86: PC04.16 Medication CDS-S Definition Mapping

Date: 2026-10-11. Specification and acceptance design only, continuing [Step 85](119-step85-pc04-16-scope-dependency-assessment.md). **PC04.16 remains MISSING; 12 mandatory IDs / three packages remain.** Local definitions resolve the core semantic dependency. **Step 87 can implement a bounded slice against these sources, subject to explicit engineering policies and the preserved release qualification.** No implementation begins here.

## 1. Git checkpoint and sources

Branch **main**, HEAD `a1645ae`, `pc10-02 referral list evidence assessment`. Starting tree contains only untracked reports 118 and 119; both are preserved unchanged. This report is the only addition. No branch switch, staging, commit or merge.

Relevant local sources, read only:

- [Primary Care Baseline - 1.7 Requirements.pdf](</D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), Version 1.7 Final: p.21 PC04.16, p.18 related PC04.05. PC04.16's table row was also visually inspected.
- [CDS-S Data Dictionary](</D:/Development/Maui .Net 10/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>): `Data Dictionary!A1:A3` identifies publication 2026-06-05, Version 2.1 Final. Medication entries `A137:K141` and the Legend supply the relevant semantics and store/display classification.
- [CDS-S Requirements](</D:/Development/Maui .Net 10/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Requirements (v1.1).pdf>), Version 1.1 Final, November 5, 2021: pp.5-6 CDS01.01/.02 require storage/display of distinct logical elements without merging them into one element.

The CDS-S folder is labelled **5.2 DFU / 2026-07-10**, while the dictionary identifies **2.1 Final / 2026-06-05**; its Version History sheet retains an older 2.0 DFC/2020 header. The Business View cover identifies 2.3 Final/July 2, 2026; it is not used to establish refill semantics. These labels are recorded, not harmonized into proof of applicability to PCON-2024-02 Stage 3. The prior Baseline 5.5-folder/Version 1.7 distinction remains. No external research or standards download was needed.

## 2. Established definitions

PC04.16, mandatory, states:

> The EMR Offering MUST provide the ability to capture a refill quantity and refill duration (days’ supply) which differs from the first dispensing.

Its guideline refers to CDS-S medication data elements. This establishes capability for **both** independent refill quantity and refill duration, correcting the tentative quantity-or-duration interpretation in Step 85. It does not require values to differ on every prescription.

| Logical element | Dictionary location | Sourced meaning |
| --- | --- | --- |
| DE09.011 Duration | Row 137 | Medication duration for the initial dispense. PC04.16 explicitly identifies duration as days' supply. |
| DE09.012 Refill Duration | Row 138 | Duration for subsequent refills, supporting a value different from initial dispensing. |
| DE09.013 Quantity | Row 139 | Medication amount for initial dispensing. |
| DE09.014 Refill Quantity | Row 140 | Medication amount for subsequent refills, supporting a value different from initial dispensing. |
| DE09.015 Number of Refills/Repeats | Row 141 | Subsequent fills following initial dispensing; the initial fill is not a refill. |

All five rows are **M** and **S/D**: mandatory element support, with storage and display. They list no code set or numeric validation constraint. This does not establish that every prescription must populate every field. Quantity is neither strength nor dose; duration is neither frequency nor an elapsed time computed from prescription dates. Directions/SIG do not replace separately captured initial/refill elements. PC04.05 separately requires prescription output capable of including duration/quantity, refills and refill duration/quantity.

The supplied definitions describe prescription-level refill values. They do **not** specify a separately varying quantity/duration for each numbered refill, a refill-consumption ledger or an automatic calculation from SIG. A common refill quantity/duration pair is a defensible minimal engineering interpretation, not an assertion that all other refill patterns are prohibited.

## 3. Existing-versus-required contracts

Reuse Step 85's bounded persistence/controller inspection; recheck relevant Application and repository contracts only. No application or SQL file changes.

| Concept | Existing MicroEMR | Remaining gap |
| --- | --- | --- |
| Initial quantity/unit | `Quantity` decimal and `QuantityUnit`; stored, returned and printed | Reuse with an explicit initial-dispense label. |
| Initial days' supply | No dedicated DTO/storage/snapshot property | Capture, persist and display separately from directions/start date. |
| Refill quantity/unit | No independent property | Add independent refill amount with an unambiguous unit. |
| Refill days' supply | No dedicated property | Add independent refill duration in days. |
| Repeat count | `AuthorizedRepeats`, nonnegative integer | Reuse; clarify that it counts subsequent fills. |
| SIG/directions | Required free-text `Directions` | Preserve; do not parse it into an authoritative duration or combine logical elements. |
| Status/history | Draft, Finalized, Cancelled, Superseded; linked correction and RowVersion | Reuse; changing refill instructions must not edit a finalized prescription. |
| Output | Frozen JSON rendered to PDF; quantity/repeats shown | Include new explicitly recorded initial/refill values, with backward-compatible reads of old snapshots. |

The clinical omission is confirmed; implementation remains missing. Historical foundation evidence in Step 85 supports reuse, not PC04.16 acceptance. No new automated or manual results are claimed.

## 4. Proposed engineering policies and acceptance examples

**The following are proposed design/acceptance scenarios, not executed tests or additional OntarioMD validation rules.** Use synthetic prescriptions with a fixed instruction of one tablet daily where arithmetic is illustrated; these are data examples, not prescribing recommendations.

Proposed policies: separately named nullable fields for initial days' supply, refill quantity/unit and refill days' supply; positive values when supplied, complete quantity/unit pairs and the existing nonnegative refill count. Reuse existing quantity precision/range; choose and document positive whole days as an engineering representation, not a sourced constraint. New zero-refill orders should clear or reject explicit refill instructions with a clear explanation; do not retroactively reject or rewrite legacy records. Require no automatic derivation from SIG. Do not silently default historical missing values to the initial amount.

| # | Proposed input | Expected observation |
| --- | --- | --- |
| 1. Identical values | Initial 30 tablets/30 days; refill 30 tablets/30 days; 2 repeats | Equal values are accepted and survive save/reopen; no artificial requirement to differ. |
| 2. Different quantities | Initial 10 tablets/10 days; refill 30 tablets/30 days; 2 repeats | Both amounts remain distinct through Draft, final snapshot and output. |
| 3. Different durations | Initial 30 tablets/30 days; refill 90 tablets/90 days; 1 repeat | Independent initial/refill days' supply is retained and displayed. |
| 4. Zero refills | Initial 30 tablets/30 days; 0 repeats; refill fields blank | Valid single initial dispense. New explicit refill values with zero repeats invoke the stated engineering policy. |
| 5. Positive count | Initial/refill 30 tablets/30 days; 3 repeats | Label shows three subsequent fills; it does not count the initial dispense as a repeat. No dispense-tracking feature is implied. |
| 6. Invalid/inconsistent inputs | Negative amount/day value, negative count, quantity without unit; separately, 30 tablets/20 days with one-tablet-daily text | Reject proposed structural/range violations without mutation/audit success. The arithmetic mismatch needs prescriber review; do not invent a general mandatory rejection rule, infer dose from free text or reject legitimate PRN/taper schedules. |
| 7. Legacy prescription | Existing finalized artifact lacks new properties | Existing content remains readable and unchanged; omit absent values or label them as not recorded. Correction can explicitly add values without modifying the source artifact. |
| 8. Display/print separation | Use scenario 2 or 3 | Clearly show initial quantity/days' supply, refill quantity/days' supply and count as separate concepts; show only recorded values. Do not merge them into Directions. |

Later verification should also cover quantity-only and duration-only recorded instructions, blank versus zero, stale RowVersion rejection, correction copying, permission/tenant/patient denial, audit and artifact preservation. Field-support obligations do not establish unconditional per-record requiredness; final missing-value/default behavior must be documented explicitly in Step 87.

## 5. Dependency classification and readiness

| Dependency | Decision |
| --- | --- |
| Authoritative definition available | PC04.16 wording and DE09.011-.015 definitions are locally available; the Step 85 semantic-source blocker is resolved for this mapped slice. |
| Engineering interpretation required | Physical field names/types, numeric bounds, optionality/defaults, zero-refill policy, shared refill pair and non-parsed consistency handling. Proposed above; do not label them mandatory certification rules. |
| External clarification required | Applicability of these differently labelled source versions to the tracked release remains unestablished. A per-refill varying schedule, mandatory numeric rules or broader certification claim would require additional authority. |
| Existing functionality reusable | Local prescription aggregate, actor/permissions, tenant-scoped stored procedures, optimistic concurrency, correction/history and frozen artifacts. Historical evidence remains qualified as in Step 85. |
| Implementation blocked | Not blocked by missing core definitions anymore. Block certification acceptance against an unconfirmed release, or an expanded per-refill schedule; do not block the bounded mapped implementation merely because no vendor numeric rules are supplied. |

**Step 87 is ready to propose/implement the narrow capability once authorized**, using the documented engineering policies. No licensed catalogue/safety supplier, pharmacy transport, medication reconciliation or refill-use tracker is established as a prerequisite for this slice. No readiness decision marks PC04.16 satisfied.

## 6. Proposed Step 87 scope

Limit a later implementation to initial days' supply plus independent refill quantity/unit/days' supply in the existing prescription workflow:

- DTOs/Application validation and responses: distinct nullable elements, explicit units/ranges and missing-value policies; preserve SIG and repeat count.
- Additive tenant migration and stored procedures: extend create/update/read/finalize paths without editing migration history or backfilling clinical instructions into old records. Carry values through correction creation.
- Existing prescription UI: label initial versus refill fields; preserve Bootstrap patterns and server-derived prescriber identity.
- Snapshot/PDF: freeze recorded values at finalization and display them separately; preserve old artifact JSON and tolerant legacy rendering. Do not bundle unrelated PC04.05 print omissions or claim that requirement closed.
- Audit/concurrency: retain patient/tenant ownership, trusted actor/provider checks, dedicated permission, RowVersion and transactional lifecycle events; no clinical hard deletion.
- Focused checks: validation, DTO/repository/procedure mappings, correction, snapshot/legacy rendering, isolation/permission/concurrency boundaries; execute the proposed manual examples after implementation. No full-suite rerun is proposed by default.

Possible later implementation branch: `feature/step-87-pc04-16-independent-refill-instructions`; not created. No Step 87 work starts here.

## 7. Qualifications and documentation checks

Inventory remains **12 IDs / three original packages**; PC04.16 remains MISSING. PC10.01/.02 qualified closure, deferred PC10.02 audit, prior PC08/PC09/PC10.01 deferrals and the historical Step 46 **33 passed / one failed / zero skipped** manifest-assertion qualification remain unchanged. Local source inspection is not OntarioMD acceptance or deployed manual verification.

Documentation checks cover source/report paths, eight proposed scenario rows, evidence labels, whitespace and Git scope. Reports 118/119 and all source artifacts are preserved. No application/database changes, migrations, browser operations, test/build reruns, commits or merges. Temporary PDF previews are source-reading aids only. Stop after this report.
