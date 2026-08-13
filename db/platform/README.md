# MicroEMR platform database

`MicroEMR_Platform` stores tenant catalog, database-assignment, membership, and
tenant-scoped role metadata. It is shared by the platform and contains no clinical
records. Each tenant clinical database remains separate; this step does not change
how the API selects or connects to the existing clinical database.

## Local configuration

Set `ConnectionStrings:PlatformDatabase` for `MicroEMR.Api`. The development
settings contain a Windows integrated-authentication example. Override it with
.NET user secrets or the `ConnectionStrings__PlatformDatabase` environment
variable when your local SQL Server requires different settings. Never commit
credentials or a tenant clinical-database connection string as a secret reference.

## Create and optionally seed

Run the scripts with a SQL account permitted to create databases, in this order:

1. `001_create_platform_database.sql`
2. `002_platform_stored_procedures.sql`
3. Optional: `003_seed_local_development.sql`
4. `004_make_membership_keys_nonclustered.sql` (existing installations only)
5. Optional local seed: `005_seed_local_user_membership.sql`
6. `006_platform_administration.sql`
7. `007_membership_activation_lifecycle.sql`
8. `008_tenant_role_management.sql`
9. `009_tenant_user_creation.sql`
10. `010_access_profiles.sql`
11. `011_access_profile_assignment_nonclustered_key.sql`

Script 006 adds internal administration procedures, platform audit events,
optimistic row versions, and a filtered unique index that permits at most one
active default membership per user. Apply it explicitly; applications do not
run platform schema changes at startup.

Scripts 007 and 008 add tenant-admin membership activation and canonical tenant
role replacement with RowVersion concurrency, audit logging, and last-active-
administrator protection. Apply them explicitly in sequence.

Script 010 adds tenant-scoped Access Profiles, built-in profile permission sets,
role-compatible assignment backfill, effective-permission procedures, audit, and
optimistic concurrency. Apply it after 009; applications do not apply it at startup.

Script 011 converts the access-profile assignment composite primary key to a
nonclustered key, avoiding SQL Server's 900-byte clustered-index key limit for
the `NVARCHAR(450)` Auth user identifier. Apply it after 010.

If the platform tables were created before the membership primary keys were
changed to nonclustered indexes, run `004_make_membership_keys_nonclustered.sql`
once to correct the existing database without removing membership data.

For example, from this directory with Windows authentication:

```powershell
sqlcmd -S localhost -E -i 001_create_platform_database.sql
sqlcmd -S localhost -E -i 002_platform_stored_procedures.sql
sqlcmd -S localhost -E -i 003_seed_local_development.sql
```

The optional seed generates its tenant UID with `NEWID()`, is idempotent by tenant
key, and stores only `development:MicroEMR_Db` as a secret reference. It does not
store a password or raw connection string and is not executed automatically by the
application.

## Link a local Identity user

Identity users remain in `MicroEMR_Auth`. Memberships and tenant-scoped roles are
stored in `MicroEMR_Platform`, using the Identity user ID as a stable reference.
There is intentionally no cross-database foreign key between these databases.

After applying `003_seed_local_development.sql`, query
`MicroEMR_Auth.dbo.AspNetUsers` in SSMS, copy the local user's `Id`, open
`005_seed_local_user_membership.sql`, and replace every occurrence of
`IDENTITY-USER-ID-HERE` with that ID. Execute the entire script in SSMS. Its
final result set shows the membership and tenant role that were created or
already existed. The script also supports connections where SSMS Always
Encrypted parameterization is enabled.

The script idempotently creates an active default membership and the
`ClinicAdministrator` tenant role. It does not alter or replace ASP.NET Identity
roles. Current login, global role claims, and issued tokens remain unchanged; a
later step will add tenant claims to authentication.
