# Step 27 — Structured Prescribing Design

## Purpose and evidence boundary

This is an analysis and design artifact only. It does not authorize prescribing implementation. The certification baseline is `PCON-2024-02`, including Primary Care Baseline 5.5 and CDS-S 5.1.

Repository-held PC04 material consists of `primary-care/PC04-medication-management.md`, `primary-care/step10-pc04-analysis.md`, the source-gap inventory, readiness summaries and the current implementation. The repository does **not** contain the complete PC04 identifiers, exact clauses, definitions, data dictionary, value sets, validation notes, or CDS-S 5.1 package. Therefore no candidate field below can honestly be labelled `REQUIRED BY AVAILABLE SPEC`; every certification assertion dependent on those sources is **NEEDS SPECIFICATION INTERPRETATION**. This document makes safety-oriented product recommendations, not invented OntarioMD wording.

## Current medication domain — verified from code

The implemented path is Patient Chart/Web → authenticated API → Application service → Infrastructure repository → tenant SQL stored procedures.

`dbo.PatientMedication` has an internal identity key and unique `MedicationUid`, `PatientUid`, medication name, strength, dosage form, route, directions, frequency, start/end dates, indication, free-text prescriber name, notes, status, created/updated actor and timestamps, and `RowVersion`. Patient association is by `PatientUid`; procedures reject absent/soft-deleted patients. Indexes support patient/name and patient/status lookup.

The governed behavior is small:

- create produces an `Active` row;
- edit overwrites the mutable list fields and checks `RowVersion`;
- discontinue retains the row, changes status to `Discontinued`, supplies an end date when absent, and optionally records a reason in audit;
- there is no physical-delete route;
- reads require `Patients.View`; mutations require `ClinicalData.Manage`;
- mutation procedures write Create, Update and Discontinue entries to the tenant `AuditLog` in the same transaction;
- the authenticated subject is centrally resolved to a tenant-local `ApplicationUser` actor; `CreatedByDisplayName` is also captured;
- API routes list by patient, get details by medication UID, create, update and discontinue;
- the Patient Chart medication tab lists the records and links to create, view, edit and discontinue UI.

This is best understood as a **medication reconciliation/longitudinal list**, not a medication order or prescription. Its directions and prescriber text make it superficially mixed, but it lacks the authority, quantity, repeats, issuance, immutability and artifact semantics required to claim that a prescription was issued. Existing behavior must remain unchanged.

## Separation of clinical concepts

| Concept | Question answered | Step 27 decision |
|---|---|---|
| Medication List | What medication is the patient taking or recorded as taking? | Keep `PatientMedication` authoritative and mutable through reconciliation. |
| Prescription | What did an authorized prescriber order, with directions, amount and authorization? | Add a separate domain in a future implementation. |
| Dispense | What did a pharmacy actually dispense? | Separate future domain/integration; not inferred from a prescription. |
| Administration | What dose was actually administered? | Separate future domain; out of scope. |

Only a local structured Prescription belongs in Step 27A. Dispense, administration and transmission do not.

## Separate prescription aggregate

A separate `PatientPrescription` is recommended. Extending `PatientMedication` would make a mutable current-state list row double as historical evidence of an issued order, conflate discontinuation with cancellation, and make correction, printing and multiple prescriptions for one medication ambiguous.

The cost is an additional domain/API/table and an explicit reconciliation boundary. That cost is justified by clearer provenance and preserved history. A prescription may later have a nullable link to a medication-list record, but the initial prescription must remain independently meaningful.

## Candidate field classification

`REQUIRED BY AVAILABLE SPEC` applies to none because exact PC04/CDS-S text is unavailable.

| Candidate | Classification | Decision/rationale |
|---|---|---|
| PrescriptionUid | CLINICALLY JUSTIFIED MVP | Stable public identity and import/export correlation. |
| PatientUid | CLINICALLY JUSTIFIED MVP | Mandatory patient ownership. |
| MedicationUid | OPTIONAL LATER | Nullable reconciliation link only; no automatic list mutation in 27A. |
| Drug/product name snapshot | CLINICALLY JUSTIFIED MVP | Required bounded display identity; never silently rewritten by a catalogue. |
| Drug identifier system/value/display | NEEDS SPECIFICATION INTERPRETATION | Migration-safe nullable slots are recommended, but the required terminology is unknown. |
| Strength value/unit and strength display | CLINICALLY JUSTIFIED MVP | Separate product strength from administered dose; allow display for products not expressible as one value/unit. |
| Dosage form | CLINICALLY JUSTIFIED MVP | Bounded display value; terminology remains unresolved. |
| Dose amount | CLINICALLY JUSTIFIED MVP | Numeric amount when the SIG can be represented this way. |
| Dose unit | CLINICALLY JUSTIFIED MVP | Required when dose amount is present; not the strength unit. |
| Route | CLINICALLY JUSTIFIED MVP | Bounded display value; governed value set is interpretation-dependent. |
| Frequency code/display | CLINICALLY JUSTIFIED MVP | Small governed application codes plus frozen display; exact values need clinical approval. |
| Duration value/unit | OPTIONAL LATER | Not safely applicable to every prescription; directions and dates cover the first slice. |
| Quantity | CLINICALLY JUSTIFIED MVP | Positive `decimal(18,3)` prescribed amount, distinct from dose. |
| Quantity unit | CLINICALLY JUSTIFIED MVP | Paired with quantity. |
| Authorized repeats | CLINICALLY JUSTIFIED MVP | Non-negative integer, default zero; not a renewal workflow. |
| PRN flag | CLINICALLY JUSTIFIED MVP | Explicit Boolean; never inferred from text. |
| Indication text | CLINICALLY JUSTIFIED MVP | Optional bounded clinical context. |
| ProblemUid | OPTIONAL LATER | Nullable linkage may help later; do not force diagnosis linkage. |
| Rendered/free-text directions (SIG) | CLINICALLY JUSTIFIED MVP | Required human-readable instruction and frozen rendering. |
| Start date | CLINICALLY JUSTIFIED MVP | Optional when different from prescribed date. |
| End date | OPTIONAL LATER | Optional; cannot be inferred from quantity or repeats. |
| Prescribed date/time | CLINICALLY JUSTIFIED MVP | Set by the server on finalization. |
| Prescriber user/provider IDs | CLINICALLY JUSTIFIED MVP | Current tenant identities required for a newly issued MicroEMR prescription. |
| Prescriber display/credential snapshots | CLINICALLY JUSTIFIED MVP | Reproducibility after profile changes; exact credential fields need interpretation. |
| Status | CLINICALLY JUSTIFIED MVP | Governed lifecycle below. |
| Notes | OPTIONAL LATER | Avoid putting authorization or SIG essentials in notes. |
| CreatedBy/CreatedAtUtc | CLINICALLY JUSTIFIED MVP | Data-entry provenance. |
| UpdatedBy/UpdatedAtUtc | CLINICALLY JUSTIFIED MVP | Draft provenance. |
| FinalizedBy/FinalizedAtUtc | CLINICALLY JUSTIFIED MVP | Authorization provenance. |
| CancelledBy/CancelledAtUtc/reason | CLINICALLY JUSTIFIED MVP | Non-destructive cancellation evidence. |
| SupersedesPrescriptionUid | CLINICALLY JUSTIFIED MVP | Trace a corrected replacement without overwriting the issued record. |
| RowVersion | CLINICALLY JUSTIFIED MVP | Optimistic concurrency for state transitions. |
| Pharmacy/destination | NOT JUSTIFIED | No evidence that local record creation requires a destination. |
| Dispense/administration data | NOT JUSTIFIED | Different clinical events and domains. |

## Drug identity and dosing model

Free text alone is weaker than desirable for prescribing and future CDS, but the repository provides no evidence for choosing DIN, Drug Product Database identifiers, RxNorm, SNOMED CT, or another standard. Step 27A should use a required bounded product-name snapshot and migration-safe optional `IdentifierSystem`, `IdentifierValue`, and `IdentifierDisplay` fields. Production activation should not claim coded prescribing or CDS until OntarioMD terminology requirements and a maintained source are approved. A large internal drug catalogue is not part of 27A.

Strength and dose remain separate. For example, `500 mg tablet` is the product strength while `1 tablet` is the dose. The model should support a structured strength value/unit where applicable, a frozen strength display for non-simple products, and a dose amount/unit pair. It must not parse a single strength/SIG string into clinical meaning.

The first SIG model uses structured dose amount/unit, bounded route, governed frequency code plus frozen display, PRN flag, and a required human-readable directions snapshot. Structured fields may be absent where the instruction cannot safely be represented; directions remain mandatory. No full SIG parser or speculative clinical-dose bounds are justified.

Frequency should use a small, clinically approved application value set with code and immutable display, plus directions for nuance. Route can initially be bounded display text while the required terminology is resolved. Neither set should be invented from certification assumptions.

Quantity is required for a useful local issued prescription and is a positive `decimal(18,3)` with its own unit. Authorized repeats are a non-negative integer (zero allowed). Reprint/duplicate output, continuation of a medication, repeat authorization, and renewal are distinct: only the repeat count is in 27A; renewal workflow is deferred.

PRN is a separate flag. PRN still requires explicit directions and must not erase frequency or maximum-use detail when clinically applicable. Indication is optional bounded text in the MVP; problem linkage is optional later.

## Prescriber identity and authorization

For a newly finalized MicroEMR prescription, the prescriber must be the authenticated tenant-local `ApplicationUser`, linked to an active tenant `Provider`, and authorized by a dedicated server-side permission. Arbitrary prescriber text is unacceptable. Preserve both IDs and immutable display/credential snapshots.

The data-entry actor and prescriber are separate roles. `CreatedBy` records who prepared a draft; `PrescriberProviderId` identifies who ordered it; `FinalizedBy` records the authorization action. In 27A the safest contract is that an authorized prescriber creates or edits and finalizes their own draft. Non-prescriber draft preparation, delegation, queues and “sign for” behavior are **NEEDS SPECIFICATION INTERPRETATION** and deferred.

`ClinicalData.Manage` remains appropriate for medication-list maintenance but is too broad for prescribing. Add a future dedicated `Prescriptions.Prescribe` permission (and a view permission only if `Patients.View` is later found insufficient). Finalization must additionally validate active provider mapping and must never trust a client-supplied prescriber identity.

## Lifecycle, finalization and correction

The bounded lifecycle is:

`Draft → Finalized → Cancelled`

and, for correction, `Finalized → Superseded`, linked to a newly created corrected prescription. `Active`, `Completed`, and `Discontinued` are not used because they imply medication taking/dispensing knowledge the local prescription does not possess.

Only Draft is editable. Finalization is an explicit atomic command that validates authorization and required content, sets server timestamps/actors, freezes display snapshots and produces the reproducible artifact. Finalized, Cancelled and Superseded records are immutable. Cancellation preserves the original and requires actor, time and reason. Post-finalization correction cancels/supersedes the original and creates a linked replacement; it never silently edits issued history. Reprint renders the same frozen content and is not renewal.

Draft update, finalize and cancel require the current `RowVersion`; the procedure must lock/recheck state and write audit atomically.

## Medication-list relationship and history

Initial contract: **maintain prescriptions independently and require explicit reconciliation**. Finalization does not automatically create or update `PatientMedication`. This avoids duplicates and avoids assuming that “prescribed” means “patient is taking.” A later explicit reconciliation action may create/link/update a list entry and must show its effect to the clinician. A nullable medication link is therefore optional later, not a prerequisite for 27A.

Medication history and prescriptions written elsewhere should continue to be entered as medication-list records in 27A, with source captured using existing capabilities where available. Creating a historical “prescription” would falsely imply MicroEMR issuance and known authorization provenance. Historical prescription import is deferred.

## Proposed persistence design (future only)

`PatientPrescription` should contain an internal bigint key, unique `PrescriptionUid`, `PatientUid`, product identity snapshots and optional identifiers, separate strength and dose fields, form, route, frequency, directions, quantity/unit, repeats, PRN, indication, dates, prescriber user/provider IDs and snapshots, governed status, creator/updater/finalizer/canceller provenance, cancellation reason, optional supersession link, timestamps, and `RowVersion`.

Recommended indexes are unique `PrescriptionUid`; `(PatientUid, Status, PrescribedAtUtc DESC)`; and optional filtered indexes for draft work and supersession. Every detail/mutation lookup must use both `PatientUid` and `PrescriptionUid`. No hard delete is permitted.

Minimum future stored procedures:

- `PatientPrescription_GetByPatientUid`
- `PatientPrescription_GetByPatientUidAndUid`
- `PatientPrescription_CreateDraft`
- `PatientPrescription_UpdateDraft`
- `PatientPrescription_Finalize`
- `PatientPrescription_Cancel`
- `PatientPrescription_CreateCorrectedDraft` (or an atomic supersede/replacement command)

They must use transactions, patient existence checks, trusted actor parameters, row-version checks, state guards and same-transaction clinical audit.

## Validation and isolation

Server validation must require a valid patient and nonblank bounded product and directions; enforce paired numeric/unit fields; positive quantity; non-negative repeats; coherent dates; valid governed status/frequency; an active mapped provider and dedicated permission at finalization; Draft-only edits; valid state transitions; cancellation reason; and matching `PatientUid` + `PrescriptionUid`. It must not invent dose ceilings.

The API must use the trusted tenant database, centralized clinical actor, patient-scoped compound lookup and server-side permissions. No cross-tenant catalogue or prescription search is introduced.

## API design (future only)

- `GET /api/patients/{patientUid}/prescriptions`
- `GET /api/patients/{patientUid}/prescriptions/{prescriptionUid}`
- `POST /api/patients/{patientUid}/prescriptions` — create Draft
- `PUT /api/patients/{patientUid}/prescriptions/{prescriptionUid}` — edit Draft with RowVersion
- `POST /api/patients/{patientUid}/prescriptions/{prescriptionUid}/finalize`
- `POST /api/patients/{patientUid}/prescriptions/{prescriptionUid}/cancel`
- `POST /api/patients/{patientUid}/prescriptions/{prescriptionUid}/corrections` — optional within 27A if correction is implemented atomically

A separate finalize action is warranted because it is an authorized, irreversible state transition with stricter validation and artifact/audit consequences than draft update.

## Audit, read audit and provenance

Approve future mutation events `PrescriptionDraftCreated`, `PrescriptionDraftUpdated`, `PrescriptionFinalized`, `PrescriptionCancelled`, and `PrescriptionSuperseded` only. Do not add `PrescriptionRenewed` until renewal exists. Each event must retain patient, prescription, actor, transition, timestamp and sufficient before/after or immutable version references to reconstruct the decision. Mutations and audit commits are atomic.

Normal display within the Patient Chart remains covered by the existing governed `PatientChartOpened` boundary. Do not mechanically create a read event for each prescription row. Whether standalone detail, print, download or export needs a distinct disclosure/read event is **NEEDS SPECIFICATION INTERPRETATION**.

## Patient Chart and creation UX

Keep one **Medications** tab and add two clearly labelled sections/subviews: **Current Medications** and **Prescriptions**. This preserves the clinical relationship without presenting them as the same record or proliferating tabs.

The compact creation flow has Product, Directions (dose, route, frequency, PRN and rendered SIG), Quantity/Repeats, Prescriber, and optional Indication/Notes sections. It provides Save Draft, Preview and Finalize actions according to permission. It contains no pharmacy, fax, transmission, CDS or renewal controls.

## Printable artifact and existing infrastructure

A local prescription must be actionable and reproducible, so 27A should include a human-readable HTML preview and PDF/print artifact frozen at finalization. Existing clinical HTML/PDF rendering components can be reused behind a prescription-specific renderer/artifact service. The prescription remains the source clinical aggregate; it must not be converted into or edited as a `PatientDocument`. Store an immutable artifact reference/hash or canonical rendering snapshot sufficient to reproduce what was issued. Exact required content, signature representation and retention are **NEEDS SPECIFICATION INTERPRETATION**.

## External, pharmacy and CDS boundaries

A local structured prescription is not electronic transmission. PrescribeIT, fax, pharmacy APIs, messaging and delivery status are separate future integrations. No pharmacy selection, contact or directory is required for the local record without specification evidence.

CDS is also separate. Step 27A performs structural validation only—no interaction, allergy, duplicate-therapy, contraindication, formulary or dose checking. Optional namespace/value drug identifiers make future CDS possible without pretending a knowledge service exists.

## Data Migration implications

Future import/export needs stable prescription UID and patient UID, source-system identifiers, product identity snapshots/codes, all structured instruction/quantity fields, status and supersession links, created/prescribed/finalized/cancelled actors and timestamps, prescriber snapshots, artifact identity/hash and provenance indicating whether MicroEMR issued or merely imported the record. Imports must not manufacture authorization. Step 26 migration framework is unchanged.

## Specification interpretation table

| Question | Evidence | Decision | Status |
|---|---|---|---|
| Drug terminology | No PC04/CDS-S dictionary; no catalogue in product. | Snapshot plus optional namespace/value; do not select DIN/DIN-DPD/RxNorm/SNOMED yet. | NEEDS SPECIFICATION INTERPRETATION |
| Structured SIG | Current list has free text only. | MVP structured components plus frozen directions; no parser. | NEEDS SPECIFICATION INTERPRETATION |
| Quantity | Absent currently; necessary for actionable local order. | Positive decimal plus unit in 27A. | CLINICALLY JUSTIFIED MVP |
| Repeats/refills | Absent; exact semantics unavailable. | Integer authorized repeats in 27A; defer refill workflow. | NEEDS SPECIFICATION INTERPRETATION |
| Prescriber identity | Tenant users may link to Provider; current medication prescriber is text. | Active mapped provider and dedicated permission at finalization. | CLINICALLY JUSTIFIED MVP; credential rules need interpretation |
| Draft/delegation | No delegation semantics or clauses. | Prescriber-only drafts in 27A; defer staff assistance. | NEEDS SPECIFICATION INTERPRETATION |
| Finalization/signature | Encounters demonstrate finalization patterns; PC04 wording absent. | Explicit immutable finalization and actor snapshot. | NEEDS SPECIFICATION INTERPRETATION |
| Prescription artifact | Existing HTML/PDF capability; no prescription requirement text. | Frozen printable artifact in 27A for credible local workflow. | NEEDS SPECIFICATION INTERPRETATION |
| Pharmacy destination | No pharmacy domain or clause. | Exclude. | NEEDS SPECIFICATION INTERPRETATION |
| Transmission | No local evidence that PC04 mandates it. | Separate future integration. | NEEDS SPECIFICATION INTERPRETATION |
| Renewal semantics | No order history/renewal behavior. | Defer; do not infer from repeat or reprint. | NEEDS SPECIFICATION INTERPRETATION |
| Medication-list synchronization | Domains have distinct truth. | Independent prescription; explicit reconciliation later. | CLINICALLY JUSTIFIED MVP |
| CDS dependency | CDS-S 5.1 package missing; no drug knowledge source. | Preserve identity extensibility; exclude CDS from 27A. | NEEDS SPECIFICATION INTERPRETATION |
| Read-audit requirements | PatientChartOpened is current boundary; exact rules absent. | No per-row read event; assess print/export separately. | NEEDS SPECIFICATION INTERPRETATION |

## Explicit deferrals

PrescribeIT; pharmacy directory; electronic transmission; fax; interaction and allergy checking; dose decision support; drug formulary/catalogue project; renewal requests; refill authorization workflow; controlled-substance special workflows; drug inventory; external prescribing; historical prescription import; dispense and administration; advanced SIG parsing; staff delegation; and automatic medication-list synchronization.

## Exact Step 27A recommendation

Implement one future **Local Structured Prescription Foundation** slice after review of this design:

- separate patient-scoped Prescription aggregate and tenant migration;
- dedicated prescribe permission and active ApplicationUser→Provider authorization;
- prescriber-only create/edit Draft, explicit Finalize and Cancel, plus non-destructive corrected replacement;
- product snapshot with optional migration-safe identifier namespace/value;
- separate strength, dose/unit, bounded route, governed code/display frequency, PRN and required rendered directions;
- positive quantity/unit, non-negative authorized repeats, optional indication and dates;
- immutable finalized record, reproducible HTML/PDF artifact, RowVersion and atomic clinical audit;
- Patient Chart Medications tab with distinct Current Medications and Prescriptions sections;
- patient-scoped API/application/infrastructure/stored-procedure vertical slice;
- independent medication-list semantics and no automatic synchronization;
- no CDS, pharmacy, transmission, renewal or delegation.

This implementation would require a tenant clinical migration. Based on the repository maximum of `0049`, its expected number is `0050` at implementation time, subject to rechecking immediately before work. It requires no platform migration unless the permission architecture at that future point cannot seed a tenant-facing permission without one; current design expectation is tenant-only. No migration is created by Step 27.

## Review gate

Before implementation, obtain or formally interpret the full PC04 and CDS-S 5.1 material, approve prescriber credential/signature and printable-content rules, approve the bounded frequency/route values and drug identity strategy, and confirm whether quantity, repeats, delegation, print audit or transmission are certification-mandated. Until then, all exact certification mapping remains **NEEDS SPECIFICATION INTERPRETATION**.
