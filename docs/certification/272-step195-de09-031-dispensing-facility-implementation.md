# Step 195 — DE09.031 target dispensing facility name implementation

2026-10-26 (America/Toronto). **Implementation COMPLETE for the approved supplied-name slice.** Focused results: **37 backend cases PASS / 6 frontend cases PASS**. Final relevant regression: **199 backend cases PASS / 24 frontend cases PASS**, including those focused cases; affected-project build and TypeScript PASS. Manual/deployed acceptance remains DEFERRED. **Migration 0094 was created and validated only in authorized disposable test databases; no deployed development/shared/production database was migrated. PC07.01 remains PARTIAL; inventory remains 7 IDs / 3 packages.**

## 1. Implemented scope and reused boundaries

Implemented the owner's approval of [Step 194](271-step194-de09-031-dispensing-facility-contract.md), including the Step 195 explicit correction-review requirement. DE09.031 is the optional manually supplied name of the intended dispensing location, mandatory to support and store-only under CDS-S v2.1 row 157. It neither verifies identity nor replaces DE09.032's identifier/cross-checking capability.

Reused [Step 191B](268-step191b-de09-033-earliest-pickup-implementation.md)'s presence, tenant-bound repository, clinical actor, stored-procedure, audit, RowVersion, correction and artifact conventions. [DE09.033's qualified closure](270-step193-de09-033-acceptance-closure.md) remains intact. No external lookup, pharmacy directory, address/identifier integration or new correction lifecycle was added.

Pre-edit inspection confirmed the actual sequence ended at 0093, current `PatientPrescription` DTO/service/repository and 0093 procedures, existing Create/Edit/modal/Detail paths, linked correction workflow and disposable fixtures. No clinical invariant blocked the change. The latest instruction's explicit review enforcement is implemented in the existing Draft save/finalization path, with its controls confined to Create/Edit; no new list-action control was introduced.

## 2. Files changed

| Area | Files | Purpose |
|---|---|---|
| Application | `src/MicroEMR.Application/PatientPrescriptions/TargetDispensingFacilityName.cs` (new) | Explicit Unicode/space/length validation and safe facility validation/review exceptions. |
| Application | `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs` | Presence-aware name request, nullable response, review/presentation flags, correction inheritance and Application normalization. |
| Infrastructure | `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs` | Wide SQL parameters, independent specified/review flags, optional legacy read mapping and safe errors. |
| API | `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs` | Safe facility validation/review responses within existing authenticated patient-scoped routes. |
| Web | `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs` | Preserve/set/clear form transport, Edit/modal mapping, review presentation and safe feedback. |
| UI | `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml`, `Detail.cshtml` | Optional name field/help/clear/review in existing editor; encoded, wrapping full Detail display with Not recorded. |
| Client | `src/MicroEMR.Web/ClientApp/patients/prescription-facility-name.ts` (new), emitted `wwwroot/dist/patients/prescription-facility-name.js` and `.js.map` under the Web project | Modal/reset/presence/review handling and non-truncating validation. Explicit module scope avoids TypeScript global-name collisions. |
| SQL | `db/tenant-clinical/migrations/0094-prescription-target-facility-name.sql` (new), `db/tenant-clinical/manifest.json` | Dedicated nullable name/review storage, canonical validation, existing read/write/finalization extensions. |
| Backend/UI tests | `tests/MicroEMR.Api.Tests/TargetDispensingFacilityNameTests.cs`, `TargetFacilityNameWebTests.cs` (new) | Contract, API/SQL/lifecycle/security and rendered production-view/form acceptance. |
| Frontend tests | `tests/prescription-facility-name.test.cjs` (new) | Actual emitted module exercised in DOM fixtures. |
| Documentation | This report | Evidence, migration safety, manual acceptance and Step 196. |

Seven existing files were changed; new implementation/test assets plus this report account for the remainder. No existing test file needed modification. Before/after file comparison and preservation hashes confirmed that **prescription list Actions (`Views/Patients/Details.cshtml`), encounter `chronology.ts`, prescription PDF output service and migration 0093 are unchanged**. No unrelated cleanup or dependency change occurred. Receipts/backups are under `.tmp/step195/`.

## 3. Migration and application status

After checking both manifest and migration directory, the next available ID was **0094-prescription-target-facility-name**. It appends once after 0093; no existing script/history was edited. CanonicalV2 SHA-256:

`7FA09B20F5704679309333787158BA733E877BD4FB8AED9CB7AE92B818EA9EF3`

Additions to `dbo.PatientPrescription`:

- `TargetDispensingFacilityName NVARCHAR(200) NULL`.
- `FacilityNameReviewed BIT NULL`, used for explicit correction review. NULL legacy state requires no backfill.
- `CK_PatientPrescription_TargetFacilityName`, plus `PrescriptionFacilityNameNormalize` / `PrescriptionFacilityNameValid` functions.

The script extends the existing `PatientPrescription_GetByPatientUid`, `GetByUid`, `CreateDraft`, `UpdateDraft`, `Finalize` and `SignAndFinalize` definitions from 0093. It retains the pickup behavior, audit transaction, patient/owner/state/RowVersion predicates and existing supersession handling.

New write parameters are defaulted for old clients. Name input is `NVARCHAR(MAX)` and validated/normalized **before** assignment to `NVARCHAR(200)`, preventing parameter-level truncation. Correction creation locks and copies the actual original name, ignoring any attempt to substitute an inherited value. New artifacts capture name/version/review metadata prospectively; no old artifact is rewritten.

**Applied only in disposable LocalDB fixture databases** through the established test setup, as explicitly authorized. Tests use generated disposable database names and dispose them. No existing development tenant, shared or production database was accessed or changed by this task. No live migration-state claim is made from test application. New facility writes require an appropriately migrated runtime/database before deployed use.

The raw script is not independently rerunnable because ADD column/constraint is additive. Use the established controlled runner/ledger/hash/transaction process in any separately authorized deployment; never manually repeat a partially applied script or overwrite tracking history. Pre-commit failure can roll back through that runner transaction. After clinical writes, prefer a forward correction; retain columns, audit and artifacts rather than dropping clinical facts to downgrade. No migration/rollback command against a deployed tenant was executed.

## 4. Data and validation behavior

The field is an independent order fact, not an extension of `OrderFields` and not a pharmacy identity. Duplicate names are permitted without a unique index, deduplication or cross-record matching.

Application and SQL implement the approved rules:

1. Accept string/null; reject invalid JSON types and duplicate submitted name properties. Invalid escaped Unicode rejects rather than becoming a successful value.
2. Reject C0/C1 controls, TAB/CR/LF/NUL and U+2028/U+2029 **before trimming**. Reject unpaired UTF-16 surrogates.
3. Trim only the approved boundary spacing set: U+0020, U+00A0, U+1680, U+2000–U+200A, U+202F, U+205F and U+3000. Preserve interior spacing, case, accents, combining sequences and valid supplementary Unicode.
4. Normalize empty/boundary-space-only input to NULL. A control-only input still rejects; it is not silently cleared.
5. Enforce **200 normalized UTF-16 code units** without truncation. Tests include 100 supplementary symbols (200 units), padded boundary spaces and overlength rejection through the actual procedure boundary.
6. SQL checks count storage units with `DATALENGTH` and explicit non-SC binary-collation iteration, not character-count assumptions. The constraint explicitly excludes whitespace-only noncanonical stored values rather than allowing SQL CHECK's UNKNOWN result for a NULL normalization.

All inputs remain parameterized and output is Razor-encoded. Apostrophes, ampersands and markup-like text are preserved as text rather than interpreted or unnecessarily stripped. The new field is not written to diagnostic logs. Existing logging/error boundaries are retained; no new logging framework was introduced merely for validation.

## 5. Omission, clear and UI behavior

`TargetDispensingFacilityNameSpecified` and the JSON wrapper distinguish omission from a present nullable value. Web form preserve/set/clear state survives internal serialization and stored-procedure parameters.

| Supplied state | Result |
|---|---|
| Property omitted | Existing name remains unchanged; new order unknown |
| Explicit null | Audited clear |
| Empty/approved-space-only string | Normalize to NULL; audited clear |
| Valid text | Normalize and replace with audit |
| Invalid type/control/Unicode/length | Reject before clinical mutation |

Legacy records remain NULL; no names are inferred from notes, clinic/provider/patient information or other orders. NULL is Not recorded, not a manufactured claim that a name was never previously known; audit retains replacement/clear history.

Create/Edit and the existing modal contain **Target Dispensing Facility Name (optional)**, length guidance, source-qualification help, clear action and validation feedback. The name is loaded during editing; unchanged form state preserves it. Client validation does not use `maxlength` to silently cut pasted text. Full-page validation redisplay retries the submitted operation; deferred resets cannot erase newer modal data or leak review state between orders.

Full Detail shows the encoded name or **Not recorded**, with wrapping for long names. No new page/navigation, prescription list Actions content, CPP projection, PDF/print row or transmission behavior was added. The existing typed API client serializes the extended DTO without modification.

## 6. Correction, review, audit and immutability

The existing linked Draft mechanism remains the only correction workflow. The new Draft carries the exact stored source name and starts unreviewed when a known name is inherited. The source may have a different prescriber; the new Draft retains the actual correcting actor/owner according to existing rules.

Explicit review is recorded by checking the field's **review checkbox in Edit and saving the Draft with its current RowVersion**. Both finalization procedures enforce review when the original or current correction has a known facility name, including a deliberate clear of an originally known name. A name change/clear invalidates prior review unless that same version's save explicitly reviews it. Unrelated saves preserve existing review. No list-action change or parallel signing/correction mechanism was necessary.

The UI's correction/review-required flags are presentation metadata; SQL derives applicability from the actual order/source, not client claims. Supplying review on correction creation does not bypass the initially unreviewed source inheritance. Name review does not satisfy pickup review, and pickup review does not satisfy name review.

Create/update/clear/review use existing transactional before/after `AuditLog` records with trusted actor and clinical scope. Audit failure rolls back clinical data and RowVersion. Finalization freezes name and review in prospective JSON; old artifacts and original name remain unchanged. Normal supersession can change original status/link/RowVersion, as already established; this is distinct from overwriting original clinical facts/artifact bytes.

## 7. Automated evidence and build results

| Check / receipt in `.tmp/step195/` | Actual result |
|---|---|
| `facility-focused.trx`, `focused.log` | **37/37 backend PASS**, zero final failures/skips |
| `frontend-focused.log` | **6/6 frontend PASS** |
| `facility-regression.trx`, `regression.log` | **199/199 relevant backend PASS**, zero failures/skips; one broader relevant run |
| `frontend-regression.log` | **24/24 frontend PASS** across new name and retained pickup/date/generic/private-note modules |
| `typescript.log` | Project `tsc --noEmit` **PASS**; only new module/map emitted |
| Affected project build | **PASS**, test project and referenced Core/Application/Infrastructure/DatabaseTool/API/Web compiled using isolated artifact output; zero errors |
| `facility-ui-final.trx`, `ui-final.log` | **7/7 targeted Web PASS** after adding long-name Detail wrapping; final build PASS |
| Warnings | **Six NU1900 warnings** for unavailable NuGet vulnerability metadata, separate from test/compiler failures; no new compiler warning |

Focused counts are **included** in the regression totals, not added to them. Rendered production Create/Edit/Detail checks verify encoded content and new controls; form tests verify presence mapping and exact modal name/review data. Node fixtures execute emitted JavaScript for Unicode/length, clear, correction-review invalidation, modal reset and validation redisplay. No live/new-feature manual browser save is claimed.

Security/lifecycle cases exercise unauthorized, wrong-owner, cross-patient/tenant and stale writes; malformed/overlength API/procedure rejection without mutation; audit rollback; both finalization review gates; carried/replaced/cleared names and legacy original artifact retention. Existing prescription/pickup regression covers adjacent accepted behavior without recreating its tests.

Development failures were resolved, not relabelled: surrogate literals in test attribute metadata were replaced with runtime-constructed invalid-code-unit fixtures; the isolated Razor renderer needed controller route metadata for `_Form` resolution; a maxlength assertion was narrowed to the new field rather than existing product controls. Project TypeScript validation caught a global-name collision, resolved by declaring the new file as a module. Review also tightened the SQL CHECK's whitespace-only/NULL edge case, with negative-write evidence. No existing clinical rules or permissions were weakened to make tests pass.

## 8. Minimal manual acceptance — prepared, DEFERRED

**Prerequisites:** separately authorized local-development migration/runtime/clinical-save scope; recovery plan; confirmed `local-dev-fresh` tenant and current compatible Web/API/schema. Reuse an existing active permitted prescriber and an owned synthetic Draft. Earlier readiness evidence identified patient `9f65d4ca-c347-406a-bb32-c709a8941f80` and actor 2 as candidate context; **current eligible prescription UID/state/RowVersion must be freshly selected**, not assumed from old receipts. If no suitable fixture exists, defer and scope any creation separately. No manual save or new live fixture was performed here.

| Scenario | Minimal normal UI steps / expected result | Disposition |
|---|---|---|
| A — enter/reopen | Patient Medications → existing Draft Edit; enter `Maple Community Pharmacy — Main Street`; Save Draft, reopen and inspect full Detail. Same recorded name; no verified-identity claim. | PASS only with actual normal-session correlation; FAIL on loss or unrelated fact change; currently **DEFERRED**. |
| B — replace/clear | Replace with another meaningful name and save/reopen; use Clear name, Save Draft, reopen/Detail → Not recorded. Existing pickup/other order facts retain their values. | PASS on persistence and explicit clear; FAIL on silent truncation/clearing other fields; **DEFERRED**. |
| C — Unicode/narrow window | Combine with replacement in B using `Pharmacie Étoile 薬局`; verify exact accents/script and readable field/help/Detail at desktop and one narrow viewport. | PASS on safe readable text and save/reopen; FAIL on corruption/layout overflow; **DEFERRED**. |
| D — scope | Confirm the name appears in full Detail and no new name/help appears in list Actions. Existing PDF layout remains unchanged; retained artifact query below can correlate approved originals. | PASS within observed scope; do not sign an order merely to repeat automated PDF/lifecycle evidence. **DEFERRED**. |

Use automated evidence for hostile/stale/correction/audit scenarios unless a specific live gap emerges. If the selected Draft is a correction with a known source/current name, the review checkbox must be used and saved before any separately authorized finalization. Routine A–D do not authorize signing, correction creation, permission changes or retrospective fixture cleanup.

### Schema-verified SELECT-only verification

References below match actual inspected schema definitions and the migration exercised in disposable databases. **Not executed on a deployed tenant.** Preflight existing identity/ledger first; post-0094 field queries are valid only after authorized application. Bind actual approved parameters, never guessed IDs.

```sql
SELECT DB_NAME() AS DatabaseName,TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,ScriptHash,AppliedAt FROM dbo.SchemaMigration
WHERE MigrationId IN(N'0093-prescription-earliest-pickup',N'0094-prescription-target-facility-name');
SELECT COL_LENGTH(N'dbo.PatientPrescription',N'TargetDispensingFacilityName') AS NameColumnBytes;
```

Expected new column size is 400 bytes. After migration, select the owned candidate and compare the exact saved value:

```sql
SELECT PrescriptionUid,PatientUid,Status,PrescriberUserId,
       TargetDispensingFacilityName,FacilityNameReviewed,
       DATALENGTH(TargetDispensingFacilityName)/2 AS NameUtf16Units,
       SupersedesPrescriptionUid,ArtifactUid,
       CONVERT(varchar(18),CONVERT(varbinary(8),RowVersion),1) AS RowVersionHex
FROM dbo.PatientPrescription
WHERE PatientUid=@PatientUid AND PrescriberUserId=@ActorUserId AND Status=N'Draft';
```

Audit and retained original artifact checks:

```sql
SELECT a.AuditLogId,a.UserId,a.ActionName,a.CreatedAt,a.OldValue,a.NewValue
FROM dbo.AuditLog a
JOIN dbo.PatientPrescription p ON a.EntityId=CONVERT(nvarchar(100),p.PrescriptionUid)
JOIN dbo.Patient patient ON patient.PatientUid=p.PatientUid AND patient.PatientId=a.PatientId
WHERE p.PatientUid=@PatientUid AND p.PrescriptionUid=@PrescriptionUid
  AND a.EntityName=N'PatientPrescription'
ORDER BY a.AuditLogId;

SELECT ArtifactUid,PrescriptionUid,
       CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),ArtifactJson)),2) AS ArtifactJsonSha256
FROM dbo.PatientPrescriptionArtifact
WHERE PatientUid=@PatientUid AND PrescriptionUid=@OriginalPrescriptionUid;
```

Capture a fresh baseline before approved actions. Unknown runtime/schema/fixture, absent authorization or unavailable credentials is DEFERRED, not PASS. Preserve historical data; do not physically delete test clinical records to recover.

## 9. Limitations and Step 196

The name remains supplied text, not a pharmacy registry identity, DE09.032 substitute, lookup result, routed prescription or actual dispensing event. DE09.030 address/DE09.032 identity, DIN/source work, coded references and other retained parent residuals remain outside scope. No new field-level certification closure is claimed before appropriate acceptance.

**Proposed Step 196:** local-development facility-name migration/runtime acceptance readiness, followed only under separate authorization by minimal A–D UI verification. Confirm actual pending set/hash, database/tenant, recoverability, appropriate runtime and exact existing fixture; apply no migration by assumption. External correspondence/source work stays deferred.

PC07.01 remains **PARTIAL**; mandatory inventory remains Prescribing PC04.01/.06/.07/.09/.14, CPP PC07.01, Scheduling PC09.03 — **7 IDs / 3 packages**. DE09.033's accepted qualified closure and list layout remain preserved.

Final review compared the seven existing changed files with preserved local baselines and rechecked protected-file hashes. No Git operation, feature branch, GitHub operation, deployment/restart, deployed-tenant migration, manual clinical save, unrelated source change, external correspondence or directory/source investigation occurred. Stop after implementation, verification and documentation.
