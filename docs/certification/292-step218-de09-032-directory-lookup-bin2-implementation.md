# Step 218 — DE09.032 tenant-local directory and exact BIN2 identity

2026-10-28 (America/Toronto). **Stage 2 implementation COMPLETE and automated-verified within the approved tenant-local scope. Persistent activation was not performed. DE09.032 remains PARTIAL pending acceptance/activation review; PC07.01 remains PARTIAL; mandatory-gap inventory remains 7 IDs / 3 packages.** No external authoritative directory validation is claimed.

## 1. Git and existing work

Resumed the Step 217 blocker report and Stage 215 implementation/fixtures without repeating the collation assessment. Read AGENTS.md. Actual branch was `feature/step-217-de09-032-directory-lookup`; its only existing change was the untracked Step 217 report. The target branch did not exist. Created and verified **`feature/step-218-de09-032-directory-lookup-bin2`** before application, SQL or test changes.

Existing work preserved: **YES**. Step 217 report SHA-256 remained `FC268404EC4C9198AD363F2F6D69F484CF5B855CA80E6CB3BD177469146D97F0`. This report did not exist before creation. No previous migration/report was changed. The sole previous test edit changes its affected guidance assertion from Stage 1's no-directory wording to the new accurate manual-reference wording; existing validation/preservation assertions remain.

No commit, merge, push, pull, fetch, reset, clean, stash or remote Git operation occurred. No persistent migration, Web/API/Auth restart, production/shared database access, external registry request or deployment occurred. Final diff/whitespace review found only the approved directory/prescription integration, its tests, and this report.

## 2. Approved exact identity semantics

Migration **0099** declares only the new directory `[System]` and `Identifier` columns as `nvarchar(200) COLLATE Latin1_General_100_BIN2`. An unfiltered unique constraint covers the pair across **all active and inactive records** in that tenant database. There is no redundant tenant-key column: verified database-per-tenant routing supplies isolation.

Exact identity retrieval, immutable-identity checking and save-time selected-pair matching use the same BIN2 semantics. Boundaries use the existing Stage 1 normalizer; there is no case folding, Unicode normalization, truncation, invented namespace or registry format. Name/address search remains distinct from identity resolution. Database-default collation and existing prescription identity column collations are unchanged.

`ExactIdentityAndBoundaryLookupAreUnambiguous` demonstrates:

| Assertion | Result |
|---|---|
| System case variants are distinct | PASS |
| `Ab-01` versus `ab-01` are distinct | PASS |
| Composed `é` versus decomposed `e` + U+0301 are distinct | PASS |
| Different supplementary Unicode characters remain distinct | PASS |
| Identical pairs reject | PASS |
| Boundary-trimmed exact lookup returns the single intended record | PASS |
| Both new column collations are BIN2 | PASS |
| Database default and existing prescription column collations retain the pre-migration value | PASS |

`LifecycleImmutableIdentityConcurrencyAndCompleteAudit` additionally proves deactivation does not release identity ownership. No full database collation change was needed or performed.

## 3. Directory model and validation

`dbo.DispensingLocation` stores LocationUid, system, identifier, facility name/address, optional phone, IsActive, CreatedAt/By, UpdatedAt/By and RowVersion. Identity is immutable through maintenance and an update trigger. No delete endpoint or deletion procedure is introduced. An incorrect identity is handled by audited deactivation followed by creation of the correct identity; the UI explains that historical identity is not reassigned. No separately approved in-place identity correction operation exists.

The Application directory service reuses Stage 1's atomic pair validator (200 UTF-16 units per component), the DE09.031 name validator (200), and DE09.030 address normalization/validator (1,000, supported canonical line breaks). Directory name and address are required. Phone follows the existing optional supplied-phone convention: boundary trimming, null when blank, maximum 30 characters, no external format verification. Supported text validation rejects malformed Unicode/control input. Procedure parameters are wide text, validated before fixed-width insertion, preventing silent truncation.

The implementation follows Application DTO/service, Infrastructure stored-procedure repository, DI and thin-controller conventions. The service uses ILogger for rejected identity/concurrency operations. No dependency or new search framework was added.

## 4. Permissions and tenant isolation

Reused **`ClinicSettings.Manage`** for tenant-local directory governance/maintenance and **`Prescriptions.Prescribe`** for prescribing lookup/detail/selection. These are existing explicit permissions. No permission grants, profile changes, platform migration or unrelated clinical access were introduced. Maintenance is available to existing authorized clinic administration staff; lookup is not inferred from authentication alone.

| API surface | Required permission |
|---|---|
| Active search, detail by UID, exact identity resolution | Prescriptions.Prescribe |
| Maintenance search, including inactive, and maintenance detail | ClinicSettings.Manage |
| Create, edit, deactivate, reactivate | ClinicSettings.Manage |

Every repository connection uses the existing tenant SQL connection factory, which verifies assigned database identity against the active tenant. Browser-supplied tenant fields are not used. Clinical actor middleware supplies the trusted active tenant ApplicationUser; directory mutations do not require granting unrelated prescribing privileges to maintenance staff. SQL independently checks that the actor is active.

Tests demonstrate unauthorized read/create denial, prescriber-only maintenance denial, tenant-separated list/detail/exact resolution, foreign-tenant mutation rejection and cross-tenant prescription selection rejection. A spoofed actor/tenant payload does not override the trusted actor.

## 5. Maintenance, search and retrieval

Three directory stored procedures provide Get, Search and audited Save operations (Create/Edit/Deactivate/Reactivate). Identity remains fixed; details may change. Every update/state transition requires the current eight-byte RowVersion and locks the existing row. Duplicate, immutable-identity and stale conflicts return bounded actionable responses.

Search supports facility name, identifier and address; active records are the default. Maintenance users may include inactive records. Page numbers are limited to 1–10,000 and page size to 1–50; SQL bounds offset/take and reads one additional row for HasMore. Stable ordering uses name and UID. Literal search text uses parameterized CHARINDEX, so wildcard characters do not become SQL pattern syntax.

Full details can be retrieved by UID or exact normalized identity. Inactive exact/detail retrieval remains possible for authorized users, preserving intelligibility of historical references. Inactive records are not selectable for new saves.

## 6. Prescription lookup and selection contract

Existing Draft Create/Edit accepts the Stage 1 optional pair and an optional request-only selection:

```json
{
  "dispensingLocationReference": {
    "system": "<selected namespace>",
    "identifier": "<selected opaque identifier>"
  },
  "dispensingLocationSelection": {
    "locationUid": "<tenant-local record UID>",
    "rowVersion": "<base64 version returned by directory detail>"
  }
}
```

The selection requires a complete explicitly supplied pair, nonempty UID and valid directory RowVersion. Existing prescription RowVersion remains independently required on Edit. Create/Update procedures lock the selected directory row **inside the existing clinical transaction**, then verify same-tenant existence, active status, exact BIN2 pair equality and the selected directory version. The lock remains until clinical mutation and its audit commit. No controller-only precheck substitutes for this transactional verification.

Invalid, stale, inactive, mismatched and foreign-tenant selections reject without changing clinical state/RowVersion or adding successful-change audit. Web errors specifically instruct the user to search/select again. No prescription is saved by searching, previewing or selecting a result; the existing Save Draft remains the write boundary.

Manual Stage 1 identifiers remain allowed, including unresolved/inactive-source references. They do not acquire directory provenance merely because a matching directory entry exists. Pair omission preserves existing values/provenance; explicit manual replacement or clear removes prior directory provenance; a valid selection records its UID. Stage 1 preserve/clear/replace semantics remain intact.

## 7. Clinician-confirmed name/address population

The Bootstrap lookup fetches active matches, retrieves selected details, and displays system, identifier, name, address and phone using text content. The clinician previews before applying. Mismatches with recorded name/address produce a warning. **Only an explicitly checked confirmation copies/replaces name and address.** Without it, choosing the identity changes only the identifier pair and pending selection. Cancel leaves prescription fields unchanged. The UI states that selection is pending validation at Save Draft.

The server never auto-populates name/address from directory data. They remain independently submitted prescription facts, validated/audited by their existing paths. The API may explicitly submit those fields as before; no server-side directory match silently authorizes copying. Browser tests verify both unchanged manual facts and explicitly confirmed replacement.

Manual edits/clear invalidate pending selection. Modal load/reset clears selection and preview; revision checks discard late search/detail responses belonging to a previous form record. Failed/inactive lookups retain manual-entry availability. Controls have associated labels, live status, keyboard search, encoded/text-safe details and responsive Bootstrap layout.

## 8. Audit and concurrency

Directory Save uses the existing `dbo.AuditLog`, not a parallel framework. Each successful Create/Edit/Deactivate/Reactivate records trusted UserId, TenantUid in the snapshots, directory identity, old/new details/state/RowVersion and source application, within the same transaction. Stale/duplicate/immutable/invalid requests produce no successful-change event. Injected audit failure rolls back both details and RowVersion.

Clinical audit continues to use complete prescription snapshots. A new nullable `DispensingLocationDirectoryUid` and foreign key retain the validated selected identity in the row/audit. No separate clinical audit stream is added. Pair clearing removes provenance atomically; omitted edits preserve it. Existing actor, patient, permission, prescriber ownership and prescription RowVersion guards remain.

The reference DTO itself does not claim authoritative identity validity or that independently recorded facility values match the directory. Only server-validated selections obtain stored directory provenance.

## 9. Historical immutability and Detail display

Directory detail edits/deactivation do not update prescriptions, facility facts, corrections or artifacts. Tests compare preserved artifacts and stored values before/after directory mutation/deactivation. Historical exact resolution can return the inactive directory entry, without substituting its current details into the prescription.

Full Detail displays saved prescription facts and distinguishes **a tenant-local selection checked when saved** from **a manual/inherited unresolved reference**. It states that current directory details may differ and no external identity/current match is implied. It performs no automatic live-directory substitution.

Corrections inherit the exact original Stage 1 pair and independent name/address with existing review requirements. A new correction does not automatically claim a fresh directory selection: it must be deliberately selected again for new resolution. Inactive original references may therefore remain preserved/reviewable as manual/inherited references. Existing finalizers, finalized-edit restrictions and artifact preservation remain unchanged. No finalized snapshot backfill or historical PDF regeneration occurred.

Prescription PDF generation/layout, list Actions, external transmission and pharmacy integration were not changed. Existing output comparison tests remain passing.

## 10. Migration and runtime scope

Verified latest migration was **0098**. Added **`0099-dispensing-location-directory`** and its tenant manifest entry. The additive migration creates the directory, constraints/immutable-identity trigger, three directory procedures, nullable prescription provenance/FK, and extends four existing procedures: CreateDraft, UpdateDraft, GetByPatientUid and GetByUid. Finalization and output procedures are not replaced by 0099. No existing migration was edited and no EF migration was introduced.

**Applied only to disposable automated-test tenant databases** through the reused Stage 1 fixture chain. Those two randomly named LocalDB tenant databases have their own verified TenantDatabaseIdentity and are disposed by the fixtures. No `MicroEMR_LocalDev_Fresh`, persistent, shared or production migration/application write occurred.

The first test build targeted normal output and encountered DLLs locked by the already running Web process. It did not execute tests. Subsequent builds/tests used `.tmp/step218/artifacts` exclusively, restoring dependencies from the existing local NuGet cache. No service was stopped or restarted. Runtime activation of 0099 and the new compiled application remains outside this task.

## 11. Automated acceptance matrix

Existing Stage 1 validation/security/correction/output fixtures and renderer/browser harnesses were inspected and reused. New tests target previously absent directory behavior. Old suites are included only once in the final affected regression because the new migration/application transport affects their integration boundaries.

| Step 217 backend scenarios | New/reused passing evidence |
|---|---|
| 1–4 Create, duplicate/inactive ownership, case/Unicode identity | `ExactIdentityAndBoundaryLookupAreUnambiguous` (4 cases); lifecycle; API maintenance |
| 5–7 Pair/name/address/optional phone validation | `DirectoryValidationRejectsWithoutMutation` (5 cases); API optional phone/normalization; existing Stage 1 validators/coverage |
| 8–12 Details edit, immutable identity, deactivate/reactivate, RowVersion | `LifecycleImmutableIdentityConcurrencyAndCompleteAudit`; `ApiMaintenanceUsesTrustedActorAndExactResolution` |
| 13–14 Permission and tenant isolation | `ActiveSearchBoundedPagingLiteralQueriesTenantAndPermissionGuards`; API maintenance tenant check; invalid selection tenant case; Draft edit guards |
| 15–18 Pagination, active default, inactive maintenance, full details | Search/paging/filter assertions; exact-resolution and API detail retrieval |
| 19–20 Trusted audit/old-new snapshots/no rejected mutation | Lifecycle audit; `DirectoryAuditFailureRollsBackDetailsAndVersion`; invalid selection state/audit assertions |
| 21–24 Selection resolution, cross-tenant/inactive/stale rejection | `InvalidSelectionNeverMutatesPrescriptionOrAudit` (4 cases), both Create and Edit; valid selected-save tests |
| 25 Manual Stage 1 support | `SelectedSaveManualFallbackAndHistoryRemainIndependent`; prior Stage 1 coverage |
| 26–27 Confirmation and no silent overwrite | Production browser preview/cancel/optional-copy assertions; selected API save retains independent name/address |
| 28 Clinical audit preserved | Selected UID in complete clinical creation snapshot; Draft provenance preservation/clear; prior audit/security coverage |
| 29–31 Corrections, finalized/history preservation | Selected-save/history/correction test; full Detail distinction; prior finalized/correction/legacy artifact assertions |
| 32 PDF/Actions unchanged | Prior complete scope/layout and legacy-renderer comparisons in final regression |

| Frontend scenarios | Passing evidence |
|---|---|
| 1–3 Search/result selection/detail preview | `LookupPreviewCancelConfirmationManualAndStaleStates`, production Create/Edit |
| 4–6 Optional population, mismatch warning, cancel | Same browser cases; new mismatch Node cases |
| 7 Manual entry | Browser manual edit clears selection; form guidance; existing Stage 1 cases |
| 8 Stale/inactive/unavailable selection | Inactive/error browser assertions; `StaleSelectionSaveShowsActionableDirectoryMessage`; transactional SQL rejection |
| 9 Maintenance permission controls | Endpoint permission tests; `MaintenanceViewsEncodeDetailsAndArePermissionProtected`, including production Add/Edit/Index and antiforgery markup |
| 10–11 Responsive/accessibility | Browser Create at 360px / Edit at 1280px, no horizontal overflow, associated search label and live status; prior responsive Stage 1 cases |
| 12 Existing behavior/historical Detail | Stage 1 compatibility cases, Web binding cases, full Detail recorded-versus-unresolved display; prior regression |
| Late response / modal-record safety | Browser delayed-response completion after form-record load is discarded without restoring stale result/selection |

## 12. Focused execution receipts

Receipts are retained under ignored `.tmp/step218/`. Counts overlap; they are not summed across reruns.

| Receipt/run | Result |
|---|---|
| Initial normal-output focused attempt | Build blocked by running Web DLL locks; no acceptance tests executed |
| `directory-focused-isolated.trx` | **17/17 PASS** new backend cases |
| `directory-integration.trx` | **11/11 PASS**, including new browser/API cases and strengthened rejected-selection cases |
| `directory-strengthened.trx` | **9/9 PASS**, including new provenance/error/selection-shape cases plus affected prior assertions |
| `ui-final.log` | Interrupted isolated test run: a delayed fetch test stub returned an unresolved promise from Playwright evaluation; no product failure asserted |
| `directory-ui-retry.trx` | **4/4 PASS** after correcting that test stub, including late-response, production maintenance forms and selected-history checks |
| `directory-listing-isolation.trx` | **2/2 PASS** supplemental focused checks after final review strengthened existing cross-tenant listing assertions; product code unchanged, overlapping final regression cases |
| Unique new Stage 2 coverage, confirmed in final regression | **28/28 PASS: 20 backend + 8 Web/browser cases** |
| `lookup-node.log` | **3/3 PASS** new focused client cases |

Only the stalled isolated Step 218 testhost/runner was stopped. Web/API/Auth were untouched. A read-only check for disposable fixture databases created during that interrupted test window found none remaining. No persistent database cleanup was performed.

## 13. Final regression, build and TypeScript

| Final validation | Result |
|---|---|
| Single affected backend/view regression, `prescription-directory-regression.trx` | **473/473 PASS**, zero failed/skipped; prior 445 plus 28 new Stage 2 cases |
| Single frontend regression, `frontend-regression.log` | **59/59 PASS**, zero failed/skipped; prior 56 plus 3 new client cases |
| Single final affected build, `final-build.log` | **PASS — zero errors, zero warnings** |
| Project TypeScript validation, `typescript.log` | **PASS — `--noEmit`** |
| Final scope/whitespace/worktree preservation review | **PASS** |

Final build: `dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/step218/artifacts --no-restore`.

Final backend run used the retained Step 215 affected prescription filter plus `FullyQualifiedName~DispensingDirectory`, with `--artifacts-path .tmp/step218/artifacts --no-build --no-restore`. Exact filter is in `.tmp/step218/regression-filter.txt`. TypeScript validation used the existing local package with `tsc -p src/MicroEMR.Web/tsconfig.json --noEmit`. Only the new client module/map was emitted. Frontend regression ran the ten prescription Node test files once. No dependency was added and no application process restart was needed.

## 14. Files and remaining qualifications

| Area | Changed/added files |
|---|---|
| Database | `db/tenant-clinical/migrations/0099-dispensing-location-directory.sql`; tenant `manifest.json` |
| Application | `PatientPrescriptions/DispensingLocationDirectory.cs`; `PatientPrescriptionModels.cs`; reference type documentation in `PrescriptionDispensingLocationReference.cs` |
| Infrastructure | `PatientPrescriptions/DispensingLocationRepository.cs`; `PatientPrescriptionRepository.cs`; `DependencyInjection.cs` |
| API | New `Controllers/DispensingLocationsController.cs`; prescription controller typed selection-conflict handling |
| Web | New directory controller/API client, directory Index/Edit views; prescription form/Detail/controller; existing sidebar link and Program typed-client registration |
| Client | `prescription-location-lookup.ts` and generated `.js`/`.js.map` |
| Tests | `DispensingLocationDirectoryTests.cs`; `DispensingLocationDirectoryWebTests.cs`; `prescription-location-lookup.test.cjs`; one affected Stage 1 guidance assertion |
| Report | This report; pre-existing Step 217 report preserved byte-for-byte |

No uncovered automated requirement blocking the approved bounded Stage 2 implementation was identified. Automated evidence demonstrates local uniqueness, exact resolution, detail retrieval, permission/tenant controls, auditing, concurrency and historical preservation. It does not establish accuracy of an actual curated facility mapping, external authority, directory completeness/freshness or live persistent-runtime activation.

Retain authenticated live UI qualifications, recovery **CONDITIONAL**, earlier **six NU1900 warnings** qualification and Auth restart migration/seeding restrictions. The offline-cache, no-restore final build emitted no warnings; that does not resolve the earlier unavailable vulnerability-metadata qualification. The owner's earlier 0098 activation/application stability confirmation remains owner-reported historical evidence, not new runtime verification.

Preserve **DE09.034 and DE09.035 — SATISFIED — VERIFIED WITH QUALIFICATIONS**. **DE09.032 remains PARTIAL during this implementation step**. **PC07.01 remains PARTIAL** and inventory remains **7 mandatory IDs / 3 packages**. No external authoritative directory validation or pharmacy routing/transmission capability is claimed.

## 15. Recommended Step 219

Perform a bounded acceptance/activation review of this tenant-local directory implementation and its automated evidence. Confirm who curates actual directory content and the permitted source/namespace mappings, review recovery readiness, and obtain separate explicit authorization before applying 0099 to any persistent tenant or activating new binaries. Authenticated live maintenance/lookup visibility remains a separate qualified check after authorized activation. Do not decrement the parent inventory or mark DE09.032 satisfied from compilation alone, and do not repeat already passing mechanical save tests without a new affected requirement.
