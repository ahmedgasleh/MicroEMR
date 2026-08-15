# Preliminary OntarioMD gap map

This is a coarse current-state map, not a requirement-level assessment and not a certification conclusion. Only the requested preliminary classifications are used.

## Foundation

### CDS-S 5.1

| Classification | Preliminary finding | Evidence boundary |
|---|---|---|
| LIKELY COVERED | Repository has core domains for demographics, allergies, medications, problems, vitals, encounters, results, documents, referrals, tasks, and scheduling. | See controllers, DTOs, repositories, tables, and procedures in the current-state inventory. |
| PARTIAL | Structured fields and lifecycle workflows exist, but terminology systems, mandatory field/cardinality mapping, provenance, and consistent audit coverage are not established. | Detailed CDS-S data-element mapping is pending. |
| LIKELY MISSING | Immunization domain and general CDS-S export were not found. | Repository-wide domain/interface search. |
| NEEDS DETAILED REQUIREMENT REVIEW | Every CDS-S 5.1 element, vocabulary, validation, relationship, export representation, and conformance test. | Certification specification and runtime/database samples required. |

### Data Migration 5.1

| Classification | Preliminary finding | Evidence boundary |
|---|---|---|
| LIKELY COVERED | Deployment schema migration ledger/manifest and tenant provisioning exist. | `SchemaMigration`, manifest, `MicroEMR.DatabaseTool`; this is not patient-data migration evidence. |
| PARTIAL | Appointment CSV export shows a narrow export mechanism. | Report controller/API and repository. |
| LIKELY MISSING | General clinical import/export, attachments migration, mapping, validation, reconciliation, exception reporting, and source-to-target controls were not found. | No relevant application domain located. |
| NEEDS DETAILED REQUIREMENT REVIEW | Migration methodology, supported formats, trial conversions, counts/totals, sign-off, security, and retention of migration evidence. | Product plus vendor/process evidence required. |

### Hosting 1.3

| Classification | Preliminary finding | Evidence boundary |
|---|---|---|
| LIKELY COVERED | HTTPS-oriented authentication, indirect database secrets, health check, tenant database identity checks, logging, and configuration separation exist in code. | Startup, tenancy, connection factory, configuration. |
| PARTIAL | Local file storage and development certificates/configuration need production-grade operational counterparts. | Repository only identifies abstractions/defaults. |
| LIKELY MISSING | No repository evidence of an application-managed backup/DR capability; this must not be treated as a missing product feature without hosting scope confirmation. | Hosting responsibility may sit in cloud operations. |
| NEEDS DETAILED REQUIREMENT REVIEW | Architecture, Canadian residency, availability, capacity, monitoring, patching, vulnerability management, backup, restore, RPO/RTO, DR tests, support, change control, and subcontractors. | Cloud/operational/vendor evidence required. |

### Privacy & Security 2.1

| Classification | Preliminary finding | Evidence boundary |
|---|---|---|
| LIKELY COVERED | OIDC/JWT authentication, tenant membership validation, permission policies, tenant database isolation checks, centralized clinical actor resolution, and mutation audit mechanisms exist. | Auth/API/Web/Application/Infrastructure and SQL evidence. |
| PARTIAL | Audit appears distributed and access-profile enforcement is layered, but completeness, read audit, patient-level controls, session security, and production settings are unproven. | Detailed route/control matrix and runtime tests pending. |
| LIKELY MISSING | Consent/care-team/break-glass controls and audit review/export tooling were not found. | Repository-wide searches and inventory. |
| NEEDS DETAILED REQUIREMENT REVIEW | PIA/TRA, policies, training, incident/breach response, access reviews, logging/retention, encryption, secure development, penetration testing, and evidence packages. | Technical plus vendor/process/operational evidence required. |

## Functional

### Primary Care Baseline 5.5

| Classification | Preliminary finding | Evidence boundary |
|---|---|---|
| LIKELY COVERED | Patient search/chart, demographics, core chart lists, encounters, documents, files, scheduling/history, referrals, results review, tasks, user administration, clinic profile, and a report exist. | Concrete implementation summarized in inventory. |
| PARTIAL | Medication list is not prescribing; generic results are not laboratory management; indicators are not a notification centre; reporting and configuration are narrow. | Visible implementation boundaries. |
| LIKELY MISSING | Billing, immunization, prescribing, laboratory management, broad external interfaces, and general import/export were not found. | Repository-wide search. |
| NEEDS DETAILED REQUIREMENT REVIEW | Workflow details, validations, coded data, clinical safety, printing, correction/history, usability, concurrency, and evidence for every Baseline 5.5 requirement. | Specification mapping and runtime scenarios required. |

### CDM 4.4

| Classification | Preliminary finding | Evidence boundary |
|---|---|---|
| LIKELY COVERED | Generic problems, medications, vitals, results, tasks, alerts, encounters, templates, and reporting primitives could hold portions of CDM data/workflow. | Core clinical domains exist. |
| PARTIAL | Template and task capabilities may support configured workflows, but no disease-specific implementation was identified. | No inference is made from generic primitives alone. |
| LIKELY MISSING | Disease registries, CDM flowsheets, guideline prompts, recalls, target tracking, and disease/population reports were not found. | Repository-wide CDM searches. |
| NEEDS DETAILED REQUIREMENT REVIEW | Every CDM 4.4 disease/workflow/data/report requirement and any clinic-configured templates outside source. | Runtime configuration, sample patients, and certification scenarios required. |

