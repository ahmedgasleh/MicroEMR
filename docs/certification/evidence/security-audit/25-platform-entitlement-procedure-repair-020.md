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

A dedicated SQL Server 2025 LocalDB 17.0 instance provided the disposable platform environment. A temporary .NET 10
harness used `Microsoft.Data.SqlClient`, Windows integrated authentication, `Encrypt=True`, and
`TrustServerCertificate=False`; the configured application database was not used for these validation scenarios and
SQL encryption was not weakened. Platform migrations are manually governed: corrected historical migration 013 is
for fresh provisioning only, while an existing database already through 019 must not rerun 001–019 and advances by
applying successor migration 020 only. See the approved exception evidence in
`26-platform-migration-013-fresh-provisioning-repair.md`.

Fresh provisioning applied every current platform script 001–020 in numeric order. Corrected migration 013 and
migrations 014–020 all succeeded; no tenant migration ran. For the existing-upgrade scenario, a separate disposable
database was prepared through 019, an explicit existing-data marker and baseline table counts were captured, and the
upgrade operation executed only `020_platform_entitlement_procedure_repair.sql`. Migrations 013, 018, and 019 were not
rerun. Counts for tenant, entitlement catalog, assignments, authorization state, platform audit, and security audit
were identical before and after 020, including preservation of the marker.

The incident fixture prepared another 019 database and replaced only the two installed procedure bodies with the
observed `DECLARE @LockResult AS INT = CONCAT(...)` shape. Both corruption signatures were confirmed and assignment
reproduced SQL error 245. Applying only migration 020 removed both signatures, preserved all captured data counts, and
restored both full governed definitions.

Post-020 metadata checks through `sys.procedures`, `sys.parameters`, and `sys.sql_modules` confirmed two procedures and
eight intended parameters. Both definitions declare `@LockResource NVARCHAR(100) = CONCAT(...)`, declare
`@LockResult INT` without an initializer, pass `@LockResource` to `sp_getapplock`, retain their transaction and explicit
audit inserts, and contain no `INT = CONCAT(...)` corruption.

The real Infrastructure entitlement repository exercised a synthetic Identity-style user identifier. Assignment
changed version 0→1, created exactly one active/history row and one contract-correct
`PlatformEntitlementAssigned` event, and returned `SecurityAudit.View`. Duplicate assignment returned 52006 with no
row, version, or audit change. Revocation changed version 1→2, retained the historical row, removed it from the active
read, and wrote exactly one `PlatformEntitlementRevoked` event. Repeat revoke returned 52007 with no change.
Reassignment changed version 2→3, retained the revoked history, produced exactly one current active row and one new
assignment audit. Tenant and `PlatformSecurityAuditEvent` counts did not change.

Atomicity was forced safely in the disposable database with temporary audit-table constraints targeted to synthetic
test users. An assignment audit failure rolled back the assignment row and authorization state. A revocation audit
failure retained the active assignment and original version. The temporary constraints existed only in the disposable
database and production procedures were not changed.

An isolated Auth/API/Web environment was started successfully with the disposable platform connection, and the known
test reviewer was assigned then revoked through the repository with authorization version 1→2. Full UI interaction
could not be completed because the in-app browser connection lacked its required sandbox-policy metadata. Entitled
list/filter/detail/review-audit behavior, unauthorized disclosure behavior, token refresh, and post-revocation browser
access therefore remain explicitly unverified. The test grant was revoked and the standard development applications
were restored. The earlier failed assignment on the affected configured database remains **NOT VERIFIED ON AFFECTED
DATABASE** for assignment/version/audit side effects.

## Readiness

Auth regression passes 30/30. The API suite passes all 669 tests: 668 ran in the restricted environment, and the sole
Playwright PDF test that could not spawn Chromium there passed when rerun with browser-launch permission. The Release
solution build succeeds with zero warnings and zero errors.

Migration 020 database repair behavior is validated for fresh, existing-correct, and existing-corrupted databases. It
contains no default entitlement assignment. Final merge readiness remains conditional on completing the isolated Step
23B browser/token authorization checks or explicitly accepting that separately documented runtime limitation.
