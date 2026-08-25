# Step 30 — Certification Gap Reprioritization

Date: 2026-08-25

Branch: `feature/ontariomd_certification_step30_gap_reprioritization`

Baseline: current `main` at `eaaed34`, including Step 29D1.

Status: **AUTHORITATIVE CURRENT READINESS SNAPSHOT FOR PRIORITIZATION**

This document supersedes the older readiness and gap inventories for prioritization purposes. It does not rewrite historical evidence. The requested Step 24 file, `28-step24-certification-readiness-reassessment.md`, is not present on current `main`; Step 26 already recorded that absence. Consequently, this reassessment reconciles the available scope, current-state, Primary Care, evidence, and Steps 25–29D1 documents against current source, migrations, and tests.

No certification conclusion is inferred from code alone. `VERIFIED` means repository evidence and existing automated/manual evidence support the bounded capability; it does not mean OntarioMD certification has been awarded.

## Baseline and classification counts

- Platform migration maximum: `021-prescriptions-prescribe-permission-governance.sql`.
- Tenant migration maximum: `0051-result-review-acknowledgement-hardening.sql`.
- The 20-domain actual product gap matrix contains: 2 `VERIFIED`, 2 `IMPLEMENTED — NEEDS RUNTIME VERIFICATION`, 2 `IMPLEMENTED — NEEDS EVIDENCE PACKAGING`, 10 `PARTIAL`, 2 `MISSING`, 1 `NEEDS SPECIFICATION INTERPRETATION`, and 1 `OPERATIONAL / INFRASTRUCTURE BLOCKED`.
- No domain in that matrix is classified `NOT APPLICABLE / OUT OF CURRENT SCOPE`; external integrations are instead identified as explicitly deferred boundaries.

## Actual product gap matrix

| Domain | Current status | Existing implementation | Remaining gap | Work type |
| ------ | -------------- | ----------------------- | ------------- | --------- |
| Demographics | PARTIAL | Structured patient identifiers, name, birth date, sex/gender fields, contact/address, status/lifecycle, tenant isolation, actor and mutation audit; controlled CDM import updates demographics. | Exact identifier/cardinality and terminology requirements, fuller demographic breadth, runtime evidence, and complete CPP presentation remain unresolved. | PRODUCT IMPLEMENTATION |
| Problems | IMPLEMENTED — NEEDS RUNTIME VERIFICATION | Patient-scoped structured Problem List with active/resolved lifecycle, concurrency, permissions, tenant isolation, audit, chart integration, and controlled migration import/provenance. | Controlled-tenant presentation, negative access, audit, concurrency, and terminology evidence must be packaged. | RUNTIME VERIFICATION |
| Allergies | IMPLEMENTED — NEEDS EVIDENCE PACKAGING | Structured allergy CRUD/resolve lifecycle, permissions, patient/tenant scoping, concurrency, and audit are present. | Current implementation needs a consolidated field, lifecycle, terminology, negative-access, and runtime evidence package. | EVIDENCE PACKAGING |
| Medications | PARTIAL | Structured medication list with create/edit/discontinue, dose-related fields, permissions, audit, concurrency, and chart presentation. | Coded drug identity, provenance/correction history, reconciliation, renewals/refills, and exact PC04/CDS-S mapping remain incomplete or unclear. | PRODUCT IMPLEMENTATION |
| Prescriptions | PARTIAL | Separate structured prescription aggregate; Draft/Finalized/Cancelled/Superseded lifecycle; dedicated permission; active provider authorization; structured dose/frequency/directions; immutable final artifact; correction/supersession; audit and chart integration. | Runtime evidence remains blocked; PrescribeIT/transmission, pharmacy destination, renewals/refills, medication-list synchronization, terminology, and medication safety are deferred or interpretation-bound. | RUNTIME VERIFICATION |
| Immunizations | PARTIAL | Local structured Basic Immunization History, administered and historical/external records, completed and entered-in-error semantics, correction, audit, permissions, and chart integration. | Refusal/not-administered, dose/series semantics, controlled terminology, forecasting, DHIR, and controlled runtime evidence remain. | PRODUCT IMPLEMENTATION |
| Encounters | PARTIAL | Create/draft/edit/sign lifecycle, immutable signed state, append-only addenda, history, author/signer attribution, concurrency, PDF artifact, permissions, audit, and scheduling linkage. | Exact multipart contributor provenance, encounter-integrated diagnoses/CPP updates, unified chronological print, and runtime evidence remain. | PRODUCT IMPLEMENTATION |
| Scheduling | PARTIAL | Resource calendars, appointments, blocked time, status workflow, arrival/start/complete integration, conflict rules, history, audit, permissions, critical appointment checks, and month/day UI. | Patient-level past/future list, privacy display modes, planned/ad-hoc double booking where required, and remaining runtime/certification demonstrations remain. | PRODUCT IMPLEMENTATION |
| Documents | IMPLEMENTED — NEEDS EVIDENCE PACKAGING | External and authored patient documents, metadata, drafts, immutable template versions, structured template runtime, finalized PDFs/artifacts, archive/restore, permissions, and audit. | Consolidated certification evidence must demonstrate version provenance, finalized immutability, archive/restore, print/download, and negative access. | EVIDENCE PACKAGING |
| Files | IMPLEMENTED — NEEDS RUNTIME VERIFICATION | Patient-file metadata and filesystem provider, opaque storage keys, archive/restore, content hashes, secure download boundary, and download audit. | Controlled runtime retrieval, missing-content behavior, storage permissions, backup/recovery consistency, and production storage evidence remain. | RUNTIME VERIFICATION |
| Referrals | PARTIAL | Patient-scoped referral creation, recipient/reason/summary, Draft/Sent/ResponseReceived/Closed lifecycle, linked supporting documents, tracking timestamps, concurrency, audit, and permissions. | Immutable/printable referral-letter artifact, selected clinical content, referrer/specialty/letter fields, reminders and complete tracking presentation remain. | PRODUCT IMPLEMENTATION |
| Results | PARTIAL | Flat structured result, patient scoping, New/Reviewed state, idempotent reviewer/time/note acknowledgement, atomic audit, concurrency, and actionable unreviewed queue. | Abnormal/critical representation, source/provenance, panels/components, correction history, terminology/units, attachments/trends, and controlled runtime evidence remain. OLIS is external. | PRODUCT IMPLEMENTATION |
| Tasks/Notifications | PARTIAL | Base patient tasks, overdue calculation and dashboard presentation, plus actionable unreviewed-result workflow. | No general governed clinical rules engine, referral-linked reminders, escalation policy, or evidence that generic notifications meet CDS/CDM requirements. | PRODUCT IMPLEMENTATION |
| Reporting | NEEDS SPECIFICATION INTERPRETATION | Narrow appointment-status reporting, CSV export, and governed report/export audit exist. | Exact required report catalogue, filters, outputs, retention and certification breadth are unavailable; a broad BI suite must not be inferred. | SPECIFICATION INTERPRETATION |
| User Administration | VERIFIED | Activation/deactivation, tenant roles, clinical provisioning, Access Profiles, effective permissions/overrides, platform entitlements, and dedicated prescribing permission are implemented and tested. | Production role-assignment procedure and certification screenshots remain evidence packaging, not a material product gap. | EVIDENCE PACKAGING |
| Security | VERIFIED | OIDC/authentication, server authorization, tenant/patient isolation, clinical actor mapping, successful-read audit, denial audit/review, authorization-version refresh, runtime secret validation, and safe telemetry foundation exist. | Production operational evidence, remaining legacy telemetry cleanup, infrastructure TLS, and security operations remain; no new application audit expansion is justified here. | SECURITY IMPLEMENTATION |
| Data Migration | PARTIAL | Canonical validation/staging, fingerprinting, provenance, replay/idempotency, dry run, and controlled demographics/Problem List import exist. | More domains, attachments, exact external required format, outgoing export, and controlled SQL/runtime evidence remain. | PRODUCT IMPLEMENTATION |
| Hosting | OPERATIONAL / INFRASTRUCTURE BLOCKED | Deployment/recovery/backup/observability designs and safe telemetry foundation are documented. | Secure SQL TLS, real backup jobs, restore/DR exercises, immutable off-site copies, central sink, monitoring/alerts, certificate lifecycle, and least-privilege production identities require infrastructure/operations authority. | INFRASTRUCTURE |
| CDS | MISSING | Structured Problems, Allergies, Medications, Prescriptions, Immunizations and Results now provide usable input boundaries; overdue tasks and result review are workflows, not a CDS engine. | No governed rule catalogue, evaluation service, patient-specific rule outcomes, override rationale, lifecycle, versioning, or CDS evidence exists. | PRODUCT IMPLEMENTATION |
| CDM | MISSING | Problems, vitals, results, tasks, encounters and scheduling are reusable foundations. | No disease registry, disease-specific monitoring/flows, targets, recalls, care-plan state, or governed CDM workflow exists. | PRODUCT IMPLEMENTATION |

## Primary Care functional reassessment

| Group | Status | Current conclusion |
| ----- | ------ | ------------------ |
| PC01 — Demographics | PARTIAL | Core demographics are usable and audited, but exact requirement mapping, some breadth, CPP presentation and controlled runtime evidence remain. It must not remain partial merely from inheritance; current gaps independently support the status. |
| PC03 — Immunizations | PARTIAL | Step 25A moves local immunization history from missing to a real structured foundation. Refusal, series/dose, terminology, forecasting, runtime and DHIR boundaries prevent a stronger whole-group claim. |
| PC04 — Medication Management | NEEDS SPECIFICATION INTERPRETATION | Medication-list and local structured prescribing capabilities are substantial, but exact PC04 identifiers/wording remain unavailable. Electronic transmission, renewals/refills, terminology and medication-safety breadth cannot be guessed. |
| PC06 — External Documents | IMPLEMENTED — NEEDS EVIDENCE PACKAGING | Files/documents, metadata, lifecycle, retrieval and audit exist; the remaining work is principally consolidated runtime and certification evidence. |
| PC07 — CPP | PARTIAL | Problems, allergies, medications, immunizations, results, encounters, demographics, alerts and medical/surgical history form a usable cumulative profile. Family history, risk factors, special needs, encounter-to-CPP workflow, complete one-operation print and customization remain. |
| PC08 — Encounter Documentation | PARTIAL | Draft/sign/addendum/history/correction boundaries and actor/signer evidence exist. Exact multipart contributor semantics and unified longitudinal print remain unresolved. |
| PC09 — Scheduling | PARTIAL | The stable scheduling lifecycle is mature and much remaining work is evidence packaging, but patient appointment history and requirement-specific display/double-booking behaviors remain product gaps. |
| PC10 — Referrals | PARTIAL | Referral creation, recipient, reason, lifecycle, supporting documents and tracking exist. The referral-letter artifact and referral-specific reminders remain concrete product gaps; exact content breadth also requires interpretation. |

### Problem List

The Problem List is not missing. It is implemented with structured lifecycle, permissions, audit, isolation, chart integration and controlled migration support. Its correct current classification is `IMPLEMENTED — NEEDS RUNTIME VERIFICATION`.

### Results and laboratory boundary

The local Results domain now has flat structured storage and a clinically meaningful acknowledgement control: trusted reviewer, database time, idempotency, atomic audit and an actionable unreviewed queue. Remaining local safety/product work is abnormal/critical representation, provenance, panels/components and correction history. Lab-feed formats and OLIS are external integration/specification work and must not obscure the value of the local review implementation.

### CDS feasibility

Genuine CDS remains `MISSING`. Overdue tasks, alerts and the unreviewed-result queue are useful workflows but are not represented as governed, versioned clinical rules and must not be relabelled as CDS.

The architecture is now ready for a bounded CDS foundation because patient-scoped structured Problems, Allergies, Medications, Prescriptions, Immunizations and Results are available behind application/repository boundaries. A future slice can evaluate explicitly approved rules against these sources without embedding rules in controllers or conflating operational notifications with clinical decisions. Exact advanced rules, drug knowledge, official terminology and certification scenarios still require specification or approved clinical content.

### CDM

CDM remains `MISSING`. A generic Problem List does not create a chronic disease registry. There is no disease-specific enrolment, monitoring plan, target tracking, recall schedule, flowsheet or governed care pathway. Existing vitals/results/tasks make future CDM feasible but do not close it.

## Foundation matrix

| Foundation | Status | Evidence | Remaining boundary |
| ---------- | ------ | -------- | ------------------ |
| CDS-S | MISSING | Structured clinical inputs and safe authorization/audit foundations exist. | Exact CDS-S requirements and governed terminology/rules are unavailable; no rules engine or decision lifecycle exists. |
| Data Migration | PARTIAL | Validation/staging, fingerprint, provenance/idempotency and controlled demographics/Problem import. | External format, expanded domains, attachments, export and runtime evidence. |
| Hosting | OPERATIONAL / INFRASTRUCTURE BLOCKED | Steps 29–29D provide readiness, TLS, backup/restore, recovery and observability designs; 29D1 implements safe telemetry. | Production infrastructure and operating controls are not established by documents or code. |
| Privacy & Security | VERIFIED | Layered authorization/isolation, governed read/mutation/denial audit, security review, entitlement/version controls, secret validation and safe telemetry. | Production evidence and the bounded legacy telemetry cleanup remain; SQL TLS belongs to infrastructure. |

## Functional matrix

| Functional group | Status | Evidence-based conclusion |
| ---------------- | ------ | ------------------------- |
| Primary Care Baseline | PARTIAL | Major local workflows exist across all reviewed groups, but CPP breadth, referral artifact/reminders, Results structure, scheduling details and exact PC04 interpretation prevent a baseline-wide verified claim. |
| Chronic Disease Management | MISSING | Reusable structured data exists, but no disease-specific registry, targets, monitoring, recall or care pathway exists. |

## Ranked remaining work

### A. Highest-priority actual product gaps

1. **Bounded CDS foundation** — completely missing certification foundation with high clinical/dependency value; current structured domains now make safe implementation possible.
2. **Results provenance, abnormality and correction foundation** — high local clinical-safety value independent of OLIS.
3. **CPP completion** — family history, risk factors/special needs and usable consolidated print/presentation.
4. **Referral completion** — immutable referral-letter artifact plus governed follow-up/reminders.
5. **CDM foundation** — disease registry/monitoring/recall model, preferably after a reusable bounded CDS evaluation foundation.

Broader Data Migration is important but follows definition of additional destination domains and acquisition of the required external format. Demographic completion remains within CPP work rather than outranking the five above.

### B. Highest-priority evidence gaps

1. Controlled-tenant runtime evidence for migrations `0047`–`0051`: Immunizations, Data Migration validation/import, Prescriptions and Results review.
2. Consolidated PC06 Documents/Files evidence: metadata, immutable versions/artifacts, archive/restore, download and disclosure audit.
3. PC09 Scheduling runtime package covering lifecycle, history, conflict, permission and browser workflows.
4. Problem List/CPP runtime package covering lifecycle, audit, isolation, presentation and imported provenance.
5. User Administration and security administration package covering activation, Access Profiles, effective permissions, prescribing authorization and denial-review UI.

### C. Infrastructure and operational blockers

1. A controlled SQL endpoint with trusted TLS/hostname validation for runtime and migration evidence.
2. Implemented, encrypted and access-restricted platform/Auth, per-tenant and patient-file backup jobs with immutable off-site copies.
3. Isolated restore and full DR exercises with SQL/file consistency evidence.
4. Protected centralized logging, retention and access controls after Step 29D2 telemetry cleanup.
5. Monitoring/alerting, production certificate lifecycle and least-privilege service identities.

These require hosting/DBA/security authority and are not the next repository-only product implementation.

### D. Specification interpretation blockers

1. Exact PC04 and CDS-S identifiers, mandatory wording, terminology and verification scenarios.
2. Official Data Migration inbound/outbound formats, domain breadth and attachment rules.
3. PC08 multipart contributor/provenance and unified-print expectations.
4. Reporting catalogue, mandatory filters/outputs and retention breadth.
5. External integration conformance for OLIS, DHIR and PrescribeIT, including official terminology and transport requirements.

## Stale and superseded evidence

| Document | Old conclusion | Current state | Action |
| -------- | -------------- | ------------- | ------ |
| Requested Step 24 reassessment (`28-step24-certification-readiness-reassessment.md`) | Referenced as the former baseline. | File is absent from current repository. | Record absence; Step 30 is the authoritative prioritization baseline. |
| `04-preliminary-gap-map.md` and `05-verification-backlog.md` | Early snapshot before later implementation. | Multiple missing/partial conclusions are stale. | Retain as historical discovery evidence; use Step 30 for current priorities. |
| `readiness/01-source-gap-inventory.md`, `03-stage1-readiness.md`, `05-certification-workstreams.md` | Broad initial source/readiness conclusions. | Security, clinical domains and hosting evidence have materially advanced. | Retain for source provenance; reconcile through Step 30. |
| Step 25 Immunization design | Immunizations were absent and only designed. | Step 25A implements local Basic Immunization History. | Treat Step 25 as design history; Step 25A and Step 30 control current status. |
| Step 26 Data Migration design | Migration was design-only/missing. | Steps 26A/26B implement validation/staging and controlled demographics/Problem import. | Treat design-only conclusion as superseded; retain unresolved format boundaries. |
| Step 27 Structured Prescribing design | Prescription aggregate was proposed. | Step 27P/27A implement governed authorization and local structured prescribing. | Use Step 27A for implementation state; retain external/terminology deferrals. |
| Step 28 Results design | Review workflow had safety deficiencies. | Step 28A implements idempotent, attributed, audited review and queue. | Use Step 28A for review status; retain broader Results gaps. |
| Step 29 initial Hosting assessment | Most controls lacked packaged design/evidence. | Steps 29A–29D add TLS evidence and backup/recovery/observability designs. | Retain as baseline; use the later documents and Step 30 for current state. |
| Step 29D logging design | Safe correlation/redaction foundation was missing. | Step 29D1 implements the bounded safe telemetry foundation. | Superseded for that bounded implementation; central monitoring gaps remain current. |

## Newly closed or materially advanced gaps since the old baseline

- Local Immunizations moved from missing to a structured, governed partial capability.
- Data Migration moved from missing/design-only to a working validation/staging and controlled-import foundation.
- Structured local prescribing and dedicated prescribing authorization now exist.
- Results acknowledgement now has trusted reviewer/time, idempotency, atomic audit and an actionable queue.
- Hosting has coherent TLS, backup/restore, recovery-policy and observability evidence, although operating controls remain blocked.
- Safe vendor-neutral W3C operational telemetry and failed-body/exception redaction foundation now exist.
- Earlier security work remains substantial: read and denial audit, review UI, tenant/patient isolation, entitlements and authorization-version refresh.

## Explicit deferrals and non-goals

The following must remain deferred until authoritative requirements, external dependencies or approved scope exist:

- OLIS and other lab feeds;
- DHIR;
- PrescribeIT and pharmacy transmission;
- broad interoperability or speculative import/export formats;
- speculative official clinical terminology;
- unsafe cross-tenant probing as a test technique;
- a broad reporting/BI suite;
- advanced CDS rules, drug interaction knowledge or clinical content without specification and clinical governance;
- centralized logging vendors, monitoring and hosting controls without operational ownership.

## One recommended next implementation step

**Implement a bounded, vendor-neutral CDS foundation using only approved deterministic rules and the current structured clinical domains.**

The slice should establish a governed rule definition/version boundary, patient-scoped evaluation, non-interruptive actionable outcome lifecycle, permissions, explanation, acknowledgement/override rationale where appropriate, audit, isolation and tests. It must not invent advanced clinical content, implement a drug-knowledge base, or claim full CDS-S conformance. Initial rules must be separately approved and supported by available specification/clinical evidence.

This outranks alternatives because CDS is a completely missing Foundation area, has direct clinical-safety and certification significance, and now unlocks value from Problems, Allergies, Medications, Prescriptions, Immunizations and Results. Results completion is the strongest alternative but improves one domain; CPP/referrals are important Baseline gaps but have less cross-domain dependency value; CDM should reuse rather than precede a governed rule/evaluation foundation; broader migration depends on external format and destination-domain decisions; evidence packaging and hosting work should proceed independently when runtime/infrastructure access is available.

Expected schema effect if that future design is approved: tenant migration `0052` is likely required for governed rule/outcome lifecycle and audit-consistent persistence. No platform migration is presently expected because existing effective permissions can be assessed during design; a new platform entitlement must not be assumed. Step 30 itself creates no migration and does not implement this recommendation.

## Verification boundary

Step 30 changes documentation only. Required verification is `git diff --check`, Release build, full API suite and full Auth suite. Controlled SQL/browser certification evidence is not claimed by this analysis document.
