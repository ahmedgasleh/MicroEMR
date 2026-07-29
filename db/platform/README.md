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
