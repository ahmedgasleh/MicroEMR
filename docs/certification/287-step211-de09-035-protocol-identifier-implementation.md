# Step 211 — PC07.01 / DE09.035 Protocol Identifier implementation

2026-10-27 (America/Toronto). **Local implementation COMPLETE; automated acceptance PASS. 46 protocol backend/view cases and 8 focused frontend cases PASS; one selected backend regression 395/395 PASS and one frontend regression 48/48 PASS. Build/TypeScript PASS. Migration 0097 executed only in disposable test databases; persistent activation not performed. PC07.01 PARTIAL; inventory 7 IDs / 3 packages unchanged.**

## 1. Authority, branch and preservation

Implemented the explicit Step 211 owner-approved bounded contract, reusing [Step 210](286-step210-pc07-01-next-element-assessment.md), [Step 204](281-step204-de09-034-reason-code-implementation.md) and [Step 206](283-step206-de09-034-provenance-and-audit-evidence.md). Exact dictionary definition:

> A unique identifier for a specific protocol or guideline which the prescription has been written in accordance with. Enables the communication of a reference to a protocol, study or guideline id that is specific to the jurisdiction.

Mandatory support/storage; terminology conformance unspecified. Namespace/identifier capture does not verify approval, clinical validity, currency or compliance with protocol content. No registry, lookup, guideline checker, external service, new endpoint, PDF/transmission change or protocol version/display field was added.

Preflight branch was `feature/step-206-de09-034-audit-evidence`, HEAD `2b68346aca416a14c4bc91489b65ba07d615d0de`. The required Step 211 branch did not exist. Before source/SQL/test edits, recorded existing work and hashes, then safely created and verified **`feature/step-211-de09-035-protocol-identifier`** at the same commit. Existing Step 206 audit-test changes and untracked reports 282–286 remain uncommitted and byte-identical: **6/6 pre-existing file hashes preserved**. Their presence on the current worktree is not attribution to Step 211.

No staging, commit, push/pull/fetch, merge, reset, clean, stash or unrelated ref change occurred. Before editing seven shared implementation/manifest files, retained their pre-step bytes under ignored `.tmp/step211/before/`. No running service was stopped/started/restarted, no normal launch binary was replaced, no persistent database was queried/modified, and no clinical/manual save, deployment or backup/restore occurred.

## 2. Files changed

| File | Step 211 change |
|---|---|
| `src/MicroEMR.Application/PatientPrescriptions/PrescriptionProtocolReference.cs` | New pair DTO, strict JSON converter, Unicode/length normalization and safe validation/review exceptions |
| `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs` | Presence-aware request, response/review fields, Application validation, exact correction mapping |
| `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs` | Wide stored-procedure parameters, optional read mapping and safe SQL-error translation |
| `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs` | Thin validation/review responses |
| `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs` | Form operation binding, Edit/modal population and safe protocol error display |
| `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml` | Grouped Bootstrap controls, explicit clear and independent correction review |
| `src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml` | Encoded full Detail / Not recorded section |
| `src/MicroEMR.Web/ClientApp/patients/prescription-protocol-reference.ts` | Pair/Unicode/boundary validation, load/reset/clear and review behavior |
| `src/MicroEMR.Web/wwwroot/dist/patients/prescription-protocol-reference.js`, `.js.map` | Only the new module's emitted assets |
| `db/tenant-clinical/migrations/0097-prescription-protocol-reference.sql` | Additive storage/CHECK, protocol validator and six extended prescription procedures |
| `db/tenant-clinical/manifest.json` | Append 0097 after verified 0096; no historical entry changed |
| `tests/MicroEMR.Api.Tests/PrescriptionProtocolReferenceTests.cs` | Protocol contract and disposable SQL/API tests, existing fixture composition |
| `tests/MicroEMR.Api.Tests/PrescriptionProtocolReferenceWebTests.cs` | Production Razor/Chromium, binding and modal JSON tests |
| `tests/MicroEMR.Api.Tests/PrescriptionProtocolReferenceScopeTests.cs` | Protocol-specific existing Actions and PDF/legacy-output comparisons |
| `tests/prescription-protocol-reference.test.cjs` | Eight focused cases using existing Node fake-DOM architecture |
| This report | Contract, coverage, receipts, qualifications and Step 212 recommendation |

No new framework/package dependency, EF migration, navigation, prescription-list Actions source or PDF renderer change. Existing DE09.034 assertions were neither modified nor removed; the pre-existing strengthened audit-test file retains its exact preflight hash.

## 3. Approved contract, validation and atomic semantics

Canonical payload on existing prescription Create/Draft-update routes:

```json
{
  "protocolReference": {
    "system": "jurisdiction-namespace",
    "identifier": "protocol-123"
  }
}
```

These conceptual values are not an approved protocol catalogue. `system` declares the namespace/source/jurisdiction; `identifier` remains opaque within it. Multiple prescriptions can reference the same pair; identical identifiers in different systems remain distinct.

| Component | SQL | Limit |
|---|---|---|
| ProtocolSystem | nullable `nvarchar(200)` | 200 UTF-16 code units after boundary trimming |
| ProtocolIdentifier | nullable `nvarchar(200)` | 200 UTF-16 code units after boundary trimming |
| ProtocolReferenceReviewed | nullable `bit` | Independent saved correction review metadata |

The pair is either entirely null or entirely populated. Boundary trimming uses the established explicit space set: U+0020, U+00A0, U+1680, U+2000–200A, U+202F, U+205F, U+3000. Controls C0/C1, CR/LF, U+2028/U+2029 and malformed UTF-16 reject before trimming. Valid Unicode, supplementary characters, case, punctuation and internal spacing are preserved. No case folding, Unicode normalization, guessed URI syntax or truncation occurs.

| Request state | Create | Existing Draft update |
|---|---|---|
| Entire group omitted | Unknown/no protocol | Preserve both components |
| Explicit null | No protocol | Atomically clear both |
| Complete valid object | Store normalized pair | Atomically replace both |
| Empty/partial/blank/invalid group | Reject | Reject without partial mutation |

JSON rejects nonobjects, nontext components, duplicate/unknown members and malformed Unicode. No version/display component is accepted. Component names follow the existing case-insensitive Application reader with canonical lowercase emission; internal SQL JSON accepts canonical keys. Empty objects are not clear commands. UI text erasure alone does not silently clear stored data; the explicit whole-group clear action does.

Existing records stay null with no backfill. Never recorded/legacy/cleared values display Not recorded; audit distinguishes explicit changes. API response/read mapping tolerates older procedure projections. When an original correction source has no protocol, the Application omits new protocol parameters, preserving legacy procedure-fixture compatibility. Protocol-bearing writes require 0097; this is not permission to activate against an older persistent schema.

## 4. Migration and stored procedures

Verified repository sequence ended at **0096** before creation. Added **0097-prescription-protocol-reference** and appended its manifest entry. Normalized SHA-256:

```text
750FA7AB0AB1111F3D68A9EDDE350BF7CEB90E0C067F561539B3AB824F46985B
```

Migration adds the three nullable columns and `CK_PatientPrescription_ProtocolReference`. The new `PrescriptionProtocolReferenceValid` helper enforces strict two-member JSON and independent 200-unit limits. It reuses unchanged 0096 single-line text normalization/validation helpers; no DE09.034 helper behavior was altered. Wide inputs are checked before bounded assignment, preventing truncation. No uniqueness constraint incorrectly limits protocol reuse across prescriptions.

Six existing definitions are carried forward from 0096 with only protocol extensions:

- `PatientPrescription_GetByPatientUid`, `PatientPrescription_GetByUid`: pair and independent review projection.
- `PatientPrescription_CreateDraft`: validated atomic storage and locked source inheritance.
- `PatientPrescription_UpdateDraft`: omission/replacement/clear, exact binary change detection and review state under existing locks.
- `PatientPrescription_Finalize`, `PatientPrescription_SignAndFinalize`: independent persisted review gate and prospective snapshot fields.

New Create/Update parameters have optional defaults. Errors **53026** (invalid group) and **53027** (missing saved review) map to safe Application/API responses. Original procedure safeguards, reason/address/name/pickup handling and audit transactions remain.

**Application scope: disposable test databases only.** Existing UUID-named LocalDB fixtures create their isolated databases and apply 0097 through existing batch conventions. No migration runner/application targeted `MicroEMR_LocalDev_Fresh`, any other persistent/shared database or production. The existing runner remains responsible for future sequencing/history/hash guards; 0097 is not advertised as a standalone rerunnable script or rollback.

## 5. Audit, concurrency, security and historical records

Existing transactional audit snapshots include both protocol components and saved review metadata. Tests assert complete populated create/new and replacement old/new pairs, clear old values and all-null new values, trusted clinical actor, patient/prescription association and absence from the other tenant fixture's audit database. Spoofed body actor/tenant/patient fields do not override trusted request context.

Invalid API/raw SQL requests leave the pair/RowVersion unchanged and append no successful prescription-change event. Missing permission, wrong prescriber, wrong patient/tenant and stale RowVersion reject or remain unavailable. Injected audit failure rolls back values and token. Existing prescribing permission, actor resolution, patient/tenant SQL connections and antiforgery remain intact.

Correction Drafts carry the exact pair under the source lock, without lookup, refresh or reinterpretation. Known source/current protocol requires **independent saved protocol review**. Other-field review, including Reason Code review, cannot satisfy it. Changing a normalized component or clearing invalidates saved review unless that Draft save explicitly acknowledges the current pair. Unrelated saves preserve review; clearing a known source retains the review obligation. Both finalizers enforce the persisted gate.

Finalized direct edits reject. Changes use the existing linked correction lifecycle. Original coding/artifact records and prior signed JSON remain unchanged; normal audited supersession metadata follows existing behavior. Prospective finalization snapshots contain the exact `ProtocolReference`, saved review and `ProtocolReferenceVersion=1`. This last marker is an **artifact schema marker**, not a clinical protocol-version field. Renderers ignore it, preserving existing PDF content. Existing artifact JSON equality and legacy output-byte/layout comparisons supply the bounded preservation evidence; no persistent PDF file was touched.

## 6. UI and transport

Existing Bootstrap fieldset contains **Protocol system / namespace** and **Protocol identifier**, supplied-reference guidance, whole-group clear and independent correction review. No maxlength truncation is used. Labels wrap their inputs and guidance is associated with both controls. Full Detail safely encodes both values and wraps long text; absent group shows Not recorded.

Create/Edit share `_Form`; the existing patient-chart prescription modal also renders this partial through `Html.PartialAsync`, so it receives the grouped controls without an Actions/source redesign. Edit JSON carries the exact pair and review state. The TypeScript module listens to the existing load event, resets absent values/review, and protects newer modal loads from delayed reset callbacks. Protocol clear leaves Reason Code and Indication controls unchanged.

Production Razor plus headless Chromium tests measure 1280×900 and 360×900 layouts with long Unicode/script-like values, blocking all network/application requests. These are isolated automated visibility/encoding/layout checks, not authenticated live persistent-tenant acceptance or universal-device usability approval.

## 7. Automated acceptance matrix

Mapped approved scenarios before new test code in `.tmp/step211/coverage-plan.md`, reusing Step 210's matrix. Existing fixtures/assertions remain evidence for their own fields; protocol-specific assertions were added rather than inferred from prior PASS.

| Approved scenarios | Existing new protocol test methods / explicit assertion |
|---|---|
| Optional create, reusable identity, namespaces | `ProtocolReferenceSqlTests.OptionalCreateAndReusableNamespacedIdentity`: API create without protocol/fresh GET null, same pair on distinct prescriptions, same identifier under another system |
| Trim, case/Unicode, both independent bounds/no truncation | `UnicodeBoundaryTrimPreservesCaseAndInternalCharacters`; `EveryComponentCountsUtf16WithoutTruncation`; `SqlAndApiUtf16BoundaryAgreementWithoutTruncation`; exact/over-limit pair and SQL/API agreement |
| Partial/blank/control/line-break/surrogate rejection | `PartialGroupsAndOrphanOptionalsReject`; `AllComponentsRejectControlsBeforeTrimming`; `InvalidSurrogatesRejectInEveryComponent`; `InvalidApiAndSqlGroupsNeverMutate` |
| JSON types, presence and duplicates | `JsonPresenceNullCompleteReplacementAndDuplicateValidation`; invalid API/raw SQL parameterized cases |
| Store/read/preserve/replace/clear, full audit metadata | `ApiAtomicReplacementOmissionClearAuditAndIndicationIndependence`: populated pair snapshots, trusted actor/patient/prescription/tenant scope, exact atomic operations and independent existing Reason Code/Indication |
| Invalid requests have no successful audit/mutation | `InvalidApiAndSqlGroupsNeverMutate`: unchanged pair/token, one original clinical-change audit event only |
| Wide SQL and constraints | `WideRawProcedureTrimsAtBoundaryAndRejectsLongGroupsBeforeAnyWrite`; `SecurityConcurrencyAuditRollbackAndSqlConstraints`: limits before assignment, invalid stored groups rejected |
| Permission/owner/patient/tenant/stale/audit rollback | `SecurityConcurrencyAuditRollbackAndSqlConstraints`: rejected cross-scope and stale operations; failed audit restores pair/token |
| Exact correction inheritance/review and both finalizers | `CorrectionCarryReviewResetFinalizationSnapshotsAndArtifacts` (two cases): independent saved review, changes/clear reset, unrelated saves retain review, other-field review insufficient |
| Finalized immutability/history/legacy | Same two cases: original update rejected, original pair/artifact equality, exact new frozen pair/review, prior signed JSON retained, cancellation reason/Indication independent |
| Create/Edit/Detail encoding and narrow layout | `ProtocolReferenceWebTests.ProductionRazorEncodingEntryEditClearAndResponsiveLayout` (three views): exact values/labels, escaping, pair validation/clear, Reason Code and Indication unchanged, no page overflow at both widths |
| Absent Detail, binding and modal population | `DetailAbsentIsNotRecordedAndOptionalsAreOmitted`; `WebPostBindsAtomicPresenceBeforeValidation` (three operations); `ModalEditIncludesExactGroupAndIndependentReviewState` |
| Existing Actions/PDF content | `ProtocolReferenceScopeTests`: two state-specific Actions comparisons, both legacy/signed complete body/header/footer comparisons and legacy-renderer bytes |
| Client behavior | Eight Node cases: optional/pair, exact Unicode load, clear/independence, review reset, both bounds, invalid characters, delayed-reset race and validation redisplay |

No identified uncovered automated assertion blocks the approved bounded implementation. Source authority, actual clinical protocol applicability and membership are deliberately not asserted by synthetic references. Persistent activation and authenticated live UI remain separate qualifications, not missing unit-test substitutes.

## 8. Execution results

| Receipt under `.tmp/step211/` | Result |
|---|---|
| `protocol-focused.trx` | Initial **45/45 PASS**, no skipped/failed cases |
| `protocol-strengthened.trx` | **6/6 PASS**: new optional/reusable identity case, both strengthened correction cases and three strengthened browser/view cases |
| `protocol-node.log` | Focused frontend **8/8 PASS**, no skipped/failed cases |
| `prescription-regression.trx` | One final selected backend pass: **395/395 PASS**, no skipped/failed cases; includes **46 protocol cases and all 49 reason-specific cases** |
| `frontend-regression.log` | One selected prescription frontend pass: **48/48 PASS**, no skipped/failed cases |
| `final-build.log` | Affected test/project dependency build **PASS**, **0 errors / 6 NU1900 warnings** |
| `typescript.log` | **PASS**, `tsc -p src/MicroEMR.Web/tsconfig.json --noEmit` |
| Final diff / preservation review | PASS; existing six changed/untracked files preserved, prior migrations unchanged, no unrelated edits |

Focused results represent **46 unique backend/view protocol cases** with final passing evidence; the six-case run overlaps five earlier cases and adds one. Counts overlap the regression totals and must not be summed as distinct coverage.

Focused development execution built affected dependencies into isolated `.tmp/step211/artifacts/`. After strengthening the tests, one final affected-project build supplied the final test assembly, followed by affected-only checks and the single planned regression pass. Cached restored assets and prior artifacts were reused without package updates. Original Step 204/206 artifacts and normal running service outputs remain intact.

No test failure or unresolved compiler failure occurred. Early local inspection/edit-helper alias/scope errors were corrected before execution of the migration/tests; they made no persistent change. Six NU1900 warnings concern unavailable vulnerability metadata, not six identified vulnerabilities. The existing Electron Node host's crashpad diagnostics are tooling messages; all TypeScript/Node commands exited successfully.

## 9. Manual-only checks and retained qualifications

No manual clinical save is needed to repeat these covered assertions. Human feedback on the usefulness/meaning of supplied protocol references and subjective visual usability can be collected later. Authenticated live Create/Edit/Detail remains **DEFERRED**; no login or session was fabricated.

If a later authorized visibility task is performed, first verify the intended development tenant, applied 0097 and matching compiled runtime, then reuse an existing owned synthetic Draft. Read-only routes are `/PatientPrescriptions/Create?patientUid={P}`, `/PatientPrescriptions/Edit?patientUid={P}&prescriptionUid={R}`, and singular `/PatientPrescriptions/Detail?patientUid={P}&prescriptionUid={R}`. Observe fields/recorded values without submitting Save/Clear/signing/correction/cancellation. Record actual observer provenance; absent prerequisites remain DEFERRED. No such live task occurred here.

Retain:

- **DE09.034 SATISFIED — VERIFIED WITH QUALIFICATIONS**; implementation/assertions preserved and 49 reason cases PASS in the current regression.
- Recovery **CONDITIONAL**; no new owner recovery confirmation, backup or restore.
- Terminology/protocol membership, source approval, clinical validity and currency **unverified**; these are not claims of the supplied-reference feature.
- Six NU1900 vulnerability-metadata warnings.
- Future Auth restarts require safeguards against its automatic migration/seeding writes; no service restart was performed.
- Persistent 0097 application and DE09.035 live runtime compatibility **not performed/verified**. Step 207's older runtime verification is not relabeled as protocol activation.

**PC07.01 remains PARTIAL; inventory 7 mandatory IDs / 3 packages:** Prescribing PC04.01/.06/.07/.09/.14; CPP PC07.01; Scheduling PC09.03. Implementation does not reduce this inventory or close other DIN/destination/authority/source/audience residuals.

## 10. Recommended Step 212 and final boundary

**DE09.035 read-only local migration/runtime/recovery readiness and automated evidence reconciliation.** Reuse this passing evidence; verify the intended development target and actual 0097 state without assuming application. Obtain separate explicit owner authorization before persistent migration application or changes to matching runtime binaries/services. No repeated broad suite, manual prescription save, fixture creation or Auth restart is indicated by implementation acceptance alone. Keep authenticated UI/recovery/source qualifications unless actual new evidence resolves them.

This step stops after implementation, automated verification and documentation. No commit, push, merge, persistent migration, service restart or deployment.
