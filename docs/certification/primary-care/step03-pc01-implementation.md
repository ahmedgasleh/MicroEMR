# Step 03 PC01 implementation

## Scope

This branch implements the smallest safe PC01 slice identified in Step 02: PC01.01 server-side demographic validation parity and explicit Web POST authorization. It does not claim full PC01.01 coverage and does not implement PC01.02 through PC01.08.

## Requirement addressed

| Requirement | Before | After this branch | Justification |
|---|---|---|---|
| PC01.01 | PARTIAL | PARTIAL | Required names and date of birth already existed. Create/update DTOs now reject whitespace-only names and future birth dates through authoritative server validation; existing email, length and required annotations remain. Web models mirror the checks. The Web edit POST now explicitly requires `Patients.Edit`. Full CDS-S fields and demographic history remain open. |

## Code changes

- `MicroEMR.Application/Patients/Contracts/CreatePatientRequest.cs`: implements `IValidatableObject` for whitespace-only first/last names and future birth date.
- `MicroEMR.Application/Patients/Contracts/UpdatePatientDemographicsRequest.cs`: adds whitespace-only first/last-name validation alongside existing future-date validation.
- `MicroEMR.Web/Models/Patients/CreatePatientRequest.cs`: mirrors the server rules for immediate form feedback.
- `MicroEMR.Web/Models/Patients/EditPatientDemographicsViewModel.cs`: mirrors mandatory-name validation alongside existing patient/date validation.
- `MicroEMR.Web/Controllers/PatientsController.cs`: adds `RequireWebPermission(PermissionKeys.PatientsEdit)` to the edit POST.
- `0039-patient-demographic-audit.sql`: replaces the patient create/update procedures so each successful mutation writes an actor-attributed audit item in the same transaction.

No endpoint, route, request field, response field, patient field name, or application-layer workflow changed.

## Database changes

Migration `0039-patient-demographic-audit.sql` was added and registered in the tenant-clinical manifest. It does not change the schema: it replaces `Patient_Create` and `Patient_UpdateDemographics` to write `AuditLog` records atomically. Existing migrations remain unchanged.

## Security considerations

- API `Patients.Edit` remains the authoritative mutation control.
- The Web edit POST now independently requires `Patients.Edit`; UI visibility is not treated as sufficient authorization.
- `ClinicalUserActorResolutionMiddleware` remains responsible for rejecting writes without a resolved clinical actor.
- `PatientService` continues to pass the resolved actor to `PatientRepository`.
- `PatientRepository` continues to use `ITenantSqlConnectionFactory`; no tenant selector was added to a request.
- `Patient_UpdateDemographics` row-version behavior is unchanged, so conflicting edits are not silently overwritten.
- Successful patient creation writes a `Create`/`Patient` audit row with the resolved actor and new demographic snapshot.
- Successful demographic editing writes an `UpdateDemographics`/`Patient` audit row with the resolved actor and old/new snapshots; a stale update rolls back without an audit row.

## Tests added

`PatientDemographicCertificationTests` verifies:

- valid create and update models;
- rejection of missing/whitespace mandatory values;
- rejection of future birth dates and invalid email values;
- actor forwarding for create and update;
- `Patients.Edit` on API create/update and both Web edit actions;
- tenant-scoped repository construction;
- preservation of SQL row-version concurrency checks;
- invalid service update rejection before repository mutation; and
- propagation of the existing concurrency exception.
- immutable migration registration, atomic create/update audit writes, actor attribution, old/new snapshots, and retained row-version enforcement.

## Runtime verification still required

1. Create a patient with valid existing demographic fields.
2. Attempt create with blank/whitespace first or last name.
3. Attempt create with a missing or future birth date and invalid email.
4. Load and edit an existing patient, then confirm all unchanged fields still round-trip.
5. Repeat invalid-value cases on edit.
6. Verify a user with `Patients.Edit` can create and edit.
7. Verify a user without `Patients.Edit` cannot open or POST the edit route and receives API denial on direct mutation.
8. Open the same patient in two sessions and verify the stale update receives the existing concurrency response.
9. Verify create/update each produces exactly one `AuditLog` item with the clinical actor, patient identifiers, action, timestamp, and expected old/new demographic snapshot.
10. Attempt access using a different tenant context and verify no cross-tenant patient read or update succeeds.

Do not perform destructive tests against production.

## Remaining PC01 gaps

- **PC01.01 — PARTIAL:** complete CDS-S 5.1 field/coded-value mapping and a supported demographic-history retrieval workflow remain; create/update audit snapshots are now persisted.
- **PC01.02 — MISSING:** clinician roster/MRP assignment.
- **PC01.03 — MISSING:** current and historical physician enrolment.
- **PC01.04 — MISSING:** multiple alternative contacts with multiple purposes, including SDM and emergency contact.
- **PC01.05 — MISSING:** issuer-aware duplicate detection/prevention on create and HCN update, excluding version code, with existing-record display.
- **PC01.06 — MISSING:** controlled whole-chart duplicate merge and immutable audit.
- **PC01.07 — LIKELY MET:** name/HCN search still requires runtime/certification evidence.
- **PC01.08 — PARTIAL:** active provider-demographic maintenance and CDS-S mapping remain.

PC01.02, PC01.03, PC01.04, PC01.05, PC01.06 and PC01.08 were not implemented because each requires a new data/workflow design or unresolved field mapping beyond this branch's safe validation slice.
