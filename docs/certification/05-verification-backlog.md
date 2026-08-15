# Certification verification backlog

Each item distinguishes source-visible product evidence from runtime, operational/cloud, vendor/process, and certification evidence.

| ID | Capability / question | Why verification is required | Likely area | Runtime? | Operational/cloud evidence? |
|---|---|---|---|:---:|:---:|
| CERT-V001 | Map every CDS-S 5.1 data element, cardinality, vocabulary, and relationship. | Similar domain names do not establish standard coverage. | DTOs, SQL tables/procedures, chart UI | Yes | No |
| CERT-V002 | Exercise patient create/edit/search including duplicates, HCN validation, inactive records, and concurrency. | Source does not prove validation or user-visible behavior. | Patients Web/API/service/repository | Yes | No |
| CERT-V003 | Test cross-patient nested-resource IDs for every chart API. | Patient UID parameters do not alone prove object ownership checks. | All `api/patients/{patientUid}` routes and procedures | Yes | No |
| CERT-V004 | Test every permission with allowed, denied, inactive-member, and override users. | Attribute presence is not complete enforcement evidence. | Web/API policy handlers; scripts `010`–`013` | Yes | No |
| CERT-V005 | Verify UI actions/navigation are hidden appropriately while direct URLs and requests are denied server-side. | UI presentation is not a security boundary. | `_Sidebar`, Web controllers, API endpoints | Yes | No |
| CERT-V006 | Verify cross-tenant denial with stale/wrong/multiple tenant claims and wrong database assignment. | Isolation must be demonstrated adversarially. | Auth claims, tenant middleware, connection factory | Yes | Yes |
| CERT-V007 | Verify writes fail when `sub` has no active tenant clinical-user mapping. | Central middleware exists; all mutation paths must be proven. | Actor middleware/accessor, `ApplicationUser` | Yes | No |
| CERT-V008 | Produce a mutation-to-audit coverage matrix for every clinical/admin change. | Audit implementation is distributed and visibly incomplete as a system claim. | All write procedures, `AuditLog`, `PlatformAuditEvent` | Yes | No |
| CERT-V009 | Determine whether patient record reads, searches, exports, and failed access are audited. | No general read-access audit was located. | Middleware, controllers, SQL, logging | Yes | Yes |
| CERT-V010 | Verify audit immutability, review/export, retention, timestamps, and actor display. | Repository cannot prove database grants or operations. | Audit tables/procedures and production logging | Yes | Yes |
| CERT-V011 | Verify encounter sign, lock, correction, addendum, history, and print behavior. | Clinical record integrity is behavior-sensitive. | Encounter controller/service/procedures/output | Yes | No |
| CERT-V012 | Verify document draft/final/version/PDF/artifact integrity and access. | Multiple persistence/output paths exist. | Document/template/output components, migrations `0031`–`0038` | Yes | Yes |
| CERT-V013 | Verify upload type checks, malware scanning, encryption, authorization, range/download headers, archive/restore, backup, and orphan cleanup. | Local storage code does not establish production controls. | Patient files and configured storage | Yes | Yes |
| CERT-V014 | Verify scheduling concurrency, timezone/DST, status transitions, history, encounter linking, cancellation, and blocked time. | Complex workflows require end-to-end validation. | Scheduling Web/API/repos/procedures | Yes | No |
| CERT-V015 | Verify referral status rules, attachments, transmission, receipt, follow-up, and closure. | Repository shows internal workflow, not closed-loop exchange. | Referral services/controllers/procedures | Yes | Possibly |
| CERT-V016 | Verify result coding, ranges, abnormal flags, review accountability, escalation, and external ingestion. | Generic result records are not lab workflow evidence. | Results controllers/repository/dashboard | Yes | Possibly |
| CERT-V017 | Verify task assignment, reassignment, overdue logic, escalation, completion/reopen audit, and visibility. | Source cannot prove role/user behavior and timing. | Tasks and overdue service/UI | Yes | No |
| CERT-V018 | Confirm notification scope and test indicator accuracy. | No notification centre or delivery subsystem was found. | Dashboard, overdue indicator, results count | Yes | No |
| CERT-V019 | Inventory all required Primary Care Baseline 5.5 reports and exports. | Only appointment status CSV was identified. | Reporting/API/SQL | Yes | No |
| CERT-V020 | Confirm intended billing solution and integration boundary. | Only a billing-number field and legacy role comment exist. | Product architecture/vendor integrations | No | Yes |
| CERT-V021 | Confirm laboratory-management and provincial lab interface scope. | No module/interface was found. | External systems/deployment | No | Yes |
| CERT-V022 | Map immunization requirements and confirm whether any separate component exists. | No repository domain was found. | Product roadmap/external systems | No | Possibly |
| CERT-V023 | Map medication management separately from prescribing requirements. | Medication-list evidence must not be treated as prescribing. | Medication domain and external prescribing | Yes | Possibly |
| CERT-V024 | Inventory HL7/FHIR/provincial/pharmacy/eReferral/claims interfaces, including separate repositories. | No clinical external interface was located here. | Architecture/deployment/vendor contracts | No | Yes |
| CERT-V025 | Define and test whole-patient/clinic export and import/migration capability. | Only narrow CSV export and schema deployment exist. | Future migration/import/export components | Yes | Yes |
| CERT-V026 | Reconcile manifest/ledger/checksums in each deployed tenant database. | Repository sequence health does not prove applied state. | `SchemaMigration`, DatabaseTool, deployed SQL | Yes | Yes |
| CERT-V027 | Demonstrate backup, restore, RPO/RTO, DR, retention, and file/SQL consistency. | These are hosting operations, not inferable from source. | Cloud SQL/storage/operations | Yes | Yes |
| CERT-V028 | Verify production OIDC clients, issuer/audience, PKCE, certificates/keys, token/session lifetimes, logout, revocation, and MFA. | Development configuration is not production evidence. | Auth/Web/API deployment | Yes | Yes |
| CERT-V029 | Review Swagger/health endpoint exposure, security headers, TLS, network boundaries, and rate limiting. | Startup visibility does not prove perimeter policy. | API and hosting ingress | Yes | Yes |
| CERT-V030 | Review database principals so application/service users cannot bypass procedures or cross tenants. | Code-level layering does not establish SQL grants. | SQL Server roles/grants/secrets | No | Yes |
| CERT-V031 | Verify retention/soft-delete/archive behavior in every clinical domain and backups. | Behavior is inconsistent/domain-specific in visible code. | Tables, procedures, storage, retention policy | Yes | Yes |
| CERT-V032 | Map CDM 4.4 requirements to configured templates, registries, reminders, targets, and reports. | Generic primitives cannot establish disease-specific capability. | Problems/vitals/results/tasks/templates/reporting | Yes | No |
| CERT-V033 | Collect PIA, TRA, privacy/security policies, incident response, breach, training, access review, and secure-development evidence. | These are vendor/process obligations. | Organization governance | No | Yes |
| CERT-V034 | Collect hosting residency, availability, monitoring, patching, vulnerability, penetration-test, support, and subcontractor evidence. | These are hosting/vendor obligations. | Cloud operations and contracts | No | Yes |
| CERT-V035 | Build requirement-to-evidence traceability for PCON-2024-02 and retain screenshots, logs, database evidence, and test results. | This inventory is not a certification evidence package. | Entire product and evidence repository | Yes | Yes |
| CERT-V036 | Track CDS-S 5.2 and Data Migration 5.2 separately as future readiness. | DFU versions must not contaminate the certification baseline. | Future-readiness backlog | No | No |

