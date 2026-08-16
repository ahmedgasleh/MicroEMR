# Step 12 tenant-isolation evidence

## Trusted chain

`OpenIddict token -> TenantResolutionMiddleware -> active catalog record -> exact active subject/tenant membership -> scoped TenantContext -> TenantSqlConnectionFactory -> assignment/secret -> SQL Initial Catalog validation -> exactly one matching TenantDatabaseIdentity -> clinical repository`.

The browser supplies neither a connection string nor a database name. The middleware accepts exactly one token tenant claim, rejects missing, malformed, duplicate, inactive, mismatched, or membership-less contexts, replaces stale role claims from current membership, and ignores request-controlled tenant values. The connection factory resolves only the current trusted `TenantUid`, checks active assignment metadata, rejects attach-file/database mismatch, and validates database identity before returning a connection. Clinical repository architecture tests require tenant-aware connections.

| Attack / control | Repository evidence | Automated evidence | Classification |
|---|---|---|---|
| Tenant A member presents Tenant B claim | membership lookup is for exact subject plus claimed tenant | `TenantResolutionMiddlewareTests.MissingTenantOrMembershipReturnsForbidden` and mismatch cases | VERIFIED BY AUTOMATED TEST |
| Browser supplies tenant/database/connection values | request values are not resolution inputs | `RequestControlledTenantValuesCannotOverrideTokenTenant` | VERIFIED BY AUTOMATED TEST |
| Missing, invalid, duplicate or stale tenant context | middleware requires one valid active tenant and membership | middleware negative cases; `TenantContextAccessorTests` | VERIFIED BY AUTOMATED TEST |
| Wrong assignment/database/identity | factory compares tenant, catalog and database identity | `TenantSqlConnectionFactoryTests` negative cases | VERIFIED BY AUTOMATED TEST |
| Tenant A resource UID queried in Tenant B | repositories connect only to Tenant B's resolved clinical database, where A's row is absent | architecture tests and `PatientReferralApplicationTests.TenantScopedServiceCannotSeeAnotherTenantRepositoryData` | VERIFIED BY AUTOMATED TEST for architecture/representative service; NEEDS RUNTIME VERIFICATION end-to-end |
| Cross-tenant mutation | same middleware/factory chain precedes mutation; clinical actor is resolved in selected tenant | actor accessor tenant-scope tests | VERIFIED BY AUTOMATED TEST for components; NEEDS RUNTIME VERIFICATION end-to-end |

Operational evidence remains necessary for SQL principals/grants, secret custody, deployed assignment records, backups, network boundaries, and a two-database adversarial run. No tenant-isolation defect was found by code inspection.
