# Product Improvement 009 — Clinic Branding and Reusable Print Header

2026-10-31 (America/Toronto). **Step 01 implemented; local focused verification PASS. Not persistently migrated, deployed or runtime accepted.** Branch at start: `main`. Git status inspected once; initial working tree was clean. No branch or Git state change was made. Product Improvements 007/008 and Scheduling source remain untouched.

## Existing infrastructure and implementation boundary

Reused the printing findings in Assessment 006. Clinic Settings already owns LegalName, address, phone, fax, email, operational defaults, actor/time and optimistic concurrency through `ClinicProfile_Get/Save`. The platform catalog supplies ClinicName/TimeZoneId. These fields and their existing save path are retained without duplication. The header prefers LegalName and falls back to the catalog ClinicName; changing the platform display name remains an existing platform operation.

Existing shared clinical HTML layouts and Playwright/Chromium PDF rendering remain intact. Prescriptions and browser printouts have separate layouts. Patient-file storage uses private rooted keys and guarded local paths; finalized artifacts already have their own ownership/audit/storage semantics. This step does not place branding in patient-file records or change those artifacts.

**First clinical print-path integration: none.** Added an authenticated, nonclinical header preview and a focused HTML/PDF demonstration, as allowed by the Step 01 boundary. Wiring a current branding lookup into the existing shared renderer would also affect several finalization paths; that broader integration is reserved for Step 02. No prescription/referral/consultation content, signing, numbering, snapshot, final artifact read/write or PDF storage behavior changed.

## Branding and logo storage

New forward migration source **0104-clinic-branding-logo** adds only:

- `dbo.ClinicBrandingLogo`: one singleton per tenant database, bounded canonical PNG bytes, Width/Height, UpdatedAtUtc/UpdatedBy and RowVersion.
- `dbo.ClinicBrandingLogo_Get` and `dbo.ClinicBrandingLogo_Save`.
- One entry in the existing tenant migration manifest.

The bounded image is stored privately inside its tenant database rather than in a public directory or a patient-file path. This uses the existing clinic configuration conventions: verified tenant connection factories, stored-procedure writes, administrative write capability, row-version checks and transactional configuration auditing. For a maximum 1 MiB image this avoids file/metadata orphan handling and makes image replacement/removal and audit atomic. No storage root, filename, client-supplied key, remote URL or tenant selector is exposed.

Replacement updates the singleton with a current version. Removal clears bytes/dimensions while retaining the row and its new version, preventing stale create/update requests from bypassing concurrency. Configuration audit records retain old/new dimensions and SHA256 digests, not the uploaded bytes; removal does not delete audit history. `PatientId` is not populated. Failed audits roll back the image change. No logo-version archive is introduced; historical signed PDFs continue using their stored original bytes.

The repository uses `ITenantSqlConnectionFactory` for reads and the existing `IAdministrativeTenantSqlConnectionFactory` for writes, exactly as ClinicProfile does. No shared global logo row exists. No existing migration, stored procedure or SQL permission was modified. Migration 0104 was applied only to disposable test databases.

## Administration and authorization

Location: **Clinic Administration → Clinic Settings → Clinic Branding & Printing**, route `/ClinicConfiguration`.

- Current-logo preview with proportional bounds, or a text-only fallback message.
- Upload/replace PNG or JPEG using **Save Logo**.
- **Remove Logo** with the current branding version.
- **Preview Print Header**, opening the nonclinical demonstration in a separate tab.
- Existing name/contact/address fields and their Save action remain below this section. Logo and contact changes are explicitly saved separately, with independent version checks.

Used existing Bootstrap/server form conventions; no new client JavaScript or TypeScript was necessary. A failed contact validation/conflict reload also resolves current branding state. Branding state is excluded from MVC request model binding.

Both API and Web require authentication. API/Web logo mutations retain `ClinicSettings.Manage`; the Application service independently checks that effective permission, a nonempty authenticated tenant context and the resolved clinical actor before writing. Web POSTs require antiforgery tokens. Neither URLs nor upload models accept a tenant identifier as authorization. The service and tenant-verified connection factories resolve the current tenant internally.

Authorized `Patients.View` users, as well as clinic-settings managers, can retrieve their current tenant's logo/header without gaining settings-management access. API read handlers enforce this in the Application service. Managers alone can read the administration state endpoint.

| Endpoint | Purpose |
| --- | --- |
| API `GET /api/clinic-branding` | Manager-only has-logo/version state |
| API `GET /api/clinic-branding/logo` | Current tenant's canonical PNG; 404 if absent |
| API `POST /api/clinic-branding/logo` | Manager-only multipart upload or removal with row version |
| API `GET /api/clinic-branding/header` | Current tenant's nonclinical header demonstration |
| Web `POST /ClinicBranding/Save` | Antiforgery-protected logo form proxy |
| Web `GET /ClinicBranding/Logo` | Authenticated logo preview proxy |
| Web `GET /ClinicBranding/Preview` | Authorized nonclinical header preview proxy |

Image responses use `image/png`, `private, no-store` and `X-Content-Type-Options: nosniff`. Preview HTML is also private/no-store. API errors return fixed validation/denial/conflict/unavailable messages rather than exception details or paths; server failures use ILogger. Web proxies preserve authorization denial and return safe upstream-failure responses.

## Image validation

Input and canonical output are each limited to **1 MiB**, with **1–2048 pixels on each side**. Web/API multipart and total request limits bound uploads. The browser's accept filter is only guidance; the server ignores the supplied filename/extension and claimed MIME type.

The processor checks PNG signature/IHDR or JPEG SOF dimensions before decoding, rejects unsupported/excessive dimensions, then decodes with the already installed Playwright Chromium dependency. The isolated decoder blocks network requests/service workers and executes only fixed application code, never uploaded HTML or script. It verifies decoded dimensions and re-encodes pixels as PNG, dropping metadata and trailing payloads. Invalid/corrupt content is rejected. SVG, HTML, GIF and executable content are not accepted. No additional image or PDF package was introduced.

Normal baseline/progressive JPEG and PNG are the supported image inputs. An image whose canonical PNG exceeds 1 MiB must be resized before upload. Chromium installation remains a deployment prerequisite, now also for logo uploads.

## Reusable header architecture and demonstration

`ClinicBrandingService.GetPrintHeaderAsync` composes a `ClinicPrintHeader` from the existing clinic fields and the authenticated tenant's image. `ClinicPrintHeaderRenderer` exposes a reusable HTML fragment, print CSS and standalone demonstration document. It reuses `ClinicalPrintClinic` rather than defining duplicate contact fields.

Layout: proportional logo on the left, clinic identity/contact lines adjacent, subtle divider and encoded document title below. Letter page margins are 0.65 inch; Arial-compatible readable typography; logo maximum 110pt × 65pt with intrinsic aspect ratio and `object-fit: contain`. Empty optional contact/address lines are omitted; absent logo produces text-only output. Text/title are HTML encoded. Image bytes are embedded as `data:image/png;base64,...`; no external fetch or PDF network policy change is required.

Focused tests produced and inspected:

- `.tmp/product-improvement-009/header-demo.html`
- `.tmp/product-improvement-009/header-demo.pdf`
- `.tmp/product-improvement-009/header-demo.png`

The standalone demonstration contains synthetic clinic identity and a synthetic rectangle image only; it is not a clinical document or a signed artifact. PDF text/page checks and native-browser dimension checks passed, and the screenshot was visually reviewed. The demonstration files are ignored local evidence. The generating test also retains its originals under the test output's `.tmp/product-improvement-009/` directory.

## Focused tests and build

**37 PASS / 0 FAIL / 0 SKIPPED**, one focused run. Receipt: `.tmp/product-improvement-009/results/clinic-branding.trx`.

| Focused class | Passed | Evidence |
| --- | ---: | --- |
| ClinicBrandingTests | 9 | Administrator save/replace/remove and actor; clinical reads; permission/context denial; tenant-selected ownership; stale versions; invalid inputs; encoded complete/missing header fields; safe/private API response; API/Web permission policies and Web antiforgery |
| ClinicBrandingImageTests | 8 | Actual PNG/JPEG decode/canonicalization; trailing payload stripping; SVG/HTML/executable/GIF rejection; corrupt/oversized/dimension rejection; native proportional bounds and existing PDF renderer demonstration |
| ClinicBrandingWebTests | 4 | Authenticated multipart proxy/current-tenant routes; no filename/key/tenant forwarding; safe 403/502 responses; explicit upload/removal and existing-settings redirect |
| ClinicBrandingSqlTests | 1 | Two disposable LocalDB databases: tenant-private reads/writes, replacement/removal, concurrency, audit-failure rollback, configuration audit and unchanged existing contact profile |
| ClinicConfigurationFoundationTests | 3 | Existing singleton/permission/request-validation regression |
| ClinicConfigurationWebTests | 5 | Existing contact API, safe conflict, stale reload, save and permission regression |
| ClinicalPdfPreviewTests — selected | 4 | Existing no-branding PDF engine, encoded layout, patient/clinical context and signature layout |
| ConsultationFinalPdfTests — selected | 3 | Existing clinical/signature content plus final download/read audit; returned fixture bytes asserted exactly unchanged |

Reused `NextAvailableSqlDatabase` and its existing guarded random-database lifecycle, pointing only to `(localdb)\MSSQLLocalDB` with integrated security. It creates/drops its own two `MicroEMR_Step71_<random>` databases; no persistent tenant or production database connection was used. No appointment was created or changed by the branding tests. Existing tenant-factory identity/security behavior was reused rather than rebuilt. No certification/full solution suite ran.

**One affected build PASS**, 0 warnings/errors: existing test project and referenced Application/Infrastructure/API/Web/Razor outputs compiled to isolated `.tmp/product-improvement-009/artifacts`. The existing DatabaseTool reference compiled as part of that project graph; Auth/full solution were not built. No output was copied into running hosts. Cached packages restored with vulnerability-feed checks disabled for this offline restore only; no dependencies or project package versions changed. **TypeScript check: not applicable**, no client script added or modified.

```powershell
dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/product-improvement-009/artifacts --ignore-failed-sources -p:NuGetAudit=false
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/product-improvement-009/artifacts
$env:MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
try {
    dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-009/artifacts --filter 'FullyQualifiedName~ClinicBranding|FullyQualifiedName~ClinicConfiguration|FullyQualifiedName~ClinicalPdfPreviewTests.PrintLayout_|FullyQualifiedName~ClinicalPdfPreviewTests.PatientDocumentLayout_|FullyQualifiedName~ClinicalPdfPreviewTests.SignedEncounterLayout_|FullyQualifiedName~ClinicalPdfPreviewTests.PlaywrightRenderer_ProducesPdfBytes|FullyQualifiedName~ConsultationFinalPdfTests.FinalRenderer|FullyQualifiedName~ConsultationFinalPdfTests.FinalPdfRead' --logger 'trx;LogFileName=clinic-branding.trx' --results-directory .tmp/product-improvement-009/results
} finally { Remove-Item Env:MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION -ErrorAction SilentlyContinue }
```

Historical artifact preservation evidence is bounded: existing production-code final artifact paths/layouts were not changed, and focused tests return an existing finalized PDF fixture byte-for-byte. No persistent signed/archived artifact was opened, rewritten, regenerated or claimed to have undergone live hash comparison.

## Files changed

| Area | Files |
| --- | --- |
| Migration source | `db/tenant-clinical/migrations/0104-clinic-branding-logo.sql`; `db/tenant-clinical/manifest.json` |
| Application | `src/MicroEMR.Application/ClinicConfiguration/ClinicBrandingService.cs`; `src/MicroEMR.Application/ClinicalOutput/ClinicPrintHeaderRenderer.cs`; `src/MicroEMR.Application/DependencyInjection.cs` |
| Infrastructure | `src/MicroEMR.Infrastructure/ClinicConfiguration/ClinicLogoRepository.cs`; `src/MicroEMR.Infrastructure/ClinicConfiguration/ClinicLogoProcessor.cs`; `src/MicroEMR.Infrastructure/DependencyInjection.cs` |
| API | `src/MicroEMR.Api/Controllers/ClinicBrandingController.cs` |
| Web | `src/MicroEMR.Web/Controllers/ClinicBrandingController.cs`; `src/MicroEMR.Web/Services/ClinicConfiguration/ClinicBrandingApiClient.cs`; `src/MicroEMR.Web/Controllers/ClinicConfigurationController.cs`; `src/MicroEMR.Web/Models/ClinicConfiguration/ClinicConfigurationViewModel.cs`; `src/MicroEMR.Web/Views/ClinicConfiguration/Index.cshtml`; `src/MicroEMR.Web/Program.cs` |
| Tests | `tests/MicroEMR.Api.Tests/ClinicBrandingTests.cs`; `ClinicBrandingImageTests.cs`; `ClinicBrandingSqlTests.cs`; `ClinicBrandingWebTests.cs`; `ClinicConfigurationWebTests.cs`; `ConsultationFinalPdfTests.cs` (all under the same test directory) |
| Documentation | `docs/product-readiness/009-clinic-branding-print-header.md` |

Final diff review was confined to these changes. No Scheduling, 007/008 source/report, existing migration, authentication configuration or clinical generator was modified.

## Deployment prerequisites and manual acceptance

**Separate owner authorization is required before persistent migration/deployment/restarts or runtime branding writes.** This implementation did none of them.

For a future authorized rollout:

1. Apply source migration 0104 through existing tenant provisioning/migration procedures to each approved tenant. Preserve prior migration history. Deploy compatible API/Web artifacts together. Settings UI expects the new logo endpoint/schema; coordinate migration before deployment.
2. Verify runtime EXECUTE access to `ClinicBrandingLogo_Get` and administrative-write access to `ClinicBrandingLogo_Save` through existing provisioning. Any new grant requires separate authorization; no grant is bundled or applied here. Existing current-tenant assignment/secret/identity checks must remain in force.
3. Ensure the existing matching Playwright Chromium installation is available to the API process, including upload decoding. Preserve current configuration/secrets and rollback artifacts. No new storage-directory configuration is needed because logos are stored in the tenant database.
4. Capture the approved tenant's existing logo/contact values and restoration direction. With no existing logo, use the implemented Remove Logo operation after testing if restoration is required; retain configuration audit history.
5. Sign in normally as an authorized clinic administrator; open Clinic Settings. Confirm existing contact values and initial fallback/current logo. Save contact information through the existing form; preview must use LegalName or ClinicName fallback and the same address/phone/fax/email.
6. Upload a valid PNG, preview, reload, then replace with JPEG and verify the new image. Check proportional scaling in both screen preview and print-to-PDF. Try renamed SVG/HTML, malformed PNG, >1 MiB and >2048-pixel images; all must fail safely without replacing the saved logo. Canonical PNG output must also fit 1 MiB.
7. Use two settings sessions: save/replace in one, then submit the stale version in the other. Expect conflict feedback and the latest state on reload. Remove the logo and verify text-only header fallback; restore the approved original logo when needed.
8. Under an existing `Patients.View` identity without `ClinicSettings.Manage`, open `/ClinicBranding/Preview` or the authorized API header route for its tenant. Reads should work; upload/remove must be denied. Verify unauthenticated access is rejected and missing antiforgery tokens cannot mutate Web settings.
9. Using two existing approved tenant fixtures, confirm different logo/contact identities. Extra tenant identifiers in requests must not change the authenticated tenant selection; Tenant A must not read or modify Tenant B's logo. Do not create users, memberships or permissions to make a test pass.
10. Compare already approved archived/signed artifact hashes before/after that future rollout where available, verify existing clinical print paths still behave unchanged, and record runtime results honestly. Those print paths will retain their existing layout in Step 01; only the new header preview demonstrates branding.

## Remaining phases

Persistent migration, deployment and live administration/tenant acceptance are **pending**, not runtime PASS. Step 02 may later opt suitable new previews/summaries into this header, then address common finalization paths, independent browser layouts and the separately versioned prescription snapshot/rendering boundary. Any integration must preserve clinical content, immutable historical artifacts, signing/numbering/read-audit policies and original snapshot provenance. No bulk regeneration is authorized by this work.

**STOPPED after implementation, focused tests, build and documentation.** No persistent migration, SQL grant, deployment, service restart, commit, remote Git operation or Step 02 implementation occurred.

## Owner-applied migration — read-only readiness checkpoint

2026-10-31, **16:09 America/Toronto / 20:09 UTC**. After the owner reported applying 0104 and asked about testing, performed targeted read-only readiness checks. Reused the 37 passing tests and existing build; no test suite or build was repeated.

Verified configured connections and actual database identity for **EC2AMAZ-TC26CQK\TELEMED52 / MicroEMR_LocalDev_Fresh / local-dev-fresh**, tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`. New logo table and both procedures exist; installed procedure definitions match migration 0104 source. The logo table currently has **zero rows / no configured logo**. This confirms object readiness, not a complete migration-ledger certification.

| Prerequisite | Observed result |
| --- | --- |
| Runtime principal `microemr_dev` | Effective EXECUTE on `dbo.ClinicBrandingLogo_Get`: **false** |
| Configured administrative writer `microemr_admin_dev` | Effective EXECUTE on `dbo.ClinicBrandingLogo_Save`: **false** |
| Running API | PID 5812; 008 hash `D1AF76A96FDC97FA01962E7FA8402CAB421928E22C1363C7D9B5F3E8C0E30BB6`, rather than built 009 hash `798DCDEF2B79422C5BCF6E0E9657084D85B2A6AE362797EFF1ABDEBC2DBC33E9` |
| Running Web | PID 18684; 008 hash `AE1DFA45AEEB6AA66F82F4A1966FEB5A66B345576D819004440D7BBBB423D6BD`, rather than built 009 hash `72C51D1645CFF7718F499C0422D28C8B7FFFF125643D85E03E570ABC36C1688F` |

**Runtime acceptance remains pending.** Next requires separate authorization for compatible development API/Web deployment and necessary restarts, provisioning precisely the two missing object EXECUTE permissions through existing conventions, and temporary logo upload/replacement/removal tests in the approved tenant. No schema-wide grants are proposed. Preserve the existing no-logo state by using the implemented Remove Logo operation after testing; audit history remains intact. Contact writes are not necessary for logo acceptance and require explicit inclusion if desired.

No migration, GRANT, stored-procedure save, logo/contact mutation, deployment, service restart, clinical operation or Git state change was performed. Only this report and ignored read-only helpers/evidence changed. Evidence: `.tmp/product-improvement-009/readiness.json`, `runtime-readiness.json`, `writer-readiness.json`; guarded helpers under `.tmp/status-acceptance-preparation/009*-readonly.fsx`.

## Step 01B — Development Deployment and Runtime Acceptance

2026-10-31 (America/Toronto). The owner subsequently authorized the development rollout, narrowly scoped procedure grants and temporary branding acceptance writes, with original-state restoration. This section supersedes the preceding pending-deployment checkpoint while preserving its evidence.

**Final classification: IMPLEMENTED — DEPLOYED, PARTIALLY VERIFIED.** Development 009 is healthy and the required logo/PDF workflows passed. Non-administrator clinical-only printing and second-tenant runtime verification remain deferred; the permission probe and unavailable historical-file limitations below are not reported as full PASS.

### Preflight and migration result

Inspected Git status once. Branch remains `main`; existing uncommitted Step 01/owner changes were preserved. Reused the 37 passing tests and verified compatible builds, without another build or test-suite run. Only this report and ignored rollout helpers/evidence changed in Step 01B; no application source correction was made.

Verified Development HTTPS profiles and actual configured catalog/tenant mapping: platform `MicroEMR_Platform` on the established development SQL endpoint; active `local-dev-fresh` maps through server key `local-sql` and secret reference `development:MicroEMR_LocalDev_Fresh` to **EC2AMAZ-TC26CQK\TELEMED52 / MicroEMR_LocalDev_Fresh**, tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`. Verified database-reported tenant identity, real read/write logins and active administrator clinical UserId 2.

**Migration 0104 was already applied by the owner; it was not reapplied.** Ledger entry/version `0104-clinic-branding-logo / 1.0.0` and canonical source hash matched:

`CCAAB43C90C8B6C023ED774899BFD936CC286052A57EE7D7C8830CD0DC064FC1`

Verified the seven-column logo contract, enabled/trusted singleton/image checks and both exact procedure definitions. Prior 0028/0103 ledger prerequisites were present. No ledger/schema disagreement, migration repair, additional SQL object or other-tenant migration occurred.

The existing Chromium dependency launched and generated a PDF before grants/deployment: **Chromium 149.0.7827.55**, existing Playwright revision 1228. Runtime uploads subsequently exercised the API's native decode/re-encode process successfully. No package installation or host configuration change occurred.

### Exact SQL permissions

At **20:16:43 UTC / 16:16:43 America/Toronto**, applied only these missing grants in the verified tenant, together in one transaction:

```sql
GRANT EXECUTE ON OBJECT::dbo.ClinicBrandingLogo_Get TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.ClinicBrandingLogo_Save TO [microemr_admin_dev];
```

The real configured runtime reader is `microemr_dev`; the separate existing administrative writer is `microemr_admin_dev`. Effective Get/Save access verified through those respective connections changed **false/false → true/true**. No explicit direct DENY was overridden. No table permission, schema/database-wide EXECUTE grant, role change, unrelated procedure or authentication setting changed. Grants remain installed.

### Deployment identity, services and rollback

Before grants, preserved and hash-verified **14** prior 008 DLL/PDB/static-asset metadata files in `.tmp/product-improvement-009/rollout/previous-artifacts/`. Prepared the established copy/restart rollback process against those exact files. Configuration/secrets and Auth start identity were checked. A preflight asset byte-hash difference was solely CRLF normalization: current `resource-defaults.js` matches the previously verified 008R served source exactly after newline normalization. Preserved the owner's current bytes and recorded their SHA256 `9A25C2D1EA1B317D5F32926FBEBB710178057DBBA5CBF6B579A67DB8B0C126B0`; all other inspected 007/008 assets matched their prior hashes.

Performed **one** controlled deployment at **20:16:54 UTC / 16:16:54 America/Toronto** using existing verified Step 01 output. No rebuild was necessary and no new static client assets exist in 009. Deployed compatible API/Web plus affected Application/Infrastructure dependencies and Web static-asset metadata, preserving current configuration and source-root assets.

| Component | Deployed identity / SHA256 |
| --- | --- |
| API | PID **7052**, DLL `798DCDEF2B79422C5BCF6E0E9657084D85B2A6AE362797EFF1ABDEBC2DBC33E9` |
| Web | PID **27904**, DLL `72C51D1645CFF7718F499C0422D28C8B7FFFF125643D85E03E570ABC36C1688F` |
| Application, both hosts | `FF579E079C131FCC6D4FABBAF1E5A0CDD7FCA28A6A85CCD5698C07467CDC2D00` |
| Infrastructure, both hosts | `2524252ED426F957D49845EE11550E4719616CD35123C3F2044D66B81829CEF2` |
| Auth | PID **14628**, original start identity unchanged; not restarted |

Only API/Web were restarted, from previous PIDs 5812/18684. Loaded module paths/hashes, Development startup/content roots/listeners and empty startup stderr were verified. API `/health/platform` returned **200**; unauthenticated Web Clinic Settings returned normal **302**. Final health at **20:33:22 UTC / 16:33:22 America/Toronto** remained healthy. All 14 deployed and 14 rollback file hashes, configuration hashes, Auth identity and preserved Scheduling asset hashes matched.

**Rollback readiness: verified; rollback performed: NO.** Application rollback restores the verified 008 files and restarts only API/Web. Database recovery for this rollout is non-destructive: leave the already-applied additive 0104 objects and narrowly authorized grants intact, and restore test branding through the approved UI. 008 does not depend on the new logo objects. No destructive SQL rollback, reverse migration, direct SQL cleanup or database restore was executed. A broader database restore would require the owner's separately approved backup/recovery process; this task does not claim a newly created or restore-tested full database backup.

### Runtime acceptance A–L

Used the established Chrome/Playwright environment, DayPilot observer and request gates. Normal sign-in was completed by the owner for admin@microemr.local and the existing rx.tenantb@example.invalid identity, selecting local-dev-fresh. Only approved branding POSTs and authentication flow were permitted; appointment, preference, contact-save and other mutation routes were blocked. No blocked appointment/preference request actually occurred.

| Scenario | Result | Actual evidence |
| --- | --- | --- |
| A — Branding screen | **PASS** | Clinic Settings returned 200; all current legal name/address/phone/fax/email values matched the SQL baseline; upload controls enabled; no initial logo and no uncaught JavaScript error. |
| B — Valid upload | **PASS** | Disposable 240×80 PNG uploaded through UI. Canonical PNG returned 200 with private/no-store and nosniff; appended safe test trailer was absent. SQL digest matched downloaded bytes and actor UserId 2 in the approved tenant. |
| C — Replacement | **PASS** | Second disposable JPEG accepted, stored/served as a new canonical PNG with a different digest. Existing contacts and baseline integrity hashes unchanged. Other-tenant runtime comparison remains J deferred. |
| D — Removal | **PASS** | Approved Remove Logo UI returned success; logo GET returned 404; text fallback and unchanged contact profile confirmed. |
| E — Invalid content | **PASS** | Safe SVG text named `.png` rejected with validation feedback. Existing PNG, row version and audit count unchanged; no unsafe content stored. |
| F — Oversize | **PASS, helper qualification below** | 1,048,577-byte safe fixture rejected with Web HTTP 400. Read-only snapshot confirmed previous logo/version/audit unchanged. |
| G — PDF with logo | **PASS** | Deployed `/ClinicBranding/Preview` returned tenant header HTML with correct clinic/contact data. Existing headless Chromium printed that HTML to a demonstration PDF, with network blocked and JS disabled; 240×80 logo rendered proportionally at approximately 146.66×48.88 CSS pixels. PDF is one Letter page, 612×792 points; clinic/title text verified; screenshot visually reviewed without clipping/distortion. |
| H — PDF without logo | **PASS** | Removed G test logo through UI; deployed header produced a readable text-only one-page Letter PDF. Correct clinic/title text verified; original no-logo state restored. |
| I — Permissions | **DEFERRED / partial verification** | Existing non-administrator modification probe was denied through the normal Access Denied page; before/after branding and audits identical. The helper expected literal 403 but followed the cookie-auth redirect to HTML 200 and stopped before the separate clinical-only header-read check. That subcheck remains deferred; existing focused authorized-clinical-reader evidence retained. |
| J — Tenant isolation | **DEFERRED runtime** | No approved ready second-tenant branding fixture. No other tenant migration, membership change or branding write. Prior automated two-database isolation/service-context evidence retained. |
| K — Existing clinical printing | **PASS, bounded stored-PDF read** | An existing stored referral PDF returned 200/application-pdf, 55,967 bytes; downloaded SHA256 exactly matched original stored bytes. No regeneration, clinical content change or artifact replacement. Unavailable filesystem artifact paths were not exercised, as described below. |
| L — Scheduling 007/008 | **PASS** | Existing appointment physician/room projections retained one underlying appointment identity; room-only filtering and room movement/resize restrictions held. Saved ordered defaults/version loaded, temporary selection applied and reload restored defaults. Zero appointment or preference POSTs. |

**F helper qualification:** The initial helper attempted a new navigation while Chrome was completing the rejected form's error-page navigation. Preserved `browser-results-at-oversize.json` with that raw failure. Web HTTP completion logs show **400**; `integrity-after-F.json` independently matches the prior logo/version and configuration audit count. Reopened the helper with normal sign-in and continued removal/remaining scenarios. No product patch, permission change or second deployment was made for this helper race.

**I qualification:** Preserved the later raw `200 !== 403` assertion in `browser-results.json`. Existing cookie authentication uses the default forbidden redirect to Account/AccessDenied; the Web logs show that AccessDenied view executing for the probe. No branding mutation or new audit occurred. This is not evidence of an authorization bypass. The remaining clinical-only header-read subcheck was not executed, and I is deliberately **not** classified as full PASS. An additional read-only diagnostic targeted the manager session; it is not counted as non-administrator printing evidence. Authentication/authorization code was inspected only to explain the result and was not changed.

Branded demonstration PDF: 39,576 bytes, SHA256 `6364D29B46F7A7D4E96DCEFC0FF0F8F0C0D6F67F1B356A38CD7BB62D87C2A1C3`.

Text-only demonstration PDF: 37,543 bytes, SHA256 `2784171E8E758FA8C5F00A18E569DEF399D11CE5A4E578247EC5A2FE4D3007DB`.

Stored referral download SHA256: `4C58D85E11D43DF20632ED3CE5DC96BD8E138CE0652631FC258E399F23DAA16C`. Only existing immutable bytes were read; no clinical generator or finalization path was invoked.

### Cleanup and data integrity

Baseline before branding writes: **zero logo rows / no logo**, with existing clinic contacts and 008 saved defaults captured. Exactly **five successful branding writes**, all actor UserId 2 and entity 1: Create, Update, Remove, Update, Remove. Audit IDs **1531–1535**, all with no patient association. Two invalid-upload attempts and the non-administrator denial probe produced no successful branding change. Eight branding POST attempts total; zero contact, appointment or preference writes.

Final original state: **no logo**, restored using the implemented Remove Logo UI. The retained singleton is the expected removal tombstone with null bytes/dimensions/digest, actor 2 and version `AAAAAAAAJ1M=`. Audit history remains intact; no physical row deletion or direct SQL cleanup was used. The original logical no-logo state is restored, rather than deleting the new audit/concurrency record.

All **13** before/after integrity hashes match: appointment metadata/history, clinical audit metadata, scheduling resource metadata, preferences across all users, complete clinic profile, patient documents, encounters, referrals, clinical artifact metadata, stored referral PDF bytes, filesystem artifact availability/available bytes and tenant membership/role state. (The reused helper's `OtherPreferencesHash` now covers all users, including UserId 2.) Saved 008 defaults and their row version also remained unchanged.

**Historical-file limitation:** Five existing filesystem-backed artifact records had no corresponding file under the configured private storage root at the pre-branding-write baseline. They remained missing at final verification. No repair, regeneration or clinical storage write was attempted; their unavailable bytes cannot be claimed as hash-verified. Artifact metadata/availability sentinels were unchanged. The stored referral PDF provided a separate available clinical download fixture with exact-byte verification. Other historical clinical print paths remain unexercised by this bounded runtime check; prior focused regression evidence is retained.

No patient/clinical record, signed PDF, appointment, resource administration, tenant membership/role, contact profile, Scheduling source/behavior or unrelated configuration was changed. Normal authentication and authorized read auditing were retained. No production/staging access, service other than API/Web restart, Git state change, commit/push or Step 02 work occurred.

### Evidence and remaining actions

Evidence directory: `.tmp/product-improvement-009/rollout/` includes `preflight.json`, `chromium-preflight.json`, `plan-and-rollback.json`, verified prior artifacts, `sql-access.json`, `deployment.json`, `health-and-versions.json`, `final-health.json`, startup/stderr logs, before/after/final integrity snapshots, original/continued browser receipts, `header-with-logo.{html,pdf,png}`, `header-without-logo.{html,pdf,png}`, `pdf-verification.json`, and consolidated `closure.json`. SQL helpers are under `.tmp/status-acceptance-preparation/009*.fsx`. Browser contexts were closed without exporting sessions/cookies. Consolidated integrity closure recorded at **20:33:17 UTC / 16:33:17 America/Toronto**.

Remaining runtime checks are the clinical-only non-administrator header read (I remainder), cross-tenant branding isolation (J), and unavailable historical artifact paths when suitable approved fixtures are available. Do not change roles/memberships, apply 0104 elsewhere or regenerate archived PDFs merely to make those checks pass. The five missing files are an existing fixture/storage limitation outside this branding rollout's repair scope.

Owner actions: review this partial verification and authorize any separate follow-up acceptance/integration scope as needed. No additional migration, grant or deployment action is required for the completed single-tenant logo workflows. **IMPLEMENTED — DEPLOYED, PARTIALLY VERIFIED. STOPPED; Step 02 not started.**
