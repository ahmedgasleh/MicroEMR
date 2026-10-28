# Step 215 — DE09.032 dispensing location identifier, Stage 1

2026-10-28 (America/Toronto). **Approved Stage 1 implementation COMPLETE. DE09.032 remains PARTIAL — IDENTIFIER SUPPORT IMPLEMENTED AND AUTOMATED-VERIFIED; LOOKUP/RETRIEVAL PENDING.** This implements structured per-prescription capture, not unique real-world identity verification. PC07.01 remains PARTIAL; mandatory-gap inventory remains 7 IDs / 3 packages.

## 1. Branch and existing work

Preflight read AGENTS.md, inspected the actual branch (`main`), working-tree status, and the requested branch's existence. The only existing change was the untracked Step 213 report, `289-step213-de09-032-dispensing-location-feasibility.md`. The requested feature branch did not exist. Created and verified **`feature/step-215-de09-032-identifier-stage1`** before implementation. The first sandboxed branch creation was denied by filesystem permissions; the authorized elevated local retry succeeded.

Existing work preserved: **YES**. The Step 213 report's SHA-256 remained `42016BEFD142B9771EE6E6C368BCDFB86CD7D16E4DA6812B0D93DA5198E85337`. The target Step 215 report did not exist before creation. Prior migrations and reports were not edited. Incidental TypeScript outputs were restored to their prior content and line endings; only the new module and its map remain added. Final diff/whitespace review found no unrelated changes.

No commit, push, pull, fetch, merge, reset, clean, stash, remote operation, persistent migration, deployment, application service restart, historical PDF regeneration, external directory call or production/shared access occurred. Inspection reused relevant Step 213/212 evidence and DE09.035/name/address implementations and fixtures; no repository-wide assessment was repeated.

## 2. Implemented contract and API documentation

The existing Draft Create, Draft Edit and retrieval DTOs now support one optional `dispensingLocationReference`:

```json
{
  "dispensingLocationReference": {
    "system": "<user-supplied namespace/source>",
    "identifier": "<opaque value within that namespace>"
  }
}
```

The placeholders above are explanatory, not an approved namespace, registry or identifier format. Each component permits **200 UTF-16 code units** independently after the same boundary trimming as DE09.035. Case, valid Unicode and meaningful internal spacing are preserved. There are no reference version, display, status, address or directory foreign-key fields. Existing correction review metadata is reused as a separate nullable persistence bit; it is not a location status or identity-verification result.

| Request state | Create | Draft Edit |
|---|---|---|
| Group omitted | No reference | Preserve saved pair |
| Explicit `null` | No reference | Clear both components atomically |
| Complete valid object | Store normalized pair | Replace both components atomically |
| Partial, blank or invalid object | Reject | Reject entire request without clinical mutation |

`system` and `identifier` must both contain nonblank text when an object is supplied. Controls U+0000–001F and U+007F–009F, CR/LF, U+2028/U+2029, malformed surrogate sequences, unknown/duplicate properties and nontext components reject. Valid surrogate pairs count as two UTF-16 units. Length checking occurs before fixed-width storage; no silent truncation or case folding occurs. There is no source-specific format or authoritative identity validation.

**Facility name (DE09.031) and address (DE09.030) remain independent supplied facts.** Changing or clearing the reference does not populate, overwrite, reconcile or clear them. Their presence does not establish a match to the reference. Reason coding (DE09.034), protocol reference (DE09.035) and free-text Indication also remain independent. This API contract is documented here and on the Application reference type; the UI states the same distinction.

## 3. Application, API and Infrastructure

The Application type `PrescriptionDispensingLocationReference` follows the existing protocol pair validator/converter. Draft DTO property-presence tracking distinguishes omission from null, and the Application service normalizes before repository mutation. Existing correction creation retrieves/inherits the original reference; SQL repeats inheritance under the existing lock. Controllers retain existing permissions, trusted actor resolution and error handling, adding only typed validation/review errors and Web form mapping.

Infrastructure passes an unbounded JSON procedure parameter after normalization so input is never silently limited to a 200-character parameter. Read mapping returns the pair and correction review state through existing prescription DTOs. No endpoint, dependency, external integration or new business/service abstraction was added. Existing DI, async calls, logging and error infrastructure remain in use.

## 4. Migration and SQL

Actual latest migration and manifest entry were **0097**. Added **`0098-prescription-dispensing-location-reference`**, registered after 0097 in the tenant manifest. No existing migration was modified and no EF migration was introduced.

The additive migration adds nullable `DispensingLocationSystem nvarchar(200)`, `DispensingLocationIdentifier nvarchar(200)` and `DispensingLocationReferenceReviewed bit` to `dbo.PatientPrescription`. The check constraint requires both components absent or both present, with valid stored normalized text; it does not impose cross-prescription uniqueness. Different prescriptions may refer to the same location. A reusable pair validator uses established reason-text normalization/validation functions.

Only the necessary six current procedure definitions are extended: `PatientPrescription_GetByPatientUid`, `PatientPrescription_GetByUid`, `PatientPrescription_CreateDraft`, `PatientPrescription_UpdateDraft`, `PatientPrescription_Finalize`, and `PatientPrescription_SignAndFinalize`. Existing protocol/reason/facility guards and parameters remain. New errors are 53028 (invalid pair) and 53029 (correction review required). Updates preserve/clear/replace the pair in the existing transaction and RowVersion predicate. Both prospective finalizers freeze the reference/review in new artifacts; there is no retroactive snapshot modification.

**Application scope: disposable automated-test databases ONLY.** `DispensingLocationReferenceDatabase` extends the existing protocol fixture and applies 0098 to its two randomly named `MicroEMR_Step87_<guid>` tenant databases on LocalDB. Existing fixture disposal removes them. No application tenant was selected or written. `MicroEMR_LocalDev_Fresh`, all other persistent databases, shared infrastructure and production were untouched. Runtime activation against a persistent tenant remains a separate owner-authorized operation.

## 5. Audit, concurrency and security

Existing stored-procedure audit snapshots include both new columns in complete before/after row JSON. Tests verify populated creation, populated-to-populated atomic replacement and complete clearing, trusted actor 7 despite spoofed actor/patient/tenant payload properties, correct patient/prescription/entity identity, and absence of that prescription's audit in the other tenant database. Tenant isolation is provided by the existing identity-checked tenant connection factory; it is not inferred from a user-supplied tenant field.

Invalid API/SQL input leaves the pair and RowVersion unchanged and adds no successful clinical-change event. The existing transaction also rolls back the clinical change when the audit insert is deliberately failed by a disposable fixture trigger. Stale RowVersion, wrong prescriber, missing prescribing permission, wrong patient and wrong tenant reject or return no row under existing guards. No permission or authentication behavior was weakened.

## 6. Corrections and finalized immutability

Correction Drafts inherit the exact original system/identifier under the existing locked source selection. Incoming replacement values during correction creation do not substitute a different reference. Saved review is independent of facility/reason/protocol review. Changes reset review unless the current save explicitly confirms it; unrelated saves preserve prior review. A deliberate clear retains the original reference's review obligation. Both finalizers enforce review.

Tests cover preserve, replace and clear, including successful finalization of reviewed preserved/cleared references through both finalizers. Direct finalized edits reject. Original reference and preserved artifact remain unchanged after correction; legacy signed JSON is unchanged. Existing correction linkage/state behavior remains in force. No finalized snapshot was backfilled and no historical PDF regenerated.

## 7. Create/Edit/full Detail UI

The shared Bootstrap form adds an optional fieldset with labeled system/namespace and identifier controls, independent clear button and correction review checkbox. The new TypeScript module follows protocol presence/reset/modal-load conventions and validates both components together, including UTF-16 bounds and malformed/control characters. Edit and modal DTO mapping populate the exact saved pair and review state.

Full Detail uses Razor encoding and displays either the exact pair or **Not recorded**. Create/Edit and Detail state: **“User-supplied location reference. Not verified against a dispensing-location directory.”** They also explain that facility name/address are independent and no match is verified. There is no lookup control, facility dropdown or verification claim. Existing name/address controls, list Actions, PDF composition, output renderer, transmission and pharmacy integration are unchanged.

## 8. Automated backend acceptance matrix

All methods below are in the new `DispensingLocationReference*` test classes unless specified. Existing fixtures/renderers/assertion patterns were inspected and reused; existing reason/protocol cases were not rewritten.

| Requested backend acceptance | Passing evidence |
|---|---|
| 1–2 Optional/valid create; repeated namespaced references | `OptionalCreateAndReusableNamespacedIdentity` |
| 3–5 Omitted preserve, explicit null clear, atomic replacement | `JsonPresenceNullCompleteReplacementAndDuplicateValidation`; `ApiAtomicReplacementOmissionClearAuditAndIndicationIndependence` |
| 6–9 Partial, blank, controls, line separators | `PartialGroupsAndOrphanOptionalsReject`; `AllComponentsRejectControlsBeforeTrimming`; `InvalidApiAndSqlGroupsNeverMutate` |
| 10–12 UTF-16 limits, Unicode/case/internal spacing, no truncation | `EveryComponentCountsUtf16WithoutTruncation`; `UnicodeBoundaryTrimPreservesCaseAndInternalCharacters`; `InvalidSurrogatesRejectInEveryComponent`; `SqlAndApiUtf16BoundaryAgreementWithoutTruncation`; `WideRawProcedureTrimsAtBoundaryAndRejectsLongGroupsBeforeAnyWrite` |
| 13 No partial mutation or successful-change audit on invalid request | `InvalidApiAndSqlGroupsNeverMutate` |
| 14–17 RowVersion, permission, patient/tenant isolation | `SecurityConcurrencyAuditRollbackAndSqlConstraints`; existing prescription security cases in regression |
| 18 Trusted actor and complete old/new audit pairs | `ApiAtomicReplacementOmissionClearAuditAndIndicationIndependence`; audit rollback case |
| 19–20 Exact correction inheritance and independent review | `CorrectionCarryReviewResetFinalizationSnapshotsAndArtifacts`; `ReviewedCorrectionCanFinalizePreservedOrClearedReference` |
| 21–23 Original finalized artifacts, direct edit rejection, legacy compatibility | Same correction tests; scope output comparisons; existing legacy/date/prescription regression |
| 24–25 Facility name/address, DE09.034/.035 and Indication independence | Strengthened API round-trip and browser clear assertions retain all independent fields; existing qualified suites remain passing |
| 26 PDF/Actions unchanged | `ProductionPrescriptionListActionsDoNotChangeWithReasonOrReview`; `LegacyOutputAndSignedPdfLayoutAreUnchangedByReasonSnapshot`, complete layout/legacy byte comparisons |

## 9. Automated frontend acceptance matrix

| Requested frontend acceptance | Passing evidence |
|---|---|
| 1 Optional Create | Node optional group case; production Create Razor/browser case |
| 2 Edit population | Production Edit browser case; `ModalEditIncludesExactGroupAndIndependentReviewState`; Node modal-load case |
| 3 Explicit clear | Browser clear assertions; Node clear case; Web bind operation cases |
| 4 Atomic group validation | Node pair/empty/review cases; browser incomplete pair rejection |
| 5 Length | Node independent 200 UTF-16 boundary cases; browser maximum-length layout |
| 6 Unicode | Node valid pairs/malformed/control cases; encoded production Razor/browser values |
| 7 Populated Detail | Production Detail browser case with exact encoded pair |
| 8 Missing Detail | `DetailAbsentIsNotRecordedAndOptionalsAreOmitted` |
| 9 Responsive/accessibility | Production Create/Edit/Detail at 1280 and 360 pixels, no horizontal overflow; associated labels and guidance |
| 10 Existing facility controls | Browser clear retains actual name/address controls plus protocol/reason/Indication values |
| 11 No false directory claim | Rendered guidance assertion and source/diff review; no directory UI or service added |
| 12 PDF/Actions unchanged | Complete scope comparisons and existing regression |

## 10. Execution results and receipts

Receipts are retained locally under ignored `.tmp/step215/`. Counts overlap and are not additive across runs.

| Execution | Result |
|---|---|
| Initial sandboxed focused backend/view run | 28 passed / 18 fixture failures: LocalDB runtime could not create/access its automatic instance. This was an execution-environment failure, not acceptance evidence for SQL. |
| Elevated focused retry, `location-focused-retry.trx` | **46/46 PASS**, zero failed/skipped |
| Additional correction cases and strengthened Unicode assertion, `location-strengthened.trx` | **5/5 PASS**, including four new cases and one overlapping contract case |
| Final unique location coverage, confirmed in final regression | **50 PASS**: 16 contract, 22 SQL, 8 Web, 4 output/scope cases |
| Focused client, `location-node.log` | **8/8 PASS** |
| Single relevant backend regression, `prescription-regression.trx` | **445/445 PASS**, zero failed/skipped, including all 50 location and prior 395 prescription cases |
| Single relevant frontend regression, `frontend-regression.log` | **56/56 PASS**, zero failed/skipped, including all eight new location client cases |
| Affected build, `final-build.log` | **PASS**, zero errors / zero warnings in this `--no-restore` build |
| Project TypeScript validation, `typescript.log` | **PASS**, `--noEmit` |
| Final whitespace, scope and preservation review | **PASS** |

Focused backend command used `dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter FullyQualifiedName~DispensingLocationReference`, with TRX output. The focused follow-up selected only `UnicodeBoundaryTrim` and `ReviewedCorrectionCanFinalizePreservedOrClearedReference`. The final backend run used `--no-build --no-restore` with the retained Step 211 prescription filter plus `FullyQualifiedName~DispensingLocationReference`; exact filter is in `regression-filter.txt`.

The affected build was `dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore`. Client execution used the existing local TypeScript package and VS Code's Node runtime through a hidden helper, without installing dependencies. TypeScript validation used `tsc -p src/MicroEMR.Web/tsconfig.json --noEmit`. Frontend regression ran all nine existing/new `tests/prescription-*.test.cjs` files once. The initial emit's incidental unrelated outputs were restored; it is not counted as an additional validation pass.

## 11. Changed files

| Area | Files changed/added |
|---|---|
| Application | `src/MicroEMR.Application/PatientPrescriptions/PrescriptionDispensingLocationReference.cs`; `PatientPrescriptionModels.cs` in the same directory |
| Infrastructure | `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs` |
| API/Web transport | `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs`; `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs` |
| Views | `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml`; `Detail.cshtml` in the same directory |
| Client assets | `src/MicroEMR.Web/ClientApp/patients/prescription-dispensing-location-reference.ts`; corresponding `wwwroot/dist/patients/*.js` and `.js.map` |
| Database | `db/tenant-clinical/migrations/0098-prescription-dispensing-location-reference.sql`; `db/tenant-clinical/manifest.json` |
| Acceptance | `tests/MicroEMR.Api.Tests/PrescriptionDispensingLocationReferenceTests.cs`; `PrescriptionDispensingLocationReferenceWebTests.cs`; `PrescriptionDispensingLocationReferenceScopeTests.cs`; `tests/prescription-dispensing-location-reference.test.cjs` |
| Documentation | This report; existing untracked Step 213 report preserved byte-for-byte |

## 12. Stage 1 boundary, qualifications and Step 216

| Acceptance dimension | Disposition |
|---|---|
| A. Structured identifier support | **PASS — implemented and automated-verified** |
| B. Atomic persistence/retrieval | **PASS — implemented and automated-verified** |
| C. Audit/security/correction/immutability | **PASS — automated-verified** |
| D. Automated UI acceptance | **PASS — isolated production Razor/browser and client tests** |
| E. Directory-backed identity resolution | **NOT IMPLEMENTED / DEFERRED** |
| F. Lookup/retrieval of location details | **NOT IMPLEMENTED / DEFERRED** |
| G. Source/namespace governance | **DEFERRED — no authoritative source or namespace policy established** |

No remaining uncovered automated requirement blocking acceptance of the bounded Stage 1 contract was identified. This does not establish identity correctness, source authority, real directory resolvability, clinician usability or authenticated persistent-runtime acceptance. **Authenticated live UI visibility remains deferred; recovery remains CONDITIONAL.** No fresh restore/recovery exercise was performed.

Retain the earlier **six NU1900 warnings** qualification: those concerned unavailable NuGet vulnerability metadata, not six identified vulnerabilities. This step's no-restore build emitted zero warnings and does not resolve or reassess that earlier qualification. Auth startup migration/seeding restrictions remain: no Auth restart was performed or authorized. No external identity, terminology or protocol validation claim is added.

Preserve **DE09.034 and DE09.035 — SATISFIED — VERIFIED WITH QUALIFICATIONS**. **PC07.01 — PARTIAL**. Mandatory-gap inventory stays **7 IDs / 3 packages**: Prescribing PC04.01/.06/.07/.09/.14, CPP PC07.01, Scheduling PC09.03. Stage 1 does not fully satisfy DE09.032 and does not reduce that inventory.

**Recommend Stage 1 acceptance:** PARTIAL — IDENTIFIER SUPPORT IMPLEMENTED AND AUTOMATED-VERIFIED; LOOKUP/RETRIEVAL PENDING.

**Recommended Step 216:** owner decision on the remaining namespace/source governance and location-resolution/detail-lookup contract, using Step 213's existing feasibility evidence. If persistent activation of Stage 1 is desired, obtain separate migration authorization and respect recovery/Auth startup restrictions before any activation. No persistent migration or additional lookup implementation is authorized by this report.
