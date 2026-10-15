# Step 87: PC04.16 Independent Refill Instructions Implementation

Date: 2026-10-11. **PC04.16 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Inventory remains **12 mandatory IDs / three original packages**. Implementation follows [Step 86](120-step86-pc04-16-medication-cdss-definition-mapping.md); no OntarioMD acceptance or manual PASS is claimed.

## 1. Git checkpoint

Starting branch **main**, HEAD `a1645ae`, `pc10-02 referral list evidence assessment`. Starting tree contained only untracked reports 118, 119 and 120. They remain unchanged and untracked. Created **`feature/step-87-pc04-16-independent-refill-instructions`** from that checkpoint; the sandbox initially blocked Git ref writing, then authorized branch creation succeeded. No staging, commit, merge or discard.

## 2. Contracts, behavior and files changed

Existing initial `Quantity`/`QuantityUnit` and `AuthorizedRepeats` are retained. Added nullable `InitialDaysSupply`, `RefillQuantity`, `RefillQuantityUnit` and `RefillDaysSupply` to Draft and response contracts. Values round-trip independently; missing values remain null and are never inferred from initial quantity or Directions.

| Area / changed files | Change |
| --- | --- |
| Application `PatientPrescriptions/PatientPrescriptionModels.cs` | New fields and focused validation; preserve separately recorded values in printable output. Application mutation entry points also validate the new instructions before repository access. |
| Infrastructure `PatientPrescriptions/PatientPrescriptionRepository.cs` | Typed SQL parameters and nullable read mappings. A scoped update returning no row now returns null, matching the existing nullable contract instead of throwing a server error. This specific cross-patient failure was uncovered by SQL tests. |
| API and Web `Controllers/PatientPrescriptionsController.cs` | Copy all new fields during correction creation and Web Draft loading. Existing permissions/actor/lifecycle paths remain intact. |
| Web `Views/PatientPrescriptions/_Form.cshtml` | Existing shared create/edit form shows Initial dispense and Refill instructions separately. Inputs distinguish quantities, units, days' supply and authorized refill count. |
| Web `Views/Patients/Details.cshtml` | Prescription rows show initial versus refill instructions; existing modal edit population includes new fields. Only the existing inline field list changes; no unrelated JavaScript-to-TypeScript migration. |
| Database manifest and new migration 0073 | Additive columns, constraints and affected procedure definitions; see below. |
| New `PrescriptionRefillInstructionsTests.cs`, `PrescriptionRefillSqlTests.cs` | Validation/serialization, actual SQL persistence and integrity, edit/correction mapping, legacy preservation, output composition and actual PDF generation. |

No medication-list synchronization, reconciliation, discontinuation, refill-use ledger, per-refill schedule, catalogue/safety integration or pharmacy transmission is added.

### Explicit engineering policies

- Days' supply is a nullable positive whole number, bounded by SQL/C# `int`; no automatic date/SIG calculation.
- Refill quantity uses the existing quantity range and SQL `decimal(18,3)` representation. Supplied refill values require positive quantity with no more than three decimal places and a nonblank unit of at most 50 characters.
- Quantity/unit must be supplied together. Quantity-only or duration-only instruction concepts are supported; both new concepts may also remain unspecified.
- Explicit refill quantity/unit/days' supply with zero authorized refills is rejected with an explanation. Blank new fields remain valid for legacy and new zero-refill orders.
- Equal and differing initial/refill values are both valid. No mathematical consistency rule is inferred from Directions, frequency, PRN or taper text.

These are technical representation policies, **not additional mandatory OntarioMD numeric/default rules**. Existing initial quantity validation and refill-count behavior are preserved.

## 3. Additive schema and stored procedures

New [0073-prescription-refill-instructions.sql](../../db/tenant-clinical/migrations/0073-prescription-refill-instructions.sql), registered once at the end of `db/tenant-clinical/manifest.json` (74 unique manifest entries):

- Adds nullable `InitialDaysSupply INT`, `RefillQuantity DECIMAL(18,3)`, `RefillQuantityUnit NVARCHAR(50)` and `RefillDaysSupply INT`, with no clinical defaults or backfill.
- Adds positive-duration, complete positive quantity/unit and zero-refill consistency constraints.
- Replaces `PatientPrescription_GetByPatientUid`, `GetByUid`, `CreateDraft`, `UpdateDraft` and `Finalize` definitions to carry the new values. New create/update parameters default to null for compatibility with existing callers.
- Finalize captures explicitly stored values and `RefillInstructionsVersion=1` in a newly created artifact snapshot. Draft-only update, patient ownership, prescriber, RowVersion, provider revalidation, transaction and lifecycle audit checks remain in the existing paths.

No old migration is edited. Existing finalized/cancelled/superseded clinical content and artifact JSON are not rewritten. Disposable upgrade tests verify legacy values remain null, legacy RowVersion is unchanged and artifact JSON survives migration and later correction/supersession.

**Deployment prerequisite:** apply migration 0073 through the existing tenant migration workflow before running the updated API/Web binaries. Step 87 applied the prescription foundation and new migration only to disposable test databases; it did not migrate or restart the running application tenants.

## 4. UI and output

Create, edit and correction retain explicitly supplied independent instructions. Chart rows label unspecified new values as **Not recorded**. The shared Bootstrap form labels initial dispensing separately from subsequent refills; refill values do not silently copy initial values.

New finalized snapshots render separate initial quantity, initial days' supply, authorized refill count, refill quantity/unit and refill days' supply. Missing optional values are labelled **Not recorded**. Snapshots without the new version marker retain the original rendering layout, including its quantity/repeats line. The existing architecture stores immutable JSON and renders PDFs on demand; no historical artifact regeneration/backfill job or artifact write is introduced. Legacy snapshot and generated HTML preservation are tested; historical PDF byte identity is not asserted.

## 5. Executed automated evidence

| Check | Actual result |
| --- | --- |
| First development attempt | Sandboxed command stalled without results; interrupted. An unsandboxed Debug build then failed because running API/Web processes locked their output DLLs. No tests passed in that blocked build. Applications were not stopped. |
| Focused tests in separate `Step87` configuration | **19 passed / nine failed / zero skipped / 28 total**. Failures exposed incorrect decimal-format expectations and the scoped-update no-row exception. |
| Targeted rerun after fixes, plus edit/correction case | **10 passed / zero failed / zero skipped / 10 total**. This overlaps earlier cases; counts are not summed. |
| Single final relevant Release build | `dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-restore --verbosity minimal -m:1 -nr:false`: **passed, zero warnings / zero errors**. Includes referenced API, Web, Infrastructure, Application and DatabaseTool projects and affected test compilation. No full solution build. |
| Single final Release medication/output regression selection | **49 passed / zero failed / zero skipped / 49 total**; no-build/no-restore. Selection: `PatientPrescriptionFoundationTests`, `PrescriptionRefillInstructionsTests`, `PrescriptionRefillSqlTests`, `ClinicalPdfPreviewTests`. Includes all 30 new cases, existing prescription permission/lifecycle contracts and output regressions. |
| Documentation/manifest/diff | Manifest ordering/unique ID/path checks and whitespace checks passed; final scope reviewed. |

SQL integration uses two isolated `MicroEMR_Step87_*` LocalDB databases and validates their names before cleanup. Actual exercised behavior includes save/read/update/finalize, equal/different amounts and durations, zero/multiple refills, quantity-only/duration-only instructions, stale RowVersion, wrong patient/tenant, other prescriber and missing provider mapping, exactly-once successful lifecycle audit and rollback on audit failure, legacy upgrade, immutability, correction and snapshot preservation. Output composition tests use a capturing renderer; a separate test also executes the existing Playwright PDF renderer and verifies PDF bytes. This is automated evidence, not deployed browser acceptance or visual print review.

No full API/Auth suite or unrelated historical migration-count assertion was rerun or repaired. Preserve the earlier Step 46 **33 passed / one failed / zero skipped** historical gate qualification; it is not replaced by this scoped green selection.

## 6. Manual verification checklist — outstanding

After migration/deployment, use synthetic patient prescriptions and an authorized mapped provider. Record deployed version and actual outcomes; no row below is presently PASS.

1. Initial/refill 30 tablets and 30 days, two refills: save, reopen/edit and confirm equal values remain separate.
2. Initial 10 tablets/10 days versus refill 30 tablets/30 days: verify distinct amounts on chart and finalized output.
3. Initial 30 tablets/30 days versus refill 90 tablets/90 days: verify distinct durations persist; also verify quantity-only and duration-only instruction cases.
4. Zero refills with blank refill fields saves; explicit refill instructions with zero refills are rejected without saving or success audit.
5. Three authorized refills displays three subsequent fills, excluding initial dispensing; blank values never become initial-value defaults.
6. Negative/zero optional values, excessive refill precision and incomplete quantity/unit pairs are rejected. Confirm legitimate PRN/taper text is not parsed into a mandatory arithmetic rule.
7. Open a legacy finalized prescription, then create/finalize a correction with new values. Verify source content/output is retained, new fields copy correctly and normal finalized editing remains unavailable.
8. View/print a new finalized prescription: initial/refill instructions and count are readable and distinct, with appropriate missing-value labels. Visually review the actual supported viewport and PDF, including any page breaks.
9. Confirm stale RowVersion recovery preserves saved values; direct permission, cross-patient/cross-tenant and actor restrictions deny unauthorized mutations/read access; correlate real audit actor/patient/prescription and successful versus rejected events.

## 7. Status and remaining qualifications

**PC04.16 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** No inventory reduction: **12 mandatory IDs / three packages**. PC04.05's unrelated demographic, timestamp and multipage omissions are not closed by this refill slice. Source-version/release applicability, unspecified numeric/default requirements and any expanded per-refill scheduling remain qualified as in Step 86.

PC10.01/.02 qualified closure, deferred PC10.02 audit and all previous PC08/PC09/PC10.01 qualifications remain unchanged. Deployment/manual verification is outstanding; no implementation blocker remains in the bounded slice. No automatic commit or merge. Stop after Step 87.
