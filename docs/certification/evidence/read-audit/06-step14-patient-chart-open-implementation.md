# Step 14 — Patient chart-open read audit implementation

## Event semantics and trigger

Step 14 implements one successful sensitive-read event: `PatientChartOpened`.

The only trigger is the central Web `PatientsController.Details` action. After the Web layer has `Patients.View` and successfully obtains the patient, it synchronously posts to `POST /api/patients/{patientUid}/chart-open`. The API independently retains `Patients.View`, resolves the patient again, and records the authoritative `PatientUid`. Only after the audit call succeeds does the Web action load documents, templates, encounters, allergies, medications, problems, and vitals and return the chart.

Find Patient, Last Selected Patient and direct patient-chart links converge on `PatientsController.Details`. A full page refresh is another intentional chart open and creates another event. Timeline, tabs and automatic child-data endpoints do not call the audit endpoint and create no chart-open event. No session/time-window coalescing was introduced.

## Structured event

| Concept | Implementation |
|---|---|
| Event UID | server-generated `AuditEventUid` GUID |
| Action | `ActionName = PatientChartOpened` |
| Category | `ClinicalRead` |
| Actor | tenant-local numeric `UserId`, obtained through `IAuthenticatedClinicalUserAccessor` |
| Patient | legacy `PatientId` plus authoritative structured `PatientUid` |
| Resource | `ResourceType = PatientChart`; `ResourceUid = PatientUid`; compatible legacy entity/id also populated |
| Outcome | `Succeeded` only; denial events remain out of scope |
| Time | database `SYSUTCDATETIME()` in existing `CreatedAt` |
| Correlation | API `HttpContext.TraceIdentifier` |
| Source | controlled `MicroEMR.Api` |
| Tenant | implicit in the trusted tenant database selected by `ITenantSqlConnectionFactory` |

No patient name, DOB, health-card number, diagnosis, note, medication, document/file content, route, token, or other clinical content is stored.

## Database and compatibility

New immutable migration `0043-patient-chart-read-audit` adds eight nullable structured columns to existing tenant-local `AuditLog`, a filtered unique event-UID index, and insert-only procedure `AuditLog_RecordPatientChartOpened`. Nullable columns preserve historic and existing mutation rows. The procedure validates an active, non-deleted patient and active clinical user before inserting. No existing migration or mutation procedure changed.

The write is a synchronous durable write after successful patient resolution. It is not transactionally coupled to a read result and creates no distributed transaction. It uses the same resolved tenant database as other clinical repositories.

## Application, API and Web integration

- Application: `PatientChartReadAuditService` validates narrow inputs and resolves the clinical actor centrally.
- Infrastructure: `ReadAuditRepository` executes only the dedicated tenant stored procedure through `ITenantSqlConnectionFactory`.
- API: protected chart-open endpoint re-resolves the patient and supplies server correlation. No new user permission exists.
- Web: central Details action invokes the endpoint once before loading child data.

An authenticated user with `Patients.View` but no active tenant-local clinical identity cannot open the chart through this path because the API mutation actor middleware returns 403. The Web action returns `Forbid`; it never invents an actor or parses OIDC `sub` numerically.

## Failure behaviour

Audit persistence is fail closed. The API logs an error with patient UID and trace identifier and returns 503 without claiming success. The Web action logs the failure and returns 503 without loading or displaying the chart. Cancellation caused by an aborted request is rethrown rather than misreported. No background queue, silent swallow, or fire-and-forget write exists.

## Automated tests

`PatientChartReadAuditTests` covers resolved actor and narrow payload, authoritative patient identity, exactly one event, missing-patient/no-event behavior, fail-closed 503, permission preservation, additive/content-free schema, historic-row compatibility, existing mutation audit continuity, central trigger/no child-feed audit, trusted tenant connection, and migration ordering/uniqueness.

Authorization middleware, tenant isolation, clinical actor resolution, patient permission, migration-source and existing mutation tests remain part of the complete suite. Denied and cross-tenant requests cannot reach the endpoint body under existing middleware; Step 14 deliberately creates no denial event.

## Manual verification

Use test patients only.

1. Sign in as an active clinical user with `Patients.View`.
2. Open Patient A through Find Patient and confirm the chart loads.
3. Query tenant A `AuditLog` and confirm exactly one `PatientChartOpened` event for that request.
4. Confirm `UserId`, `PatientUid`, `ResourceUid`, category, outcome, source, UTC time and correlation.
5. Refresh the full chart and confirm a second event is expected.
6. Switch tabs and allow automatic chart feeds to refresh; confirm no extra `PatientChartOpened` event.
7. Open Patient B and confirm a distinct Patient B event.
8. Open the remembered/last-selected patient and confirm it converges on the same audited Details path.
9. Use a user without `Patients.View`; confirm access is denied and no successful chart-open event exists.
10. Use an authenticated user without an active clinical mapping; confirm the chart is denied and no successful event exists.
11. Repeat in tenant B; confirm its event exists only in tenant B's database.
12. Simulate audit database/procedure failure; confirm 503, application error log and no chart disclosure.
13. Perform a patient demographic mutation and confirm its existing mutation audit still works.

## Known limitations and remaining slices

- There is no audit review UI/API, retention automation, immutable replication, SIEM, or security-denial event.
- SQL deployment grants and append-only operational enforcement still require evidence.
- Chart access currently depends on a clinical actor even for a permissioned platform administrator; this is deliberate under the approved actor model.
- API consumers calling the demographic GET do not automatically create a chart event; this event represents the MicroEMR Web chart action, not every patient read.
- Remaining slices are `EncounterViewed`, document views, file/document/PDF downloads, print, report execution/export, search/schedule interpretation, security denials, review tooling and immutable replication.
