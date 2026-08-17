# Step 13 sensitive-read audit design summary

## Outcome

MicroEMR has strong tenant-local clinical mutation audit, platform-administration audit and domain histories, but no systematic durable evidence for sensitive reads. The smallest practical design extends the existing clinical `AuditLog` for successful patient disclosures and uses platform/security auditing for administrative reads and attempts rejected before a clinical database is safely selected. Nothing is implemented in this design step.

## Candidate events and model

First-class events are `PatientChartOpened`, `EncounterViewed`, `PatientDocumentViewed`, `PatientFileDownloaded`, `ClinicalPdfGenerated`, `EncounterPrinted`, `ReportExecuted`, `ReportExported`, `AuditLogViewed/Exported`, and safe security-denial events. Initial automatic chart feeds, result rendering, scheduler polling and clinic-setting views are not separately audited. Patient search and future immunization/referral-print treatment require interpretation.

The minimal model requires stable event UID, trusted tenant identity, resolved clinical actor for clinical reads, patient UID where applicable, controlled resource type/UID, semantic action/category, UTC timestamp, correlation, outcome and source. Bounded safe report metadata is optional. IP requires privacy/proxy review; user-agent and copied clinical content are not recommended.

## Identity and ownership

Tenant comes only from validated tenant context/database identity. Numeric clinical actor comes only from centralized `sub`-to-clinical-user resolution. Patient/resource identity is resolved or compound-validated server-side before an event is written. Platform administrators use opaque authenticated subject for non-clinical events. Unauthenticated attempts never receive invented identities.

## Noise, downloads and failure

Audit meaningful actions, not every GET. A single chart-open covers automatic lists. Approved short-window coalescing may apply to repeated chart/encounter views, but never to downloads, prints, exports, audit access, failures or denials. Portable disclosures should always create an event and fail closed if it cannot be persisted. High-sensitivity detail views should initially fail closed; chart availability versus audit integrity requires an explicitly governed clinical-safety decision. Writes should start synchronous with bounded transient retry, not fire-and-forget.

## Retention, integrity and review

No retention period is invented. Obtain OntarioMD/business/privacy guidance for retention, legal hold, archive, tenant termination, backup and destruction. Product controls should be insert-only stored procedures, least-privilege grants, server identity/time, controlled values and no update/delete API. Operational controls should add privileged-access review, monitoring, encrypted backup/restore and an immutable centralized copy/SIEM where approved. Checksums/signatures are not justified before risk analysis.

Clinic Administration should review its tenant clinical audit with date, user, patient, action/category and resource filters. Cross-tenant review belongs in security-only operational tooling. Search/export must be separately authorized and audited.

## Implementation status

Step 14 implements the first vertical slice: additive structured fields in the existing tenant `AuditLog` and one synchronous, fail-closed `PatientChartOpened` event at the central Web chart action. It does not implement any other candidate event, denial auditing, review tooling, retention, or replication. See `06-step14-patient-chart-open-implementation.md` for evidence and runtime verification.

Step 15B additionally implements synchronous, fail-closed `EncounterViewed` and `PatientDocumentViewed` events at the two intentional individual-detail API actions, reusing migration `0044` and the Step 14 actor/tenant conventions. List and automatic chart feeds remain unaudited. See `08-step15b-encounter-document-view-implementation.md`.

Step 16A extends the controlled procedure allow-list through immutable migration `0045` for future `PatientDocumentDownloaded` and `PatientFileDownloaded` events. No endpoint is wired yet; print events remain deferred because the current application has no reliable server-controlled print semantic. See `10-step16a-disclosure-event-allowlist.md`.

Step 16B1 wires synchronous, fail-closed `PatientFileDownloaded` auditing into the existing explicit file-content endpoint after compound ownership and storage resolution but before stream release. Patient Document download remains deferred because the current product provides Preview only. See `11-step16b1-patient-file-download-audit.md`.

Step 17A adds the aggregate audit contract through immutable migration `0046`: `ReportExecuted` and `CsvExported` use resource type `Report`, null patient/resource UIDs and the governed `AppointmentStatusDateReport` key. Filters are intentionally omitted and no report controller is wired yet. See `13-step17a-aggregate-report-contract.md`.

## Delivery recommendation

The Step 13A **MEDIUM** vertical slice is implemented by Step 14. Later slices cover encounter/document views; downloads/prints; reports/exports; platform denials/monitoring; and finally review tooling/immutable replication. The detailed plan contains future automated and runtime evidence cases.

## Unresolved OntarioMD / governance questions

1. Exact Privacy & Security 2.1 wording and validation evidence for viewing, search, export, print and denied access.
2. Whether patient search, schedule viewing, aggregate reporting, and future immunization/referral output are mandatory audit events.
3. Required audit retention period, availability, export format and reviewer capabilities.
4. Whether repeated view coalescing is acceptable and what constitutes one clinical access session.
5. Required failure semantics during urgent clinical care and any break-glass/emergency access evidence.
6. Whether IP address/device metadata is required and how privacy, proxies and residency affect it.
7. Required tamper-evidence strength, centralized replication and separation-of-duty expectations.
8. Which clinic/platform roles may search or export audit records and whether patient access-report requests must be supported.

These questions must not be represented as satisfied until authoritative material and execution evidence are available.
