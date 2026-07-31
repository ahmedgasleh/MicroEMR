# Encounter lifecycle

MicroEMR uses one tenant-local `PatientEncounter` model. Clinical notes are stored on the encounter (legacy note plus SOAP fields); patient documents remain a separate existing feature and are not duplicated or linked by this workflow.

## States and rules

- `Open` is the persisted compatibility value for the single pre-sign **Draft** state.
- `Signed` is final. Note update procedures reject every non-Open encounter, and there is no unlock transition.
- An amended indicator is derived from immutable `PatientEncounterAddendum` rows; amendments do not overwrite the original note, signer, or signing time.
- Signing requires an encounter date, type, responsible provider, reason for visit, and content in at least one clinical-note field. Validation is centralized in `EncounterSigningValidator` and repeated defensively in SQL.
- Draft note saves and signing require the current RowVersion. Stale requests return a conflict rather than overwriting newer work.
- Signing and amendment identity/time are supplied by the server. Only tenant `Physician` and `ClinicAdministrator` roles may sign or amend. A platform role alone grants no clinical write access.

## Relationships and isolation

Chart-created/walk-in encounters do not require appointments. The existing appointment start procedure resolves the appointment and patient inside the active tenant database, reuses an existing encounter for the appointment, and rejects cancelled/completed appointments. All encounter repositories continue to use `ITenantSqlConnectionFactory`; history, audit, and amendments remain in that tenant database.

Migration `0014-encounter-workflow` adds amendment reason/signature/concurrency metadata and hardens existing read, update, sign, and addendum procedures. It does not modify the applied encounter migration.

## Manual regression checklist

For Tenant A, create and resave a chart encounter, verify a stale save conflicts, complete the required fields, sign it, verify UI and direct API edits are rejected, inspect history, add an amendment, and confirm the original content/signature is unchanged. Start another encounter from Today's Schedule and confirm its patient and appointment.

Switch to Tenant B and confirm Tenant A lists, encounter UID, amendments, and history are unavailable. Create and sign a Tenant B encounter. Then verify documents, allergies, medications, scheduling, login, tenant selection, logout, and the browser console.

Deferred to separate work: vitals, diagnoses, appointment check-in/completion workflow, advanced document templates, and PDF generation.
