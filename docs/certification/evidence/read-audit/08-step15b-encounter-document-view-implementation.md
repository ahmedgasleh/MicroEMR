# Step 15B — Encounter and patient-document view auditing

## Implemented events and trigger points

Step 15B implements only `EncounterViewed`/`Encounter` and `PatientDocumentViewed`/`PatientDocument`. The triggers are the API `PatientEncountersController.GetEncounter` and `PatientDocumentsController.GetDocument` actions used by intentional individual opens. Each action first resolves the requested resource, returns not-found when it is absent, then synchronously records one event before returning clinical content.

List endpoints, chart child feeds, encounter history/addendums, PDF endpoints, mutations and automatic metadata loads do not invoke the audit service. Reopening a detail intentionally creates another event; no time-based deduplication was added.

## Reused audit infrastructure

The application `StructuredReadAuditService` reuses Step 15A's `IReadAuditRepository.RecordStructuredReadAsync`, which calls only `dbo.AuditLog_RecordStructuredRead` from migration `0044`. It supplies controlled event/resource constants, the server-resolved clinical actor, authoritative resource and patient UIDs, the ASP.NET request trace identifier and `MicroEMR.Api` source. No clinical content is accepted or copied.

No migration or direct C# insert was added. Migrations `0043`, `0044` and every earlier migration remain unchanged.

## Identity, ownership and tenant behavior

Actor resolution uses the same `IAuthenticatedClinicalUserAccessor` model as Step 14; OIDC `sub` is not parsed as a number. Patient UID comes from the resolved encounter/document response, never client input. The individual API routes accept only the resource UID, so there is no browser-provided patient UID to trust or mismatch. Existing patient-scoped mutation/history routes retain their existing compound checks.

The repository continues to obtain its connection solely through `ITenantSqlConnectionFactory`; therefore resolution and audit persistence occur in the current validated tenant database. No database, connection or tenant-database identity is client supplied.

## Authorization and failure semantics

The existing controller permissions remain `Encounters.View` and `Documents.View`. There is no audit-specific permission. Middleware authorization and tenant resolution precede controller execution, so denied requests create no successful view event.

Both detail actions preserve Step 14's fail-closed approach. If actor resolution or audit persistence fails, the API logs the resource UID and trace identifier, returns 503, and does not return the resolved clinical response. Request cancellation is rethrown.

## Automated evidence

`EncounterDocumentReadAuditTests` covers both controlled pairs, actor/correlation/source propagation, authoritative patient/resource properties, exactly one trigger per detail controller, absence from list endpoints, fail-closed 503 source behavior and content-free audit contracts. Existing Step 14 and Step 15A tests cover chart noise control, tenant connection selection, procedure allow-list/rejection/insert-only behavior, immutable migration hashes and mutation-audit continuity.

## Manual runtime verification

Use synthetic test patients only.

### Encounter

1. Sign in with `Encounters.View`, open Patient A, and open one encounter.
2. Confirm the encounter loads and exactly one `EncounterViewed` row has the expected actor, PatientUid, EncounterUid, correlation, outcome and source.
3. Return to the list and confirm no new encounter-view event.
4. Open the same encounter intentionally and expect one additional event; open another encounter and verify its distinct ResourceUid.
5. Repeat without `Encounters.View`; confirm denial and no successful event.
6. Attempt a resource UID from another patient and another tenant; confirm no disclosure/event in the wrong tenant.

### Patient document

7. Open Patient A's Documents tab and confirm the list creates no `PatientDocumentViewed` event.
8. Open one document and confirm exactly one event with the expected actor, PatientUid and DocumentUid.
9. Close/reopen intentionally and expect one additional event.
10. Repeat without `Documents.View`; confirm denial and no successful event.
11. Attempt a document UID from another patient and another tenant; confirm no disclosure/event in the wrong tenant.

### Failure and regression

12. Make the audit procedure unavailable in a disposable environment; verify both detail opens return 503 and disclose no clinical content.
13. Open a patient chart and verify one `PatientChartOpened`; switch tabs and verify no chart, encounter or document audit flood.
14. Perform representative patient, encounter/sign, scheduling and referral mutations and confirm their existing mutation audit rows remain unchanged.

## Limitations and next slice

No denied-access event, audit review UI, retention automation, immutable replication or SIEM integration exists. Downloads, prints, report execution/export, search and schedule access are unchanged. The recommended next bounded slice is download/print auditing, beginning with patient-document and patient-file downloads and encounter printing after confirming their distinct central trigger paths and controlled procedure values.
