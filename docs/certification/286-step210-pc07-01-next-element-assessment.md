# Step 210 — PC07.01 next prescription element assessment

2026-10-27 (America/Toronto). **Recommend DE09.035 — Protocol Identifier as the next bounded local implementation candidate, conditional on owner approval of its supplied-reference contract. Repository implementation is MISSING. No mandatory external service is identified by the mapped row. DE09.034 remains SATISFIED — VERIFIED WITH QUALIFICATIONS; PC07.01 PARTIAL; inventory 7 mandatory IDs / 3 packages.** Assessment only.

## 1. Preservation and authority

Read-only Git status confirms **`feature/step-206-de09-034-audit-evidence`**. Existing work consists of the modified reason-coding audit test and untracked reports 282–285. All are preserved; targeted preflight/final hashes show no changes. The target report did not exist before creation. No branch switch, Git mutation, source/test/SQL/configuration edit, build, test, database operation, clinical request, service operation or external research/correspondence occurred. Only this report was added; ignored `.tmp/step210/` holds preservation hashes.

Authority/mapping chain:

- [Step 115](150-step115-pc07-01-medication-de09-requirement-resolution.md), section 2 and its complete 36-element ledger: authoritative dictionary definitions/support/storage/conformance mapping.
- [Step 136](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md), sections 3–4: consolidated element/source/audience coverage; [Step 161](228-step161-pc07-01-remaining-gap-prioritization.md), section 3: remaining medication slices.
- [Step 185](261-step185-remaining-mandatory-gap-prioritization.md), PC07.01 section, and [Step 190](266-step190-independent-gap-implementation-assessment.md): later seven-ID inventory and dependencies. Older ten-ID counts are historical.
- Later closures supersede old missing-field descriptions: [Step 193](270-step193-de09-033-acceptance-closure.md), [Step 198](275-step198-de09-031-closure-de09-030-assessment.md), [Step 202](279-step202-de09-030-automated-acceptance.md), and [Steps 204](281-step204-de09-034-reason-code-implementation.md), [206](283-step206-de09-034-provenance-and-audit-evidence.md), [207](284-step207-de09-034-local-runtime-verification.md), [208](285-step208-de09-034-ui-and-recovery-confirmation.md).

Fresh authoritative check was limited to relevant workbook rows, using local ZIP/XML reads of [CDS-S Data Dictionary v2.1 Final](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), published 2026-06-05, `Data Dictionary` rows 131, 153, 156–161. No whole-repository assessment or completed implementation investigation was repeated. Accepted-release/applicability qualifications remain.

## 2. Concise remaining-element ledger

Definitions below are exact dictionary E-cell wording; the two paragraphs in DE09.027 are joined with a space. All rows are **mandatory product support / S (store)**. Mandatory support does not prescribe a known value on every order or a particular UI workflow. This table covers the established residual lane and its recently closed neighbors, not a reassessment of every previously accepted DE09 element.

| Element | Exact definition | Current status / evidence | Remaining gap / dependency |
|---|---|---|---|
| DE09.005 | The DIN extracted from the EMR's Drug Database for the medication selected by the provider for the purpose of prescribing a medication. | **PARTIAL — IMPLEMENTED** container; source-backed capability **DEFERRED due to external dependency**. Steps 115/136/185/190. | Arbitrary product-identifier pair does not establish DIN extraction/provenance. Drug-source/catalogue dependency retained; H131 conformance Mandatory. |
| DE09.027 | Indicates if a paper copy of the prescription must be viewed before the medication can be dispensed. Whether or not an electronically retrieved prescription is authoritative all by itself is a notion that must be explicitly stated. | **PARTIAL — IMPLEMENTED** restricted paper Yes / Not recorded. Step 161; 0089/0090 implementation summarized in Step 206 lineage. | Wider authority/No applicability and source/audience policy unresolved; governing policy dependency, not a new field-only task. |
| DE09.030 | Identifies the physical location of a dispensing service location and also allows for the location to be contacted. | **SATISFIED — VERIFIED WITH QUALIFICATIONS**, supplied-address scope. Step 202. | Retain address/source/live/recovery qualifications; do not count .032 identity again as an address defect. No mandated terminology service in this row. |
| DE09.031 | The name assigned to the dispensing service location. Used for human communication, and for cross-checking of the Targeted Dispensing Facility - Service Location Identifier. | **SATISFIED — VERIFIED WITH QUALIFICATIONS**, supplied-name scope. Step 198. | Verified registry identity remains separate .032 work; no automatic lookup claimed. |
| DE09.032 | Unique identifier for a dispensing location. Allows for lookup and retrieval of detailed information about a specific service location. Also ensures unique identification of service location. | **MISSING** source-qualified identity/lookup capability. Steps 115/136/190 and retained later residuals. | Approved unique namespace, authoritative location details and lookup/retrieval source unestablished. External service not explicitly mandated, but no usable local source is established. |
| DE09.033 | The earliest date and time on which the dispensed medication is expected to be picked up. | **SATISFIED — VERIFIED WITH QUALIFICATIONS**. Step 193. | Preserve Day/Minute/time-zone and native-prescription scope qualifications; no new external dependency. |
| DE09.034 | A coded form of the problem that is the reason for the current prescription. Facilitates cross-checking the use of a therapy against its indication. | **SATISFIED — VERIFIED WITH QUALIFICATIONS**. Steps 204/206–208. | Supplied-code scope, membership unverified; H160 conformance Optional. Not selected again. |
| **DE09.035** | **A unique identifier for a specific protocol or guideline which the prescription has been written in accordance with. Enables the communication of a reference to a protocol, study or guideline id that is specific to the jurisdiction.** | **MISSING**. Steps 115/136/190 and the targeted source findings below. | No per-order protocol reference. Need approved source/namespace/jurisdiction semantics and capture policy; no required external lookup/service identified. |

Status distinctions are intentional: **SATISFIED — VERIFIED** requires unqualified evidence; **SATISFIED — VERIFIED WITH QUALIFICATIONS** preserves bounded closures; **PARTIAL — IMPLEMENTED** is incomplete capability; **MISSING** is absent mapped functionality; **DEFERRED due to external dependency** identifies the blocked source-dependent portion. None of the qualified rows above is upgraded to unqualified status.

The cross-cutting PC07.01(f)/PC07.06 source/ownership/full-retrieval/audience correlation remains partial under the consolidated mapping. It is not an invented DE09 element or an additional inventory ID. DIN's shared prescribing/CPP dependency is likewise not counted twice. Other accepted categories/elements retain their existing evidence and are not reopened.

## 3. Candidate comparison and recommendation

| Candidate | Importance and local feasibility | Complexity / dependencies | Change, testing and workflow risk |
|---|---|---|---|
| **DE09.035 protocol reference** | Retains the actual jurisdiction-specific protocol reference associated with an order; useful independent storage slice. | Low–medium implementation scope after local source/capture decisions. No mandated terminology service, CCDD or DHDR integration in the row. | One additive migration anticipated; existing Draft/correction/audit patterns and disposable tests fit. Medium risk: false clinical/protocol assurance or accidental inherited-reference changes. |
| DE09.032 dispensing location identity | Important destination disambiguation; definition also enables detail lookup/retrieval. | Medium–high until an approved location source/namespace and usable lookup exist. A typed pair alone cannot prove all of that capability. | Persistence plus source-resolution contract; tests need independent source examples. Higher wrong-destination risk. |
| DE09.005 DIN/source | Important drug identity and shared safety foundation. | External/source-dependent catalogue capability remains deferred; arbitrary strings are insufficient. | Broader selection/provenance scope and meaningful source-backed fixtures; higher prescribing risk. |
| Wider DE09.027 authority | Important dispensing-authority distinction. | Governing applicability/actor policy unresolved; restricted slice already works. | Avoid exposing an unsupported authoritative/No assertion; high policy risk. |

**Select only DE09.035.** It can be implemented and mechanically tested locally without external correspondence/integration, after owner contract approval. Availability of a genuine approved protocol reference is not yet established; supplied opaque synthetic identifiers prove mechanics only. If the owner requires live lookup, content validation or clinical protocol-compliance checking, obtain that precise source/contract and re-scope rather than invent it.

Source classification: row **161**, A161 `DE09.035`, D161 `Protocol Identifier`, C161 **M**, F161 **S**; G161/H161 are **blank**. A named terminology and code-set conformance are **not specified**. Blank H is not labeled Optional; DE09.034's explicit Optional conformance must not be copied into this row. Cardinality, namespace, wire/SQL type, bounds, version handling and mandatory per-record entry are not specified.

## 4. Existing implementation assessment

Targeted repository findings, not a live database attestation:

| Layer / inspected evidence | Finding and reusable behavior |
|---|---|
| `PatientPrescriptionModels.cs` | Existing request/response, presence-aware reason/name/address/pickup fields, Draft validation and service correction mapping. **No protocol-reference property, presence contract or validation**. Product identifiers and ReasonCoding have different clinical meanings. |
| `PatientPrescriptionRepository.cs`; API prescription controller/Swagger configuration | Existing stored-procedure parameter/read mapping, safe validation, trusted actor and thin API boundaries. **No protocol transport, parameter or response mapping**. |
| Tenant manifest and 0096 procedure definitions | Repository migration sequence currently ends at 0096. Six latest read/create/update/finalize/sign definitions have no protocol-reference contract; available schema additions do not provide protocol storage. No inference from an absent UI field alone. No persistent database query was performed. |
| 0096 SQL safeguards | Existing patient/tenant-scoped reads, prescriber/state checks, locked correction inheritance, eight-byte RowVersion, transactional complete-row audit snapshots and finalization artifacts. Reusable safeguards; adding protocol values still requires explicit mappings/assertions. |
| Web prescription controller, `_Form`, Create/Edit/full Detail | Existing binding/serialization, optional entry, encoded retrieval, clear/review and safe error patterns. **No protocol controls or full Detail projection**. No new navigation is needed for a standalone prescription-field slice. |
| Reason-coding contract/Web/scope tests | Reusable disposable SQL/API, Razor/browser, modal serialization, audit rollback and output fixtures. **No protocol-specific assertion** in the inspected paths. Passing reason assertions do not verify future protocol fields. |

**Overall: MISSING**, with strong existing infrastructure. Correction carry-forward, review, audit and prospective frozen history exist for other fields; protocol behavior is not implicitly implemented by them. No database change or persistent migration application is authorized by this assessment.

## 5. Proposed bounded contract — owner approval required

Only the exact field meaning and **mandatory support / store** classification are sourced mandates. Security/history rules below are existing MicroEMR conventions. All new representation, cardinality, bounds, review policy and UI choices are proposed engineering decisions.

| Concern | Proposed bounded behavior |
|---|---|
| Meaning and identity | Reference the specific jurisdiction-relevant protocol/study/guideline followed for this order. Never derive it from Indication, ReasonCoding, DIN, patient IDs or artifact IDs. No claim that protocol content/compliance has been checked. |
| Cardinality | One optional reference group per prescription, unknown/not applicable represented by null. Mandatory support does not require a fabricated value on every order. Re-scope if multiple protocols are required. |
| Representation | Proposed `protocolReference: { system, identifier }`, both required when known. `system` identifies the approved source/namespace including its jurisdictional context; `identifier` is the exact supplied ID. No default namespace or guessed jurisdiction. |
| Storage/type | Two dedicated nullable `nvarchar(200)` components, atomic all-null/complete CHECK, plus nullable independent correction-review state if approved. Async DTO/Application/Infrastructure/SP pattern; no EF migration or protocol master/directory. Same protocol can be referenced by many orders: **do not impose per-order-table uniqueness on the protocol value**. |
| Validation/bounds | Proposed 200 UTF-16 units per component after existing agreed boundary trimming. Valid single-line Unicode; reject controls/line separators/unpaired surrogates before trim. Preserve case, punctuation and internal characters; no truncation, case folding or invented ID regex. These are engineering limits, not dictionary limits. |
| Create/Edit | Optional on new Draft; only authorized original-prescriber Draft mutation under existing controls. No automatic protocol selection or clinical attestation. Preserve other fields. |
| Omission | Omitted group creates unknown on a new Draft and preserves existing group on unrelated updates. Keep omitted distinct from explicit null through API/Web/repository/SQL. Legacy clients must not clear new data. |
| Clear/replacement | Explicit null clears both components atomically; complete object replaces both. Partial/empty/malformed groups reject without mutation. UI uses an explicit whole-group clear. JSON duplicate/unknown/nontext components reject. |
| Audit | Existing transaction logs complete before/after protocol components and any saved review with trusted actor/patient/tenant association. Failure rolls back values and RowVersion. No separate unaudited update path. |
| Concurrency/security | Existing current eight-byte RowVersion, prescribing permission, trusted actor, prescriber ownership, patient/tenant scope and antiforgery remain mandatory. No client-supplied actor/tenant authority. |
| Correction | Exact locked carry-forward, preserving the original reference without resolving a newer guideline/version. Propose independent saved review for known inherited/current protocol, invalidated by changes/clear; enforce both finalization paths. This review gate is local policy, not E161 wording. |
| Finalized/history | No direct finalized edits or historical backfill/artifact rewrite. Freeze exact protocol pair/review in prospective versioned artifact metadata for both finalizers. Preserve originals and existing audited supersession behavior. |
| UI | Small optional fieldset in existing standalone Create/Edit; encoded full Detail with Not recorded. Separate from reason coding/Indication. Display as text; no automatic URL fetch, external navigation or new directory. Verify actual host/modal behavior before claiming modal UI coverage. |
| PDF/output | Keep existing PDF/list Actions/print/transmission unchanged; renderers ignore prospective metadata. S alone does not mandate new output placement or an exchange integration. |
| Migration | One additive tenant migration is anticipated. Verify then-current next sequence and SP definitions in the implementation step; **no migration number is reserved** and no historical migration is edited. |

If an approved source requires a separate version component, resolve that before implementation. Do not silently substitute a current release, append a guessed version or claim that the two-component proposal meets an unidentified source's identity rules.

## 6. Required owner decisions

Approve or revise: actual source/namespace and jurisdiction meaning with representative real IDs; supplied-reference capture versus verified selection; one optional group and unknown/applicability policy; exact two-component/version contract and 200/200 limits; omission/null/empty semantics; independent saved correction review; prescribing/full-Detail audience and unchanged PDF/transmission scope. Clinician input should establish who records the reference and how they know the prescription follows that protocol.

These are not requests to approve already-settled dictionary wording or existing security rules. No approval is inferred from this assessment. A required unavailable lookup/reference dataset is a re-scope condition, not permission to invent protocols or integrate external services.

## 7. Automated acceptance plan — no execution

Existing test names below identify reusable assertions/fixtures for their **current fields**. All protocol-specific behaviors in the third column remain uncovered.

| Scenario | Existing reusable evidence | Genuine focused additions proposed |
|---|---|---|
| Pair/Unicode/JSON | `UnicodeBoundaryTrimPreservesCaseAndInternalCharacters`, `EveryComponentCountsUtf16WithoutTruncation`, `JsonPresenceNullCompleteReplacementAndDuplicateValidation` | Protocol omission/null/complete pair, empty/partial/type/duplicate rejection; each exact bound/over-limit; valid supplementary/case/punctuation; invalid controls/surrogates |
| Persistence and audit | `ApiAtomicReplacementOmissionClearAuditAndIndicationIndependence`, strengthened Step 206 | Protocol create/fresh GET, preserve, exact replace/clear and both populated old/new snapshots; trusted actor and patient/tenant association; unchanged reason/Indication |
| SQL boundary and security | `WideRawProcedureTrimsAtBoundaryAndRejectsLongGroupsBeforeAnyWrite`, `SecurityConcurrencyAuditRollbackAndSqlConstraints` | Wide raw protocol parameters/CHECK; no mutation on invalid, forbidden, wrong owner/patient/tenant, stale/finalized update; audit failure rolls back protocol/token |
| Correction/history | `CorrectionCarryReviewResetFinalizationSnapshotsAndArtifacts`, both finalizers | Exact protocol carry-forward, independent saved review/reset/clear gates, unrelated save preservation, full new snapshot equality, old original/artifact preservation, uncoded-source legacy parameter compatibility |
| Web/rendering | `ProductionRazorEncodingEntryEditClearAndResponsiveLayout`, `DetailAbsentIsNotRecordedAndOptionalsAreOmitted`, `WebPostBindsAtomicPresenceBeforeValidation`, `ModalEditIncludesExactGroupAndIndependentReviewState` | Actual protocol field/model binding, encoded Detail/Not recorded, validation redisplay, exact modal JSON, no stale state, long/Unicode values at existing desktop/narrow widths |
| Frontend | Existing eight-case reason Node fake-DOM harness | Protocol pair/Unicode/bounds, explicit whole clear, review invalidation, modal reset/load race and redisplay; preserve reason/Indication controls |
| Output/identity | `ProductionPrescriptionListActionsDoNotChangeWithReasonOrReview`, `LegacyOutputAndSignedPdfLayoutAreUnchangedByReasonSnapshot` | Protocol-specific Actions/PDF equality and frozen metadata; same reference reusable on multiple orders; same identifier under different systems remains distinct |

Reuse `ReasonCodingDatabase`/existing prescription fixtures and supported Razor/browser/Node infrastructure; no new testing framework. Future SQL behavioral checks require only authorized UUID-named disposable databases, never persistent development fixtures by default. Map cases before adding tests, avoid duplicating shared assertions, run only affected focused tests during implementation, and broaden testing only for demonstrated affected boundaries. No exact future test count is promised.

Synthetic references establish mechanics, security and history, not actual protocol authority, applicability or clinical compliance. Reserve human feedback for source/workflow suitability and subjective usability; do not require repeated manual saves to reproduce automated assertions.

## 8. Effort, retained status and Step 211

Conditional estimate after approval: **16–24 active engineering hours** (roughly 2–3 working days): contract/DTO 2–3; Application validation/presence 2–3; SQL/repository/audit/correction 4–6; UI/TypeScript 2–4; focused tests/review/report 6–8. Excludes approval waiting, protocol dataset governance/integration, clinical feedback, persistent migration/runtime activation and recovery work. Re-estimate for lookup, multiple protocols, source-specific version rules or output expansion.

Risks: misidentifying a jurisdiction/reference; implying verified clinical compliance; losing a pair through old-client omission; stale correction review; cross-scope mutation; rewritten history. Proposed explicit source policy, atomic SP contract and focused safety tests address mechanics without weakening controls. DE09.032/DIN/wider authority and cross-cutting owner/audience residuals remain separate.

Retain **DE09.034 SATISFIED — VERIFIED WITH QUALIFICATIONS**: historical 49/8 focused and 349/40 reconciled regression PASS, strengthened audit 1/1 PASS, Step 207 runtime VERIFIED/API PASS/0096 APPLIED. Authenticated Create/Edit/Detail visibility stays DEFERRED, recovery confirmation CONDITIONAL, terminology membership unverified, six NU1900 warnings retained, and future Auth restarts require protection against automatic migration/seeding. Step 209 medical-staff/owner observations need not complete before this assessment or approved independent local work.

**PC07.01 PARTIAL; mandatory-gap inventory unchanged at 7 IDs / 3 packages.** Selecting a child element neither closes PC07.01 nor reduces the inventory.

**Proposed Step 211:** approve/revise the protocol-reference contract and, only once approved, implement the bounded DE09.035 slice on **`feature/step-211-de09-035-protocol-identifier`**. Before any application/SQL/migration/test edit, inspect and protect the current worktree, safely create/verify that dedicated branch, and verify the migration sequence. Stop for owner instructions if preservation/switching is unsafe or a material source/contract decision remains unresolved. No branch, implementation or persistent activation is authorized/performed by Step 210.

Only this report was added; final read-only status/whitespace and targeted preservation checks completed. Stop after assessment and documentation.
