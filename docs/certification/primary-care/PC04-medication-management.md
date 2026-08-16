# PC04 Medication Management

## Source limitation

OntarioMD's public library identifies Primary Care Baseline 5.5 as Final, but the repository does not contain its PC04 clauses, requirement identifiers, types, definitions or validation notes, and the accessible official index does not expose them. Older specifications are not treated as equivalent.

The complete PC04 identifier set and exact wording are therefore **NEEDS SPECIFICATION INTERPRETATION**. No numbered requirement rows are invented below. This is a current-state gap analysis, not a certification conclusion.

## Requirement traceability

| Requirement ID | Requirement Summary | Requirement Type | MicroEMR Status | Existing Evidence | Exact Gap | Structured Data Needed | Workflow Needed | Drug Knowledge Dependency | UI Impact | API Impact | Database Impact | Security Impact | Audit/History Impact | External Dependency | Interpretation Issue | Recommended Slice | Size |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PC04 family — exact IDs unavailable | Exact Baseline 5.5 clauses unavailable. | NOT ESTABLISHED | NEEDS SPECIFICATION INTERPRETATION | Patient medication list supports create, edit and discontinue with patient-scoped stored procedures, permissions, audit and concurrency. | Cannot map list maintenance, prescribing, safety, history, printing or terminology to exact requirements. | See verified current fields and conditional gap matrix below. | Requirement-specific lifecycle and prescribing behavior unknown. | No catalogue or safety knowledge source exists. | Existing Medication tab is extensible; prescribing may require a distinct flow. | Current list CRUD exists; future order/safety endpoints depend on PC04. | Current single entity cannot reconstruct prescriptions or safety decisions. | Existing `Patients.View` and `ClinicalData.Manage`; future prescribing boundaries unknown. | Generic audit exists, but prior medication/order content is not reconstructable. | DHDR is separate unless PC04 explicitly links it. | Exact identifiers, wording, mandatory type and validation scenarios missing. | Specification acquisition and approved traceability matrix. | S |

## Verified implementation

The vertical slice is Patient Chart/Web → authenticated API → Application service → tenant-scoped Infrastructure repository → stored procedures → `PatientMedication`.

Structured fields are: `MedicationUid`, `PatientUid`, medication name, strength, dosage form, route, directions, frequency, start/end dates, indication, prescriber name, notes, Active/Discontinued status, created/updated actor and timestamp, and `RowVersion`.

Implemented workflows are list, details, create, edit and discontinue. Updates use optimistic concurrency. Mutations require `ClinicalData.Manage`; reads require `Patients.View`. Tenant selection comes from trusted context, and mutation procedures write generic audit entries. No physical-delete medication route was found.

## Data classification

| Concept | Classification | Finding |
|---|---|---|
| UID, patient, name, strength, form, route, directions, frequency, dates, indication, prescriber name, notes, status | EXISTING BUT NEEDS VERIFICATION | Structured and displayed, but PC04/CDS-S cardinality and terminology are unverified. |
| Created/updated actor and timestamps; RowVersion | EXISTING BUT NEEDS VERIFICATION | Supports provenance and concurrency; clinical prescriber is only free text. |
| DIN/NPN/generic/brand coding | NOT ESTABLISHED | No structured drug identifier or catalogue. Requirement status depends on PC04/CDS-S. |
| Dose value/unit distinct from strength; quantity; refills; prescribed date | NOT ESTABLISHED | No dedicated fields. Needed for conventional prescription orders, but PC04 wording is unavailable. |
| Prescription/order UID, version, signed/issued actor and immutable artifact | NOT ESTABLISHED | No prescription entity or output. |
| Pharmacy/transmission data | BELONGS TO ANOTHER SPECIFICATION unless PC04 says otherwise | No external pharmacy integration. |
| Historical/source/entered-in-error/correction | NOT ESTABLISHED | Existing status only supports Active and Discontinued. |
| Interaction/allergy alert, severity, override reason/actor | NOT ESTABLISHED | No drug knowledge or safety-decision model. |

## Prescribing, safety and history gaps

Medication-list maintenance is not prescription authoring. MicroEMR cannot produce, sign, preserve, print, renew or transmit a prescription; it does not retain quantity/refills or an immutable order version. Exact PC04 obligations remain unknown.

No drug-drug, drug-allergy, duplicate-therapy, contraindication or dose checking exists. The Allergy module should remain authoritative for allergy data, but meaningful matching requires coded medication and allergy concepts plus a maintained knowledge source. A future provider must separately supply terminology/catalogue and the safety knowledge required by confirmed PC04 rules.

The current update overwrites mutable medication content. Generic audit identifies an action and actor but does not reconstruct prior dose, directions or prescriber. Discontinuation preserves the row, but exact correction, entered-in-error, completed and renewal semantics are unavailable.

## Conditional architecture

Retain `PatientMedication` as the longitudinal medication list. If prescribing is confirmed, add a separate immutable/versioned `MedicationOrder` or `Prescription` aggregate linked to the patient, medication concept and clinical prescriber. Do not overload the current list row as both current-state summary and original prescription.

If safety checking is confirmed, define a drug-knowledge abstraction independent of DHDR and persist clinically relevant alert decisions/overrides with rule/provider version, actor and timestamp. Do not duplicate allergies; consume the existing patient allergy source.

The current Patient Chart Medication tab should remain the longitudinal list. A confirmed prescribing workflow may launch from it or an encounter, but should have a distinct order composer and preview/print step. Encounter/appointment linkage must remain optional unless required.

## API, security and external boundary

Existing list/details/create/update/discontinue endpoints should be preserved. Confirmed future capabilities may justify prescribe, renew, render/print, safety-check and override operations. Server authorization, patient binding, trusted tenant resolution, clinical actor resolution and row-version checks remain mandatory.

The medication domain is authoritative for PC07 CPP display; do not create CPP medication duplicates. DHDR retrieval/submission and provincial repository synchronization are separate EHR-connectivity concerns unless the exact PC04 clause explicitly incorporates them.

## Runtime verification backlog

- `CERT-PC04-R01`: create, edit and discontinue a medication; verify fields, status, actor, audit and persistence.
- `CERT-PC04-R02`: submit a stale RowVersion and verify rejection without overwrite.
- `CERT-PC04-R03`: attempt cross-patient and cross-tenant reads/writes and verify denial.
- `CERT-PC04-R04`: verify `Patients.View` versus `ClinicalData.Manage` enforcement through direct API calls.
- `CERT-PC04-R05`: inspect whether prior values can be reconstructed; expected current finding is technical audit only.
- `CERT-PC04-R06`: after specification acquisition, validate every confirmed prescription, print and safety scenario.

## Proposed sequence

| Slice | Scope | Size |
|---|---|---|
| Step 10A | Obtain PC04/CDS-S clauses and approve exact traceability, terminology, lifecycle and safety matrix. | S |
| Step 10B | Harden confirmed medication-list data, provenance and clinical history gaps. | M |
| Step 10C | Prescription/order foundation and immutable versioning if required. | L |
| Step 10D | Prescription composer, renewal and print workflow if required. | L |
| Step 10E | Drug terminology/provider decision and integration. | L |
| Step 10F | Confirmed drug-drug and drug-allergy safety with auditable overrides. | XL |

