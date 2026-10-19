# Step 117 - PC07.01 Medication DE09 remaining gap prioritization

Date: 2026-10-19. Assessment and documentation only. Reuses [Step 115](150-step115-pc07-01-medication-de09-requirement-resolution.md) and [Step 116](151-step116-pc07-01-prescription-date-precision-implementation.md). **PC07.01 PARTIAL; 10 mandatory IDs / three packages.** Step 118 is recommended, not implemented.

## 1. Actual Git checkpoint and assessment boundary

- Actual branch: `main`; HEAD: `a19603a154989c0b069301be40fe05396ba8f324`.
- `git status --short` was empty at entry. This supersedes assumptions based on the historical Step 116 feature-branch checkpoint; no branch/HEAD change was made here.
- Only this report is added. Existing files, including Steps 115/116 and all prior user work, remain unchanged. No staging, commit, merge or remote Git operation.
- Read AGENTS.md first. Reused the retained authoritative ledger and source findings; no workbook/PDF extraction or repeated 36-field mapping. Targeted source inspection covered prescription DTO/service/repository, API and Web prescription controllers, prescription output composer, CPP prescription loader/models and migration 0083. These are static source observations, not manual runtime verification.
- No tests, builds, database queries, migration/status commands, browser checks or application changes. Only documentation diff/whitespace checks are performed.

## 2. Step 116 evidence checkpoint

Preserve Step 116 sections 7-8 verbatim as the execution authority:

- **95 distinct relevant .NET cases with passing latest outcomes; 0 unresolved failures; 0 skipped.** This consolidates **94 PASS / 1 FAIL** in the reviewed gate plus the corrected case's subsequent **1 PASS**; it is not a single 95/95 run. Historical failed/setup runs remain recorded.
- **25 new .NET cases**: 13 validation, 11 prescription/CPP integration and one chronology case. Reused cases remain regressions, not new coverage.
- **4 frontend PASS**, TypeScript `--noEmit` and targeted emission **PASS**; final relevant build **0 warnings / 0 errors**, 5.92 seconds.
- V1 draft editor controls/validation, V2 saved/correction/signed state, V3 CPP/selected-print date rendering: **USER-REPORTED PASS** on 2026-10-19. No independent visual verification or new execution result is claimed here.
- Prescription written/start precision, correction copying, immutable artifacts, fixture tenant/patient isolation, trusted actor, stale writes and transactional audit rollback retain their bounded evidence. Partial written dates remain qualified for exact-day chronology; no interval chronology was implemented.
- Step 114 retains **53 .NET PASS, 11 frontend PASS, print script PASS, TypeScript PASS, final build 0 warnings/errors** and V1-V3 USER-REPORTED PASS. Nothing here revises earlier acceptance.
- Disposable-store migration success is not application-tenant deployment verification. Do not assume migration 0082 or 0083 has been deployed or verified in an application tenant from this assessment.

## 3. Remaining requirement matrix

Authority and row locators are inherited from Step 115 section 2: CDS-S Data Dictionary v2.1 Final, Medication rows 126-161, plus its retained Baseline/Requirements citations. All elements below are **mandatory product support**. **S/D** means store/display; **S** means store-only. Neither means every record must contain a known value, every field must appear on a compact CPP card, or store-only data is optional. This is a remaining-gap matrix, not a new full ledger.

Status applies to the stated slice. READY is engineering readiness for a separately authorized bounded implementation, not certification acceptance or an assertion of clinical facts.

| Exact ID / obligation | Current implementation and confirmed gap | Reusable contract; dependencies and independence | Priority / classification |
| --- | --- | --- | --- |
| **DE09.036 Generic Name**, row 129, M S/D | ProductName/ProductDisplayText and MedicationName do not independently identify the non-proprietary name. No explicit generic-name property in inspected order request/response. | Existing prescription authority, draft CRUD/correction and CPP projection can carry an explicitly supplied nullable name. No required code set. Never parse a name or claim actual dispensing from an order. A bounded manually recorded order value is independent of a catalogue and list reconciliation; wider source coverage remains qualified. | **P1 READY — IMPLEMENTATION**, limited scope in section 7. |
| **DE09.019 Private prescription Notes**, row 145, M S/D | No independent private order-note field. List Notes, Directions, Indication and NotesToPharmacist have different meanings. Prescription-print exclusion is mandatory. | Draft CRUD and clinical audit reusable. Decide permitted readers, correction/copy policy and CPP audience before exposing text. Generic CPP print emits projection properties: accidental inclusion is a concrete risk. Independent of other fields after policy closure. | **BLOCKED — CLINICAL DECISION**; missing field confirmed. |
| **DE09.018 Patient Compliance**, row 144, M S/D | No explicit compliance contract. Active, Ongoing, issued refills or a finalized order do not establish adherence. | List/order storage patterns reusable only after approving assessment owner, values, unknown, attribution/time and correction history. No supplied mandatory terminology. Must not attach a current assessment to a frozen signed order by assumption. | **BLOCKED — CLINICAL DECISION**; missing assessment support confirmed. |
| **DE09.029 Substitution Not Allowed**, row 155, M S/D | No explicit instruction; brand selection does not imply prohibition. | Prescriber-owned draft/sign/correction pattern reusable. Approve unknown versus allowed/prohibited, explicit intent and output wording. Future snapshots could carry it; historical signatures cannot acquire it. Independent once approved. | **BLOCKED — CLINICAL DECISION**; missing intent support confirmed. |
| **DE09.004 Drug Description**, row 130, M S/D | ProductDisplayText is a candidate, but dedicated compound/complex-description meaning and separate retrieval are unproven. SIG is insufficient. | Resolve name/display/complex-description roles and applicable source before adding a duplicate field. Current free-text transport reusable. | **BLOCKED — CLINICAL DECISION**; semantic contract, not proven absence of all storage. |
| **DE09.005 Drug Code/DIN**, row 131, M S | Optional product namespace/value pair exists; database-selected DIN extraction, namespace and provenance capability unproven. | Reuse identifier pair after authorized source and uncoded/compound/legacy rules are defined. Typed names remain allowed; never fabricate DIN. No catalogue purchase or format rule inferred. | **BLOCKED — CLINICAL DECISION** for source/applicability; additional integration definition required if a particular source is selected. |
| **DE09.016 Long-Term Medication**, row 142, M S/D; **DE09.025 Treatment Type**, row 151, M S | List Ongoing/Acute/unknown is accepted treatment intent/default behavior; no independent order TreatmentType. Long-period equivalence and plan/order ownership unresolved. | Reuse classification only after explicit mapping; .016 and .025 remain distinct. Optional CT-034 does not mandate conversion. No duration threshold/backfill. | **BLOCKED — CLINICAL DECISION** for DE09 equivalence; accepted PC07.06 default remains satisfied. |
| **DE09.017 Past Medication Indicator**, row 143, M S/D | Discontinued list status is a candidate for plan discontinuation; retrieval correlation remains pending. Inactive, Acute, Cancelled and Superseded are not substitutes. | Existing lifecycle filtering/RowVersion reusable; extending Discontinue requires repairing conditional audit, incomplete snapshots and missing matching active-actor SQL guard recorded in Step 115 section 9. Plan ownership must be confirmed first. | **IMPLEMENTED — EVIDENCE PENDING** for existing status behavior; **BLOCKED — CLINICAL DECISION** for full DE09 mapping. |
| **DE09.021 Prescribed By Name / .022 Identifier**, rows 147-148, M S/D | Provider first/last/BillingNumber and immutable display/credential snapshots exist. Explicit OHIP meaning, first/last correlation and original external-prescriber attribution are unproven. | Trace existing provider/source snapshots before adding fields. Operator/creator is not automatically original prescriber. Do not reinterpret old credential strings. | **IMPLEMENTED — EVIDENCE PENDING** for native sources; **BLOCKED — CLINICAL DECISION** for historical/external identity policy. |
| **DE09.023 Prescription Identifier**, row 149, M S | PrescriptionUid already provides the internally unique identity permitted by D. No missing ID service established. | Existing resource/artifact linkage and tests reusable; itemize DE09-specific evidence correlation. Independent documentation closure possible. | **IMPLEMENTED — EVIDENCE PENDING**. |
| **DE09.026 Prescription Status**, row 152, M S | Draft/Finalized/Cancelled/Superseded exist. No confirmed missing lifecycle field; DE09 correlation remains pending. | Existing lifecycle evidence reusable. CT-035 optional; no blind status rename or inference that all optional codes are required. | **IMPLEMENTED — EVIDENCE PENDING**. |
| **DE09.027 Non Authoritative Indicator**, row 153, M S | Local/non-transmission footer is not an explicit stored authority/paper-review indication. | Decide authority, actor and local/electronic applicability. A false default would invent dispensing authority. Draft/snapshot patterns alone cannot resolve this. | **BLOCKED — CLINICAL DECISION**; **BLOCKED — AUTHORITATIVE DEFINITION** for any claimed electronic-dispense policy not supplied by the retained sources. |
| **DE09.028 Dispense Interval**, row 154, M S | No explicit minimum interval. Initial/refill days' supply are different. | Need approved unit, precision, unknown and validation contract. Source defines meaning but no units/ranges; omission is not a reason to invent a clinical limit. Independent after resolution. | **BLOCKED — CLINICAL DECISION**. |
| **DE09.030/.031/.032 Target Dispensing Facility address/name/ID**, rows 156-158, M S | No target-location fields in inspected prescription contract. Clinic/patient/provider address and ArtifactUid cannot substitute. | Confirm location source, association, identifier namespace and per-order applicability. Do not create a pharmacy master or integration implicitly. | **BLOCKED — CLINICAL DECISION**; source-specific identifier definition also pending. |
| **DE09.033 To Be Picked Up When**, row 159, M S | Earliest expected pickup date/time absent. Written/start/sign time cannot substitute. | Approve time-zone, precision, unknown and provenance policy. This is a separate time-bearing requirement, outside Step 116. | **BLOCKED — CLINICAL DECISION**. |
| **DE09.034 Reason Code / .035 Protocol Identifier**, rows 160-161, M S | Indication is text; no distinct coded reason or jurisdiction-specific protocol reference. | Approve supplied code/reference source, namespace and applicability. No compulsory code set is named for .034; no invented vocabulary. | **BLOCKED — CLINICAL DECISION**; source-specific definitions pending if selected. |
| **DE09.001 Written Date / .002 prescription Start Date**, rows 126-127, M S/D | Step 116 supports full/year-month/year on PatientPrescription, explicit missing start, honest correction dates and frozen old artifacts. No confirmed remaining precision gap in that authority. | Retain written-required product policy, distinct dates, legacy omission preservation and exact-day chronology qualification. Unlinked medication-list StartDate remains full-only; broader plan/source applicability is unresolved, not closed by order support. | **SATISFIED WITH RECORDED EVIDENCE** for Step 116 prescription slice; **RELEASE-QUALIFIED** for deployment; wider source mapping **BLOCKED — CLINICAL DECISION**. |

Already-supported anchors are reused without repeating their mapping: DE09.006-.010 structured prescription fields, .011-.015 initial/refill instructions, .020 pharmacist-facing notes and .024 replaced-prescription linkage retain **SATISFIED WITH RECORDED EVIDENCE** within their recorded prescription slices (Step 115 section 10, Step 116 regressions). DE09.003 typed name remains **IMPLEMENTED — EVIDENCE PENDING** for explicit DE09 correlation. These do not establish a reconciled whole medication list.

No supplied DE09 field definition is missing from the restored mapping. AUTHORITATIVE DEFINITION blockers above concern additional claimed integration/authority rules, not a request to reopen or re-extract the dictionary.

## 4. Confirmed gaps and priority rationale

1. Independent generic name (.036) is missing and can be supported without choosing clinical assessment categories, dispensing authority or a new source integration. Preserve absence as unknown, use trusted prescriber entry and retain attribution. This is the highest-priority safe implementation slice.
2. Private prescription notes (.019), compliance (.018) and substitution intent (.029) are confirmed absent distinct capabilities. Their audience/assessment/default decisions affect clinical meaning or disclosure and must precede implementation. No combined notes/compliance/substitution step is recommended.
3. Explicit authority (.027), interval (.028), dispensing destination (.030-.032), pickup (.033), coded reason/protocol (.034/.035) are missing contracts. They remain mandatory product gaps despite store-only status.
4. DIN capability/provenance (.005), complex description (.004), long-term/treatment-type (.016/.025), original prescriber (.021/.022) and plan discontinuation (.017) require mapping/policy or evidence closure; existing generic text/flags must not be relabeled as completed DE09 support.
5. Dates: no additional DE09 end/discontinuation/recorded-date element was identified in Step 115. Preserve existing list dates and historical records. .033 remains a distinct unresolved pickup date/time gap. Full-only unlinked list start and partial-date chronology remain explicitly bounded; do not claim Step 116 solved either broader workflow.

## 5. Blocked decisions and resolution owners

Clinical/product ownership must approve plan versus order ownership, external/non-prescribed provenance, long-term equivalence, compliance assessment, private-note access/CPP audience, substitution unknown/default/intent, authority and interval/pickup semantics. Drug/provider/location source owners must establish DIN, OHIP/original prescriber, location identity and supplied code/reference provenance. Governance must establish accepted release and any external electronic-dispense rules.

For the proposed generic slice, use the existing native prescription aggregate and an explicitly entered, nullable generic name. This is a bounded product design recommendation supported by .036's clear name meaning and absence of prescribed terminology; it does not assert drug-database validation, that an actual dispense occurred, or that the separate medication-list ownership question is settled. Unknown stays unknown. If implementation requires inferred names, an authoritative dispensing feed or cross-source association, stop and resolve that specific dependency rather than expanding this step.

## 6. Reusable implementation and coupled safeguards

Reuse `PatientPrescriptionModels.cs`, `PatientPrescriptionRepository.cs`, existing API/Web prescription controllers and `_Form.cshtml`; `PatientCppPrescription`, `PatientCppService`, preference catalogue/screen and `CppPrintService`; `PrescriptionOutputService`; effective procedure patterns in 0083. Existing signatures/artifacts and clinical dates are not mutable enrichment sources.

| Safeguard | Required treatment in a generic-name extension |
| --- | --- |
| Tenant and patient ownership | Existing tenant connection factory plus patient/resource predicates for every read/write. Validate all source rows before status filtering/projection, for screen and selected output; fail closed on foreign/mismatched rows. |
| Permissions and trusted actor | Keep Patients.View for reads and Prescriptions.Prescribe for writes; use server clinical actor, active linked Provider and prescriber ownership. Reject client identity overrides. |
| Transactional audit and snapshots | Required audit in the same transaction as create/update, including generic name and complete before/after clinical JSON; create-before NULL. Audit failure must roll back values/token/success event. Keep signing/output audit mandatory; snapshot attribution must remain accurate. |
| Concurrency | Reviewed eight-byte RowVersion checked atomically for update/lifecycle; no lost updates. Validate length before Infrastructure/SQL fixed-width conversion. Correction follows current-version/source-status rules and cannot mutate source. |
| Compatibility | Additive nullable field, no backfill. Absent old-client update preserves value; explicit null clears a draft value. Track presence separately from null through API/Application/repository/SQL. New Web editors explicitly submit the intended state. |
| CPP/preferences/selected print | Preserve current keys, classification/default scopes and five-row card/full selected-source behavior. Add an explicit prescription generic-name projection/preference if shown on CPP; screen hiding does not cancel explicit selected output. Escape values; no load/output for unselected source; no list/order name matching. |
| Signed history | Copy recorded generic name to a new correction draft; freeze it only in future artifact versions. No updates to existing signed rows/artifact JSON, no enrichment from live product/provider data on reprint, no new labels in historical output lacking the new marker. |

**Known coupled repairs:** Step 115's dedicated prescription List/Get read path has no dedicated read-audit call; chart-open audit does not prove ownership-before-success audit for those resources. Add the established patient/resource-authorized, fail-closed read-audit pattern to affected generic-bearing reads before returning content; never log note/name content in operational messages. Inspect the existing read-audit contract at implementation time rather than inventing one here. CPP currently filters Finalized rows before checking ownership, checks only for full selected loading, and does not check discarded nonfinalized source rows. Move ownership validation ahead of filtering/projection for affected screen/print loads. Prove no successful content/read event on ownership failure and no content on audit failure. These are coupled acceptance work, not separate broad security refactoring.

Medication Discontinue's conditional audit/incomplete snapshots/actor guard remain a recorded limitation. Step 118 must not call or extend that operation; repair it within a future past-medication scope. Existing prescription lifecycle audit text is not itself a new-field before/after guarantee; add affected assertions rather than claiming blanket coverage from old tests.

## 7. Exactly one recommended Step 118

**Step 118 - DE09.036 bounded native-prescription generic-name support.** No other DE09 feature is bundled. Goal: separately store/retrieve/display a provider-recorded generic name while preserving unknowns, provenance, concurrency and signed history.

### Functional and source boundary

- Add nullable, separately labeled Generic name to native prescription draft create/edit, response/detail and correction copying. It is the recorded non-proprietary name, not an inferred alias or a claim of actual dispensing. ProductName/ProductDisplayText/DIN retain their current meanings.
- Use `PatientPrescription` as sole authority; existing trusted creator/updater and clinical audit provide recording provenance. No automatic copy into `PatientMedication`, no catalogue lookup and no normalization into an invented terminology.
- Recommend maximum 300 Unicode characters and trimmed empty-to-null as engineering constraints, validated consistently in Application/HTTP/SQL; these are not authoritative clinical limits. Omission preserves on update; explicit null clears only in a mutable draft.
- Display saved values on the existing prescription detail/list context and CPP Current Prescriptions projection with an explicit field preference. Selected Current Prescriptions output includes the recorded value independently of screen hiding; no unselected prescription data is fetched/output. Unknown is displayed honestly as Not recorded where a value label is shown.

### Expected changes after separate authorization

- One new additive tenant migration/manifest entry after verifying the actual next free ID; do not modify 0083/history. Nullable GenericName column and compatible trailing value/presence procedure parameters; update effective get/create/update and future finalize/sign snapshot projections. Preserve old rows, dates, tokens and artifacts without backfill.
- Extend existing DTO/Application normalization and presence semantics, Infrastructure parameter/mapping, thin API/Web binding/edit/correction paths, existing Bootstrap field and saved display. Include coupled ownership/read-audit repairs from section 6.
- Extend CPP prescription projection/catalogue/preference/display and selected print for this field only. Stable prior preference keys and scopes remain unchanged.
- Introduce a new optional artifact capability marker for future generic-name snapshots and conditional output composition on both current and legacy-renderer paths that can create new artifacts. Old artifacts without the marker render through their unchanged branch; no live-row fallback. Existing page layout and pharmacist/refill/date content remain.

### Automated acceptance to run only during Step 118

1. Real HTTP/SQL create/read/update for supplied, blank/unknown, Unicode and escaped name; independent brand/display names; over-limit rejection without mutation. Test Application callers as well as HTTP validation.
2. Old-client omission preserves; explicit clear works; stale/malformed token conflicts preserve original row/audit count. Direct SQL contract does not silently truncate or overwrite unknown presence.
3. Wrong patient, wrong tenant in both directions, unauthorized role, inactive/unmapped actor and wrong prescriber fail without data disclosure/mutation. Client-supplied recording identities are ignored/rejected.
4. Mandatory mutation-audit failure rolls back generic name, full snapshots and token. Affected read-audit failure releases no content; foreign source rows, including nonfinalized rows, are rejected before filtering or success read events.
5. Finalize/sign freeze the entered name; correction copies it into a new mutable draft; later correction/cancellation cannot alter original signed row/artifact. Existing artifacts lacking the marker remain byte-preserved and render without new generic labels or enrichment.
6. CPP screen preference hiding/restoration, selected-print independence, more than five eligible records, escaping and unselected-source non-loading. Generic presence must not change classification/defaults or Step 116 date precision/correction behavior.

Reuse the existing real SQL/HTTP prescription, signed-output and CPP selected-print fixtures. Run focused new/affected checks only; prior passing suites need rerun only where the implementation changes their contract. Step 117 runs none of these.

### Essential visual checks after implementation

- V1: readable Bootstrap Generic name label/control, distinction from product/display name, clear optional/Not recorded behavior and validation; saved edit round trip.
- V2: saved value and copied correction name readable; signed row read-only; original artifact output unchanged while future output shows its recorded value.
- V3: CPP name wrapping, field preference hide/restore and explicit selected output; unselected prescription absence. Do not reopen unrelated accepted categories/date visuals.

### Exclusions and unresolved decisions

Exclude .019 notes, .018 compliance, .029 substitution, .004 complex-description redesign, DIN/catalogue integration, other metadata, medication-list linkage/reconciliation/import, Discontinue, chronology interval support, clinical backfill, historical artifact rewrites, e-prescribing and pharmacy master data.

No new clinical category/default is needed for an explicitly supplied name. The 300-character bound, field placement, preference key and artifact marker are proposed engineering details to confirm against existing patterns during implementation. Wider dispensed-drug/source ownership and external medication coverage remain unresolved; this native-order slice must not be represented as whole-DE09 satisfaction. If these boundaries cannot be preserved, resolve that specific contract before implementation proceeds.

## 8. Deferred evidence and deployment qualifications

- Preserve Step 115 section 12 and Step 116 section 12: Baseline folder 5.5/document 1.7 versus CDS-S folder 5.2/workbook 2.1 Final, Requirements 1.1, accepted release and external OntarioMD/CPSO acceptance remain qualified.
- Migration 0082/0083 disposable-store evidence remains separate from controlled matching application/tenant/SQL-engine deployment evidence. The earlier PlatformAdministration-disabled command established no migration state. No deployment or migration verification is performed or inferred here.
- Real OIDC/session/roles/selected-tenant and source provenance retain their recorded qualifications. Controlled integration identities are not deployed identity evidence. User-reported visual passes do not settle those boundaries.
- Preserve Family M7/M8, Immunization numeric dose/unit/pairing, Allergy external recorded-date/ambiguous DIN and separate PC07.10 encounter-managed obligations. They are outside this assessment's implementation recommendation.
- No READY recommendation is a runtime PASS. Evidence-pending rows need explicit correlation or focused future evidence, not invented results or mandatory duplicate fields.

## 9. Certification inventory and documentation status

**PC07.01 remains PARTIAL. Inventory remains 10 mandatory IDs / three packages:** prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). DE09 element IDs do not add inventory IDs. No SATISFIED - VERIFIED promotion.

Only this report is added. Documentation diff reviewed and whitespace checked; pre-existing files preserved. No application code, SQL, migration, UI, tests or existing reports changed. No tests/builds/manual verification, commits or merges. Stop after Step 117; Step 118 is not executed.
