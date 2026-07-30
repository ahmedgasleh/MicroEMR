# MicroEMR multi-tenancy

## Step 04: token claims

The Auth application resolves a user's active membership from the validated
`MicroEMR_Platform` data before completing an OpenIddict authorization request.
Only a resolved membership is added to the token principal.

The issued claims are:

- `tenant_id`: canonical tenant GUID and the authoritative tenant security identity.
- `tenant_key`: stable convenience value for the tenant.
- `tenant_name`: display name for the tenant.
- `tenant_role`: one claim for each distinct tenant-scoped role.

ASP.NET Identity global role claims remain unchanged and separate from
`tenant_role`. A user with no active membership is denied a token. A user with
multiple memberships and no single default is also denied because tenant
selection is intentionally not implemented yet. Invalid platform membership
data produces a generic denial and is logged without exposing internal details.

The access token includes all four tenant claim types. When `openid` is granted,
the identity token also includes `tenant_id`, `tenant_key`, and `tenant_name`.
Tenant roles are access-token-only.

The OpenIddict token endpoint remains framework-managed and preserves the
principal stored with the authorization/refresh token. Consequently, this step
does not revalidate membership on refresh. A membership suspension or revocation
will not affect an already-issued refresh token until that token expires or is
revoked. Refresh-time membership revalidation must be added in a later security
hardening step.

This step does not select a clinical database, change connection handling, add
API tenant middleware, or enforce `tenant_id` in the API. The next multi-tenant
step will establish and validate tenant context in the API.

## Local claim verification

1. In `MicroEMR_Platform`, confirm the local Identity user has exactly one
   resolvable active membership, the sample tenant is active, and at most one
   active membership is marked as default.
2. Start Auth, Web, and API using the existing development launch configuration.
3. Log out and log back in so a new authorization code and tokens are issued.
4. In browser developer tools, inspect the Web authentication exchange using the
   application's existing diagnostics. Do not paste a real token into a public
   decoding website.
5. Decode the JWT locally. One option in PowerShell is to copy only the access
   token into a local variable and decode its payload without transmitting it:

   ```powershell
   $tokenParts = $accessToken.Split('.')
   $payload = $tokenParts[1].Replace('-', '+').Replace('_', '/')
   $payload = $payload.PadRight($payload.Length + ((4 - $payload.Length % 4) % 4), '=')
   [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload))
   ```

6. Confirm the access token contains `tenant_id`, `tenant_key`, `tenant_name`,
   and one `tenant_role` value per assigned tenant role.
7. Confirm the standard global role claim is still present when applicable.
8. Confirm the token contains no connection string, database name, database
   server key, or secret reference.
9. Confirm existing Web pages and authenticated API calls continue to work, then
   verify logout returns to the login screen.

## Step 05: API tenant context

The access token's signed `tenant_id` claim proposes the tenant for an
authenticated API request. After JWT authentication, the API tenant-resolution
middleware validates that exactly one non-empty GUID claim is present. It then
loads the tenant from `MicroEMR_Platform`; the platform catalog, rather than the
token's convenience claims, is authoritative for tenant status, key, and display
name.

The middleware also revalidates the authenticated `sub` user's active membership
for that tenant on every request. Missing or invalid claims, inactive or missing
tenants, and inactive memberships receive `403 Forbidden`. Platform database
failures receive a safe `503 Service Unavailable` response.

The resolved `ITenantContext` and its accessor are scoped to one request. The
middleware clears the context in a `finally` block. Tenant identity cannot be
selected or replaced through browser headers, query strings, route values,
cookies, hostnames, or form fields.

The API pipeline order is:

```csharp
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
```

Endpoints explicitly marked anonymous bypass tenant resolution. Swagger runs
before the tenant middleware and remains available under its existing setup.
Older tokens without `tenant_id` are rejected, so users must log out and log back
in after deployment.

Tenant roles remain dedicated `tenant_role` claims and do not become global
ASP.NET roles. The reusable `TenantClinicAdministrator` policy checks only the
tenant-role claim type and is not broadly applied to existing endpoints.

For authenticated verification, call `GET /api/context/tenant`. It returns only
`tenantUid`, `tenantKey`, and `displayName`, sourced from the platform catalog.

This step does not modify or select the clinical database connection. Existing
repositories continue using the original clinical connection. The next step will
resolve and open the appropriate tenant clinical database.

### Manual verification

1. Configure `ConnectionStrings:PlatformDatabase` for the API using user secrets
   or `ConnectionStrings__PlatformDatabase`; do not commit new credentials.
2. Confirm the platform tenant and user membership are active.
3. Start Auth, API, and Web, then log out and back in to replace older tokens.
4. Confirm existing authenticated pages and API calls succeed.
5. Call `GET /api/context/tenant` and compare its values with the platform tenant.
6. Temporarily suspend the tenant and confirm authenticated API calls return 403.
7. Reactivate the tenant and confirm a newly attempted API call succeeds.
8. Confirm the configured clinical database remains unchanged throughout.

## Step 06: tenant clinical database connections

Clinical database selection now starts exclusively from the validated
`ITenantContext.TenantUid`. `ITenantDatabaseResolver` reads the matching database
assignment from `MicroEMR_Platform`, which stores metadata only: server key,
database name, status, and an opaque secret reference. It never stores raw
credentials or a connection string.

`ConfigurationTenantDatabaseSecretProvider` resolves the opaque reference from
server-side configuration. This local provider is replaceable with a managed
secret store in production. The browser, tokens, controllers, and API responses
never receive database metadata or credentials.

The implementation uses a full protected connection string per secret reference
(Option A). Before opening it, `TenantSqlConnectionFactory` requires:

- An assignment for the current tenant UID, with the same UID.
- `DatabaseStatus` equal to `Active`.
- Nonblank server key, database name, and secret reference.
- A valid SQL Server connection string with nonblank data source and catalog.
- An `InitialCatalog` matching the platform database name, ignoring case.
- No `AttachDbFilename` option.

All clinical repositories open late and dispose early through
`ITenantSqlConnectionFactory`. Platform catalog, database-assignment, and user
membership repositories continue using the separate fixed `PlatformDatabase`
connection. The old `MicroEmrDatabase` setting remains transitional but is no
longer consumed by API clinical repositories.

### Local secret configuration

Configure the existing `local-dev` assignment with `DatabaseName = MicroEMR_Db`,
`DatabaseStatus = Active`, and `SecretReference = development:MicroEMR_Db`.
Store its current clinical connection string in API user secrets:

```powershell
dotnet user-secrets set `
  "TenantDatabaseSecrets:development:MicroEMR_Db" `
  "YOUR-LOCAL-MICROEMR-DB-CONNECTION-STRING" `
  --project src/MicroEMR.Api/MicroEMR.Api.csproj
```

An environment variable may be used instead:

```text
TenantDatabaseSecrets__development__MicroEMR_Db
```

Do not commit the substituted value. Existing clinical calls provide safe manual
verification; no connection-diagnostic endpoint was added.

### Pooling and verification

ADO.NET pools connections by normalized connection string, so each distinct
tenant connection string can create a separate pool. Connections remain pooled,
are opened only for an operation, and are disposed promptly. Pool count and SQL
connection usage should be monitored as tenant count grows.

After configuring the secret, log out and back in, then verify dashboard,
patients, demographics, documents, encounters, allergies, medications,
scheduling, and appointment details. Temporarily setting the assignment to
`Unavailable` must make clinical calls fail safely. A temporary database-name
mismatch must also be rejected. Restore both values afterward. Logs may identify
tenant UID, server key, database name, and status, but never secret contents or
the connection string.

This step still uses only the existing `MicroEMR_Db`; it does not create, copy,
or migrate another tenant database. The next step will introduce repeatable
tenant database creation and schema migration.
