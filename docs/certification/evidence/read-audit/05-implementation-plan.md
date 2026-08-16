# Sensitive-read audit implementation and evidence plan

This plan describes future work only. No implementation or schema change is included in Step 13.

## First safe vertical slice (Step 13A)

Implement the additive structured extension to tenant `AuditLog`, controlled event vocabulary, insert-only repository/service, and an explicit `PatientChartOpened` event at the server-side chart action after authoritative patient resolution. Include correlation, trusted tenant, clinical actor, patient UID, outcome and source. Do not audit the chart's individual automatic feeds. Add a narrow clinic audit search for engineering/runtime verification only if required; a production review UI is a later decision.

Estimated size: **MEDIUM**. It crosses an immutable-new migration, stored procedure, Infrastructure repository, Application service, Web/API integration, authorization, failure handling and tests, but touches one user action.

## Later slices

| Slice | Scope | Qualitative size |
|---|---|---:|
| Step 13B | `EncounterViewed` and `PatientDocumentViewed`; authoritative compound ownership; approved chart/encounter coalescing | MEDIUM |
| Step 13C | Every file/document/PDF download and encounter print; fail-closed audit persistence | MEDIUM |
| Step 13D | Report execution and CSV/export, bounded filter metadata, no result content | SMALL-MEDIUM |
| Step 13E | Platform administrative reads, denied/cross-tenant security events, monitoring integration | MEDIUM |
| Step 13F | Permission-protected clinic audit review/search/export and operational immutable replication | HIGH |

Do not combine all slices into generic GET middleware. Each integration point must represent a meaningful completed action.

## Future automated security tests

- Authorized chart/encounter/document read creates exactly the expected event with correct tenant, clinical actor, patient and resource.
- Unauthorized request remains denied and creates no successful clinical event.
- Cross-patient resource manipulation is denied; security record uses authoritative/safe identity and never false ownership.
- Cross-tenant attempt is denied before tenant DB access and is recorded only in the platform/security stream.
- Opaque `sub` maps through the clinical-user resolver; numeric parsing is absent.
- Event payload contains no note, diagnosis, document/file content, patient name, HCN, token, path or report rows.
- Automatic chart feed calls do not create events; deliberate repeat opens follow the approved coalescing rule.
- Browser/server retry is idempotent while every successful download/export remains separately recorded.
- Audit insert failure follows the approved fail-closed/emergency policy and raises monitoring.
- Normal application permissions cannot update/delete audit records; audit search/export requires a dedicated permission and is audited.
- Existing mutation audit rows remain readable after the additive schema migration.

## Runtime/certification-readiness evidence

Use synthetic test patients and retain: test ID, environment/build, actor/profile, UTC timestamp, screenshot of the meaningful action, redacted request correlation, matching audit row, trusted tenant/patient/resource IDs, and query/export evidence. Demonstrate chart open, encounter view, document/file download, report export, denied access, cross-patient and cross-tenant attempts, duplicate-load control, audit-write failure, and audit-review authorization. Never place real PHI in certification documents.

## Audit review recommendation

Successful tenant clinical events should be searchable by authorized Clinic Administration users using UTC/local date range, clinical user, patient, event category/action and resource type/UID. Cross-tenant operational/security review belongs in security-only platform tooling. Platform Administration should not automatically gain unrestricted clinical audit visibility. Audit search and export need distinct least-privilege permissions, pagination, bounded ranges, export safeguards and self-auditing. Exact reviewer roles require privacy-owner and OntarioMD interpretation.
