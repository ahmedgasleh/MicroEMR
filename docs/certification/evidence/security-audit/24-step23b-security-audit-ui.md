# Step 23B — Security Audit review UI

## Scope and location

Step 23B adds only a read-only server-rendered Web interface for the secured Step 23A API. Entitled reviewers see
`Platform Administration` → `Security Audit` in the application sidebar. The MVC route is
`/PlatformSecurityAudit`; event detail is `/PlatformSecurityAudit/Details/{uid}`. No database object, API query
contract, export, mutation, annotation, retention, alerting, tenant-admin path, or clinical enrichment was added.

## Authorization and session behavior

The controller has the exact `PlatformEntitlement:SecurityAudit.View` requirement. The sidebar checks the same
entitlement through `IWebPlatformEntitlementAccessor`; it does not consult global roles, tenant roles, or tenant
permissions. Direct navigation without authorization follows the existing Web challenge/access-denied behavior and
does not invoke the review API or render event data. The Step 23A bearer-token check remains authoritative.

`SecurityAuditApiClient` is a typed `HttpClient` registered with the existing `WebApiBearerTokenHandler`. The page
therefore uses the Step 23P-R protected-ticket refresh and rotation behavior and contains no browser token handling,
custom refresh path, bearer value, or client-side audit request. A later API 401/403 returns the established Web
forbid behavior instead of retaining review content in a client cache.

## Search and filters

Initial GET performs one API search using the Step 23A default UTC window and fixed page size 25. The returned
normalized `[fromUtc, toUtc)` values populate UTC-labelled `datetime-local` fields. The UI validates that both dates
are present together, From precedes To, and the range is at most 31 days; the API remains authoritative.

The compact Bootstrap filter panel provides governed dropdowns for denial reason, capability, and source
application, plus exact trusted `TargetTenantUid` and correlation inputs. It uses the UID because adding tenant-name
infrastructure was outside scope. The exact ActorSubject filter is deliberately placed under an advanced restricted
section, disables autocomplete, and supports no partial/wildcard behavior. Apply is explicit and resets paging;
Reset redirects to a clean initial/default search. Filter submission is POST with antiforgery protection, keeping
exact investigation filters out of URLs and ordinary request-target logs.

After actor-filter submission, the exact value is cleared from the rendered model. Paging uses an ASP.NET Data
Protection-protected Web state token containing the API continuation and complete normalized filters. Consequently,
the exact actor value is neither a visible form value nor a plaintext hidden field in list HTML. The UI only states
that a restricted actor filter was applied.

## List, paging, and states

The responsive list displays only Step 23A list fields: UTC time, denial reason, capability, nullable permission,
source, trusted tenant UID, masked actor summary, correlation ID, and a Details action. It displays the API-provided
mask exactly. Requested tenant, exact actor, clinical user, patient IDs, and resource IDs are absent from list HTML.
Missing values render as an em dash.

Paging is keyset-only. `Older events` submits the protected paging state and API continuation; `Back to newest`
starts a new first-page request. No offset, page number, total count, or fabricated page count exists. A changed
filter submission clears the old continuation.

The page has a compact loading indicator that is announced through `aria-live`, disables the submitted button,
and uses no full-page splash. Zero rows show a neutral empty state. Local/API validation shows a concise warning;
temporary service or review-audit failure shows a generic unavailable error. No SQL message, procedure name, stack
trace, token, or internal authorization decision is rendered.

## Detail and audit integration

The list does not prefetch detail. Only the explicit Details link calls the Step 23A detail endpoint, causing exactly
one server-side `SecurityAuditViewed` audit event when disclosure succeeds. The detail page groups approved fields
into Event, Identity, Tenant context, and Resource context. It labels `TargetTenantUid` as Trusted Tenant and
`RequestedTenantUid` as Requested Tenant (untrusted), handles reason-specific nulls, and displays identifiers only—
never patient names, clinical content, document titles, or tenant-database enrichment.

Each successful initial search, filter application, and older-page request is a single Step 23A search disclosure
and therefore one server-side `SecurityAuditSearched` event. The UI issues no separate audit call and never fetches
detail per row, avoiding duplicate/event-per-row audit noise.

## Accessibility and responsive behavior

Controls have labels, table headers use scope, the Details action has an accessible name, advanced ActorSubject is
keyboard-operable through native `details`, status/error regions use appropriate roles, and Bootstrap responsive
columns/table scrolling preserve use on narrow screens. The detail page uses labelled semantic sections and neutral
missing-value output.

## Automated and runtime verification

Focused tests cover API defaults/page size, every supported exact filter, filter-apply continuation reset, protected
older-page state, local 31-day validation without API disclosure, explicit-only detail retrieval, exact entitlement
metadata/sidebar behavior, list/detail minimization, requested/trusted labels, loading/empty/error states, absence of
export/mutation/page-number behavior, and API endpoint/status classification. Existing platform entitlement tests
cover no entitlement, role-only identities, tenant permissions, and tenant/clinical independence. TypeScript and
Razor compile through the Release build.

Local HTTPS checks confirmed unauthenticated direct Web navigation returns the normal OIDC challenge and no page
data. Interactive browser automation could not be completed because the configured in-app browser execution context
was unavailable; the manual checklist below remains required for visual and entitled-user runtime evidence.

### Manual runtime checklist

1. Apply platform migration 019 once through the approved deployment process if the target environment is below 019.
2. Assign `SecurityAudit.View` through the approved platform-entitlement bootstrap and reauthenticate.
3. Confirm Platform Administration → Security Audit appears only for that reviewer.
4. Confirm initial results use the last 24 hours, page size 25, and newest-first ordering.
5. Exercise each governed reason/capability/source, trusted tenant UID, exact correlation, and restricted exact actor.
6. Confirm Reset restores the default search and changed filters never reuse an old continuation.
7. Exercise Older events and Back to newest; confirm no page number/count is displayed.
8. Confirm empty, invalid >31-day, and temporary-unavailable states reveal no internal detail.
9. Open one event explicitly and verify approved metadata, trusted/requested labels, valid nulls, and no enrichment.
10. Verify one `SecurityAuditSearched` per successful page/filter disclosure and one `SecurityAuditViewed` per opened detail.
11. Confirm no background detail/event-per-row audits occur.
12. Continue using the page across a five-minute access-token boundary and confirm Step 23P-R renews the session.
13. Remove/revoke the entitlement, allow the existing short-token window to close, and confirm navigation/direct access
    deny further disclosure.
14. Test narrow viewport, keyboard navigation, labels, loading announcement, responsive table, and detail sections.

## Migration safety and deferred features

No migration was created or modified. Platform remains through immutable migration 019 and tenant clinical remains
through immutable migration 0046. There is no export/download/CSV/print, SecurityAudit.Export, edit/delete/clear,
mark-reviewed/note workflow, patient or clinical-user lookup, tenant clinical database call, or CrossTenantAccess.
