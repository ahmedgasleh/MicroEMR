# Step 23B Manual Runtime Validation Checklist

Date prepared: 2026-08-22  
Branch: `feature/ontariomd_certification_step23b_runtime_validation`  
Purpose: operator checklist for non-production manual validation. Record only directly observed results as **PASS**, **FAIL**, or **NOT VERIFIED**.

## Safety and environment

- Use a dedicated non-production reviewer and a separate unauthorized account.
- Do not use real patients or production identities.
- Do not copy, display, or log passwords, access tokens, refresh tokens, bearer values, or client secrets.
- Do not create migration 021 or modify migrations 020, 018, or 013.
- Do not change authentication, UI, entitlement semantics, or DatabaseTool.
- Stop immediately if an unauthorized caller receives Security Audit data, stale refresh recreates revoked access, the authorization version does not increment, or a raw token is exposed.

Expected local endpoints:

- Auth: `https://localhost:7179`
- Web: `https://localhost:7002`
- API: `https://localhost:7003`
- Security Audit UI: `https://localhost:7002/PlatformSecurityAudit`
- Search API: `POST https://localhost:7003/api/platform/security-audit/search`

Keep SSMS connected to `MicroEMR_Auth` and `MicroEMR_Platform`. Keep browser Developer Tools open on the Network tab. Replace `<REVIEWER_EMAIL>`, `<REVIEWER_USER_ID>`, and `<UNAUTHORIZED_USER_ID>` below.

## 1. Identify the test reviewer

Run against `MicroEMR_Auth`:

```sql
USE MicroEMR_Auth;

SELECT Id, UserName, Email, EmailConfirmed, IsActive,
       LockoutEnabled, AccessFailedCount, LockoutEnd
FROM dbo.AspNetUsers
WHERE Email = N'<REVIEWER_EMAIL>';
```

Expected: exactly one active, unlocked test-only identity. Copy `Id` as `<REVIEWER_USER_ID>`. Repeat for the unauthorized account. Do not treat `admin@microemr.local` as the reviewer unless it is explicitly approved as a test-only identity.

## 2. Confirm migration 020 prerequisites

Run against `MicroEMR_Platform`:

```sql
USE MicroEMR_Platform;

SELECT
    OBJECT_ID(N'dbo.PlatformEntitlement_AssignToUser', N'P') AS AssignProcedureId,
    OBJECT_ID(N'dbo.PlatformEntitlement_RevokeFromUser', N'P') AS RevokeProcedureId;

SELECT OBJECT_DEFINITION(
    OBJECT_ID(N'dbo.PlatformEntitlement_AssignToUser')) AS AssignDefinition;

SELECT OBJECT_DEFINITION(
    OBJECT_ID(N'dbo.PlatformEntitlement_RevokeFromUser')) AS RevokeDefinition;
```

Expected: both IDs are non-null. Both definitions contain `sp_getapplock`, `PlatformAuthorizationState`, and `PlatformAuditEvent`. Assignment contains `PlatformEntitlementAssigned`; revocation contains `PlatformEntitlementRevoked`. Also confirm migration 020 in the environment's governed deployment journal. Repository file presence alone is not runtime proof.

Stop if either procedure is missing or unrepaired.

## 3. Record starting state

```sql
DECLARE @UserId nvarchar(450) = N'<REVIEWER_USER_ID>';

SELECT UserId, AuthorizationVersion, UpdatedAtUtc
FROM dbo.PlatformAuthorizationState
WHERE UserId = @UserId;

SELECT e.EntitlementKey, upe.AssignedAtUtc, upe.AssignedBy,
       upe.RevokedAtUtc, upe.RevokedBy
FROM dbo.UserPlatformEntitlement AS upe
JOIN dbo.PlatformEntitlement AS e
  ON e.PlatformEntitlementUid = upe.PlatformEntitlementUid
WHERE upe.UserId = @UserId
  AND e.EntitlementKey = N'SecurityAudit.View'
ORDER BY upe.AssignedAtUtc DESC;
```

Record the initial authorization version. Begin with no active assignment. Use only the governed revoke command below if cleanup is required; never delete the records.

## 4. Assign `SecurityAudit.View`

First prove DatabaseTool targets the intended non-production platform database. The known configuration-precedence issue remains unresolved. Trusted configuration must enable platform administration and provide an audited actor ID. If the target cannot be proven, use the already-approved governed administration mechanism instead.

From the repository root:

```powershell
dotnet run --project src/MicroEMR.DatabaseTool -- `
  platform-entitlement assign `
  --user-id "<REVIEWER_USER_ID>" `
  --entitlement "SecurityAudit.View" `
  --confirm "<REVIEWER_USER_ID>"
```

Record the returned authorization version and correlation ID.

Verify:

```sql
DECLARE @UserId nvarchar(450) = N'<REVIEWER_USER_ID>';

SELECT AuthorizationVersion, UpdatedAtUtc
FROM dbo.PlatformAuthorizationState WHERE UserId = @UserId;

SELECT COUNT(*) AS ActiveAssignmentCount
FROM dbo.UserPlatformEntitlement AS upe
JOIN dbo.PlatformEntitlement AS e
  ON e.PlatformEntitlementUid = upe.PlatformEntitlementUid
WHERE upe.UserId = @UserId
  AND e.EntitlementKey = N'SecurityAudit.View'
  AND upe.RevokedAtUtc IS NULL;

SELECT Action, ActorUserId, TargetUserId, Outcome,
       OccurredAtUtc, CorrelationId, DetailsJson
FROM dbo.PlatformAuditEvent
WHERE TargetUserId = @UserId
  AND Action = N'PlatformEntitlementAssigned'
ORDER BY OccurredAtUtc DESC;
```

PASS when the version increments exactly once, active count is one, and exactly one new successful assignment event has the command's correlation ID.

## 5. Fresh authentication and entitled UI

1. Sign out completely and clear only the local application's cookies if necessary.
2. Open `https://localhost:7002` and sign in as the reviewer.
3. Confirm `Platform Administration` → `Security Audit` appears.
4. Open it, then separately try `/PlatformSecurityAudit` directly.

Expected: fresh authentication succeeds; navigation is present; the page opens without authorization error or tenant-context requirement.

## 6. Default search and filters

On initial load verify:

- approximately the last 24 hours in UTC;
- page size 25 and no more than 25 rows;
- newest-first ordering;
- successful results or the governed empty state.

Exercise filters when matching test data exists:

- Denial Reason;
- date/time range;
- Source Application;
- Correlation ID;
- Trusted Tenant UID;
- Capability;
- exact Actor Subject under the restricted section.

For each filter, apply it, confirm all returned rows match, and confirm paging restarts. Confirm Reset restores the default search. Confirm Actor Subject is not retained visibly or placed in the URL. Test a range over 31 days; expect concise validation with no disclosure or internal SQL/stack detail. Mark unavailable test-data cases **NOT VERIFIED**.

## 7. Keyset paging

Requires more than 25 matching events.

1. Note several UIDs/timestamps on the newest page.
2. Click `Older events`.
3. Confirm older records and no obvious duplicates.
4. Confirm there is no page number, total, or fabricated page count.
5. Click `Back to newest`.
6. Change a filter and confirm the previous continuation is discarded.

If `Older events` is unavailable, record **NOT VERIFIED**.

## 8. Detail and absence of clinical enrichment

Open one explicit `Details` action. Using the Network tab, confirm detail is requested only after opening it and is not prefetched per list row.

Expected detail content:

- approved security metadata and safe rendering of nulls;
- trusted and requested/untrusted tenant labels where applicable;
- no patient name, clinical note, document title, or clinical content;
- no visible tenant-clinical enrichment.

## 9. Review-access audit cardinality

Record the start time:

```sql
SELECT SYSUTCDATETIME() AS AuditCheckStartedAtUtc;
```

After that time perform exactly one successful search and open exactly one detail. Query:

```sql
DECLARE @ReviewerId nvarchar(450) = N'<REVIEWER_USER_ID>';
DECLARE @StartedAtUtc datetime2(7) = '<RECORDED_UTC_TIME>';

SELECT Action, ActorUserId, ActorType, OccurredAtUtc,
       CorrelationId, DetailsJson
FROM dbo.PlatformAuditEvent
WHERE ActorUserId = @ReviewerId
  AND OccurredAtUtc >= @StartedAtUtc
  AND Action IN (N'SecurityAuditSearched', N'SecurityAuditViewed')
ORDER BY OccurredAtUtc;
```

Expected: one `SecurityAuditSearched` per successful disclosure and one `SecurityAuditViewed` for the opened detail; `ActorType = PlatformReviewer`; no detail event per returned row and no duplicate client-side audit call. Remember that initial page loading is itself a search.

## 10. Unauthorized UI and API

Confirm the unauthorized identity has no active assignment:

```sql
DECLARE @UserId nvarchar(450) = N'<UNAUTHORIZED_USER_ID>';

SELECT COUNT(*) AS ActiveAssignmentCount
FROM dbo.UserPlatformEntitlement AS upe
JOIN dbo.PlatformEntitlement AS e
  ON e.PlatformEntitlementUid = upe.PlatformEntitlementUid
WHERE upe.UserId = @UserId
  AND e.EntitlementKey = N'SecurityAudit.View'
  AND upe.RevokedAtUtc IS NULL;
```

Expected count: zero. Sign in freshly as that identity and verify independently:

- navigation is absent;
- `/PlatformSecurityAudit` denies and renders no records;
- the secured API returns no data;
- an authenticated API call is `403 Forbidden` (an unauthenticated `401` does not prove this gate).

If available, repeat with a platform-role-only user and a tenant-admin-only user. Neither role may imply this entitlement. Do not paste cookies or tokens into command lines.

## 11. Five-minute continuity and automatic refresh

1. Sign out and freshly sign in as the entitled reviewer.
2. Confirm Security Audit works and record UTC time.
3. Leave the session idle beyond six minutes.
4. Trigger a Security Audit search/filter request.
5. Trigger another request afterward.

PASS when requests succeed without forced sign-in, operational server diagnostics show renewal without token values, no refresh loop occurs, and subsequent use succeeds. The token redemption is server-to-server and need not appear in browser Network. Do not claim ticket-expiry metadata was updated unless an approved server-side diagnostic proves it without revealing tokens.

## 12. Revoke while the reviewer remains authenticated

Record `VersionBeforeRevoke`:

```sql
DECLARE @UserId nvarchar(450) = N'<REVIEWER_USER_ID>';
SELECT AuthorizationVersion, UpdatedAtUtc
FROM dbo.PlatformAuthorizationState WHERE UserId = @UserId;
```

Run:

```powershell
dotnet run --project src/MicroEMR.DatabaseTool -- `
  platform-entitlement revoke `
  --user-id "<REVIEWER_USER_ID>" `
  --entitlement "SecurityAudit.View" `
  --confirm "<REVIEWER_USER_ID>"
```

Record UTC time, returned version, and correlation ID. Verify:

```sql
DECLARE @UserId nvarchar(450) = N'<REVIEWER_USER_ID>';

SELECT AuthorizationVersion, UpdatedAtUtc
FROM dbo.PlatformAuthorizationState WHERE UserId = @UserId;

SELECT COUNT(*) AS ActiveAssignmentCount
FROM dbo.UserPlatformEntitlement AS upe
JOIN dbo.PlatformEntitlement AS e
  ON e.PlatformEntitlementUid = upe.PlatformEntitlementUid
WHERE upe.UserId = @UserId
  AND e.EntitlementKey = N'SecurityAudit.View'
  AND upe.RevokedAtUtc IS NULL;

SELECT Action, Outcome, OccurredAtUtc, CorrelationId, DetailsJson
FROM dbo.PlatformAuditEvent
WHERE TargetUserId = @UserId
  AND Action = N'PlatformEntitlementRevoked'
ORDER BY OccurredAtUtc DESC;
```

PASS when the version increments exactly once, active count is zero, and exactly one new successful revocation event matches the correlation ID.

## 13. Residual window and stale refresh

Do not sign out. Immediately attempt a Security Audit request, then repeat approximately once per minute for no longer than six minutes. Record UTC time and success/denial for each attempt.

```text
Revoked at:
+0 minute:
+1 minute:
+2 minutes:
+3 minutes:
+4 minutes:
+5 minutes:
+6 minutes:
```

An already-issued self-contained token may temporarily retain access. Do not describe revocation as instantaneous unless observed. Expected maximum residual authorization is approximately five minutes, depending on token issue time.

At the refresh/expiry boundary verify:

- stale refresh is rejected;
- no new token recreates `SecurityAudit.View`;
- reauthentication is required;
- no refresh loop or endless old-request retry occurs;
- no audit data is disclosed afterward.

Stop and report a security defect if refresh extends revoked access beyond the bounded lifetime.

## 14. Fresh authentication after revocation

Authenticate again as the same reviewer. Expected:

- authentication may succeed;
- Security Audit navigation is absent;
- direct page access denies;
- secured API returns no records;
- revoked entitlement is not effective in the fresh session.

## 15. Token secrecy

Review browser URLs/responses/rendered HTML and Auth, Web, and API logs. Do not search using an actual token value.

```powershell
rg -n -i "access_token|refresh_token|authorization: bearer|client_secret" `
  auth-run*.log api-run*.log web-run*.log
```

Field names or configuration text may match; inspect carefully. PASS only when no actual access token, refresh token, bearer value, or client secret is exposed. Refresh tokens must remain server-side.

## 16. Final cleanup

Confirm the reviewer remains revoked:

```sql
DECLARE @UserId nvarchar(450) = N'<REVIEWER_USER_ID>';

SELECT COUNT(*) AS ActiveAssignmentCount
FROM dbo.UserPlatformEntitlement AS upe
JOIN dbo.PlatformEntitlement AS e
  ON e.PlatformEntitlementUid = upe.PlatformEntitlementUid
WHERE upe.UserId = @UserId
  AND e.EntitlementKey = N'SecurityAudit.View'
  AND upe.RevokedAtUtc IS NULL;
```

Expected: zero. Never physically delete entitlement or audit history.

## Operator results

Complete this section and provide it for incorporation into `27-step23b-runtime-validation.md`.

```text
Reviewer label:
Environment: non-production
Manual browser and tools used:
Migration 020 confirmed: PASS / FAIL / NOT VERIFIED

1. Explicit assignment:
2. Fresh authentication after assignment:
3. Entitled navigation:
4. Entitled page:
5. Default search:
6. Filters:
7. Keyset paging:
8. Event detail:
9. No clinical enrichment:
10. SecurityAuditSearched event:
11. SecurityAuditViewed event:
12. No per-row audit noise:
13. Unauthorized navigation:
14. Unauthorized direct URL:
15. Unauthorized API:
16. Five-minute continuity:
17. Automatic server-side refresh:
18. Entitlement revocation:
19. PlatformAuthorizationVersion increment:
20. Bounded residual access:
21. Stale-refresh rejection:
22. Revoked entitlement not reissued:
23. Reauthentication required:
24. Fresh authentication after revocation:
25. Security Audit inaccessible after revocation:
26. Token secrecy:

Authorization version before assignment:
Authorization version after assignment:
Authorization version before revocation:
Authorization version after revocation:
Revocation UTC time:
Last successful residual-access UTC time:
First denied-access UTC time:
Final active entitlement count:
Platform-role-only result:
Tenant-admin-only result:
Errors or unexpected behavior:
```

Unperformed gates must be recorded as **NOT VERIFIED**, never inferred as PASS.
