# Step 191B — DE09.033 earliest pickup implementation

2026-10-26 (America/Toronto). **Implementation COMPLETE for the approved local slice.** Latest results: **162/162 distinct relevant .NET cases PASS**, including **20 pickup cases**; **18 distinct frontend cases PASS**; project TypeScript check and affected-project build PASS. The migration was applied **only to owner-authorized disposable test databases**. No deployed/shared/production database, clinical manual save, deployment or restart occurred. Manual clinical UI acceptance remains DEFERRED. **PC07.01 remains PARTIAL; inventory unchanged at 7 IDs / 3 packages.**

## 1. Scope and authority

Implemented the owner's Step 191B approval of [Step 191A](267-step191a-de09-033-earliest-pickup-contract.md): optional Day/Minute pickup precision, tenant clinical zone, retained offset, DST validation, independent omission/null semantics, audit/RowVersion, linked corrections with explicit review, and immutable original artifacts. DE09.033 remains the store-only expected pickup fact, not a dispensing restriction, pharmacy-readiness assertion or transmission obligation.

Before edits, targeted inspection confirmed `dbo.PatientPrescription`, existing Draft/Finalized/Cancelled/Superseded lifecycle, `SupersedesPrescriptionUid` correction mechanism, 0090 effective prescription procedures and `Tenant.DefaultTimeZoneId` through trusted tenant context/catalog. Existing immutable artifact/rendering and isolated SQL test patterns were reused. Neither correction support nor the timezone source required a parallel lifecycle or authorization relaxation.

The owner separately authorized application of the migration **only in disposable isolated test databases**. The existing fixtures generate `MicroEMR_Step87_<GUID>` names, initialize their own synthetic data and dispose those databases. No migration application to an existing development tenant is inferred from that authorization.

## 2. Changed files

All paths below are relative to the repository. No Git command was used; file review used targeted source inspection, migration comparison against 0090, test receipts and explicit affected-file checks.

| Area | Files | Change |
|---|---|---|
| Application | `src/MicroEMR.Application/PatientPrescriptions/EarliestPickup.cs` (new) | Precision-bearing value, strict JSON conversion, safe validation errors, trusted tenant-zone policy, valid-offset lookup and bounded logging. |
| Application | `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs` | Independent presence-aware request fact, response/detail/review flag, Application validation, correction inheritance and reviewed finalization boundary. |
| Application | `src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs`; `src/MicroEMR.Application/DependencyInjection.cs` | Review confirmation transport and policy registration. PDF composition unchanged. |
| Infrastructure | `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs`; `PatientPrescriptionOutputRepository.cs` in the same directory | Pickup parameter/read mapping and reviewed finalization calls through existing stored procedures. |
| API | `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs`; `PrescriptionPickupOptionsController.cs` (new) | Preserve patient-scoped clinical routes; forward explicit review; separate authenticated reference-only options endpoint with Patients.View and prescribing permissions. |
| Web transport | `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs`; `src/MicroEMR.Web/Services/PatientPrescriptions/PatientPrescriptionApiClient.cs` | Form presence mapping, modal/Edit serialization, options proxy, safe field feedback and review forwarding. |
| UI | `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml`; `Detail.cshtml`; `src/MicroEMR.Web/Views/Patients/Details.cshtml` | Small Bootstrap pickup subgroup and Detail display; explicit correction review checkbox beside the current pickup value during signing. No new navigation. |
| Client | `src/MicroEMR.Web/ClientApp/patients/prescription-pickup.ts` (new); emitted `src/MicroEMR.Web/wwwroot/dist/patients/prescription-pickup.js` and `.js.map` | Precision, clear/preserve/set state, server-provided offsets, ambiguous-time choice, asynchronous validation, modal/reset and validation-redisplay handling. |
| SQL | `db/tenant-clinical/migrations/0093-prescription-earliest-pickup.sql` (new); `db/tenant-clinical/manifest.json` | Additive nullable fact and validation; current reads/writes/finalization extended without rewriting historical scripts. |
| New tests | `tests/MicroEMR.Api.Tests/EarliestPickupTests.cs`; `tests/prescription-pickup.test.cjs` | Focused contract, Application/API/SQL/lifecycle/form/frontend coverage. |
| Existing test infrastructure | `tests/MicroEMR.Api.Tests/PrescriptionDatePrecisionTests.cs`; `PatientPrescriptionFoundationTests.cs`; `PrescriptionPrintTests.cs`; `PrescriptionRefillSqlTests.cs` | Optional test-host registration hook; repository-root discovery compatible with isolated artifacts; two correction scenarios use the existing current order-field fixture rather than historical 0084-only procedures. |
| Documentation | This report | Implementation evidence, safety/deployment boundary and remaining manual checklist. |

Existing manual/uncommitted work and the unrelated encounter `chronology.ts` were not edited. Temporary build/test receipts are under `.tmp/step191b/`.

## 3. SQL migration and rollback considerations

**0093** appends to the existing tenant manifest after 0092. It adds nullable `dbo.PatientPrescription.EarliestPickup` JSON storage, `dbo.PrescriptionPickupValid` and `CK_PatientPrescription_EarliestPickup`. Existing rows retain NULL; no inferred clinical values or artifact rewrites occur.

The following existing procedures are extended from their current 0090 definitions:

- `PatientPrescription_GetByPatientUid` / `GetByUid`: return pickup and computed `PickupReviewRequired`.
- `PatientPrescription_CreateDraft`: validates incoming shape and, for a correction, locks/re-reads and carries the exact original fact within the create transaction.
- `PatientPrescription_UpdateDraft`: changes pickup only when its independent specified flag is true. Existing patient, Draft-owner, active-provider and RowVersion controls remain.
- `PatientPrescription_Finalize` / `SignAndFinalize`: enforce correction review, freeze prospective pickup JSON/version/review evidence and retain existing transactional audit/supersession behavior.

No EF migration, historical migration edit, grant change or new clinical mutation path was introduced. Existing audit before/after JSON includes pickup automatically; finalization audit additionally records `EarliestPickupReviewed`. Both newly generated artifact forms carry the prospective marker and exact pickup fact; renderers ignore this store-only metadata.

**Application status:** applied successfully in disposable fixtures only, then disposed. Not applied to an existing development tenant, shared or production database. The feature needs an explicitly authorized migration/deployment plan before use against a deployed tenant. Existing reads/old payloads remain compatible; new pickup writes must not be used against unmigrated procedures.

**Rollback:** prefer a forward correction while preserving stored clinical values and audit. A code rollback may leave nullable storage and retained JSON in place; do not drop the new column, rewrite the manifest/history or strip artifacts to downgrade. Any database rollback after pickup records exist needs a separate reviewed retention/compatibility plan. No rollback script or database rollback was executed.

## 4. Precision, timezone and JSON behavior

Known values have exactly three canonical stored properties:

```json
{"Precision":"Day","Value":"2026-11-03","TimeZoneId":"America/Toronto"}
```

```json
{"Precision":"Minute","Value":"2026-11-01T01:30-04:00","TimeZoneId":"America/Toronto"}
```

Examples are illustrative. Date-only retains the calendar date with no invented midnight or UTC instant. Minute values retain the supplied local minute, numeric offset and named zone. UTC is derivable from the offset-bearing value; no redundant writable UTC column was added.

Application validation resolves the trusted selected tenant's configured zone and validates the local time on the entered date. Nonexistent DST times reject. Offset lookup returns both valid choices for repeated times, and the UI leaves the choice blank; an explicit valid offset is required. Unique times may select their sole server-derived offset. Wrong-zone/wrong-offset inputs reject. No workstation/browser/chat timezone or current-date offset supplies clinical meaning.

Strict shape/calendar/offset checks run before persistence; SQL additionally checks canonical structure and lexical precision before conversion. SQL requires a seconds component internally when converting a minute-bearing date-time, so it temporarily pads `:00` for validation only; the retained value remains the original minute-precision form. Named-zone/DST rules remain in Application, not falsely attributed to SQL structural validation. Unsupported partial dates, seconds/fractions, duplicate/unknown nested keys and incomplete objects reject.

Historical read/unrelated save preserves recorded zone/offset without recalculation. A new or explicitly replaced known fact requires the current tenant-zone policy. An inherited correction fact is copied as recorded even if the tenant default zone has changed. Missing zone configuration prevents new known entry without blocking omitted/unrelated edits. A safe ILogger warning covers unavailable zone configuration without clinical payloads.

## 5. Omission, null, UI and correction semantics

`EarliestPickupSpecified` and its JSON wrapper preserve omission independently of nullable value. Web forms carry preserve/set/clear operations; the typed internal API retains the same distinction; Infrastructure sends the specified bit and nullable JSON only when applicable.

- Omitted property: retain current value exactly.
- Explicit null: clear current value, retaining before/after audit history.
- Complete supplied object: validate and atomically replace.
- Malformed/empty object: reject; never reinterpret as clear.
- Legacy NULL: remains unknown, without backfill or a fabricated “never specified” provenance claim.

The editor offers Not recorded / Date only / Date and time, an explicit clear action, visible timezone/offset context and validation feedback. Detail preserves original precision and reports date-only time as unspecified. A modal-load revision prevents a queued reset or stale offset request from erasing newer data. Full-page validation redisplay retains the submitted operation instead of silently omitting it on retry.

Finalized orders cannot receive Draft updates. Corrections use the existing linked Draft: the original pickup fact is carried exactly; any later change/clear is an ordinary audited Draft edit. If the original or current correction has known pickup, both finalization procedures reject absent review. The normal signing UI shows the current value (or cleared state) with an explicit required review checkbox, and the confirmation is tied to the submitted RowVersion. Review appears in finalization audit and the new artifact. Original clinical facts/artifact bytes remain retained; the normal existing status/link change on supersession is preserved.

There is no new dispensing/transmission behavior, PDF row, CPP card field, navigation item, pharmacy lookup, regulatory date-order rule or inferred pickup default.

## 6. Verification and actual results

### Backend, SQL and build

The initial ordinary-output build encountered running-process DLL locks. Verification switched to `.tmp/step191b/artifacts` without stopping services. Sandboxed LocalDB could not initialize; the authorized outside-sandbox run used only generated disposable fixture databases. Focused tests caught SQL Server's minute-lexical conversion requirement; the validation-only padding fix was retested successfully.

| Receipt | Actual outcome |
|---|---|
| `pickup-focused.trx` | Initial focused final state: **16 PASS / 0 FAIL**. |
| `pickup-regression.trx` | One broader relevant run: **152 PASS / 10 FAIL**. Failures were historical test-root discovery, reference options placed in a patient-route-only reflection scope, and two old correction fixtures stopping before existing order fields. |
| `pickup-remedy.trx` | Targeted rerun after corrections: **43 PASS / 0 FAIL**, including all **20 pickup cases**, the previously failed cases and directly affected Web tests. |
| `latest-results.json` | Correlation by stable TRX test ID: **162 distinct .NET cases, all latest outcomes PASS**, zero unresolved failures/skips. Test IDs avoid collisions from truncated theory display names. This is not a claim of one 162-PASS execution. |
| `remedy.log` | Affected test project and referenced Core/Application/Infrastructure/DatabaseTool/API/Web build: **PASS, zero errors**. Six NU1900 warnings reflect unavailable NuGet vulnerability metadata; no new compiler warning remains. |

The new focused cases cover Day/Minute SQL/API round trips; exact zone/offset; DST gap, repeated time and non-hour offset; malformed/duplicate JSON; JSON/Web form presence; clear audit; stale RowVersion; owner/inactive/permission boundaries using retained checks; cross-patient and cross-tenant behavior; audit-failure rollback; SQL malformed-fact rejection; both finalization review gates; correction carry-forward after tenant-zone change; cleared correction review; and original/legacy artifact retention. Existing relevant signing/refill/permission/precision/Details/PDF tests were reused rather than recreated.

The test-root fix supports the isolated artifact layout. The two historical correction tests now explicitly use `Step143OrderDatabase`; their previous 0084-only fixture could not accept the existing `OrderFields` payload. Clinical rules were not changed to accommodate obsolete fixture procedures. Reference-only pickup options were moved to a separate thin controller, preserving patient-scoped route invariants.

### Client verification

Project TypeScript `--noEmit`: **PASS**. Only the new module/map were emitted using existing local compiler/runtime assets; no dependency/package change.

`frontend-regression.log`: **17 PASS**, covering the initial five pickup tests and 12 existing date/generic/private-note cases. `frontend-focused.log`: **6 PASS** after adding validation-redisplay coverage. Combined latest coverage: **18 distinct frontend cases PASS**, including explicit ambiguous offsets, DST feedback, clear, date-only precision, modal reset and full-page retry preservation. No new-screen browser acceptance is claimed from these Node/DOM fixtures.

## 7. Minimum manual acceptance — prepared, not executed

**Common prerequisites:** separately authorized local development migration/deployment/runtime scope; confirmed non-production tenant with resolvable `DefaultTimeZoneId`; ordinary existing authorized prescriber linked to an active provider; existing suitable synthetic patient and owned Draft with reviewed RowVersion. Reuse fixtures; do not create accounts, grant permissions or make clinical saves under this report. Finalization/correction scenarios require explicitly authorized synthetic order fixtures. External correspondence remains deferred.

Use the existing patient Medications → prescription Create/Edit/Detail workflow. No new navigation is required.

| Scenario | UI steps / expected result | PASS / FAIL / DEFERRED |
|---|---|---|
| M1: changed controls and usability | At desktop and one narrow viewport, choose Date only, enter a date, save/reopen and compare Detail; replace with Date and time, verify clinical zone/offset and save/reopen; clear and verify Not recorded. Check labels, keyboard use and error readability. Date-only must never shift or acquire time. | PASS only with authorized actual UI/read correlation. FAIL on loss/shift/unclear state or unrelated field change. **Currently DEFERRED.** |
| M2: ambiguous-time usability, only if the clinical zone has a transition | Use a known fall-back repeated local minute in that zone, confirm no automatic offset choice, select an occurrence and verify displayed offset after save. A spring gap must show a validation error without mutation. | PASS with deliberate occurrence choice and matching read value; FAIL on silent selection/shift. DEFERRED if prerequisites/authorization absent; not applicable for a zone without transitions, with automated transition evidence retained. **Currently DEFERRED.** |
| M3: correction-review usability | With a separately approved finalized synthetic order, create correction, inspect carried precision/zone/offset, edit or clear deliberately, and verify review is required before signing. Compare retained original artifact reference/hash before and after. | PASS only with authorized lifecycle scope and unchanged original facts/artifact; FAIL on bypass/history change. **Currently DEFERRED.** |

### Schema-verified SELECT-only verification queries

Column/object references were checked against 0050, 0090, the new 0093 and `db/initial.sql` AuditLog definitions; equivalent reads are exercised in disposable tests. These queries were **not executed against a deployed tenant**. Use parameterized `@PatientUid` / `@PrescriptionUid` from the approved fixture. No write, dynamic SQL, guessed identifiers or live credentials are included.

Prerequisite check before any query using the new column:

```sql
SELECT DB_NAME() AS SelectedDatabase,
       COL_LENGTH(N'dbo.PatientPrescription', N'EarliestPickup') AS PickupColumnLength;
```

After authorized migration, current fact and concurrency:

```sql
SELECT p.PrescriptionUid, p.PatientUid, p.Status, p.PrescriberUserId,
       p.EarliestPickup, p.RowVersion, p.SupersedesPrescriptionUid,
       p.SupersededByPrescriptionUid, p.ArtifactUid
FROM dbo.PatientPrescription AS p
WHERE p.PatientUid = @PatientUid AND p.PrescriptionUid = @PrescriptionUid;
```

Audit correlation, including clear or finalization review:

```sql
SELECT a.AuditLogId, a.UserId, a.ActionName, a.CreatedAt,
       a.OldValue, a.NewValue
FROM dbo.AuditLog AS a
JOIN dbo.PatientPrescription AS p
  ON a.EntityId = CONVERT(nvarchar(100), p.PrescriptionUid)
JOIN dbo.Patient AS patient ON patient.PatientUid = p.PatientUid
WHERE p.PatientUid = @PatientUid AND p.PrescriptionUid = @PrescriptionUid
  AND a.EntityName = N'PatientPrescription' AND a.PatientId = patient.PatientId
ORDER BY a.AuditLogId;
```

Retained artifact fingerprint, using the **original** order parameters for before/after comparison:

```sql
SELECT a.ArtifactUid, a.PrescriptionUid, a.CreatedAtUtc,
       CONVERT(varchar(64), HASHBYTES('SHA2_256',
           CONVERT(varbinary(max), a.ArtifactJson)), 2) AS ArtifactJsonSha256
FROM dbo.PatientPrescriptionArtifact AS a
WHERE a.PatientUid = @PatientUid AND a.PrescriptionUid = @PrescriptionUid;
```

Missing approved tenant/fixture, unapplied migration or unavailable session is DEFERRED, not PASS. A mismatch or unauthorized mutation is FAIL and stops acceptance. Do not substitute an administrator or broaden permissions to make the checks pass.

## 8. Limitations, remaining gaps and Step 192

No deployed acceptance, physical-device validation or clinician usability approval is claimed. SQL enforces shape/offset consistency; Application supplies named-zone/DST semantics. The native input supports Day/Minute only and one trusted tenant clinical zone for new facts. Historical captured context is retained; destination-specific timezone/import/transmission needs remain outside this slice. Pickup remains optional and does not establish stock, pharmacy readiness, actual dispensing or a regulatory restriction.

**Recommended Step 192:** controlled **local development** migration/deployment readiness followed by the minimal authorized clinician UI checks above, with existing synthetic fixtures and selective evidence. Review the additive script, tenant identity, backup/retention and application/schema compatibility before any actual tenant migration. Obtain explicit runtime/clinical-save scope separately. No production/shared migration, service restart or external contact is authorized by Step 191B.

PC07.01 remains PARTIAL: source-backed DIN, applicable destination/reference metadata, broader authority applicability and complete applicable source/ownership/retrieval/audience mapping remain. PC04.01 and PC04.14 stay unresolved/deferred. **Inventory remains Prescribing PC04.01/.06/.07/.09/.14; CPP PC07.01; Scheduling PC09.03 — 7 IDs / 3 packages.**

Final scope review found only the listed implementation/test/documentation assets and temporary receipts. No Git operations, feature branch, GitHub operation, production/shared database access/change, existing-tenant migration, deployment/restart, manual clinical save or external correspondence/research occurred. Stop after implementation, verification and documentation.
