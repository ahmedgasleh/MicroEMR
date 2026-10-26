# Step 204 — PC07.01 / DE09.034 Prescription Reason Code implementation

Date: 2026-10-26. **Local implementation COMPLETE; automated acceptance PASS with the qualifications below.** Persistent migration and runtime activation are outside this step. PC07.01 remains PARTIAL; mandatory inventory remains **7 IDs / 3 packages**.

## 1. Authority, scope and approved decisions

Reuse [Step 203](280-step203-de09-034-reason-code-assessment.md), its existing CDS-S v2.1 Final Data Dictionary mapping (DE09.034, row 160), and the explicit Step 204 owner-approved contract. Exact definition:

> A coded form of the problem that is the reason for the current prescription. Facilitates cross-checking the use of a therapy against its indication.

Mandatory support/storage, Optional terminology conformance; no mandated terminology or allowed-value list was identified in that mapping. The owner has now authorized manually supplied identifiers and codes without terminology membership verification. This resolves the bounded implementation approval prerequisite described in Step 203; it does not establish an authoritative clinical vocabulary.

One optional coding group per prescription. No terminology lookup, code generation, automatic indication cross-check, external integration, new navigation, transmission change, PDF change or prescription-list Actions change. Existing free-text Indication and cancellation Reason remain independent. Prior DE09.030/.031/.033 qualified conclusions are preserved.

## 2. Branch and preservation

Active branch: **`feature/step-204-de09-034-reason-code`**, created from the existing `main` HEAD `8921891a16567eae5603301a7cfd3a3b61c39c10` before application/SQL/test edits. The branch did not exist. Existing modified/untracked Step 199–203 work and development configuration were inspected first; creating a same-HEAD branch was safe and did not replace any working files. Hashes were checked immediately after switching.

Main still points to the original HEAD. No staging, commit, merge, push, pull, fetch, stash, reset, clean, discard or GitHub operation occurred. All changes remain uncommitted. The feature branch's working tree includes the owner's pre-existing work; it is not a clean, reason-only changeset.

Seven already-modified files were augmented. Their complete pre-step bytes were retained for comparison under `.tmp/step204/before/`. All other pre-existing file hashes still match `.tmp/step204/pre-existing.json`; results are in `.tmp/step204/preservation-check.json`. In particular, prior migration 0095, address implementation/tests/assets, reports and `appsettings.Development.json` were preserved. No chronology edit was made.

## 3. Files changed by Step 204

| File | Change |
|---|---|
| `src/MicroEMR.Application/PatientPrescriptions/PrescriptionReasonCoding.cs` | New coding DTO, strict JSON converter, structural normalization and safe validation/review exceptions |
| `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs` | Atomic presence contract, response/review fields, Application validation, correction inheritance |
| `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs` | Wide stored-procedure parameters, optional read mapping, SQL error mapping |
| `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs` | Thin safe validation/review error responses |
| `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs` | Form operation binding, edit/modal values and safe error display |
| `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml` | Shared Create/Edit coding fields, whole-group clear and independent correction review |
| `src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml` | Encoded full Detail coding section / Not recorded |
| `src/MicroEMR.Web/ClientApp/patients/prescription-reason-coding.ts` | Pair/Unicode/length validation, modal load/reset, clear and review state |
| `src/MicroEMR.Web/wwwroot/dist/patients/prescription-reason-coding.js` and `.js.map` | Emitted module and source map, only for the new TypeScript file |
| `db/tenant-clinical/migrations/0096-prescription-reason-coding.sql` | New additive schema, validation helpers and six updated procedures |
| `db/tenant-clinical/manifest.json` | Append 0096 after verified existing 0095; no historical entry rewritten |
| `tests/MicroEMR.Api.Tests/PrescriptionReasonCodingTests.cs` | Contract and disposable SQL/API tests; existing fixture composition |
| `tests/MicroEMR.Api.Tests/PrescriptionReasonCodingWebTests.cs` | Existing Razor/Chromium harness, form binding and modal serialization tests |
| `tests/MicroEMR.Api.Tests/PrescriptionReasonCodingScopeTests.cs` | Reason-specific list Actions and legacy/signed PDF output comparisons |
| `tests/prescription-reason-coding.test.cjs` | Eight focused tests using the existing Node fake-DOM approach |
| This report | Contract, evidence, limitations and Step 205 recommendation |

Ignored `.tmp/step204/` holds local receipts, preservation snapshots and test/build artifacts. No new framework or package dependency was introduced. Existing prescription models and stored-procedure architecture were reused; there is no EF migration or unrelated domain refactor.

## 4. Data contract and validation

Canonical API shape:

```json
{
  "reasonCoding": {
    "system": "urn:TEST:example",
    "code": "Ab-01",
    "version": "2026",
    "display": "TEST ONLY supplied problem"
  }
}
```

| Component | SQL storage | Maximum UTF-16 units | Obligation |
|---|---|---:|---|
| system | `ReasonCodeSystem nvarchar(200) NULL` | 200 | Required when group exists |
| code | `ReasonCode nvarchar(100) NULL` | 100 | Required when group exists |
| version | `ReasonCodeVersion nvarchar(100) NULL` | 100 | Optional within known group |
| display | `ReasonCodeDisplay nvarchar(500) NULL` | 500 | Optional within known group |

These are approved engineering limits, not externally mandated lengths. `ReasonCodingReviewed bit NULL` supports the existing linked-correction lifecycle.

Application, Infrastructure boundary validation and SQL enforce the group. Boundary trimming uses the established explicit space set: U+0020, U+00A0, U+1680, U+2000–200A, U+202F, U+205F, U+3000. Limits apply after trimming. C0/C1 controls, CR/LF, U+2028/U+2029 and unpaired UTF-16 surrogates reject before trimming. Valid supplementary/combining characters, case, punctuation and internal spacing are preserved; no Unicode normalization, case folding or code substitution occurs. Optional blank components normalize to null. Required blank components reject. Nothing is truncated.

JSON rejects nonobjects, nontext components, duplicate/unknown members and malformed Unicode. Application JSON accepts component names case-insensitively, emits canonical lowercase; SQL's internal JSON parameter accepts those canonical names. This is an internal transport rule, not a coding-system restriction.

| Input | Initial Draft | Existing Draft |
|---|---|---|
| Entire property omitted | No coding | Preserve all four stored values |
| Explicit `reasonCoding: null` | No coding | Atomically clear all four |
| Complete valid system/code group | Store normalized group | Atomically replace group |
| Optional version/display omitted from supplied group | Null | Null, not inherited from old group |
| Empty object, blank/partial pair, orphan version/display, invalid component | Reject | Reject with no prescription mutation |

Never recorded, legacy unknown and explicitly cleared currently read as null / **Not recorded**; audit retains the distinction between absent history and an explicit change. No new mandatory entry or clinical coding policy is imposed. The UI requires its explicit whole-group clear action; manually deleting the pair and submitting a replacement rejects as an empty group.

## 5. Migration, persistence and compatibility

The actual manifest/directory ended at 0095 before editing. **0096 was created and successfully executed only inside disposable test databases** through existing fixture batch conventions. It was **not applied to `MicroEMR_LocalDev_Fresh` or any persistent/shared/production database** by this step. No persistent database was queried or modified; no clinical save, service restart or deployment occurred.

Migration adds nullable columns without backfill and a CHECK enforcing all-null versus a complete, canonical system/code pair with optional values. SQL helpers validate wide input before assignment to bounded columns, preventing silent truncation. Previous migrations are unchanged. The existing migration runner remains responsible for sequencing, transaction/history/hash guards; this is not a standalone rerunnable script or a rollback migration.

Six procedure definitions are carried forward from 0095 and extended narrowly:

- `PatientPrescription_GetByPatientUid` and `PatientPrescription_GetByUid`: group projection and independent review state.
- `PatientPrescription_CreateDraft`: validated atomic insert and locked correction inheritance.
- `PatientPrescription_UpdateDraft`: atomic omission/replacement/clear, binary exact change detection and review invalidation under existing locks.
- `PatientPrescription_Finalize` and `PatientPrescription_SignAndFinalize`: independent persisted review gate and prospective coding snapshots.

New parameters have optional defaults. Repository reads tolerate absent new projection columns. For a correction whose source has no coding, the Application leaves the new input omitted, preserving compatibility with legacy procedure fixtures and avoiding unnecessary parameters. With 0096, the locked source read remains authoritative even when no coding is present. Coding-bearing writes require 0096; this does not authorize activation before schema/runtime readiness is established.

Invalid SQL groups use error 53024; missing correction review uses 53025. These map to safe errors. Existing permissions, actor resolution, patient/tenant scoping, owner/state guards, transaction/audit behavior and eight-byte RowVersion checks remain in place. Migration validation is not a claim of backup/recovery verification.

## 6. Corrections, audit and historical records

All four source components carry forward exactly, under the existing correction source lock, without refreshing terminology/version/display. Independent review starts false for known inherited coding. Review is saved through the authorized Draft update, shares its audit and RowVersion, and is required by both finalizers when source/current coding warrants it. Another field's review or a transition flag cannot satisfy this gate.

Changes to any normalized component, including optional members or clearing, invalidate saved review unless the same reviewed Draft save explicitly acknowledges the new group. Unrelated saves preserve review. Clearing known source coding retains the correction review obligation. SQL derives the obligation from persisted source/current data; UI flags cannot bypass it.

Create, replacement and clear use the existing clinical audit snapshots, including all four SQL components. A failing audit write rolls the prescription update back. Finalized prescription coding cannot be edited. Existing source facts and original artifact bytes/JSON remain unchanged; normal existing supersession metadata still advances in its audited workflow. New finalization artifacts freeze `ReasonCodingVersion`, the complete group and its saved review state. Renderers ignore that new metadata, preserving PDF behavior. Cancellation keeps its independent reason and the clinical coding/Indication.

## 7. UI and transport

Create/Edit use one optional Bootstrap fieldset near the unchanged Indication control. Coding System and Code are required together; Version and Display are optional. The guidance discloses manual supply and absence of terminology/clinical verification. Visible values are not truncated by maxlength attributes. Clear removes the entire group. Correction review has its own control and must be saved before finalization.

Modal Edit returns the exact group and saved review state. TypeScript listens to the existing prescription load event and guards delayed reset against a newer record; absent optional members and review never leak between drafts. Full Detail encodes user text and uses text wrapping; absent coding shows **Not recorded**, and absent optional members have no populated rows.

No list Actions view, PDF renderer, print/transmission implementation, navigation or existing clinical field was edited. Automated comparisons test that populated/cleared coding and review do not change Actions or rendered PDF layouts. Browser checks use production Razor/CSS/assets at 1280×900 and 360×900 with long unbroken and Unicode values; all application/network requests are blocked in that harness.

## 8. Automated acceptance mapping

Requirements were mapped before new test code: reuse Step 203 §8 and `.tmp/step204/coverage-plan.md`. Prior name/address/pickup tests supply fixtures and shared behavior evidence; their passing results were not treated as proof of reason coding. All following new methods passed. SQL methods use the new fixture in `PrescriptionReasonCodingTests.cs`; Web and scope methods use their respective files above. Multiple parameterized cases are counted separately.

| Requirement | Exact test method | Explicit assertions / result |
|---|---|---|
| Optional group, Unicode trim/case/internal spacing | `ReasonCodingContractTests.UnicodeBoundaryTrimPreservesCaseAndInternalCharacters` | Null accepted, exact normalized record, optional blanks null — PASS |
| Four UTF-16 limits and no truncation | `EveryComponentCountsUtf16WithoutTruncation`; `ReasonCodingSqlTests.SqlAndApiUtf16BoundaryAgreementWithoutTruncation` | Exact supplementary-character limit accepted, +1 rejected for each member; SQL validator/API agree — PASS |
| Pair atomicity / orphan values | `PartialGroupsAndOrphanOptionalsReject`; `InvalidApiAndSqlGroupsNeverMutate` | Missing/null/blank required values, orphan optional values and empty object reject — PASS |
| Controls and broken Unicode | `AllComponentsRejectControlsBeforeTrimming`; `InvalidSurrogatesRejectInEveryComponent` | Every component rejects controls/line separators and unpaired surrogates — PASS |
| JSON presence/type/duplicates | `JsonPresenceNullCompleteReplacementAndDuplicateValidation`; `InvalidApiAndSqlGroupsNeverMutate` | Omission and null survive serialization; duplicate/unknown/nontext members reject — PASS |
| Persistence, exact retrieval, replacement, clear, Indication | `ReasonCodingSqlTests.ApiAtomicReplacementOmissionClearAuditAndIndicationIndependence` | API create/fresh GET; omission preserves; normalized replacement clears omitted optionals; explicit clear all-null; Indication unchanged — PASS |
| Clinical audit and RowVersion | Same method; `SecurityConcurrencyAuditRollbackAndSqlConstraints` | Create/replace/clear audit values, changed version, stale token rejected, failed audit leaves group/version unchanged — PASS |
| Wide raw SQL / storage constraints | `WideRawProcedureTrimsAtBoundaryAndRejectsLongGroupsBeforeAnyWrite`; `SecurityConcurrencyAuditRollbackAndSqlConstraints` | Procedure trims before checking exact limit; each overlength rejects without insert; orphan/noncanonical/control/malformed storage writes reject — PASS |
| Permissions, ownership, patient and tenant isolation | `SecurityConcurrencyAuditRollbackAndSqlConstraints` | Reader API forbidden; wrong owner rejected; other patient/tenant update unavailable; no changed group/version — PASS |
| Exact correction inheritance / independent saved review | `CorrectionCarryReviewResetFinalizationSnapshotsAndArtifacts` (both finalizers) | All four inherited; unreviewed and other-field-only review reject; unrelated save retains review; optional replacement/clear invalidate; explicit saved review permits finalization — PASS |
| Immutability, snapshots, legacy and cancellation independence | Same method | Original finalized update rejects, original coding/artifact equal, new snapshot equals all four coding components and saved review, prior signed artifact unchanged, cancellation reason and Indication independent — PASS |
| Production Create/Edit/Detail encoding/responsiveness | `ReasonCodingWebTests.ProductionRazorEncodingEntryEditClearAndResponsiveLayout` (3 views) | Exact four values, escaped script-like text, labels, required pair, clear and unchanged Indication editor, no page overflow at both widths including long values — PASS |
| Absent Detail / optional display | `DetailAbsentIsNotRecordedAndOptionalsAreOmitted` | Not recorded for null; no Version/Display row for omitted members — PASS |
| Atomic form operations and modal JSON | `WebPostBindsAtomicPresenceBeforeValidation` (3 cases); `ModalEditIncludesExactGroupAndIndependentReviewState` | Preserve/clear/set group binding, unchanged Indication, exact modal values, canonical JSON and review flags — PASS |
| List Actions independence | `ReasonCodingScopeTests.ProductionPrescriptionListActionsDoNotChangeWithReasonOrReview` (Draft/Finalized) | Complete Actions markup equal before/after coding and review, excluding only dynamic antiforgery token — PASS |
| PDF independence | `LegacyOutputAndSignedPdfLayoutAreUnchangedByReasonSnapshot` (2 cases) | Full body/header/footer equality for coding metadata and null; legacy capture-renderer bytes equal — PASS |
| Client behavior | Eight named cases in `tests/prescription-reason-coding.test.cjs` | Pair/optional validation, exact Unicode modal load, complete clear, review invalidation, each limit/control, reset race, validation redisplay — **8/8 PASS** |

Existing regression fixtures additionally verify trusted actors, permission attributes, clinical ownership, older client/date/correction behavior, private notes, CPP preferences and output. New reason tests do not replace those protections. No production database or authenticated browser clinical save is part of the evidence.

## 9. Execution results and corrected findings

Receipts are local under `.tmp/step204/`:

| Execution | Result |
|---|---|
| Initial backend focused run, `reason-focused.trx` | 32 cases: 31 PASS / 1 FAIL. New omission test incorrectly searched a substring also found in `reasonCodingReviewed`; corrected to inspect the exact JSON property |
| Initial Web test compilation | Missing test namespace imports corrected; not a test execution |
| Expanded focused run, `reason-focused-complete.trx` | 44 cases: 43 PASS / 1 FAIL. New Detail test assumed the pre-existing view rendered Indication; corrected the assertion to its existing Create/Edit scope, without adding unrelated Detail behavior |
| Affected JSON/browser cases, `reason-affected.trx` | **4/4 PASS**, including long-value responsive checks |
| Raw SQL / invalid-group additions, `reason-sql-boundaries.trx` | **11/11 PASS** |
| New frontend focused, `reason-node.log` | **8/8 PASS**, zero skipped |
| One selected backend regression run, `prescription-regression.trx` | **349 cases: 328 PASS / 21 FAIL**; all **49 new reason-specific cases PASS** |
| Compatibility correction retest, `corrected-corrections.trx` | **23/23 PASS**: every one of the 21 failing existing correction cases plus both reason-code correction cases |
| Strengthened full snapshot assertion, `reason-complete-snapshots.trx` | **2/2 PASS**: both finalizers compare all four frozen components, not only display/review; affected cases only |
| Frontend regression, `frontend-regression.log` | **40/40 PASS** (32 existing + 8 new), zero failed/skipped |
| Affected-project build, `final-build.log`; post-fix build, `corrected-build.log` | Both PASS; **0 errors / 6 NU1900 warnings** in each |
| TypeScript project validation, `typescript.log` | PASS, `tsc -p src/MicroEMR.Web/tsconfig.json --noEmit` |
| Final diff/preservation checks | PASS; no whitespace errors; unrelated pre-existing hashes and main ref unchanged |

The regression failures exposed a real compatibility error in the initial new correction path: a source with absent coding was converted into an explicit-null parameter, which older stored-procedure fixtures do not support. The Application now omits that parameter when source coding is absent. No old SQL/test fixture was changed to hide the failure. The targeted retest covers all 21 failures and both coding-bearing correction finalizers; **all 349 selected backend cases now have passing evidence across these runs**. This is not a claim that a single post-fix 349-case run occurred. No second full regression cycle was needed.

Focused cases overlap regression and correction retests; do not add their counts as unique tests. Six NU1900 warnings concern unavailable NuGet vulnerability metadata, not six identified package vulnerabilities. Cached restored assets were reused with `--no-restore`; no dependencies were updated. The existing VS Code/Electron Node host also emitted crashpad access-denied diagnostics, while TypeScript and all Node test commands exited successfully.

## 10. Qualifications and Step 205

No identified uncovered functional assertion blocks this bounded automated implementation acceptance. API/SQL and production Razor/browser assertions are separate automated harnesses; they do not establish a live authenticated end-to-end Web/API session on the owner's persistent tenant. The browser checks establish measured behavior at the two stated viewports, not universal device coverage or subjective usability. No manual acceptance was performed or claimed, and repeated manual Draft saves are not needed to prove the covered assertions.

Remaining qualifications:

- Persistent migration 0096 and matching live runtime activation were not authorized or performed. Existing running services were untouched. Current live compatibility with this change is unverified.
- Recovery remains **CONDITIONAL**, carrying forward the owner-approved recovery-point/restore qualifications from prior steps; no backup or restore was performed or verified here.
- Structural acceptance does not verify terminology membership, coding-source authority, clinical suitability or external coding conformance. Clinician feedback on entry responsibility and workflow usefulness remains appropriate; no fabricated code set or external lookup was added.
- Historical artifacts remain unchanged; coding is prospective metadata and Create/Edit/full Detail functionality only, without PDF/transmission/destination identity certification claims.
- NuGet vulnerability-metadata availability remains qualified as above.

**Recommended Step 205:** DE09.034 local migration/runtime/recovery readiness and automated evidence reconciliation. Verify the actual intended development server/database/tenant, recovery prerequisites and 0096 state read-only; reuse this evidence and identify any actual activation gap. Obtain separate explicit authorization before applying 0096 to a persistent database, changing running Web/API/Auth or performing any clinical save. If activation is later authorized, verify matching compiled assets/runtime and prefer an existing supported authenticated automation harness; retain any genuinely unverified live-session qualification rather than require repeated manual saves by default.

DE09.034 is implemented and automatically verified within the approved supplied-code scope, with persistent activation/live acceptance still qualified. **PC07.01 stays PARTIAL**, including remaining source-qualified destination identity, DIN/source, applicable protocol/authority/retrieval/audience residuals described in Step 203. This step does not reassess those gaps or deferred external requirements. Inventory unchanged: Prescribing PC04.01/.06/.07/.09/.14, CPP PC07.01, Scheduling PC09.03 — **7 IDs / 3 packages**.
