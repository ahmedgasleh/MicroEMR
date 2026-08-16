# Existing audit and history architecture

| Mechanism | Storage and schema | Identity/context | Integration | Retention/integrity status |
|---|---|---|---|---|
| Clinical `AuditLog` | Per-tenant clinical DB; identity key, nullable clinical `UserId`/`PatientId`, action/entity/id, old/new values, optional IP/browser, UTC timestamp | Tenant is implicit in selected database; patient is legacy numeric FK; resource is text | Clinical mutation stored procedures insert audit rows transactionally | No source retention, append-only grants, review API, export, signing, or immutable replica established |
| `PlatformAuditEvent` | Platform DB; event UID, actor subject/type, action, target tenant/user, outcome, UTC time, correlation UID, bounded JSON | Explicit target tenant; opaque authenticated/platform actor | Tenant, membership, role, profile, override and provisioning procedures | No retention/review/tamper operational evidence established |
| `PatientEncounterHistory` | Tenant DB; encounter/patient UIDs, action/description, old/new status, reason, UTC time, clinical actor | Patient/resource explicit | Encounter procedures write domain history | Clinical history, not a complete security audit; retention not specified |
| `AppointmentHistory` | Tenant DB; appointment UID, old/new time/status/resource, reason, UTC time, clinical actor | Patient is indirectly resolved through appointment | Scheduling procedures write domain history | Domain history, not complete security audit; retention not specified |
| Referral history | `AuditLog` mutation rows for create/status/link/unlink; referral carries status timestamps | clinical actor and patient | Referral procedures | No distinct durable read history or review surface |
| Medication history | `AuditLog` mutation rows and medication status fields | clinical actor and patient | Medication procedures | Mutation-focused; no read evidence |
| Patient/document/file histories | `AuditLog` rows for demographic, document, artifact and file lifecycle mutations | clinical actor and patient where applicable | Stored procedures, mostly transactional | Mutation-focused; file/document reads absent |
| User/permission administration | `PlatformAuditEvent` plus application operational logs | opaque subject and explicit tenant/target user | Platform procedures/services | Strong mutation evidence; read/review behaviour absent |
| Authentication/security logs | ASP.NET Core/OpenIddict/application `ILogger`; permission, tenant and actor rejection logs | subject/tenant/path varies by event | Middleware/handlers/runtime logging | OPERATIONAL LOG ONLY; sink, retention, correlation, access, immutability and alerting not established |

## Reuse assessment

The smallest design is to evolve tenant-local `AuditLog` into the canonical clinical audit stream, not create a second tenant clinical audit table. A future additive migration can add structured nullable columns for stable event UID, patient UID, event category, outcome, request correlation and source application while preserving all existing mutation rows. A dedicated append-only stored procedure can validate trusted identities and insert read events. Platform administrative reads and denied/cross-tenant attempts belong in `PlatformAuditEvent` or security telemetry because a tenant clinical database may be unavailable or must not be selected for a rejected request.

This produces a hybrid architecture without duplicating successful clinical disclosures across stores. Exact schema and migration work are deferred.
