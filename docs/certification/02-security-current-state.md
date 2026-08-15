# Security current state

## Authentication

`MicroEMR.Auth` uses ASP.NET Core Identity with an EF Core auth store and OpenIddict server. `src/MicroEMR.Auth/Program.cs` configures authorization-code and refresh-token flows, PKCE, OpenID/profile/email/roles/offline-access/API scopes, and development signing/encryption certificates. `AuthorizationController` issues claims and handles authorization/logout; `AccountController` handles login and tenant selection.

`MicroEMR.Web` is an OIDC client using cookies, authorization code, PKCE, and saved tokens. `MicroEMR.Api/Program.cs` validates JWT bearer tokens using configured authority/audience and requires HTTPS metadata. Actual issuer, client registration, certificate custody/rotation, cookie policy, session expiry, MFA, password policy, lockout, revocation, and production transport settings require runtime/operational evidence.

## Authorization and access-profile enforcement

| Enforcement layer | Current evidence | Classification / boundary |
|---|---|---|
| A. UI presentation | `_Sidebar.cshtml` loads effective permissions and conditionally presents navigation. Tenant-user views conditionally present actions. | IMPLEMENTED for identified navigation/actions, but presentation is not treated as a security boundary. Full UI coverage needs runtime inspection. |
| B. Web/server | `PermissionAuthorization.cs` provides `RequireWebPermission`, policy provider, and handler. Patient, encounter, document, scheduling, report, template, clinic, access-profile, and user-admin controllers contain permission attributes. | PARTIAL: strong coverage is visible, but several proxy controllers (allergies, medications, results, tasks, files, referrals, chart alerts, vitals) show only `[Authorize]`; their downstream API is the primary permission boundary. Direct Web-action denial and consistency need testing. |
| C. API endpoint | API permission provider/handler and `RequirePermission` protect read/manage/sign/export operations across principal clinical/admin controllers. `AccessSecurityStabilizationTests` assert coverage for important controller groups. | IMPLEMENTED for the identified endpoints; exhaustive route-to-permission review and live 401/403 tests remain required. |
| D. Repository/data access | Repositories use tenant-scoped connection factories and stored procedures. Access-profile SQL is tenant-keyed. Business permissions are normally enforced above repositories. | PARTIAL: repositories are not generally an independent authorization boundary. Direct invocation protections and stored-procedure grants require deployment verification. |
| E. Tenant-data isolation | `TenantResolutionMiddleware` requires exactly one tenant claim, revalidates active tenant and membership, refreshes tenant roles, then sets scoped context. `TenantSqlConnectionFactory` validates assignment, catalog, and exactly one matching `TenantDatabaseIdentity`. | IMPLEMENTED in code; adversarial cross-tenant, stale-token, inactive membership, wrong-secret, and wrong-database tests are required in a running deployment. |
| F. Patient/data-level authorization | Patient UIDs scope nested resources and repositories/procedures commonly take both patient and resource UID. | PARTIAL: no consent, care-team, break-glass, patient masking, chart segment, or record-level policy system was found. Runtime negative-ID tests are required. |

The permission catalog in `src/MicroEMR.Application/AccessProfiles/AccessProfileModels.cs` contains granular keys for patients, clinical data, encounters, scheduling, documents, results, referrals, tasks, templates, users, clinic settings, and reports. Platform scripts `010_access_profiles.sql`, `012_user_permission_overrides.sql`, and `013_access_security_stabilization.sql` implement tenant-scoped profiles, effective permissions, overrides, audit events, and concurrency.

## Roles and claims

Global Identity roles and tenant-role claims are distinct. Auth claim enrichment adds tenant ID/key/name and tenant roles after membership resolution. The API replaces tenant-role claims using current platform membership data, rather than relying solely on token-age role state. Clinic-administrator policies and permission policies coexist; later detailed review must identify any inconsistent role-only paths.

## Tenant and database resolution

Platform tables hold tenant catalog, database assignment, user membership, roles, profiles, overrides, and platform audit. Clinical records are stored in separate tenant databases. Connection strings are resolved indirectly through `ITenantDatabaseSecretProvider`; assignment metadata must match the selected tenant and the SQL initial catalog. The clinical database must contain one matching `TenantDatabaseIdentity` row.

## Clinical-user and write rejection

`AuthenticatedClinicalUserAccessor` maps the authenticated `sub` to the selected tenant's `ApplicationUser.AuthSubjectId`. `ClinicalUserActorResolutionMiddleware` runs for authenticated POST/PUT/PATCH/DELETE requests and returns 403 if it cannot resolve a clinical actor. Controllers retrieve the required actor from `ClinicalUserActorContext`; mutation services/repositories pass it to stored procedures. This is concrete evidence of centralized mutation rejection, but every mutation and exceptional administrator path still needs trace testing.

## Audit mechanisms

- Tenant clinical `AuditLog` writes are visible in allergy, medication, file, clinic configuration, vitals, and other procedure assets.
- `AppointmentHistory` and encounter history/addenda preserve domain-specific changes.
- Platform scripts write `PlatformAuditEvent` for membership, tenant, access-profile, and permission changes.
- Actor identity is a tenant clinical numeric user ID for clinical changes and authenticated subject string for platform changes.

Classification: **PARTIAL**. Repository inspection has not established complete mutation coverage, read-access audit, failed-access audit, before/after consistency, tamper protection, retention, export/review, clock synchronization, or production log access controls.

## Security-relevant configuration and verification gaps

- `Authentication:Authority`, `Authentication:Audience`, Auth/Platform connection strings, secret references, and patient-file storage are configuration-driven; deployed values were not inspected.
- Swagger is enabled unconditionally in API startup; exposure and policy must be verified in the deployed environment.
- API uses `RequireHttpsMetadata = true`; actual TLS termination, headers, certificate lifecycle, and network controls require operational evidence.
- Development signing/encryption certificates are configured in source; production key material and rotation require verification.
- Local patient-file storage validates paths but does not by itself establish encryption, malware scanning, durable storage, backup, or least-privilege filesystem access.
- No source conclusion is made about PIA, TRA, incident response, privacy training, support access, vulnerability management, penetration tests, or breach handling; these are vendor/process evidence.

