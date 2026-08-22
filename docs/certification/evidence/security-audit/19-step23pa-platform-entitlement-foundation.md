# Step 23P-A — Platform entitlement persistence foundation

## Scope and outcome

Step 23P-A implements only platform persistence and controlled administration for explicit platform entitlements.
It adds platform migration 018, Application contracts/service, an Infrastructure stored-procedure repository, a
narrow configuration-gated DatabaseTool assignment/revocation path, and focused tests. It does not issue token
claims, validate refresh tokens, add an authorization policy/handler, grant a role, build Security Audit review, or
change tenant permissions/UI.

## Migration 018 and catalog

`018_platform_entitlement_foundation.sql` is the unique next platform script after 017. It creates
`dbo.PlatformEntitlement` with a stable GUID, binary-collated unique bounded key, display name, description, active
flag, server creation time/actor, and row version. The binary collation makes canonical key comparisons exact-case.
The check constraint rejects wildcard semantics and governs the current catalog to the single approved key.

Migration 018 seeds exactly one active catalog row: `SecurityAudit.View`. It creates no assignment. In particular,
`PlatformAdministrator`, `PlatformOperator`, System/Web Administrator, tenant administrators, migration users, and
development users receive nothing automatically. There is no role-entitlement mapping.

## Assignment history and identity

`dbo.UserPlatformEntitlement` targets the ASP.NET Identity `ApplicationUser.Id`/OIDC `sub` as `NVARCHAR(450)`, matching
the established cross-database platform-membership identity convention. It has no clinical user or tenant key and no
cross-database foreign key. The trusted service/DatabaseTool validates the target through the existing Auth user
lookup before mutation.

Each assignment occurrence retains assigned time/actor, nullable revoked time/actor, and row version. Revocation is
an update of the active occurrence; rows are never deleted. A filtered unique index on user plus entitlement permits
only one unrevoked occurrence. A revoked entitlement can be explicitly assigned again, producing a new historical
occurrence.

## Authorization version

`dbo.PlatformAuthorizationState` is keyed by Identity user ID and stores a monotonic `BIGINT` authorization version.
A user with no state/history reads as version 0 without requiring a row. The first assignment creates version 1;
each subsequent successful assignment or revocation increments it atomically in the mutation transaction. Version
state is platform-owned and does not alter ASP.NET Identity.

## Governed procedures

`dbo.PlatformEntitlement_AssignToUser` validates bounded user, exact active entitlement, actor, and nonempty
correlation. It serializes the user/entitlement pair with an exclusive application lock whose fixed-length resource
uses SHA-256 of the normalized pair, takes update/hold locks, rejects an existing active assignment, inserts one
assignment, increments version, and writes one administrative audit event in one `XACT_ABORT` transaction. A failed
lock or validation produces no partial assignment/version/audit evidence.

`dbo.PlatformEntitlement_RevokeFromUser` uses the same concurrency boundary, requires an active occurrence, marks it
revoked, increments the existing version, and writes one audit event transactionally. Repeated or simultaneous
revocation has one winner and a governed rejection for the other caller.

`dbo.PlatformEntitlement_GetActiveForUser` returns only canonical keys whose assignment is unrevoked and whose
catalog entry remains active. It has no tenant or Auth-database query. `dbo.PlatformAuthorization_GetVersionForUser`
returns the current version or stable 0 for a user without state. These procedures are the only Auth-facing database
contract prepared in this step; no Auth integration is present.

## Administrative audit compatibility

Successful changes use the existing unchanged `dbo.PlatformAuditEvent` shape with explicit column lists:

- actions `PlatformEntitlementAssigned` and `PlatformEntitlementRevoked`;
- actor type `PlatformAdminTool` and explicit actor subject;
- null target tenant and exact target Identity user;
- `Succeeded`, server UTC, required correlation GUID;
- procedure-generated JSON containing only the governed entitlement key.

Migration 006 and every legacy positional audit insert remain unchanged. Entitlement administration does not write
to `PlatformSecurityAuditEvent`. Because audit insertion shares the mutation transaction, an audit write failure
rolls back the assignment/revocation and version increment.

## Repository, service, and bootstrap

`IPlatformEntitlementRepository` exposes active-key read, version read, assignment, and revocation. The SQL repository
uses only the four typed stored procedures and contains no direct DML. `IPlatformEntitlementService` validates exact
known keys, bounded identities, actor/correlation, configured Auth lookup, and target Identity existence before a
mutation. Account active/locked status remains an Auth token concern for Step 23P-B, as approved by the design.

The existing DatabaseTool already requires `PlatformAdministration:Enabled=true` and an explicit configured actor.
Step 23P-A adds only:

```text
platform-entitlement assign|revoke --user-id ID --entitlement SecurityAudit.View --confirm ID
```

It requires exact operation, target, entitlement, and confirmation; generates a correlation GUID; calls the governed
service/procedure; and prints no secret. It performs no automatic assignment or role fallback. This is the approved
near-term bootstrap path; a broad management API/UI remains deferred.

## Concurrency and runtime verification

A disposable SQL Server LocalDB platform schema was constructed through 017 and migration 018 applied successfully.
Runtime results were:

- no-history version returned 0;
- assign returned version 1 and the active key;
- revoke returned version 2, retained one history row, and returned no active key;
- explicit reassign returned version 3 with two history rows and one active row;
- audit counts matched two assignments and one revocation;
- duplicate active assignment returned error 52006 and caused no version/audit/row side effect;
- inactive catalog entry was excluded from reads and assignment returned error 52005;
- simultaneous assignment produced one success and one 52006 rejection;
- simultaneous revocation produced one success and one 52007 rejection;
- the concurrency final state was version 2, one history row, zero active rows, and exactly one audit event per
  successful change.

The disposable database was removed after verification.

## Migration safety and inherited fresh-provisioning defect

Platform source migrations 001–017 retain their established SHA-256 hashes and 018 is unique. Tenant migration files
remain unchanged through 0046 and no tenant migration was introduced. The supported 017→018 transition succeeds and
creates the exact binary-collated catalog, one seed, procedures, indexes, and version-zero behavior.

A complete fresh 001→018 execution cannot be claimed. In a continuous SQL session with required quoted-identifier
settings, the unchanged migration 013 fails while defining `PlatformMembership_Deactivate` with SQL error 102 near
`MicroEMR:AccessAdmin:`. This is the same pre-existing migration-013 defect documented in earlier security-audit
evidence. Step 23P-A does not modify applied migration 013. Migration 018 itself passed both creation and runtime
verification once the platform schema was at 017. Resolving fresh provisioning requires separate migration
governance (for example an approved corrective successor/bootstrap strategy), not an edit hidden in this step.

## Automated verification

Focused tests cover:

- minimal catalog schema, exact binary key, one canonical seed, no speculative keys/wildcards;
- historical assignments, no delete, filtered one-active constraint, no tenant/clinical identity;
- platform-owned monotonic version and stable zero default;
- active/inactive read semantics and narrow output;
- transactional application locks, duplicate/repeat rejection, version increments, and audit count;
- explicit-column `PlatformAuditEvent` compatibility and no security-denial writes;
- stored-procedure-only repository and DI registration;
- service key/identity validation and assign/read/version/revoke behavior;
- explicit confirmed DatabaseTool command with no role fallback;
- unique migration 018, tenant maximum 0046, and immutable hashes for 001–017;
- existing platform administration and all four security-denial foundation regressions.

## Step 23P-B readiness

Step 23P-B can load active entitlements and version through the new read contracts, emit access-token-only
`platform_entitlement`/version claims, validate refresh version, and add exact-key API/Web authorization without
another database migration. It must not infer entitlements from roles or tenants. Before deployment, operations must
apply migration 018 through the approved platform process and separately resolve the inherited fresh-provisioning
governance issue for brand-new platform databases.
