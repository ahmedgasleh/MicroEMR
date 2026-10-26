# Step 203 — PC07.01 / DE09.034 Reason Code contract and local feasibility

2026-10-26 (America/Toronto). **DE09.034: CONDITIONAL — locally implementable coded-value capture is technically feasible, but code-source and clinical/owner policy decisions are unresolved. Not implemented or satisfied. PC07.01 remains PARTIAL; inventory 7 IDs / 3 packages unchanged.**

**Active branch: `main`.** Read-only Git status identified existing modified/untracked prescription implementation, tests, configuration and certification reports. No branch switch was attempted: this step is documentation-only. Only this report was added. No application/SQL/test-code changes, database access, clinical save, migration, deployment, restart, build/test execution, external correspondence or CCDD/DHDR investigation occurred. Existing uncommitted work was preserved.

## 1. Authoritative DE09.034 requirement

Re-read only the relevant local workbook XML row and Legend entries, reusing [Step 115's PC07.01 mapping](150-step115-pc07-01-medication-de09-requirement-resolution.md).

Source: [EMR Core Data Set Standard (CDS-S) — Data Dictionary (v2.1).xlsx](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), publication **2026-06-05**, **2.1 Final**, sheet **Data Dictionary**, row **160**; Legend rows **3, 5, 6, 7**.

| Property | Exact source / assessment |
|---|---|
| A160 | `DE09.034` |
| D160 — exact name | **Reason Code** |
| E160 — exact definition | **“A coded form of the problem that is the reason for the current prescription. Facilitates cross-checking the use of a therapy against its indication.”** |
| C160 — element support | `M`: mandatory to support |
| F160 — storage/display | `S`: store; distinct from `S/D` |
| G160 — named code system | Blank: **NOT SPECIFIED** |
| H160 — code-set conformance | **Optional** |
| K160 — logical entity | **Medication**; this is not a primitive data type |
| Primitive/wire/SQL data type | **NOT SPECIFIED**; definition establishes coded clinical meaning, not string/identifier/CodeableConcept/physical storage type |
| Allowed values, fixed list, namespace/version, length and character restrictions | **NOT SPECIFIED** |
| Cardinality, multiple coding systems, per-record required entry | **NOT SPECIFIED** |
| Required reason-description field | **NOT SPECIFIED** as an additional DE09.034 component |
| Required terminology service, directory, code-table maintenance or update mechanism | **NOT SPECIFIED** |
| Automatic clinical checking, alert severity, override workflow or matching algorithm | **NOT SPECIFIED** by this row |
| PDF placement, transmission trigger, exchange format | **NOT SPECIFIED** by this row |

Legend B3 distinguishes mandatory/optional **element support**. Legend B5 distinguishes S from S/D. Legend B7's Optional permits supporting or not supporting a code set. **H160 Optional does not make DE09.034 an optional product element**, and a blank G160 does not establish a hidden mandatory vocabulary. Conversely, plain free text does not become a coded problem merely because vocabulary conformance is optional.

No claim is made that this excerpt settles every independently applicable prescribing/CDS/exchange requirement. Those remain separately mapped. The source does not identify a conditional circumstance that requires a known code on every historical prescription; no arbitrary default is justified.

## 2. Applicability and related elements

The definition concerns the **problem motivating the current prescription**, enabling therapy/indication cross-check. It is not a cancellation reason, free-text SIG, pharmacist instruction, drug identifier, protocol identifier or dispensing location.

- Existing PC07.01(f)/PC07.06 mapping places DE09.034 in the Medication group. The source does not prescribe whether entry happens in Create, Edit, a medication plan, an encounter or an external import. Capturing it on the native prescription is a proposed local ownership/workflow choice, not verbatim regulatory workflow wording.
- **DE09.035**, neighboring row 161, is the distinct protocol/guideline reference, not the problem code. Its definition was checked to avoid substituting a protocol identifier for the reason.
- **DE09.005** is drug identity/DIN, not the coded problem. Destination DE09.030–.033 remains separate.
- Existing native `Indication` is potentially useful context for a human cross-check, but there is no source instruction to parse a code from it or equate it with coded data.
- Existing encounter diagnoses/problems can provide clinical context. An internal diagnosis/problem UUID alone is not an external clinical code or a code-system identifier.

An automatic drug-to-diagnosis safety engine is not established by “Facilitates cross-checking” plus S alone. A proposed editor/full Detail can place the coded reason beside indication/context for review; any stronger automated clinical-checking requirement must be evidenced/scoped independently, not implied into this field task.

## 3. Current implementation evidence

| Inspected implementation | Actual reusable behavior and gap |
|---|---|
| `Application/PatientPrescriptions/PatientPrescriptionModels.cs` | `PrescriptionDraftRequest.Indication` is nullable free text with 500-character validation; response carries it. No reason-code/system/version/display group, presence bit or reason-specific validation/review metadata in this contract |
| `PrescriptionTransitionRequest.Reason` / cancellation fields | Transition Reason and `PatientPrescription.CancellationReason` describe cancellation. They do not represent the coded prescribing problem |
| `PatientPrescriptionService` in the models file | Existing create/update normalization, trusted interfaces, linked-source inheritance for name/address/pickup and immutable transition boundary are reusable. No coded indication normalization, source selection or code membership validation |
| `Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs` | Existing tenant-bound connection, stored-procedure writes, DTO mapping, trusted actor, RowVersion, specified flags and safe errors. `Indication` is sent as a string parameter; no reason-coding read/write mapping |
| `0050-patient-prescription-foundation.sql` | Actual `dbo.PatientPrescription` foundation has nullable `Indication nvarchar(500)`, cancellation reason, audit/lifecycle columns and RowVersion. No dedicated reason coding in this table definition |
| Current `0095-prescription-target-facility-address.sql` | Six existing prescription read/create/update/finalization procedures retain text Indication, transactional AuditLog, locked correction source and prospective artifact JSON. No reason-code projection, dedicated coding fields, specified flag, validator or reason-review gate in these current definitions |
| API and Web `PatientPrescriptionsController` | Existing authenticated/patient-scoped routes, prescribing policy, trusted actor, antiforgery and Draft/correction paths. Correction request copies free-text Indication. This does not establish coded-reason carry-forward |
| `_Form.cshtml` / `Detail.cshtml` | Existing Create/Edit optional Indication text input and supplied-field editor patterns; full prescription Detail and encoding/wrapping patterns exist. No coded reason/system/version/display editor or Detail presentation |
| `PrescriptionOutputService.cs` / legacy artifact rendering | Free-text Indication is snapshotted/used in existing output. Frozen artifacts/output-generation audit are reusable. No reason-code snapshot or print obligation has been implemented |
| `Application/PatientEncounters/EncounterDiagnoses.cs` | Current inspected diagnosis input/record has Name, Description, OnsetDate and internal diagnosis/problem identifiers; no code-system/code/version pair. It is **not evidence of an immediately reusable coded terminology source** |

This is a targeted finding for the inspected native prescription/diagnosis path, not a repository-wide assertion that no unrelated code catalog exists. No current database metadata query or clinical record was needed. Existing Indication, cancellation Reason or an internal UUID must not be relabeled to claim DE09.034 support.

## 4. Gap and feasibility classification

**CONDITIONAL — owner and clinical decisions required.** A bounded per-prescription coded-value capture can be implemented and tested locally using established architecture; the inspected requirement does not mandate an external integration. However, no approved reason-code source/system or applicable clinical capture policy is established in the reused evidence. Syntactically accepting arbitrary strings under a newly invented namespace would not prove a genuine coded problem.

| Dependency / decision | Requirement versus local feasibility |
|---|---|
| Fixed code list | NOT SPECIFIED by source. Do not invent a finite list solely to make acceptance pass |
| External terminology service | NOT SPECIFIED and not needed for the proposed storage/capture scope; do not add one |
| Locally maintained terminology table | Not inherently required. Could be an owner-approved alternative if a genuine local vocabulary is chosen, but it adds governance/import/versioning scope |
| Versioned code system | Mandatory version handling NOT SPECIFIED. Stable system identity is recommended to disambiguate codes; optional supplied version avoids guessing a release |
| Code source/provenance | **Unresolved local prerequisite:** owner/clinicians must identify what actual coded problem values users will enter/select and what authority supplies them |
| Allowed-value membership | Source list/membership rule NOT SPECIFIED. Syntactic completeness is locally testable; membership needs the approved vocabulary/reference data if required by the chosen policy |
| Clinical workflow | Decision needed: who records the current prescription's reason, when unknown is allowed, and how code/indication disagreement is reviewed |
| Reason description | Additional mandatory field NOT SPECIFIED. Optional display-text snapshot recommended for understandable review, distinct from Indication |
| Existing finalized artifacts | **No rewrite permitted/needed**. New future snapshots can contain coding metadata; old artifacts remain byte-preserved |

A declared-system supplied-code scope has no unavoidable external runtime dependency. If owner policy instead requires terminology lookup, code membership verification, translations/mappings, automatic drug-indication checking or an unavailable licensed dataset, that portion becomes BLOCKED until its precise source/interface/evidence exists. This step neither investigates suppliers nor assumes such data is already available.

## 5. Proposed minimum coded-reason contract — recommendations, not approval

Implement only after the decisions in section 7. All field shapes, lengths, review gates and UI choices below are **local recommendations**; the source requirements remain section 1.

| Concern | Recommended default |
|---|---|
| Ownership/cardinality | One optional coding group on the native prescription for the problem motivating that order. Multiple coding mappings/automatic plan or encounter synchronization excluded from the initial slice; cardinality is a local MVP choice |
| Logical representation | `reasonCoding: { system, code, version?, display? }` or NULL. Proposed logical fields: ReasonCodeSystem, ReasonCode, ReasonCodeVersion, ReasonCodeDisplay plus independent saved review metadata; these are **not existing schema names** |
| Physical minimum | Dedicated nullable fields on the existing prescription, updated atomically through its existing procedures; no new terminology master or prescription aggregate by default. Prospective migration numbering must be checked later, not reserved now |
| System identity | Require a stable declared system/namespace when a code is recorded. Approve the supported source/system identifiers before implementation; never invent “local”, infer ICD/SNOMED or use a patient/problem UUID as a clinical namespace |
| Allowed values | No spec-mandated list to hardcode. For an approved supplied-code mode, validate system/code pair structure and agreed technical bounds; do **not** claim membership/clinical correctness was automatically verified. If membership is required, use an explicitly approved available local source and source-specific tests; re-scope if absent |
| Version/display | Store supplied version and optional human-readable code display as snapshots. Do not guess current version or translate a code into an invented label. Code-system identity is not inferred from display or Indication |
| Technical bounds | Recommend system 200, code 100, version 100, display 500 **UTF-16 units after agreed boundary trimming**. These are proposed technical limits, not source limits |
| Text validation | Single-line valid Unicode; reject malformed surrogates/control/line-separator input before trimming; no silent truncation, case folding, code punctuation rewriting or postal/drug-specific regex. Apply additional syntax only when the chosen system actually requires it |
| Complete/partial group | Known coding requires both system and code. Version/display alone or one missing member is invalid. An entirely blank normalized group can clear; an empty/malformed JSON object must not silently masquerade as a known code. Exact empty-object policy needs approval |
| Omission/null | Omitted group preserves on unrelated updates; explicit null clears all coding components atomically; valid object replaces the group. Independent JSON/DTO/Web/SQL presence handling prevents old clients from clearing it |
| Unknown/legacy | NULL means **Not recorded**, not “no indication.” No default code, guessed namespace, bulk coding of old Indication, or new known values in legacy orders. Audit distinguishes clear from never specified |
| Draft editing | Existing owned Draft only, current RowVersion, prescribing permission and trusted actor. Preserve existing Indication behavior; no unrelated refactor or automatic replacement of that text |
| Correction source | Exact code/system/version/display carry-forward under existing source locks into the linked correction Draft; no re-lookup against a newer code-set release and no forced recoding of history |
| Explicit correction review | Recommend independent reason-coding review saved with current Draft RowVersion; known source/current coding requires it before both finalization paths, including intentional clear of known source. Changing group invalidates earlier review unless reviewed in that same save. Other field reviews cannot satisfy it. This is local safety policy, not an E160 mandate |
| Audit/concurrency | Existing transactional before/after AuditLog covers complete coding group, changes/clear/review and trusted actor. Failed validation/audit/concurrency must leave data and token unchanged |
| Finalized/history | Do not directly edit finalized originals. Use the existing linked correction lifecycle. Freeze coding/system/version/display/review in prospective artifact JSON for both finalization paths. No historical backfill/artifact rewrite; ordinary supersession metadata can still change normally |
| UI/retrieval | Existing Create/Edit/modal with optional system/code, optional version/display, preserve/set/clear, validation and correction review; authorized full Detail shows the recorded coding and Indication context. Display is a local retrieval recommendation, not newly imposed S/D |
| PDF/transmission | Preserve existing PDF/list Actions/compact CPP content and no new transmission. Row S does not itself mandate those output changes; any independently applicable exchange requirement needs separate evidence/scope |

The coded group's display text can describe the coded problem; existing Indication remains the clinician's independent explanatory text. Neither display-only nor free-text-only entry satisfies coded storage. A source-qualified manual code is not evidence that therapy/indication agreement was automatically validated. Human review of real coding/indication context is an appropriate proposed local workflow; an alert engine is a different scope.

## 6. External dependencies and meaningful local acceptance

No external service, jurisdictional code set, namespace, version requirement or mandatory list is named by the assessed row. **No external integration is required by the proposed supplied-code storage scope.** A genuine coding source is nevertheless a local decision prerequisite; its availability must be evidenced, not assumed from the existing uncoded diagnoses.

Owner-provided approved system identifiers and representative genuine code/display/version examples would permit local round-trip/history/UI acceptance. Synthetic opaque codes are sufficient to prove transport mechanics but not actual vocabulary membership or clinical suitability. If clinical correctness/membership is part of the approved contract, an available authorized reference list and independent examples are required for that assertion. Choosing such a source does not automatically authorize imports, licensing work, supplier research or external calls.

## 7. Owner decisions and clinician feedback

| Decision | Recommended default / rationale |
|---|---|
| **Owner + clinician:** actual coding source/system and source authority | Identify a real coded-problem source usable by staff. No assumed ICD/SNOMED table, auto-derived diagnosis code or invented vocabulary. This is the principal readiness gap |
| **Owner:** supplied-code capture versus membership-validated selection | Approve the bounded declared-system supplied-code mode if useful; disclose no automatic membership verification. If a lookup/list is required, identify the existing local data and revise scope before implementation |
| **Owner:** representation, cardinality, lengths, syntax and empty-object behavior | Approve one atomic optional coding group and proposed technical limits; recommend explicit null as the unambiguous clear, all-blank form fields mapped to that clear, malformed/empty API object rejected |
| **Owner:** version/display/history | Optional supplied version/display preserved per order; no guessing/latest-release substitution. Require version only if the selected source's real identity rules require it |
| **Clinician:** when a reason code is known/applicable | Optional per-order entry with no fabricated required value; define practical entry responsibility and exceptional/unknown cases without changing mandatory product support |
| **Clinician:** indication cross-check and disagreement | Show coding with indication context; clinician resolves disagreement, no silent text/code synchronization or unsupported drug-safety inference |
| **Owner + clinician:** correction review | Independent saved coding review, invalidated by changes, in both finalization paths; consistent with name/address/pickup patterns, but requires approval as a local rule |
| **Owner:** audiences/output | Existing prescribing/detail access and frozen prospective metadata; no new PDF/list/transmission feature in the minimum slice |

Source-settled properties are not requests for owner approval. Existing architecture/security/history rules are preserved conventions, not optional shortcuts. None of these proposed local decisions is treated as approved by the request for an assessment.

## 8. Automated testing assessment — no tests added or run

Reused [Step 202 evidence](279-step202-de09-030-automated-acceptance.md): existing 42 backend/8 frontend focused and 295 backend/32 frontend regression PASS, plus five distinct new acceptance cases with final passing evidence and the documented corrected fixture failure. Counts/provenance stay unchanged; this step executes zero tests/builds. Prior tests validate their existing fields/shared boundaries, **not an unimplemented reason-coding feature**.

| Proposed criterion | Existing exact test / reusable fixture | What is already asserted | Missing reason-specific automated acceptance |
|---|---|---|---|
| Presence/atomic clear/legacy | `TargetFacilityAddressContractTests.JsonPresenceSurvivesInternalAndWebSerializationAndDuplicateOrNontextRejects`; `TargetFacilityAddressSqlTests.ApiUnicodeRoundTripDuplicatesOmissionClearAndAudit` in `TargetDispensingFacilityAddressTests.cs` | Existing address omission/null/duplicate/type serialization and audited clear | Reason object omission/null/all-blank, partial components, duplicate/malformed keys, type validation, old client preservation and atomic multi-field replacement/clear: **NOT COVERED** |
| Code/system/version/source semantics | Current diagnosis Name/Description model and prescription Indication | No genuine reason-code catalog/coding assertion established | Approved source/system identifiers, supported syntax, supplied version/display, same code under different systems, unknown system policy; membership only against an identified approved reference: **NOT COVERED** |
| Unicode/bounds | Existing address contract `LengthCountsNormalizedUtf16WithoutTruncation`, malformed-surrogate/control tests and SQL wide-input cases | Actual existing field's Unicode/length normalization behavior | Each coding component's proposed single-line rules, exact limits/over-limit, punctuation/case/combining characters, no truncation and Application/SQL agreement: **NOT COVERED** |
| Security/audit/RowVersion | `TargetFacilityAddressSqlTests.PermissionOwnershipTenantConcurrencyAndAuditFailureProtectName`; `InvalidApiAndProcedureInputLeavesNoClinicalMutation` | Existing route/record scope, owner/state/token and audit rollback assertions for address | Reuse same fixtures, add only coding-bearing requests and atomic coding/review/token audit invariants. Do not assert existing PASS proves mapping of future coding columns |
| Correction/review/history | `LinkedCorrectionInheritsNameRequiresReviewAndPreservesArtifacts`; `AddressReviewCannotBeReplacedByNamePickupOrTransitionFlags` | Existing address source inheritance, both finalization gates, independent review and retained artifacts | Exact coding-group carry-forward; no source-version refresh; new independent review, replacement/clear invalidation; both finalization paths; old artifact and original coding preservation: **NOT COVERED** |
| Create/Edit/modal/full Detail | `TargetFacilityAddressWebTests.ProductionViewsRenderNameAsEncodedTextWithoutChangingOutputScope`, `ExistingWebPostMapsPresenceBeforeValidation`, `ExistingModalEditCarriesExactNameAndReviewState` | Actual existing Razor, safe encoding, operations/loading/review and browser checks at 1280/360 px | Coding controls/model mapping, partial-code errors, display alongside context, clear/Not recorded, exact saved/reopened version/system/code and field-independent review: **NOT COVERED** |
| API persistence and retrieval | `TargetFacilityAddressAcceptanceSqlTests.MultilineUnicodeReplacementAndClearReopenThroughApiAndFullDetail` | Existing address exact create/replace/clear via API/fresh GET and production Detail | Equivalent approved coding values through production DTO/controller/service/repository/procedure, fresh GET and actual rendered coding: **NOT COVERED** |
| Scope/output regression | `TargetFacilityAddressAcceptanceViewTests.ProductionPrescriptionListActionsDoNotChangeWithAddressOrAddressReview`; `LegacyOutputAndSignedPdfLayoutAreUnchangedByAddressSnapshot` | Existing field-independent Actions and complete PDF layout/legacy capture comparison | Coding-specific before/after output independence, frozen prospective coding snapshot, no unexpected list/PDF change: **NOT COVERED** |

Use existing `TargetFacilityAddressDatabase`, `PrescriptionDateHost`, prescription Draft helpers, tenant/actor test variants, Razor/DI stubs, capture renderer and supported Playwright harness. Any fixture writes/schema setup must stay in explicitly authorized disposable databases. No new browser framework is needed. Map criteria first, add only uncovered assertions, run focused tests during development and broader regression only for identified affected boundaries; do not repeat complete historical suites by default.

Storage, API/SQL validation, audit/concurrency, correction, encoding, clear behavior, scope and measured responsive layout are automatable. No repeated manual Draft saves are inherently necessary. **Clinician confirmation of the intended coded problem/source and whether the workflow is clinically useful cannot be established by synthetic round-trip tests.** Authenticated deployed-session behavior and subjective visual usability may retain explicit qualifications unless independently exercised later; do not claim manual execution from component automation.

## 9. Estimated implementation effort

After approval of a supplied-code contract and availability of genuine source examples: **16–24 engineering hours (approximately 2–3 working days)**. Estimate includes atomic DTO/validation/presence handling 3–4 h; SQL/repository/audit/correction/artifact changes 5–7 h; UI/transport 3–5 h; focused tests/review/report 5–8 h.

Owner/clinician decision time, vocabulary acquisition/governance/import, terminology service integration, automated drug-indication checking, persistent deployment/migration and recovery work are excluded. If approved local terminology selection rather than supplied code is required, revise the estimate based on actual available data and validation obligations. No implementation is authorized by this estimate.

## 10. Proposed Step 204 and branch boundary

**Step 204 should resolve and approve the coded-reason contract/source prerequisites first.** Obtain the named coding source/system and real sample values, capture-versus-membership policy, group/validation/empty semantics, optional version/display, audience and independent correction-review policy. If owner approval supplies these, propose a bounded DE09.034 implementation with the tests above; otherwise keep CONDITIONAL and continue documentation only. STOP/re-scope for unsupported terminology claims, an unavailable required dataset, automatic clinical checking or incompatible cardinality/output obligations.

Before any Step 204 application/SQL/migration/test-code edit, check the current working tree and create a dedicated step branch, proposed name **`step204-de09-034-reason-code`**. Existing unrelated/uncommitted work must be preserved. If switching would endanger it, STOP and request instructions; do not commit, stash, reset, clean or discard to make a branch possible. Do not leave `main` unnecessarily for documentation-only assessment. No branch/commit/merge/push was performed in Step 203.

Any future migration must use the then-verified next sequence and existing runner conventions; no migration number, database application, clinical save or runtime change is authorized/reserved here. No supplier/agency correspondence is part of the next local contract task.

## 11. PC07.01, inventory and preserved qualifications

DE09.034 remains **NOT IMPLEMENTED; CONDITIONAL for local work**, with source-backed semantics and explicit gaps. Qualified closures of DE09.030, DE09.031 and DE09.033 remain intact. Recovery remains CONDITIONAL as previously reported; no new backup/restore or owner recovery approval is claimed. Existing live-UI/visual/runtime evidence limits and NuGet vulnerability-metadata qualifications are not silently cleared.

PC07.01 still has source-qualified destination identity, DIN/source, applicable coded reason/protocol and authority/retrieval/audience residuals. **PC07.01 PARTIAL; inventory unchanged: Prescribing PC04.01/.06/.07/.09/.14, CPP PC07.01, Scheduling PC09.03 — 7 IDs / 3 packages.** No deferred requirement status changed.

## 12. Work record

- **Branch:** `main`, verified read-only; no switching or Git mutation.
- **Changed by this step:** only `docs/certification/280-step203-de09-034-reason-code-assessment.md`.
- **Automated execution:** none; mapped existing tests and retained their passing evidence without reruns.
- **Manual-only consideration:** clinical source/workflow suitability feedback; no manual saves/test session performed or demanded as a substitute for automatable checks.
- **Preservation:** pre-existing modified/untracked application, SQL, tests, configuration and documentation left intact; no shared/production database or external service access.

Stop after this assessment and documentation. No implementation, new test code or new certification closure is claimed.
