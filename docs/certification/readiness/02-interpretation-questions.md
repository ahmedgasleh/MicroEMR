# OntarioMD Interpretation Questions

## Blocked inventory

Seven identified requirement IDs plus the PC04 family (unknown ID count) are interpretation-blocked.

| Requirement | Known issue | Design affected | Risk of guessing | Question / disposition |
|---|---|---|---|---|
| PC03.01 | Exact clause unavailable. | Minimum immunization data/workflow. | Unsafe clinical schema. | Supply exact wording, type, validation notes and CDS-S mapping; defer. |
| PC03.02 | Exact clause unavailable. | Historical/refusal/contraindication/correction states. | Incorrect lifecycle and provenance. | Supply state definitions and correction expectations; defer. |
| PC03.03 | Exact clause unavailable. | Forecasting/reminders/reports. | Unsafe clinical recommendations. | Supply rules, terminology, effective dates and scenarios; defer. |
| PC04 family | Full ID set and clauses unavailable. | Medication data, prescriptions, safety, history and printing. | Wrong order model or unsafe alerting. | Supply complete PC04 family, types, notes and CDS-S/drug-knowledge dependencies; defer detailed design. |
| PC08.02 | “Each contributor in each part” lacks unit/visibility rules. | SOAP/template contribution storage and print. | Overbuilt versioning or inadequate authorship. | Define “part”; multiple contributors; persistence through edits; inline/print identity; whether audit is sufficient; defer. |
| PC08.07 | Multipart office-visit compilation is unclear. | Encounter grouping/authorship/output. | Incorrect visit aggregation. | Provide validation scenario and acceptable grouping model; defer. |
| PC10.01 | Required referral-letter snapshot/selection semantics unavailable. | Immutable artifact, clinical-source selection and printing. | Destructive or incomplete record. | Define required content, snapshot timing, edits, preservation and print evidence; defer. |
| PC10.02 | Referrer/date/notes and reminder semantics unclear. | Tracking fields, overdue rules and UI reminder. | Invented timing/identity rules. | Define referrer identity, letter date, notes, outstanding threshold and reminder mechanism; defer. |

## Meeting question inventory

### Certification release / versions

- Confirm PCON-2024-02 remains the applicable target and exact Stage 3 versions.
- Are applications currently paused, and what engagement/readiness path is available meanwhile?
- How should future 5.2 DFU work be tracked without affecting 5.1 validation?

### Primary Care validation

- Provide the exact Baseline 5.5 package, definitions and requirement-to-validation mapping.
- Which interpretation notes and screenshots/data artifacts are expected?

### CDS-S and Data Migration

- Provide exact 5.1 packages, dictionaries, code systems, schemas, examples and validation tools.
- Clarify whether newer DFU artifacts may inform—but not replace—5.1 work.

### Stage 3 / Stage 4

- Which scripts, setup data, attestation templates and evidence checklists follow Stage 2?
- When should Stage 4 connectivity planning begin, and which integrations are mandatory for this release?

### Hosting/security evidence

- Provide Hosting 1.3 substantiation requirements/reference sheet and Privacy & Security 2.1 evidence rubric.
- Confirm expectations for PIA, TRA, penetration testing, backup/restore, DR, monitoring and provider assurance.

