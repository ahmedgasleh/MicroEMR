# Step 10 — PC04 Medication Management Analysis

## Executive summary

MicroEMR has a structured longitudinal medication list, but no prescription-order or medication-safety subsystem. Exact Primary Care Baseline 5.5 PC04 identifiers and clauses are unavailable in the repository and accessible official index, so requirement-level certification mapping is **NEEDS SPECIFICATION INTERPRETATION**. No implementation is authorized by this analysis.

## Current implementation

- Patient medication list and details in the Patient Chart.
- Create and edit medication fields including name, strength, form, route, directions, frequency, dates, indication, free-text prescriber and notes.
- Active and Discontinued states; discontinuation retains the row.
- Patient-scoped Web/API/application/repository/stored-procedure flow.
- `Patients.View` reads, `ClinicalData.Manage` mutations, tenant-local connections, audit events and RowVersion concurrency.

## Confirmed product gaps

- No structured drug identifier/catalogue or CDS-S mapping.
- No prescription/order entity, issuance/signature, quantity/refills, renewal, artifact or printing.
- No electronic pharmacy transmission.
- No drug-drug, drug-allergy, duplicate-therapy, contraindication or dose checking.
- No auditable safety override model.
- No reconstructable medication/order version history or explicit entered-in-error correction.

These are implementation findings. Their certification status cannot be assigned to numbered PC04 requirements until the source clauses are obtained.

## Architecture recommendation

Preserve `PatientMedication` as the authoritative longitudinal list used by PC07 CPP. If PC04 confirms prescribing, introduce a separate immutable/versioned order aggregate rather than turning the current mutable list row into a prescription. A drug-knowledge service should be separated into terminology/catalogue and interaction knowledge capabilities. Provincial DHDR connectivity remains separate.

The existing Medication tab should remain. Confirmed prescribing may add a focused order composer and print/preview workflow. Existing patient, tenant, permission, actor, audit and concurrency conventions should be retained.

## Allergy, CPP and DHDR boundaries

Medication-allergy checking should consume the authoritative Allergy module, not duplicate allergy records. Coded allergy and drug concepts will be prerequisites for meaningful matching. CPP consumes the medication domain. DHDR is provincial connectivity and is not treated as PC04 functionality without an explicit Baseline dependency.

## Interpretation issues

- Complete PC04 requirement IDs, wording and mandatory/optional types.
- Required medication terminology and CDS-S elements.
- Prescription content, signature, printing, renewal and transmission obligations.
- Required interaction categories, severities, overrides and audit evidence.
- Lifecycle semantics for completed, historical, cancelled and entered-in-error records.
- Required clinical history and correction behavior.
- Encounter linkage and list/history printing requirements.

## Proposed implementation sequence

1. Step 10A — source/CDS-S acquisition and approved requirement matrix (**S**).
2. Step 10B — confirmed list/history hardening (**M**).
3. Step 10C — prescription/order foundation if required (**L**).
4. Step 10D — prescribing, renewal and print UI if required (**L**).
5. Step 10E — drug terminology provider integration (**L**).
6. Step 10F — confirmed medication-safety checks and overrides (**XL**).

## Runtime evidence

Current behavior still needs runtime proof for field persistence, discontinuation retention, actor/audit attribution, stale-write rejection, permissions, patient isolation and tenant isolation. Prescription and safety scenarios must be derived from the actual PC04 validation material before testing.

## Conclusion

Obtain the Baseline 5.5 PC04 pages, validation notes and relevant CDS-S mappings before selecting drug data, designing orders or implementing clinical safety behavior.
