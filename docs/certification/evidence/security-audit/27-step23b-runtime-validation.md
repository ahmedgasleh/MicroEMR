# Step 23B Runtime Validation

Date: 2026-08-23  
Branch: `main`  
Result: **PASS - runtime qualification complete**

## Scope and safety boundaries

This continuation qualified the existing tenant-dependency runtime fix and completed the remaining data-dependent Security Audit gates. No migration was created. Platform migrations 013, 018, 019, and 020 were not modified. No production delete or test-cleanup capability was added. No credential value was printed or copied into evidence. No commit, merge, or push was performed during this continuation.

The current working branch already contained the merged runtime fix before this continuation began:

- `CurrentUserPermissionService` defers tenant resolution until permission loading.
- `AuthenticatedClinicalUserAccessor` defers tenant resolution until clinical-actor resolution.
- `DeferredTenantContext` permits dependency construction without inventing a tenant, but throws when tenant properties are requested before trusted resolution.
- API dependency injection registers the deferred tenant-context adapter.

The defect was that platform-wide Security Audit authorization could construct shared tenant-dependent services before any tenant context existed. The fix changes construction timing only; it does not make tenant-scoped behavior tenant-optional.

## Qualification gate

| Gate | Result | Evidence |
|---|---|---|
| Dependency construction without tenant | **PASS** | `DeferredTenantContext` construction and platform-entitlement authorization tests |
| Platform Security Audit path remains tenant-independent | **PASS** | controller dependency test plus platform-entitlement test without tenant or clinical claims |
| TenantUid/TenantKey/DisplayName before resolution | **PASS - fail closed** | all three property accesses throw `InvalidOperationException` |
| Tenant database access before resolution | **PASS - fail closed** | connection factory throws before resolver or secret-provider invocation |
| Trusted resolved tenant properties | **PASS** | resolved UID, key, and display name are proxied exactly |
| Permission loading before resolution | **PASS - fail closed** | throws before access-profile repository invocation |
| Clinical actor resolution before resolution | **PASS - fail closed** | throws before clinical-user repository invocation |
| No fallback/default tenant UID | **PASS** | unresolved property access throws; no empty UID is returned |
| No default tenant database | **PASS** | resolver and secret-provider call counts remain zero |
| Tenant isolation | **PASS** | 40/40 focused tenant context/resolution/database tests |
| Focused Security Audit/entitlement/tenant tests | **PASS** | 63/63 |
| Auth tests | **PASS** | 30/30 |
| Full API tests | **PASS** | 677/677; Playwright test required permission to launch installed Chromium |
| Release solution build | **PASS** | 0 warnings, 0 errors |

An initial full-API run inside the restricted process sandbox produced one unrelated `spawn EPERM` failure when Playwright attempted to launch Chromium. The identical suite passed 677/677 when the installed browser was permitted to launch. This was an execution-environment restriction, not an application regression.

## Governed test-event generation

Exactly 32 controlled events were generated in the test platform database through the existing governed stored procedures only:

- `dbo.PlatformSecurityAudit_RecordMissingPermission`
- `dbo.PlatformSecurityAudit_RecordCrossPatientOwnership`
- `dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor`
- `dbo.PlatformSecurityAudit_RecordInvalidTenantMembership`

The dataset uses the correlation prefix `step23b-20260823`, a synthetic actor, synthetic tenant/resource identifiers, approved capability/permission combinations, and no clinical content. Eight events of each denial type were created. No direct insert into `PlatformSecurityAuditEvent` was used. The rows remain as immutable security evidence because the architecture supplies no approved deletion mechanism.

## Data-dependent validation

| Gate | Result | Direct result |
|---|---|---|
| DenialReason filter | **PASS** | `MissingPermission` returned 8/8 matching governed rows |
| SourceApplication filter | **PASS** | `MicroEMR.Auth` returned 8/8 matching governed rows |
| Date/time filter | **PASS** | bounded split range returned 16; independently expected 16 |
| Capability filter | **PASS** | `EncounterEdit` returned 8/8 matching governed rows |
| Trusted tenant filter | **PASS** | returned 24 matching rows; the eight invalid-membership rows correctly store the requested untrusted tenant separately |
| Exact correlation filter | **PASS** | exact correlation returned one matching row |
| Reset/default semantics | **PASS** | controller test proves filters/continuation are cleared; unfiltered governed search returned all 32 controlled rows |
| Matching-only behavior | **PASS** | every filter result matched its exact governed predicate |
| Keyset first page | **PASS** | 25 rows |
| Keyset continuation | **PASS** | 7 older rows |
| Keyset stability | **PASS** | 32 unique rows, no duplicates/skips, exact `(OccurredAtUtc DESC, SecurityAuditEventUid DESC)` order |
| Continuation reset on filters | **PASS** | controller and continuation-fingerprint tests |
| Fabricated total pages | **PASS - absent** | continuation-only model; no total-page count |

## Detail, disclosure audit, and enrichment

| Gate | Result | Evidence |
|---|---|---|
| List does not prefetch detail | **PASS** | request-interception test records zero detail calls during list load |
| One explicit detail action | **PASS** | one known governed event returned by `PlatformSecurityAudit_GetByUid`; interception test records exactly one detail call |
| Approved detail fields | **PASS** | governed detail returned the approved 17-field DTO shape |
| Closing detail is non-mutating | **PASS** | detail is a GET-only view; no mutation endpoint/action exists |
| Unopened rows do not fetch detail | **PASS** | interception test records no per-row detail calls |
| No clinical enrichment | **PASS** | review controller has no tenant/clinical dependency, review repository uses only platform procedures, list/detail DTO tests exclude names/titles, and interception tests show no tenant-clinical lookup path |
| `SecurityAuditViewed` cardinality | **PASS** | exactly one successful review audit for one known detail |
| Security-event disclosure duplication | **PASS - none** | opening detail changed `PlatformSecurityAuditEvent` count by zero |
| Sensitive payload copied to platform audit | **PASS - no** | platform audit details contain only the reviewed security-event UID |
| Search audit cardinality | **PASS** | one multi-row disclosure produced exactly one `SecurityAuditSearched` audit |
| Per-row audit noise | **PASS - none** | 25 disclosed rows produced an audit delta of one, not 25 |

The browser Network-tab integration remained unavailable because the Codex browser backend lacked required sandbox-policy metadata. Lazy-loading and no-enrichment behavior were therefore verified by focused request interception and dependency/repository tests, combined with live governed stored-procedure execution, rather than by browser Network-tab screenshots.

## Authenticated unauthorized API

An in-process authenticated ASP.NET HTTP test invoked `POST /api/platform/security-audit/search` with authenticated principals that lacked `SecurityAudit.View`. It did not manually extract or paste any cookie or bearer token.

| Identity | Result |
|---|---|
| PlatformAdministrator role only | **PASS - 403 Forbidden** |
| Tenant Administrator role plus tenant claim only | **PASS - 403 Forbidden** |
| Review service invocation | **PASS - zero calls** |
| Audit row disclosure | **PASS - none in response** |

This proves that neither platform role nor tenant-admin context implicitly grants `SecurityAudit.View` and that the HTTP authorization pipeline fails before the disclosure service.

## Previously completed manual runtime gates

The following results supplied by the operator remain valid because this continuation did not change application runtime source, entitlement semantics, token/session behavior, or migrations:

| Gate | Result |
|---|---|
| Explicit entitlement assignment | **PASS** |
| Fresh authentication after assignment | **PASS** |
| Entitled navigation and page | **PASS** |
| Default governed search/empty state | **PASS** |
| `SecurityAuditSearched` | **PASS** |
| Unauthorized navigation | **PASS** |
| Unauthorized direct URL | **PASS** |
| Five-minute continuity | **PASS** |
| Automatic server-side refresh | **PASS** |
| Entitlement revocation | **PASS** |
| Platform authorization version increment | **PASS** |
| Bounded residual access | **PASS** |
| Stale-refresh rejection | **PASS** |
| Revoked entitlement not reissued | **PASS** |
| Reauthentication after revocation | **PASS** |
| Post-revocation Security Audit denial | **PASS** |
| Token secrecy | **PASS - observed token values were redacted** |

The final entitlement state reported by the operator was revoked/inactive. Governed test-event creation did not change entitlement state.

## Deferred security/tooling issues

- **HIGH PRIORITY - OPEN:** a plaintext SQL credential remains tracked in `src/MicroEMR.Auth/appsettings.json`. The database password was changed, but repository/source and history remediation remains a separate authorized security task. The credential was not printed or modified here.
- **BACKLOG - OPEN:** DatabaseTool configuration precedence remains unresolved. It was not modified or bypassed as a source change in this work.

## Final state

- Remaining Step 23B `NOT VERIFIED` critical gates: **none**.
- New security defects found: **none**.
- Current uncommitted changes are limited to qualification/regression tests and this evidence record.
- Runtime-fix changes and added regression coverage: **SAFE TO COMMIT after review**.
- Step 23B runtime complete: **YES**.
- Step 23 Security Audit workstream complete: **YES**, subject to normal review/commit and the separately tracked credential/tooling remediation items above.
