# Sensitive-read classification

This is a design recommendation, not implemented behaviour or a compliance claim. Exact OntarioMD Privacy & Security 2.1 wording is not present locally; rows marked **NEEDS SPECIFICATION INTERPRETATION** require confirmation.

## Classification principles

- Audit a meaningful disclosure or deliberate record-open action, not every database query or automatic component request.
- Audit every successful download, print generation, or export because it creates a portable copy.
- A patient-chart-open event represents the automatically composed chart landing page; its allergy, medication, problem, vital, task, and referral feeds should not each generate another event.
- Audit a resource detail separately when the user deliberately opens content materially more sensitive or detailed than the chart summary.
- Keep denied attempts in security monitoring; do not create misleading successful clinical-view records.

| Candidate action | Classification | Volume | Reason / boundary |
|---|---|---:|---|
| Patient search query | NEEDS SPECIFICATION INTERPRETATION | HIGH | Useful for detecting browsing, but query text can contain PHI and volume is high. Prefer security telemetry with normalized metadata if required. |
| Patient search-result display | DO NOT AUDIT | HIGH | Automatic consequence of search; adds little beyond query/open and may disclose result-set PHI in audit data. |
| Patient chart open / demographics view | AUDIT REQUIRED | MEDIUM | Meaningful access to an identifiable clinical record; one chart event covers initial automatic feeds. |
| CPP summary, allergy, medication, problem, vital and task lists auto-loaded in an open chart | DO NOT AUDIT separately | HIGH | Covered by chart-open; separate API events would create noise. |
| Explicit full CPP or clinical-history detail | AUDIT RECOMMENDED | MEDIUM | Deliberate expansion may expose materially more detail; confirm UI semantics. |
| Future immunization list | NEEDS SPECIFICATION INTERPRETATION | MEDIUM | Module and exact requirement are unavailable. |
| Encounter list | DO NOT AUDIT separately | HIGH | Covered by chart-open when automatically displayed. |
| Encounter detail / history open | AUDIT REQUIRED | MEDIUM | Deliberate view of detailed clinical narrative. |
| Referral list | DO NOT AUDIT separately | MEDIUM | Covered by chart-open. |
| Referral detail | AUDIT RECOMMENDED | LOW | Deliberate access to external-care details. |
| Patient document/file metadata list | DO NOT AUDIT separately | MEDIUM | Covered by chart-open and does not disclose document content. |
| Patient document preview/view | AUDIT REQUIRED | MEDIUM | Content disclosure. |
| Patient file content download | AUDIT REQUIRED | LOW | Portable-copy/high-value disclosure; record every successful download. |
| Generated clinical PDF view/download | AUDIT REQUIRED | LOW | Portable clinical output; distinguish generation from retrieval where both occur. |
| Encounter history print | AUDIT REQUIRED | LOW | Printable disclosure; record successful generation. |
| Future referral print | NEEDS SPECIFICATION INTERPRETATION | LOW | Exact workflow/design unresolved; likely required once implemented. |
| Aggregate report execution | AUDIT RECOMMENDED | LOW | Record report type and bounded filter summary, never result rows. |
| Patient-specific report execution | AUDIT REQUIRED | LOW | Direct patient disclosure. |
| CSV/report export or download | AUDIT REQUIRED | LOW | Portable dataset; record every successful export. |
| Scheduler/calendar display and polling | DO NOT AUDIT as sensitive read | HIGH | Operational noise; access remains permission-controlled. Whether schedule access must be audited requires interpretation. |
| User list and access-profile view | AUDIT RECOMMENDED | LOW | Security-administration visibility; belongs in platform audit. |
| Clinic settings view | DO NOT AUDIT | LOW | Low-value administrative read; changes are already audited. |
| Audit-log search/view/export | AUDIT REQUIRED | LOW | Audit data is sensitive; search and export should themselves be auditable. |
| Unauthorized/cross-patient/cross-tenant read attempt | AUDIT REQUIRED in security telemetry | LOW | Security event, not successful clinical access. Capture trusted resolved context and safe route/resource identifiers only. |

## Noise and duplication policy

Audit at explicit Web/API use cases: `PatientChartOpened`, `EncounterViewed`, `PatientDocumentViewed`, `PatientFileDownloaded`, `ClinicalPdfGenerated`, `EncounterPrinted`, `ReportExecuted`, and `ReportExported`. Do not place generic auditing on every GET. Browser retries should carry or receive a server correlation/idempotency value. Chart and encounter view events may be coalesced for the same tenant, actor, patient, resource, action, and short configurable activity window; downloads, prints, exports, audit-log access, failures, and denied attempts must never be coalesced. The window requires privacy-owner approval and must not be hard-coded as a certification claim.
