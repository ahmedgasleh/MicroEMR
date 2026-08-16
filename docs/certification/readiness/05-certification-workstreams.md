# Certification Workstreams

| Workstream | Can begin now | Blocked / gate |
|---|---|---|
| A — OntarioMD engagement/source acquisition | Confirm application status, release, request packages/forms/scenarios and submit interpretation questions. | Detailed PC03/PC04/PC08/PC10 design waits for answers. |
| B — Foundation/security/hosting readiness | Architecture evidence, authorization/tenant/patient test inventory, audit coverage, PIA/TRA planning, backup/DR/monitoring/support policies. | Formal clause mapping waits for exact Hosting/P&S packages and production hosting decisions. |
| C — Existing-core validation evidence | Execute demographic, file, CPP, encounter, scheduler and referral runtime backlogs; collect screenshots/API/DB/audit traces. | Final acceptance mapping waits for validation scripts. |
| D — Missing major modules | High-level product roadmap for billing, labs, immunization, prescribing/safety, CDM and Data Migration export. | Detailed implementation waits for exact applicable requirements; do not speculate. |
| E — EHR connectivity | Inventory future DHDR/DHIR/OLIS/HRM architecture and dependencies only. | Stage 4 scope/material and Stage 3 readiness. |

## High-confidence broad gaps

- No complete OHIP billing subsystem.
- No laboratory-management subsystem.
- No structured immunization domain.
- No prescription authoring/renewal/printing or medication-safety engine.
- No CDM 4.4 implementation established.
- No certification-grade Data Migration 5.1 export/import capability established.
- No complete immutable referral-letter artifact.
- No unified cross-domain encounter-document chronology.

These findings justify planning, not invented detailed schemas.

## Safe near-term engineering

- Expand automated negative authorization, tenant-isolation and patient-isolation coverage.
- Complete audit-coverage and concurrency inventories.
- Execute existing runtime certification backlogs and assemble repeatable test data/evidence.
- Document deployment, backup/restore, monitoring and release procedures once production decisions exist.

## Blocked engineering

- PC03 clinical schema/forecasting.
- PC04 prescription and interaction semantics/provider selection.
- PC08.02 shared-contribution provenance and PC08.07 multipart grouping.
- PC10 letter/tracking/reminder semantics.
- Certification-version Data Migration/CDS-S mapping without exact 5.1 packages.

