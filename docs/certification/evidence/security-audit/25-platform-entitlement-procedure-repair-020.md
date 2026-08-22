# Platform entitlement procedure repair — migration 020

## Incident and scope

An installed `MicroEMR_Platform` database rejected a governed entitlement assignment with a conversion from the
`PlatformEntitlement|<SHA-256>` lock resource to `int`. Inspection supplied during the incident showed the installed
assignment procedure contained this incorrect declaration:

```sql
DECLARE @LockResult AS INT = CONCAT(N'PlatformEntitlement|', ...);
```

The checked-in `018_platform_entitlement_foundation.sql` does not contain that declaration. It correctly declares an
`NVARCHAR(100)` lock resource from the hash, declares `@LockResult INT` without an initializer, and assigns the integer
return code from `sys.sp_getapplock`. The likely cause is SSMS Always Encrypted parameterization while migration 018
was manually executed. This finding distinguishes an incorrectly installed object from correct repository history.

The governed correction is the additive successor
`020_platform_entitlement_procedure_repair.sql`. It uses `CREATE OR ALTER PROCEDURE` to recreate both affected
procedures completely from migration 018:

- `dbo.PlatformEntitlement_AssignToUser`
- `dbo.PlatformEntitlement_RevokeFromUser`

It does not modify schema, catalog data, assignments, authorization versions, audit history, Auth/token behavior, or
tenant databases, and it does not grant `SecurityAudit.View`.

## Contract preservation

The repaired assignment procedure retains exact active-entitlement lookup, the authoritative Identity user string,
one-active-assignment protection, historical rows, application-lock and row-lock concurrency, atomic authorization
version increment, and one explicit-column `PlatformEntitlementAssigned` audit insert.

The repaired revocation procedure retains active-assignment lookup, historical row preservation, revocation metadata,
application-lock and row-lock concurrency, atomic authorization version increment, and one explicit-column
`PlatformEntitlementRevoked` audit insert. Neither procedure introduces a tenant or clinical-user dependency.

`PlatformEntitlementProcedureRepairTests` compares both complete migration-020 procedure bodies with their normalized
migration-018 source bodies. It also verifies expected parameters/types and table/audit references, excludes generated
Always Encrypted `@p...` artifacts and the observed `INT = CONCAT(...)` corruption, excludes schema/data reset and grant
statements, verifies migration 020 is unique, verifies tenant migrations remain at 0046, and pins SHA-256 hashes for
every platform migration from 001 through 019. Migration 018 remains byte-for-byte unchanged with SHA-256
`59191CC39EACA18C81303B72FFA7A99DB1C728B682612917C3E3A668E211615A`.

## Verification status

Repository-focused migration-020 tests pass 7/7 in Release configuration, and the combined platform-entitlement and
platform-security-audit focused regression passes 59/59. The repair contains exactly two
`CREATE OR ALTER PROCEDURE` batches and their complete bodies match migration 018.

A read-only inspection of the configured installed database was attempted without exposing credentials. The installed
ODBC 17 `sqlcmd` client could not negotiate the configured encrypted connection, so no live metadata result was
recorded and encryption was not weakened. No approved disposable platform database was identified in configuration.
Consequently the following database-dependent checks remain pending and must not be represented as passed:

- post-020 `sys.procedures`, `sys.parameters`, `sys.sql_modules`, and `OBJECT_DEFINITION` verification;
- assign, duplicate assign, revoke, repeat revoke, and reassignment runtime behavior;
- exact authorization-version and audit-event counts;
- forced-failure transaction rollback;
- corrupted-installed-state repair and existing-data preservation;
- 019-to-020 upgrade and fresh 001-through-020 provisioning.

Run those checks only on an approved disposable/test SQL Server connection, with SSMS Always Encrypted parameterization
disabled if SSMS is used. Apply scripts once in numeric order; do not rerun or edit migration 018. For an existing
database at 019, capture assignment/version/audit counts, apply 020, verify both installed definitions and all four
parameter rows per procedure using SQL Server metadata, run the synthetic lifecycle cases, and confirm the captured
data is unchanged except for the explicitly created synthetic test history. For fresh provisioning, use an isolated SQL
instance because platform scripts target `MicroEMR_Platform` explicitly.

## Readiness

Auth regression passes 30/30. The API suite passes all 669 tests: 668 ran in the restricted environment, and the sole
Playwright PDF test that could not spawn Chromium there passed when rerun with browser-launch permission. The Release
solution build succeeds with zero warnings and zero errors.

The source hotfix is isolated from Step 23B and contains no default entitlement assignment. It is not ready to merge
until the pending database runtime, upgrade, and fresh-provisioning checks are completed and recorded here. After the
validated hotfix is merged to `main`, Step 23B can update from repaired `main`,
explicitly assign `SecurityAudit.View` through the governed tool, reauthenticate, and resume runtime UI validation.
