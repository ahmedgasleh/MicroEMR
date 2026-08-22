# Step 23A — Security Audit review foundation

## Scope and authorization

This step adds only the central database, application, Infrastructure, and API foundation for read-only Security
Audit review. It adds no Web page, navigation, export, entitlement management, tenant-admin grant, clinical
enrichment, retention, alerting, mutation, or deletion behavior.

Both API operations require the existing exact `PlatformEntitlement:SecurityAudit.View` policy through
`RequirePlatformEntitlementAttribute`. Platform, Web, Identity, and tenant roles do not satisfy the requirement.
Tenant permissions are not consulted. The platform routes bypass tenant resolution and clinical-actor resolution,
so an explicitly entitled reviewer needs neither `TenantContext` nor `ClinicalUserId`. The API bearer-token policy
remains the authoritative boundary.

## Migration 019

`019_platform_security_audit_review.sql` is the only new migration. Platform migrations 001–018 and all tenant
migrations remain unchanged; tenant clinical remains at 0046.

Migration 019 adds:

- `IX_PlatformSecurityAuditEvent_ReviewKeyset` on
  `(OccurredAtUtc DESC, SecurityAuditEventUid DESC)`, with only the minimized list projection included;
- `dbo.PlatformSecurityAudit_Search`;
- `dbo.PlatformSecurityAudit_GetByUid`;
- `dbo.PlatformAudit_RecordSecurityAuditReview`.

It does not alter `PlatformSecurityAuditEvent` or `PlatformAuditEvent`, redefine an existing procedure, or grant
direct table DML. Deployment should grant the review API principal execute only on the three governed procedures.
The new review-audit procedure uses an explicit `PlatformAuditEvent` column list, leaving every legacy positional
insert untouched.

## Search contract

The API route is `POST /api/platform/security-audit/search`; POST keeps exact actor and correlation filters out of
URLs, browser history, and ordinary request-target logs. Responses are `no-store`.

The application defaults to `[now - 24 hours, now)` and page size 25. It rejects invalid/reversed/future-only
windows, windows over 31 days, empty tenant GUIDs, invalid controlled values, empty/overlong exact strings, and page
sizes outside 1–100. SQL independently enforces the time, page, cursor, GUID, reason, capability, source, actor, and
correlation bounds. Supported exact optional filters are `DenialReason`, `Capability`, `SourceApplication`, trusted
`TargetTenantUid`, `RequestCorrelationId`, and restricted `ActorSubject`. There is no free-text or wildcard query.

Results order by `OccurredAtUtc DESC, SecurityAuditEventUid DESC`. SQL selects `TOP (@PageSize + 1)` and applies the
next-page predicate to both ordering keys. The client receives a Data Protection-protected continuation token bound
to the normalized filter/window/page-size fingerprint. Malformed, tampered, out-of-window, or filter-mismatched
tokens are rejected. No offset paging or client sort expression exists.

The list contains only `SecurityAuditEventUid`, `OccurredAtUtc`, `DenialReason`, `Capability`, nullable
`RequiredPermission`, `SourceApplication`, trusted nullable `TargetTenantUid`, nullable `RequestCorrelationId`, and
`MaskedActorSubject`. Masking occurs in SQL (first/last four characters when possible). Exact actor, requested
tenant, clinical user, patient identifiers, resource identifiers, names, titles, and clinical content never enter
the list repository result.

`TargetTenantUid` is an optional trusted filter only; it is never inferred from request context. A null target is
not filtered out, so `InvalidTenantMembership` remains reviewable. `RequestedTenantUid` is returned only by detail
as untrusted historical context and never authorizes or routes a query.

## Detail contract

`GET /api/platform/security-audit/events/{securityAuditEventUid}` returns one approved platform event or the normal
404. Detail fields are the actual existing event identifiers: event UID/type/outcome/reason, exact actor subject,
nullable clinical user, trusted target tenant, untrusted requested tenant, capability/permission/source/correlation,
nullable requested/authoritative patient IDs, resource type/UID, and occurrence time. There is no Identity lookup,
tenant clinical database access, patient/physician name, document title, payload, token, route, or other enrichment.
A missing event creates no successful view audit.

## Review-access audit and fail-closed disclosure

A successful search, including zero results, writes one `PlatformAuditEvent` action `SecurityAuditSearched`; the
event records reviewer subject, server time, a generated correlation GUID, result count, time bounds, and only the
names of applied filters. It never stores an actor-filter value, request body, token, or returned row IDs. One search
returning 25 rows still writes one event.

A found detail writes one `SecurityAuditViewed` event with only the reviewed event UID in bounded `DetailsJson`.
Not-found detail writes no successful review event. Neither action writes to `PlatformSecurityAuditEvent`.

The service retrieves the candidate result, persists the single administrative review event, and only then returns
the DTO to the controller. Any review-audit failure throws a disclosure-specific exception; the controller logs a
filter-free operational error and returns generic 503 with no review data. Unauthorized requests are rejected by
server authorization before the action and therefore write no successful review event.

## Application, Infrastructure, and API

`IPlatformSecurityAuditReviewRepository` is separate from the four insert-only denial writers. Its SQL
implementation uses only the search, detail, and review-audit stored procedures with typed parameters.
`IPlatformSecurityAuditReviewService` owns normalization, allowlists, bounds, protected cursor validation,
list/detail separation, audit orchestration, and fail-closed behavior. The controller remains limited to trusted
subject/correlation acquisition and HTTP result mapping.

## Verification

Focused automated coverage verifies defaults/limits, all exact filters, cursor binding/malformed cursors, minimized
masked lists, 25-row/zero-row single audits, found/not-found detail, fail-closed search/detail, stored-procedure-only
Infrastructure, explicit-column migration SQL, sequence safety, no-store routes, and exact entitlement metadata.
Existing exact-entitlement tests cover explicit grant, absence, PlatformAdministrator, PlatformOperator,
Administrator/SystemAdmin, tenant role/permission separation, and operation without tenant/clinical claims.

Disposable LocalDB validation applied the repository chain in a continuous SQLCMD session with required quoted
identifier settings. The unchanged migration 013 again failed at `PlatformMembership_Deactivate` near
`MicroEMR:AccessAdmin:`—the inherited fresh-provisioning defect documented by Step 23P-A. Without editing that
predecessor, the actual 014–018 security/audit schema and migration 019 were exercised successfully. Synthetic
events written through all four governed denial procedures were returned newest-first with the correct nullable
shapes and masked actors; detail returned the approved fields; the new index existed; and one search plus one detail
produced exactly one corresponding `PlatformAuditEvent` each. Thus the 018-shaped schema to 019 change is verified,
while an unqualified fresh 001–019 provisioning claim remains blocked by pre-existing migration 013 governance.

The Step 23B read-only UI can consume these API contracts without another database migration. Export remains a
separate deferred entitlement and implementation.
