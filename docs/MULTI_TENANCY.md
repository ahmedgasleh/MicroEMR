# MicroEMR multi-tenancy

## Step 10: internal platform administration

`MicroEMR.DatabaseTool` is the internal administration surface. No platform
administration endpoints or pages were added to Web or API. Enable an audited
local execution deliberately with `PlatformAdministration:Enabled=true` and set
`PlatformAdministration:ActorId` through user secrets or environment variables.
Neither value is accepted as a command argument. Production operators should
restrict executable and configuration access to the platform operations group.

Global Identity roles `PlatformAdministrator` and `PlatformOperator` describe
the platform authorization boundary for future authenticated surfaces. Tenant
roles (`Physician`, `Nurse`, `MedicalAssistant`, `Scheduler`, and
`ClinicAdministrator`) authorize activity only inside one tenant. A tenant role
never satisfies platform authorization, and a platform role never creates a
membership or grants clinical access. The local CLI uses its explicit trusted
execution mode instead of an HTTP principal and never accepts a role name as
proof of authorization.

Apply `db/platform/006_platform_administration.sql` explicitly before using the
commands. Configure `ConnectionStrings:PlatformDatabase`; configure
`ConnectionStrings:AuthDatabase` only for membership creation. The latter is
used solely to confirm the supplied ID exists in `AspNetUsers`; there is no
cross-database foreign key and Identity records are not copied. Membership
creation fails closed when this lookup is unavailable. Clinical database
secrets are needed only by `tenant provision`.

Example local onboarding (PowerShell line wrapping omitted):

```powershell
dotnet run --project src/MicroEMR.DatabaseTool -- tenant create --tenant-key admin-tool-test --display-name "Admin Tool Test" --time-zone America/Toronto
dotnet run --project src/MicroEMR.DatabaseTool -- tenant assign-database --tenant-key admin-tool-test --database-server-key local-sql --database-name MicroEMR_Tenant_AdminToolTest --secret-reference development:MicroEMR_Tenant_AdminToolTest
dotnet run --project src/MicroEMR.DatabaseTool -- tenant provision --tenant-key admin-tool-test
dotnet run --project src/MicroEMR.DatabaseTool -- tenant activate --tenant-key admin-tool-test --confirm admin-tool-test
dotnet run --project src/MicroEMR.DatabaseTool -- membership add --user-id ID --tenant-key admin-tool-test --default
dotnet run --project src/MicroEMR.DatabaseTool -- tenant-role add --user-id ID --tenant-key admin-tool-test --role ClinicAdministrator
```

Read-only commands are `tenant list`, `tenant show --tenant-key KEY`,
`tenant members --tenant-key KEY`, `membership list --user-id ID`, and
`tenant-role list --user-id ID --tenant-key KEY`. Lifecycle commands are
`tenant suspend|activate|archive`; membership commands are
`add|activate|suspend|revoke|set-default|clear-default|list`; role commands are
`add|remove|list`. Suspension, revocation, role removal, and tenant lifecycle
changes require `--confirm TENANT-KEY` where high impact. Commands do not prompt,
so automation remains deterministic and retryable.

Tenant creation records only `Provisioning` metadata. Assignment is a separate
explicit step and cannot silently overwrite an active assignment. Activation
requires an active assignment with a schema version. Provisioning remains the
existing explicit migration operation and never runs on application startup.
Suspended tenants and memberships fail existing per-request API revalidation.
Archiving never drops a database or deletes clinical data.

Platform changes write whitelisted, metadata-only audit events. Details contain
status or tenant-role names, never request serialization, secret references,
credentials, tokens, connection strings, patient data, or clinical counts.
Unique constraints and serializable lock patterns protect tenant keys, composite
memberships, roles, and active defaults; row-version columns support future
optimistic-concurrency tokens. SQL errors are translated to safe CLI messages;
`--verbose` is for local developer diagnostics only.

Safe cleanup is `tenant archive --tenant-key KEY --confirm KEY`. This retains all
metadata and the clinical database. Any later database removal must be a separate
approved operation after independently verifying the exact non-production name.
Known limitations: this release has no remote authenticated administration API,
no support impersonation, and no database creation. Failed audit events caused
inside rolled-back SQL transactions cannot be retained reliably without an
independent audit sink; failures are still logged without secret content.

## Step 09: secure tenant selection

The Auth server now presents `GET/POST /Account/SelectTenant` when the existing
membership resolver returns `SelectionRequired`. The resolver's established
policy is preserved: zero active memberships are denied, one active membership
continues automatically, and one valid default among multiple memberships also
continues automatically. Multiple memberships without a default require user
selection. Multiple active defaults are invalid platform state and fail closed.

The authorization endpoint creates a cryptographically random opaque selection
ID. Its server-side record is bound to the authenticated Identity user, the
exact locally validated `/connect/authorize` return URL, the allowed tenant
UIDs, and a five-minute expiration. Each authorization attempt and browser tab
gets an independent record. The selection POST requires authentication and
antiforgery validation, checks the submitted UID against the original allowlist,
and reloads active memberships from `MicroEMR_Platform` before proceeding.

After a successful atomic consume, Auth creates a separate opaque two-minute,
single-use continuation. The resumed authorization request must match both its
owner and exact stored authorization URL. Auth reloads membership and current
tenant roles once more before building claims. Replays, expiration, cross-user
use, modified authorization requests, revoked memberships, and inactive tenants
fail closed. No return URL submitted by the selection form is used.

`IPendingTenantSelectionStore` is registered as a singleton over
`IDistributedCache`. Development uses `AddDistributedMemoryCache`, which is
single-instance only. Production must configure a shared distributed provider
and an atomic distributed consume implementation before running multiple Auth
instances; the current per-key lock makes consume atomic only within one Auth
process. Pending records expire naturally. Logout behavior is unchanged and no
tenant choice is written to the Identity cookie, user record, or persistent
browser cookie.

> A browser-submitted tenant UID is never trusted by itself. The Auth server must validate it against the authenticated user's active platform memberships before issuing tenant claims.

Only the selected membership contributes `tenant_id`, `tenant_key`,
`tenant_name`, and `tenant_role` claims. Global Identity roles remain separate,
and one access token continues to represent exactly one tenant. No database
metadata is stored in selection state or rendered in the page. Clinical database
routing and API tenant middleware are unchanged.

Tenant switching remains out of scope. A future switch must end or replace the
current tenant session/token, return to Auth, revalidate memberships, make a new
selection, and issue a new authorization code and one-tenant token. It must not
edit an existing token or tenant context.

### Manual verification

Use only a non-production Identity user with active memberships in two active
test tenants (for example `local-dev` and `provisioning-test`) and no default.
Start Auth, API, and Web; log out; sign in once; and verify the selection page
shows only those memberships. Select each tenant in separate login cycles and
verify only that tenant's data and claims are available. While a selection page
is open, revoke its membership and then suspend its tenant in separate attempts;
both submissions must be denied. Also submit an unlisted tenant UID, replay a
successful form, and use two simultaneous browser tabs. The unlisted and replayed
submissions must fail, while the tabs must remain independent. Finally verify
logout returns to login and that neither browser content nor tokens expose
database names, server keys, secret references, or connection strings.

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

## Step 07: tenant database provisioning

Tenant clinical schema deployment is explicit and never runs during Auth, API,
or Web startup. `MicroEMR.DatabaseTool` initializes a manually created blank SQL
Server database selected from the platform tenant assignment. Ordinary runtime
API credentials do not need `CREATE DATABASE` permission.

The canonical manifest is `db/tenant-clinical/manifest.json`. It deterministically
orders the existing verified clinical SQL assets, beginning with
`0000-tenant-metadata.sql`. Migration files are deployment-controlled assets;
their SHA-256 hashes are stored at application time. After release, change schema
by adding a new migration rather than editing an applied script. A retry rejects
any applied migration whose stored hash differs from the controlled asset.

Each clinical database contains:

- `dbo.TenantDatabaseIdentity`: exactly one tenant identity, with no credentials,
  secret references, or membership data. A different tenant UID is never allowed
  to take over an initialized database.
- `dbo.SchemaMigration`: stable migration ID, schema version, SHA-256 script hash,
  application time, and applying machine identity.

Provisioning obtains an exclusive SQL Server session application lock named from
the assigned database, with a 30-second timeout. Each pending migration executes
in its own SQL transaction and is recorded only before that transaction commits.
A failed migration is rolled back, is not recorded, and prevents later scripts
from running. `GO` batches are parsed only when `GO` appears alone on a line;
strings and comments are respected, and repeat counts are rejected.

Platform state transitions use stored procedures:

```text
Tenant:         Provisioning -> Active
TenantDatabase: Provisioning -> Active
                              -> MigrationFailed on failure
```

The platform schema version is updated only after tenant-database migrations and
post-provisioning object checks succeed. If the platform completion update fails,
the command reports failure; retry reads tenant-local history and does not reapply
recorded migrations.

### Provision a blank local test database

First rerun `db/platform/002_platform_stored_procedures.sql` in SSMS to deploy the
provisioning transition procedures. Do not use or modify `MicroEMR_Db` for this
test.

Create a separate blank database in SSMS:

```sql
USE master;
GO
CREATE DATABASE MicroEMR_Tenant_ProvisioningTest;
GO
```

Generate a new tenant UID and register metadata in `MicroEMR_Platform`:

```sql
USE MicroEMR_Platform;
GO

DECLARE @TenantUid UNIQUEIDENTIFIER = NEWID();
SELECT @TenantUid AS ProvisioningTestTenantUid;

EXEC dbo.TenantDatabase_RegisterProvisioning
    @TenantUid = @TenantUid,
    @TenantKey = N'provisioning-test',
    @DisplayName = N'Provisioning Test Clinic',
    @DatabaseServerKey = N'local-sql',
    @DatabaseName = N'MicroEMR_Tenant_ProvisioningTest',
    @SecretReference = N'development:MicroEMR_Tenant_ProvisioningTest';
GO
```

Configure both values in the shared API/database-tool user-secret store:

```powershell
dotnet user-secrets set `
  "ConnectionStrings:PlatformDatabase" `
  "YOUR-PLATFORM-DATABASE-CONNECTION-STRING" `
  --project src/MicroEMR.DatabaseTool/MicroEMR.DatabaseTool.csproj

dotnet user-secrets set `
  "TenantDatabaseSecrets:development:MicroEMR_Tenant_ProvisioningTest" `
  "YOUR-CONNECTION-STRING-WITH-INITIAL-CATALOG-MicroEMR_Tenant_ProvisioningTest" `
  --project src/MicroEMR.DatabaseTool/MicroEMR.DatabaseTool.csproj
```

Run the controlled command from the repository root:

```powershell
dotnet run --project src/MicroEMR.DatabaseTool -- `
  provision-tenant-database --tenant-key provisioning-test
```

The command prints only status, version, and migration count. It never prints a
connection string, password, or secret value. Run it again to verify
`AlreadyCurrent`.

Verify in the test clinical database that one identity row and all migration rows
exist, and verify in `MicroEMR_Platform` that both tenant statuses are `Active`
and `CurrentSchemaVersion` is `1.0.0`. An identity mismatch or changed script hash
must fail. After correcting a failed migration, rerun the same command; recorded
migrations are skipped safely.

To remove the test after validation, first verify the exact database name in
SSMS, remove only the `provisioning-test` platform metadata through an approved
administrative process, and then explicitly drop only
`MicroEMR_Tenant_ProvisioningTest`. Never target `MicroEMR_Db`.

Required reference data from the historical baseline is applied automatically.
No patients, encounters, appointments, documents, allergies, or medications are
added as demo data. A future step should normalize the overlapping historical
encounter SQL assets into newly numbered immutable migrations; the manifest
currently selects the consolidated encounter script and deliberately excludes
the older duplicate addendum/SOAP-note fragments.

## Tenant isolation hardening

The authenticated `tenant_id` claim is the only request input used to begin
tenant resolution. The API requires exactly one non-empty GUID claim, loads the
tenant from `MicroEMR_Platform`, requires `Active` status, and revalidates an
exact active subject/tenant membership on every authenticated request. Tenant
roles are refreshed from that membership, so stale token roles are not trusted.

> A tenant identifier in a route or request is never sufficient to select a database. Only the validated tenant context may control database resolution.

Headers, routes, query strings, forms, cookies, tenant key/name claims, and
hostnames never select a clinical database. Missing, conflicting, inactive, or
unavailable state fails closed with a safe 403 or 503 response. The scoped tenant
context starts empty, allows only exact same-tenant reassignment, rejects a
different UID/key/name, and is cleared in middleware `finally` handling.

Every clinical repository must inject `ITenantSqlConnectionFactory`; it must not
inject `IConfiguration`, a raw connection string, or `SqlConnection`. Platform
repositories intentionally use only `ConnectionStrings:PlatformDatabase`. The
tenant connection factory has no fallback: it validates the active assignment,
secret, and initial catalog, then verifies exactly one matching row in
`dbo.TenantDatabaseIdentity` before returning the connection. Verification is
cached only in the scoped factory and keyed by tenant plus assignment/server
metadata. `GET /health/platform` checks platform connectivity without enumerating
tenants or exposing database metadata.

No tenant-dependent cache or clinical background job currently exists. Future
cache keys must include `tenant:{tenantUid}:`; future clinical jobs must carry an
explicit tenant UID and correlation ID, establish a dedicated scope, and never
capture request tenant state. External clinical file/export storage is not yet
implemented; future server-generated paths must live under
`tenants/{tenantUid}/...` and clients must never supply physical paths. Clinical
audit/history writes remain in the selected tenant database. Logs must not
contain clinical content, credentials, connection strings, tokens, or secrets.

Fixed `ConnectionStrings:MicroEmrDatabase` configuration is unsupported. Store
platform and tenant connections in user secrets or environment variables. The
API/database tool share a secret store; Auth uses its own:

```powershell
dotnet user-secrets set "ConnectionStrings:PlatformDatabase" "YOUR-PLATFORM-CONNECTION" --project src/MicroEMR.Api/MicroEMR.Api.csproj
dotnet user-secrets set "ConnectionStrings:PlatformDatabase" "YOUR-PLATFORM-CONNECTION" --project src/MicroEMR.Auth/MicroEMR.Auth.csproj
```

For release regression, provision two non-production tenant databases and use
only synthetic records. Verify patient, scheduling, encounter, document,
allergy, medication, and audit reads/writes in both directions. While logged in
as Tenant A, submit known Tenant B entity UIDs and require not-found/forbidden
without modification or ownership disclosure. Repeat with membership and tenant
suspension and a temporary assignment to the other database; identity validation
must block that mismatch. Restore metadata and clean up through normal workflows.

Automated tests cover context isolation, request-input override resistance,
status/membership checks, assignment/name/identity mismatches, current tenant
roles, safe errors, and repository construction. Remaining work is a controlled
SQL-backed two-database integration harness and explicit tenant job/file
envelopes when those features are introduced.
