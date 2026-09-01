# Platform access-management administrator repair

## Purpose

`024_access_management_administrator_repair.sql` repairs a platform database that
has the migration 023 provider-permission procedures but is missing the
`dbo.AccessManagementAdministrator` inline table-valued function originally
introduced by migration 013.

The missing function causes access-profile or permission-override updates to fail
with `Invalid object name 'dbo.AccessManagementAdministrator'` when the procedures
perform their final-administrator lockout check.

## Deployment

Back up the platform database, confirm migrations through 023 were applied, and
run `db/platform/024_access_management_administrator_repair.sql` against the
platform database. Do not replay migration 013: it also recreates older procedure
definitions and could regress later permission-governance changes.

The repair fails closed if its tables or the migration 023 procedure versions are
not present. It does not alter tenant clinical databases.

## Verification

Confirm the function exists:

```sql
SELECT OBJECT_ID(N'dbo.AccessManagementAdministrator', N'IF') AS FunctionObjectId;
```

Then retry the access-profile permission update. The procedure will continue to
enforce optimistic concurrency, audit logging, and protection against removing
the tenant's final effective `Users.ManageAccess` administrator.
