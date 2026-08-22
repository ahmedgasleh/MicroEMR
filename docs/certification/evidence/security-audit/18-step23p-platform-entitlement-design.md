# Step 23P — Platform entitlement foundation design

## Status and decision

This is design, analysis, and test planning only. It adds no migration, schema, entitlement, token claim, authorization
handler, assignment, API, UI, role mapping, or production behavior.

MicroEMR needs a small platform-entitlement model before `SecurityAudit.View` can govern central security-audit
review. The model should store a controlled catalog and explicit user assignments in `MicroEMR_Platform`, emit exact
multi-valued entitlements into access tokens, and authorize them through a reusable exact-key policy. It must remain
independent of global roles, tenant roles, tenant effective permissions, selected tenant, and clinical user identity.

Recommended migration sequence follows chronological delivery:

1. `018_platform_entitlement_foundation.sql` for the platform catalog, assignments, queries, audited changes, and
   authorization-state version.
2. Auth token issuance and API/Web authorization wiring after the database contract is reviewed.
3. `019_platform_security_audit_review.sql` when Step 23A resumes.

No tenant migration is required; the tenant migration maximum remains 0046.

## Current authorization inventory

### Global identity roles

`MicroEMR.Auth` uses ASP.NET Core Identity in `MicroEMR_Auth`. `ApplicationUser.Id` is the stable identity key and adds
`FullName`, `IsActive`, and a legacy nullable `ClinicId`. Auth creates an OIDC `sub` from `ApplicationUser.Id`, loads
Identity roles through `UserManager.GetRolesAsync`, and emits each as a `role` claim. Seeded roles include
`SystemAdmin`, `ClinicAdmin`, `Physician`, `Nurse`, `MedicalAssistant`, and `Reception`.

These are global identity roles, not independent platform entitlements. Auth also contains legacy `AppModule`,
`AppPermission`, `RolePermission`, and `AppMenuButton` tables seeded with underscore-style permissions. No current
runtime reader or token issuer consumes those permission rows; assignments are role-derived. Reusing them would not
satisfy explicit per-user platform entitlement assignment and would conflate an inactive legacy menu model with the
new security boundary.

`PlatformRoles.Administrator` (`PlatformAdministrator`) and `PlatformRoles.Operator` (`PlatformOperator`) are only
constants in Application contracts. No current authorization policy, token logic, controller, or assignment path
uses them. The Web `AppRoles.Administrator` value is a separate legacy `Administrator` role. Leave all of these
untouched and do not map them implicitly to entitlements.

### Tenant authorization

Tenant membership and tenant roles are stored in `MicroEMR_Platform`, keyed by Identity user ID and tenant UID. Auth
resolves an active membership and emits tenant ID/key/name and tenant-role claims. API middleware establishes trusted
`TenantContext`. Tenant effective permissions such as `Patients.View`, `Encounters.Edit`, `Documents.View`, and
`Reports.Export` come from tenant access profiles and user overrides and are loaded server-side for that trusted
tenant. They are intentionally not token claims.

Tenant permissions cannot authorize central platform review: they require an active trusted membership, vary by
tenant, cannot govern events with no trusted tenant, and would let clinic administration influence cross-tenant
security access. They remain unchanged.

### Tokens, refresh, and logout

Auth uses OpenIddict authorization-code flow with PKCE and refresh-token flow. Web requests `offline_access`, saves
tokens, and forwards its access token to API. Claims destinations currently place role, subject, and tenant claims in
the access token, with selected claims also in the identity token. Access-token encryption is disabled; tokens are
signed. No explicit access/refresh lifetime is configured in repository code, no entitlement/security-stamp claim is
emitted, no API security-stamp validation exists, and logout signs out the Identity cookie/OpenIddict session but
does not establish immediate rejection of an already issued self-contained access token.

The current authorization endpoint rejects globally inactive users before token issuance and requires successful
tenant enrichment. Existing tokens therefore remain bounded by their configured/library lifetime rather than being
continuously revalidated against `IsActive`, security stamp, membership, or future entitlement state.

## Three separate concepts

| Concept | Authority and scope | Must not imply |
|---|---|---|
| Global identity role | ASP.NET Identity role claim; broad identity classification | Any platform entitlement unless a future explicit, audited bundling policy is approved |
| Tenant effective permission | Platform DB profile/override evaluated for one trusted tenant | Cross-tenant or pre-tenant platform access |
| Platform entitlement | Exact, governed platform capability explicitly assigned to an Identity subject | Tenant permission, clinical identity, role membership, wildcard, or another entitlement |

The governing rule is: a user has `SecurityAudit.View` only when an active catalog entry and an active explicit
assignment exist for that exact Identity user ID.

## Authoritative identity and storage boundary

Assignments target the ASP.NET Identity `ApplicationUser.Id` string. That same value is the OIDC `sub` and already
crosses into `MicroEMR_Platform` as `UserTenantMembership.UserId`; no duplicate identity map or tenant-local
`ClinicalUserId` is needed. As with memberships, do not add a cross-database foreign key to `MicroEMR_Auth`.
Assignment procedures must validate nonblank bounded user IDs, and the management service must verify the Identity
user exists before assignment without copying profile data into the platform database.

Store platform entitlements in `MicroEMR_Platform`, not the Auth EF schema or tenant clinical databases. This keeps
the platform authorization catalog beside platform administration/audit data and makes it available to Auth through
the existing platform-infrastructure connection. Auth remains authoritative for whether the global account exists,
is active, locked, and allowed to authenticate.

## Minimal database model

### `PlatformEntitlement`

Recommended fields:

- `EntitlementKey NVARCHAR(100)` primary or unique key, exact ordinal/case-normalized canonical value;
- `DisplayName NVARCHAR(150)` and `Description NVARCHAR(500)` for governed administration;
- `IsActive BIT`;
- `CreatedAtUtc DATETIME2(7)` and `CreatedBy NVARCHAR(450)`.

Migration 018 should seed exactly one canonical entry: `SecurityAudit.View`. Do not seed manage, export, wildcard,
bundle, or speculative entitlements. Runtime callers cannot create arbitrary catalog entries; future keys require a
reviewed migration and matching code catalog.

### `UserPlatformEntitlement`

Use one current assignment row per `(UserId, EntitlementKey)` with:

- server-generated assignment UID;
- bounded Identity `UserId` and governed entitlement key;
- `AssignedAtUtc`, `AssignedBy`;
- nullable `RevokedAtUtc`, `RevokedBy`;
- row version for optimistic concurrency.

A unique key prevents duplicate assignment. Assigning an active assignment is rejected or treated as an explicitly
tested idempotent no-op without a false audit event. Reassignment after revocation reactivates the existing row or
creates a new history row according to the procedure contract; prefer a new immutable assignment occurrence if a
filtered unique active index is used. In either case, historical assign/revoke facts must not be overwritten.

### `PlatformAuthorizationState`

Maintain a per-user monotonically increasing `AuthorizationVersion BIGINT` (or equivalent row-version projection)
changed in the same transaction as every assignment/revocation. Auth places the value in the token. This provides a
small invalidation contract without turning entitlements into roles or an unbounded claim bag.

## Governed procedures and least privilege

Migration 018 should add narrow stored procedures for:

- listing active canonical entitlements for token issuance by exact user ID;
- reading the user's authorization version;
- assigning an exact known active entitlement with optimistic/idempotency rules;
- revoking an exact active assignment;
- reading assignments for later administration.

No generic SQL, comma-separated arbitrary keys, wildcard matching, direct table DML, tenant context, or role-based
implicit assignment is allowed. Auth receives execute rights only for the effective-entitlement/version reads. A
separate trusted administration principal receives execute rights for assignment/revocation. Application principals
do not receive direct table permissions.

## Bootstrap and assignment authority

Viewing security audit and managing platform entitlements are separate powers. `SecurityAudit.View` must never grant
assignment authority. A possible future `PlatformEntitlements.Manage` should not be seeded or required until a
governed management workflow exists.

The smallest safe bootstrap is an offline, configuration-gated DatabaseTool command run by an approved platform
security administrator. It uses the narrow assignment/revocation procedures, requires an explicit actor ID and exact
target Identity user ID, verifies the target in Auth, displays the intended change, and produces the same platform
audit evidence as every later management path. Database operators should not insert rows directly. Existing roles
do not confer bootstrap authority in application code.

After bootstrap, a later Platform Administration → Users → Platform Access surface may be designed. Its authorization
must be separately approved—likely an explicitly assigned management entitlement—and must not be part of the first
foundation. This avoids circularly granting `SecurityAudit.View` or treating `PlatformAdministrator` as an automatic
super-entitlement.

## Assignment and revocation audit

Use the existing `dbo.PlatformAuditEvent` successful administrative stream. Its unchanged columns can represent the
actions cleanly:

| Column | Assignment/revocation value |
|---|---|
| `ActorUserId` | Explicit authenticated/bootstrap administrator subject |
| `ActorType` | Governed value such as `PlatformAdministrator` or `BootstrapTool`, approved by procedure |
| `Action` | `PlatformEntitlementAssigned` or `PlatformEntitlementRevoked` |
| `TargetTenantUid` | NULL; platform entitlement is global |
| `TargetUserId` | Exact target Identity user ID |
| `Outcome` | `Succeeded` |
| `OccurredAtUtc` | Server UTC |
| `CorrelationId` | Required caller correlation GUID |
| `DetailsJson` | Procedure-generated JSON containing only the exact entitlement key |

Add a narrowly governed explicit-column audit insert inside each assignment/revocation transaction. Do not alter
`PlatformAuditEvent`, migration 006, or its legacy positional insert procedures. Failed validation is operationally
logged; if failed privileged attempts later require durable audit, design that separately rather than claiming a
successful change. Successful entitlement administration must not be written to `PlatformSecurityAuditEvent`.

## Canonical token claim

Use the exact multi-valued claim type `platform_entitlement`. It is concise, unambiguously separate from `role`,
tenant-role claims, OAuth scopes, and tenant effective permissions, and follows the current lower-case underscore
custom-claim convention. Define it once in shared Application security contracts together with an exact governed
entitlement catalog.

Emit one claim per active, explicitly assigned, code-recognized entitlement. Reject duplicate values and never emit
inactive/unknown catalog keys, wildcard values, display names, descriptions, assignment metadata, or tenant IDs as
part of this claim. Platform entitlements should remain few; impose a small reviewed maximum per user and fail token
issuance if corrupt data exceeds it rather than silently truncating.

The claim destination is **access token only**. Do not include platform entitlements in the identity token: UI
identity display does not need them, identity tokens may outlive UI assumptions, and API authorization is
authoritative. Web can read the saved/validated access-token claim for navigation hints or call a protected API; it
must not treat navigation as enforcement.

## Auth loading and token issuance

At the authorization endpoint, after the active Identity user is established and before destinations/sign-in are
finalized, an Auth application service queries active explicit entitlements by `ApplicationUser.Id`, intersects them
with the compiled governed catalog, and adds `platform_entitlement` claims plus a `platform_authorization_version`
claim. This lookup is independent of tenant enrichment.

The current endpoint nevertheless requires a resolved tenant before issuing a normal Web token. The entitlement
authorization architecture must not require `TenantContext`, tenant claims, or a clinical user, but truly issuing a
platform-only token to a user with no active tenant would require a separately reviewed platform-client/login flow.
Do not weaken the current tenant-login rule incidentally in Step 23P.

Refresh-token issuance is enabled, but the current built-in token endpoint does not demonstrate reloading current
Identity or platform authorization state. Step 23P-B must add an explicit refresh validation/claim-regeneration hook:

1. resolve `sub` to an active, nonlocked Identity user;
2. compare the refresh principal's authorization version with current platform state;
3. on mismatch, reject that refresh and require interactive sign-in (or regenerate only after equivalent full
   authentication checks are explicitly proven);
4. on match, reload the active governed entitlements rather than trusting stale entitlement claims.

Configure and test explicit access- and refresh-token lifetimes rather than relying on package defaults. The smallest
safe revocation model is: assignment/revocation increments the user authorization version; refresh is rejected when
stale; new interactive tokens reflect current state; and a self-contained access token remains valid only until a
short, explicitly approved access-token expiry. Immediate mid-lifetime revocation would require API online state
checks or reference-token introspection and is deferred unless the approved risk window requires it. Logout alone is
not entitlement revocation.

Disabled/locked users are rejected during new authorization and refresh validation. Their assignments remain for
history but are ineffective. An inactive catalog entitlement is omitted from all new tokens; assignments remain
auditable and cannot be newly granted. API policy also recognizes only active code-catalog keys, so a forged unknown
claim fails closed.

## API authorization design

Add a reusable exact-key `PlatformEntitlementRequirement` and
`PlatformEntitlementAuthorizationHandler`, plus a policy provider using names such as
`PlatformEntitlement:SecurityAudit.View`. The handler succeeds only when:

1. the requested key is a known exact canonical platform entitlement; and
2. the authenticated principal has an exact `platform_entitlement` claim with that value.

It performs no role fallback, tenant-permission lookup, wildcard/prefix match, selected-tenant requirement, clinical
user resolution, or resource enrichment. Unknown policy keys fail closed. A `RequirePlatformEntitlement` attribute
may provide compile-time-friendly endpoint metadata.

The initial `SecurityAudit.View` consumer must enforce this policy at the API search/detail boundary. Missing
entitlement uses normal 403 behavior. Existing `MissingPermission` auditing is tenant-permission-specific and should
not be extended or conflated in Step 23P; platform entitlement denial auditing requires a later governed design.

## Web behavior

Web may use the same shared claim constant and policy to protect its future controller and hide the future navigation
item. Because the access token is saved and forwarded to API, the claim is available to the Web authentication
session according to OIDC claim mapping; Step 23P-B tests must prove this explicitly. API enforcement remains
authoritative even if Web claim mapping or navigation fails. Web never queries entitlement tables directly and never
uses an identity role or tenant permission as fallback.

## Least privilege and non-goals

- No entitlement has a default assignment, including `SecurityAudit.View`.
- No role, tenant profile, clinic administrator, clinical user, or bootstrap seed inherits an entitlement.
- Exact assignments are deny-by-absence; no wildcard, hierarchy, bundle, group, delegation, ABAC, external IAM, or
  dynamic policy expression is introduced.
- Entitlements cannot bypass global account disable/lock, authentication, or downstream data minimization.
- Platform entitlement administration and security-audit review/export are distinct capabilities.
- No tenant database, patient data, clinical identity, or tenant selection participates in entitlement evaluation.

## Migration safety and numbering

Current source has unique platform migrations through 017 and tenant migrations through 0046. Because entitlement is
now the chronological prerequisite, reserve `018_platform_entitlement_foundation.sql` for Step 23P-A. The resumed
security-audit review foundation becomes `019_platform_security_audit_review.sql`. Do not create either migration in
this design step, and never edit or manually rerun migrations 001–017. Step 23P-A must prove both 017→018 upgrade and
fresh provisioning through 018 while preserving every existing migration hash and positional
`PlatformAuditEvent` writer.

## Implementation slices

### Step 23P-A — database and audited persistence

Create platform migration 018 only: governed catalog with exactly `SecurityAudit.View`, explicit historical/current
assignment model, per-user authorization version, least-privilege read/assign/revoke procedures, explicit-column
`PlatformAuditEvent` writes, Infrastructure repositories, and database/application contract tests. Add no token
claim, authorization policy, consumer endpoint, UI, broad role assignment, or tenant migration. Include the
configuration-gated DatabaseTool bootstrap command only if security review approves that operational boundary;
otherwise stop after persistence and use an approved DBA execution runbook for the first explicit assignment.

### Step 23P-B — token and authorization wiring

Add shared exact catalogs/claim constants, Auth entitlement loading, access-token-only destinations,
authorization-version refresh validation, explicit token lifetimes, API/Web requirement/handler/policy provider, and
tests. Do not yet build Security Audit review. Prove role/tenant separation, no implicit assignments, disabled state,
refresh revocation behavior, and operation without `TenantContext`.

### Step 23P-C — management mechanism

A separate slice is necessary unless the Step 23P-A bootstrap command is approved and sufficient for the near term.
It should add only an audited, explicitly authorized assignment/revocation API and later a Platform Access management
UI. It needs a separately approved administration entitlement or controlled bootstrap authority; it must not infer
authority from `SecurityAudit.View`. Step 23A can proceed after one safe explicit assignment path exists, without
waiting for a general management UI.

### Resumed Step 23A

Create platform migration 019 with the bounded security-audit search/detail procedures and successful review-access
auditing, then add the `SecurityAudit.View`-protected repository/service/API. No additional entitlement architecture
or database migration should be required for authorization.

## Future automated test plan

### Persistence and audit

1. Migration seeds exactly the canonical active `SecurityAudit.View` catalog entry and no speculative key.
2. Exact user assignment succeeds; duplicate active assignment is prevented without duplicate success audit.
3. Revocation succeeds once and preserves assignment history; repeated/missing revocation is governed.
4. Unknown/inactive entitlement and blank/overlong/unknown Identity user are rejected.
5. Assignment and revocation increment only the target user's authorization version transactionally.
6. Each successful change writes exactly one correctly shaped `PlatformAuditEvent`; audit failure rolls back change.
7. Existing administrative positional inserts and security-denial writers remain unchanged and pass regressions.

### Token issuance and lifecycle

8. Explicitly entitled active user receives one exact `platform_entitlement=SecurityAudit.View` access-token claim.
9. Non-entitled user and inactive catalog/assignment receive no claim.
10. Entitlement does not appear in identity token; role and tenant claims remain unchanged.
11. Tenant effective permissions are never emitted as platform entitlements and legacy Auth `AppPermission` rows are
    ignored.
12. Disabled/locked user cannot receive or refresh tokens despite retained assignment.
13. Assignment is reflected in a new token; revocation removes it from a new token.
14. Refresh with unchanged version reloads current active entitlements; stale version is rejected.
15. Existing access token behavior matches the explicit bounded lifetime, and logout is not misrepresented as
    immediate JWT revocation.
16. Corrupt duplicate/unknown/excessive entitlement results fail closed.

### Authorization separation

17. Exact claim authorizes `PlatformEntitlement:SecurityAudit.View`; missing or wrong-case claim is denied.
18. Unknown entitlement policy and wildcard/prefix values are denied.
19. `PlatformAdministrator`, `PlatformOperator`, Web Administrator/SystemAdmin, tenant ClinicAdministrator, and
    tenant permissions alone never authorize.
20. Policy succeeds without `TenantContext`, tenant claims, or clinical UserId when the exact entitlement exists.
21. Web navigation policy and controller use the shared claim contract, while API independently rejects unauthorized
    calls.

### Migration and regression

22. Platform 018 is unique; migrations 001–017 and their hashes are unchanged.
23. 017→018 upgrade and fresh provisioning through 018 succeed against disposable SQL Server.
24. Tenant migration maximum remains 0046 and every tenant migration hash is unchanged.
25. Full API, Auth, Web/Release build, platform administration, permission, tenant resolution, and security-audit
    regression suites pass.

## Approval gates

Before Step 23P-A, approve the platform database as entitlement authority, Identity user ID key, schema/history
shape, bootstrap operator/runbook, audit representation, and absence of default assignments. Before Step 23P-B,
approve the claim names, access-token-only destination, explicit token lifetimes, acceptable residual access-token
revocation window, refresh rejection behavior, and whether a later platform-only login flow is required. These are
security decisions, not assumptions to hide in implementation.
