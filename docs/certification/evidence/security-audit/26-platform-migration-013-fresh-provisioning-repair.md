# Platform migration 013 fresh-provisioning repair

## Approved historical exception

Fresh platform provisioning was blocked while compiling `013_access_security_stabilization.sql`. Four procedures passed
`CONCAT(N'MicroEMR:AccessAdmin:', @TenantUid)` directly as the named `@Resource` argument to
`sys.sp_getapplock`; SQL Server reported incorrect syntax near `MicroEMR:AccessAdmin:`. This defect had already been
recorded by the Step 21A and Step 22A evidence, and occurs before later successor migrations can execute.

A narrowly scoped historical-migration exception was explicitly approved. The repair changes only the four invalid
application-lock calls in:

- `dbo.PlatformMembership_Deactivate`;
- `dbo.AccessProfile_AssignUser`;
- `dbo.AccessProfile_ReplacePermissions`;
- `dbo.UserPermissionOverride_Set`.

Each procedure now constructs the same lock name in a local `NVARCHAR(255)` variable and passes that variable to
`sp_getapplock`. Lock name, exclusive transaction ownership, timeout, error behavior, transaction boundaries,
authorization behavior, data mutation, and audit behavior are otherwise unchanged.

The previous migration-013 SHA-256 was
`B6B1E60E67281217EAB3C75759C0714053EEFA0F3DCCB57DFC28C425C6139E3D`. The approved corrected SHA-256 is
`154FC46D4BF2DE480EA5FF6FAAC5843A86A881F8662523D112C4187AD0D26AC3`. Immutable-migration tests were updated only
for that explicitly approved hash exception. Focused contract coverage verifies exactly four lock-resource declarations,
exactly four variable-based calls, and absence of the invalid direct-expression form.

## Deployment boundary

This source correction does not roll an installed database back to migration 013. Existing databases at 019 or 020
must not rerun migration 013; they continue forward through the applicable successor migration. The corrected source
exists so a brand-new database can replay the platform sequence successfully.

The repository has no platform migration ledger or platform migration runner. Platform scripts are applied in numeric
order with migration 001 connected to `master` and migrations 002 onward connected to `MicroEMR_Platform`. Tenant
ledger behavior is unrelated and unchanged.

## Runtime verification

A dedicated SQL Server 2025 LocalDB 17.0 instance was created for this validation. A temporary .NET 10 harness used
the same `Microsoft.Data.SqlClient` provider family as the application, Windows integrated authentication,
`Encrypt=True`, and `TrustServerCertificate=False`. No configured application database was accessed and encryption was
not weakened.

The harness created a new `MicroEMR_Platform` and applied every repository platform script from 001 through 019 in
numeric order, including the two development seed scripts. All 19 scripts succeeded. Installed metadata confirmed:

- all four migration-013 procedures contain the local lock-resource and variable-based `sp_getapplock` contract;
- both migration-018 entitlement mutation procedures exist;
- all three migration-019 Security Audit review procedures exist.

Result: `FRESH_PROVISION|PASS|scripts=19|locks=4|entitlement=2|audit=3|ledger=not-present-by-design`.

Migration 020 remains isolated on its own hotfix branch. After this repair is reviewed and merged to `main`, that branch
must update from repaired `main` and repeat fresh provisioning through 020 plus its normal-upgrade, corrupted-state,
metadata, entitlement lifecycle, and atomicity validation. This repair alone does not make migration 020 merge-ready.
