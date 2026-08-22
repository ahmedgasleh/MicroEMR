# Step 22B — Invalid tenant membership runtime audit

## Scope and exact trigger

Step 22B wires `InvalidTenantMembership` only at authenticated `POST /Account/SelectTenant`. The trigger occurs after
the existing pending-selection state has been loaded and validated for expiry and ownership, and after the current
active platform membership set has been recomputed successfully. It runs only when a submitted nonempty parsed
TenantUid is absent from the intersection of that current membership set and the server-held pending allow-list.

It does not audit anonymous requests, missing/malformed/empty TenantUid values, expired/replayed/wrong-user pending
state, antiforgery rejection, membership/platform exceptions, API tenant claims, stale API tokens, tenant database
outages, or ordinary validation. No API tenant middleware or clinical workflow is changed.

## Membership source of truth

The controller continues to call `IUserTenantMembershipService.GetActiveMembershipsAsync` after validating the
pending selection. That service uses the platform active-membership repository; inactive/removed memberships are
excluded by its normal completed result. The selected tenant must also remain in the pending server-side
`AllowedTenantUids`. The form value, old membership list, token tenant claim, tenant metadata, or tenant clinical
database is never treated as membership authority.

If membership recomputation throws, execution never reaches the mismatch branch or recorder. The exception retains
the existing operational path and is not classified as `InvalidTenantMembership`.

## Event ownership and semantics

`TenantSelectionSecurityAuditRecorder` is the single event owner. The Account controller invokes it only at the
governed mismatch boundary; repositories, membership services, authorization endpoints, and middleware do not emit
this event.

It calls the Step 22A repository contract with:

- authenticated opaque Identity/OIDC subject (`ApplicationUser.Id`) unchanged;
- rejected form UID as `RequestedTenantUid`;
- `SourceApplication = MicroEMR.Auth`;
- `RequestCorrelationId = HttpContext.TraceIdentifier`.

Migration 017 and its procedure fix `SecurityAccessDenied`, `Denied`, `InvalidTenantMembership`, `TenantSelection`,
null RequiredPermission, null ClinicalUserId, null TargetTenantUid, and null patient/resource fields. The recorder does
not parse the subject, establish TenantContext, resolve a clinical user, query a tenant database, or enrich patient/
resource information.

The rejected requested tenant is never promoted to `TargetTenantUid`. Tenant trust and clinical context do not exist
at this boundary.

## Precedence, response, and anti-enumeration

Tenant membership denial precedes effective permission, clinical actor, and patient/resource processing. The rejected
selection returns from the existing action before continuation storage or redirect, so MissingPermission,
UnresolvedClinicalActor, CrossPatientOwnership, tenant-local access, and successful selection cannot occur.

The outward behavior is unchanged: the selection page is redisplayed with “The selected clinic is unavailable. Please
try again.” and the currently allowed options. The response does not say whether the submitted tenant exists, whether
a membership exists or is inactive, or whether an audit event was recorded. No 403 was introduced for audit
convenience.

## Persistence failure and duplicate control

The recorder marks `HttpContext.Items` before attempting persistence. Re-entry within one POST produces at most one
attempt/event; separate POST requests may each produce one event. SQL/repository exceptions are caught and
operationally logged with only the governed capability and trace identifier. Selection remains denied and the
existing view/message is returned. No persistence failure can create a continuation, establish a tenant, or turn the
denial into success. No retry queue, worker, or delayed audit was added.

## Automated verification

Focused tests verify:

- exact opaque subject, rejected RequestedTenantUid, Auth source, and correlation contract;
- one event/attempt per request and separate events for separate POST requests;
- no event for anonymous, missing subject, or empty tenant inputs;
- persistence failure containment;
- pending validation → authoritative membership lookup → mismatch → recorder ordering;
- the existing validation message and selection view remain after the recorder call;
- continuation creation remains after, and unreachable from, the mismatch return;
- no API tenant middleware, TenantContext, clinical actor, TargetTenantUid, or CrossTenantAccess wiring.

Existing Auth tenant selection, platform security foundation, MissingPermission, UnresolvedClinicalActor,
CrossPatientOwnership, tenant isolation, and full API/Auth/Release suites remain regression gates.

## Manual runtime verification

Use test identities and tenants only:

1. Give a test user active Tenant A membership, select Tenant A, and confirm normal continuation with no
   `InvalidTenantMembership` event.
2. Through a valid pending selection flow, submit a valid Tenant B UID not in the user's current active memberships.
   Confirm the existing selection page/message, no continuation or Tenant B context, and exactly one event with the
   exact subject/requested UID, null trusted tenant/clinical user/permission/clinical fields, `TenantSelection`, Auth
   source, and correlation.
3. Confirm no MissingPermission, UnresolvedClinicalActor, CrossPatientOwnership, or successful-selection event for
   that request and no tenant clinical database access.
4. Add then deactivate the test user's Tenant B membership before submission. Confirm the same denial/event under the
   completed active-membership result.
5. Reactivate membership and select Tenant B. Confirm normal continuation and no denial event.
6. Use an automated/integration fault for platform membership lookup. Confirm the operational failure is not recorded
   as invalid membership; do not damage production-like databases.

## Deferred scope and next recommendation

`CrossTenantAccess` remains deferred. Tenant-local resource misses still do not trigger foreign-tenant searches or
events. API stale-token membership auditing, invalid tenant claims, operational outage auditing, and other tenant
denials also remain separate work.

The recommended next phase is **Step 23 — Security Audit Review & Evidence Access Design**, design-only first, for a
narrowly permissioned, content-free review surface covering MissingPermission, CrossPatientOwnership,
UnresolvedClinicalActor, and InvalidTenantMembership.
