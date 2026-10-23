# Step 147 — PC07.01 API runtime diagnosis and smoke-test unblocking

2026-10-22. **Swagger generation collision reproduced and locally corrected. Separate anonymous Web permission-lookup failure identified and locally corrected. Four focused tests PASS; running services unchanged.** Platform health, Swagger UI and Auth discovery respond. Authenticated clinical acceptance remains unverified. PC07.01 **PARTIAL**; inventory **10 mandatory-gap IDs / three packages**, unchanged.

## 1. Git checkpoint and boundaries

Read AGENTS.md and reused [Step 145 report 212](212-step145-pc07-01-development-deployment-preflight.md) and [Step 146 report 213](213-step146-pc07-01-recovery-and-controlled-smoke-acceptance.md). No broad assessment or prior suite rerun.

Recorded initial branch/HEAD/status once: **`feature/step-144-pc07-01-integrated-acceptance`**, **`0a561092e595d2d963de40065a9eb0727094fa92`**. Existing untracked reports 211–213 and `tests/MicroEMR.Api.Tests/Step144IntegratedAcceptanceTests.cs` preserved. Before adding the focused reproduction test or editing source, created **`feature/step-147-api-swagger-runtime-fix`** locally. Initial branch creation lacked filesystem permission; approved escalation succeeded without changing existing files. No reset, clean, stash, staging, commit, merge or push.

No migrations, database writes, backup/restore, service restart or authentication bypass. Ignored `.tmp/step147` contains focused build/test and sanitized read-only runtime evidence. No credentials/tokens printed. Localhost metadata/browser requests ignored the local HTTPS certificate validation error explicitly; this is not TLS trust verification and changed no application settings.

Automatic approval review rejected a proposed **unauthenticated clinical GET** as potentially exposing patient data and conflicting with the authentication boundary. That command did not execute. It was omitted; subsequent API checks used non-clinical endpoints only. Normal protected Web navigation reached authorization failure before the clinical action. No workaround or unauthenticated clinical API call followed.

## 2. API failure boundary and cause

Read-only checks against the existing running API:

| Request | Result | Interpretation |
|---|---|---|
| `https://localhost:7003/swagger/index.html` | **200**, Chromium Swagger container visible | UI assets work |
| `https://localhost:7003/swagger/v1/swagger.json` | **500**, safe generic ProblemDetails | Document generation fails; response does not expose its inner exception |
| `https://localhost:7003/health/platform` | **200 Healthy**, HTTP and Chromium observations | API started and platform SQL health dependency works |
| `https://localhost:7179/.well-known/openid-configuration` | **200** | Auth discovery available; issuer `https://localhost:7179/` retained from Step 146 |

A focused test discovers the production API controllers and generates the complete Swagger document using MVC/Swagger services **without opening a database or starting the application**. Before correction it failed with:

> Failed to generate Operation for SchedulingController.RescheduleAppointment; schema ID RescheduleAppointmentRequest already used.

The conflicting types are **`MicroEMR.Application.Scheduling.DTOs.RescheduleAppointmentRequest`** and **`MicroEMR.Application.Scheduling.Contracts.RescheduleAppointmentRequest`**. Default simple-name schema IDs collide. Evidence `.tmp/step147/swagger-before.trx` and `swagger-before.log` contains the nested SwaggerGeneratorException/InvalidOperationException identifying both types.

This establishes a reproducible source-level cause matching the live Swagger JSON failure. The live inner exception was not captured directly: SafeApiExceptionHandler intentionally returns safe ProblemDetails and logs only structured error categories. No verbose exception setting was enabled. In Program.cs, Swagger middleware precedes authentication and tenant resolution; the isolated failure needs neither. It is not evidence of API startup failure, clinical database failure or a particular authenticated clinical endpoint failing. A successful platform health check proves its SELECT 1 path, not every repository, tenant database or DI dependency.

## 3. Runtime context and actual API operational status

Existing API process **PID 3184**, executable `src/MicroEMR.Api/bin/Debug/net10.0/MicroEMR.Api.exe`, remains running. Its adjacent disk DLL metadata remains file version **1.0.0.0**, product version **`1.0.0+e1adc3f73bebe3e3dd949e197385149d54021f2a`**, SHA-256 **`DC6DB3B4E406ED437197307A48142E04E865E64732FD8A690DB41A3F6BD4AE10`**. This matches Step 146's normal-output artifact. No process reload or file replacement occurred. Disk metadata is not exact loaded-memory/source attestation; builds can include uncommitted changes.

Endpoint baseline retained: API HTTPS **7003** / HTTP **5245**, Web HTTPS **7002** / HTTP **5002**, Auth HTTPS **7179** / HTTP **5080**. Web configuration targets those API/Auth HTTPS endpoints. API launch profiles configure Development; actual API process environment was not independently exposed by an approved diagnostic endpoint. Swagger is unconditional and does not establish environment. Web serves a DeveloperExceptionPage, consistent with Development; no environment setting changed.

Fixed target remains **local-dev-fresh**, tenant UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`**, database **`MicroEMR_LocalDev_Fresh`**, endpoint **`192.168.50.1,50013`**, actual SQL instance **`EC2AMAZ-TC26CQK\TELEMED52`**. Reuse Step 145/146's identity/configuration checks and **91/91 migration** evidence; no database connection or new SQL inspection was needed in this step. **No migration rerun.** Configured tenant routing is established; actual authenticated claim/membership routing is **not yet verified**.

Step 143 running-feature evidence remains bounded: the Web's served compliance script source match and disk property markers from Step 146 are retained. The corrected Swagger test includes paper-requirement and compliance schemas. This verifies the locally built API source, not the still-running API's full feature contract. No representative authenticated clinical GET succeeded in this step; no authenticated session was established. Consequently **API operational status is partial: process/UI/platform health work, live Swagger generation fails, and authenticated clinical operation remains unverified**.

## 4. Actual browser readiness and separate Web failure

Existing Playwright Chromium was launched against the actual localhost applications using a fresh context. Only GET/HEAD to localhost/127.0.0.1 were permitted; external hosts and write requests were blocked by the diagnostic harness. No fixture transport or authentication substituted for the running applications.

At desktop **1440×900** and mobile **390×900**, normal navigation to the existing protected **PatientPrescriptions/Detail** route returned **500**, before a login control or clinical view appeared. Sanitized stack methods identify:

`AccessProfileApiClient.EffectivePermissionsAsync → WebPermissionService.HasAsync → WebPermissionHandler.HandleRequirementAsync → AuthorizationMiddleware`.

The exception is **UnauthorizedAccessException: Attempted to perform an unauthorized operation.** Source inspection confirms the permission handler calls the permission API client even when the principal is anonymous; that client throws when no access token exists. Authorization handlers can run even while the authenticated-user requirement is unsatisfied. This failure occurs before the clinical controller action and is separate from Swagger generation. It is not proof of incorrect tenant membership or a denied clinical actor.

The diagnostic script initially used an ambiguous Swagger selector, then corrected it. Final observations are in `.tmp/step147/browser-readiness.json`; helper `.tmp/step147/browser-readiness.cjs`. Later repetitions only narrowed status/exception categories and stack method names; they did not log in or exercise clinical workflows. Both widths reach the same permission-layer error. The mobile developer-error page overflows; this is **not** a clinical-layout acceptance failure or a clinical DOM/keyboard PASS. No screenshots, visual-baseline comparison or accessibility acceptance claimed.

**Browser reachability PASS; authenticated clinical browser readiness BLOCKED pending updated runtime and normal login.** Do not reuse the isolated Step 144 fixture authentication against this tenant, invent a token, impersonate a user, guess credentials or change permissions.

## 5. Targeted local corrections and verification

| File changed | Exact change |
|---|---|
| `src/MicroEMR.Api/ApiSwaggerConfiguration.cs` (new) | Shares actual production Swagger configuration with its generation test; gives only the conflicting Scheduling.Contracts type the distinct schema ID **SchedulingContractRescheduleAppointmentRequest**. Default IDs for all other types and existing document metadata retained |
| `src/MicroEMR.Api/Program.cs` | Registers that shared Swagger configuration in place of the inline registration |
| `src/MicroEMR.Web/Authorization/PermissionAuthorization.cs` | Returns from permission handling for an unauthenticated principal, leaving requirements unsatisfied so the existing authorization system can challenge normally; no permission granted and no exception swallowing for authenticated users |
| `tests/MicroEMR.Api.Tests/Step147SwaggerTests.cs` (new) | Complete production-controller Swagger generation and distinct schema checks, Step 143 property checks, plus anonymous/no-lookup, authenticated-denied and authenticated-granted cases |
| This report | Diagnosis, evidence and remaining gates |

No business DTO, controller action, clinical lifecycle, SQL, audit or tenant logic changed. The Swagger schema-name change affects documentation references for one type, not JSON request fields or routes. Generated clients depending on the former ambiguous schema name may require regeneration; previously the complete document could not generate.

Focused results:

- **Before:** one new generation test **FAIL**, reproducing the collision without database access.
- **After Swagger correction:** generation test **PASS**, complete document including both scheduling schemas and Step 143 paper/compliance fields.
- **Final after Web guard:** **4/4 PASS**, zero skipped, `.tmp/step147/step147-final.trx`. Anonymous makes zero permission lookups; authenticated denied/granted cases each make one lookup and preserve the expected result.
- Affected test project/dependencies built to existing isolated `.tmp/step138/build` outputs, never the running Debug paths. Incremental builds were needed for reproduction, correction and the newly established Web guard; final build **PASS**, zero errors, **six existing NU1900 vulnerability-metadata warnings**. No full solution build or previous Step 143/144 suite rerun.
- Tracked diff/whitespace reviewed, new files reviewed; no unrelated changes. Prior passing evidence retained.

**These fixes are local and safely tested, not deployed.** The running API/Web still use their existing artifacts; do not claim live Swagger or login repaired. Auth requires no restart for these changes.

## 6. Remaining approvals and proposed Step 148

**Stop before restarting services.** Owner must explicitly authorize a targeted API/Web build/deployment to their normal runtime outputs and minimal stop/start (or approve the exact validated artifact deployment procedure). This is necessary to load the changed Swagger registration and permission handler; an isolated successful build cannot update the existing processes. No broad service restart or Auth restart is justified. Recheck environment and fixed tenant after refresh; then repeat only Swagger metadata, normal login and a representative authorized read-only request.

Keep report 213's server-side backup proposal unchanged: **`C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup`**, unique COPY_ONLY/CHECKSUM backup with VERIFYONLY/HEADERONLY. It exists and had ample capacity; SQL service file-write access and a usable backup remain unverified. **Backup NOT EXECUTED.** Destination/execution still need explicit approval; no restore authorized.

Retain these existing synthetic candidates, **none approved for writes**:

- Patient `9f65d4ca-c347-406a-bb32-c709a8941f80`.
- Active medication plan `2521d27e-172f-47a1-9326-72eea8b0f571` — owner must designate it for bounded synthetic assessment/history testing.
- Test-marked Draft `ffd80110-e565-46de-b660-3d3f9dc7e463`, original prescriber clinical user ID 2; test-marked finalized original `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8` for read-only preservation.

Clinical writes still require verified backup, confirmed runtime/tenant, explicit record IDs/action scope and an existing authorized account. Approval to restart or perform authenticated reads does not authorize clinical writes. No new fixtures, permission grants, finalize/correction, cleanup deletion, database restore or migration execution is implied.

**Proposed Step 148:** explicitly authorized targeted API/Web refresh, then minimal read-only Swagger/Auth/login/tenant/clinical-read confirmation; execute the retained backup and bounded real-tenant smoke only if separately approved and all report 213 gates pass. Stop on wrong context, failed backup, missing permission or concurrency conflict. Do not reopen certification analysis or rerun passing suites.

## 7. Certification disposition

**PC07.01 PARTIAL. Inventory before → after: 10 mandatory-gap IDs / three packages → 10 mandatory-gap IDs / three packages.** Preserve all prior verified closures, qualifications, four Drug Description owner PASS observations and Step 143/144 automated evidence. Existing-tenant smoke remains **NOT EXECUTED**. The only subjective owner checks remain **PENDING**: Prescription wording/layout, Medication Plan wording/layout and Allergy wording/layout. Technical diagnosis/local test success does not establish owner acceptance or deployed clinical verification.
