# Step 23P-B — Platform entitlement token and authorization wiring

## Security decision and session continuity

OpenIddict access tokens now have an explicit five-minute lifetime and refresh tokens an explicit
14-day lifetime. Existing signed, self-contained access tokens may retain a revoked platform
entitlement until they expire. The approved maximum residual authorization window is therefore
approximately five minutes; this implementation does not claim immediate access-token revocation.

Step 23P-R is the session-continuity prerequisite. MicroEMR Web refreshes within the configured
one-minute near-expiry window, serializes concurrent refreshes, persists rotated tokens and the
new expiry in the protected ticket, and renews the cookie. An unchanged authorization version
therefore allows a normal session to continue without interactive login across five-minute access
token boundaries.

The five-minute integration review identified that the original Web cookie inherited access-token
expiry. Web now keeps the protected authentication ticket for an explicit 14-day renewal window,
does not use the access-token lifetime as cookie lifetime, and invokes the same centralized refresh
service during cookie validation. This preserves the server-side refresh token after an access token
expires and avoids forcing an otherwise renewable user to authenticate every five minutes.

## Claims and initial issuance

The canonical multi-valued claim is `platform_entitlement`; the initial governed value is exactly
`SecurityAudit.View`. `platform_authorization_version` contains the authoritative per-Identity-user
version. Both claim types have access-token destination only and are absent from the identity token.
OpenIddict retains the protected authorization-version claim in refresh-token state for trusted
server-side validation; it is never accepted from request parameters.

After the authenticated `ApplicationUser` is confirmed active and not locked, Auth preserves the
existing roles and tenant-selection/enrichment behavior, loads active explicit entitlements and
the authorization version through the Step 23P-A platform service, filters to the compiled governed
catalog, and issues one exact claim per effective entitlement. No selected tenant, tenant permission,
or clinical user participates in platform entitlement evaluation. Loading failures are logged
operationally and fail token issuance closed without database details.

## Refresh validation and reload

The OpenIddict token endpoint uses ASP.NET Core passthrough for refresh grants. It authenticates the
protected refresh-token principal, resolves `sub` as `ApplicationUser.Id`, and rejects missing,
inactive, or locked accounts. It reads `platform_authorization_version` only from that trusted
principal. The platform state is loaded and version-checked twice around entitlement reload to
detect assignment/revocation changes during processing.

Token-endpoint passthrough also preserves any successfully authenticated non-refresh principal from
OpenIddict and returns it for normal authorization-code/PKCE token issuance. The server enables only
authorization-code and refresh grants, so no additional grant is admitted by this fallback. The
refresh-only platform validation branch does not intercept login.

If both comparisons match, old entitlement/version claims are removed, current claims are rebuilt
from the database, destinations are reapplied, and OpenIddict issues the refreshed token set. Claims
are never copied blindly from the old access state. A version mismatch returns `invalid_grant`; no
new access token is minted. Step 23P-R then clears the local Web cookie, stops the API request, and
requires interactive authentication without retry loops or protocol details. Temporary platform DB
failures return a generic server error, preserve the Web session under Step 23P-R conventions, and
never authorize from stale state.

Revocation and assignment have the same governed behavior: an existing access token changes only at
its five-minute expiry, the version increment invalidates the prior refresh state, and a fresh login
loads current assignments. Entitlements never override Identity account state.

## Authorization infrastructure

API and Web register the canonical policy `PlatformEntitlement:SecurityAudit.View`. Their reusable
`PlatformEntitlementRequirement`, exact authorization handler, and attribute succeed only for a
known governed key with an ordinal-exact `platform_entitlement` claim value. Unknown keys fail closed.
There is no wildcard, expression, scope, global-role, tenant-role, tenant-permission, tenant-context,
or clinical-user fallback. Platform denial is not routed through tenant `MissingPermission` auditing.
No Security Audit endpoint or UI consumes the policy in this step.

Because the entitlement is intentionally excluded from the ID token, Web reads it from the current
access token held inside the integrity-protected authentication ticket. It does not return or log the
token. This provides Web navigation/controller enforcement while API bearer-token validation remains
the authoritative security boundary.

## Verification and migration safety

Focused tests cover explicit lifetimes, access-token-only destinations, current claim filtering,
trusted refresh-version origin, unchanged/stale/mid-reload version behavior, exact API/Web policy
authorization, role and tenant-permission separation, and tenant/clinical independence. The existing
Step 23P-R suite covers near-expiry refresh, rotation, protected-ticket/cookie persistence,
concurrency, invalid-grant reauthentication, and temporary failure handling.

No database or migration file changed. Platform remains through `018_platform_entitlement_foundation.sql`
and tenant clinical remains through `0046-aggregate-report-audit-events.sql`. The governed offline
bootstrap is sufficient for current certification/testing, so Step 23P-C remains deferred.

After automated regression and controlled runtime session-continuity verification, Security Audit
Review Step 23A may resume. Its next platform migration is expected to be
`019_platform_security_audit_review.sql`, assuming no intervening platform migration exists.
