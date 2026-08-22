# Step 22A — Invalid tenant membership audit foundation

## Scope

Step 22A adds only the platform persistence foundation for a future `InvalidTenantMembership` denial from the
authenticated Auth tenant-selection workflow. It does not wire `POST /Account/SelectTenant`, change tenant or
membership resolution, alter responses, add tenant-clinical schema, or implement `CrossTenantAccess` or stale API
token auditing.

## Why platform migration 017 is required

Migration 016 cannot accurately represent a rejection that occurs before tenant trust and permission authorization:

- `TargetTenantUid` is reserved for an authoritative resolved tenant and cannot store rejected input;
- there is no requested-tenant field;
- `RequiredPermission` is non-null even though tenant selection precedes permission evaluation;
- existing reason, capability, source, and shape constraints do not permit an Auth tenant-selection event;
- no narrow persistence procedure exists.

`017_platform_tenant_security_audit.sql` is additive. Platform migrations 001–016 and all tenant migrations through
0046 remain unchanged.

## Requested versus trusted tenant

The new nullable `RequestedTenantUid UNIQUEIDENTIFIER` column stores only the tenant UID submitted through the future
governed selection flow. It is explicitly untrusted context. For `InvalidTenantMembership`, `RequestedTenantUid` must
be non-null/nonempty while `TargetTenantUid` must be null. The procedure performs no tenant, membership, database,
clinical actor, patient, or resource lookup and never converts the requested value into trusted tenant context.

All existing denial reasons require `RequestedTenantUid IS NULL`; their established `TargetTenantUid` semantics are
unchanged.

## RequiredPermission nullability and compatibility

Migration 017 changes `RequiredPermission` to nullable. The capability constraint explicitly guards the existing
branch with `RequiredPermission IS NOT NULL` and retains every governed capability/permission pair. The reason-specific
shape additionally requires a non-null permission for `MissingPermission`, `Encounters.View` for
`CrossPatientOwnership`, and `Encounters.Edit` for `UnresolvedClinicalActor`.

Only `TenantSelection` permits `RequiredPermission IS NULL`. The constraint is written this way because SQL Server
CHECK constraints accept `UNKNOWN`; merely adding a null branch without an explicit non-null guard would weaken the
existing mappings.

## Governed event shape

The only new denial reason is `InvalidTenantMembership`. Its enforced shape is:

- `EventType = SecurityAccessDenied` and `Outcome = Denied` under existing global constraints;
- nonempty opaque `ActorSubject`;
- `ClinicalUserId = NULL`;
- `TargetTenantUid = NULL`;
- nonempty `RequestedTenantUid`;
- `Capability = TenantSelection`;
- `RequiredPermission = NULL`;
- `SourceApplication = MicroEMR.Auth`;
- optional bounded correlation;
- null requested/authoritative patient and resource ownership fields.

The revised source constraint permits `MicroEMR.Auth` only for `InvalidTenantMembership`; existing reasons remain
limited to `MicroEMR.Api` or `MicroEMR.Web`, with CrossPatientOwnership and UnresolvedClinicalActor still API-only in
their reason shapes. `CrossTenantAccess`, `InvalidTenantClaim`, and other future reasons/capabilities are not added.

## Stored procedure

`dbo.PlatformSecurityAudit_RecordInvalidTenantMembership` accepts only:

- `ActorSubject`;
- `RequestedTenantUid`;
- `SourceApplication`;
- optional `RequestCorrelationId`.

It rejects null/empty/oversized subject, null/empty requested tenant, any source except `MicroEMR.Auth`, and oversized
source/correlation values. It fixes event type, outcome, denial reason, `TenantSelection`, null permission, null
clinical user, null trusted tenant, and null ownership fields; generates UID/time; and inserts exactly one row. It
does not overload or redefine the three existing security procedures.

## Application and infrastructure contract

`InvalidTenantMembershipSecurityEvent` carries the same narrow subject/requested-tenant/source/correlation contract.
`IPlatformSecurityAuditRepository.RecordInvalidTenantMembershipAsync` and
`SqlPlatformSecurityAuditRepository` call only the new platform stored procedure with typed parameters. There is no
direct SQL insert, error swallowing, retry, queue, or runtime Auth/controller call. Persistence exceptions propagate
to the future Step 22B owner.

The actor subject remains the authenticated opaque OIDC/Identity subject. It is not parsed, transformed into a
numeric clinical ID, or resolved in a tenant database. ClinicalUserId and TargetTenantUid are intentionally null.

## Automated verification

Focused source/contract tests cover:

- nullable requested tenant addition and permission alteration;
- exact reason, source, and capability governance;
- explicit non-null preservation for existing permissions;
- MissingPermission, CrossPatientOwnership, and UnresolvedClinicalActor shape compatibility;
- exact InvalidTenantMembership null/non-null shape;
- procedure parameters, fixed semantics, one insert, and rejection validation;
- stored-procedure-only repository behavior and no Auth runtime wiring;
- no administrative audit/procedure change;
- unique platform migration 017 and tenant maximum 0046;
- immutable SHA-256 coverage through platform migration 016.

The focused InvalidTenantMembership and existing platform security-audit regression set passes 69/69.

## SQL migration verification

A disposable SQL Server LocalDB upgrade applied migrations 014, 015, 016, and 017 in sequence. Migration 017 added a
16-byte nullable requested-tenant column, made RequiredPermission nullable, and created the new procedure. After the
upgrade, the existing MissingPermission, CrossPatientOwnership, and UnresolvedClinicalActor procedures each inserted
a valid row with `RequestedTenantUid = NULL`. The new procedure inserted exactly one row with the requested tenant,
null trusted tenant/clinical user/permission/ownership, `TenantSelection`, `MicroEMR.Auth`, and correlation. An attempt
using `MicroEMR.Api` was rejected with procedure error 51905.

Fresh platform provisioning was executed in a separate disposable LocalDB instance using the production sequence and
correct database context. It remains blocked at the pre-existing immutable
`013_access_security_stabilization.sql`: SQL Server reports incorrect syntax near `MicroEMR:AccessAdmin:` because an
expression is passed directly to the named `sp_getapplock @Resource` procedure argument. Migrations 014–017 are not
reached. Step 22A does not modify applied migration 013. Therefore the supported 016→017 upgrade and migration-017
behavior are verified, but fresh provisioning through 017 cannot be claimed until migration governance resolves the
existing migration-013 defect.

## Step 22B readiness and deferred scope

The persistence contract is ready for Step 22B to wire exactly one authenticated, valid-pending-selection POST
mismatch after current membership recomputation, with response preservation and duplicate/persistence-failure handling.
No further database change is required for that exact slice.

`CrossTenantAccess`, authorization changes, API stale-token membership events, operational tenant/database outages,
and all other tenant-denial workflows remain deferred. No safe clinical-resource CrossTenantAccess detection point
currently exists.
