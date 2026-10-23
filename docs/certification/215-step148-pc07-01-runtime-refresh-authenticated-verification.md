# Step 148 — PC07.01 controlled runtime refresh and authenticated verification

2026-10-22. **Targeted API/Web refresh PASS. Live Swagger and anonymous Web corrections PASS. Owner-approved single login, authenticated return, fixed-tenant routing and read-only prescription access PASS.** No database migrations, backup, restore or clinical content changes. PC07.01 **PARTIAL**; inventory unchanged at **10 mandatory-gap IDs / three packages**.

## 1. Git checkpoint and scope

Read AGENTS.md; reused relevant recovery/runtime sections of [report 213](213-step146-pc07-01-recovery-and-controlled-smoke-acceptance.md) and [report 214](214-step147-pc07-01-api-runtime-diagnosis.md). Recorded branch, HEAD and status once:

- Branch **`feature/step-147-api-swagger-runtime-fix`**; HEAD **`0a561092e595d2d963de40065a9eb0727094fa92`**.
- Existing modifications: API Program.cs and Web Authorization/PermissionAuthorization.cs. Existing untracked reports 211–214, ApiSwaggerConfiguration.cs, Step144IntegratedAcceptanceTests.cs and Step147SwaggerTests.cs preserved.
- No product/test/configuration/secret changes, new feature branch, staging, commit, merge, reset, clean or push. Only this report added outside ignored local runtime helpers/evidence.

Owner explicitly authorized the affected local API/Web refresh. No authorization inferred for Auth restart, SQL operations or clinical writes. The four Step 147 focused PASS results and successful build were reused; no build or test suite rerun.

## 2. Controlled refresh and environment

Before stopping anything, process path and port checks identified exactly these local repository executables: API PID **3184**, ports **7003/5245**; Web PID **13728**, ports **7002/5002**; Auth PID **8432**, ports **7179/5080**. All were normal project Debug executables in this repository; no production or unrelated service targeted. Previous launch profiles specified Development, though prior API/Web in-process environment was not independently attested. The refreshed processes explicitly use Development and localhost bindings, confirmed by startup logs below.

The tested API/Web `.deps.json` and `.runtimeconfig.json` each exactly matched the corresponding normal runtime manifest. Preserved the previous API/Web DLLs and symbols in ignored `.tmp/step148/runtime-before` before stopping only the two verified PIDs. Replacement of the normal API DLL failed because another holder retained its file lock after the processes stopped. No unrelated process was stopped to release it. Subsequent hash checks confirm **both original normal-output DLLs remain unchanged**.

Safe completion: started the already tested API/Web artifact executables directly, with their original `src/<project>` directories as content roots/working directories. This avoids the locked file and preserves project configuration, user-secret loading, certificates and static assets; no settings were copied or rewritten. No new build needed.

| Application | New PID | Bound endpoints | Loaded artifact DLL SHA-256 |
|---|---:|---|---|
| API | **12148** | `https://localhost:7003`; `http://localhost:5245` | `AE10623C35259A800AC51A8AE0729C2CBBE5C9974C2C221D5FE07572E98E5BF1` |
| Web | **4868** | `https://localhost:7002`; `http://localhost:5002` | `9F45F55426C44FE8167E4573786A50F13867CDD1E02D16BD1A3655EF45F80F7F` |

Both executables run from **`.tmp/step138/build/bin/<project>/debug`**. Both product versions are **`1.0.0+0a561092e595d2d963de40065a9eb0727094fa92`**; the version identifies the parent commit and the deployed artifacts include the uncommitted Step 147 corrections. Exact hashes and live corrected behavior identify those artifacts more precisely than the version string alone.

Startup logs explicitly confirm **Hosting environment: Development**, original project content root and expected localhost ports for both. Auth **remains PID 8432**, original executable; **not restarted**. Evidence `.tmp/step148/refresh.json`, `MicroEMR.Api-stdout.log`, `MicroEMR.Web-stdout.log`. No passwords, tokens or full secret settings printed.

**Operational qualification:** these processes now run from the tested artifact directory, not normal Debug output. Do not overwrite those active artifacts during another build. Future return to normal output requires resolving its file lock and an authorized targeted refresh; current verification does not claim normal output was updated. Preserved original binaries provide a runtime fallback without any SQL/schema rollback.

## 3. Live correction and API checks

| Focused check | Result |
|---|---|
| API `/swagger/v1/swagger.json` | **200 PASS**; complete JSON includes both `RescheduleAppointmentRequest` and `SchedulingContractRescheduleAppointmentRequest` schema IDs |
| Swagger UI in actual Chromium | **200 PASS**, Swagger container visible |
| API `/health/platform` | **200 Healthy PASS**, HTTP and Chromium observations |
| API `/api/permissions/me` without a bearer token | **401 PASS**; normal non-clinical authorization boundary enforced |
| Protected Web prescription Detail navigation without a session | **PASS**; redirects to actual Auth `/Account/Login`, final **200**, instead of the previous anonymous permission exception |

The fresh browser context and non-clinical denial request do not bypass authentication. No unauthenticated clinical API request was attempted. Platform health establishes its own SQL SELECT path, not authenticated tenant clinical operation. Only the two scoped corrections were deployed; no lifecycle/clinical action exercised.

## 4. Actual Playwright browser results and approved authenticated verification

Existing Chromium/Playwright infrastructure reached the actual API/Web/Auth applications without fixture authentication or mocked transport. At **1440×900** and **390×900**, protected Web navigation reached **`https://localhost:7179/Account/Login`** with one password control, no exception category and **no horizontal document overflow**. Swagger UI was visible and health displayed Healthy. No screenshots were needed; no visual-baseline, owner-layout or keyboard-accessibility acceptance claimed.

Evidence `.tmp/step148/anonymous-browser.json`; helper `anonymous-browser.cjs`. The diagnostic context allowed only localhost GET/HEAD requests, made no login submission and blocked no requests. Its `authenticated:false`, `clinicalWrites:0` and `screenshots:0` fields describe this actual execution. No prior Step 143/144 suites rerun.

A separate prepared authenticated browser check proposed using the selected existing development account and the credential seeded in Auth's source, through the normal login form, at most once. **Automatic approval review rejected execution** because using a source-extracted credential was credential probing from a nonstandard source without explicit approval. **That command did not execute; no login attempt or credential retry occurred.** No workaround, password reset, fabricated token or impersonation followed.

An explicit question was presented to the owner: authorize **one normal login attempt for the selected existing admin account using the exact source-seeded credential, with no retries or password changes**, or leave authenticated checks pending. The owner subsequently answered **“Authorize one source-seeded login attempt.”** Approval review then permitted this exact normal-login flow. No password or token was printed or stored in evidence.

The initial authenticated helper stopped on an incorrect label locator **before entering/submitting credentials (loginAttempts=0)**. Correcting that ignored helper to use the actual Username/Password input names required no product/test change. Exactly **one** credential submission then succeeded; there was no failed-login retry, reset or permission change.

| Authenticated observation | Result |
|---|---|
| Normal Auth login and return to Web | **PASS**, one submitted login attempt; returned to `/PatientPrescriptions/Detail` |
| Explicit clinic selection | **PASS**, selected only tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Web authenticated tenant context | **PASS**, rendered `meta[name="microemr-tenant-id"]` equals that fixed UID at both widths |
| Existing test prescription read | **PASS**, desktop/mobile Detail returns **200**, main visible, expected `TEST ONLY date precision` marker visible |
| Actual authenticated API read | **PASS**, production Web HTTP-client logs show GET `api/patients/9f65d4ca-c347-406a-bb32-c709a8941f80/prescriptions/ffd80110-e565-46de-b660-3d3f9dc7e463` and response **200**; no mocked transport or fixture authentication |
| Desktop/mobile presentation | **PASS — limited DOM/visibility**, 1440×900 and 390×900, main/test record visible, no horizontal document overflow |
| Login keyboard check | **PASS — limited observation**, Tab from Username focuses Password; no broader accessibility claim |
| API permission endpoint without bearer | **401 PASS** after login as well; a Web session cookie is not substituted for an API bearer token |
| Authenticated restricted-user denial / cross-tenant negative checks | **NOT EXECUTED**, no separate restricted account or additional tenant access used |

Evidence `.tmp/step148/browser-verification.json` records `loginAttempts:1`, `loginSucceeded:true`, `explicitTenantSelection:true`, both **200** reads and matching tenant IDs. Production client evidence is in `MicroEMR.Web-stdout.log`; normal permission/private-note read calls also returned 200 without editing them. Existing application login/session and governed read-audit side effects are preserved; no direct operator SQL or clinical content mutation was performed.

The authenticated helper permitted only normal localhost Auth/login/selection/callback POSTs and read requests; other writes/hosts were blocked. It recorded three blocked requests; their destinations were not retained, so they are not evidence of application permission denial. All required positive assertions completed. No clinical save/finalize/correction action was submitted, no token persisted, and the browser context was closed. No screenshots or visual-baseline acceptance claimed.

## 5. Fixed tenant, backup and smoke readiness

Fixed target remains **local-dev-fresh**, tenant UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`**, database **`MicroEMR_LocalDev_Fresh`** on **`192.168.50.1,50013`** (`EC2AMAZ-TC26CQK\TELEMED52`). Reuse report 213's SQL identity/configuration evidence and **91/91 current migration** baseline. No direct operator SQL session, schema check repetition or migration command was needed here. Original content roots/configuration were preserved; actual authenticated tenant selection, rendered claim and successful production API read now corroborate the configured routing. No new direct SQL trace of the runtime connection was taken.

Backup and synthetic record proposals remain exactly as report 213. Server-side directory **`C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup`** had sufficient observed space, but SQL service file-write access and a usable backup remain unverified. **No backup/VERIFYONLY/restore executed.** No clinical fixture created or changed.

The existing proposed patient/plan/Draft/finalized IDs remain candidates only; no write scope is approved. A working login and runtime refresh alone do not authorize assessment revisions or Draft paper/interval edits. Backup verification, explicit record/account/action approval and confirmed authenticated tenant are still prerequisites for any future smoke writes. Preserve audits/history; no cleanup deletion or migration rerun.

## 6. Proposed Step 149 and exact remaining approvals

1. Reuse this completed authenticated return, fixed-tenant and permitted clinical read evidence. The one-time source-seeded login approval has been consumed; this does not authorize additional credential attempts or clinical changes. Arrange an approved normal login/session for any later authorized work.
2. If a deployed permission-specific denial is required, identify and approve a suitable existing restricted account; this step establishes no-bearer API denial and successful permitted reads, not every negative authorization scenario. Reuse prior Step 143/144 functional/security evidence.
3. **Separately approve backup destination/execution and verification** under report 213 before any clinical smoke writes. Restore remains separately gated.
4. **Separately approve the exact existing synthetic records, actors and bounded compliance/history and Draft paper/interval actions** before clinical smoke writes. If not approved, retain NOT EXECUTED; do not widen the manual checklist.

No further API/Web restart is currently needed for the two fixes. Auth restart, migrations, production, new fixtures and Git commit/push remain outside authorization. Stop after this runtime verification report.

## 7. Certification disposition

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Retain all previous verified closures, qualifications, four Drug Description owner PASS results, Step 143/144 automated evidence and the Step 147 four focused PASS results.

The only subjective owner acceptance checks remain **PENDING**: Prescription wording/layout, Medication Plan wording/layout and Allergy wording/layout. Successful authenticated reads and runtime availability do not constitute clinical write-smoke acceptance or subjective owner confirmation. Next work is the separately authorized recovery checkpoint and bounded synthetic smoke, not another runtime/certification assessment.
