# Step 88: PC04.16 Functional Evidence Reconciliation

Date: 2026-10-15. **PC04.16 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Inventory remains **12 mandatory IDs / three original packages**. Functional closure is not yet supported by the recorded manual evidence. No requirement is marked SATISFIED — VERIFIED.

## 1. Scope and evidence provenance

This assessment reconciles the eight acceptance scenarios in [Step 86, section 4](120-step86-pc04-16-medication-cdss-definition-mapping.md) with [Step 87, sections 2–7](121-step87-pc04-16-independent-refill-implementation.md), the relevant existing refill tests/form/output implementation, and the user's manual reports in this conversation and two supplied `Pasted text.txt` attachments. The attachments are local session evidence, not durable repository exports; this report preserves their relevant assertions. Actual response/SQL captures, deployed version and execution timestamps have not been independently inspected here.

The user accepted the listed security/audit scenarios and confirmed the lifecycle sequence: initial 10 tablets/10 days, refill 30 tablets/30 days, two refills; save/reopen; update refill values to 90/90; finalize; create/finalize correction; retain the original artifact. Subsequent messages explicitly confirmed correlated SQL rows, no successful mutation events for rejected requests, no-view denials, and successful view-only Web reads after fresh sign-in. These are **user-reported manual PASS**, not Codex-executed tests.

Step 87 records a final relevant Release build with zero warnings/errors and a focused **49 passed / zero failed / zero skipped** selection, including 30 new cases. These historical results are reused, not rerun. Earlier overlapping development counts are not summed. Source inspection establishes what those cases cover, not new execution results or manual acceptance.

## 2. Functional acceptance matrix

PASS means the recorded evidence supports the stated scenario; PENDING means an explicit deployed manual observation remains necessary. No new functional FAIL is established. A missing prerequisite during the procedures below changes the affected check to DEFERRED, not PASS.

| Step 86 scenario | Existing implementation and automated evidence | Recorded manual evidence / remaining gap | Assessment |
| --- | --- | --- | --- |
| 1. Identical values: initial 30 tablets/30 days; refill 30 tablets/30 days; two repeats | Distinct DTO/storage fields; `IndependentValuesAreValidAndSurviveJson` and `PersistenceAndFinalizedOutputKeepInitialAndRefillValuesDistinct` include this input. | No explicit equal-value manual save/reopen result. | **PENDING — M1** |
| 2. Different quantities: initial 10/10; refill 30/30; two repeats | Typed parameters, scoped reads/update, frozen final snapshot and output; both tests above include this input. | User reports the dedicated lifecycle sequence PASS, including initial 10/10 versus refill 30/30, subsequent refill update to 90/90, persistence and finalized output. This supports the different-quantity capability; it does not independently prove every visual-layout detail. | **PASS — user-reported manual evidence plus recorded automated evidence** |
| 3. Different durations: initial 30/30; refill 90/90; one repeat | Separate nullable initial/refill days; automated persistence input includes exactly these values. | User's 10/10 versus 90/90 sequence supports independence, but does not explicitly record this one-repeat acceptance example or quantity-only/duration-only cases. | **PENDING — M2**, limited to those unreported cases |
| 4. Zero refills: initial 30/30; no refill values | Blank fields remain null; supplied refill values with zero count are rejected. Recorded validation/persistence cases cover both. | No explicit deployed zero-refill/blank-versus-zero observations. | **PENDING — M3** |
| 5. Positive count: initial/refill 30/30; three repeats | Existing `AuthorizedRepeats`; form explicitly says refills follow initial dispense; recorded three-repeat persistence case. | Two refills are manually reported, but three subsequent fills/count wording is not explicitly verified. | **PENDING — M4** |
| 6. Invalid/inconsistent inputs | Recorded technical validation cases cover zero/negative values, precision, pair completeness, long units, negative count and zero-count instructions. Directions-not-parsed case accepts a quantity/duration arithmetic mismatch. | Earlier JSON conversion errors are not this acceptance evidence. No explicit manual structural/range rejection and non-parsed SIG observations. | **PENDING — M5** |
| 7. Legacy finalized prescription lacking new properties | `LegacyUpgradeAndCorrectionRetainOldSnapshotAndRendering` checks null new fields, unchanged upgrade RowVersion/JSON, old HTML layout and retained artifact after supersession. | New-prescription correction/history PASS does not establish deployed pre-0073 legacy compatibility. No reusable deployed legacy fixture is identified. | **PENDING — M6**; **DEFERRED** if a suitable existing synthetic legacy fixture cannot be identified |
| 8. Display/print separation | New snapshot renderer separately labels quantity, days, refill values and count; missing values display `Not recorded`; recorded actual PDF-generation test and legacy HTML comparison. | User reports correct PDF access and lifecycle output. Explicit visual review of all labels, missing values, supported viewport and print page breaks is not recorded. The referenced downloaded PDF was not visually inspected in this step. | **PENDING — M7**, visual/missing-value checks only |

Relevant tests: [PrescriptionRefillInstructionsTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionRefillInstructionsTests.cs) and [PrescriptionRefillSqlTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionRefillSqlTests.cs). Automated evidence does not convert PENDING rows to manual PASS.

## 3. Completed security, concurrency and audit evidence — do not repeat

All entries below are accepted **user-reported PASS**:

| Scenario | Accepted result |
| --- | --- |
| RowVersion concurrency | Stale save rejected; saved owner content preserved. |
| Missing prescribing permission | Unauthorized mutations rejected. |
| Mismatched patient/prescription identifiers | No unauthorized disclosure/mutation. |
| Cross-tenant identifiers | Tenant isolation preserved. |
| Prescriber draft ownership | Another prescriber cannot modify the owner's draft. |
| Patient-view permission | View-only API reads succeed; no-view API list/read/PDF each return 403; no-view Web access denied. |
| View-only Web chart/artifact | Both succeed after fresh Web sign-in; prior missing-token exception did not recur. Root cause is **not conclusively established**; no code fix or general session-recovery closure is claimed. |
| Clinical audit | User confirms successful lifecycle rows correlated to tenant, patient, prescription, resolved actor, operation/time and saved state, with zero successful mutation events attributable to rejected requests. |

The accepted correction sequence covers new finalized records, not necessarily pre-0073 artifacts. Preserve the distinction. Clinical audit is tenant-local `dbo.AuditLog`; platform security events are separate. No additional audit/security/concurrency run is requested by this assessment.

## 4. Remaining manual procedures — PENDING functional portions only

### Common prerequisites and recording

Use the same isolated tenant, existing authenticated Web session and supported viewport. Record the actual deployed version and migration 0073 status from existing deployment evidence; do not redeploy or migrate for this assessment. Prescriber A must already have effective `Patients.View` and `Prescriptions.Prescribe`, active clinical provisioning and an active linked Provider. Reuse the established fictional **Avery Refill-Test, DOB 1990-01-01**, verifying its recorded UID `7ffd7c45-8178-407f-aa2e-ad6f83430d37` in the selected tenant before use. Do not substitute a real patient.

Navigate **patient chart → Medications → New Prescription**. Common mock values: product name/display `Step88 Mock Tablet — TEST ONLY`; Route `Oral`; Frequency `Once daily`; Directions `TEST ONLY — Take one mock tablet daily.`; prescribed date = actual execution date. Leave optional identifiers, strength, dose, indication and start date blank unless a case says otherwise. Quantity units are `tablets`. Capture actual prescription UIDs from supported requests/responses; aliases below are labels, not fabricated IDs.

For each case record UTC window, account/tenant, input values, actual UI result, captured UID, reopened values and relevant screenshot/PDF reference. Prefer already captured observations for any identical completed action: report them rather than execute again. Use separate drafts for cases needing different final snapshots. A normal save/reopen here verifies the remaining input case; it is not a repeat of concurrency or security testing.

### M1 — Equal values (scenario 1)

1. Set initial quantity/days **30/30**, refill quantity/unit/days **30/tablets/30**, authorized refills **2**.
2. Save Draft; close and reopen Edit on that actual draft.
3. Verify each input retains its value, with initial and refill labels separate and no requirement that values differ. Confirm the chart displays the separate recorded values.

**PASS:** exact equal inputs persist without merging/defaulting. **FAIL:** rejection because values are equal, data loss or substitution. **DEFERRED:** common prerequisite unavailable.

### M2 — One-repeat duration and optional instructions (scenario 3 and Step 86 follow-up)

1. Save/reopen a draft with initial **30 tablets/30 days**, refill **90 tablets/90 days**, authorized refills **1**. Confirm separate 30/90 days and count 1.
2. On a separate draft, use initial **30 tablets**, initial days blank; refill **90 tablets**, refill days blank; count **2**. Save/reopen: optional days remain blank, not inferred.
3. On another draft use initial **30 tablets/30 days**, refill quantity and unit blank, refill days **90**, count **2**. Save/reopen: duration persists without inventing a refill quantity/unit.

**PASS:** all three cases retain exactly the recorded and absent concepts. **FAIL:** valid case rejected, values coupled/defaulted, or missing value becomes zero. **DEFERRED:** prerequisite unavailable. Retain the one-repeat draft for M7 if new output is needed.

### M3 — Zero refills and blank versus zero (scenario 4)

1. Create initial **30 tablets/30 days**, count **0**, all refill fields blank. Save/reopen; verify a valid draft with absent refill instructions.
2. Edit that draft, keep count 0 and enter refill **30 tablets/30 days**. Save: expect an explanatory validation rejection and no successful save. Reopen the original draft and verify unchanged persisted values/RowVersion.
3. Enter **0** into an optional days field; saving must reject it rather than treat it as blank. Clear it and confirm the field can remain absent.

**PASS:** blank zero-refill order saves, incompatible/zero values do not. **FAIL:** silent persistence or coercion of invalid values. **DEFERRED:** prerequisite unavailable. Existing successful/rejected audit correlation remains accepted; no repeat audit run is required here.

### M4 — Three subsequent fills (scenario 5)

1. Create initial **30 tablets/30 days**, refill **30 tablets/30 days**, count **3**.
2. Save/reopen and inspect the chart: count remains **3 authorized refills**, excluding the initial dispense. Do not record four as the repeat count or expect refill-consumption tracking.

**PASS:** count and wording preserve three subsequent fills. **FAIL:** count includes initial dispensing or changes on reopen. **DEFERRED:** prerequisite unavailable. Reuse this draft for print review if needed.

### M5 — Engineering validation and unparsed directions (scenario 6)

Use an otherwise valid, dedicated draft with initial/refill **30 tablets/30 days**, count **2**. Record its saved baseline. In Edit, change **one invalid field/pair at a time**, attempt Save Draft, then discard/reload before the next case:

| Invalid change | Expected |
| --- | --- |
| Initial quantity -1; refill quantity -1 or 0; either days field -1 or 0 | Validation rejection |
| Authorized refills -1 | Validation rejection |
| Refill quantity 1.0001 | Rejection of excessive precision, not silent saved rounding |
| Refill quantity 30 with unit blank; then quantity blank with unit `tablets` | Pair-completeness rejection |
| Refill unit of 51 characters | Length rejection |

Browser validation may stop submission; record that as UI validation, not proof of server execution. Reopening must show the saved baseline unchanged for each rejected input. Existing automated server validation and accepted audit evidence remain separately identified.

Then save a valid mock draft with initial **30 tablets/20 days**, refill **30 tablets/20 days**, count **2**, retaining the one-tablet-daily test directions. Expect acceptance without deriving 30 days from the text. Also use Frequency `Other`, PRN checked, Directions `TEST ONLY — As directed during a taper; mock instructions only.` with otherwise valid fields; expect no arithmetic/SIG-derived rejection or field rewriting.

**PASS:** structural violations are rejected without persistence, valid deliberately non-arithmetic/PRN examples save unchanged. **FAIL:** invalid persistence, silent precision loss, or invented arithmetic restriction. **DEFERRED:** prerequisite unavailable. These checks validate documented engineering policies, not additional OntarioMD numeric rules.

### M6 — Existing legacy artifact (scenario 7)

Prerequisite: an authorized observer identifies an **existing synthetic finalized pre-0073 prescription** whose frozen snapshot lacks the new refill properties/version marker, and supplies its actual patient/prescription IDs and authorized baseline export. Do not create a modern blank-field prescription and call it legacy. If no such fixture/export exists, record **DEFERRED — suitable legacy fixture unavailable**; do not manufacture one using SQL or change migration history.

1. Open that patient's chart and original View/Print. Save baseline clinical response, original output and the observer's original stored artifact JSON export.
2. Confirm historical values remain readable; missing new fields are not inferred from quantity or Directions. Normal editing of the finalized prescription remains unavailable.
3. Create Correction through the existing UI. Enter explicit initial **10 tablets/10 days**, refill **30 tablets/30 days**, count **2**; save/reopen and finalize the correction.
4. Verify correction values/output and links, original Superseded status, and retained original artifact. Have the observer compare original stored artifact JSON before/after: it must be unchanged. Compare displayed historical content rather than requiring regenerated PDF byte identity. Status, supersession link and RowVersion changes associated with supersession are expected; do not require the entire source record to be byte-identical.

**PASS:** original frozen content is retained and correction records explicit new values independently. **FAIL:** original artifact rewritten, legacy content unreadable or new values silently backfilled. **DEFERRED:** fixture, historical baseline or authorized read-only export unavailable. Do not repeat already accepted new-record correction/audit testing.

### M7 — Remaining display/print review (scenario 8)

1. First inspect the already generated lifecycle PDF and existing captured chart at the supported viewport. Do not regenerate a completed lifecycle solely to obtain the same evidence.
2. Confirm clear separate labels and correct values for initial quantity/unit, initial days, refill quantity/unit, refill days and authorized refill count; Directions must remain separate. Inspect every page in print preview/PDF for clipping, overlap or separated labels/values that obscure meaning. Record viewport and print settings.
3. Use M2's quantity-only/duration-only case or M3's blank-field case for missing-value display. If no suitable finalized output already exists, finalize only the necessary pending-case draft and open View/Print. Confirm missing new values display `Not recorded` rather than inferred initial values or zeros. Do not redo lifecycle/audit assertions.

**PASS:** recorded and unspecified concepts are clear and readable in chart and output. **FAIL:** merged/mislabelled values, invented defaults or unreadable output. **DEFERRED:** required output/viewport access unavailable. This does not close unrelated PC04.05 output omissions.

### Cleanup for M1–M7

Retain synthetic drafts, finalized/correction records, PDFs and audit history as evidence. Do not physically delete clinical data or edit frozen artifacts. No permission changes are needed. Sign out after testing. Follow existing administration policy for eventual deactivation of temporary accounts without deleting their history.

## 5. Certification recommendation and retained qualifications

The smallest remaining work is the unreported functional portions M1–M7, reusing existing drafts/output wherever possible. No completed security, audit, concurrency, ownership or view-access test should be repeated. No code fix is proposed from this assessment; the earlier token exception did not recur, and its root cause remains unproven.

After explicit manual outcomes are supplied, recommend a **separate evidence-closure assessment**, not automatic satisfaction. A deferred legacy fixture/export or incomplete visual check must remain visible. Source/release applicability still requires the appropriate authority before any broader certification claim.

Preserved qualifications:

- Step 86 source labels (Baseline folder 5.5 versus document 1.7; CDS-S folder 5.2 DFU versus dictionary 2.1 Final and other publication labels) are not proof of applicability to PCON-2024-02 Stage 3 or OntarioMD acceptance.
- Common prescription-level refill values, nullable fields, positive whole days, precision/ranges and zero-refill handling are documented engineering policies. No authoritative mandatory per-record default/numeric constraint, varying per-refill schedule or SIG calculation is inferred.
- Legacy JSON/HTML preservation has recorded automated coverage; historical regenerated PDF byte identity is not asserted. New-artifact retention PASS does not substitute for legacy verification.
- Preserve the historical Step 46 **33 passed / one failed / zero skipped** manifest-assertion qualification. Step 87's focused green selection does not replace it or imply a new full API/Auth/solution gate.
- Preserve PC10.01/.02 qualified closure, deferred PC10.02 audit, all prior PC08/PC09/PC10.01 limitations, and unrelated PC04.05 demographic/timestamp/multipage output omissions. No other requirement or inventory count changes.

## 6. Git checkpoint and assessment checks

Observed branch: `feature/step-87-pc04-16-independent-refill-instructions`; HEAD `a1645ae`. This report is the only file added by Step 88. All pre-existing changes are preserved, including the Auth Postman callback addition.

Starting modified tracked files:

```text
db/tenant-clinical/manifest.json
src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs
src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs
src/MicroEMR.Auth/Data/SeedData.cs
src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs
src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs
src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml
src/MicroEMR.Web/Views/Patients/Details.cshtml
```

Starting untracked files:

```text
db/tenant-clinical/migrations/0073-prescription-refill-instructions.sql
docs/certification/118-step84-pc10-02-evidence-closure-next-gap.md
docs/certification/119-step85-pc04-16-scope-dependency-assessment.md
docs/certification/120-step86-pc04-16-medication-cdss-definition-mapping.md
docs/certification/121-step87-pc04-16-independent-refill-implementation.md
tests/MicroEMR.Api.Tests/PrescriptionRefillInstructionsTests.cs
tests/MicroEMR.Api.Tests/PrescriptionRefillSqlTests.cs
```

Final status adds only this untracked report to that list. Checks are limited to scoped source/evidence reading, eight-row reconciliation, local report-link/whitespace review and final Git status/diff inspection. No application changes, database operations/migrations, automated tests/builds, browser execution, commits or merges. Manual instructions are not claimed executions. Stop after assessment and await results.
