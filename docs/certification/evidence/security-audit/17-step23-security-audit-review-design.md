# Step 23 — Security audit review design

## Status, scope, and decision

This is analysis, design, and test planning only. It changes no runtime behavior, database object, permission,
controller, user interface, writer, export, retention rule, or alert. The reviewed baseline is platform migration
017 and tenant migration 0046.

The recommended first implementation is a **platform-only, read-only review surface** protected by a new dedicated
platform authorization policy, backed by narrowly permissioned stored procedures over
`dbo.PlatformSecurityAuditEvent`. It must cover all four currently governed reasons: `MissingPermission`,
`CrossPatientOwnership`, `UnresolvedClinicalActor`, and `InvalidTenantMembership`.

A platform migration **018 is required** for the first implementation because no governed read procedure exists.
The table columns introduced by migrations 014–017 are sufficient; migration 018 should not add event fields or
alter existing writers. It should add only the two read procedures described below and, after an execution-plan
check with representative volume, the deterministic paging index if SQL Server cannot use the current time index
efficiently. Migration 016 is already part of the current migration chain and must not be rerun or edited in
isolation; an environment below 017 should apply each pending platform migration once, in order, through 017 before
the future 018 implementation.

## Evidence reviewed

The review considered security-audit documents 01–05 and the Step 19–22 design/foundation/runtime evidence. It also
inspected migrations 014–017, `PlatformSecurityAuditEvent`, its four narrow write procedures, the write-only
`IPlatformSecurityAuditRepository`/`SqlPlatformSecurityAuditRepository`, platform administration contracts and SQL
services, the tenant permission catalog/effective-permission service, API and Web authorization conventions, and
the current Web administration navigation.

Current findings:

- The security stream is central and distinct from tenant clinical `AuditLog` and administrative
  `PlatformAuditEvent`.
- The four writers are narrow, insert-only stored-procedure calls. There is no list/detail query, reviewer service,
  controller, page, export, review-access audit writer, or review policy.
- Existing indexes support time, trusted-tenant/time, actor/time, correlation, and cross-patient resource
  investigation. None includes `SecurityAuditEventUid` as the time-order tie-breaker.
- `PermissionKeys` and effective permissions are tenant-scoped and require an active trusted membership. They cannot
  authorize central records that have no trusted tenant, especially `InvalidTenantMembership`.
- `PlatformRoles.Administrator` and `PlatformRoles.Operator` exist as contract constants but are not wired to a
  platform Web/API review surface. The Web `AppRoles.Administrator` is a separate legacy role and must not be treated
  as equivalent.
- `PlatformAuditEvent` records administrative mutations through positional inserts. It should not be structurally
  changed as part of the first review slice.

## Reviewer authorization model

Introduce a named policy such as `PlatformSecurityAuditReview` whose sole grant is a dedicated platform entitlement,
for example an identity claim/permission value `SecurityAudit.View`. The authorization handler must validate the
entitlement from the trusted issuer and must not infer it from tenant role, tenant access profile, Web
`Administrator`, or possession of `PlatformAdministrator`/`PlatformOperator` alone.

Provisioning that entitlement is an identity/operations responsibility with separation-of-duties approval. The
first slice should not add it to the tenant `PermissionCatalog`, built-in access profiles, or user-access screens.
An operator or administrator receives review access only when explicitly assigned the reviewer entitlement. API and
Web endpoints enforce the same policy; hiding a navigation item is not authorization.

Use a separate future `SecurityAudit.Export` entitlement if export is approved. `SecurityAudit.View` must not imply
export, writer access, retention changes, alert administration, tenant administration, or user administration.

## Tenant scope and authorization boundary

The first slice is platform-reviewer only and may search across tenants because the stream contains pre-trust and
cross-tenant investigation evidence. The service must never derive authorization scope from query input.

`TargetTenantUid` is the only trusted tenant dimension. `RequestedTenantUid` is explicitly untrusted and is never an
authorization boundary, database-routing value, or substitute for `TargetTenantUid`. A future clinic reviewer
surface would require a separate tenant-scoped `SecurityAudit.View` business permission and server-enforced
`TargetTenantUid = current trusted tenant`. That future surface must exclude rows with null `TargetTenantUid`, which
includes `InvalidTenantMembership`, even if `RequestedTenantUid` equals the clinic tenant. No such clinic surface is
included in the first implementation.

The query operates only in the platform database. It must not connect to tenant databases, resolve patient names,
look up resources, verify foreign ownership, or enrich subjects from identity services.

## Field visibility and minimization

The list response should expose only:

| Field | List treatment |
|---|---|
| `SecurityAuditEventUid` | Opaque detail link identifier; not presented as a business value. |
| `OccurredAtUtc` | Visible in UTC with an explicit UTC label; optional browser-local rendering may be secondary. |
| `DenialReason` | Visible controlled label. |
| `Capability` | Visible controlled label. |
| `RequiredPermission` | Visible when present; display “Not applicable” for tenant-membership denials. |
| `SourceApplication` | Visible controlled label. |
| `TargetTenantUid` | Visible to the platform reviewer; show “No trusted tenant” when null. |
| `RequestCorrelationId` | Visible but visually de-emphasized; exact-copy action is permitted. |
| `ActorSubject` | Masked summary only, for example first/last four characters when length permits. |

The detail view may additionally expose the exact `ActorSubject`, `ClinicalUserId`, `RequestedTenantUid`,
`RequestedPatientUid`, `AuthoritativePatientUid`, `ResourceType`, and `ResourceUid`. These values remain identifiers,
not clinical content. Detail must clearly label trusted versus requested/untrusted tenant and requested versus
authoritative patient. It must not expose raw SQL, tokens, claims, request routes/query strings, filenames, document
titles, report rows, stack traces, arbitrary JSON, or inferred names. Exact subject and clinical identifiers should
not be present in list-page HTML, telemetry, page title, analytics, or URL query parameters.

## Safe search contract

Require a bounded UTC interval for every search. Initial defaults and limits:

- default `fromUtc = now - 24 hours`, `toUtc = now`;
- `fromUtc < toUtc`, with an inclusive lower bound and exclusive upper bound;
- maximum interval 31 days per request;
- page size 25 by default and 100 maximum;
- optional exact controlled filters: denial reason, capability, source application, and trusted tenant UID;
- optional exact investigation filters: request correlation ID and actor subject;
- actor-subject search is an exact-match advanced filter, never contains/prefix/free text, and must not echo the
  supplied value into logs or URLs;
- correlation is exact after trim and existing maximum-length validation;
- no patient/resource UID filters in the first list slice; investigators can use a known event's restricted detail.

Reject unknown controlled values, empty GUIDs, invalid cursor pairs, overlong values, future-only windows, and ranges
over 31 days with a generic 400 response. Do not silently broaden a rejected filter. Use POST for search so sensitive
advanced filters do not enter browser history, referrers, proxy URLs, or access-log query strings. Detail uses the
opaque event UID in its route and returns 404 for missing records without alternate lookup.

## Paging and ordering

Use keyset paging, ordered by `(OccurredAtUtc DESC, SecurityAuditEventUid DESC)`. The opaque continuation token is a
server-protected encoding of both values plus a normalized-filter fingerprint and contract version. It must not be
accepted with changed filters and must not reveal or accept arbitrary SQL/order expressions.

The next-page predicate is:

```sql
OccurredAtUtc < @CursorOccurredAtUtc
OR (OccurredAtUtc = @CursorOccurredAtUtc AND SecurityAuditEventUid < @CursorSecurityAuditEventUid)
```

Select `TOP (@PageSize + 1)` to determine whether another page exists. Offset paging and client-supplied sort fields
are excluded because concurrent inserts would produce unstable pages and large offsets invite expensive scans.

## Proposed application, API, repository, and SQL contracts

Keep controllers thin. Add application-layer immutable query/DTO types and a review service that validates the
window, allowlists, page size, cursor/filter binding, field projection, and authorization-independent business
rules. Use a distinct read interface, for example `IPlatformSecurityAuditReviewRepository`; do not expand the
insert-oriented writer interface into a mixed-privilege repository.

Recommended endpoints:

- `POST /api/platform/security-audit/search` → minimized page plus opaque continuation token;
- `GET /api/platform/security-audit/events/{securityAuditEventUid}` → restricted detail.

Both require the platform review policy. The API must return no database entities and must set normal authenticated
no-store response caching headers. The Web server calls the API using the current authenticated delegated-token
pattern; it does not query SQL directly.

Migration 018 should create:

- `dbo.PlatformSecurityAudit_Search`, with typed parameters, fixed projection/order, `TOP` cap, allowlisted exact
  predicates, and no dynamic SQL;
- `dbo.PlatformSecurityAudit_GetByUid`, with a typed non-empty UID and the explicit restricted detail projection.

Grant the dedicated review database principal `EXECUTE` only on these procedures. Do not grant direct table
`SELECT`, any writer procedure, `INSERT`, `UPDATE`, or `DELETE`. Keep the existing application writer principal from
executing review procedures unless deployment architecture proves it is the same constrained API identity; prefer
separate credentials/roles where operationally feasible.

The existing `(OccurredAtUtc DESC)` index can seed a bounded time scan, but it lacks the UID tie-breaker and covering
columns. Migration 018 should add `(OccurredAtUtc DESC, SecurityAuditEventUid DESC)` with a minimal INCLUDE set only
when representative execution plans show it is needed. Do not create an index per optional filter. The existing
tenant/time, actor/time, correlation, and ownership-resource indexes remain useful and unchanged.

## Auditing reviewer access

Every successful search and detail retrieval must itself create one administrative access event after authorization
and before returning data. Record one event per request—not one event per returned row—with actor subject, action
(`SecurityAuditSearchViewed` or `SecurityAuditEventViewed`), UTC time, request correlation, outcome, target event UID
for detail, and a minimized filter summary for search (time bounds, controlled filter names/values, result count;
never raw actor subject). Denied attempts remain ordinary authorization/security-denial handling and must not recurse
through the reviewed stream.

The current `PlatformAuditEvent` is the appropriate semantic stream for reviewer activity, but its current schema
and positional administrative writers make casual extension unsafe. Before implementation, choose one of two
reviewed approaches:

1. Preferred if the existing columns can represent the event without ambiguity: add a narrow
   `PlatformAudit_RecordSecurityAuditReview` procedure that performs an explicit-column insert and stores only a
   strictly generated, bounded JSON summary in existing `DetailsJson`.
2. If target-event identity and query semantics cannot be governed in those fields, introduce a separate append-only
   review-access table rather than altering existing positional writers.

This choice belongs in migration 018 and requires contract tests. Review-audit persistence failure must fail closed
for the disclosure request (generic 503, no review data returned) and be operationally logged without sensitive
filters. This is stricter than denial-event write failure because the protected disclosure has not yet occurred.

## User-interface design

Place the page in a distinct **Platform Administration → Security → Security audit** area, not in patient charts,
clinic administration, user management, or the current legacy `AdministrationController`. Show the navigation item
only to the platform review policy, while enforcing the same policy at Web and API boundaries.

The initial page contains a 24-hour UTC filter panel, reason/capability/source/trusted-tenant selectors, an advanced
exact correlation/actor search, a compact results table, and Previous/Next keyset controls. The detail page uses
clear sections for event, actor, trusted context, requested/untrusted context, and resource identifiers. Add warning
text that identifiers are sensitive and that an event records a denied attempt, not a confirmed disclosure or
clinical finding. Do not add bulk selection, CSV/download, print layout, dashboards, counts across unbounded time,
alerts, acknowledgment workflow, notes, assignment, editing, or deletion.

Use Bootstrap 5 patterns already present in Web. Filters and paging must remain usable with keyboard and screen
reader, validation summaries must associate with controls, table headers need scope, masked values need accessible
labels, and empty/error/loading states must not leak filter values.

## First implementation slice

Implement only:

1. dedicated platform reviewer entitlement/policy and common API/Web enforcement;
2. migration 018 with two read procedures, the reviewed access-audit procedure/table decision, least-privilege grant
   guidance, and only the execution-plan-supported paging index;
3. separate application review service/DTO and Infrastructure read repository;
4. POST search, GET detail, no-store responses, opaque bound cursor, and one access-audit event per successful request;
5. platform-only Bootstrap list/detail UI with the minimized fields and safe filters above;
6. automated contract, authorization, scoping, minimization, paging, access-audit, and regression tests.

Explicitly defer tenant-admin access, export, saved searches, dashboards, alerting/SIEM, retention/destruction, legal
hold, immutable replication, enrichment, anomaly scoring, reviewer workflow, and changes to the four denial writers.

## Automated verification plan

1. No entitlement, tenant permission only, legacy Administrator only, PlatformAdministrator only, and
   PlatformOperator only each receive 403 and no review data.
2. Explicit `SecurityAudit.View` permits search/detail; `SecurityAudit.View` does not permit any mutation or export.
3. Web navigation and page, API search, and API detail all enforce the same policy.
4. Search always requires a valid window and rejects windows over 31 days, invalid controlled values, empty GUIDs,
   overlong exact values, invalid/tampered cursors, cursor/filter mismatch, page size over 100, and client sort input.
5. Default search covers only the prior 24 hours; upper bound is exclusive.
6. Results order deterministically by time then event UID descending, including equal timestamps.
7. Keyset pages contain no duplicate/omitted baseline rows and remain stable when newer rows arrive.
8. Each of the four denial reasons is returned with the correct nullable shape.
9. List projection excludes exact actor subject and all patient/resource identifiers; detail includes only approved
   identifiers and no content/free-text fields.
10. Requested tenant never grants or expands access and is labeled untrusted.
11. No tenant database, identity enrichment, patient lookup, or resource lookup occurs during search/detail.
12. Repository commands are stored-procedure type, use only the two read procedures, use typed bounded parameters,
    and contain no direct SQL/dynamic SQL.
13. Review database principal has only required execute rights; writer does not gain select/update/delete rights.
14. Every successful search/detail records exactly one reviewer-access event with correlation and minimized metadata.
15. Review-access write failure returns generic 503 and returns no audit data.
16. Unauthorized, invalid-filter, missing-detail, and cancelled requests do not create misleading successful review
    events; operational failure handling is verified separately.
17. Search/detail responses are no-store and sensitive advanced filters do not appear in URLs or application logs.
18. Existing four writer procedures and migrations 014–017 remain byte-for-byte unchanged and their regression
    tests pass.
19. Platform migration numbering is uniquely 018, tenant maximum remains 0046, and supported 017→018 upgrade is
    exercised against disposable SQL Server.
20. Migration tests verify explicit-column access-audit insert behavior and no redefinition of existing positional
    administrative procedures.
21. Accessibility tests cover labels, validation, focus order, table headers, paging, masking, and empty/error states.
22. Full API, Auth, and Release build suites remain green.

## Manual validation plan

Use synthetic identities and data only. Apply pending platform migrations once in order in a disposable environment;
do not manually rerun migration 016. Confirm the platform migration ledger reports 017 before testing a future 018.

1. Sign in without the reviewer entitlement and confirm the navigation is absent and direct Web/API requests return
   generic 403 with no event data.
2. Grant only the reviewer entitlement to the test reviewer. Confirm the page appears without granting tenant,
   writer, export, or administration privileges.
3. Seed one governed test event for each current reason using the existing narrow procedures. Search the default
   24-hour window and verify reason-specific null fields and masking.
4. Exercise every filter, invalid range, max range, page size, equal-timestamp ordering, next page, tampered cursor,
   and changed-filter cursor. Confirm no duplicates and no URL/log exposure of exact actor filters.
5. Open each detail and verify trusted/requested labels, exact approved identifiers, no enrichment, no clinical
   content, and no tenant-database activity.
6. Query the administrative review-access evidence with a separately authorized test tool. Confirm one event per
   successful search/detail, accurate actor/correlation/action/result count, and no raw actor-search value.
7. Deny the access-audit writer temporarily and confirm search/detail returns generic 503 with no records. Restore it
   and verify recovery.
8. Confirm database grants: reviewer can execute only read procedures, writer can execute only approved writer
   procedures, and neither has direct table update/delete rights.
9. Capture timestamped screenshots, request/response status and headers, correlation IDs, narrowly redacted source
   event rows, review-access rows, execution plans, grant results, migration ledger, and automated-test output.

## Migration answer and readiness gate

For the current Step 23 design work, **do not run migration 016** and do not create/run migration 018: this branch is
documentation only. Migration 016 is an already-applied predecessor that introduced the unresolved-actor contract;
017 depends on it and is the expected current platform level. Validate an environment with the migration ledger and
the presence/shape of `dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor`, then apply only genuinely pending
migrations through the normal runner. Never execute an old migration ad hoc against an unknown schema.

Step 23 implementation is ready to begin only after security/privacy owners approve the dedicated entitlement,
platform-only first scope, list/detail field matrix, 31-day/100-row limits, exact actor search, review-access audit
representation, fail-closed behavior, and database-principal separation. OntarioMD interpretation, retention, export,
clinic visibility, and incident-response obligations remain explicit governance decisions rather than claims made by
this design.
