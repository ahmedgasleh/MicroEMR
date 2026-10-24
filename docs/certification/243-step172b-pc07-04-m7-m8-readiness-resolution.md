# Step 172B — PC07.04 M7/M8 readiness resolution

2026-10-24 (America/Toronto). **M7 READY; M8 READY — prerequisites verified, authenticated execution not performed.** The original named Reader A and Writer B accounts, their actual tenant assignments, active clinical bindings and populated existing fixtures are resolved. **No fixture, permission, membership or product change is needed.** PC07.04 remains PARTIAL — IMPLEMENTED; PC07.01 PARTIAL; inventory **9 mandatory-gap IDs / three packages**, unchanged.

## 1. Scope and key resolution

Reuse [Step 171](241-step171-pc07-04-family-history-evidence-reconciliation.md), [Step 172A](242-step172a-pc07-04-family-readiness.md), and the existing [M7/M8 definitions](131-step97-pc07-01-family-history-implementation.md) / [read-only M8 supplement](132-step97-m8-m9-automation.md). Six-field functionality, owner M1–M6, isolated security/audit/concurrency and reported API denials remain accepted; no tests repeated.

**The earlier assumption that local-dev-fresh should be scenario Tenant A was provisional.** The actual existing test identities and fixtures resolve the original labels as follows:

| Scenario label | Actual registered development tenant | Actual database / endpoint |
|---|---|---|
| **Tenant A — Reader A / original Family F** | **local-dev**, UID **8531aab9-e647-4d3f-852c-8fbcf9b4dd7b** | **MicroEMR_Db**, **192.168.50.1,50013** |
| **Tenant B — Writer B / other Family B** | **local-dev-fresh**, UID **f94c6ecd-4417-483e-8a94-0507cf8384c2** | **MicroEMR_LocalDev_Fresh**, **192.168.50.1,50013** |

This changes only **test-plan labels**, not database records, tenant assignment or permissions. It follows actual catalog/identity/account evidence and aligns with the original Mother/type-2-diabetes A and Maternal-aunt/asthma B examples. Step 172A's local-dev-fresh fixture remains valid; it is now correctly used as scenario B. Do not move it, rename its notes or grant Reader A access to the wrong tenant to preserve the provisional labels.

## 2. Read-only metadata evidence and method

Read the existing development platform catalog in **MicroEMR_Platform**, existing Auth identity metadata in **MicroEMR_Auth**, and the two catalog-identified nonproduction clinical databases. Connections were constrained to **192.168.50.1,50013**, exact named development databases and matching tenant identities. No production connection, authenticated application request or tenant switch.

Catalog/permissions observation: **2026-10-24T19:27:59.0233538Z**. Account/second-database observation: **2026-10-24T19:29:40.4937649Z**. Final active-account/audit-observer metadata: **2026-10-24T19:31:25.7790996Z**.

The catalog confirms both selected tenants and database assignments **Active**. The local-dev-fresh operator binding exists; the local-dev binding is supplied by the existing API development configuration. Its absence from the DatabaseTool's narrower secret map was not proof of an unavailable tenant. Existing API configuration also supplies the Auth connection. Only sanitized endpoint/database metadata was recorded; no credential strings, password hashes, cookies or tokens were printed/retrieved for login.

Permission SELECTs used actual `UserTenantMembership`, `UserTenantAccessProfile`, active `AccessProfile`, `AccessProfilePermission` and `UserPermissionOverride` definitions. Relevant effective permissions were calculated with the existing [012 override precedence](../../db/platform/012_user_permission_overrides.sql): explicit Allow, otherwise Deny, otherwise active profile membership; Active membership checked separately. No access-grant stored procedure ran. Auth SELECTs read only identity/activity/lockout metadata, not password material.

No service probes, prior fixture/test suites or broad repository scans were repeated. Step 172A's Web/API/Auth reachability remains retained context; future execution must stop on actual connection/authentication failure rather than assume it cannot change.

## 3. M7 — Reader A READY

| Property | Verified metadata |
|---|---|
| Intended account | **a.reader@example.invalid** |
| Auth subject | **02449cfa-075a-4345-a09d-500f98b8018d** |
| Auth account | IsActive **true**, LockoutEnd **NULL** |
| Actual membership | **Active in local-dev**, not local-dev-fresh |
| Access profile | **Nurse**, active |
| Patients.View | **Allowed** |
| ClinicalData.Manage | **Denied**, explicit per-user **Deny** override |
| Tenant-local clinical binding | `MicroEMR_Db.dbo.ApplicationUser`: **UserId 8**, matching subject/username, IsActive **true** |

EmailConfirmed is false for the named accounts; existing Auth `Program.cs` explicitly sets `RequireConfirmedEmail=false`. No email/lockout setting changed. This is readiness metadata, **not a successful login/credential check**. Owner supplies credentials directly through normal login later, never in chat. No forgotten-password/reset or account repair is authorized.

Reader A is suitable for the documented M7: permitted local reads, no management, valid tenant-local actor and existing Active target. Use this named identity rather than infer a role from display names, row creator or an admin account. The earlier positive Risk session cannot substitute for this restricted session.

### Existing Tenant A resources

| Purpose | Existing identifier / state |
|---|---|
| **A_PATIENT** | **2509f6b0-430e-4503-a837-b9e25782122e**, nondeleted |
| **F_A — selected Active target** | **500e500f-c3a9-4d8a-bba1-fd091cf0a001**; RowVersion **0x00000000000065A2** |
| F_A six fields | Mother; `TEST ONLY - type 2 diabetes`; `45 years`; **A → Adult**; `TEST ONLY - oral therapy reviewed`; `TEST ONLY - first editor saved` |
| Existing archived row on A_PATIENT | **80b4da7e-0889-449e-ac89-9c54a9caa803**, Archived, version **0x00000000000065A0**; retain unchanged |
| **A_OTHER — distinct same-tenant patient** | **d44f99a3-467c-484a-8a50-b6e4785caddc**, nondeleted |
| Existing local A_OTHER row | **118787dd-eb4b-4e17-9254-a4c0116f6eb0**, Active, version **0x000000000000659C**; not the target F_A |

The recorded A_OTHER condition includes duplicated text `TEST ONLY - hypertensionTEST ONLY - hypertension`; preserve it as baseline data, do not “clean it up.” It does not affect compound F_A ownership. Active F_A and an archived row already exist, so no new transition or test record is needed. All development data is fictional/synthetic under the standing project-owner policy; identifiers/states were verified rather than inferred from that policy.

## 4. M8 — Tenant B/account/fixture READY

**Tenant B is local-dev-fresh / MicroEMR_LocalDev_Fresh**, verified independently by `dbo.TenantDatabaseIdentity` and its active platform assignment. The former unknown second tenant is **local-dev / MicroEMR_Db**, used as scenario A to match the existing Reader A. No arbitrary database or fixture-note label is used as identity authority.

| Property | Verified metadata / retained fixture evidence |
|---|---|
| Intended B account | **b.writer@example.invalid** |
| Auth subject | **53d487fb-307c-4f20-8e97-b4722d3218c9** |
| Auth account | IsActive **true**, LockoutEnd **NULL** |
| Membership / profile | **Active local-dev-fresh** membership; active **Medical Assistant** profile |
| Effective read/manage | Patients.View **allowed**; ClinicalData.Manage **allowed** |
| Clinical actor | local-dev-fresh **UserId 5**, active, matching subject/username |
| **B_PATIENT** | **1cf3493f-d4b1-4d75-a327-c39f50adb9db**, nondeleted |
| **F_B** | **f491203c-da0e-477e-b5c5-7217d50ecf1b**, Active, version **0x0000000000002712** |
| F_B six values | Maternal aunt; `TEST ONLY - asthma`; `12 years`; **C → Child**; `TEST ONLY - inhaled therapy`; `TEST ONLY - Tenant B fixture` |

B patient/resource/value/version evidence is reused from Step 172A; its database identity and actor/membership configuration were resolved here. Refresh its selected rows/version/audit boundary immediately before later execution; do not repeat fixture discovery/implementation analysis. The API-binding audit SELECT check in local-dev-fresh was denied under its existing restricted SQL permissions; **no grant or bypass was attempted**. The already authorized development **operator binding** successfully SELECTs the required audit columns there. The existing development API binding for MicroEMR_Db successfully SELECTs its audit columns. Use those existing observer bindings; no security weakening is required.

The M8 **remaining normal-session read-routing assertions are executable without fixture creation** using Reader A's A session and Writer B's B session. Writer B's management permission does not authorize or require a clinical write in this plan. Original full get/update/archive backend permutations remain covered by isolated evidence; normal-session GET routing is the specific residual defined by the supplement.

## 5. Exact future execution scope — no execution in Step 172B

### Sessions and normal presentation

**Two normal logins total**, one per separate fresh Chromium context:

1. **a.reader@example.invalid → local-dev**, required rendered tenant UID `8531aab9-e647-4d3f-852c-8fbcf9b4dd7b`.
2. **b.writer@example.invalid → local-dev-fresh**, required UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`.

One attempt each; no automatic retry, context/cookie/token sharing or manual tenant claims. No session reuse from earlier unrelated admin verification. Password entry remains normal and private. Confirm allowed local list/detail reads, active account/actor binding and fresh effective permission context before expected-denied/foreign requests.

Reader A chart:

`GET https://localhost:7002/Patients/Details?patientUid=2509f6b0-430e-4503-a837-b9e25782122e&tab=family-history`

Writer B chart:

`GET https://localhost:7002/Patients/Details?patientUid=1cf3493f-d4b1-4d75-a327-c39f50adb9db&tab=family-history`

Reuse Step 171's actual `#patientFamilyHistoryRoot`, `#familyHistoryList`, status select and CPP field/preference selectors. Correlate six rendered values/CT-016 labels with each local baseline where needed, inspect saved CPP visibility without changes, and confirm Reader A Add/Edit/Archive controls absent/disabled. A positive local scoped detail read must return **200** with the correct owned resource before its negative GETs. Retain M1–M6 appearance and full-print evidence; only minimal missing desktop/mobile context/label/focus captures. No valid clinical save, archive, stale-write, preference save or PDF-generation submission.

### The three expected-denied Web requests — established, count 3

These are **M7 permission requests**, not M8 foreign-routing requests. Definitions confirmed in [PatientFamilyHistoryController](../../src/MicroEMR.Web/Controllers/PatientFamilyHistoryController.cs); all require ClinicalData.Manage and antiforgery in addition to normal authentication/Patients.View.

Under **Reader A / local-dev only**:

| Method | Exact application path | Required response |
|---|---|---|
| **POST** | `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history` | **403 Forbidden** |
| **POST** | `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **403 Forbidden** |
| **POST** | `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001/archive` | **403 Forbidden** |

Use Reader A's own existing form antiforgery material within the helper and a valid six-field shape from F_A; update/archive carry F_A's freshly read Base64 RowVersion. Do not copy Writer B or admin cookies/tokens, use a malformed body to provoke a different error, or attempt a positive write. Client guard rejection, 400, 401, redirect, 404 or merely hidden buttons does not satisfy the required server **403**.

Exactly one attempt per approved path; no repeat of the three already reported **API** denials by default. API update is PUT, but the actual **Web** update above is POST; do not conflate them. Capture fresh unchanged-state/audit proof around these missing Web checks. It cannot reconstruct historical API snapshot evidence; preserve that qualification rather than silently replay old checks. Unexpected success/change is FAIL: stop, no repair/retry/cleanup write.

### M8 five foreign-resource GETs — established, count 5

Origin `https://localhost:7002`, actual normal selected contexts:

| Context | Exact GET path | Expected |
|---|---|---|
| Reader A / local-dev | `/patients/d44f99a3-467c-484a-8a50-b6e4785caddc/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **404**, same-tenant wrong patient |
| Writer B / local-dev-fresh | `/patients/1cf3493f-d4b1-4d75-a327-c39f50adb9db/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **404**, foreign resource under local patient |
| Writer B / local-dev-fresh | `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/500e500f-c3a9-4d8a-bba1-fd091cf0a001` | **404**, foreign patient/resource |
| Reader A / local-dev | `/patients/2509f6b0-430e-4503-a837-b9e25782122e/family-history/f491203c-da0e-477e-b5c5-7217d50ecf1b` | **404**, foreign resource under local patient |
| Reader A / local-dev | `/patients/1cf3493f-d4b1-4d75-a327-c39f50adb9db/family-history/f491203c-da0e-477e-b5c5-7217d50ecf1b` | **404**, foreign patient/resource |

Validate no foreign clinical values/resource-version disclosure and unchanged selected-tenant context. A 401/403/redirect is not this **404** routing assertion. Preserve complete local positive UID sets; do not use random IDs or null placeholders. No live cross-tenant PUT/archive or nonmember-tenant scenario is added. Reader A's own A read context is sufficient for the proposed GET-only residual; no third writer login is needed just to repeat backend mutation assertions.

## 6. SELECT-only state/audit correlation and criteria

Use Step 171's verified identity, row/version, complete Family fingerprint and bounded audit-window SELECTs on **each exact separate tenant connection**. Baseline includes F_A Active, existing A archived/other-patient rows and F_B. Never treat one database's resource parameters as a substitute for opening/verifying the other database.

```sql
SELECT DB_NAME() AS DatabaseName,TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;

SELECT FamilyHistoryUid,PatientUid,Relationship,ConditionDescription,
       AgeAtOnset,LifeStageCode,Treatment,Notes,Status,
       CreatedAt,CreatedBy,UpdatedAt,UpdatedBy,
       CONVERT(varchar(18),CONVERT(varbinary(8),RowVersion),1) AS RowVersionHex
FROM dbo.PatientFamilyMedicalHistory ORDER BY FamilyHistoryUid;

SELECT SYSUTCDATETIME() AS StartedAtUtc,
       ISNULL(MAX(AuditLogId),0) AS AuditStartId FROM dbo.AuditLog;

-- @AuditStartId: bind the fresh boundary from that same tenant
SELECT AuditLogId,UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,
       EventCategory,Outcome,OldValue,NewValue,CreatedAt
FROM dbo.AuditLog WHERE AuditLogId>@AuditStartId ORDER BY AuditLogId;
```

Query columns/SELECT access were verified without historical audit scans. Expected after execution: all Family fields, states, row sets and eight-byte versions unchanged, no new successful Family Create/Update/Archive audit; ordinary authenticated ClinicalRead/security audit is acceptable and remains distinguishable. Correlate reads to the proper per-tenant clinical actor (**A reader 8 / B writer 5**) and actual patient; no client-supplied actor. Preserve legacy PatientId/EntityId audit semantics where appropriate. No direct audit procedure invocation or mutation SQL.

| Future observation | Classification |
|---|---|
| Correct two contexts, allowed local reads/values, M7 restricted UI and three Web 403s, five M8 404s, no disclosure/change, permitted read-audit | PASS for the executed bounded residuals; review qualified PC07.04 closure afterward |
| Authentication/selected-tenant/fixture/observer unavailable at runtime | BLOCKED/DEFERRED for that assertion; stop, no automatic account/profile/fixture repair |
| Unexecuted optional no-view/nonmember/full live mutation scenarios | Preserve existing explicit qualification, not PASS |
| Wrong tenant/value/disclosure, unexpected successful denied write, version/data or mutation-audit change attributable to attempts | FAIL, stop and document; no compensating write |

Reuse actual Chromium helper, independent contexts and per-stage request guard. Permit normal authentication POSTs, GET/HEAD and only the explicitly approved three Reader A denial requests. Block all Writer B clinical POSTs and every other non-read request, including preference/print submissions and automatic CDS evaluation; log guard-induced limitations. No mocks, completed-suite reruns or broad deployment reassessment.

## 7. Missing prerequisites, authorization and Step 172C

**No clinical fixture/account/membership/permission prerequisite remains missing in the inspected metadata.** Credential usability, normal session selection, effective permissions observed through the deployed session and runtime outcomes are deliberately **not executed**. READY means prerequisite readiness, not authenticated verification PASS. Owner supplies known existing credentials directly at normal login; no credential retrieval/reset in this plan. Services may change after retained preflight; report actual failures without automatic restart.

**Separate execution authorization needed:** two one-attempt normal logins for the exact named accounts/tenants above; allowed local/read-only presentation/CPP checks; three exact expected-denied Reader A Web POSTs and five exact foreign-resource GETs; SELECT-only before/after observers in both development databases; ordinary read/security audit and minimal captures. Expected successful clinical writes **0**. No fixture creation, permission/membership change, valid clinical save, additional login, PDF/preference submission, cleanup or source change.

**Proposed Step 172C — execute this bounded two-session M7/M8 verification**, after that explicit authorization. Reuse the now-confirmed existing fixtures; refresh identity/state/audit boundaries only as needed for execution. Do not repeat metadata discovery, M1–M6, reported API denials, isolated mutation tests or fixture readiness loops. If successful, recommend formal qualified PC07.04 closure with preserved clinician/release/security/Family historical qualifications, then reconcile its separate counted ID in a later documentation decision. No automatic closure/count reduction now.

Remaining estimate: **1–2 hours** helper/session/denied-request adaptation, **1–2 hours** authorized checks/captures and **0.5–1 hour** state/audit review/report: **2.5–5 engineering hours**, assuming owner availability and unchanged services/accounts. Fixture preparation, permission changes and implementation: **0 planned**. Approval/credential/service availability and external accepted-release/clinician decisions may extend elapsed time without a supported upper bound. No overall certification/evaluation date promised.

## 8. Files, safety and inventory

Only this report added outside ignored `.tmp/step172b/` metadata helpers/results (`catalog.json`, `resolution.json`, `final-metadata.json`). Existing development configuration was read in memory; no secret/permission/configuration changes. The initial runtime-binding audit metadata denial in local-dev-fresh was resolved using its existing permitted operator observer, leaving denied runtime privileges intact. No automatic approval rejection or permission escalation of the application occurred.

Current branch **main** retained; existing untracked reports 233–242 preserved unchanged. Targeted source/metadata SELECTs and final report whitespace/status checks only. No login, authenticated test/tenant switch, clinical write, fixture creation, test/browser execution, service probe/restart, migration/build, product edit, feature branch, commit, merge, push, reset, clean, stash or GitHub operation.

**PC07.04 PARTIAL — IMPLEMENTED; PC07.01 PARTIAL; 9 mandatory-gap IDs / three packages unchanged:** prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.10 (3), scheduling PC09.03 (1). PC07.07 and DE08.008 qualified closures retained. Stop after documentation.
