# Product Improvement 009 — Step 04C: Development Deployment Preflight

2026-11-03 (America/Toronto). **BLOCKED — deployment not authorized or performed.** Existing v3-compatible recovery binaries are intact, but no existing rollback script is bound to them for this API-only rollout. Runtime fixture/account prerequisites also remain incomplete. No runtime scenario is reported as passed.

## Baseline and repository state

Reuse [Step 04B](009-step04b-branded-encounter-chart-pdfs.md): 79 focused tests passed, affected build had zero warnings/errors, nine synthetic PDFs and 51 page images were reviewed. Those checks were not repeated. No Step 04A reassessment was performed.

One initial Git status inspection: `main...origin/main`, clean working tree. HEAD is `4e6f0eb74671c131fcefa241785c4aeef33a77a8`. This is local tracking information, not a remote freshness check. Read-only commit inspection confirms the eight Step 04B files and ten preceding owner files are committed together; no owner edits were reverted or overwritten.

Exact Step 04B production files:

- `src/MicroEMR.Application/PatientCpp/CppPrintService.cs`
- `src/MicroEMR.Application/PatientImmunizations/PatientImmunizationSummaryService.cs`
- `src/MicroEMR.Application/ClinicalOutput/ClinicalPdfPreviewService.cs`
- `src/MicroEMR.Application/ClinicConfiguration/ClinicBrandingService.cs`

Other Step 04B files: `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs`, `ImmunizationSummaryTests.cs`, `BrandedChartPdfTests.cs` in the same directory, and the Step 04B report.

The ten preceding owner files, reconciled against the commit's 18-file list:

- `db/tenant-clinical/manifest.json`
- `db/tenant-clinical/migrations/0105-prescription-branding-snapshot.sql`
- `docs/product-readiness/009-step03-prescription-printing-assessment.md`
- `docs/product-readiness/009-step03b-versioned-prescription-branding.md`
- `docs/product-readiness/009-step04-encounter-chart-print-assessment.md`
- `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs`
- `src/MicroEMR.Application/PatientPrescriptions/PrescriptionBrandingSnapshot.cs`
- `src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs`
- `src/MicroEMR.Infrastructure/ClinicalOutput/PlaywrightPdfRenderer.cs`
- `tests/MicroEMR.Api.Tests/PrescriptionBrandingTests.cs`

## Affected components and actual development targets

**Deploy API only.** All three generators and the branding resolver execute in API-scoped services. Existing Web controllers forward CPP, immunization summary and encounter preview to API clients. Web's project reference to Application alone does not require its deployment. There is no Web route, DTO, client asset, shared renderer, Auth or database change in Step 04B.

| Component | Existing target | Step 04C action after approval |
| --- | --- | --- |
| API | `src/MicroEMR.Api/bin/Debug/net10.0/`, content root `src/MicroEMR.Api`; HTTPS `https://localhost:7003`, HTTP `http://localhost:5245` | Replace only the compatible Application DLL/PDB pair, after dependency/manifest binding; start API under the existing `https` profile |
| Web | `src/MicroEMR.Web/bin/Debug/net10.0/`, content root `src/MicroEMR.Web`; HTTPS `https://localhost:7002`, HTTP `http://localhost:5002` | No deployment; needs an available existing host for browser acceptance |
| Auth | `https://localhost:7179` | No deployment; existing OIDC service is an acceptance prerequisite |

Current disk API/Web/Application/Infrastructure DLLs match Step 03 deployment hashes and report product version `1.0.0+fea56d66b887d73562dd303ea10a9c991a11211b`. No `MicroEMR.Api`, `MicroEMR.Web` or `MicroEMR.Auth` process was found during preflight. This establishes no current live version or healthy service claim. Starting services is outside this preflight.

Candidate: `.tmp/product-improvement-009/step04/artifacts/bin/MicroEMR.Application/debug/MicroEMR.Application.dll` and matching PDB from the established tested build. All four Step 04 host/library binaries exist and have different hashes from Step 03; a different hash does not establish a functional change. Step 04B's production checksum evidence bounds functional changes to the four Application files. Do not copy the entire test output or redeploy Infrastructure/Web/API host binaries just because those build hashes differ. Binding the minimal DLL/PDB pair to a fresh deployment manifest remains required.

Configuration to preserve: source and target appsettings, launch settings, API/Web development user secrets, authentication authority/audience/client, tenant secret mappings, patient-file storage `D:\MicroEMR-Data\patient-files`, runtime/dependency files and Web static assets. Required launch environment is `ASPNETCORE_ENVIRONMENT=Development` (set by the profile); preserve any existing `DOTNET_ENVIRONMENT`, URL or Playwright overrides rather than inventing replacements. No new Step 04 environment variable is required. The existing renderer launches installed Playwright Chromium headlessly; availability under the deployment identity is a prerequisite, not newly verified by launching it here. Preserve existing HTTPS certificates and trusted issuer configuration.

Expected health checks after separately approved service activation: API `GET /health/platform` => 200; Web unauthenticated `GET /ClinicConfiguration` => 302 to OIDC. The latter is a routing/authentication smoke check, not a dedicated health endpoint or permission proof. Last recorded results are from Step 03 on November 1; no HTTP request was issued here.

## Development tenant/database verification

Trusted evidence is the repository's existing preflight helpers and retained deployment receipts under `.tmp/status-acceptance-preparation/` and `.tmp/product-improvement-009/step03-rollout/`.

- Intended tenant: `local-dev-fresh`, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`.
- Catalog mapping previously verified: server key `local-sql`, secret reference `development:MicroEMR_LocalDev_Fresh`.
- Current DatabaseTool development secret was inspected without printing credentials: server `192.168.50.1,50013`, database `MicroEMR_LocalDev_Fresh`; the corresponding API tenant secret is present. Credentials are not copied into this report.
- The retained Step 03 read-only database identity receipt verified that UID/database and installed migration 0105/procedure definitions on November 1. Its canonical ledger hash is `0485DD8CE3666F9A75C65BD5323AC2491C13AE11A82784BDF910CF2DC2286B08`.
- **Current configuration target verified; current catalog mapping, connected database identity, permissions, records and branding not freshly queried.** Reconfirm these read-only immediately before an authorized rollout; do not treat historical receipts as today's database state.
- `local-dev` / `MicroEMR_Db` is a separate, unqualified target. Its Step 03 migration was owner-reported; the DatabaseTool alias was absent. Do not use it as an automatic substitute or connect to production.

No SQL connection or persistent database operation was made in this preflight. No migration or grant is needed for Step 04B.

## Existing deployment and rollback evidence

Inspected, did not execute: `step03-rollout/prepare.ps1`, `deploy.ps1`, and `step02c/rollback.ps1`, relative to `.tmp/product-improvement-009/`. Existing deployment mechanism is hash-guarded copies into the local Debug host directories, followed by `dotnet run --no-build --no-restore --launch-profile https` in the corresponding content root. These are local development scripts, not publishing infrastructure.

All **14 retained Step 03 source files** match `step03-rollout/plan-and-rollback.json` ExpectedHash values, including both host DLL/PDB sets, Application/Infrastructure DLL/PDB pairs and Web static-asset metadata. Six current target DLLs also match their recorded Step 03 deployment hashes. Thus a known v3-capable recovery release exists at `.tmp/product-improvement-009/step03/artifacts/bin/`. In particular, its Application DLL SHA-256 is `258AEF1BD0639C6ED24887760691100F62C891FB1F9E8D03D09C57B14BF8C5D5` and PDB is `566DA728C31AD8931F41067A71F6674F8ACEE7C9A27A8D1B121B114A1F80363B`.

The six historical Step 03 previous-artifact DLLs still match PreviousHash, but those are pre-v3-reader backups and **must not be restored**. `step02c/rollback.ps1` is hard-coded to an even earlier plan. `step03-rollout/deploy.ps1` copies 14 files and restarts both hosts using old PIDs. Neither is an executable Step 04C command. Do not run them unchanged.

**Rollback readiness: compatible recovery bytes verified; deployable rollback procedure BLOCKED.** There is no inspected existing Step 04C manifest/script pairing the API-only target with the verified v3 recovery files. Under this task's requirement not to invent rollback procedures, stop before deployment. Closure requires a separately scoped adaptation/review of the established copy/hash/restart mechanism, preserving earlier evidence, and validation of its exact source/target/recovery hashes. No SQL rollback, snapshot downgrade, historical deletion or whole-directory overwrite is appropriate.

## Runtime acceptance matrix

Every row is **NOT EXECUTED**. “Pending” means the scenario is prepared and needs a qualified session/environment; “Deferred” means an essential fixture or authorization is not established. The existing patient candidate is `9f65d4ca-c347-406a-bb32-c709a8941f80` from Step 03; its continued suitability for these chart outputs is unverified. Prior prescription/consultation fixtures do not establish an Open encounter, long CPP or immunization history.

| ID | Read-only acceptance observation | Prerequisite / availability / planning disposition | P1 account / service / availability | Clinical writes needed | Temporary branding needed |
| --- | --- | --- | --- | --- | --- |
| CPP-RT01 | Authorized CPP PDF opens | Patients.View, resolved clinical actor, permitted CPP sources, valid selected clinician/categories, existing patient; candidate above; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No |
| CPP-RT02 | Clinic logo correct | Existing approved logo tenant/session; last target receipt says no logo; Deferred | A1 / S1 / DEFERRED; NOT RUN | No | Only if no existing logo fixture; separate authorization |
| CPP-RT03 | Text-only header correct | Confirm target remains no-logo; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No if prior no-logo state persists; otherwise defer |
| CPP-RT04 | Patient identifiers correct | Compare PDF to existing patient/source values; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No |
| CPP-RT05 | Selected clinical sections complete | Record source/category baseline without changing selections persistently; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No |
| CPP-RT06 | Long CPP paginates without loss | Existing sufficiently long CPP not identified; Deferred | A1 / S1 / DEFERRED; NOT RUN | No | No |
| CPP-RT07 | Unauthorized request denied | Existing account lacking required permission/source access; Deferred | A4 / S1 / DEFERRED; NOT RUN | No | No |
| IMM-RT01 | Immunization PDF opens | Patients.View and existing candidate/history; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No |
| IMM-RT02 | Logo correct | Logo prerequisite as CPP-RT02; Deferred | A1 / S1 / DEFERRED; NOT RUN | No | Only if no existing logo fixture; separate authorization |
| IMM-RT03 | Text-only header correct | Confirm no-logo state; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No if prior no-logo state persists; otherwise defer |
| IMM-RT04 | Records unchanged and complete | Read-only before/after history comparison including correction/refusal distinctions; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No |
| IMM-RT05 | Long history paginates | Existing long immunization history not identified; Deferred | A1 / S1 / DEFERRED; NOT RUN | No | No |
| IMM-RT06 | Existing permissions enforced | Existing denied account; Deferred | A4 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT01 | Saved Open preview opens | Existing Open record, historical template provenance, submitted processed values equal saved values; no suitable UID established; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT02 | Branding correct | Qualified Open record plus configured logo; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | Only if no existing logo fixture; separate authorization |
| ENC-RT03 | Text-only fallback correct | Qualified Open record plus no-logo configuration; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | No if prior no-logo state persists; otherwise defer |
| ENC-RT04 | Encounter-only user can preview | Encounters.View AND Encounters.Edit, without Patients.View/ClinicSettings.Manage, correct tenant/clinical identity; account unqualified; Deferred | A3 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT05 | Wrong-patient access denied | Existing foreign/unowned encounter; preview accepts encounter UID, not a patient selector. Exercise ownership through that UID and existing scoped reads; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT06 | Unauthorized encounter denied | Missing-view and missing-edit sessions; Deferred | A4 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT07 | Attribution retained | Existing saved fields/narratives with author/editor contributions; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT08 | Status unchanged | Capture status/signature/rowversion before/after preview using reads; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT09 | No signed artifact creation/repair | Before/after artifact metadata and existing file availability; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | No |
| ENC-RT10 | Existing read-audit behavior retained | Existing preview has no dedicated structured-read-audit event; compare established behavior, do not require a new event; Deferred | A2 / S1 / DEFERRED; NOT RUN | No | No |
| HIST-RT01 | Existing signed PDF downloads | Known Signed encounter with readable stored PDF and authorized session; none identified; Deferred | A1 / S1 / DEFERRED; NOT RUN | No | No |
| HIST-RT02 | Signed bytes unchanged | Same known artifact, pre/post SHA-256 and byte length; Deferred | A1 / S1 / DEFERRED; NOT RUN | No | No |
| HIST-RT03 | Missing historical PDF remains missing | Existing known missing signed-encounter artifact needed; five prior missing historical files are not qualified as encounter fixtures; Deferred | A1 / S1 / DEFERRED; NOT RUN | No | No |
| HIST-RT04 | Original signed rendering path retained | Step 04B source/test evidence already established; combine with stored-only final-download observations; no signed rendering invocation; Pending evidence review | A1 / S1 / DEFERRED; NOT RUN | No | No |
| TEN-RT01 | Branding uses authorized tenant | Verified session tenant and current clinic baseline; Pending | A1 / S1 / DEFERRED; NOT RUN | No | No |
| TEN-RT02 | Foreign patient/encounter denied | Second authenticated development tenant/session and known foreign records; Deferred | A5 / S1 / DEFERRED; NOT RUN | No | No |
| TEN-RT03 | No branding leakage | Two qualified tenants with distinguishable existing branding; Deferred | A5 / S1 / DEFERRED; NOT RUN | No | No |

Acceptance order after prerequisites and explicit approval: capture identity/configuration/clinical/artifact baselines; health; current no-logo CPP/immunization and Open preview; existing-logo tenant if available; long content; encounter-only/denied/wrong-patient cases; authenticated second-tenant cases; historical hashes and final integrity/audit comparison. No fixture creation, permission edits, clinical saves, signing, finalization, retry/Ensure, or historical repair is part of this plan. PDF POST requests must carry existing saved inputs and valid antiforgery; source read audits may be written by the established application, without clinical mutation. These requests were not executed in preflight.

## Accounts, security and audit

Existing retained account evidence: `admin@microemr.local`, active Provider UID `4432ffba-69f1-4d68-9969-202022051ab0` on November 1. It is only a candidate for broad authorized/admin and restoration checks. It cannot prove encounter-only or patient-view-only behavior. The prior user inventory contains other accounts but does not establish their effective permissions or tenant memberships; no credentials or roles are inferred from usernames.

Required existing sessions: patient reader with necessary CPP source permissions and clinical actor; encounter-only reader/editor without Patients.View or clinic management; denied users missing each relevant permission; second-tenant reader/editor with known records; clinic administrator only if separately authorized restoration is needed. Missing sessions cause deferral, not account/role creation.

Reuse Step 04B's endpoint policy, wrong-patient, scoped-repository tenant denial, real branding authorization and audit-order tests. CPP/immunization retain Patients.View and governed source read audits; preview requires Encounters.View AND Encounters.Edit and saved Open eligibility before branding. Operation branding reads require trusted tenant/authenticated subject and do not require Patients.View for encounter preview. General branding/admin authorization remains unchanged; no public route or grant is added. Runtime denial and audit verification remain pending, including sensitive document read behavior where applicable; no new preview-specific audit event is claimed.

## Historical baseline and temporary branding restoration

Signed encounter integrity can be checked with read-only stored-file downloads and SHA-256, provided a known readable signed artifact is first identified. Capture encounter/patient/tenant/artifact identity, storage availability, byte length and hash before deployment and after Open previews/branding checks. Use only the existing final-download path; never call signing, retry, Ensure or render endpoints. Record absent files as missing without repair. Do not substitute prescription PDFs or local synthetic preserved-byte tests for a persistent signed encounter baseline.

Last retained branding receipt (`step03-rollout/integrity-final.json`, November 1): **Test Clinic**, address **19 cottongrass lane**, Brampton, Ontario, L6R 3A9, Canada; phone **1416-555-4444**, email **abc@testclinic.ca**; address line 2/fax absent; logo absent (null digest/dimensions). These are historical values, not a fresh branding read.

Prefer existing no-logo and logo configurations in separately authorized tenants, avoiding writes. If the owner explicitly authorizes a temporary logo change later: first capture current contacts, sanitized PNG bytes/hash/dimensions if present, and concurrency token through existing administration reads. Use Clinic Settings / `ClinicBranding.Save` with ClinicSettings.Manage, antiforgery and current rowversion; restore the original PNG through that same mechanism, or remove the temporary logo if originally absent. Preserve contact values. Re-read current state and compare contacts/logo bytes/hash/dimensions to the captured baseline; rowversion and configuration audit timestamps are expected to advance. Stop on conflict and revalidate rather than overwrite. Never use SQL for restoration. No branding change is authorized or performed here.

## Deployment checklist, command boundary and stop conditions

1. Resolve the API-only manifest/rollback binding blocker using the verified Step 03 v3 recovery pair; preserve existing receipts and configuration. Verify candidate/dependency compatibility and current target hashes, including the matching PDB, without rebuilding/repeating established tests absent a specific reason.
2. Reconfirm the configured development catalog/tenant/database identity and required existing permissions/fixtures; capture current branding and known signed-encounter baseline. Mark insufficient fixtures Deferred. Confirm services/Chromium/certificates and obtain explicit service-start authorization if hosts remain stopped.
3. Present the exact API-only deployment/rollback script and manifest for owner approval. Existing deployment scripts are references only, not approved Step 04C commands.
4. Only after authorization: use the established hash-check/copy/host-start mechanism against the API target. Preserve Web/Auth/assets/configuration and SQL history. Verify loaded Application version/hash, Development content root, API health and existing Web/OIDC availability before acceptance.
5. Stop on unexpected tenant, mismatched source/target/recovery/configuration hashes, unavailable v3 recovery, lock/copy failure, unhealthy API, permission regression, cross-tenant result, clinical mutation, changed historical bytes or unexpected artifact creation. Do not repair or broaden access to proceed.
6. If rollback is needed after a separately approved rollout, use only the reviewed manifest bound to the verified Step 03 Application pair and existing host mechanism. Preserve the v3 reader and database history. Verify restored hashes/configuration/content root, API health, Web redirect and known stored PDF reads/hashes. No executable rollback is authorized by this report.

**Proposed command sequence is gated, not runnable yet.** Existing scripts contain stale PIDs, both-host operations and incompatible backup destinations; supplying them as deployment commands would be unsafe. After the manifest/script blocker is resolved, the approved sequence must be: read-only manifest/hash/tenant checks → approved API-only copy operation → existing `dotnet run --no-build --no-restore --launch-profile https` from `src/MicroEMR.Api` → health/hash checks → approved acceptance. Failure recovery must select the verified v3-capable Step 03 source, never any `previous-artifacts` directory. No speculative replacement script was created or executed.

## Deferred qualifications and authorization checkpoint

Carry forward unchanged: (A) signed encounter artifact creation occurs after signing commits; (B) chronology exposes prescription-v3 branding snapshot properties/base64. No chronology/prescription change or historical repair is proposed.

Step 02: documents-only account qualification, authenticated second-tenant branding isolation, successful persisted consultation signing/referral sending, and five missing historical files remain qualified; retain the other visual/print qualifications in that report. Step 03: independent `local-dev` migration verification, patient-view-only reader, authenticated cross-tenant access and deployed multipage/worst combined contacts remain pending. Step 04 does not resolve certification status or these release qualifications.

Only this report was added. Checks: one Git status, targeted configuration/source/deployment-receipt inspection, local commit reconciliation, file-version and SHA-256 comparisons, process presence inspection and diff whitespace review. No tests/builds, health/runtime requests, SQL operations, deployment/publishing, service start/restart, branding/clinical writes, Git state changes or remote operations occurred.

**STOP — BLOCKED. Deployment cannot safely proceed under the currently bound scripts.** Owner authorization is required before deployment/service activation and runtime acceptance; temporary branding writes require separate explicit authorization. First resolve the rollback binding and present the concrete API-only command/manifest for review. No Step 05 work is authorized.

## Step 04C-P1 — Recovery and Environment Readiness

2026-11-03 (America/Toronto). **PARTIALLY READY — DEPLOYMENT BLOCKED.** This follow-up supersedes the original rollback-binding blocker only. The preceding preflight findings remain historical evidence; the original 30 rows now have additive P1 prerequisite columns. All runtime scenarios remain **NOT RUN**.

### Binding correction and recovery validation

Exact defect: `.tmp/product-improvement-009/step02c/rollback.ps1` selected `step02c/plan-and-rollback.json`, restored its `Backup`/`PreviousHash` files and iterated both API/Web hosts. Merely changing that directory to `step03-rollout` would still restore the pre-v3 reader because that plan's Backup entries precede Step 03.

Corrected that existing script, without adding a script, package, dry-run switch or execution path. It now reads the existing Step 03 plan, selects only the API Application DLL/PDB targets, and binds recovery to **Source/ExpectedHash**, with the two independently established Step 03 hashes pinned in the script. Its local absolute roots, exact source/target names and single API host binding are checked before mutations. It checks the Development HTTPS profile, conflicting process environment settings, recorded API configuration hashes and unchanged API host/Infrastructure hashes. Missing/duplicate entries, missing files, mismatched hashes, a different source path or incompatible dependencies stop recovery before file copying/service operations. Existing process-path checks, terminating errors, post-copy hashes and hidden `dotnet run --no-build --no-restore --launch-profile https` behavior are retained. Only API can be selected; Web/Auth are excluded. Future logs/receipt use `step04-recovery` names to preserve prior deployment evidence.

Changed files: that existing ignored rollback script and this report. No manifest, recovery artifact, application source, launch setting, database or other script was changed. The rollback script has no parameters and no safe dry-run mode; **it was not invoked or dot-sourced**. It now serves the bounded Step 04 API recovery use and must not be used to recreate the historical Step 02 rollback.

Static validation: PowerShell AST parse **0 errors**; independently resolved precisely two API Application recovery files from the existing manifest; rechecked both against ExpectedHash. Inspected selection, exact paths, Development environment, dependency/configuration checks, missing/duplicate-entry and missing/hash-mismatch failure branches. The first mutating command is `Stop-Process` at line 43, after recovery/configuration/dependency guards. Failure branches were reviewed statically, not exercised through script execution. No behavioral rollback, process-stop/start or copy claim is made.

Recovery compatibility evidence: all **14 Step 03 Source files remain readable and match ExpectedHash**. The selected Application DLL/PDB hashes are the two recorded above; the retained release and current disk API have product version `1.0.0+fea56d66b887d73562dd303ea10a9c991a11211b`. Compatibility is supported by identity with the November 1 Step 03 release that successfully rendered signed/unsigned v3 prescription output, not by version text alone. No pre-v3 Backup/PreviousHash is selected. All ten recorded API/Web configuration hashes still match; API-required configuration exists. These local recovery bytes are suitable for the documented Application-only rollback while its host/dependency guards continue to pass. No production default, secret or alternate release selector was introduced.

### Environment, identity and availability

Expected API remains `src/MicroEMR.Api`, .NET 10 development executable launched with `dotnet run` under the `https` profile; content root `src/MicroEMR.Api`, URL `https://localhost:7003`, health `/health/platform`. Source appsettings, Development settings, existing user secrets and environment overrides are the configuration sources. Expected current disk release is the Step 03 product version above; there is no verified running version.

Read-only requests to API `/health/platform`, Web `/ClinicConfiguration` and Auth `/.well-known/openid-configuration` all failed with WebException (five-second per-request limit, normal certificate validation, no TLS bypass). No status or response identity was obtained. Process inspection found no hosts with the three expected process names. **API availability: DEFERRED**, not a successful health check or a finding of incorrect identity. Web/Auth availability is also DEFERRED. No service was started, stopped or restarted.

**CONFIGURED IDENTITY:** the current API user-secret mapping `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` resolves to development server `192.168.50.1,50013` / database `MicroEMR_LocalDev_Fresh`; no credentials are recorded here. Intended `local-dev-fresh` tenant/UID remains supported by the existing trusted deployment/catalog receipts. Tenant selection is not a hard-coded default database: API middleware reads exactly one authenticated `MicroEmrClaimTypes.TenantId`, resolves the catalog and verifies active membership before establishing scoped tenant context.

**LIVE VERIFIED IDENTITY: DEFERRED.** With API unavailable and no authenticated tenant session, configuration does not prove the actual tenant context. No SQL was needed or performed; historical November 1 database identity evidence remains historical. A subsequent authenticated context check must confirm tenant, membership and database mapping before deployment acceptance. No migration is pending for these Step 04 changes.

### Account and fixture inventory

Account keys added to the existing matrix:

| Key | Required existing account | Availability / qualification |
| --- | --- | --- |
| A1 | Patients.View plus CPP source permissions/resolved clinical actor where required; authorized signed-download reader | Broad administrator candidate exists in retained evidence; requires independent verification of current session, permissions and clinical mapping. Suitable for CPP/immunization positive checks only after verification |
| A2 | Encounters.View AND Encounters.Edit with appropriate record/tenant access | Administrator candidate exists historically; current access requires independent verification |
| A3 | Same encounter permissions, without Patients.View or ClinicSettings.Manage | No qualified account available in inspected evidence; owner must identify an existing candidate, or separately authorize provisioning outside P1 |
| A4 | Denied account lacking the applicable view/edit/source permission | No qualified account available; requires owner identification/provisioning and independent effective-permission verification |
| A5 | Authenticated second-tenant reader/editor plus A1/A2 in the first tenant | Other usernames exist in prior inventory, but no qualified second-tenant membership/session/permissions; requires independent verification and owner assistance |

No password, membership or permission changed. An administrator cannot substitute for A3 or A4. Wrong-patient cases need A2/A5 plus an actual foreign/unowned record, not a new account or guessed UID. `S1` means API and Auth for authenticated API access, plus Web for the planned browser flow. All S1 services are presently unverified/unavailable to these probes, so every row is Deferred and NOT RUN. The original prerequisite cell supplies the required record/content; account/service keys supplement it. All scenarios can be conducted without clinical writes; established read-audit writes remain application behavior. Temporary branding is never authorized by P1; use existing logo/no-logo fixtures or defer those rows.

| Fixture category | Documented availability / tenant | Suitability / missing prerequisites |
| --- | --- | --- |
| Normal CPP | Existing patient candidate, local-dev-fresh, in Step 03 receipt | Patient existence is historical; CPP content/source access/clinician not verified; DEFERRED |
| Long CPP | No qualified persistent fixture documented | Local synthetic 16-page PDF is implementation evidence only; DEFERRED |
| Immunization records | Same existing patient candidate | Actual history not established; DEFERRED |
| Long immunization history | No qualified persistent fixture documented | Synthetic 14-page sample is not a runtime fixture; DEFERRED |
| Saved Open encounter | No qualified existing UID/provenance documented | Need Open status, historical template and equality with saved processed values; DEFERRED |
| Long saved Open encounter | No qualified persistent fixture documented | Need existing long notes/contributions, without editing; DEFERRED |
| Existing signed encounter PDF | No qualified readable stored encounter PDF documented | Prior prescription/consultation receipts cannot substitute; DEFERRED |
| Historical hash baseline | No qualified signed encounter artifact identified | Read-only stored-byte hash possible once a valid artifact is known; no download/hash performed; DEFERRED |
| Wrong-patient denial | No qualified encounter/other-patient pair documented | Need owned/foreign record identities in verified tenant; DEFERRED |
| Second-tenant isolation | No qualified record/session pair documented | Need authorized second development tenant and distinct branding/records; DEFERRED |

Known missing historical files remain missing/unqualified evidence, never candidates for repair. No encounter signing, artifact Ensure/retry, finalization, regeneration or stored-file change was attempted. No historical clinical details or additional patient identifiers are added here.

### Deployment safety gates

| Gate | Classification | Meaning |
| --- | --- | --- |
| G1 Correct rollback binding | PASS | Existing script now selects verified Step 03 Source pair for API only |
| G2 v3-compatible recovery | PASS | Hash-identical to the previously runtime-verified v3 release |
| G3 Safe rollback validation | PASS | Static-only scope: syntax, binding, hashes, environment/config/dependency guards and failure review; no execution |
| G4 API target | PASS | Exact local API content/binary roots and HTTPS profile verified |
| G5 Development environment | PASS | Configured profile verified; running environment remains unverified |
| G6 Tenant configuration | PASS | Current configured development mapping verified; tenant selected by authenticated claim/catalog |
| G7 Live tenant identity | DEFERRED | Required before acceptance/deployment decision; no live authenticated context |
| G8 API health | DEFERRED | Health request failed; no service activation allowed in P1 |
| G9 Historical safety baseline | DEFERRED | Runtime integrity qualification; no suitable existing encounter PDF established |
| G10 Accounts/fixtures | DEFERRED | Runtime qualifications, not evidence of unsafe recovery; missing optional records do not justify writes |
| G11 No unauthorized writes required | PASS | P1 uses local script/report edits and read-only inspections; acceptance needs no clinical mutation |
| G12 Owner deployment authorization | BLOCKED | Mandatory authorization absent; P1 explicitly excludes deployment, service activation and rollback execution |

The unsafe artifact-selection blocker is resolved within static validation limits. Deployment remains blocked by G12 and cannot be presented as ready while live target/health gates G7/G8 remain unverified. Optional runtime fixture gaps G9/G10 remain Deferred rather than being treated as mandatory fixture-creation work.

### Step 04C-P2 boundary and remaining qualifications

P2 is **not started or authorized**. A later owner instruction may authorize development service activation if needed, read-only live identity/health confirmation, and a concrete API-only deployment using the established tested Application pair with this corrected recovery binding. Reconfirm all hashes/configuration immediately before mutations; preserve Web/Auth binaries and all clinical/database history. Deployment still needs an exact reviewed copy manifest/command, owner approval, and a healthy verified live target. Do not execute the old both-host deployment script unchanged. Missing accounts/fixtures may leave explicitly listed scenarios deferred; no account provisioning, clinical creation, signing, historical repair or branding mutation is implied by deployment approval.

Preserve all prior deferred issues: signed PDF creation after the signing transaction; chronology exposure of prescription-v3 branding data; Step 02 documents-only/second-tenant and other historical qualifications; Step 03 patient-view-only/cross-tenant/local-dev migration and long-output qualifications. None was changed or reopened.

P1 checks: one authorized Git status (only the existing untracked Step 04C report at entry), targeted script/configuration/receipt reads, all 14 release hashes and ten configuration hashes, static PowerShell parse/path/failure review, process inspection and three read-only health probes. No further Git operations, builds, application tests, SQL, runtime acceptance scenarios, binary copies, service operations, deployment, rollback execution or persistent clinical/branding writes occurred. **PARTIALLY READY — DEPLOYMENT BLOCKED; STOP at the P2 authorization boundary.**

### Owner-started services — read-only availability recheck

2026-11-03 (America/Toronto). After the owner reported starting all services, process paths matched the local API/Web/Auth `src/<project>/bin/Debug/net10.0/` executables (PIDs 32620 / 18204 / 20724 respectively). Read-only HTTPS checks with normal TLS validation returned:

- API `https://localhost:7003/health/platform`: **200**.
- Web `https://localhost:7002/ClinicConfiguration`: **302**, expected unauthenticated redirect.
- Auth `https://localhost:7179/.well-known/openid-configuration`: valid discovery response with issuer **https://localhost:7179/**.
- API `https://localhost:7003/api/context/tenant`: **401** without an authenticated session; no tenant identity was exposed or verified.

Initial restricted-context probes failed before HTTP with Windows Schannel `SEC_E_NO_CREDENTIALS`. The same read-only probes succeeded outside that restriction; no certificate validation bypass or application change was used. The earlier failed probes therefore do not establish that services were unavailable.

**G8 now PASS for reachability/health.** Service prerequisite S1 is now available; the original matrix's P1 service-deferral notes are historical. Account/fixture qualifications and all 30 NOT RUN results remain unchanged. G7 live authenticated tenant identity remains DEFERRED, and G12 deployment authorization remains BLOCKED. These probes do not prove loaded release hashes or tenant/database identity. No deployment, restart, SQL, clinical/branding mutation or runtime PDF acceptance scenario was performed. Only this report was updated during the recheck.

## Step 04C-P1C — Authenticated Tenant Identity Confirmation

2026-11-03 (America/Toronto). **DEFERRED — AUTHENTICATION REQUIRED.** No authenticated request was executed. The connected-session tool returned no document/browser sessions; no authorized API authentication session is available to this run. A separate personal Web browser session, if present, is not automatically accessible to the agent or command-line HTTP client. Per the P1C authentication stop condition, no SQL or further identity investigation was performed after establishing that limitation.

Inspected `TenantContextController`, `TenantResolutionMiddleware`, API bearer-authentication registration and the existing Web `Account.Login` action. Exact request is **GET https://localhost:7003/api/context/tenant**, with `[Authorize]` using the existing JWT bearer scheme. It returns only `TenantUid`, `TenantKey`, `DisplayName` (JSON naming follows configured serialization). No body, tenant-selection header, clinical permission or client-supplied database selector is required. A successful response must follow validation of exactly one tenant claim, an active catalog tenant, a nonempty authenticated subject and a matching Active membership; middleware then establishes scoped tenant context. This is source evidence, not a live membership PASS. The endpoint does not query or expose the resolved clinical database, so even its successful response alone would not prove a live request-resolved database.

Actual authenticated status, subject, active tenant and live membership: **not obtained / DEFERRED**. The earlier unauthenticated 401 is retained as authorization evidence only. Expected tenant remains `local-dev-fresh`, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`; configured database remains `MicroEMR_LocalDev_Fresh`. Catalog mapping has only the previously dated receipts; neither current catalog-mapped nor live request-resolved database identity was independently established in P1C. No mismatch was observed because no authenticated identity response was available.

### Minimum owner action and reproducible request

1. Use the existing Web sign-in at **https://localhost:7002/Account/Login** with an already authorized development account. If already signed in, retain the session. Do not change an active tenant assignment or membership for this check. If the current tenant is different, stop and report the mismatch.
2. Make an existing authorized development authentication session available through supported session tooling, or run the request below in an already authenticated API client. Web uses an OIDC/cookie session while API uses bearer authentication: opening the API URL in a signed-in Web browser does not automatically authenticate it. The inspected Swagger configuration has no configured OAuth/Authorize flow; do not assume Swagger provides one.
3. In the existing authenticated API client, issue **GET https://localhost:7003/api/context/tenant**. Nonsecret header: `Accept: application/json`. Body: none. Authentication: the client's existing securely managed development bearer credential for this API, supplied internally as `Authorization: Bearer …`; do not copy it into chat, this report, logs or a new helper. If no such authorized client/session exists, leave the check deferred rather than extracting browser cookies/tokens or constructing a new login flow.
4. Share only the HTTP status and the nonsecret tenant-context JSON. Expected: **200**, tenant key `local-dev-fresh` and the UID above; display name is informational, not a value to invent. **401** => authentication remains deferred; **403** => active tenant/membership could not be validated, stop; **503** => identity validation unavailable, defer; **200 with another tenant/UID** => BLOCKED — IDENTITY MISMATCH, stop without switching tenants. A matching 200 still needs a trusted current tenant/database mapping check before P2 readiness can be asserted.

No passwords, bearer/refresh tokens, authorization codes, session cookies or client secrets are requested. Ordinary sign-in is owner interaction through the established application; this task does not create accounts, reset credentials or change authorization. Further database mapping verification is contingent on obtaining the authenticated active tenant, rather than treating a configured database as live proof.

### Affected gates and P2 decision

| Gate | P1C result | Evidence / limitation |
| --- | --- | --- |
| G5 Development environment | PASS, configured only | Reuse P1 local Development configuration; no new running-environment claim |
| G6 Tenant configuration | PASS, configured only | Reuse P1 configuration; current catalog/live mapping still unverified |
| G7 Authenticated live tenant | DEFERRED | Authorized session unavailable; no authenticated response |
| G8 API health | PASS, prior recheck | Reused successful health result; no redundant probe |
| G11 No unauthorized writes | PASS | Only report edit, read-only source/session inspection and the single authorized Git status |
| G12 Owner deployment authorization | BLOCKED | P1C does not authorize deployment |

G1–G3 recovery evidence is reused without recalculating hashes or invoking scripts. Restricted-user, second-tenant, historical artifact and other fixture qualifications remain deferred; all 30 acceptance scenarios remain NOT RUN. No cross-tenant clinical access was attempted. The signed-artifact lifecycle risk, chronology v3-data issue and previous Step 02/03 qualifications remain unchanged.

Only this report changed during P1C. No tests/builds, health probes, authenticated API requests, SQL, service operations, binary copies, deployments, persistent clinical/branding writes or Git operations beyond the one explicitly requested status inspection occurred. **P2 is not yet ready for a deployment-authorization decision: obtain the authenticated active tenant and verify its current database mapping first. STOP — DEFERRED — AUTHENTICATION REQUIRED.**

## Step 04C-P2 — API Deployment Preparation

2026-11-03 (America/Toronto). **BLOCKED — DEPLOYMENT PREPARATION.** Preparation only is authorized. Execution requires BOTH (A) the owner's successful authenticated tenant-verification result and (B) explicit owner authorization to execute API deployment. Neither has been supplied. In addition, the current target no longer matches the baseline required by the corrected recovery script. No deployment or rollback script was executed or changed in this phase.

### Artifact and target inspection

Intended source remains the established tested Application pair:

- `.tmp/product-improvement-009/step04/artifacts/bin/MicroEMR.Application/debug/MicroEMR.Application.dll`, SHA-256 `3A7A1B59E8EFE11491140AD918E644D557D8CC54706AB4BA4EBD69C731409B86`.
- Matching `MicroEMR.Application.pdb`, SHA-256 `B81C82FDF9080F2BE75B08CED7ECA9948479D3179BBA28A225DA861DBF5D6E55`.
- Targets: identically named files under `D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api\bin\Debug\net10.0\`.

Current on-disk API, Application and Infrastructure DLLs report **`1.0.0+4e6f0eb74671c131fcefa241785c4aeef33a77a8`**, superseding the previously observed Step 03 disk version. All six DLL/PDB files differ from both the recorded Step 03 target hashes and the isolated Step 04 build. API process 32620 remains at the expected local executable path; disk metadata alone does not establish its loaded assembly hashes or feature behavior. This can result from an ordinary local rebuild; no code defect or unauthorized action is inferred.

| Current disk component | DLL SHA-256 | PDB SHA-256 |
| --- | --- | --- |
| API | `A5C061D28F71CB4FB4CFB30F8D392D3336ADE7938DDCED02E43565C721B3D314` | `57847BCE7984BAA9885F01252D470829D68A98AB4AFD2E3B42718633AC2E78F1` |
| Application | `9430F266BFD60E603B82D4635083BCCAF247A4312C08397C64D1FE674DFD89EB` | `9BD40A96AFC7911010BC60710961879FEA8CC11BD7956A86B6BCA7B207728276` |
| Infrastructure | `FEF76B57C5018A1C98394C4702133187EEBA59D4A6A4CCC6ED0DF0866895F04F` | `F42C28FDD8AE3A582116B68B08BF19BF3796F9808A5868CECCDA1351A280FF82` |

The retained Step 03 recovery pair remains the previously verified v3-capable release; no recovery hashes were needlessly recalculated. However, `.tmp/product-improvement-009/step02c/rollback.ps1` deliberately requires the API host/Infrastructure to match that release before restoring only Application. Those dependencies no longer match, so **that script must currently fail before stopping services or replacing files**. Changing its guards to accept the new hashes without compatibility evidence would weaken the established recovery safety assertion. G1's artifact selection remains correct; G3's applicability to today's target is now BLOCKED pending baseline reconciliation. Do not broaden rollback to the entire old API directory or overwrite the current build.

Only the API Application pair is in the proposed deployment scope. No Web/Auth deployment or database migration/grant is required. Preserve source/target configuration, development secrets, storage roots, runtime/dependency files, Web assets, signed encounter artifacts and database history. The Step 04B source/verification evidence preserves prescription-v3 reading, referral/consultation branding, authorization, audit and tenant isolation; it does not by itself certify mixing that isolated Application build with newly rebuilt host dependencies. Do not rebuild or rerun 79 tests to address a release-binding question without a specific further need.

### Owner tenant checkpoint

Use the exact supported login/API-client procedure in P1C above. Request: **GET https://localhost:7003/api/context/tenant**, `Accept: application/json`, no body, using an existing authorized client's securely managed bearer authentication. The owner must provide only HTTP status and nonsecret tenant JSON, never credentials or tokens. Normal Web login at `https://localhost:7002/Account/Login` does not automatically authenticate a direct API request; Swagger has no configured OAuth flow. If an authenticated API client/session is unavailable, identity remains deferred, not bypassed.

Required result: **200**, `tenantKey = local-dev-fresh`, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`. Successful middleware traversal establishes active catalog tenant and matching active membership, not database identity. A different tenant is a stop condition. Do not switch tenant assignments for this check.

Then verify the corresponding current tenant/database mapping using the existing trusted read-only mechanism. Existing source/configuration and dated catalog receipts are supporting evidence only. If SELECT is necessary, first inspect the actual platform mapping schema and use the established development connection and confirmed tenant UID; no provisioning procedures or grants. Report configured database, current catalog-mapped database and live request-resolved database separately. A verified mapping must identify `MicroEMR_LocalDev_Fresh`; do not claim a live database query from the tenant endpoint. No SQL was executed during preparation while authenticated identity is pending.

### Exact command sequence and applicability boundary

The existing local procedure is hash-checked file replacement followed by the API `https` launch profile. The existing deployment script `.tmp/product-improvement-009/step03-rollout/deploy.ps1` is **not an API-only command**: it uses the historical 14-file plan, both hosts and stale PIDs. It must not be executed for this task. No current approved script binds the isolated Step 04 pair to the changed API host and a safe recovery baseline. Consequently there is no safe executable deployment command to supply against today's files; inventing one that ignores this drift would misrepresent readiness.

The prepared order for a later reviewed execution is:

1. Obtain owner tenant result, independently confirm current mapping, and reconcile today's host/dependency build with the tested candidate and recovery release. Determine whether today's build already includes Step 04B before replacing it. Bind an API-only manifest/script to the exact accepted source/target/recovery/configuration hashes; no Web/Auth targets or migrations.
2. Obtain explicit owner execution authorization after presenting that concrete manifest and commands. Revalidate hashes/configuration, expected API executable and v3 recovery applicability immediately before any mutation; do not reuse stale process IDs.
3. Through the reviewed API-only script, stop only the verified API host, wait for exit, copy only the Application DLL/PDB pair, verify copied hashes and preserved configuration. On locks or partial-copy failures, stop and use only the reviewed applicable recovery procedure; no live DLL overwrites or fallback to incompatible backups.
4. The existing launch command, only after those gates and authorized copying, is:

   ```powershell
   Push-Location 'D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api'
   try { dotnet run --no-build --no-restore --launch-profile https }
   finally { Pop-Location }
   ```

   This command is documentation only and was not run. The established script mechanism uses hidden Start-Process with equivalent arguments and captured logs.
5. Verify loaded module paths/hashes, Development content root, preserved configuration and empty/error-free startup logs. Read-only commands for the separately authorized postdeployment checks are:

   ```powershell
   curl.exe --silent --show-error --fail-with-body https://localhost:7003/health/platform
   curl.exe --silent --show-error --output NUL --write-out 'Web HTTP %{http_code}' https://localhost:7002/ClinicConfiguration
   curl.exe --silent --show-error --fail-with-body https://localhost:7179/.well-known/openid-configuration
   ```

   Expect API 200, unauthenticated Web 302 and the established Auth issuer. Preserve TLS validation. Repeat authenticated tenant context/mapping verification through the qualified session; do not include bearer credentials in shell commands or receipts. Reuse existing healthy-service evidence for preparation; these commands were not executed here.

### Rollback sequence and triggers

The exact corrected script command, **currently blocked by its dependency guards and therefore NOT TO BE RUN**, is:

```powershell
& 'D:\MasterProgram_Ahmed\MicroEMR\.tmp\product-improvement-009\step02c\rollback.ps1'
```

It selects only the pinned Step 03 v3-capable Application Source/ExpectedHash pair, checks configuration/host dependencies/environment, stops only API, restores and verifies the pair, then starts API with the existing Development profile. It cannot select the pre-v3 Backup/PreviousHash release. Before any later deployment, resolve its present host/dependency mismatch and review the resulting exact recovery binding; do not run the command merely to demonstrate the expected refusal. No rollback is authorized or executed by this preparation.

Rollback triggers after a separately authorized deployment: failed API startup/health, copy/hash mismatch, regression in v3 reading or existing output, authorization/audit/isolation failure, unexpected clinical mutation or changed preserved signed bytes. Stop acceptance immediately on identity mismatch or artifact creation/repair. Recovery must preserve v3 support, configuration and all historical data; no SQL reversal. Post-recovery checks: restored loaded hashes, configuration/content root, API health, existing Web/Auth availability, authenticated expected tenant and stored-artifact baseline where available. Those checks remain planned, not passed.

### Acceptance priorities and final checkpoint

Reuse the existing 30 IDs and prerequisite matrix; all remain **NOT RUN**. After health/identity and signed-byte baseline capture, prioritize CPP, immunization and saved Open encounter logo/text-only output; encounter-only access without Patients.View; wrong-patient/unauthorized denial; preserved signed bytes and no artifact repair; tenant branding isolation. Use existing fixtures, saved values and existing accounts. Long-content, restricted-user, second-tenant or historical-baseline cases remain Deferred when prerequisites are absent. No fixture creation, clinical edit, signing, branding change or role/membership change is implicit. Temporary logo writes would need separate authorization.

Only this report changed in P2 preparation. Targeted existing scripts/artifacts/metadata were read; current target/candidate hashes were compared specifically because baseline drift was found. No Git operation, application build/test, new deployment package, service operation, binary replacement, SQL or runtime PDF scenario occurred. **BLOCKED — DEPLOYMENT PREPARATION:** reconcile the changed API/recovery baseline, receive the owner's successful authenticated tenant result and verify its mapping, then request explicit execution authorization. Neither P2 execution nor Step 05 has begun.

## Step 04C-P2A — Recovery Baseline Reconciliation

2026-11-04 (America/Toronto). **BLOCKED — RECOVERY APPLICABILITY NOT ESTABLISHED.** The trusted recovery release is intact and prescription-v3-capable. The current API host/Infrastructure differ from that release, so restoring only its Application pair is not certified by the existing evidence. No safe stale-binding correction was established; the rollback script and manifests remain unchanged. This section supersedes only the affected recovery-readiness findings, without authorizing deployment.

### Files inspected and exact comparison

Read `AGENTS.md`, this report's preflight/P1/service-recheck/P1C/P2 evidence, and the Step 03B v3 compatibility/test evidence. One initial `git status --short` returned no entries (clean working tree); no further status inspection was performed. Inspected, without executing, `.tmp/product-improvement-009/step02c/rollback.ps1` and `step03-rollout/deploy.ps1`; read `step03-rollout/plan-and-rollback.json`, `versions.json` and `pdf-verification.json`. Paths in this paragraph under `step03-rollout/` are relative to `.tmp/product-improvement-009/`.

Compared the six API targets below with their manifest Source/ExpectedHash files, and reverified all 14 existing recovery Source entries against ExpectedHash. Inspected the already identified Step 04 Application candidate pair, binary size/write-time/product-version metadata, retained rollout log metadata, and the narrowly relevant existing build-output directories for build logs. Read the API `.deps.json` in the current and retained Step 03 outputs and the corresponding `.runtimeconfig.json`; inspected Step 04 API dependency-file metadata. Also inspected the existing v3 dispatch in `src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs:62`. No application source was changed.

All target paths below are relative to `src/MicroEMR.Api/bin/Debug/net10.0/`. Recovery paths are the same filenames under `.tmp/product-improvement-009/step03/artifacts/bin/MicroEMR.Api/debug/`. P1 baseline values are the existing manifest ExpectedHash values, not Backup/PreviousHash. Each recovery hash was recalculated and equals the baseline shown.

| Relative target / established role | Recorded P1 SHA-256 | Current SHA-256 | Recovery SHA-256 | Content changed from P1 |
| --- | --- | --- | --- | --- |
| `MicroEMR.Api.dll` — API host | `DB1577D74F5D072EE8B9FAAF2C4F485A9E9E8372F9D7EC125E7376DD808738AB` | `A5C061D28F71CB4FB4CFB30F8D392D3336ADE7938DDCED02E43565C721B3D314` | `DB1577D74F5D072EE8B9FAAF2C4F485A9E9E8372F9D7EC125E7376DD808738AB` | Yes |
| `MicroEMR.Api.pdb` — host debug symbols | `BBF906371196A0D7C0D127B18BB92CA3E8B492E7B9F5850F83FD74C23B2FAEC9` | `57847BCE7984BAA9885F01252D470829D68A98AB4AFD2E3B42718633AC2E78F1` | `BBF906371196A0D7C0D127B18BB92CA3E8B492E7B9F5850F83FD74C23B2FAEC9` | Yes |
| `MicroEMR.Application.dll` — shared business/output logic | `258AEF1BD0639C6ED24887760691100F62C891FB1F9E8D03D09C57B14BF8C5D5` | `9430F266BFD60E603B82D4635083BCCAF247A4312C08397C64D1FE674DFD89EB` | `258AEF1BD0639C6ED24887760691100F62C891FB1F9E8D03D09C57B14BF8C5D5` | Yes |
| `MicroEMR.Application.pdb` — Application debug symbols | `566DA728C31AD8931F41067A71F6674F8ACEE7C9A27A8D1B121B114A1F80363B` | `9BD40A96AFC7911010BC60710961879FEA8CC11BD7956A86B6BCA7B207728276` | `566DA728C31AD8931F41067A71F6674F8ACEE7C9A27A8D1B121B114A1F80363B` | Yes |
| `MicroEMR.Infrastructure.dll` — shared data/rendering infrastructure | `2E6B390C5E75B3B1659E9965989D29A4CEE708131AFF39F138B15C0AA41C5F0C` | `FEF76B57C5018A1C98394C4702133187EEBA59D4A6A4CCC6ED0DF0866895F04F` | `2E6B390C5E75B3B1659E9965989D29A4CEE708131AFF39F138B15C0AA41C5F0C` | Yes |
| `MicroEMR.Infrastructure.pdb` — Infrastructure debug symbols | `6E696AA0C91589B1CF7FC25FBC933BE1C51CBBF3EA4C30BA0C9382B0D81480C8` | `F42C28FDD8AE3A582116B68B08BF19BF3796F9808A5868CECCDA1351A280FF82` | `6E696AA0C91589B1CF7FC25FBC933BE1C51CBBF3EA4C30BA0C9382B0D81480C8` | Yes |

These are exactly the six changed API files established by the original manifest comparison; all six current hashes still equal P2. This is not an assertion that no other directory files changed. No whole-directory hash inventory was taken.

The intended Step 04B artifact remains `.tmp/product-improvement-009/step04/artifacts/bin/MicroEMR.Application/debug/`:

| Candidate | Recorded P2 SHA-256 | Recalculated SHA-256 | Relation to current/recovery |
| --- | --- | --- | --- |
| `MicroEMR.Application.dll` | `3A7A1B59E8EFE11491140AD918E644D557D8CC54706AB4BA4EBD69C731409B86` | `3A7A1B59E8EFE11491140AD918E644D557D8CC54706AB4BA4EBD69C731409B86` | Different from both |
| `MicroEMR.Application.pdb` | `B81C82FDF9080F2BE75B08CED7ECA9948479D3179BBA28A225DA861DBF5D6E55` | `B81C82FDF9080F2BE75B08CED7ECA9948479D3179BBA28A225DA861DBF5D6E55` | Different from both |

### Supported explanation and coverage limits

The mismatch is a changed application/dependency baseline, not a wrong API path or selection of the pre-v3 recovery pair. Current API/Application/Infrastructure DLL product versions are `1.0.0+4e6f0eb74671c131fcefa241785c4aeef33a77a8`; P1 recorded `1.0.0+fea56d66b887d73562dd303ea10a9c991a11211b`. Current last-write UTC times are November 3, 15:12:06 (Application), 15:12:09 (Infrastructure), and 15:16:13 (API); their PDB times occur in the same respective seconds. The candidate pair retains November 1, 19:16:18–19 timestamps and the older product-version suffix. These metadata support different build outputs, but do not prove who produced/copied them, which command ran, or exact compiled source/functional changes. **Cause: UNKNOWN.** A local rebuild is plausible, not established. The narrowly searched existing build-output/evidence locations yielded no matching build log/binlog; retained Step 03 startup logs are dated November 1 and cannot attribute the November 3 changes. No broad log search or new build was performed.

Current API `MicroEMR.Api.deps.json` and `MicroEMR.Api.runtimeconfig.json` are text-identical to their retained Step 03 counterparts (26,670 and 488 bytes respectively). No declared dependency/runtime configuration difference was found there. These files are outside the 14-file trusted hash inventory: their text equality is supporting evidence, not an extension of that historical SHA-256 manifest. Third-party DLLs, the API executable/apphost, other runtime files and other files outside the manifest have no established P1 hash baseline here and were not hashed. Their byte-level integrity/change status remains unverified. Matching dependency declarations and assembly version text do not prove cross-build binary compatibility.

### Recovery compatibility and binding decision

**Recovery artifact integrity PASS: 14/14 Source hashes match the existing trusted manifest.** The Step 03 `versions.json` binds the retained API/Application/Infrastructure hashes to the previously deployed release, and `pdf-verification.json` records successful signed/unsigned branded and restored prescription output. Reuse P1's v3 release qualification and Step 03B's tests; current source still explicitly dispatches version 3 to `PrescriptionBrandingSnapshot.Compose`. Hash identity with that qualified release establishes retained recovery v3 support; source inspection or product-version text alone would not establish it. Pre-v3 `previous-artifacts` files remain excluded.

The existing recovery set is consistent with the historically qualified Step 03 release. **Its Application pair mixed with today's API host/Infrastructure is not established as a consistent set.** Both supporting DLLs and PDBs now differ. There is insufficient evidence to determine that Application-only restoration needs no additional dependency restoration. Returning to the known host/Infrastructure baseline would involve additional replacements outside the approved Application-only boundary, and full-directory recovery is neither authorized nor certified by this manifest. No such recovery is proposed or prepared.

Invocation remains the exact P2 command for `.tmp/product-improvement-009/step02c/rollback.ps1`, **BLOCKED / NOT TO BE RUN**. It still reads `step03-rollout/plan-and-rollback.json`, pins the two v3 Application Source hashes, validates the exact local API target/host and Development profile, and checks configuration/dependency hashes before its first `Stop-Process`. Static inspection establishes that today's API host/Infrastructure mismatches trigger its terminating dependency guard before service operations/copies. This is not a new executed rejection. No invocation, dot-sourcing, copy/restore command or service operation occurred.

The expectation is stale relative to disk, but replacing expected hashes with today's values would assert unsupported compatibility. The manifest is intact for its original 14-file scope, but is not a complete certification of today's runtime dependencies or a new mixed recovery set. **Leave script and manifest unchanged.** Strict SHA-256 checks, exact target paths, v3 selection and fail-closed behavior are preserved. No new dry-run/execution mechanism, recovery artifact or deployment package was created.

### Affected gates and remaining boundary

| Affected gate | P2A result | Evidence / limitation |
| --- | --- | --- |
| Current API baseline identified | PASS within existing inventory | Six exact current hashes equal P2; loaded assemblies and unmanifested files unverified |
| Recovery artifact integrity | PASS | All 14 Source/ExpectedHash comparisons match |
| G2 Prescription-v3 compatibility | PASS for retained release | Identical to historically qualified v3 recovery bytes; no mixed-set qualification |
| Dependency consistency | BLOCKED for current-target recovery | Old Application with current host/Infrastructure not established compatible |
| G1 Rollback target binding | PASS for path/artifact selection; BLOCKED for current applicability | Exact API-only v3 Source pair; required dependency baseline differs |
| G3 Fail-closed protection | PASS by static inspection; recovery applicability BLOCKED | Existing dependency guards reject drift before mutation; script not executed |
| Deployment/recovery command boundaries | PASS as restrictions; executable rollout readiness BLOCKED | Existing both-host deploy script unsuitable; Application-only recovery cannot be widened here |

Retain all unaffected prior results: Step 04B locally verified, 79 focused tests/build and nine PDFs/51 images already reviewed; API/Web/Auth prior health results reused without new probes. G7 authenticated tenant identity and current database mapping remain **DEFERRED**. G12 owner deployment authorization remains **NOT GRANTED / BLOCKED**. All 30 runtime scenarios remain NOT RUN and existing fixture/historical qualifications remain unchanged. **P2 is not ready for deployment.** Closure needs evidence certifying the exact current host/dependency and candidate/recovery combination, or separately scoped review of a complete consistent recovery boundary; do not accept new hashes without that evidence. Tenant/mapping verification and explicit execution authorization remain separate mandatory gates.

Only this report changed in P2A. Checks were limited to the one Git status, targeted read-only evidence/source/metadata inspection, existing-manifest/candidate SHA-256 comparisons and static script review. No builds/tests were rerun, no deployment/publish/rollback was executed, no service was started/stopped/restarted, and no binaries, configuration, database, grants, migrations, clinical records, snapshots, historical PDFs or branding were modified. No recovery artifacts were created/replaced and no remote Git operation or commit occurred.

### P2A owner-requested follow-up — dependency equivalence and safe binding

2026-11-04 (America/Toronto). The owner requested safe recovery preparation and an explanation of the recurring blockage. **The current dependency-binding blocker is now resolved within static validation limits.** This follow-up supersedes the preceding finding that compatibility evidence was insufficient. It does not authorize or claim rollback execution, deployment, service changes or live acceptance.

The earlier comparison stopped at different file hashes. Additional read-only inspection now establishes why accepting these particular dependency bytes is justified. Added `.tmp/product-improvement-009/step04/inspect-recovery-metadata.fsx`, using the existing F# metadata-inspection pattern. Executed it with `dotnet fsi --exec`; it reads existing DLL/PDB files without loading application assemblies, compiling the application or running application tests. Its metadata comparisons fail on unexpected differences. It is an inspection utility, not a recovery artifact or execution mechanism.

| Static comparison | Result |
| --- | --- |
| Recorded non-generated API source checksums, Step 03 versus current | 79/79 identical |
| Recorded non-generated Infrastructure source checksums, Step 03 versus current | 93/93 identical |
| Recorded non-generated Application source checksums, Step 04 candidate versus current | 276/276 identical |
| Application source checksums, Step 03 versus current | Exactly the four Step 04B files differ: `ClinicBrandingService.cs`, `ClinicalPdfPreviewService.cs`, `CppPrintService.cs`, `PatientImmunizationSummaryService.cs` |
| API method bodies, local signatures, stack/init flags and exception regions | 2,003/2,003 identical to Step 03 |
| Infrastructure method bodies, local signatures, stack/init flags and exception regions | 2,027/2,027 identical to Step 03 |
| API/Infrastructure full assembly metadata | Identical after the bounded normalization described below; zero remaining differences |
| Initialized field data | Identical; Infrastructure fields contain 8 and 5 bytes; API has none |
| API/Infrastructure DLL/PDB association | CodeView GUID and stamp match the portable PDB identity for both current and retained pairs |

Metadata normalization removes only the module MVID, the two known informational commit suffixes, the path-derived 64-hex component of compiler-generated file-local type names, and FieldRVA layout addresses. It retains the remainder of the metadata, including assembly references, signatures, type visibility, custom attributes and token relationships. Field contents are independently compared using their declared sizes, rather than ignored with their addresses. Method IL and exception/local information are compared without normalization. This establishes static code/contract equivalence of the changed host/Infrastructure dependencies to the qualified release; it does not claim a runtime rollback test or byte identity. The full metadata comparison also covers references to Core and third-party assemblies, but does not newly certify the bytes of those external files.

This is stronger evidence than matching version strings or timestamps. The differing hashes do not indicate a source regression in those two dependencies: recorded application source and executable method content match, with build identity/generated naming/layout differences accounted for. The exact process/person/command that produced or copied the current outputs remains UNKNOWN. The current Application's recorded source matches the tested Step 04B source; this suggests the normal development output already contains those changes, but is not a claim of verified loaded assemblies or completed deployment acceptance.

Changed only the dependency binding in the existing ignored `.tmp/product-improvement-009/step02c/rollback.ps1`. It now pins the four current API/Infrastructure DLL/PDB hashes shown in the P2A comparison table, supported by the equivalence evidence above. It requires exactly four dependency manifest entries, each bound once to its exact API filename, and checks all four exact hashes before mutation. It accepts neither arbitrary rebuilt files nor independently mixed historical/current dependency pairs. The original manifest is unchanged. Recovery still selects only the same pinned Step 03 Application Source/ExpectedHash pair, retaining prescription-v3 support; no old Backup/PreviousHash pair, additional dependency restore or whole-directory restore was introduced. Existing configuration, Development profile/environment, process-path and post-copy hash checks remain in place.

Focused validation: PowerShell AST parse **0 errors**; independently extracted the literal dependency hash table without invoking the script and verified **4/4** pinned targets. Reverified **14/14** recovery Source hashes and **5/5** recorded API configuration hashes. All six current target hashes still equal the P2/P2A values above. Reviewed exact target selection and guard ordering: missing/duplicate/extra dependency entries and missing files or unexpected hashes terminate before `Stop-Process` or `Copy-Item`. Any later byte change fails the exact-hash comparison. Failure behavior was reviewed statically; no mutation command or script invocation/dot-sourcing was used. An initial standalone validation helper used an incorrect AST traversal and did not validate the hash table; the corrected helper used the literal HashtableAst, required exactly four entries and passed. This helper error did not invoke or alter rollback behavior.

Affected gate updates: **dependency consistency PASS for the bounded Application-only static recovery**, **G1 current-target recovery binding PASS**, and **G3 static validation/fail-closed checks PASS**. Recovery integrity/v3 compatibility remain PASS. These do not certify operational rollback or authorize execution. There is still no reviewed executable API-only deployment command for this changed target; the historical both-host deploy script remains unsuitable. G7 authenticated tenant identity and current database mapping remain DEFERRED; G12 deployment authorization remains NOT GRANTED. All runtime scenarios remain NOT RUN. **P2 deployment readiness remains BLOCKED by those separate gates, rather than by the now-reconciled dependency binding.**

Why the blockage recurs: the API runs from `src/MicroEMR.Api/bin/Debug/net10.0/`, which is also the ordinary development build output. A subsequent IDE/CLI build can replace those files while the recovery manifest still describes an earlier exact build. Changes in embedded commit identity and generated paths can change hashes even when executable methods remain identical. Starting the apps does not repair that mismatch. Future unexpected hashes must continue to fail closed. For the present baseline, avoid rebuilding/replacing that output between qualification and a separately authorized operation. A lasting separation of normal build output from a dedicated deployment directory would require a separately scoped change to launch/deployment paths and a complete validated release manifest; that change was not made here. No repeated test run or automatic acceptance of new hashes is prescribed.

Follow-up files changed: this report, the existing ignored rollback script, and the new ignored metadata-inspection utility. No recovery artifacts, application binaries/source, manifests, configuration, databases, clinical records, historical PDFs or branding changed. No application builds/tests, deployments, rollback execution, service operations, remote Git operations or commits occurred. The apps need not be started/restarted for this static recovery preparation.

## Step 04C-P2B — Simplified Development Deployment Readiness

2026-11-04 (America/Toronto). **Recovery blocker: RESOLVED. Development hash policy: SIMPLIFIED. Tenant/database verification: DEFERRED. API deployment: NOT EXECUTED.** This section establishes the owner's current local-development policy; earlier evidence is retained unchanged.

Normal Visual Studio/dotnet builds may overwrite development output. Exact SHA-256 matches against that changing output are **no longer routine development deployment gates**; expected build drift alone must not reopen the resolved P2A blocker. No further hash reconciliation, separate deployment folder or release-manifest infrastructure is required for this task. This supersedes P2A's recommendation to freeze development output between qualification and deployment. The existing rollback script retains its strict checks unchanged. If rollback becomes necessary, assess its applicability to the then-current files before any execution; never bypass a refusal or overwrite unexpected files. Preserve the trusted v3-compatible recovery artifacts.

Reuse completed evidence: Step 04B's three branded PDF improvements (CPP, immunization summary and saved Open encounter preview) are implemented; 79 focused tests and the affected build passed, with nine synthetic PDFs/51 images reviewed. P2A confirmed the current Application's recorded source matches the tested Step 04B candidate and verified the available recovery release's integrity and prescription-v3 support. API/Web/Auth were previously healthy; no new health or loaded-version claim is made. Scope remains **API only**; Web/Auth deployment and database migrations are unnecessary. No clinical changes are needed. Prescription-v3 reading, authorization, tenant isolation and audit protections must remain intact, and historical signed PDF bytes must remain unchanged. Existing evidence is reused without rebuilding, rerunning tests, rehashing or re-executing inspection utilities.

### Minimum owner verification

1. In an existing authorized API client, using its securely managed bearer authentication, send **GET https://localhost:7003/api/context/tenant** with `Accept: application/json` and no body. Require HTTP **200**, `tenantKey = local-dev-fresh` and `tenantUid = f94c6ecd-4417-483e-8a94-0507cf8384c2`. The other actual response field is `displayName`; the controller exposes `TenantUid`, `TenantKey`, `DisplayName` through the default controller JSON configuration. A successful request traverses middleware that validates the active tenant and matching Active subject membership; there is no membership-status or database field in this response. Web cookie sign-in alone does not authenticate a direct API bearer request. If no authenticated API client is available, leave this deferred; do not extract credentials or invent a login flow. A different tenant or 403 is a stop condition; 401/503 leaves verification incomplete.
2. Through the owner's existing trusted development SQL connection, run the following read-only query in the configured **platform database**, after confirming the tenant above. Names are verified from `db/platform/001_create_platform_database.sql` and `002_platform_stored_procedures.sql`; `SqlTenantDatabaseResolver` reads the same assignment through `dbo.TenantDatabase_GetByTenantUid`. Require one Active assignment to `MicroEMR_LocalDev_Fresh`. The recorded server key/secret reference are `local-sql` / `development:MicroEMR_LocalDev_Fresh`; a different mapping requires investigation before deployment, without changing it.

   ```sql
   SELECT TenantUid, DatabaseServerKey, DatabaseName, SecretReference, DatabaseStatus
   FROM dbo.TenantDatabase
   WHERE TenantUid = 'f94c6ecd-4417-483e-8a94-0507cf8384c2';
   ```

3. Privately confirm that the existing development secret referenced by that assignment resolves to the assigned database; do not share the connection string. In that mapped **development clinical database**, run this read-only identity query. Require database name `MicroEMR_LocalDev_Fresh` and exactly one identity row with the expected UID and key. The identity table/columns are verified in `db/tenant-clinical/migrations/0000-tenant-metadata.sql`; the application connection factory also enforces assignment/database-name and tenant-identity consistency. These manual observations establish the mapping and directly connected database identity, not a new live request-resolved database claim from the tenant endpoint.

   ```sql
   SELECT DB_NAME() AS ConnectedDatabase, TenantUid, TenantKey
   FROM dbo.TenantDatabaseIdentity;
   ```

Return only the HTTP status, nonsecret tenant JSON and nonsecret mapping/identity results. No passwords, tokens, cookies, connection strings, permission changes, tenant reassignment or grants are needed. **No owner verification results have been supplied: authenticated tenant, membership and current database mapping remain DEFERRED.** No HTTP or SQL request was made in P2B.

### Practical acceptance and authorization checkpoint

After the owner's identity/mapping evidence and separate explicit API deployment authorization, retain the existing 30-case matrix and prioritize: CPP/immunization branding and patient/content correctness; saved Open encounter preview including encounter-only access; unauthorized/wrong-patient denial and tenant branding isolation; unchanged encounter state and historical signed PDF bytes with no repair/regeneration. Use existing saved records, accounts and logo/no-logo configurations. Long-content, restricted-user, second-tenant and readable historical-artifact cases remain deferred where suitable fixtures are unavailable. No fixture creation, signing, branding edit or permission change is implied. All runtime cases remain **NOT RUN**.

**Remaining owner actions:** supply the manual verification evidence above, then explicitly authorize the controlled API-only deployment. No deployment or service command is executed or newly approved by this report. Readiness is awaiting these practical identity/authorization prerequisites; normal development hash drift is not an additional recurring gate. Step 05 has not begun.

Only this report changed in P2B. Checks: targeted P2A evidence review and read-only verification of endpoint fields, membership validation, mapping schema/resolver and database identity validation; reviewed the appended section for preservation of prior content and restrictions. No Git operations, hash calculations, health probes, builds/tests, rollback/script execution, deployment/publish, service operations, application-source edits, configuration/database/clinical/branding changes or recovery-artifact modifications occurred. **STOP at the owner verification and deployment-authorization checkpoint.**

## Step 04C-P3 — Controlled Development API Deployment

2026-11-04 (America/Toronto). **BLOCKED — PREDEPLOYMENT.** The owner authorized local development API-only deployment. No deployment occurred because the required actual platform schema/current mapping verification could not be completed through the available development connection. This result supersedes earlier statements that execution authorization was absent; the prior report content and owner edits are preserved.

### Tenant mapping and required gate

Owner-provided authenticated Postman evidence: `GET https://localhost:7003/api/context/tenant` returned tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`, key `local-dev-fresh`, display name `Local Development Clinic Fresh`. Authentication was not repeated and no credentials were requested.

The exact SELECT recorded in P2B was inspected:

```sql
SELECT TenantUid, DatabaseServerKey, DatabaseName, SecretReference, DatabaseStatus
FROM dbo.TenantDatabase
WHERE TenantUid = 'f94c6ecd-4417-483e-8a94-0507cf8384c2';
```

`db/platform/001_create_platform_database.sql` defines those columns in that order and `002_platform_stored_procedures.sql` / `SqlTenantDatabaseResolver` use the same named mapping. Thus the owner's reported `local-sql` value denotes **DatabaseServerKey**, not TenantKey. The supplied row associates the expected UID with `MicroEMR_LocalDev_Fresh`, secret reference `development:MicroEMR_LocalDev_Fresh`, status `Active`. This is consistent owner-provided mapping evidence; it is not a newly executed successful query by this run.

The existing API development connection was checked privately against the established platform server/database allowlist before connecting; no connection string or credentials were printed. Live `sys.columns` inspection could not confirm the five mapping columns through this principal. A subsequent attempt of the exact parameterized SELECT above failed with **SQL error 229 (permission denied)**. This distinguishes an access limitation from a demonstrated schema or mapping inconsistency. No alternative administrative credential was obtained, no permissions were changed, and the later clinical identity SELECT was not reached. **Actual live platform schema/current mapping and directly connected clinical identity remain unverified by this run.** Deployment stopped at this mandatory gate; owner evidence is retained without inventing column meanings or claiming live SQL verification.

### Target, actions and health

- Intended target: existing local `src/MicroEMR.Api/bin/Debug/net10.0/`, content root `src/MicroEMR.Api`, `https` profile with `ASPNETCORE_ENVIRONMENT=Development`, HTTPS 7003 / HTTP 5245. Running API process 25124 resolved to that repository's API executable. Web 20824 and Auth 16368 likewise resolved to their local repository executable paths. No production target was used.
- Step 04B opt-ins are present in the four previously identified Application files. Reused the established 79 passing tests, affected build and nine PDFs/51 images, plus P2A's current Application/source equivalence and recovery qualification. No tests or inspection utilities were rerun. API-only scope remains appropriate; no Web/Auth deployment, migration, SQL grant, source/configuration fix or new manifest is needed or performed.
- Trusted recovery Application DLL/PDB and existing guarded rollback script remain available. Prescription-v3 compatibility is supported by retained Step 03/P2A evidence. Recovery artifacts and guards were not changed or rehashed. No rollback was needed; applicability would require validation before any actual recovery execution.
- **Deployment actions: none. Build/publish: not required or executed because predeployment stopped. API restart: not performed.** No binary copies, service stops/starts, Web/Auth operations, configuration changes, database writes, clinical writes, branding changes or historical artifact operations occurred.
- Fresh read-only predeployment probes with TLS validation: API `/health/platform` **200**; Web `/ClinicConfiguration` **302**; Auth `/.well-known/openid-configuration` **200**. These prove current availability, not a new deployment or PDF acceptance result.

Initial sandbox SQL access failed with error 10013 and curl TLS credential acquisition failed. Retrying the authorized read-only probes outside the sandbox succeeded for HTTP and reached SQL, exposing the schema visibility/SELECT permission limitation above. CIM process enumeration was denied; ordinary Get-Process supplied executable paths. A temporary F# helper initially had an unresolved relative assembly reference; it was corrected to the established absolute dependency path before SQL execution. None of these diagnostic attempts deployed software or altered database state.

### Acceptance and preservation

**Existing 30-scenario runtime matrix: PASS 0 / FAIL 0 / DEFERRED 30; executed 0.** All 30 remain NOT RUN because the predeployment gate stopped execution. CPP and immunization PDF generation, saved Open preview, logo/text-only rendering, referral/consultation runtime compatibility, permission/tenant isolation and long-content cases have no new runtime acceptance evidence. Existing qualified authenticated sessions/fixtures would still be required; none were created or modified.

Prescription v3: **prior local/recovery evidence retained; deployed-runtime check DEFERRED**. Historical signed PDFs: **no files accessed or modified; before/after byte preservation DEFERRED**, because no verified signed-encounter baseline was captured. Absence of file operations is not a passing byte-comparison result. The three health observations above are separate predeployment checks and are not counted among the 30 PDF acceptance scenarios.

Remaining blocker: complete the required live platform schema/mapping verification through an existing authorized development administrative connection, and establish the mapped clinical identity as required by P2B. Do not grant the API principal extra access or request owner credentials. Then resume within the existing explicit API-only authorization after the mandatory gates pass; no deployment approval was revoked by this stop. No Step 05 work began.

Files changed in P3: only this tracked report, appended without replacing prior content; temporary read-only diagnostic helper `.tmp/product-improvement-009/step04/p3-mapping-readonly.fsx` was created. One initial Git status showed this report already modified; those existing edits were preserved. No other Git command, commit or remote operation was performed. Checks: targeted source/schema/procedure/resolver/profile and recovery-availability inspection, development SQL read attempts, local process paths, three successful HTTP probes, and append-only report review. **Final classification: BLOCKED — PREDEPLOYMENT.**

## Step 04C-P3A — Complete Controlled Development API Deployment

2026-11-04 (America/Toronto). **DEPLOYED — PARTIALLY VERIFIED.** Local development API-only deployment completed. This section supersedes P3's permission-based deployment block. Owner-authenticated tenant and administrative catalog evidence are accepted as instructed; the restricted API principal's expected SQL error 229 is retained as a security qualification, not a tenant-mapping failure. No administrative query was repeated and no credentials or additional SQL permissions were requested.

### Accepted identity and deployment scope

Accepted HTTP 200 tenant evidence: UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`, key `local-dev-fresh`, display name `Local Development Clinic Fresh`. Accepted administrative mapping: `DatabaseServerKey = local-sql`, database `MicroEMR_LocalDev_Fresh`, secret reference `development:MicroEMR_LocalDev_Fresh`, status `Active`. These establish authenticated tenant identity and catalog assignment; no new direct clinical identity query or authenticated postdeployment tenant request is claimed. No contradictory evidence appeared.

Reused Step 04B's 79 passing focused tests, affected build, nine PDFs/51 images and P2A's bounded dependency compatibility evidence. No tests, build, publish, binary hash reconciliation, recovery-inspection utility or SQL investigation was repeated. No migration or grant was needed. Compatible recovery DLL/PDB remain available; recovery artifacts and rollback guards were preserved and rollback was not invoked.

### Actual deployment and restart

Used the existing local development copy/restart procedure, narrowed to the two tested Step 04B Application files. Executed temporary helper `.tmp/product-improvement-009/step04/p3a-deploy.ps1`; did not invoke the historical both-host deployment script or create new release-manifest infrastructure.

- Source: `.tmp/product-improvement-009/step04/artifacts/bin/MicroEMR.Application/debug/MicroEMR.Application.dll` and matching PDB.
- Destination: `src/MicroEMR.Api/bin/Debug/net10.0/`, with API content root `src/MicroEMR.Api`. Copied only that DLL/PDB pair. Immediate byte comparisons confirmed successful copies; no recurring SHA-256 gate was introduced.
- Stopped verified local API PID **25124**, then launched `dotnet run --no-build --no-restore --launch-profile https` in the API content root, hidden with captured logs. Launcher PID **12616**, restarted API PID **15124**. Startup log confirms **Development**, HTTPS **7003**, HTTP **5245**, and the intended repository content root. Standard error was empty at inspection. Loaded Application/Infrastructure module paths resolve to the API output directory.
- Five existing API source/output appsettings and launch-settings files remained byte-identical. Authentication, tenant secrets and storage configuration were not edited. Web PID **20824** and Auth PID **16368** remained running at their original repository paths; neither was deployed or restarted.

The initial script load was blocked by the host execution policy before mutation. It ran with a process-scoped `-ExecutionPolicy Bypass`; no persistent policy changed. The first execution stopped the API but exited before copying due to a transient process-exit check. A subsequent process inspection confirmed API exit and unchanged Web/Auth. Corrected the temporary helper to wait for process exit and handle the already-stopped API, then completed the same authorized two-file deployment/restart. This was a deployment-helper correction, not an application or recovery change. The completed receipt records the initial stopped PID separately from the resumed execution.

### Focused runtime results

All six supplemental smoke checks **PASS**, with TLS validation retained:

| Observation | Actual result |
| --- | --- |
| API `GET /health/platform` | HTTP **200** |
| Web `GET /ClinicConfiguration` | HTTP **302** |
| Auth `GET /.well-known/openid-configuration` | HTTP **200** |
| Anonymous CPP print request | HTTP **401** |
| Anonymous immunization summary PDF request | HTTP **401** |
| Anonymous saved-encounter preview request | HTTP **401** |

Anonymous denial requests used the known development patient candidate for CPP/immunization; preview used an empty encounter GUID and was denied before authenticated controller execution. They prove anonymous authentication enforcement, not role-specific authorization, tenant isolation, valid-record eligibility or successful PDF generation. Only summary/print/preview routes were called; no clinical mutation, signing, finalization, artifact regeneration or branding operation was invoked.

**Counts: supplemental smoke PASS 6 / FAIL 0; original 30-scenario acceptance matrix PASS 0 / FAIL 0 / DEFERRED 30.** These counts describe different scopes; the six smoke checks do not turn any full matrix scenario into a pass. Prior local automated tests are not counted as runtime acceptance.

Authenticated CPP PDF rendering, immunization PDF rendering, saved Open preview, visual branding/content checks and referral/consultation runtime behavior are **DEFERRED**: no reusable authenticated API client/browser session was available to this execution. The retained Step 03 browser receipt records its session closed on November 1; the current unauthenticated Web route redirects to login. The owner's authenticated Postman identity evidence is accepted but supplies no client session to this process. No login was invented, credentials requested/extracted, or fixture/account/permission created. Restricted-user and second-tenant cases additionally lack qualified existing sessions; long-content, logo and saved Open cases retain their original fixture qualifications. Successful PDF generation is not claimed.

Prescription v3 compatibility: **retained local/source and qualified recovery evidence preserved**; the selected Step 04B Application includes the existing v3 reader by the established verification. Postdeployment authenticated prescription reading remains **DEFERRED**. Referral/consultation branding remains covered by prior source/compatibility evidence; no runtime rendering pass is claimed.

Historical signed PDF preservation: **no historical files accessed, replaced, repaired or regenerated by deployment/acceptance**. No verified signed-encounter before/after baseline was available, so byte-preservation acceptance remains **DEFERRED**. Unchanged stored bytes were not inferred from local synthetic tests or absence of clinical operations.

### Remaining qualifications and files

Deployment has no remaining permission-based blocker. Remaining work is authenticated read-only acceptance with existing qualified sessions, saved fixtures and a verified historical signed-artifact baseline. No clinical records, branding, database, configuration, host/Infrastructure binaries or Web/Auth deployment files changed. No build/tests, SQL, grants, migrations, rollback, Git operation, commit or push occurred. Step 05 was not begun.

Only this tracked report was appended, preserving earlier owner edits. Temporary deployment helper and receipts/logs are under `.tmp/product-improvement-009/step04/`: `p3a-deploy.ps1`, `p3a-deployment.json`, `p3a-acceptance.json`, `p3a-api-startup.log`, `p3a-api-stderr.log`. Runtime file changes are limited to the API Application DLL/PDB pair. Validation: deployment script syntax review, exact process/profile paths, immediate copied-byte and five configuration-byte comparisons, restart logs/loaded module paths, unchanged Web/Auth processes, six HTTP observations, and append-only report review. **Final classification: DEPLOYED — PARTIALLY VERIFIED.**
